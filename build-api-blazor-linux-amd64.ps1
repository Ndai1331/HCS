param (
    [string]$version = 'latest01042026',
    [switch]$BuildBase,
    [string]$BaseTag = 'libreoffice-v1',
    [switch]$ClearDockerCache
)

$currentFolder = $PSScriptRoot
$slnFolder = $currentFolder
$blazorBaseImage = "longnguyen1331/hc-blazor-base:$BaseTag"
$blazorAppImage = "longnguyen1331/hc-blazor"

if ($ClearDockerCache) {
    Write-Host "Clearing safe Docker cache (preserve buildx cache for base images)..." -ForegroundColor Yellow
    try {
        # Keep buildx cache to avoid losing the libreoffice-v1 base cache.
        # docker buildx prune -af
        docker image prune -f
        if (-not $?) {
            throw "docker image prune failed"
        }
    } catch {
        Write-Host "ERROR: failed to clear Docker cache" -ForegroundColor Red
        exit 1
    }
}

# Write-Host "********* BUILDING AuthServer (HC.AuthServer) *********" -ForegroundColor Green
# $authServerFolder = Join-Path $slnFolder "src/HC.AuthServer"
# Set-Location $authServerFolder

# Write-Host "Publishing AuthServer..." -ForegroundColor Yellow
# try {
#     $authPublishDir = Join-Path $authServerFolder "bin/Release/net10.0/publish"
#     if (Test-Path $authPublishDir) {
#         Remove-Item $authPublishDir -Recurse -Force
#     }
#     $result = dotnet publish -c Release -o bin/Release/net10.0/publish 2>&1
#     if (-not $?) {
#         throw "dotnet publish failed"
#     }
# } catch {
#     Write-Host "ERROR: dotnet publish failed for AuthServer" -ForegroundColor Red
#     Write-Host $result -ForegroundColor Red
#     exit 1
# }

# Start-Sleep -Seconds 1
# $currentDir = Get-Location
# $publishPath = Join-Path $currentDir "bin/Release/net10.0/publish"
# $publishPathFull = [System.IO.Path]::GetFullPath($publishPath)
# if (-not (Test-Path $publishPathFull)) {
#     Write-Host "ERROR: Publish folder not found: $publishPathFull" -ForegroundColor Red
#     exit 1
# }

# Write-Host "Publish successful. Output: $publishPathFull" -ForegroundColor Green
# Write-Host "Building Docker image for AuthServer (linux/amd64)..." -ForegroundColor Yellow
# try {
#     docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-authserver:$version -t longnguyen1331/hc-authserver:latest . --push
#     if (-not $?) {
#         throw "docker build failed"
#     }
# } catch {
#     Write-Host "ERROR: Docker build failed for AuthServer" -ForegroundColor Red
#     exit 1
# }
# Write-Host "Docker image built and pushed successfully for AuthServer (tags: $version, latest)" -ForegroundColor Green



Write-Host "********* BUILDING API (HC.HttpApi.Host) *********" -ForegroundColor Green
$apiFolder = Join-Path $slnFolder "src/HC.HttpApi.Host"
Set-Location $apiFolder

Write-Host "Publishing API..." -ForegroundColor Yellow
try {
    $apiPublishDir = Join-Path $apiFolder "bin/Release/net10.0/publish"
    if (Test-Path $apiPublishDir) {
        Remove-Item $apiPublishDir -Recurse -Force
    }
    $result = dotnet publish -c Release -o bin/Release/net10.0/publish 2>&1
    if (-not $?) {
        throw "dotnet publish failed"
    }
} catch {
    Write-Host "ERROR: dotnet publish failed for API" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 1
$currentDir = Get-Location
$publishPath = Join-Path $currentDir "bin/Release/net10.0/publish"
$publishPathFull = [System.IO.Path]::GetFullPath($publishPath)
if (-not (Test-Path $publishPathFull)) {
    Write-Host "ERROR: Publish folder not found: $publishPathFull" -ForegroundColor Red
    exit 1
}

Write-Host "Publish successful. Output: $publishPathFull" -ForegroundColor Green
Write-Host "Building Docker image for API (linux/amd64)..." -ForegroundColor Yellow
try {
    docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-api:$version -t longnguyen1331/hc-api:latest . --push
    if (-not $?) {
        throw "docker build failed"
    }
} catch {
    Write-Host "ERROR: Docker build failed for API" -ForegroundColor Red
    exit 1
}
Write-Host "Docker image built and pushed successfully for API (tags: $version, latest)" -ForegroundColor Green

Write-Host "********* BUILDING Blazor Application *********" -ForegroundColor Green
$blazorFolder = Join-Path $slnFolder "src/HC.Blazor"
Set-Location $blazorFolder

if ($BuildBase) {
    Write-Host "Building Blazor base image ($blazorBaseImage)..." -ForegroundColor Yellow
    try {
        docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.base -t $blazorBaseImage . --push
        if (-not $?) {
            throw "docker base build failed"
        }
    } catch {
        Write-Host "ERROR: Docker base image build failed for Blazor" -ForegroundColor Red
        exit 1
    }
    Write-Host "Blazor base image built and pushed successfully ($blazorBaseImage)" -ForegroundColor Green
}

Write-Host "Publishing Blazor..." -ForegroundColor Yellow
try {
    $blazorPublishDir = Join-Path $blazorFolder "bin/Release/net10.0/publish"
    if (Test-Path $blazorPublishDir) {
        Remove-Item $blazorPublishDir -Recurse -Force
    }
    $result = dotnet publish -c Release -o bin/Release/net10.0/publish 2>&1
    if (-not $?) {
        throw "dotnet publish failed"
    }
} catch {
    Write-Host "ERROR: dotnet publish failed for Blazor" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 1
$currentDir = Get-Location
$publishPath = Join-Path $currentDir "bin/Release/net10.0/publish"
$publishPathFull = [System.IO.Path]::GetFullPath($publishPath)
if (-not (Test-Path $publishPathFull)) {
    Write-Host "ERROR: Publish folder not found: $publishPathFull" -ForegroundColor Red
    exit 1
}

Write-Host "Publish successful. Output: $publishPathFull" -ForegroundColor Green
Write-Host "Building Docker image for Blazor (linux/amd64)..." -ForegroundColor Yellow
try {
    $blazorDll = Join-Path $publishPathFull "HC.Blazor.dll"
    if (-not (Test-Path $blazorDll)) {
        Write-Host "ERROR: Blazor publish output is invalid. Missing file: $blazorDll" -ForegroundColor Red
        exit 1
    }

    $publishDockerfile = Join-Path $publishPathFull "Dockerfile.publish.local"
    $buildDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
@"
FROM $blazorBaseImage
USER `$APP_UID
EXPOSE 8080
EXPOSE 8081
WORKDIR /app
COPY . .
ARG BUILD_DATE
LABEL org.opencontainers.image.created=`"`$BUILD_DATE`"
ENTRYPOINT ["dotnet", "HC.Blazor.dll"]
"@ | Set-Content -Path $publishDockerfile -Encoding UTF8

    docker buildx build --pull --no-cache --platform linux/amd64 -f $publishDockerfile --build-arg BUILD_DATE=$buildDate -t "${blazorAppImage}:$version" -t "${blazorAppImage}:latest" $publishPathFull --push
    if (-not $?) {
        throw "docker build failed"
    }
    if (Test-Path $publishDockerfile) {
        Remove-Item $publishDockerfile -Force
    }
} catch {
    Write-Host "ERROR: Docker build failed for Blazor" -ForegroundColor Red
    exit 1
}
Write-Host "Docker image built and pushed successfully for Blazor (tags: $version, latest)" -ForegroundColor Green

Write-Host "********* BUILD COMPLETED *********" -ForegroundColor Green
Set-Location $currentFolder
exit 0




# cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.AuthServer && docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-authserver:latest -t longnguyen1331/hc-authserver:latest . --push
# cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.HttpApi.Host && docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-api:latest -t longnguyen1331/hc-api:latest . --push
# cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.Blazor && docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-blazor:latest -t longnguyen1331/hc-blazor:latest . --push

# docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-blazor:latest -t longnguyen1331/hc-blazor:latest . --push
# docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-api:latest -t longnguyen1331/hc-api:latest . --push
# docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-authserver:latest -t longnguyen1331/hc-authserver:latest . --push
# docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-db-migrator:latest -t longnguyen1331/hc-db-migrator:latest . --push