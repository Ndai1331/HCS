# HC.PushNotificationWorker — production image (framework-dependent)
#
# Publish from repo:
#   dotnet publish -c Release -o bin/Release/net10.0/publish src/HC.PushNotificationWorker/HC.PushNotificationWorker.csproj
# Build:
#   docker build --platform linux/amd64 -f Dockerfile -t <registry>/hc-pushnotificationworker:<tag> .
# (run from src/HC.PushNotificationWorker)
#
# Mount Firebase JSON via secret and set Firebase__CredentialPath, or bake credentials via mount at runtime.
FROM mcr.microsoft.com/dotnet/runtime:10.0
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --chown=app:app bin/Release/net10.0/publish/ .
USER app
ENTRYPOINT ["dotnet", "HC.PushNotificationWorker.dll"]
