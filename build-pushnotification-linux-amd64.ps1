param (
    [string]$version = 'latest01042026',
    [switch]$ClearDockerCache
)

$currentFolder = $PSScriptRoot
$slnFolder = $currentFolder
$pushNotificationWorkerAppImage = "longnguyen1331/hc-pushnotificationworker"
$dockerfile = Join-Path $slnFolder "src/HC.PushNotificationWorker/Dockerfile"

if (-not (Test-Path $dockerfile)) {
    Write-Host "ERROR: Dockerfile not found: $dockerfile" -ForegroundColor Red
    exit 1
}

if ($ClearDockerCache) {
    Write-Host "Clearing safe Docker cache..." -ForegroundColor Yellow
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

Write-Host "********* BUILDING HC.PushNotificationWorker (linux/amd64, in-container publish) *********" -ForegroundColor Green
Set-Location $slnFolder

Write-Host "Building and pushing Docker image (SDK publish inside linux/amd64)..." -ForegroundColor Yellow
try {
    docker buildx build --pull --no-cache --platform linux/amd64 `
        -f src/HC.PushNotificationWorker/Dockerfile `
        -t "${pushNotificationWorkerAppImage}:$version" `
        -t "${pushNotificationWorkerAppImage}:latest" `
        . --push
    if (-not $?) {
        throw "docker build failed"
    }
} catch {
    Write-Host "ERROR: Docker build failed for HC.PushNotificationWorker" -ForegroundColor Red
    Set-Location $currentFolder
    exit 1
}

Write-Host "Docker image built and pushed successfully (tags: $version, latest) -> $pushNotificationWorkerAppImage" -ForegroundColor Green
Write-Host "On server: docker compose pull hc-pushnotificationworker && docker compose up -d hc-pushnotificationworker" -ForegroundColor Cyan
Write-Host "********* BUILD COMPLETED (HC.PushNotificationWorker) *********" -ForegroundColor Green
Set-Location $currentFolder
exit 0

# Usage:
#   ./build-pushnotification-linux-amd64.ps1
#   ./build-pushnotification-linux-amd64.ps1 -version latestlvt
#   ./build-pushnotification-linux-amd64.ps1 -version latest01042026 -ClearDockerCache
