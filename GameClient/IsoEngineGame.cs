using GameCore;
using GameCore.Configuration;
using GameCore.Entities;
using GameCore.Input;
using GameCore.Rendering;
using GameCore.Services;
using GameCore.State;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using GameClient.Entities;
using GameClient.Input;
using GameClient.Rendering;
using GameClient.Services;
using GameClient.UI;
using GameClient.Editor;
using GameClient.Utilities;
using GameCore.Dialogs;
using GameCore.Interactions;
using GameCore.Items;
using System.Runtime.InteropServices;
using System.IO;
using System.Timers;

namespace GameClient;

/// <summary>
/// Main game class for the IsoEngine Game.
/// Handles initialization, game loop, and Myra UI integration.
/// </summary>
public class IsoEngineGame : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Desktop? _desktop;
    private Label? _debugLabel;
    private Camera2D? _camera;
    private IsometricTileMap? _tileMap;
    private TileMapRenderer? _tileMapRenderer;
    private CameraController? _cameraController;
    private Player? _player;
    private CollisionService? _collisionService;
    private InteractionService? _interactionService;
    private PlayerControllerService? _playerController;
    private EntityRenderer? _entityRenderer;
    private CollisionDebugRenderer? _collisionDebugRenderer;
    private InteractionRadiusRenderer? _interactionRadiusRenderer;
    private AggroRadiusRenderer? _aggroRadiusRenderer;
    private ClickEffectRenderer? _clickEffectRenderer;
    private LightingRenderer? _lightingRenderer;
    private GameCore.Rendering.Light? _playerLight; // Orange light attached to player
    private LightingRadiusRenderer? _lightingRadiusRenderer;
    private ParticleSystem? _particleSystem;
    private ScreenShakeService? _screenShakeService;
    private ScreenEffectRenderer? _screenEffectRenderer;
    private DamageNumberRenderer? _damageNumberRenderer;
    private HitIndicatorRenderer? _hitIndicatorRenderer;
    private TrailRenderer? _trailRenderer;
    private PathIndicatorRenderer? _pathIndicatorRenderer;
    private DeathEffectService? _deathEffectService;
    private ProjectileService? _projectileService;
    private ProjectileRenderer? _projectileRenderer;
    private DayNightCycleService? _dayNightCycle;
    private WeatherSystem? _weatherSystem;
    private Label? _interactionLabel;
    private Label? _hoverNameTag;
    private InventoryWindow? _inventoryWindow;
    private EnemyInventoryWindow? _enemyInventoryWindow;
    private DialogWindow? _dialogWindow;
    private Dictionary<string, GameCore.Items.Item>? _itemDatabase;
    private Entity? _hoveredEntity;
    private float _saveLoadCooldown;
    private GameStateService? _gameStateService;
    private Panel? _titleScreenPanel;
    private Panel? _pauseMenuPanel;
    private OptionsWindow? _optionsWindow;
    private bool _showCollisionDebug = false;
    private bool _collisionTogglePrev;
    private bool _showInteractionRadius = false;
    private bool _interactionRadiusTogglePrev;
    private bool _showAggroRadius = false;
    private bool _aggroRadiusTogglePrev;
    private bool _showLightingRadius = false;
    private bool _lightingRadiusTogglePrev;
    private bool _prevEscDown;
    private bool? _previousDialogState = null; // Track previous dialog state to detect transitions
    private Dictionary<string, GameCore.Dialogs.DialogGraph>? _dialogDatabase;
    private DialogService? _dialogService;
    private InGameEditorService? _editorService;
    private bool _prevF12Down;
    private bool _prevCtrlSDown;
    private string? _currentMapFileName;
    private Label? _mapInfoLabel;
    private Label? _timeWeatherLabel;
    private WeatherWindow? _weatherWindow;
    private bool _prevTDown;
    private bool _prevPDown;
    private bool _prevRDown;
    private List<int> _solidTiles = new List<int>(); // No solid tiles - collision disabled
    private FileSystemWatcher? _mapFileWatcher;
    private FileSystemWatcher? _tilesLibraryFileWatcher;
    private volatile bool _reloadTileGraphicsPending = false;
    private volatile bool _reloadMapPending = false;

    // Windows API to disable context menu
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    private const int GWL_WNDPROC = -4;
    private const uint WM_CONTEXTMENU = 0x007B;
    private IntPtr _originalWndProc = IntPtr.Zero;

    public IsoEngineGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = Configuration.WindowTitle;
    }

    protected override void Initialize()
    {
        // Set window size from configuration
        _graphics.PreferredBackBufferWidth = Configuration.WindowWidth;
        _graphics.PreferredBackBufferHeight = Configuration.WindowHeight;
        _graphics.ApplyChanges();

        // Initialize Myra
        MyraEnvironment.Game = this;

        // Validate GameContent path (ensures consistency with editor)
        GameContentPathHelper.ValidateGameContentPath();

        // Register core services
        RegisterServices();

        // Initialize all registered game services
        foreach (var service in ServiceLocator.GetAllGameServices())
        {
            service.Initialize();
        }

        base.Initialize();
    }

    private void DisableContextMenu()
    {
        try
        {
            var windowHandle = Window.Handle;
            if (windowHandle != IntPtr.Zero)
            {
                // Install a custom window procedure to intercept WM_CONTEXTMENU
                _originalWndProc = GetWindowLong(windowHandle, GWL_WNDPROC);
                var newWndProc = Marshal.GetFunctionPointerForDelegate(new WndProcDelegate(CustomWndProc));
                SetWindowLong(windowHandle, GWL_WNDPROC, newWndProc);
            }
        }
        catch
        {
            // Ignore errors if window handle is not available yet
        }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Block context menu messages
        if (msg == WM_CONTEXTMENU)
        {
            return IntPtr.Zero; // Return 0 to prevent context menu from showing
        }
        
        // Call original window procedure for all other messages
        if (_originalWndProc != IntPtr.Zero)
        {
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }
        
        return IntPtr.Zero;
    }

    /// <summary>
    /// Registers all core game services with the service locator.
    /// Services that implement IGameService are automatically tracked for lifecycle methods.
    /// </summary>
    private void RegisterServices()
    {
        // Register logger (implements both ILogger and IGameService)
        var logger = new DebugLogger();
        ServiceLocator.Register<ILogger>(logger);

        // Register time manager (implements IGameService)
        var timeManager = new GameTimeManager();
        ServiceLocator.Register<GameTimeManager>(timeManager);

        // Register input service (implements IInputService and IGameService)
        var inputService = new MonoGameInputService();
        ServiceLocator.Register<IInputService>(inputService);

        // Try to load map from file, otherwise create default test map
        _tileMap = LoadTileMapFromFile() ?? CreateDefaultTileMap();
        
        if (_tileMap == null)
        {
            // Fallback: create default test map with default tile size (isometric tiles are typically 2:1 ratio)
        _tileMap = new IsometricTileMap(
                GameConstants.Tiles.DefaultMapWidth, 
                GameConstants.Tiles.DefaultMapHeight, 
                GameConstants.Tiles.DefaultWidth, 
                GameConstants.Tiles.DefaultHeight);
        CreateTestMap(_tileMap);
        }

        // Create camera positioned at center of map
        var mapCenterWorld = _tileMap.WorldToScreen(new Vector2(_tileMap.Width / 2.0f, _tileMap.Height / 2.0f));
        
        // CRITICAL: Camera zoom must always be 1.0f to match editor scale
        // The editor uses zoom 1.0f, and tiles are rendered at their actual dimensions
        // from the map file (TileWidth x TileHeight). Any deviation will cause scale mismatch.
        // This ensures perfect visual consistency: what you see in the editor matches the game.
        const float cameraZoom = 1.0f;
        
        _camera = new Camera2D
        {
            Position = mapCenterWorld,
            Zoom = cameraZoom
        };

        // Create collision service
        _collisionService = new CollisionService();
        if (_tileMap != null)
        {
        ConfigureCollisionService(_collisionService);
            
            // Export the default map to world.json if it doesn't exist
            var worldMapPath = GameContentPathHelper.GetWorldMapPath();
            if (worldMapPath != null && !File.Exists(worldMapPath))
            {
                GameClient.Utilities.GameDataExporter.ExportTileMap(_tileMap, _solidTiles);
            }
        }
        ServiceLocator.Register<CollisionService>(_collisionService);
        ServiceLocator.Register<IGameService>(_collisionService);

        // Create interaction service
        _interactionService = new InteractionService();
        ServiceLocator.Register<InteractionService>(_interactionService);
        ServiceLocator.Register<IGameService>(_interactionService);

        // Create dialog service (requires logger)
        _dialogService = new DialogService(logger);
        ServiceLocator.Register<DialogService>(_dialogService);
        ServiceLocator.Register<IGameService>(_dialogService);

        // Create combat service (requires logger)
        var combatService = new CombatService(logger);
        ServiceLocator.Register<CombatService>(combatService);
        ServiceLocator.Register<IGameService>(combatService);

        // Create enemy service (requires logger)
        var gameStateService = ServiceLocator.Get<GameStateService>();
        var enemyService = new EnemyService(logger, gameStateService, _itemDatabase);
        ServiceLocator.Register<EnemyService>(enemyService);
        ServiceLocator.Register<IGameService>(enemyService);
        
        // Set item database in enemy service after it's loaded (will be done in LoadContent)

        // Create game state service (start at title screen)
        _gameStateService = new GameStateService();
        ServiceLocator.Register<GameStateService>(_gameStateService);
        ServiceLocator.Register<IGameService>(_gameStateService);

        // Create save/load service
        var saveGameService = new SaveGameService();
        ServiceLocator.Register<SaveGameService>(saveGameService);
        ServiceLocator.Register<IGameService>(saveGameService);

        // Load weather cycle configuration
        var configPath = Path.Combine(GameContentPathHelper.GetGameContentPath() ?? "", "weather_cycle.json");
        var config = GameCore.Configuration.WeatherCycleConfig.LoadFromFile(configPath);

        // Create day/night cycle service
        _dayNightCycle = new DayNightCycleService(timeManager);
        // Speed up day/night cycle 2x by halving the duration
        _dayNightCycle.DayDuration = config.DayNight.DayDuration / 2.0f;
        _dayNightCycle.DawnStart = config.DayNight.DawnStart;
        _dayNightCycle.DawnEnd = config.DayNight.DawnEnd;
        _dayNightCycle.DuskStart = config.DayNight.DuskStart;
        _dayNightCycle.DuskEnd = config.DayNight.DuskEnd;
        // Start the game during the day (noon - 0.5 is middle of day cycle)
        _dayNightCycle.CurrentTimeOfDay = 0.5f;
        ServiceLocator.Register<DayNightCycleService>(_dayNightCycle);
        ServiceLocator.Register<IGameService>(_dayNightCycle);

        // Try to load player start position from map file
        Vector2 playerStartWorld;
        string? mapFile = null;
        var playerStartMapsDir = GameContentPathHelper.GetMapsDirectory();
        if (playerStartMapsDir != null && Directory.Exists(playerStartMapsDir))
        {
            var mapFiles = Directory.GetFiles(playerStartMapsDir, "*.json");
            mapFile = GameCore.GameConstants.DefaultFiles.GetPreferredMapFile(mapFiles);
        }
        
        var playerStartFromFile = mapFile != null ? TileMapLoader.GetPlayerStartPosition(mapFile) : null;
        
        if (playerStartFromFile.HasValue)
        {
            // Use player start position from map file
            playerStartWorld = playerStartFromFile.Value;
        }
        else if (_tileMap != null)
        {
            // Fallback: Create player at center of map
            // No collision checks needed - all tiles are walkable
            var centerX = _tileMap.Width / 2;
            var centerY = _tileMap.Height / 2;
            playerStartWorld = new Vector2(centerX, centerY);
        }
        else
        {
            // Final fallback: Use origin
            playerStartWorld = Vector2.Zero;
        }
        
        var playerStartScreen = _tileMap?.WorldToScreen(playerStartWorld) ?? playerStartWorld;
        _player = new Player(playerStartScreen);
        
        // Player movement speed is fixed and does not scale with tile size
        _player.MovementSpeed = 150.0f;
        
        // Player size is fixed at default size, does not scale with tile size
        var fixedPlayerSize = GameConstants.Player.DefaultSize;
        _player.Size = new Vector2(fixedPlayerSize, fixedPlayerSize);
        
        // Register player so services like SaveGameService can access it
        ServiceLocator.Register<Player>(_player);
        
        // Set camera to player's starting position
        if (_camera != null)
        {
            _camera.Position = playerStartScreen;
        }

        // Create click effect renderer
        _clickEffectRenderer = new ClickEffectRenderer(_camera!);
        ServiceLocator.Register<IGameService>(_clickEffectRenderer);

        // Create lighting renderer
        _lightingRenderer = new LightingRenderer(_camera!);
        ServiceLocator.Register<IGameService>(_lightingRenderer);

        // Create lighting radius renderer for debug visualization
        _lightingRadiusRenderer = new LightingRadiusRenderer(_camera!, _lightingRenderer);
        ServiceLocator.Register<IGameService>(_lightingRadiusRenderer);

        // Create player controller
        _playerController = new PlayerControllerService(_player, _camera!, _tileMap!);
        _playerController.SetClickEffectRenderer(_clickEffectRenderer);
        ServiceLocator.Register<IGameService>(_playerController);

        // Create entity renderer
        _entityRenderer = new EntityRenderer(_camera!, _tileMap!);
        _entityRenderer.AddEntity(_player);
        ServiceLocator.Register<IGameService>(_entityRenderer);
        ServiceLocator.Register<EntityRenderer>(_entityRenderer);

        // Load items from JSON (needed before creating interactables)
        LoadItems();

        // Load dialogs from JSON (needed before creating NPCs)
        LoadDialogs();

        // Try to load entity layout from file, otherwise create test interactables
        LoadEntityLayoutFromFile();
        
        // Only create test interactables if no entities were loaded from file
        if (_interactionService != null && _interactionService.GetAllInteractables().Count() == 0)
        {
        CreateTestInteractables();
        }

        // Create collision debug renderer to visualize solid tiles
        _collisionDebugRenderer = new CollisionDebugRenderer(_camera!, _tileMap!, _collisionService!);
        ServiceLocator.Register<IGameService>(_collisionDebugRenderer);

        // Create interaction radius renderer to visualize interaction ranges
        _interactionRadiusRenderer = new InteractionRadiusRenderer(_camera!, _interactionService!);
        ServiceLocator.Register<IGameService>(_interactionRadiusRenderer);

        // Create aggro radius renderer to visualize enemy aggro ranges
        var enemyServiceForRenderer = ServiceLocator.Get<GameClient.Services.EnemyService>();
        if (enemyServiceForRenderer != null)
        {
            _aggroRadiusRenderer = new AggroRadiusRenderer(_camera!, enemyServiceForRenderer);
            ServiceLocator.Register<IGameService>(_aggroRadiusRenderer);
        }

        // Create tile map renderer
        _tileMapRenderer = new TileMapRenderer(_camera!, _tileMap!);
        ServiceLocator.Register<IGameService>(_tileMapRenderer);

        // Create camera controller (will follow player instead of WASD)
        _cameraController = new CameraController(_camera!);
        _cameraController.SetGraphicsDevice(GraphicsDevice);
        ServiceLocator.Register<IGameService>(_cameraController);

        // Create in-game editor service
        _editorService = new InGameEditorService();
        _editorService.SetTileMap(_tileMap!);
        _editorService.SetCamera(_camera!);
        ServiceLocator.Register<IGameService>(_editorService);

        // Create VFX services
        _particleSystem = new ParticleSystem(_camera!);
        ServiceLocator.Register<IGameService>(_particleSystem);
        ServiceLocator.Register<ParticleSystem>(_particleSystem);

        _screenShakeService = new ScreenShakeService(_camera!);
        ServiceLocator.Register<IGameService>(_screenShakeService);
        ServiceLocator.Register<ScreenShakeService>(_screenShakeService);

        _screenEffectRenderer = new ScreenEffectRenderer();
        ServiceLocator.Register<IGameService>(_screenEffectRenderer);

        _damageNumberRenderer = new DamageNumberRenderer(_camera!);
        ServiceLocator.Register<IGameService>(_damageNumberRenderer);

        _hitIndicatorRenderer = new HitIndicatorRenderer(_camera!, _particleSystem);
        ServiceLocator.Register<IGameService>(_hitIndicatorRenderer);

        _trailRenderer = new TrailRenderer(_camera!);
        ServiceLocator.Register<IGameService>(_trailRenderer);

        _pathIndicatorRenderer = new PathIndicatorRenderer(_camera!);
        ServiceLocator.Register<IGameService>(_pathIndicatorRenderer);

        _deathEffectService = new DeathEffectService(_particleSystem);
        ServiceLocator.Register<IGameService>(_deathEffectService);
        ServiceLocator.Register<DeathEffectService>(_deathEffectService);

        // Create projectile service and renderer for shooting
        _projectileService = new ProjectileService();
        ServiceLocator.Register<IGameService>(_projectileService);
        ServiceLocator.Register<ProjectileService>(_projectileService);

        _projectileRenderer = new ProjectileRenderer(_camera!, _projectileService);
        ServiceLocator.Register<IGameService>(_projectileRenderer);

        // Register VFX settings (default to high quality)
        var vfxSettings = GameCore.Configuration.VFXSettings.HighQuality;
        ServiceLocator.Register<GameCore.Configuration.VFXSettings>(vfxSettings);

        // Get logger to log service registration
        var log = ServiceLocator.Get<ILogger>();
        log?.Info("Core services registered.");
        log?.Info("Isometric tile map and camera initialized.");
        log?.Info("Player and collision system initialized.");
        log?.Info("VFX services registered.");
    }

    /// <summary>
    /// Configures collision service with a collision grid.
    /// </summary>
    private void ConfigureCollisionService(CollisionService collisionService)
    {
        if (_tileMap == null)
            return;
            
        collisionService.SetTileMap(_tileMap);
        
        // Try to load collision grid from file
        GameCore.Collision.CollisionGrid? collisionGrid = null;
        
        // Determine collision file path (same directory as map file, with _collision suffix)
        var worldMapPath = GameContentPathHelper.GetWorldMapPath();
        if (worldMapPath != null && File.Exists(worldMapPath))
        {
            var directory = Path.GetDirectoryName(worldMapPath);
            var fileName = Path.GetFileNameWithoutExtension(worldMapPath);
            var extension = Path.GetExtension(worldMapPath);
            var collisionFilePath = Path.Combine(directory ?? "", $"{fileName}_collision{extension}");
            
            if (File.Exists(collisionFilePath))
            {
                collisionGrid = GameCore.Collision.CollisionGridSerializer.LoadFromJson(collisionFilePath);
            }
        }
        
        // Create new grid if loading failed
        if (collisionGrid == null)
        {
            collisionGrid = new GameCore.Collision.CollisionGrid(
                worldWidth: _tileMap.Width,
                worldHeight: _tileMap.Height,
                cellSize: 1.0f);
            
            // No collision - grid is created empty (all tiles are non-solid)
            // Previously this would mark tiles 4, 5, 6, 7 as solid, but collision is now disabled
        }
        
        collisionService.SetCollisionGrid(collisionGrid);
    }

    /// <summary>
    /// Creates a village-style map with roads, buildings, and open areas.
    /// </summary>
    private void CreateTestMap(IsometricTileMap tileMap)
    {
        var random = new Random(42); // Fixed seed for consistent results

        for (int y = 0; y < tileMap.Height; y++)
        {
            for (int x = 0; x < tileMap.Width; x++)
            {
                int tileIndex;

                // Border: stone walls
                if (x == 0 || x == tileMap.Width - 1 || y == 0 || y == tileMap.Height - 1)
                {
                    tileIndex = 6; // Stone
                }
                // Main roads (horizontal and vertical)
                else if (x == tileMap.Width / 2 || y == tileMap.Height / 2)
                {
                    tileIndex = 2; // Dirt road
                }
                // Crossroads
                else if ((x == tileMap.Width / 2 - 1 || x == tileMap.Width / 2 + 1) && 
                         (y == tileMap.Height / 2 - 1 || y == tileMap.Height / 2 + 1))
                {
                    tileIndex = 2; // Dirt road
                }
                // Side roads (every 4 tiles)
                else if (x % 4 == 0 || y % 4 == 0)
                {
                    tileIndex = 2; // Dirt road
                }
                // Buildings (stone structures)
                else if (IsBuildingLocation(x, y, tileMap.Width, tileMap.Height))
                {
                    tileIndex = 6; // Stone building
                }
                // Village square/plaza (center area)
                else if (x >= tileMap.Width / 2 - 2 && x <= tileMap.Width / 2 + 2 &&
                         y >= tileMap.Height / 2 - 2 && y <= tileMap.Height / 2 + 2)
                {
                    tileIndex = 2; // Dirt plaza
                }
                // Decorative elements - occasional dark grass patches
                else if (random.Next(100) < 15)
                {
                    tileIndex = 1; // Dark grass
                }
                // Default: light grass (village green)
                else
                {
                    tileIndex = 0; // Light grass
                }

                tileMap.SetTile(x, y, tileIndex);
            }
        }
    }

    /// <summary>
    /// Determines if a location should have a building.
    /// </summary>
    private bool IsBuildingLocation(int x, int y, int width, int height)
    {
        // Don't place buildings on roads or borders
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
            return false;
        if (x == width / 2 || y == height / 2)
            return false;
        if (x % 4 == 0 || y % 4 == 0)
            return false;
        if (x >= width / 2 - 2 && x <= width / 2 + 2 &&
            y >= height / 2 - 2 && y <= height / 2 + 2)
            return false;

        // Place buildings in clusters
        var buildingSpacing = 3;
        var buildingOffset = 2;
        
        // Check if this is a building location (grid pattern with offset)
        var buildingX = (x - buildingOffset) % buildingSpacing;
        var buildingY = (y - buildingOffset) % buildingSpacing;
        
        // Create building clusters (2x2 or 3x3 buildings)
        if (buildingX == 0 && buildingY == 0)
        {
            // Main building
            return true;
        }
        else if (buildingX == 1 && buildingY == 0 && (x + y) % 6 < 3)
        {
            // Adjacent building (some clusters)
            return true;
        }
        else if (buildingX == 0 && buildingY == 1 && (x + y) % 6 < 3)
        {
            // Adjacent building (some clusters)
            return true;
        }

        return false;
    }

    protected override void LoadContent()
    {
        // Disable context menu on game window (call after window is created)
        DisableContextMenu();

        // Create sprite batch for rendering
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Load tile map renderer content
        _tileMapRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        
        // Load custom tile graphics if available
        ReloadTileGraphics();
        
        // Set up file watchers to reload tiles when map file or tiles library changes
        SetupMapFileWatcher();
        SetupTilesLibraryFileWatcher();
        
        // Load entity renderer content
        _entityRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        
        // Load collision debug renderer content
        _collisionDebugRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        
        // Load interaction radius renderer content
        _interactionRadiusRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        
        // Load aggro radius renderer content
        _aggroRadiusRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        
        // Load click effect renderer content
        _clickEffectRenderer?.LoadContent(GraphicsDevice, _spriteBatch);

        // Load lighting renderer content (pass Content manager for shader loading)
        _lightingRenderer?.LoadContent(GraphicsDevice, _spriteBatch, Content);

        // Load lighting radius renderer content
        _lightingRadiusRenderer?.LoadContent(GraphicsDevice, _spriteBatch);

        // Load VFX services content
        _particleSystem?.LoadContent(GraphicsDevice, _spriteBatch);
        _screenEffectRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        _damageNumberRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        _hitIndicatorRenderer?.LoadContent(GraphicsDevice, _spriteBatch);

        // Create weather system (requires GraphicsDevice, so must be after LoadContent)
        if (_dayNightCycle != null && _particleSystem != null && _screenEffectRenderer != null && _camera != null)
        {
            var timeManager = ServiceLocator.Get<GameTimeManager>();
            if (timeManager != null)
            {
                var configPath = Path.Combine(GameContentPathHelper.GetGameContentPath() ?? "", "weather_cycle.json");
                var config = GameCore.Configuration.WeatherCycleConfig.LoadFromFile(configPath);

                _weatherSystem = new WeatherSystem(
                    timeManager,
                    _particleSystem,
                    _screenEffectRenderer,
                    _camera,
                    GraphicsDevice);

                _weatherSystem.MinChangeInterval = config.Weather.MinChangeInterval;
                _weatherSystem.MaxChangeInterval = config.Weather.MaxChangeInterval;
                _weatherSystem.ProbabilityClear = config.Weather.Probabilities.Clear;
                _weatherSystem.ProbabilityLightRain = config.Weather.Probabilities.LightRain;
                _weatherSystem.ProbabilityHeavyRain = config.Weather.Probabilities.HeavyRain;
                _weatherSystem.ProbabilitySnow = config.Weather.Probabilities.Snow;
                _weatherSystem.ProbabilityFog = config.Weather.Probabilities.Fog;

                ServiceLocator.Register<WeatherSystem>(_weatherSystem);
                ServiceLocator.Register<IGameService>(_weatherSystem);
            }
        }

        // Load projectile renderer content
        _projectileRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        _trailRenderer?.LoadContent(GraphicsDevice, _spriteBatch);
        _pathIndicatorRenderer?.LoadContent(GraphicsDevice, _spriteBatch);

        // Load lights from file instead of hardcoded generation
        // These lights maintain their colors regardless of day/night cycle
        if (_lightingRenderer != null && _tileMap != null)
        {
            var lightsPath = GameContentPathHelper.GetWorldLightsPath();
            if (lightsPath != null && File.Exists(lightsPath))
            {
                var loadedLights = GameCore.Rendering.LightLoader.LoadFromJson(lightsPath, _tileMap);
                foreach (var light in loadedLights)
                {
                    _lightingRenderer.Lights.Add(light);
                }
            }
            // If no lights file exists, no lights will be loaded (empty world)
        }

        // Create orange light attached to player (fades on at night, off during day)
        if (_lightingRenderer != null && _player != null && _tileMap != null)
        {
            var playerScreenPos = _player.Position; // Player position is already in screen coordinates
            _playerLight = new GameCore.Rendering.Light(
                playerScreenPos,
                new Color(255, 150, 50, 255), // Orange color (R:255, G:150, B:50)
                120.0f, // Radius
                0.0f); // Intensity starts at 0 (will be updated based on time of day)
            _lightingRenderer.Lights.Add(_playerLight);
        }

        // Set graphics device for player controller (needed for mouse coordinate conversion)
        _playerController?.SetGraphicsDevice(GraphicsDevice);

        // Set up editor service
        _editorService?.SetGraphicsDevice(GraphicsDevice);
        _editorService?.SetSpriteBatch(_spriteBatch);
        // Note: Font loading would go here if we had a font asset

        // Create Myra desktop and UI
        _desktop = new Desktop();

        // Create root panel
        var rootPanel = new Panel
        {
            Width = Configuration.WindowWidth,
            Height = Configuration.WindowHeight
        };

        // Create debug label for input display (top-left)
        _debugLabel = new Label
        {
            Text = "Input: Movement=(0, 0) | Actions: None",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextColor = Color.LightGreen,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 200)), // Semi-transparent black background
            Padding = new Myra.Graphics2D.Thickness(6, 4, 6, 4),
            Margin = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
        };

        // Create interaction label (bottom-center) - shows item descriptions only when available
        _interactionLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextColor = Color.Yellow,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 220)), // Semi-transparent black background for item descriptions
            Padding = new Myra.Graphics2D.Thickness(12, 8, 12, 8),
            Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 50),
            Visible = false // Start hidden, only show when there's a description
        };

        // Create hover name tag label (initially hidden)
        // Use absolute positioning (Left/Top) so we can place it directly above entities
        _hoverNameTag = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextColor = Color.White,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 180)), // Semi-transparent black background
            Padding = new Myra.Graphics2D.Thickness(4, 2, 4, 2),
            Visible = false
        };

        // Create map info label (top-right)
        _mapInfoLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            TextColor = Color.Cyan,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 200)), // Semi-transparent black background
            Padding = new Myra.Graphics2D.Thickness(6, 4, 6, 4),
            Margin = new Myra.Graphics2D.Thickness(0, 10, 10, 0)
        };

        // Create time and weather label (lower-left)
        _timeWeatherLabel = new Label
        {
            Text = "Time: 12:00 | Weather: Clear",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextColor = Color.LightYellow,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 200)), // Semi-transparent black background
            Padding = new Myra.Graphics2D.Thickness(6, 4, 6, 4),
            Margin = new Myra.Graphics2D.Thickness(10, 0, 0, 10)
        };

        rootPanel.Widgets.Add(_debugLabel);
        rootPanel.Widgets.Add(_interactionLabel);
        rootPanel.Widgets.Add(_hoverNameTag);
        rootPanel.Widgets.Add(_mapInfoLabel);
        rootPanel.Widgets.Add(_timeWeatherLabel);
        _desktop.Root = rootPanel;

        // Create inventory window
        if (_player != null)
        {
            _inventoryWindow = new InventoryWindow(_player);
            _inventoryWindow.CreateUI(_desktop);
            _inventoryWindow.SetGraphics(GraphicsDevice, _spriteBatch);
            
            // Set inventory check function for player controller (includes both player and enemy inventories)
            if (_playerController != null)
            {
                _playerController.SetInventoryOpenCheck(() => 
                    (_inventoryWindow?.IsVisible ?? false) || 
                    (_enemyInventoryWindow?.IsVisible ?? false));
            }
        }

        // Create enemy inventory window
        if (_player != null)
        {
            _enemyInventoryWindow = new EnemyInventoryWindow(_player);
            _enemyInventoryWindow.CreateUI(_desktop);
            _enemyInventoryWindow.SetGraphics(GraphicsDevice, _spriteBatch);
            
            // Link the two inventory windows for cross-window drag and drop
            if (_inventoryWindow != null && _enemyInventoryWindow != null)
            {
                _inventoryWindow.SetEnemyInventoryWindow(_enemyInventoryWindow);
                _enemyInventoryWindow.SetPlayerInventoryWindow(_inventoryWindow);
            }
        }

        // Create dialog window
        _dialogWindow = new DialogWindow();
        _dialogWindow.CreateUI(_desktop);

        // Create weather window
        _weatherWindow = new WeatherWindow();
        _weatherWindow.CreateUI(_desktop);

        // Create options window
        _optionsWindow = new OptionsWindow(_graphics);
        _optionsWindow.CreateUI(_desktop);

        // Create title and pause screens
        CreateTitleScreenUI(rootPanel);
        CreatePauseMenuUI(rootPanel);
    }

    protected override void Update(GameTime gameTime)
    {
        // Check if map needs to be reloaded (from file watcher)
        if (_reloadMapPending)
        {
            _reloadMapPending = false;
            try
            {
                ReloadMap();
            }
            catch (Exception ex)
            {
                var log = ServiceLocator.Get<ILogger>();
                log?.Warning($"Error reloading map: {ex.Message}");
            }
        }
        
        // Check if tile graphics need to be reloaded (from file watcher)
        if (_reloadTileGraphicsPending)
        {
            _reloadTileGraphicsPending = false;
            try
            {
                ReloadTileGraphics();
            }
            catch (Exception ex)
            {
                var log = ServiceLocator.Get<ILogger>();
                log?.Warning($"Error reloading tile graphics: {ex.Message}");
            }
        }
        
        // First, check if dialog window was closed via close button - handle this BEFORE checking pause state
        // This ensures the dialog is ended before we compute isPausedByUI
        if (_gameStateService?.CurrentState == GameState.GameScreen &&
            _dialogService != null && _dialogWindow != null)
        {
            // Fail-safe: If dialog is active but window is hidden, the close button was clicked
            // End the dialog immediately so the game can unpause this frame
            if (_dialogService.IsDialogActive && !_dialogWindow.IsVisible)
            {
                _dialogService.EndDialog();
            }
        }
        
        // Check if dialog, inventory, enemy inventory, or weather window is active - if so, pause gameplay services
        // When dialog/inventory/weather window ends, the game automatically unpauses
        // by updating all services normally in the else branch below
        bool isDialogActive = _dialogService?.IsDialogActive ?? false;
        bool isInventoryOpen = _inventoryWindow?.IsVisible ?? false;
        bool isEnemyInventoryOpen = _enemyInventoryWindow?.IsVisible ?? false;
        bool isWeatherWindowOpen = _weatherWindow?.IsVisible ?? false;
        bool isPausedByUI = isDialogActive || isInventoryOpen || isEnemyInventoryOpen || isWeatherWindowOpen;
        
        // Update all registered game services
        // When dialog or inventory is active, only update essential services (DialogService, InputService, GameTimeManager, GameStateService)
        // When dialog/inventory is NOT active, update all services normally to unpause the game
        // Note: PlayerControllerService is always updated so it can handle pause/resume state properly
        foreach (var service in ServiceLocator.GetAllGameServices())
        {
            // Skip gameplay services when dialog or inventory is active
            if (isPausedByUI)
            {
                // Always update PlayerControllerService so it can handle pause/resume state
                if (service is PlayerControllerService)
                {
                    service.Update(gameTime);
                }
                // Only update other services needed for UI interaction
                else if (service is DialogService || 
                    service is IInputService || 
                    service is GameTimeManager || 
                    service is GameStateService ||
                    service is DayNightCycleService) // Day/night cycle should continue even when paused
                {
                    service.Update(gameTime);
                }
                // Skip all other gameplay services (EnemyService, etc.)
            }
            else
            {
                // UI is not active - update all services normally (game is unpaused)
                service.Update(gameTime);
            }
        }

        // Update lighting ambient color based on time of day
        if (_lightingRenderer != null && _dayNightCycle != null)
        {
            _lightingRenderer.AmbientColor = _dayNightCycle.GetAmbientColor();
        }

        // Update player light position and intensity based on day/night cycle
        if (_playerLight != null && _player != null && _dayNightCycle != null)
        {
            // Update light position to follow player
            _playerLight.Position = _player.Position;

            // Calculate light intensity based on time of day
            // Bright at night (1.0), dim/off during day (0.0), smooth transition during dawn/dusk
            float normalizedTime = _dayNightCycle.CurrentTimeOfDay;
            float intensity = 0.0f;

            if (_dayNightCycle.IsNight())
            {
                // Full night - maximum intensity
                intensity = 1.0f;
            }
            else if (_dayNightCycle.IsDay())
            {
                // Full day - no light (or very dim)
                intensity = 0.0f;
            }
            else if (_dayNightCycle.IsDawn())
            {
                // Dawn: fade from bright (night) to dim (day) with smooth curve
                var t = (normalizedTime - _dayNightCycle.DawnStart) / (_dayNightCycle.DawnEnd - _dayNightCycle.DawnStart);
                t = Math.Clamp(t, 0.0f, 1.0f);
                // Apply smooth step for smoother transition
                t = t * t * (3.0f - 2.0f * t);
                intensity = 1.0f - t; // Fade from 1.0 to 0.0 during dawn
            }
            else if (_dayNightCycle.IsDusk())
            {
                // Dusk: fade from dim (day) to bright (night) with smooth curve
                var t = (normalizedTime - _dayNightCycle.DuskStart) / (_dayNightCycle.DuskEnd - _dayNightCycle.DuskStart);
                t = Math.Clamp(t, 0.0f, 1.0f);
                // Apply smooth step for smoother transition
                t = t * t * (3.0f - 2.0f * t);
                intensity = t; // Fade from 0.0 to 1.0 during dusk
            }

            // Apply smooth curve for better transitions
            intensity = Math.Clamp(intensity, 0.0f, 1.0f);
            _playerLight.Intensity = intensity;
        }


        // Camera no longer follows player - it's controlled by WASD
        // Player moves independently via mouse clicks

        // Update debug label with input information
        UpdateDebugLabel();
        
        // Update map info label
        UpdateMapInfoLabel();

        // Update time and weather label
        UpdateTimeWeatherLabel();

        // Ensure title/pause panel visibility matches game state and options window
        if (_gameStateService != null)
        {
            if (_titleScreenPanel != null)
            {
                _titleScreenPanel.Visible =
                    _gameStateService.CurrentState == GameState.TitleScreen &&
                    (_optionsWindow == null || !_optionsWindow.IsVisible);
            }

            if (_pauseMenuPanel != null)
            {
                _pauseMenuPanel.Visible =
                    _gameStateService.CurrentState == GameState.PauseScreen &&
                    (_optionsWindow == null || !_optionsWindow.IsVisible);
            }

        }

        // Update interaction label and hover detection only when in game and not paused by UI
        if (_gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            UpdateInteractionLabel();
            UpdateHoverDetection();
        }

        // Handle inventory toggle, pause, and save/load keys
        var inputService = ServiceLocator.Get<IInputService>();
        var keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

        // Pause / resume with Escape when in game or pause (edge-detected to avoid flicker)
        bool escDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape);
        if (escDown && !_prevEscDown)
        {
            if (_gameStateService?.CurrentState == GameState.GameScreen)
            {
                _gameStateService.SetState(GameState.PauseScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = true;
            }
            else if (_gameStateService?.CurrentState == GameState.PauseScreen)
            {
                _gameStateService.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        }
        _prevEscDown = escDown;

        // Handle inventory toggle - allow toggling even when dialog is active (closing inventory resumes game)
        if (inputService?.IsActionPressed(GameAction.OpenInventory) == true &&
            _gameStateService?.CurrentState == GameState.GameScreen)
        {
            _inventoryWindow?.Toggle();
        }

        // Handle weather window toggle with T key (edge-detected)
        bool tDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.T);
        if (tDown && !_prevTDown && _gameStateService?.CurrentState == GameState.GameScreen)
        {
            _weatherWindow?.Toggle();
        }
        _prevTDown = tDown;

        // Cycle weather with P key (edge-detected)
        bool pDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.P);
        if (pDown && !_prevPDown && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            CycleWeather();
        }
        _prevPDown = pDown;

        // Respawn player with R key when dead (edge-detected)
        bool rDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R);
        if (rDown && !_prevRDown && _gameStateService?.CurrentState == GameState.GameScreen && _player?.Stats?.IsDead == true)
        {
            RespawnPlayer();
        }
        _prevRDown = rDown;

        // Toggle collision on/off with C key (only in game and not paused by UI)
        bool cDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C);
        if (cDown && !_collisionTogglePrev && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            if (_collisionService != null)
            {
                _collisionService.IsEnabled = !_collisionService.IsEnabled;
                // Also toggle debug overlay to show collision state
                _showCollisionDebug = _collisionService.IsEnabled;
            }
        }
        _collisionTogglePrev = cDown;

        // Toggle interaction radius visualization with F1 key (only in game and not paused by UI)
        bool f1Down = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F1);
        if (f1Down && !_interactionRadiusTogglePrev && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            _showInteractionRadius = !_showInteractionRadius;
            if (_interactionRadiusRenderer != null)
            {
                _interactionRadiusRenderer.IsVisible = _showInteractionRadius;
            }
        }
        _interactionRadiusTogglePrev = f1Down;

        // Toggle lighting radius visualization with F2 key (only in game and not paused by UI)
        bool f2Down = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F2);
        if (f2Down && !_lightingRadiusTogglePrev && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            _showLightingRadius = !_showLightingRadius;
            if (_lightingRadiusRenderer != null)
            {
                _lightingRadiusRenderer.IsVisible = _showLightingRadius;
            }
        }
        _lightingRadiusTogglePrev = f2Down;

        // Toggle aggro radius visualization with F4 key (only in game and not paused by UI)
        bool f4Down = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4);
        if (f4Down && !_aggroRadiusTogglePrev && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            _showAggroRadius = !_showAggroRadius;
            if (_aggroRadiusRenderer != null)
            {
                _aggroRadiusRenderer.IsVisible = _showAggroRadius;
            }
        }
        _aggroRadiusTogglePrev = f4Down;

        // Simple cooldown to avoid repeated save/load on key hold
        _saveLoadCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_saveLoadCooldown < 0f) _saveLoadCooldown = 0f;

        // Handle quick save/load (F5 / F9) when in game or paused
        if (_saveLoadCooldown <= 0f)
        {
            var saveService = ServiceLocator.Get<SaveGameService>();

            if (saveService != null &&
                (_gameStateService?.CurrentState == GameState.GameScreen ||
                 _gameStateService?.CurrentState == GameState.PauseScreen))
            {
                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F5))
                {
                    saveService.SaveGame(GameCore.Save.SaveSlot.Slot1);
                    _saveLoadCooldown = 0.5f;
                }
                else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F9))
                {
                    saveService.LoadGame(GameCore.Save.SaveSlot.Slot1);
                    _saveLoadCooldown = 0.5f;
                }
            }
        }

        // Toggle editor mode with F12 (edge-detected)
        bool f12Down = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F12);
        if (f12Down && !_prevF12Down && _gameStateService?.CurrentState == GameState.GameScreen)
        {
            if (_editorService != null)
            {
                var wasEditorMode = _editorService.IsEditorMode;
                _editorService.IsEditorMode = !_editorService.IsEditorMode;
                
                // Save map when exiting editor mode
                if (wasEditorMode && !_editorService.IsEditorMode)
                {
                    SaveCurrentMap();
                }
            }
        }
        _prevF12Down = f12Down;

        // Save map with Ctrl+S (when in game and not paused by UI) - edge detected to avoid repeated saves
        bool ctrlKeyDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) || 
                           keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);
        bool sKeyDown = keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.S);
        bool ctrlSDown = ctrlKeyDown && sKeyDown;
        if (ctrlSDown && !_prevCtrlSDown && _gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            SaveCurrentMap();
        }
        _prevCtrlSDown = ctrlSDown;

        // Update inventory display if visible
        if (_inventoryWindow?.IsVisible == true)
        {
            _inventoryWindow.Update();
            _inventoryWindow.UpdateDisplay();
        }

        // Update enemy inventory display if visible
        if (_enemyInventoryWindow?.IsVisible == true)
        {
            _enemyInventoryWindow.Update();
            _enemyInventoryWindow.UpdateDisplay();
            
            // Check if player moved away from the enemy and close the window if so
            _enemyInventoryWindow.CheckDistanceAndClose(80.0f); // Close radius of 80 pixels
        }

        // Handle clicking on dead enemies to open their inventory
        if (!isPausedByUI && _gameStateService?.CurrentState == GameState.GameScreen)
        {
            var deadEnemyInputService = ServiceLocator.Get<IInputService>();
            if (deadEnemyInputService != null && deadEnemyInputService.IsLeftClickPressed && GraphicsDevice != null && _enemyInventoryWindow != null && _camera != null)
            {
                var mouseScreenPos = new Vector2(deadEnemyInputService.MousePosition.X, deadEnemyInputService.MousePosition.Y);
                var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                
                var enemyService = ServiceLocator.Get<EnemyService>();
                if (enemyService != null)
                {
                    const float DeadEnemyClickRadius = 30.0f; // Click radius for dead enemies
                    bool foundEnemy = false;
                    
                    // Check melee enemies
                    foreach (var enemy in enemyService.GetAllEnemies())
                    {
                        // Check if enemy is dead and within click radius
                        if (enemy.Stats.IsDead)
                        {
                            var distanceToClick = Vector2.Distance(mouseWorldPos, enemy.Position);
                            if (distanceToClick <= DeadEnemyClickRadius)
                            {
                                // Open enemy inventory
                                _enemyInventoryWindow.OpenForEnemy(enemy);
                                foundEnemy = true;
                                break; // Only open one inventory at a time
                            }
                        }
                    }
                    
                    // Check ranged enemies if no melee enemy was found
                    if (!foundEnemy)
                    {
                        foreach (var rangedEnemy in enemyService.GetAllRangedEnemies())
                        {
                            // Check if enemy is dead and within click radius
                            if (rangedEnemy.Stats.IsDead)
                            {
                                var distanceToClick = Vector2.Distance(mouseWorldPos, rangedEnemy.Position);
                                if (distanceToClick <= DeadEnemyClickRadius)
                                {
                                    // Open ranged enemy inventory
                                    _enemyInventoryWindow.OpenForRangedEnemy(rangedEnemy);
                                    break; // Only open one inventory at a time
                                }
                            }
                        }
                    }
                }
            }
        }

        // Update weather window display if visible
        if (_weatherWindow?.IsVisible == true)
        {
            _weatherWindow.UpdateDisplay();
        }

        // Update dialog window based on dialog service state (only in game)
        if (_gameStateService?.CurrentState == GameState.GameScreen &&
            _dialogService != null && _dialogWindow != null)
        {
            if (_dialogService.IsDialogActive)
            {
                // Dialog is active - show the window
                if (!_dialogWindow.IsVisible)
                {
                    _dialogWindow.Show(); // Show() calls UpdateDisplay() internally
                }
                // Note: UpdateDisplay is called when Show() is called and when choices are selected
                // This prevents recreating buttons every frame which could interfere with clicks
                // The VisibleChanged event handler in DialogWindow will handle close button clicks
            }
            else
            {
                // Dialog is not active - ensure window is hidden and doesn't block input
                if (_dialogWindow.IsVisible)
                {
                    _dialogWindow.Hide();
                }
                
                // If dialog just ended, ensure player controller is reset
                // The dialog window's Hide() already clears its content, so input should be released
            }
        }
        
        // Track dialog state transitions to clear player state when dialog ends
        if (_gameStateService?.CurrentState == GameState.GameScreen && _dialogService != null)
        {
            bool currentDialogState = _dialogService.IsDialogActive;
            if (_previousDialogState == true && currentDialogState == false)
            {
                // Dialog just ended - force reset player controller to ensure controls unlock immediately
                _playerController?.ForceResetPauseState();
                
                // Also ensure player state is clean
                if (_player != null)
                {
                    _player.Velocity = Vector2.Zero;
                }
            }
            _previousDialogState = currentDialogState;
        }
        else if (_gameStateService?.CurrentState != GameState.GameScreen)
        {
            // Reset dialog state tracking when not in game screen
            _previousDialogState = null;
        }

        // Update Myra UI - always update to handle all windows (inventory, options, dialog)
        // Myra should only process input for visible windows, so hidden dialogs won't block
        _desktop?.UpdateInput();
        _desktop?.UpdateLayout();

        base.Update(gameTime);
    }

    /// <summary>
    /// Updates the debug label to show current input state.
    /// </summary>
    private void UpdateDebugLabel()
    {
        if (_debugLabel == null)
            return;

        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService == null)
            return;

        // Get movement vector
        var movement = inputService.GetMovementVector();
        var movementText = $"({movement.X:F2}, {movement.Y:F2})";

        // Get pressed actions
        var pressedActions = new List<string>();
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            if (inputService.IsActionDown(action))
            {
                pressedActions.Add(action.ToString());
            }
        }

        var actionsText = pressedActions.Count > 0
            ? string.Join(", ", pressedActions)
            : "None";

        // Get camera and player info
        var cameraPos = _camera?.Position ?? Vector2.Zero;
        var cameraZoom = _camera?.Zoom ?? 1.0f;
        var playerPos = _player?.Position ?? Vector2.Zero;
        var playerVel = _player?.Velocity ?? Vector2.Zero;
        
        var cameraText = $"Camera: ({cameraPos.X:F0}, {cameraPos.Y:F0}) Zoom: {cameraZoom:F2}";
        var playerText = $"Player: ({playerPos.X:F0}, {playerPos.Y:F0}) Vel: ({playerVel.X:F0}, {playerVel.Y:F0})";

        // Update label text
        _debugLabel.Text = $"Input: Movement={movementText} | Actions: {actionsText}\n{cameraText}\n{playerText}";
    }

    /// <summary>
    /// Saves the current tile map to the default world.json file.
    /// </summary>
    private void SaveCurrentMap()
    {
        if (_tileMap == null || _collisionService == null)
            return;

        try
        {
            var mapPath = GameContentPathHelper.GetWorldMapPath();
            if (mapPath == null)
            {
                var log = ServiceLocator.Get<ILogger>();
                log?.Error("Could not resolve path to world.json");
                return;
            }
            
            // Get player's current world position for saving (using fixed tile size conversion)
            Microsoft.Xna.Framework.Vector2? playerStartPos = null;
            if (_player != null)
            {
                var playerWorldPos = _tileMap.ScreenToWorldFixed(_player.Position);
                playerStartPos = playerWorldPos;
            }
            
            // Use stored solid tiles (from loaded map or defaults)
            if (TileMapLoader.SaveToJson(_tileMap, mapPath, _solidTiles, playerStartPos))
            {
                _currentMapFileName = GameConstants.DefaultFiles.WorldMap;
                var log = ServiceLocator.Get<ILogger>();
                log?.Info($"Map saved to {mapPath}");
            }
            else
            {
                var log = ServiceLocator.Get<ILogger>();
                log?.Error("Failed to save map");
            }
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Error($"Error saving map: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the map info label to show map name and location.
    /// </summary>
    private void UpdateMapInfoLabel()
    {
        if (_mapInfoLabel == null || _tileMap == null || _player == null || _camera == null)
            return;

        var mapName = _currentMapFileName ?? "Unknown Map";
        
        // Get player's world position in tile coordinates
        var playerWorldPos = _tileMap.ScreenToWorld(_player.Position);
        var tileX = (int)Math.Floor(playerWorldPos.X);
        var tileY = (int)Math.Floor(playerWorldPos.Y);
        
        // Get camera position in world coordinates
        var cameraWorldPos = _tileMap.ScreenToWorld(_camera.Position);
        
        // Get player health info
        var healthText = _player.Stats != null 
            ? $"Health: {_player.Stats.CurrentHealth}/{_player.Stats.MaxHealth}" 
            : "Health: ???";
        var statusText = _player.Stats?.IsDead == true ? " [DEAD - Press R to respawn]" : "";
        
        _mapInfoLabel.Text = $"Map: {mapName}\n" +
                             $"Location: Tile ({tileX}, {tileY})\n" +
                             $"World: ({playerWorldPos.X:F1}, {playerWorldPos.Y:F1})\n" +
                             $"{healthText}{statusText}";
    }

    /// <summary>
    /// Updates the time and weather label to show current time of day and weather.
    /// </summary>
    private void UpdateTimeWeatherLabel()
    {
        if (_timeWeatherLabel == null)
            return;

        string timeText = "00:00";
        string weatherText = "Unknown";

        if (_dayNightCycle != null)
        {
            timeText = _dayNightCycle.GetTimeString();
        }

        if (_weatherSystem != null)
        {
            weatherText = GetWeatherDisplayName(_weatherSystem.CurrentWeather);
        }

        _timeWeatherLabel.Text = $"Time: {timeText} | Weather: {weatherText}";
    }

    /// <summary>
    /// Gets the display name for a weather type.
    /// </summary>
    private string GetWeatherDisplayName(WeatherType weather)
    {
        return weather switch
        {
            WeatherType.Clear => "Clear",
            WeatherType.LightRain => "Light Rain",
            WeatherType.HeavyRain => "Heavy Rain",
            WeatherType.LightningRain => "Lightning Rain",
            WeatherType.Snow => "Snow",
            WeatherType.Blizzard => "Blizzard",
            WeatherType.Fog => "Fog",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Cycles through weather types when P key is pressed.
    /// </summary>
    private void CycleWeather()
    {
        if (_weatherSystem == null)
            return;

        // Get current weather and cycle to next
        WeatherType current = _weatherSystem.CurrentWeather;
        WeatherType next = current switch
        {
            WeatherType.Clear => WeatherType.LightRain,
            WeatherType.LightRain => WeatherType.HeavyRain,
            WeatherType.HeavyRain => WeatherType.LightningRain,
            WeatherType.LightningRain => WeatherType.Snow,
            WeatherType.Snow => WeatherType.Blizzard,
            WeatherType.Blizzard => WeatherType.Fog,
            WeatherType.Fog => WeatherType.Clear,
            _ => WeatherType.Clear
        };

        // Set the new weather (this will trigger the transition)
        _weatherSystem.CurrentWeather = next;
        
        // Disable random weather changes when manually cycling
        _weatherSystem.RandomWeatherEnabled = false;
    }

    /// <summary>
    /// Respawns the player at their starting position with full health.
    /// </summary>
    private void RespawnPlayer()
    {
        if (_player == null || _tileMap == null)
            return;

        // Reset player health to max
        _player.Stats?.Heal(_player.Stats.MaxHealth);
        
        // Move player to starting position (use default spawn point)
        var spawnPos = _tileMap.WorldToScreenFixed(new Vector2(5, 5)); // Default spawn at tile (5, 5)
        _player.Position = spawnPos;
        _player.Velocity = Vector2.Zero;
        
        // Reset player controller state
        _playerController?.ForceResetPauseState();
        
        var log = ServiceLocator.Get<ILogger>();
        log?.Info("Player respawned at starting position with full health");
    }

    /// <summary>
    /// Draws a red death overlay when the player is dead.
    /// </summary>
    private void DrawDeathOverlay()
    {
        if (_spriteBatch == null)
            return;

        // Create a pixel texture if needed
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        var viewport = GraphicsDevice.Viewport;
        var overlayRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        
        // Draw lighter semi-transparent red overlay (less intense)
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend);
        _spriteBatch.Draw(pixel, overlayRect, new Color(80, 0, 0, 100)); // Reduced alpha from 150 to 100
        _spriteBatch.End();

        // Draw "YOU DIED" text in center
        var font = _debugLabel?.Font;
        if (font != null)
        {
            string deathText = "YOU DIED";
            string respawnText = "Press R to Respawn";
            
            var deathSize = font.MeasureString(deathText);
            var respawnSize = font.MeasureString(respawnText);
            
            float deathX = (viewport.Width - deathSize.X) / 2;
            float deathY = viewport.Height / 2 - 30;
            float respawnX = (viewport.Width - respawnSize.X) / 2;
            float respawnY = viewport.Height / 2 + 10;
            
            _spriteBatch.Begin();
            // Death text with shadow
            font.DrawText(_spriteBatch, deathText, new Vector2(deathX + 2, deathY + 2), Color.Black);
            font.DrawText(_spriteBatch, deathText, new Vector2(deathX, deathY), Color.Red);
            // Respawn text with shadow
            font.DrawText(_spriteBatch, respawnText, new Vector2(respawnX + 1, respawnY + 1), Color.Black);
            font.DrawText(_spriteBatch, respawnText, new Vector2(respawnX, respawnY), Color.White);
            _spriteBatch.End();
        }

        pixel.Dispose();
    }

    /// <summary>
    /// Draws the player's health bar on screen.
    /// </summary>
    private void DrawHealthBar()
    {
        if (_spriteBatch == null || _player?.Stats == null)
            return;

        // Create a pixel texture
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        var viewport = GraphicsDevice.Viewport;
        
        // Health bar dimensions and position (upper-left, below debug panel)
        const int barWidth = 200;
        const int barHeight = 20;
        const int padding = 10;
        const int borderSize = 2;
        const int topOffset = 95; // Space below the debug panel
        
        int barX = padding;
        int barY = topOffset;
        
        // Calculate health percentage
        float healthPercent = _player.Stats.MaxHealth > 0 
            ? (float)_player.Stats.CurrentHealth / _player.Stats.MaxHealth 
            : 0f;
        int filledWidth = (int)(barWidth * healthPercent);
        
        // Health bar color (green to yellow to red based on health)
        Color healthColor;
        if (healthPercent > 0.6f)
            healthColor = new Color(50, 200, 50); // Green
        else if (healthPercent > 0.3f)
            healthColor = new Color(220, 180, 50); // Yellow
        else
            healthColor = new Color(200, 50, 50); // Red
        
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend);
        
        // Draw border (dark background)
        var borderRect = new Rectangle(barX - borderSize, barY - borderSize, barWidth + borderSize * 2, barHeight + borderSize * 2);
        _spriteBatch.Draw(pixel, borderRect, new Color(20, 20, 20, 220));
        
        // Draw background (empty health)
        var bgRect = new Rectangle(barX, barY, barWidth, barHeight);
        _spriteBatch.Draw(pixel, bgRect, new Color(60, 20, 20, 200));
        
        // Draw filled health
        if (filledWidth > 0)
        {
            var healthRect = new Rectangle(barX, barY, filledWidth, barHeight);
            _spriteBatch.Draw(pixel, healthRect, healthColor);
        }
        
        _spriteBatch.End();
        
        // Draw health text using FontStashSharp
        var font = _debugLabel?.Font;
        if (font != null)
        {
            string healthText = $"{_player.Stats.CurrentHealth}/{_player.Stats.MaxHealth}";
            var textSize = font.MeasureString(healthText);
            float textX = barX + (barWidth - textSize.X) / 2;
            float textY = barY + (barHeight - textSize.Y) / 2;
            
            _spriteBatch.Begin();
            font.DrawText(_spriteBatch, healthText, new Vector2(textX + 1, textY + 1), Color.Black); // Shadow
            font.DrawText(_spriteBatch, healthText, new Vector2(textX, textY), Color.White);
            _spriteBatch.End();
        }
        
        pixel.Dispose();
    }

    /// <summary>
    /// Updates the interaction label to show descriptions only when available.
    /// </summary>
    private void UpdateInteractionLabel()
    {
        if (_interactionLabel == null || _player == null || _interactionService == null)
            return;

        var nearest = _interactionService.GetNearestInteractable(_player);
        if (nearest != null && !string.IsNullOrWhiteSpace(nearest.Description))
        {
            // Only show the description, no interaction prompts
            _interactionLabel.Text = nearest.Description;
            _interactionLabel.TextColor = Color.Yellow;
            _interactionLabel.Visible = true;
        }
        else
        {
            // Hide the label when there's no description
            _interactionLabel.Text = "";
            _interactionLabel.Visible = false;
        }
    }

    /// <summary>
    /// Creates the title screen UI (New Game, Continue, Options, Quit).
    /// </summary>
    private void CreateTitleScreenUI(Panel rootPanel)
    {
        _titleScreenPanel = new Panel
        {
            Width = Configuration.WindowWidth,
            Height = Configuration.WindowHeight,
            // Opaque black so the game is not visible behind the main menu
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 255)),
            Visible = true // Show title screen on startup
        };

        var centerPanel = new VerticalStackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };

        var titleLabel = new Label
        {
            Text = "IsoEngine",
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var versionLabel = new Label
        {
            Text = "version 002",
            TextColor = Color.Gray,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var newGameButton = new TextButton { Text = "New Game", Width = 200 };
        newGameButton.Click += (s, e) =>
        {
            _gameStateService?.SetState(GameState.GameScreen);
            if (_titleScreenPanel != null)
                _titleScreenPanel.Visible = false;
        };

        var continueButton = new TextButton { Text = "Continue", Width = 200 };
        continueButton.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            if (saveService == null)
                return;

            // Find the most recent save slot (1-3)
            var bestSlot = GameCore.Save.SaveSlot.Slot1;
            var bestTime = System.DateTime.MinValue;
            bool anyFound = false;

            for (int i = 1; i <= 3; i++)
            {
                var slot = (GameCore.Save.SaveSlot)i;
                var path = System.IO.Path.Combine("GameContent/saves", $"save{i}.json");
                if (System.IO.File.Exists(path))
                {
                    anyFound = true;
                    var time = System.IO.File.GetLastWriteTime(path);
                    if (time > bestTime)
                    {
                        bestTime = time;
                        bestSlot = slot;
                    }
                }
            }

            if (!anyFound)
                return;

            if (saveService.LoadGame(bestSlot))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_titleScreenPanel != null)
                    _titleScreenPanel.Visible = false;
            }
        };

        // Enable continue only if at least one save file exists
        bool hasAnySave = false;
        for (int i = 1; i <= 3; i++)
        {
            var path = System.IO.Path.Combine("GameContent/saves", $"save{i}.json");
            if (System.IO.File.Exists(path))
            {
                hasAnySave = true;
                break;
            }
        }
        continueButton.Enabled = hasAnySave;

        var optionsButton = new TextButton { Text = "Options", Width = 200 };
        optionsButton.Click += (s, e) =>
        {
            // Hide title while options are open
            if (_titleScreenPanel != null)
                _titleScreenPanel.Visible = false;
            _optionsWindow?.Show();
        };

        var quitButton = new TextButton { Text = "Quit", Width = 200 };
        quitButton.Click += (s, e) => { Exit(); };

        centerPanel.Widgets.Add(titleLabel);
        centerPanel.Widgets.Add(versionLabel);
        centerPanel.Widgets.Add(newGameButton);
        centerPanel.Widgets.Add(continueButton);
        centerPanel.Widgets.Add(optionsButton);
        centerPanel.Widgets.Add(quitButton);

        _titleScreenPanel.Widgets.Add(centerPanel);
        rootPanel.Widgets.Add(_titleScreenPanel);
    }

    /// <summary>
    /// Creates the pause menu UI.
    /// </summary>
    private void CreatePauseMenuUI(Panel rootPanel)
    {
        _pauseMenuPanel = new Panel
        {
            Width = Configuration.WindowWidth,
            Height = Configuration.WindowHeight,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 160)),
            Visible = false
        };

        var centerPanel = new VerticalStackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };

        var resumeButton = new TextButton { Text = "Resume", Width = 200 };
        resumeButton.Click += (s, e) =>
        {
            _gameStateService?.SetState(GameState.GameScreen);
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.Visible = false;
        };

        // Save slots
        var saveSlot1Button = new TextButton { Text = "Save Slot 1", Width = 200 };
        saveSlot1Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            saveService?.SaveGame(GameCore.Save.SaveSlot.Slot1);
        };

        var saveSlot2Button = new TextButton { Text = "Save Slot 2", Width = 200 };
        saveSlot2Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            saveService?.SaveGame(GameCore.Save.SaveSlot.Slot2);
        };

        var saveSlot3Button = new TextButton { Text = "Save Slot 3", Width = 200 };
        saveSlot3Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            saveService?.SaveGame(GameCore.Save.SaveSlot.Slot3);
        };

        // Load slots
        var loadSlot1Button = new TextButton { Text = "Load Slot 1", Width = 200 };
        loadSlot1Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            if (saveService != null && saveService.LoadGame(GameCore.Save.SaveSlot.Slot1))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var loadSlot2Button = new TextButton { Text = "Load Slot 2", Width = 200 };
        loadSlot2Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            if (saveService != null && saveService.LoadGame(GameCore.Save.SaveSlot.Slot2))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var loadSlot3Button = new TextButton { Text = "Load Slot 3", Width = 200 };
        loadSlot3Button.Click += (s, e) =>
        {
            var saveService = ServiceLocator.Get<SaveGameService>();
            if (saveService != null && saveService.LoadGame(GameCore.Save.SaveSlot.Slot3))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var optionsButton = new TextButton { Text = "Options", Width = 200 };
        optionsButton.Click += (s, e) =>
        {
            // Hide pause menu while options are open
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.Visible = false;
            _optionsWindow?.Show();
        };

        var quitToTitleButton = new TextButton { Text = "Quit to Title", Width = 200 };
        quitToTitleButton.Click += (s, e) =>
        {
            _gameStateService?.SetState(GameState.TitleScreen);
            if (_pauseMenuPanel != null)
                _pauseMenuPanel.Visible = false;
            if (_titleScreenPanel != null)
                _titleScreenPanel.Visible = true;
        };

        centerPanel.Widgets.Add(resumeButton);
        centerPanel.Widgets.Add(saveSlot1Button);
        centerPanel.Widgets.Add(saveSlot2Button);
        centerPanel.Widgets.Add(saveSlot3Button);
        centerPanel.Widgets.Add(loadSlot1Button);
        centerPanel.Widgets.Add(loadSlot2Button);
        centerPanel.Widgets.Add(loadSlot3Button);
        centerPanel.Widgets.Add(optionsButton);
        centerPanel.Widgets.Add(quitToTitleButton);

        _pauseMenuPanel.Widgets.Add(centerPanel);
        rootPanel.Widgets.Add(_pauseMenuPanel);
    }

    /// <summary>
    /// Updates hover detection to show name tags when hovering over entities.
    /// </summary>
    private void UpdateHoverDetection()
    {
        if (_hoverNameTag == null || GraphicsDevice == null || _camera == null || _entityRenderer == null || _tileMap == null)
            return;

        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService == null)
            return;

        // Convert mouse position to world coordinates
        var mouseScreenPos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
        var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Check for entities under the mouse cursor
        Entity? hoveredEntity = null;
        
        // Calculate hover radius based on tile size (reuse scaling factor from player)
        const int baseHoverTileWidth = 64;
        const float baseHoverRadius = 25.0f;
        var hoverTileScale = (float)_tileMap.TileWidth / baseHoverTileWidth;
        var hoverRadius = baseHoverRadius * hoverTileScale;

        // Check interactables
        if (_interactionService != null)
        {
            foreach (var interactable in _interactionService.GetAllInteractables())
            {
                if (interactable is Entity entity && entity.IsActive)
                {
                    var distance = Vector2.Distance(mouseWorldPos, entity.Position);
                    if (distance <= hoverRadius)
                    {
                        hoveredEntity = entity;
                        break;
                    }
                }
            }
        }

        // Check enemies (if no interactable was found)
        if (hoveredEntity == null)
        {
            var enemyService = ServiceLocator.Get<GameClient.Services.EnemyService>();
            if (enemyService != null)
            {
                foreach (var enemy in enemyService.GetAllEnemies())
                {
                    if (!enemy.IsActive || enemy.Stats.IsDead)
                        continue;

                    var distance = Vector2.Distance(mouseWorldPos, enemy.Position);
                    if (distance <= hoverRadius)
                    {
                        hoveredEntity = enemy;
                        break;
                    }
                }
            }
        }

        // Update hover state
        _hoveredEntity = hoveredEntity;

        if (hoveredEntity != null)
        {
            // Get entity name
            string entityName = hoveredEntity switch
            {
                GameCore.Entities.InteractiveObject io => io.Name,
                Enemy => "Enemy",
                _ => "Entity"
            };

            _hoverNameTag.Text = entityName;
            _hoverNameTag.Visible = true;

            // Position the name tag above the entity
            var entityScreenPos = _camera.WorldToScreen(hoveredEntity.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _hoverNameTag.Left = (int)(entityScreenPos.X - 50); // Approximate center (will be adjusted by Myra)
            _hoverNameTag.Top = (int)(entityScreenPos.Y - hoveredEntity.Size.Y / 2 - 30); // Above the entity
        }
        else
        {
            _hoverNameTag.Visible = false;
        }
    }

    /// <summary>
    /// Loads dialogs from JSON files.
    /// </summary>
    private void LoadDialogs()
    {
        try
        {
            _dialogDatabase = new Dictionary<string, DialogGraph>();

            var dialogsPath = GameContentPathHelper.GetDialogsDirectory();
            if (dialogsPath != null && Directory.Exists(dialogsPath))
            {
                var dialogFiles = Directory.GetFiles(dialogsPath, "*.json");
                foreach (var file in dialogFiles)
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(file);
                        var dialog = DialogLoader.LoadDialogFromJson(jsonContent);
                        _dialogDatabase[dialog.Id] = dialog;

                        var log = ServiceLocator.Get<ILogger>();
                        log?.Info($"Loaded dialog '{dialog.Name}' (ID: {dialog.Id}) from {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        var log = ServiceLocator.Get<ILogger>();
                        log?.Error($"Error loading dialog from {file}: {ex.Message}");
                    }
                }
            }
            else
            {
                var log = ServiceLocator.Get<ILogger>();
                log?.Warning("Dialogs directory not found");
            }
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Error($"Error loading dialogs: {ex.Message}");
            _dialogDatabase = new Dictionary<string, DialogGraph>();
        }
    }

    /// <summary>
    /// Loads items from the items.json file.
    /// </summary>
    private void LoadItems()
    {
        try
        {
            var itemsJsonPath = GameContentPathHelper.GetItemsPath();
            if (itemsJsonPath != null && File.Exists(itemsJsonPath))
            {
                var jsonContent = File.ReadAllText(itemsJsonPath);
                _itemDatabase = ItemLoader.LoadItemsFromJson(jsonContent);
                
                var log = ServiceLocator.Get<ILogger>();
                log?.Info($"Loaded {_itemDatabase.Count} items from items.json");
            }
            else
            {
                // Create default items if file doesn't exist
                _itemDatabase = new Dictionary<string, Item>
                {
                    { "gold_coin", new Item("gold_coin", "Gold Coin", "A shiny gold coin.", true, 20) },
                    { "health_potion", new Item("health_potion", "Health Potion", "A red potion that restores health.", true, 20) },
                    { "sword", new Item("sword", "Iron Sword", "A basic iron sword.", true, 20) }
                };
                
                var log = ServiceLocator.Get<ILogger>();
                log?.Warning("items.json not found, using default items");
            }

            // Register item database so other services (e.g., saving/loading) can use it
            if (_itemDatabase != null)
            {
                ServiceLocator.Register<Dictionary<string, Item>>(_itemDatabase);
                
                // Also set it in enemy service for dropping items
                var enemyService = ServiceLocator.Get<EnemyService>();
                enemyService?.SetItemDatabase(_itemDatabase);
            }
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Error($"Error loading items: {ex.Message}");
            _itemDatabase = new Dictionary<string, Item>();

            // Still register an empty database so services can handle missing items gracefully
            ServiceLocator.Register<Dictionary<string, Item>>(_itemDatabase);
            
            // Set empty database in enemy service too
            var enemyService = ServiceLocator.Get<EnemyService>();
            enemyService?.SetItemDatabase(_itemDatabase);
        }
    }

    /// <summary>
    /// Creates test interactable objects in the map.
    /// </summary>
    private void CreateTestInteractables()
    {
        if (_tileMap == null || _interactionService == null || _entityRenderer == null || _itemDatabase == null)
            return;

        // Entities have fixed sizes (32x32) and do not scale with tile size

        // Create a few test objects at different locations
        // Use fixed tile size conversion so entity positions don't scale with tile size
        var interactables = new List<IInteractable>
        {
            new TestInteractable(
                _tileMap.WorldToScreenFixed(new Vector2(5, 5)),
                "Well",
                "An old stone well. The water looks clear and fresh."
            ),
            new TestInteractable(
                _tileMap.WorldToScreenFixed(new Vector2(15, 8)),
                "Chest",
                "A wooden chest. It appears to be locked."
            ),
            new TestInteractable(
                _tileMap.WorldToScreenFixed(new Vector2(8, 15)),
                "Sign",
                "A wooden signpost. It reads: 'Welcome to the Village'."
            )
        };

        // Create dialog NPC if we have the guard dialog loaded
        if (_dialogDatabase != null && _dialogDatabase.TryGetValue("guard_dialog", out var guardDialog))
        {
            interactables.Add(new DialogNPC(
                _tileMap.WorldToScreenFixed(new Vector2(12, 12)),
                "NPC: Guard",
                "A village guard stands watch. Press E to talk.",
                guardDialog
            ));
        }
        else
        {
            // Fallback to test interactable if dialog not loaded
            interactables.Add(new TestInteractable(
                _tileMap.WorldToScreenFixed(new Vector2(12, 12)),
                "NPC: Guard",
                "A village guard stands watch. 'Greetings, traveler!'"
            ));
        }

        // Add ground items if we have items loaded
        if (_itemDatabase.TryGetValue("gold_coin", out var goldCoin))
        {
            interactables.Add(new GroundItem(
                _tileMap.WorldToScreenFixed(new Vector2(6, 6)),
                goldCoin,
                5
            ));
        }

        if (_itemDatabase.TryGetValue("health_potion", out var potion))
        {
            interactables.Add(new GroundItem(
                _tileMap.WorldToScreenFixed(new Vector2(10, 10)),
                potion,
                2
            ));
        }

        if (_itemDatabase.TryGetValue("sword", out var sword))
        {
            interactables.Add(new GroundItem(
                _tileMap.WorldToScreenFixed(new Vector2(14, 14)),
                sword,
                1
            ));
        }

        foreach (var interactable in interactables)
        {
            _interactionService.RegisterInteractable(interactable);
            if (interactable is Entity entity)
            {
                // Entities maintain their fixed sizes (32x32) and do not scale with tile size
                _entityRenderer.AddEntity(entity);
            }
        }

        // Create test enemies
        CreateTestEnemies();
        
        // Export test entities to file so they can be edited (after enemies are created)
        var worldEntitiesPath = GameContentPathHelper.GetWorldEntitiesPath();
        if (worldEntitiesPath != null && !File.Exists(worldEntitiesPath) && _tileMap != null && _interactionService != null)
        {
            var allInteractables = _interactionService.GetAllInteractables().ToList();
            var enemyService = ServiceLocator.Get<EnemyService>();
            var enemies = enemyService?.GetAllEnemies().ToList();
            GameClient.Utilities.GameDataExporter.ExportEntities(allInteractables, enemies, _tileMap);
        }
    }

    /// <summary>
    /// Loads a tile map from a file, or returns null if not found.
    /// </summary>
    private IsometricTileMap? LoadTileMapFromFile()
    {
        try
        {
            // Always use GetWorldMapPath() to ensure we use the same file as the editor
            var worldMapPath = GameContentPathHelper.GetWorldMapPath();
            var log = ServiceLocator.Get<ILogger>();
            
            if (worldMapPath == null || !File.Exists(worldMapPath))
            {
                var gameContentPath = GameContentPathHelper.GetGameContentPath();
                var currentDir = Directory.GetCurrentDirectory();
                log?.Warning($"Could not find world.json. GameContent path: {gameContentPath ?? "null"}, Current dir: {currentDir}");
                return null;
            }

            // Validate we're using the source directory, not a runtime copy
            var sourceGameContentPath = GameContentPathHelper.GetGameContentPath();
            if (sourceGameContentPath != null && worldMapPath.Contains(sourceGameContentPath))
            {
                log?.Info($"LoadTileMapFromFile: Using source GameContent directory: {sourceGameContentPath}");
            }
            else
            {
                log?.Warning($"LoadTileMapFromFile: WARNING - Map path may not be from source directory! GameContent: {sourceGameContentPath}, Map: {worldMapPath}");
            }

            _currentMapFileName = Path.GetFileName(worldMapPath);
            log?.Info($"Attempting to load map from: {worldMapPath}");
            var tileMap = TileMapLoader.LoadFromJson(worldMapPath);
            if (tileMap != null)
            {
                log?.Info($"Loaded tile map from {_currentMapFileName} (Size: {tileMap.Width}x{tileMap.Height}, TileSize: {tileMap.TileWidth}x{tileMap.TileHeight})");

                // Load solid tiles from the map file (set in editor tile properties)
                var solidTiles = TileMapLoader.GetSolidTilesFromJson(worldMapPath);
                
                // Use defaults if map file doesn't specify any solid tiles
                if (solidTiles.Count == 0)
                {
                    _solidTiles = new List<int>(GameConstants.Tiles.DefaultSolidTiles);
                    log?.Info($"No solid tiles in map file, using defaults: [{string.Join(", ", _solidTiles)}]");
                }
                else
                {
                    // Store solid tiles - they will be applied in ConfigureSolidTiles after collision service is created
                    _solidTiles = solidTiles;
                    log?.Info($"Loaded {solidTiles.Count} solid tiles from map file");
                }
            }

            return tileMap;
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning($"Error loading tile map from file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a default tile map.
    /// </summary>
    private IsometricTileMap CreateDefaultTileMap()
    {
        var tileMap = new IsometricTileMap(
            GameConstants.Tiles.DefaultMapWidth, 
            GameConstants.Tiles.DefaultMapHeight, 
            GameConstants.Tiles.DefaultWidth, 
            GameConstants.Tiles.DefaultHeight);
        CreateTestMap(tileMap);
        return tileMap;
    }

    /// <summary>
    /// Loads entity layout from a file and creates entities.
    /// </summary>
    private void LoadEntityLayoutFromFile()
    {
        try
        {
            var entitiesDir = GameContentPathHelper.GetEntitiesDirectory();
            if (entitiesDir == null || !Directory.Exists(entitiesDir))
                return;

            var entityFiles = Directory.GetFiles(entitiesDir, "*.json");
            if (entityFiles.Length == 0)
                return;

            // Prefer "world_entities.json" or "entities.json", then load first file found
            var preferredFile = Array.Find(entityFiles, f => 
                Path.GetFileName(f).Equals(GameCore.GameConstants.DefaultFiles.WorldEntities, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(f).Equals("entities.json", StringComparison.OrdinalIgnoreCase));
            var entityFile = preferredFile ?? entityFiles[0];
            var entities = EntityLayoutLoader.LoadFromJson(entityFile);
            if (entities.Count == 0 || _tileMap == null || _interactionService == null || _entityRenderer == null)
                return;

            var log = ServiceLocator.Get<ILogger>();
            log?.Info($"Loading entity layout from {Path.GetFileName(entityFile)}");

            // Entities have fixed sizes (32x32) and do not scale with tile size

            // Create entities from layout
            foreach (var entityData in entities)
            {
                // Use actual tile map dimensions to match editor positioning
                var screenPos = _tileMap.WorldToScreen(entityData.Position);
                Entity? createdEntity = null;

                if (entityData.Type == "NPC")
                {
                    // Try to create NPC with dialog if DialogId is provided and exists
                    if (!string.IsNullOrEmpty(entityData.DialogId) && 
                        _dialogDatabase != null && 
                        _dialogDatabase.TryGetValue(entityData.DialogId, out var dialog))
                    {
                        var npc = new DialogNPC(screenPos, entityData.Name, entityData.Description, dialog);
                        createdEntity = npc;
                        _interactionService.RegisterInteractable(npc);
                        _entityRenderer.AddEntity(npc);
                    }
                    else
                    {
                        // Fallback to interactable if dialog is missing or not found
                        log?.Warning($"NPC '{entityData.Name}' at {entityData.Position} has missing or invalid DialogId '{entityData.DialogId}', creating as Interactable");
                        var interactable = new TestInteractable(screenPos, entityData.Name, entityData.Description);
                        createdEntity = interactable;
                        _interactionService.RegisterInteractable(interactable);
                        _entityRenderer.AddEntity(interactable);
                    }
                }
                else if (entityData.Type == "GroundItem")
                {
                    // Try to create GroundItem if ItemId is provided and exists
                    if (!string.IsNullOrEmpty(entityData.ItemId) &&
                        _itemDatabase != null &&
                        _itemDatabase.TryGetValue(entityData.ItemId, out var item))
                    {
                        var groundItem = new GroundItem(screenPos, item, entityData.Quantity ?? 1);
                        createdEntity = groundItem;
                        _interactionService.RegisterInteractable(groundItem);
                        _entityRenderer.AddEntity(groundItem);
                    }
                    else
                    {
                        // Fallback to interactable if item is missing or not found
                        log?.Warning($"GroundItem '{entityData.Name}' at {entityData.Position} has missing or invalid ItemId '{entityData.ItemId}', creating as Interactable");
                        var interactable = new TestInteractable(screenPos, entityData.Name, entityData.Description);
                        createdEntity = interactable;
                        _interactionService.RegisterInteractable(interactable);
                        _entityRenderer.AddEntity(interactable);
                    }
                }
                else if (entityData.Type == "Interactable")
                {
                    var interactable = new TestInteractable(screenPos, entityData.Name, entityData.Description);
                    createdEntity = interactable;
                    _interactionService.RegisterInteractable(interactable);
                    _entityRenderer.AddEntity(interactable);
                }
                else if (entityData.Type == "Enemy")
                {
                    if (entityData.Stats != null && _player != null)
                    {
                        var enemyService = ServiceLocator.Get<EnemyService>();
                        if (enemyService != null)
                        {
                            var enemy = new Enemy(
                                screenPos,
                                _player,
                                _tileMap,
                                maxHealth: entityData.Stats.MaxHealth,
                                attackPower: entityData.Stats.AttackPower,
                                defense: entityData.Stats.Defense
                            );
                            createdEntity = enemy;
                            enemyService.RegisterEnemy(enemy);
                            _entityRenderer.AddEntity(enemy);
                            
                            // Populate enemy inventory with random items
                            PopulateEnemyInventory(enemy);
                        }
                    }
                    else
                    {
                        log?.Warning($"Enemy '{entityData.Name}' at {entityData.Position} is missing Stats, skipping");
                    }
                }
                else if (entityData.Type == "RangedEnemy")
                {
                    if (entityData.Stats != null && _player != null)
                    {
                        var enemyService = ServiceLocator.Get<EnemyService>();
                        if (enemyService != null)
                        {
                            var rangedEnemy = new RangedEnemy(
                                screenPos,
                                _player,
                                _tileMap,
                                patrolWaypoints: null,
                                maxHealth: entityData.Stats.MaxHealth,
                                attackPower: entityData.Stats.AttackPower,
                                defense: entityData.Stats.Defense
                            );
                            createdEntity = rangedEnemy;
                            enemyService.RegisterRangedEnemy(rangedEnemy);
                            _entityRenderer.AddEntity(rangedEnemy);
                            
                            // Populate ranged enemy inventory with random items
                            PopulateRangedEnemyInventory(rangedEnemy);
                            
                            log?.Info($"Created ranged enemy '{entityData.Name}' at {entityData.Position}");
                        }
                    }
                    else
                    {
                        log?.Warning($"RangedEnemy '{entityData.Name}' at {entityData.Position} is missing Stats, skipping");
                    }
                }
                else
                {
                    log?.Warning($"Unknown entity type '{entityData.Type}' for entity '{entityData.Name}' at {entityData.Position}, skipping");
                }

                // Entities maintain their fixed sizes (32x32) and do not scale with tile size
            }
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning($"Error loading entity layout from file: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates test enemy entities on the map.
    /// </summary>
    private void CreateTestEnemies()
    {
        if (_tileMap == null || _entityRenderer == null || _player == null)
            return;

        var enemyService = ServiceLocator.Get<EnemyService>();
        if (enemyService == null)
            return;

        // Create a few melee enemies at different locations
        // Use fixed tile size conversion so entity positions don't scale with tile size
        var enemies = new[]
        {
            new Enemy(
                _tileMap.WorldToScreenFixed(new Vector2(7, 7)),
                _player,
                _tileMap,
                maxHealth: 50,
                attackPower: 8,
                defense: 1
            ),
            new Enemy(
                _tileMap.WorldToScreenFixed(new Vector2(13, 13)),
                _player,
                _tileMap,
                maxHealth: 75,
                attackPower: 10,
                defense: 2
            )
        };

        foreach (var enemy in enemies)
        {
            enemyService.RegisterEnemy(enemy);
            _entityRenderer.AddEntity(enemy);
            
            // Populate enemy inventory with random items
            PopulateEnemyInventory(enemy);
        }
    }

    /// <summary>
    /// Populates an enemy's inventory with random items (gold, health potions, ammo).
    /// </summary>
    private void PopulateEnemyInventory(Enemy enemy)
    {
        if (enemy == null || _itemDatabase == null)
            return;

        PopulateInventory(enemy.EnemyInventory);
    }

    /// <summary>
    /// Populates a ranged enemy's inventory with random items (gold, health potions, ammo).
    /// </summary>
    private void PopulateRangedEnemyInventory(RangedEnemy enemy)
    {
        if (enemy == null || _itemDatabase == null)
            return;

        PopulateInventory(enemy.EnemyInventory);
    }

    /// <summary>
    /// Populates an inventory with random items (gold, health potions, ammo).
    /// </summary>
    private void PopulateInventory(GameCore.Items.Inventory inventory)
    {
        if (inventory == null || _itemDatabase == null)
            return;

        var random = new Random();

        // Random gold coins (1-10)
        if (_itemDatabase.TryGetValue("gold_coin", out var goldCoin))
        {
            var goldAmount = random.Next(1, 11);
            inventory.AddItem(goldCoin, goldAmount);
        }

        // Random health potions (0-3)
        if (_itemDatabase.TryGetValue("health_potion", out var healthPotion))
        {
            var potionCount = random.Next(0, 4);
            if (potionCount > 0)
            {
                inventory.AddItem(healthPotion, potionCount);
            }
        }

        // Random ammo (0-20)
        if (_itemDatabase.TryGetValue("ammo", out var ammo))
        {
            var ammoCount = random.Next(0, 21);
            if (ammoCount > 0)
            {
                inventory.AddItem(ammo, ammoCount);
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        // Capture scene to render target for day/night cycle lighting
        if (_lightingRenderer != null)
        {
            _lightingRenderer.BeginSceneCapture();
        }
        
        GraphicsDevice.Clear(Color.DarkBlue); // Darker background for better tile visibility

        // Draw tile map first
        _tileMapRenderer?.Draw(gameTime);

        // Draw collision debug overlay (semi-transparent red on solid tiles) when enabled
        if (_showCollisionDebug && _collisionDebugRenderer != null)
        {
            _collisionDebugRenderer.Draw(gameTime);
        }

        // Draw interaction radius visualization when enabled
        if (_showInteractionRadius && _interactionRadiusRenderer != null)
        {
            _interactionRadiusRenderer.Draw(gameTime);
        }

        // Draw aggro radius visualization when enabled
        if (_showAggroRadius && _aggroRadiusRenderer != null)
        {
            _aggroRadiusRenderer.Draw(gameTime);
        }

        // Draw lighting radius visualization when enabled
        if (_showLightingRadius && _lightingRadiusRenderer != null)
        {
            _lightingRadiusRenderer.Draw(gameTime);
        }

        // Draw entities (player) on top of tiles
        _entityRenderer?.Draw(gameTime);

        // Draw projectiles
        _projectileRenderer?.Draw(gameTime);

        // Draw click effects
        _clickEffectRenderer?.Draw(gameTime);

        // Draw VFX effects (particles, trails, etc.)
        _particleSystem?.Draw(gameTime);
        _trailRenderer?.Draw(gameTime);
        _hitIndicatorRenderer?.Draw(gameTime);
        _damageNumberRenderer?.Draw(gameTime);
        _pathIndicatorRenderer?.Draw(gameTime);

        // Apply day/night cycle lighting using simplified lighting system
        if (_lightingRenderer != null)
        {
            GraphicsDevice.SetRenderTarget(null);
            _lightingRenderer.Draw(gameTime);
        }
        else
        {
            GraphicsDevice.SetRenderTarget(null);
        }

        // Draw screen effects on top of everything (flashes, fades, tints)
        _screenEffectRenderer?.Draw(gameTime);

        // Draw death overlay if player is dead
        if (_player?.Stats?.IsDead == true && _spriteBatch != null)
        {
            DrawDeathOverlay();
        }

        // Draw editor overlay if in editor mode (on top of lit scene)
        _editorService?.Draw(gameTime);

        // Render Myra UI within sprite batch (on top of everything)
        if (_spriteBatch != null && _desktop != null)
        {
            _spriteBatch.Begin();
            _desktop.Render();
            _spriteBatch.End();
        }

        // Draw player health bar (on top of UI)
        if (_player != null && _spriteBatch != null && _gameStateService?.CurrentState == GameState.GameScreen)
        {
            DrawHealthBar();
        }

        // Draw dragged item cursors (on top of UI)
        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService != null)
        {
            _inventoryWindow?.DrawDragCursor(inputService);
            _enemyInventoryWindow?.DrawDragCursor(inputService);
        }

        base.Draw(gameTime);
    }
    
    private void ReloadMap()
    {
        // Always use the source GameContent directory (same as editor)
        var worldMapPath = GameContentPathHelper.GetWorldMapPath();
        if (worldMapPath == null || !File.Exists(worldMapPath))
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning($"ReloadMap: Could not find world.json at {worldMapPath ?? "null"}");
            return;
        }
            
        try
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Info($"Reloading map from: {worldMapPath}");
            
            // Validate we're using the source directory, not a runtime copy
            var gameContentPath = GameContentPathHelper.GetGameContentPath();
            if (gameContentPath != null && worldMapPath.Contains(gameContentPath))
            {
                log?.Info($"ReloadMap: Using source GameContent directory: {gameContentPath}");
            }
            else
            {
                log?.Warning($"ReloadMap: WARNING - Map path may not be from source directory! GameContent: {gameContentPath}, Map: {worldMapPath}");
            }
            
            var newTileMap = TileMapLoader.LoadFromJson(worldMapPath);
            if (newTileMap != null && _tileMapRenderer != null && _spriteBatch != null && GraphicsDevice != null && _camera != null)
            {
                // Update the tile map
                _tileMap = newTileMap;
                
                // Update tile map renderer with new map
                _tileMapRenderer = new TileMapRenderer(_camera, _tileMap);
                _tileMapRenderer.LoadContent(GraphicsDevice, _spriteBatch);
                
                // Reload tile graphics BEFORE creating procedural tileset
                // This ensures custom graphics are loaded first
                ReloadTileGraphics();
                
                // Update collision service
                if (_collisionService != null)
                {
                    ConfigureCollisionService(_collisionService);
                }
                
                // Update camera bounds if needed
                var mapCenterWorld = _tileMap.WorldToScreen(new Vector2(_tileMap.Width / 2.0f, _tileMap.Height / 2.0f));
                if (_camera != null)
                {
                    _camera.Position = mapCenterWorld;
                }
                
                log?.Info($"Map reloaded successfully (Size: {_tileMap.Width}x{_tileMap.Height})");
            }
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Error($"Error reloading map: {ex.Message}");
        }
    }
    
    private void ReloadTileGraphics()
    {
        // Try to load from shared tiles.json library first
        var tilesLibraryPath = GameContentPathHelper.GetTilesLibraryPath();
        var worldMapPath = GameContentPathHelper.GetWorldMapPath();
        var log = ServiceLocator.Get<ILogger>();
        
        log?.Info($"ReloadTileGraphics: tilesLibraryPath={tilesLibraryPath}, worldMapPath={worldMapPath}");
        
        if (worldMapPath != null)
        {
            var tileGraphics = TileMapLoader.GetTileGraphics(worldMapPath, tilesLibraryPath);
            if (tileGraphics != null && tileGraphics.Count > 0)
            {
                log?.Info($"ReloadTileGraphics: Found {tileGraphics.Count} tile graphics to load");
                _tileMapRenderer?.LoadCustomTileGraphics(tileGraphics);
                var source = tilesLibraryPath != null && File.Exists(tilesLibraryPath) ? tilesLibraryPath : worldMapPath;
                log?.Info($"Loaded {tileGraphics.Count} custom tile graphics from {source}");
            }
            else
            {
                log?.Warning($"ReloadTileGraphics: No tile graphics found! tileGraphics is {(tileGraphics == null ? "null" : "empty")}");
                if (tilesLibraryPath != null)
                {
                    log?.Info($"ReloadTileGraphics: tiles.json exists: {File.Exists(tilesLibraryPath)}");
                }
            }
        }
        else
        {
            log?.Warning("ReloadTileGraphics: worldMapPath is null!");
        }
    }
    
    private void SetupMapFileWatcher()
    {
        var worldMapPath = GameContentPathHelper.GetWorldMapPath();
        if (worldMapPath == null || !File.Exists(worldMapPath))
            return;
            
        try
        {
            var directory = Path.GetDirectoryName(worldMapPath);
            var fileName = Path.GetFileName(worldMapPath);
            
            if (directory == null)
                return;
                
            _mapFileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            
            // Use a flag to debounce rapid file changes (editor may save multiple times)
            bool reloadPending = false;
            System.Timers.Timer? reloadTimer = null;
            
            _mapFileWatcher.Changed += (sender, e) =>
            {
                // Skip if reload already pending
                if (reloadPending)
                    return;
                    
                reloadPending = true;
                
                // Debounce: wait 500ms after last change before reloading
                if (reloadTimer != null)
                {
                    reloadTimer.Stop();
                    reloadTimer.Dispose();
                }
                
                reloadTimer = new System.Timers.Timer(500);
                reloadTimer.Elapsed += (s, args) =>
                {
                    reloadTimer.Stop();
                    reloadTimer.Dispose();
                    reloadTimer = null;
                    reloadPending = false;
                    
                    // Set flags to reload map and tile graphics on main thread
                    _reloadMapPending = true;
                    _reloadTileGraphicsPending = true;
                };
                reloadTimer.AutoReset = false;
                reloadTimer.Start();
            };
            
            var log = ServiceLocator.Get<ILogger>();
            log?.Info($"Watching map file for changes: {worldMapPath}");
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning($"Failed to set up map file watcher: {ex.Message}");
        }
    }
    
    protected override void UnloadContent()
    {
        if (_mapFileWatcher != null)
        {
            _mapFileWatcher.Dispose();
            _mapFileWatcher = null;
        }
        
        if (_tilesLibraryFileWatcher != null)
        {
            _tilesLibraryFileWatcher.Dispose();
            _tilesLibraryFileWatcher = null;
        }
        
        base.UnloadContent();
    }
    
    private void SetupTilesLibraryFileWatcher()
    {
        var tilesLibraryPath = GameContentPathHelper.GetTilesLibraryPath();
        if (tilesLibraryPath == null || !File.Exists(tilesLibraryPath))
            return;
            
        try
        {
            var directory = Path.GetDirectoryName(tilesLibraryPath);
            var fileName = Path.GetFileName(tilesLibraryPath);
            
            if (directory == null)
                return;
                
            _tilesLibraryFileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            
            // Use a flag to debounce rapid file changes (editor may save multiple times)
            bool reloadPending = false;
            System.Timers.Timer? reloadTimer = null;
            
            _tilesLibraryFileWatcher.Changed += (sender, e) =>
            {
                // Skip if reload already pending
                if (reloadPending)
                    return;
                    
                reloadPending = true;
                
                // Debounce: wait 500ms after last change before reloading
                if (reloadTimer != null)
                {
                    reloadTimer.Stop();
                    reloadTimer.Dispose();
                }
                
                reloadTimer = new System.Timers.Timer(500);
                reloadTimer.Elapsed += (s, args) =>
                {
                    reloadTimer.Stop();
                    reloadTimer.Dispose();
                    reloadTimer = null;
                    reloadPending = false;
                    
                    // Set flag to reload on main thread
                    _reloadTileGraphicsPending = true;
                };
                reloadTimer.AutoReset = false;
                reloadTimer.Start();
            };
            
            var log = ServiceLocator.Get<ILogger>();
            log?.Info($"Watching tiles library file for changes: {tilesLibraryPath}");
        }
        catch (Exception ex)
        {
            var log = ServiceLocator.Get<ILogger>();
            log?.Warning($"Failed to set up tiles library file watcher: {ex.Message}");
        }
    }
}

