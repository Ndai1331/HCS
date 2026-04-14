using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Volo.Abp.PermissionManagement;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using HC.EntityFrameworkCore;
using HC.MultiTenancy;
using StackExchange.Redis;
using Microsoft.OpenApi.Models;
using HC.HealthChecks;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp;
using Volo.Abp.Studio;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
// Chat feature - SignalR module needs to be created
// using HC.Chat.SignalR;

namespace HC;

[DependsOn(
    typeof(HCHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpIdentityAspNetCoreModule),
    typeof(HCApplicationModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(HCEntityFrameworkCoreModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule)
    // Chat SignalR module needs to be created
    // typeof(HCChatSignalRModule)
    )]
public class HCHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        ConfigureUrls(configuration);
        ConfigureConventionalControllers();
        ConfigureAuthentication(context, configuration);
        ConfigureSwagger(context, configuration);
        ConfigureCache(configuration);
        ConfigureVirtualFileSystem(context);
        ConfigureDataProtection(context, configuration, hostingEnvironment);
        ConfigureDistributedLocking(context, configuration);
        ConfigureCors(context, configuration);
        ConfigureHealthChecks(context);
        ConfigureBlobStoring(context, configuration);

        Configure<PermissionManagementOptions>(options =>
        {
            // Forms module was removed; disable dynamic permission store to avoid
            // loading stale permission definitions from database (e.g. Volo.Forms.*).
            options.IsDynamicPermissionStoreEnabled = false;
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddHCHealthChecks();
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
        });
    }

    private void ConfigureCache(IConfiguration configuration)
    {
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "HC:";
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<HCDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HC.Domain.Shared", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<HCDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HC.Domain", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<HCApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HC.Application.Contracts", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<HCApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HC.Application", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<HCHttpApiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}src{0}HC.HttpApi", Path.DirectorySeparatorChar)));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(HCApplicationModule).Assembly);
            // Also register controllers from HCHttpApiModule assembly
            options.ConventionalControllers.Create(typeof(HCHttpApiModule).Assembly);
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                var authorityRaw = configuration["AuthServer:Authority"];
                var authority = string.IsNullOrWhiteSpace(authorityRaw) 
                    ? null 
                    : authorityRaw.TrimEnd('/'); // Remove trailing slash - issuer should not have trailing slash
                
                options.Authority = authority;
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = "HC";
                
                // Set ValidIssuer (without trailing slash) - AuthServer should create tokens with issuer without trailing slash
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.TokenValidationParameters.ValidIssuer = authority;
                }

                // Handle Kubernetes/reverse proxy scenarios
                if (configuration.GetValue<bool>("AuthServer:IsOnK8s", false))
                {
                    var metaAddressRaw = configuration["AuthServer:MetaAddress"];
                    var metaAddress = string.IsNullOrWhiteSpace(metaAddressRaw) 
                        ? null 
                        : metaAddressRaw.TrimEnd('/'); // Remove trailing slash
                    
                    if (!string.IsNullOrWhiteSpace(metaAddress))
                    {
                        // Use MetaAddress to fetch metadata (signing keys) from internal URL
                        options.MetadataAddress = metaAddress + "/.well-known/openid-configuration";
                        
                        // Accept both Authority and MetaAddress as valid issuers (both without trailing slash)
                        if (!string.IsNullOrWhiteSpace(authority))
                        {
                            options.TokenValidationParameters.ValidIssuers = new[]
                            {
                                authority,    // Without trailing slash: 'https://qldhaut.bvlevanthinh.vn'
                                metaAddress   // Internal URL without trailing slash
                            };
                        }
                        else
                        {
                            options.TokenValidationParameters.ValidIssuer = metaAddress;
                        }
                    }
                }
            });

        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = false;
        });
    }

    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["HC"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            configuration["AuthServer:MetaAddress"],
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "HC API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private void ConfigureDataProtection(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebHostEnvironment hostingEnvironment)
    {
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        var dataProtectionBuilder = context.Services.AddDataProtection().SetApplicationName("HC");
        if (!hostingEnvironment.IsDevelopment())
        {
            var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!);
            dataProtectionBuilder.PersistKeysToStackExchangeRedis(redis, "HC-Protection-Keys");
        }
    }

    private void ConfigureDistributedLocking(
    	ServiceConfigurationContext context,
    	IConfiguration configuration)
    {
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var connection = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureBlobStoring(ServiceConfigurationContext context, IConfiguration configuration)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container =>
            {
                container.UseMinio(minio =>
                {
                    // MinIO EndPoint chỉ cần hostname:port (không có http://)
                    // Protocol được xác định bởi WithSSL
                    minio.EndPoint = configuration["MinIO:EndPoint"] ?? "minio:9000";
                    minio.AccessKey = configuration["MinIO:AccessKey"] ?? "hcsadmin";
                    minio.SecretKey = configuration["MinIO:SecretKey"] ?? "hcsadminpassword";
                    minio.BucketName = configuration["MinIO:BucketName"] ?? "hcs_bucket";
                    minio.WithSSL = configuration.GetValue<bool>("MinIO:WithSSL", false);
                    minio.CreateBucketIfNotExists = configuration.GetValue<bool>("MinIO:CreateBucketIfNotExists", true);
                });
            });
        });
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseCors();
        app.UseAuthentication();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        
        // Debug: Log tenant ID for troubleshooting permission issues
        app.Use(async (httpContext, next) =>
        {
            if (MultiTenancyConsts.IsEnabled)
            {
                var currentTenant = httpContext.RequestServices.GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>();
                var logger = httpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HCHttpApiHostModule>>();
                logger.LogWarning($"[DEBUG] TenantId: {currentTenant.Id}, Name: {currentTenant.Name}, IsAvailable: {currentTenant.IsAvailable}");
            }
            await next();
        });
        
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "HC API");

            var configuration = context.GetConfiguration();
            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        
        app.UseConfiguredEndpoints();
    }
}
