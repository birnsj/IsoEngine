# File Structure and Map/Entity File Management

## Overview
The game and editor both use the **same source GameContent directory** located at the project root: `GameContent/`

This ensures:
- No duplicate map or entity files
- Game and editor always use the same data
- Changes in the editor are immediately reflected in the game

## Directory Structure

```
IsoEngine/
├── GameContent/              ← SOURCE DIRECTORY (use this one!)
│   ├── maps/
│   │   ├── world.json       ← Main map file
│   │   └── world_collision.json
│   ├── entities/
│   │   └── world_entities.json
│   └── dialogs/
├── GameClient/
│   └── GameContent/         ← DUPLICATE (should be removed)
├── GameClient/bin/Debug/net8.0/GameContent/  ← Build output (can be removed)
└── GameEditor/
```

## Path Resolution

Both the game and editor use path helpers that:
1. Search from the current directory up to find `GameContent/`
2. Verify it's the source directory by checking for `maps/` or `entities/` subdirectories
3. Always resolve to the same source directory at the project root

### Game Path Helper
- **Location**: `GameClient/Utilities/GameContentPathHelper.cs`
- **Method**: `GetGameContentPath()` - Returns path to source GameContent

### Editor Path Helper
- **Location**: `GameEditor/Utilities/PathHelper.cs`
- **Method**: `GetGameContentPath()` - Returns path to source GameContent (same logic as game)

## File Loading Priority

### Maps
1. Prefers `world.json`
2. Falls back to `world_map.json`
3. Otherwise uses first `.json` file found

### Entities
- Uses `world_entities.json` in the entities directory

## Important Notes

1. **Always save to source GameContent**: The editor automatically saves to the source `GameContent/` directory
2. **No duplicates needed**: The game and editor both read from the same source directory
3. **Build output**: Any `GameContent` directories in `bin/` or `GameClient/` are duplicates and can be safely removed

## Cleaning Up Duplicates

If you find duplicate `GameContent` directories:
1. **Keep**: `GameContent/` at project root (source)
2. **Remove**: `GameClient/GameContent/` (duplicate)
3. **Remove**: `GameClient/bin/Debug/net8.0/GameContent/` (build output, if exists)

The game and editor will automatically find the source `GameContent/` directory regardless of where they're run from.







