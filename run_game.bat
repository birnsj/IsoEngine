@echo off
echo Starting IsoEngine Game...
echo.

cd GameClient\bin\Debug\net8.0
if not exist GameClient.exe (
    echo ERROR: GameClient.exe not found!
    echo Building the game first...
    cd ..\..\..\..\..
    dotnet build GameClient\GameClient.csproj --configuration Debug
    cd GameClient\bin\Debug\net8.0
)

if exist GameClient.exe (
    echo Launching game...
    echo.
    GameClient.exe
    if errorlevel 1 (
        echo.
        echo Game exited with error code %errorlevel%
        pause
    )
) else (
    echo ERROR: Could not find or build GameClient.exe
    pause
)




