using System;
using HC.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace HC.BackgroundJobWorker;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpBlobStoringMinioModule),
    typeof(AbpDistributedLockingModule),
    typeof(HCApplicationModule),
    typeof(HCEntityFrameworkCoreModule)
)]
public class HCBackgroundJobWorkerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = ResolveWorkerConfiguration(context.Services);

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "HC:"; });

        Configure<PermissionManagementOptions>(options =>
        {
            options.IsDynamicPermissionStoreEnabled = false;
        });

        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
        });

        ConfigureBlobStoring(context, configuration);
        ConfigureDistributedLocking(context, configuration);
    }

    /// <summary>
    /// ABP's GetConfiguration() may return HostBuilderContext.Configuration (minimal). ReplaceConfiguration
    /// registers IConfiguration as a descriptor that GetSingletonInstanceOrNull sometimes does not surface.
    /// Scan descriptors, then fall back to file-based config next to the assembly.
    /// </summary>
    private static IConfiguration ResolveWorkerConfiguration(IServiceCollection services)
    {
        static bool HasRedis(IConfiguration c) =>
            !string.IsNullOrWhiteSpace(c["Redis:Configuration"]);

        foreach (var d in services)
        {
            if (d.ServiceType != typeof(IConfiguration) || d.ImplementationInstance is not IConfiguration cfg)
            {
                continue;
            }

            if (HasRedis(cfg))
            {
                return cfg;
            }
        }

        var singleton = services.GetSingletonInstanceOrNull<IConfiguration>();
        if (singleton != null && HasRedis(singleton))
        {
            return singleton;
        }

        var abpConfig = services.GetConfiguration();
        if (HasRedis(abpConfig))
        {
            return abpConfig;
        }

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.secrets.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private void ConfigureBlobStoring(ServiceConfigurationContext context, IConfiguration configuration)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container =>
            {
                container.UseMinio(minio =>
                {
                    minio.EndPoint = configuration["MinIO:EndPoint"] ?? "minio:9000";
                    minio.AccessKey = configuration["MinIO:AccessKey"] ?? "hcsadmin";
                    minio.SecretKey = configuration["MinIO:SecretKey"] ?? "hcsadminpassword";
                    minio.BucketName = configuration["MinIO:BucketName"] ?? "hcsbucket";
                    minio.WithSSL = configuration.GetValue<bool>("MinIO:WithSSL", false);
                    minio.CreateBucketIfNotExists = configuration.GetValue<bool>("MinIO:CreateBucketIfNotExists", true);
                });
            });
        });
    }

    private void ConfigureDistributedLocking(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        var redisConn = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConn))
        {
            throw new System.InvalidOperationException(
                "Redis:Configuration is missing or empty. Background jobs require a distributed lock provider backed by Redis; " +
                "set Redis:Configuration in appsettings (and check appsettings.secrets.json / environment variables do not clear it).");
        }

        context.Services.AddSingleton<IDistributedLockProvider>(_ =>
        {
            var connection = ConnectionMultiplexer.Connect(redisConn);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }
}
