using System.IO;
using System.Linq;
using System.Reflection;

namespace GameClient.Utilities;

/// <summary>
/// Helper class for resolving paths to game content files in the source directory.
/// Ensures the game and editors use the same JSON files.
/// </summary>
public static class GameContentPathHelper
{
    private static string? _gameContentPath;

    /// <summary>
    /// Gets the path to the source GameContent directory (not the runtime copy).
    /// </summary>
    public static string? GetGameContentPath()
    {
        if (_gameContentPath != null && Directory.Exists(_gameContentPath))
        {
            return _gameContentPath;
        }

        // Try multiple possible locations
        var possiblePaths = new List<string>();

        // From current working directory (when running from project root with dotnet run)
        var currentDir = Directory.GetCurrentDirectory();
        possiblePaths.Add(Path.Combine(currentDir, "GameContent")); // D:\IsoEngine\GameContent
        possiblePaths.Add(Path.Combine(currentDir, "..", "GameContent"));
        possiblePaths.Add(Path.Combine(currentDir, "..", "..", "GameContent"));
        possiblePaths.Add(Path.Combine(currentDir, "..", "..", "..", "GameContent"));
        possiblePaths.Add(Path.Combine(currentDir, "..", "..", "..", "..", "GameContent"));
        possiblePaths.Add(Path.Combine(currentDir, "..", "..", "..", "..", "..", "GameContent"));
        
        // Also check relative to GameClient directory (when running from GameClient folder)
        var gameClientDir = Path.Combine(currentDir, "GameClient");
        if (Directory.Exists(gameClientDir))
        {
            possiblePaths.Add(Path.Combine(currentDir, "..", "GameContent")); // From GameClient, go up to IsoEngine, then GameContent
        }

        // From executable location (when running from bin/Debug/net8.0/ or as single-file)
        try
        {
            // Use AppContext.BaseDirectory for single-file deployments (works in all cases)
            var baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                // From bin/Debug/net8.0/GameContent or dist/Game/GameContent -> find GameContent
                possiblePaths.Add(Path.Combine(baseDirectory, "GameContent"));
                
                // Also try parent directory (for single-file deployments where exe is in root)
                var parentDir = Path.GetDirectoryName(baseDirectory);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    possiblePaths.Add(Path.Combine(parentDir, "GameContent"));
                    
                    // Search up parent directories
                    var searchDir = new DirectoryInfo(parentDir);
                    for (int i = 0; i < 6 && searchDir != null; i++)
                    {
                        var gameContentPath = Path.Combine(searchDir.FullName, "GameContent");
                        possiblePaths.Add(gameContentPath);
                        searchDir = searchDir.Parent;
                    }
                }
                
                // Also search from base directory itself
                var searchBaseDir = new DirectoryInfo(baseDirectory);
                for (int i = 0; i < 6 && searchBaseDir != null; i++)
                {
                    var gameContentPath = Path.Combine(searchBaseDir.FullName, "GameContent");
                    possiblePaths.Add(gameContentPath);
                    searchBaseDir = searchBaseDir.Parent;
                }
            }
        }
        catch
        {
            // Ignore errors getting base directory
        }

        // Search up from current directory
        var searchCurrentDir = new DirectoryInfo(currentDir);
        for (int i = 0; i < 6 && searchCurrentDir != null; i++)
        {
            var gameContentPath = Path.Combine(searchCurrentDir.FullName, "GameContent");
            possiblePaths.Add(gameContentPath);
            searchCurrentDir = searchCurrentDir.Parent;
        }

        // Remove duplicates and check each path
        // Prioritize source directories over runtime copies
        var checkedPaths = new HashSet<string>();
        var sourcePaths = new List<string>();
        var runtimePaths = new List<string>();
        
        foreach (var path in possiblePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (checkedPaths.Contains(fullPath))
                    continue;
                checkedPaths.Add(fullPath);
                
                if (Directory.Exists(fullPath))
                {
                    // Verify it's a valid GameContent directory (has maps, entities subdirectories)
                    var mapsDir = Path.Combine(fullPath, "maps");
                    var entitiesDir = Path.Combine(fullPath, "entities");
                    if (Directory.Exists(mapsDir) || Directory.Exists(entitiesDir))
                    {
                        // Check if this is a runtime copy (in bin/ or obj/ directory)
                        var normalizedPath = fullPath.Replace('\\', '/');
                        if (normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || 
                            normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
                            normalizedPath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
                            normalizedPath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                        {
                            runtimePaths.Add(fullPath);
                        }
                        else
                        {
                            sourcePaths.Add(fullPath);
                        }
                    }
                }
            }
            catch
            {
                // Continue searching
            }
        }
        
        // ALWAYS prioritize source directories over runtime copies
        // Runtime copies don't have TileGraphics sections and are outdated
        if (sourcePaths.Count > 0)
        {
            // Prefer the path closest to the workspace root (most likely to be the source)
            // Sort by path length (shorter = closer to root)
            var sortedSourcePaths = sourcePaths.OrderBy(p => p.Length).ToList();
            _gameContentPath = sortedSourcePaths[0];
            
            // Validation: Log the resolved path to help debug if game and editor use different paths
            System.Diagnostics.Debug.WriteLine($"[GameContentPathHelper] Resolved GameContent path: {_gameContentPath}");
            
            return sortedSourcePaths[0];
        }
        
        // If no source directory found, return null (don't use outdated runtime copies)
        // The editor saves to the source directory, so we should always find it
        System.Diagnostics.Debug.WriteLine("[GameContentPathHelper] WARNING: Could not find source GameContent directory!");
        return null;
    }

    /// <summary>
    /// Gets the path to the maps directory in the source GameContent.
    /// </summary>
    public static string? GetMapsDirectory()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        var mapsDir = Path.Combine(gameContentPath, "maps");
        if (!Directory.Exists(mapsDir))
        {
            Directory.CreateDirectory(mapsDir);
        }
        return mapsDir;
    }

    /// <summary>
    /// Gets the path to the entities directory in the source GameContent.
    /// </summary>
    public static string? GetEntitiesDirectory()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        var entitiesDir = Path.Combine(gameContentPath, "entities");
        if (!Directory.Exists(entitiesDir))
        {
            Directory.CreateDirectory(entitiesDir);
        }
        return entitiesDir;
    }

    /// <summary>
    /// Gets the path to the dialogs directory in the source GameContent.
    /// </summary>
    public static string? GetDialogsDirectory()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        var dialogsDir = Path.Combine(gameContentPath, "dialogs");
        if (!Directory.Exists(dialogsDir))
        {
            Directory.CreateDirectory(dialogsDir);
        }
        return dialogsDir;
    }

    /// <summary>
    /// Gets the path to world.json map file in the source GameContent.
    /// Uses the shared constant to ensure consistency with the editor.
    /// </summary>
    public static string? GetWorldMapPath()
    {
        var mapsDir = GetMapsDirectory();
        if (mapsDir == null)
            return null;

        return Path.Combine(mapsDir, GameCore.GameConstants.DefaultFiles.WorldMap);
    }

    /// <summary>
    /// Gets the path to world_entities.json file in the source GameContent.
    /// Uses the shared constant to ensure consistency with the editor.
    /// </summary>
    public static string? GetWorldEntitiesPath()
    {
        var entitiesDir = GetEntitiesDirectory();
        if (entitiesDir == null)
            return null;

        return Path.Combine(entitiesDir, GameCore.GameConstants.DefaultFiles.WorldEntities);
    }

    /// <summary>
    /// Gets the path to world_lights.json file in the source GameContent.
    /// Uses the shared constant to ensure consistency with the editor.
    /// </summary>
    public static string? GetWorldLightsPath()
    {
        var mapsDir = GetMapsDirectory();
        if (mapsDir == null)
            return null;

        return Path.Combine(mapsDir, GameCore.GameConstants.DefaultFiles.WorldLights);
    }

    /// <summary>
    /// Gets the path to tiles.json file in the source GameContent.
    /// Uses the shared constant to ensure consistency with the editor.
    /// </summary>
    public static string? GetTilesLibraryPath()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        return Path.Combine(gameContentPath, GameCore.GameConstants.DefaultFiles.TilesLibrary);
    }

    /// <summary>
    /// Gets the path to items.json file in the source GameContent.
    /// Uses the shared constant to ensure consistency with the editor.
    /// </summary>
    public static string? GetItemsPath()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        return Path.Combine(gameContentPath, GameCore.GameConstants.DefaultFiles.Items);
    }

    /// <summary>
    /// Gets the path to the items directory in the source GameContent.
    /// </summary>
    public static string? GetItemsDirectory()
    {
        var gameContentPath = GetGameContentPath();
        if (gameContentPath == null)
            return null;

        var itemsDir = Path.Combine(gameContentPath, "items");
        if (!Directory.Exists(itemsDir))
        {
            Directory.CreateDirectory(itemsDir);
        }
        return itemsDir;
    }

    /// <summary>
    /// Validates that the resolved GameContent path is correct and logs it for debugging.
    /// This helps ensure the game and editor are using the same source directory.
    /// </summary>
    public static void ValidateGameContentPath()
    {
        var path = GetGameContentPath();
        if (path == null)
        {
            System.Diagnostics.Debug.WriteLine("[GameContentPathHelper] VALIDATION FAILED: GameContent path is null!");
            return;
        }

        if (!Directory.Exists(path))
        {
            System.Diagnostics.Debug.WriteLine($"[GameContentPathHelper] VALIDATION FAILED: GameContent path does not exist: {path}");
            return;
        }

        // Verify it's the source directory (not a runtime copy)
        var normalizedPath = path.Replace('\\', '/');
        if (normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || 
            normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Debug.WriteLine($"[GameContentPathHelper] VALIDATION WARNING: GameContent path appears to be a runtime copy: {path}");
            System.Diagnostics.Debug.WriteLine("[GameContentPathHelper] This may cause issues - the game should use the source directory!");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[GameContentPathHelper] VALIDATION PASSED: Using source GameContent directory: {path}");
        }
    }
}

