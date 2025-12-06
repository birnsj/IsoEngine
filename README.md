# Ultima 8-Like Prototype

A 2D isometric action RPG prototype inspired by Ultima 8, built with MonoGame and Myra UI.

## Project Structure

- **GameClient**: MonoGame DesktopGL project - entry point and main game loop
- **GameCore**: Class library - core engine, systems, and shared logic
- **GameContent**: Content project - assets and resources

## Prerequisites

- .NET 8 SDK (or latest stable LTS)
- Visual Studio 2022 (recommended) or VS Code with C# extension

## Build Instructions

### Using .NET CLI:

```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run the game
dotnet run --project GameClient
```

### Using Visual Studio:

1. Open `IsoEngine.sln` in Visual Studio
2. Restore NuGet packages (should happen automatically)
3. Set `GameClient` as the startup project
4. Press F5 to run

## NuGet Packages

- **MonoGame.Framework.DesktopGL** (3.8.1.303) - MonoGame framework for desktop OpenGL (includes Content Pipeline tools)
- **Myra** (1.2.0.215) - UI framework for MonoGame

## Current Features

- Basic MonoGame window setup
- Myra UI integration with centered title label
- Configuration class for window settings

## Next Steps

- Implement isometric rendering
- Add game entities and world system
- Create player character and controls
- Add content assets (sprites, tiles, etc.)

