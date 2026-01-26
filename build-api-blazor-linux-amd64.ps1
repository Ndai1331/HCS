param ($version='latest')

$currentFolder = $PSScriptRoot
$slnFolder = $currentFolder

Write-Host "********* BUILDING API (HC.HttpApi.Host) *********" -ForegroundColor Green
$apiFolder = Join-Path $slnFolder "src/HC.HttpApi.Host"
Set-Location $apiFolder

Write-Host "Publishing API..." -ForegroundColor Yellow
try {
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
    docker buildx build --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-api:$version -t longnguyen1331/hc-api:latest . --push
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

Write-Host "Publishing Blazor..." -ForegroundColor Yellow
try {
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
    docker buildx build --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-blazor:$version -t longnguyen1331/hc-blazor:latest . --push
    if (-not $?) {
        throw "docker build failed"
    }
} catch {
    Write-Host "ERROR: Docker build failed for Blazor" -ForegroundColor Red
    exit 1
}
Write-Host "Docker image built and pushed successfully for Blazor (tags: $version, latest)" -ForegroundColor Green

Write-Host "********* BUILD COMPLETED *********" -ForegroundColor Green
Set-Location $currentFolder
exit 0


