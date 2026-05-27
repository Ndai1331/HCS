using System;
using System.IO;
using HC.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using HC.PushNotificationWorker.Services;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace HC.PushNotificationWorker;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpDistributedLockingModule),
    typeof(HCEntityFrameworkCoreModule)
)]
public class HCPushNotificationWorkerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = ResolveWorkerConfiguration(context.Services);

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "HC:"; });

        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Inboxes.Configure(config =>
            {
                config.UseDbContext<HCDbContext>();
            });
        });

        ConfigureDistributedLocking(context, configuration);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = context.ServiceProvider.GetRequiredService<ILogger<HCPushNotificationWorkerModule>>();
        var path = configuration["Firebase:CredentialPath"];
        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogWarning(
                "Firebase:CredentialPath is not set. FCM delivery is disabled until a service account JSON path is configured.");
            return;
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        if (!File.Exists(path))
        {
            logger.LogError("Firebase credential file not found at {Path}.", path);
            return;
        }

        var metadata = FirebaseCredentialHelper.TryReadCredentialMetadata(path);
        try
        {
            if (FirebaseApp.DefaultInstance != null)
            {
                return;
            }

            var credential = FirebaseCredentialHelper.LoadCredential(path);
            FirebaseCredentialHelper.ValidateAccessTokenAsync(credential).GetAwaiter().GetResult();

            FirebaseApp.Create(new AppOptions { Credential = credential });
            logger.LogInformation(
                "Firebase Admin SDK initialized and OAuth token validated. Path={Path}. {Metadata}",
                path,
                metadata);
        }
        catch (TokenResponseException ex) when (FirebaseCredentialHelper.IsCredentialError(ex))
        {
            logger.LogError(
                ex,
                "Firebase service account rejected by Google ({Error}: {Description}). "
                + "Regenerate the JSON key in GCP (project hanh-chinh-so), replace the mounted file, and restart. "
                + "Path={Path}. {Metadata}",
                ex.Error?.Error,
                ex.Error?.ErrorDescription,
                path,
                metadata);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Firebase Admin SDK from {Path}. {Metadata}", path, metadata);
        }
    }

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

    private void ConfigureDistributedLocking(
        ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        var redisConn = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConn))
        {
            throw new InvalidOperationException(
                "Redis:Configuration is missing or empty. Push worker requires Redis for distributed locks (inbox processor).");
        }

        context.Services.AddSingleton<IDistributedLockProvider>(_ =>
        {
            var connection = ConnectionMultiplexer.Connect(redisConn);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }
}
