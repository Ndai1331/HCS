# HC.PushNotificationWorker — production image
#
# Build from repository root (required for project references):
#   docker buildx build --platform linux/amd64 -f src/HC.PushNotificationWorker/Dockerfile -t <registry>/hc-pushnotificationworker:<tag> .
#
# Mount Firebase JSON at runtime:
#   Firebase__CredentialPath=/app/secrets/hanh-chinh-so-firebase.json

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
WORKDIR /src/src/HC.PushNotificationWorker
RUN dotnet restore
RUN dotnet publish -c Release -r linux-x64 -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "HC.PushNotificationWorker.dll"]
