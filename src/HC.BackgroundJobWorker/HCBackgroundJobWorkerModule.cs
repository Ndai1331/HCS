using HC.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
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
        var configuration = context.Services.GetConfiguration();

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
                    minio.BucketName = configuration["MinIO:BucketName"] ?? "hcs_bucket";
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
            return;
        }

        context.Services.AddSingleton<IDistributedLockProvider>(_ =>
        {
            var connection = ConnectionMultiplexer.Connect(redisConn);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }
}
