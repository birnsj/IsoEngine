# Run IsoEngine Editor
Write-Host "IsoEngine Editor Launcher" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
$projectFile = "GameEditor\GameEditor.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Host "ERROR: GameEditor project not found!" -ForegroundColor Red
    Write-Host "Please run this script from the IsoEngine root directory." -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "Building editor..." -ForegroundColor Yellow
dotnet build GameEditor\GameEditor.csproj --configuration Debug --no-incremental

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host "Starting editor..." -ForegroundColor Yellow
Write-Host "The editor window should appear shortly." -ForegroundColor Cyan
Write-Host ""

# Run the editor from the correct directory
$editorExe = "GameEditor\bin\Debug\net8.0-windows\GameEditor.exe"
$editorDir = "GameEditor\bin\Debug\net8.0-windows"

if (Test-Path $editorExe) {
    Set-Location $editorDir
    Start-Process -FilePath ".\GameEditor.exe" -WorkingDirectory $PWD
    Set-Location ..\..\..\..
    Write-Host "Editor launched!" -ForegroundColor Green
} else {
    Write-Host "ERROR: Editor executable not found at: $editorExe" -ForegroundColor Red
    pause
    exit 1
}

