param (
    [string]$BaseTag = 'libreoffice-v1',
    [switch]$ClearDockerCache
)

$currentFolder = $PSScriptRoot
$slnFolder = $currentFolder
$blazorBaseImage = "longnguyen1331/hc-blazor-base:$BaseTag"
$apiBaseImage = "longnguyen1331/hc-api-base:$BaseTag"

if ($ClearDockerCache) {
    Write-Host "Clearing safe Docker cache (preserve buildx cache for base images)..." -ForegroundColor Yellow
    try {
        docker image prune -f
        if (-not $?) {
            throw "docker image prune failed"
        }
    } catch {
        Write-Host "ERROR: failed to clear Docker cache" -ForegroundColor Red
        exit 1
    }
}

Write-Host "********* BUILDING Docker base images (Dockerfile.base) *********" -ForegroundColor Green

$blazorFolder = Join-Path $slnFolder "src/HC.Blazor"
Set-Location $blazorFolder

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

$apiBaseFolder = Join-Path $slnFolder "src/HC.HttpApi.Host"
Set-Location $apiBaseFolder

Write-Host "Building API base image ($apiBaseImage)..." -ForegroundColor Yellow
try {
    docker buildx build --no-cache --platform linux/amd64 -f Dockerfile.base -t $apiBaseImage . --push
    if (-not $?) {
        throw "docker api base build failed"
    }
} catch {
    Write-Host "ERROR: Docker base image build failed for HttpApi.Host" -ForegroundColor Red
    exit 1
}
Write-Host "API base image built and pushed successfully ($apiBaseImage)" -ForegroundColor Green

Set-Location $currentFolder
Write-Host "********* BASE BUILD COMPLETED (hc-blazor-base + hc-api-base, tag: $BaseTag) *********" -ForegroundColor Green
exit 0
