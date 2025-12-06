# Run IsoEngine Game
Write-Host "IsoEngine Game Launcher" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
$projectFile = "GameClient\GameClient.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Host "ERROR: GameClient project not found!" -ForegroundColor Red
    Write-Host "Please run this script from the IsoEngine root directory." -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "Building game..." -ForegroundColor Yellow
dotnet build GameClient\GameClient.csproj --configuration Debug --no-incremental

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host "Starting game..." -ForegroundColor Yellow
Write-Host "The game window should appear shortly." -ForegroundColor Cyan
Write-Host "If you don't see it, check if it's behind other windows." -ForegroundColor Yellow
Write-Host ""

# Run the game from the correct directory
$gameExe = "GameClient\bin\Debug\net8.0\GameClient.exe"
$gameDir = "GameClient\bin\Debug\net8.0"

if (Test-Path $gameExe) {
    Set-Location $gameDir
    Start-Process -FilePath ".\GameClient.exe" -WorkingDirectory $PWD
    Set-Location ..\..\..\..
    Write-Host "Game launched!" -ForegroundColor Green
} else {
    Write-Host "ERROR: Game executable not found at: $gameExe" -ForegroundColor Red
    pause
    exit 1
}




