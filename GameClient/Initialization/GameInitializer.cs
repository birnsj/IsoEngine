using GameCore;
using GameCore.Entities;
using GameCore.Rendering;
using GameCore.Services;
using GameClient.Services;
using GameClient.Utilities;
using Microsoft.Xna.Framework;

namespace GameClient.Initialization;

/// <summary>
/// Handles game initialization including service registration, map loading, and entity creation.
/// </summary>
public class GameInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public GameInitializer(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes all game services.
    /// </summary>
    public void InitializeServices()
    {
        foreach (var service in ServiceContainer.GetAllGameServices(_serviceProvider))
        {
            service.Initialize();
        }
        _logger.Info("All game services initialized.");
    }

    /// <summary>
    /// Loads or creates the tile map.
    /// </summary>
    /// <returns>The loaded or created tile map.</returns>
    public IsometricTileMap LoadTileMap(out List<int> solidTiles)
    {
        solidTiles = new List<int>(); // No solid tiles - collision disabled

        var tileMap = LoadTileMapFromFile();
        if (tileMap != null)
        {
            var mapFile = GameContentPathHelper.GetWorldMapPath();
            if (mapFile != null && File.Exists(mapFile))
            {
                solidTiles = GameCore.Rendering.TileMapLoader.GetSolidTilesFromJson(mapFile);
                _logger.Info($"Loaded {solidTiles.Count} solid tiles from map file");
            }
            return tileMap;
        }

        // Create default map
        _logger.Info("Creating default test map");
        tileMap = new IsometricTileMap(
            GameConstants.Tiles.DefaultMapWidth, 
            GameConstants.Tiles.DefaultMapHeight, 
            GameConstants.Tiles.DefaultWidth, 
            GameConstants.Tiles.DefaultHeight);
        CreateTestMap(tileMap);
        return tileMap;
    }

    /// <summary>
    /// Configures collision service with a collision grid.
    /// </summary>
    public void ConfigureCollisionService(CollisionService collisionService, IsometricTileMap tileMap)
    {
        if (collisionService == null)
            throw new ArgumentNullException(nameof(collisionService));
        if (tileMap == null)
            throw new ArgumentNullException(nameof(tileMap));

        collisionService.SetTileMap(tileMap);
        
        // Try to load collision grid from file
        GameCore.Collision.CollisionGrid? collisionGrid = null;
        
        // Determine collision file path (same directory as map file, with _collision suffix)
        var worldMapPath = GameContentPathHelper.GetWorldMapPath();
        if (worldMapPath != null && System.IO.File.Exists(worldMapPath))
        {
            var directory = System.IO.Path.GetDirectoryName(worldMapPath);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(worldMapPath);
            var extension = System.IO.Path.GetExtension(worldMapPath);
            var collisionFilePath = System.IO.Path.Combine(directory ?? "", $"{fileName}_collision{extension}");
            
            if (System.IO.File.Exists(collisionFilePath))
            {
                collisionGrid = GameCore.Collision.CollisionGridSerializer.LoadFromJson(collisionFilePath);
            }
        }
        
        // Create new grid if loading failed
        if (collisionGrid == null)
        {
            collisionGrid = new GameCore.Collision.CollisionGrid(
                worldWidth: tileMap.Width,
                worldHeight: tileMap.Height,
                cellSize: 1.0f);
            
            // No collision - grid is created empty (all tiles are non-solid)
            // Previously this would mark tiles 4, 5, 6, 7 as solid, but collision is now disabled
        }
        
        collisionService.SetCollisionGrid(collisionGrid);
    }

    /// <summary>
    /// Creates the player at the specified position.
    /// </summary>
    public Player CreatePlayer(IsometricTileMap tileMap, Vector2? startPosition = null)
    {
        if (tileMap == null)
            throw new ArgumentNullException(nameof(tileMap));

        Vector2 playerStartWorld;
        if (startPosition.HasValue)
        {
            playerStartWorld = startPosition.Value;
        }
        else
        {
            // Try to load from map file
            var playerStartFromFile = GameContentPathHelper.GetWorldMapPath() != null 
                ? GameCore.Rendering.TileMapLoader.GetPlayerStartPosition(GameContentPathHelper.GetWorldMapPath()!)
                : null;

            if (playerStartFromFile.HasValue)
            {
                playerStartWorld = playerStartFromFile.Value;
            }
            else
            {
                // Fallback: center of map
                var centerX = tileMap.Width / 2;
                var centerY = tileMap.Height / 2;
                playerStartWorld = new Vector2(centerX, centerY);
            }
        }

        var playerStartScreen = tileMap.WorldToScreen(playerStartWorld);
        var player = new Player(playerStartScreen);
        player.MovementSpeed = 150.0f;
        player.Size = new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize);

        return player;
    }

    private IsometricTileMap? LoadTileMapFromFile()
    {
        try
        {
            var mapsDir = GameContentPathHelper.GetMapsDirectory();
            if (mapsDir == null || !Directory.Exists(mapsDir))
            {
                _logger.Warning($"Could not find maps directory: {mapsDir ?? "null"}");
                return null;
            }

            var mapFiles = Directory.GetFiles(mapsDir, "*.json");
            if (mapFiles.Length == 0)
            {
                _logger.Warning($"No map files found in {mapsDir}");
                return null;
            }

            // Use shared method to get preferred map file (ensures consistency with editor)
            var mapFile = GameCore.GameConstants.DefaultFiles.GetPreferredMapFile(mapFiles);
            if (mapFile == null)
            {
                _logger.Warning($"No valid map files found in {mapsDir}");
                return null;
            }

            _logger.Info($"Loading map from: {mapFile}");
            var tileMap = GameCore.Rendering.TileMapLoader.LoadFromJson(mapFile);
            if (tileMap != null)
            {
                _logger.Info($"Loaded tile map (Size: {tileMap.Width}x{tileMap.Height}, TileSize: {tileMap.TileWidth}x{tileMap.TileHeight})");
            }
            return tileMap;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error loading tile map from file: {ex.Message}");
            return null;
        }
    }

    private void CreateTestMap(IsometricTileMap tileMap)
    {
        var random = new Random(42);

        for (int y = 0; y < tileMap.Height; y++)
        {
            for (int x = 0; x < tileMap.Width; x++)
            {
                int tileIndex;

                if (x == 0 || x == tileMap.Width - 1 || y == 0 || y == tileMap.Height - 1)
                {
                    tileIndex = 6; // Stone border
                }
                else if (x == tileMap.Width / 2 || y == tileMap.Height / 2)
                {
                    tileIndex = 2; // Dirt road
                }
                else if (IsBuildingLocation(x, y, tileMap.Width, tileMap.Height))
                {
                    tileIndex = 6; // Stone building
                }
                else if (random.Next(100) < 15)
                {
                    tileIndex = 1; // Dark grass
                }
                else
                {
                    tileIndex = 0; // Light grass
                }

                tileMap.SetTile(x, y, tileIndex);
            }
        }
    }

    private bool IsBuildingLocation(int x, int y, int width, int height)
    {
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
            return false;
        if (x == width / 2 || y == height / 2)
            return false;

        var buildingSpacing = 3;
        var buildingOffset = 2;
        var buildingX = (x - buildingOffset) % buildingSpacing;
        var buildingY = (y - buildingOffset) % buildingSpacing;

        return (buildingX == 0 && buildingY == 0) ||
               (buildingX == 1 && buildingY == 0 && (x + y) % 6 < 3) ||
               (buildingX == 0 && buildingY == 1 && (x + y) % 6 < 3);
    }
}

