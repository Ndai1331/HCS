using System;
using System.IO;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.MultiTenancy;
using HC.Blazor.Components;
using HC.Blazor.Menus;
using HC.Localization;
using HC.MultiTenancy;
using HC.Blazor.HealthChecks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components.Web.Theming.Toolbars;
using Volo.Abp.Studio;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.Routing;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.AspNetCore.Components.Server;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.Autofac;
using Volo.Abp.Mapperly;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.SettingManagement.Blazor.Server;
using Volo.Abp.FeatureManagement.Blazor.Server;
using Volo.Abp.Account.LinkUsers;
using Volo.Abp.Account.Pro.Admin.Blazor.Server;
using Volo.Abp.Account.Pro.Public.Blazor.Server;
using Volo.Abp.Account.Public.Web.Impersonation;
using Volo.Abp.Identity.Pro.Blazor;
using Volo.Abp.Identity.Pro.Blazor.Server;
using Volo.Abp.AuditLogging.Blazor.Server;
using Volo.Abp.LanguageManagement.Blazor.Server;
using Volo.FileManagement.Blazor.Server;
using Volo.Abp.TextTemplateManagement.Blazor.Server;
using Volo.Saas.Host.Blazor;
using Volo.Saas.Host.Blazor.Server;
using Volo.Abp.Gdpr.Blazor.Extensions;
using Volo.Abp.Gdpr.Blazor.Server;
// Chat feature - using HC.Chat
// Note: SignalR module needs to be created separately
using Volo.Abp.OpenIddict.Pro.Blazor.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Volo.Abp.AspNetCore.Authentication.OpenIdConnect;
using Volo.Abp.AspNetCore.Mvc.Client;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.RabbitMq;
using HC.Blazor.Components.Layout;
using HC.Blazor.Toolbars;
using Volo.Abp.AspNetCore.Components.Web.LeptonXTheme;
using Volo.Abp.AspNetCore.Components.Server.LeptonXTheme;
using Volo.Abp.AspNetCore.Components.Server.LeptonXTheme.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonX;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonX.Bundling;
using Volo.Abp.LeptonX.Shared;
using Volo.Abp.Http.Client.Web;
using Volo.Abp.Http.Client.IdentityModel.Web;
using Volo.Abp.Security.Claims;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Ui.LayoutHooks;
using Volo.Abp.AspNetCore.Components.Web.Theming.Layout;

namespace HC.Blazor;

[DependsOn(
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpAutofacModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpAspNetCoreMvcClientModule),
    typeof(AbpAspNetCoreAuthenticationOpenIdConnectModule),
    typeof(AbpHttpClientWebModule),
    typeof(AbpHttpClientIdentityModelWebModule),
    typeof(AbpAccountPublicWebImpersonationModule),
    typeof(AbpAccountAdminBlazorServerModule),
    typeof(AbpAccountPublicBlazorServerModule),
    typeof(AbpIdentityProBlazorServerModule),
    typeof(AbpAuditLoggingBlazorServerModule),
    typeof(AbpOpenIddictProBlazorServerModule),
    typeof(LanguageManagementBlazorServerModule),
    typeof(FileManagementBlazorServerModule),
    typeof(SaasHostBlazorServerModule),
    // Chat feature - SignalR module needs to be created
    // typeof(HCChatBlazorServerModule),
    // typeof(HCChatSignalRModule),
    typeof(TextTemplateManagementBlazorServerModule),
    typeof(AbpGdprBlazorServerModule),
    typeof(AbpAspNetCoreComponentsServerLeptonXThemeModule),
    typeof(AbpAspNetCoreMvcUiLeptonXThemeModule),
    typeof(HCHttpApiClientModule),
    typeof(AbpFeatureManagementBlazorServerModule),
    typeof(AbpSettingManagementBlazorServerModule),
    typeof(AbpBlobStoringMinioModule)
    )]
public class HCBlazorModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(HCResource),
                typeof(HCDomainSharedModule).Assembly,
                typeof(HCApplicationContractsModule).Assembly,
                typeof(HCBlazorModule).Assembly
            );
        });

        PreConfigure<AbpAspNetCoreComponentsWebOptions>(options =>
        {
            options.IsBlazorWebApp = true;
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        // Add services to the container.
        context.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        // Add HttpContextAccessor for SignalR authentication
        context.Services.AddHttpContextAccessor();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        ConfigureUrls(configuration);
        ConfigureToolbars();
        ConfigureBundles();
        ConfigureAuthentication(context, configuration);
        ConfigureHealthChecks(context);
        ConfigureImpersonation(context, configuration);
        ConfigureCookieConsent(context);
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureSwaggerServices(context.Services);
        ConfigureCache(configuration);
        ConfigureDataProtection(context, configuration, hostingEnvironment);
        ConfigureDistributedLocking(context, configuration);
        ConfigureBlazorise(context);
        ConfigureRouter();
        ConfigureMenu(configuration);
        ConfigureTheme();
        ConfigureAntiForgery(context, configuration);
        ConfigureBlobStoring(context, configuration);
        ConfigureSignalR(context);
        ConfigureEventHandlers(context);
        ConfigureLayoutHooks(context);
    }
    
    private void ConfigureAntiForgery(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // CRITICAL FIX: Configure AntiForgery cookie for HTTPS deployment
        // XSRF-TOKEN cookie must have Secure flag when SameSite=None
        // This fixes the warning: "The cookie 'XSRF-TOKEN' has set 'SameSite=None' and must also set 'Secure'"
        Configure<Volo.Abp.AspNetCore.Mvc.AntiForgery.AbpAntiForgeryOptions>(options =>
        {
            options.TokenCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            options.TokenCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });
    }

    
    private void ConfigureToolbars()
    {
        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new HCToolbarContributor());
        });
    }


    private void ConfigureCookieConsent(ServiceConfigurationContext context)
    {
        context.Services.AddAbpCookieConsent(options =>
        {
            options.IsEnabled = true;
            options.CookiePolicyUrl = "/CookiePolicy";
            options.PrivacyPolicyUrl = "/PrivacyPolicy";
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
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });

        Configure<AbpAccountLinkUserOptions>(options =>
        {
            options.LoginUrl = configuration["AuthServer:Authority"];
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            // MVC UI
            options.StyleBundles.Configure(
                LeptonXThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                }
            );

            // Blazor UI
            options.StyleBundles.Configure(
                BlazorLeptonXThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        
        context.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Cookies";
            options.DefaultChallengeScheme = "oidc";
        })
        .AddCookie("Cookies", options =>
        {
            // Reduce cookie expiration to reasonable time (was 365 days - too long)
            options.ExpireTimeSpan = TimeSpan.FromHours(8); // 8 hours instead of 365 days
            options.SlidingExpiration = true;
            options.IntrospectAccessToken();
            
            // Configure cookie settings for SignalR compatibility
            // SameSite=Lax allows cookies to be sent with SignalR negotiation requests
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            
            // Use Distributed Session Store to reduce cookie size
            // Instead of storing entire authentication ticket in cookie, store it in Redis
            // This reduces cookie from ~36KB to just a session ID (~100 bytes)
            if (!string.IsNullOrEmpty(configuration["Redis:Configuration"]))
            {
                var redisConnection = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]!);
                options.SessionStore = new DistributedCookieAuthenticationSessionStore(
                    redisConnection.GetDatabase(),
                    "HC:AuthSession:"
                );
            }
        })
        .AddAbpOpenIdConnect("oidc", options =>
        {
            options.Authority = configuration["AuthServer:Authority"];
            options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
            options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
            // options.ResponseType = OpenIdConnectResponseType.Code;
            // options.UsePkce = true;
            options.ClientId = configuration["AuthServer:ClientId"];
            options.ClientSecret = configuration["AuthServer:ClientSecret"];
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            // Clear default scopes and add only valid scopes
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("roles");
            options.Scope.Add("email");
            options.Scope.Add("phone");
            options.Scope.Add("HC");

            var previousOnRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
            options.Events.OnRedirectToIdentityProvider = async ctx =>
            {
                if (!string.IsNullOrEmpty(ctx.ProtocolMessage.RedirectUri) && 
                    ctx.ProtocolMessage.RedirectUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new UriBuilder(ctx.ProtocolMessage.RedirectUri)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = -1 // Remove port when using HTTPS default port
                    };
                    ctx.ProtocolMessage.RedirectUri = uri.ToString();
                }

                if (previousOnRedirectToIdentityProvider != null)
                {
                    await previousOnRedirectToIdentityProvider(ctx);
                }
            };

            var previousOnRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
            options.Events.OnRedirectToIdentityProviderForSignOut = async ctx =>
            {
                if (!string.IsNullOrEmpty(ctx.ProtocolMessage.PostLogoutRedirectUri) && 
                    ctx.ProtocolMessage.PostLogoutRedirectUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new UriBuilder(ctx.ProtocolMessage.PostLogoutRedirectUri)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = -1 // Remove port when using HTTPS default port
                    };
                    ctx.ProtocolMessage.PostLogoutRedirectUri = uri.ToString();
                }

                if (previousOnRedirectToIdentityProviderForSignOut != null)
                {
                    await previousOnRedirectToIdentityProviderForSignOut(ctx);
                }
            };
        });

        if (Convert.ToBoolean(configuration["AuthServer:IsOnK8s"]))
        {
            context.Services.Configure<OpenIdConnectOptions>("oidc", options =>
            {
                options.TokenValidationParameters.ValidIssuers = new[]
                {
                    configuration["AuthServer:MetaAddress"].EnsureEndsWith('/'), 
                    configuration["AuthServer:Authority"].EnsureEndsWith('/')
                };

                options.MetadataAddress = configuration["AuthServer:MetaAddress"].EnsureEndsWith('/') +
                                        ".well-known/openid-configuration";

                var previousOnRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = async ctx =>
                {
                    // Intercept the redirection so the browser navigates to the right URL in your host
                    ctx.ProtocolMessage.IssuerAddress = configuration["AuthServer:Authority"].EnsureEndsWith('/') + "connect/authorize";

                    // Force redirect_uri to HTTPS if it's HTTP (for reverse proxy scenarios)
                    if (!string.IsNullOrEmpty(ctx.ProtocolMessage.RedirectUri) && 
                        ctx.ProtocolMessage.RedirectUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new UriBuilder(ctx.ProtocolMessage.RedirectUri)
                        {
                            Scheme = Uri.UriSchemeHttps,
                            Port = -1 // Remove port when using HTTPS default port
                        };
                        ctx.ProtocolMessage.RedirectUri = uri.ToString();
                    }

                    if (previousOnRedirectToIdentityProvider != null)
                    {
                        await previousOnRedirectToIdentityProvider(ctx);
                    }
                };
                var previousOnRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
                options.Events.OnRedirectToIdentityProviderForSignOut = async ctx =>
                {
                    // Intercept the redirection for signout so the browser navigates to the right URL in your host
                    // ctx.ProtocolMessage.IssuerAddress = configuration["AuthServer:Authority"].EnsureEndsWith('/') + "connect/logout";
                    ctx.ProtocolMessage.IssuerAddress = configuration["AuthServer:Authority"].EnsureEndsWith('/') + "connect/endsession";

                    // Force post_logout_redirect_uri to HTTPS if it's HTTP (for reverse proxy scenarios)
                    if (!string.IsNullOrEmpty(ctx.ProtocolMessage.PostLogoutRedirectUri) && 
                        ctx.ProtocolMessage.PostLogoutRedirectUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new UriBuilder(ctx.ProtocolMessage.PostLogoutRedirectUri)
                        {
                            Scheme = Uri.UriSchemeHttps,
                            Port = -1 // Remove port when using HTTPS default port
                        };
                        ctx.ProtocolMessage.PostLogoutRedirectUri = uri.ToString();
                    }

                    if (previousOnRedirectToIdentityProviderForSignOut != null)
                    {
                        await previousOnRedirectToIdentityProviderForSignOut(ctx);
                    }
                };
            });
        }


        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            // Enable dynamic claims to load permissions from userinfo endpoint
            // This is required for permissions to be loaded after login
            options.IsDynamicClaimsEnabled = false;
        });
        
        // Configure HttpClient timeout for RemoteServices to prevent timeout errors
        // This is especially important for dynamic claims refresh
        // Increase timeout for all HttpClient instances used by ABP RemoteServices
        context.Services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120); // Increase from default 100 seconds
            });
        });
    }

    private void ConfigureImpersonation(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.Configure<SaasHostBlazorOptions>(options =>
        {
            options.EnableTenantImpersonation = true;
        });
        context.Services.Configure<AbpIdentityProBlazorOptions>(options =>
        {
            options.EnableUserImpersonation = true;
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<HCDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}HC.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<HCApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}HC.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<HCBlazorModule>(hostingEnvironment.ContentRootPath);
            });
        }
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "HC API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    private void ConfigureCache(IConfiguration configuration)
    {
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "HC:";
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

    private void ConfigureBlazorise(ServiceConfigurationContext context)
    {
        context.Services
            .AddBlazorise(options =>
            {
                // TODO (IMPORTANT): To use Blazorise, you need a license key. Get your license key directly from Blazorise, follow  the instructions at https://abp.io/faq#how-to-get-blazorise-license-key
                //options.ProductToken = "Your Product Token";
            })
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();
    }

    private void ConfigureMenu(IConfiguration configuration)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new HCMenuContributor(configuration));
        });
    }
    
    private void ConfigureTheme()
    {
        Configure<LeptonXThemeOptions>(options =>
        {
            options.DefaultStyle = LeptonXStyleNames.System;
        });

        Configure<LeptonXThemeMvcOptions>(options =>
        {
            //options.ApplicationLayout = LeptonXMvcLayouts.SideMenu;
            options.ApplicationLayout = LeptonXMvcLayouts.TopMenu;
        });

        Configure<LeptonXThemeBlazorOptions>(options =>
        {
            //options.Layout = LeptonXBlazorLayouts.TopMenu;
            options.Layout = LeptonXBlazorLayouts.TopMenu;
        });
    }

    private void ConfigureRouter()
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AppAssembly = typeof(HCBlazorModule).Assembly;
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

    private void ConfigureSignalR(ServiceConfigurationContext context)
    {
        context.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
        });
        
        // Configure SignalR to use NameIdentifier claim for user mapping
        // This ensures that Clients.User(userId.ToString()) works correctly
        context.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
        {
        });
        
        // Configure SignalR Hub options for Blazor Server with Cookie Authentication
        // In Blazor Server, SignalR automatically uses HttpContext from the browser connection
        // Cookies are sent automatically with the connection negotiation request
        context.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions<Hubs.NotificationHub>>(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

        // Configure ChatHub options
        context.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions<Hubs.ChatHub>>(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });
    }

    private void ConfigureLayoutHooks(ServiceConfigurationContext context)
    {
        Configure<AbpLayoutHookOptions>(options =>
        {
            options.Add(
                LayoutHooks.Body.Last,
                typeof(Components.NotificationToast),
                layout: StandardLayouts.Application
            );
        });
    }

    private void ConfigureEventHandlers(ServiceConfigurationContext context)
    {
        // Register chat event handlers for real-time messaging
        context.Services.AddTransient<
            Volo.Abp.EventBus.Distributed.IDistributedEventHandler<HC.Chat.Messages.ChatMessageEto>,
            HC.Blazor.EventHandlers.ChatEventHandler>();

        context.Services.AddTransient<
            Volo.Abp.EventBus.Distributed.IDistributedEventHandler<HC.Chat.Messages.ChatDeletedMessageEto>,
            HC.Blazor.EventHandlers.ChatEventHandler>();

        context.Services.AddTransient<
            Volo.Abp.EventBus.Distributed.IDistributedEventHandler<HC.Chat.Messages.ChatDeletedConversationEto>,
            HC.Blazor.EventHandlers.ChatEventHandler>();
    }

    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        // Log after application is initialized
        var logger = context.ServiceProvider.GetRequiredService<ILogger<HCBlazorModule>>();
        logger.LogInformation("[HCBlazorModule] ✅ Layout Hook configured: Hook={Hook}, Component={Component}, Layout={Layout}",
            LayoutHooks.Body.Last,
            typeof(Components.NotificationToast).FullName,
            StandardLayouts.Application);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var env = context.GetEnvironment();
        var app = context.GetApplicationBuilder();

        app.UseForwardedHeaders();
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
            app.UseHsts();
        }

        app.UseCorrelationId();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseAntiforgery();
        app.UseAuthentication();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseDynamicClaims();
        
        // Debug: Log tenant ID and SignalR requests for troubleshooting
        app.Use(async (httpContext, next) =>
        {
            var path = httpContext.Request.Path.Value ?? "";
            if (path.Contains("/notificationHub"))
            {
                var logger = httpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HCBlazorModule>>();
                var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated ?? false;
                var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
                var hasCookies = httpContext.Request.Cookies.Count > 0;
                
                logger.LogInformation("[SignalR Debug] Path={Path}, IsAuthenticated={IsAuthenticated}, UserId={UserId}, HasCookies={HasCookies}, CookieCount={CookieCount}",
                    path, isAuthenticated, userId, hasCookies, httpContext.Request.Cookies.Count);
            }
            
            if (MultiTenancyConsts.IsEnabled)
            {
                var currentTenant = httpContext.RequestServices.GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>();
                var logger = httpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HCBlazorModule>>();
                var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
                logger.LogWarning($"[DEBUG] UserId: {userId}, TenantId: {currentTenant.Id}, Name: {currentTenant.Name}, IsAvailable: {currentTenant.IsAvailable}");
            }
            await next();
        });
        
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "HC API");
        });
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(builder =>
        {
            // Map SignalR Hub - must be mapped on IEndpointRouteBuilder
            // SignalR negotiation endpoint will use authentication from cookies
            // The [Authorize] attribute on the Hub will require authentication
            // In Blazor Server, the connection is established from browser, so cookies will be sent automatically
            // Map SignalR Hub with WebSockets transport for Blazor Server
            // In Blazor Server, cookies are automatically sent with the connection negotiation
            // The [Authorize] attribute on the Hub will use cookies for authentication
            builder.MapHub<Hubs.NotificationHub>("/notificationHub", options =>
            {
                // WebSockets is the preferred transport for Blazor Server
                // Cookies from the browser will be used automatically for authentication
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                     Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            });

            // Map ChatHub for real-time messaging
            builder.MapHub<Hubs.ChatHub>("/chatHub", options =>
            {
                // WebSockets is the preferred transport for Blazor Server
                // Cookies from the browser will be used automatically for authentication
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                     Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            });

            builder.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddAdditionalAssemblies(builder.ServiceProvider.GetRequiredService<IOptions<AbpRouterOptions>>().Value.AdditionalAssemblies.ToArray());
        });
        
        // Log SignalR hub mapping
        var logger = context.GetApplicationBuilder().ApplicationServices.GetRequiredService<ILogger<HCBlazorModule>>();
        logger.LogInformation("[HCBlazorModule] SignalR Hubs mapped: /notificationHub, /chatHub");
    }
}
