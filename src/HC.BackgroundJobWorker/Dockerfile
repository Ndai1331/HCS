# HC.BackgroundJobWorker — production image (framework-dependent)
#
# 1) Publish (from repo root or this folder):
#    dotnet publish -c Release -o bin/Release/net10.0/publish
# 2) Build linux/amd64 image:
#    docker build --platform linux/amd64 -f Dockerfile -t <registry>/hc-backgroundjobworker:<tag> .
#    (run from the directory that contains this Dockerfile, i.e. src/HC.BackgroundJobWorker)
#
# If jobs require LibreOffice / fonts like the API, use the same base or extend your hc-api image
# and replace ENTRYPOINT with HC.BackgroundJobWorker.dll.
FROM mcr.microsoft.com/dotnet/runtime:10.0
# Npgsql/PostgreSQL: libgssapi_krb5 (avoids "libgssapi_krb5.so.2" load warnings when using SSL)
# PDFsharp: Liberation fonts (avoids "Helvetica" on minimal Linux; see PdfFontEnvironment / HC_FONT_DIRS)
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgssapi-krb5-2 \
        fontconfig \
        fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN echo "BUILD AT: $(date)"
COPY --chown=app:app bin/Release/net10.0/publish/ .
USER app
ENTRYPOINT ["dotnet", "HC.BackgroundJobWorker.dll"]
