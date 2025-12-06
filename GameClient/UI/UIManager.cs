using GameCore;
using GameCore.Configuration;
using GameCore.Entities;
using GameCore.Input;
using GameCore.Rendering;
using GameCore.Services;
using GameCore.State;
using GameClient.Services;
using GameClient.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using GameClient.Rendering;

namespace GameClient.UI;

/// <summary>
/// Manages all UI elements including title screen, pause menu, debug labels, and interaction displays.
/// </summary>
public class UIManager
{
    private readonly IInputService _inputService;
    private readonly ILogger _logger;
    private readonly InteractionService? _interactionService;
    private readonly GameStateService? _gameStateService;
    private readonly SaveGameService? _saveGameService;
    private readonly Camera2D? _camera;
    private readonly IsometricTileMap? _tileMap;
    private readonly Player? _player;
    private readonly EntityRenderer? _entityRenderer;
    private readonly GraphicsDevice _graphicsDevice;

    // UI Elements
    private Label? _debugLabel;
    private Label? _interactionLabel;
    private Label? _hoverNameTag;
    private Label? _mapInfoLabel;
    private Panel? _titleScreenPanel;
    private Panel? _pauseMenuPanel;
    private OptionsWindow? _optionsWindow;
    private Desktop? _desktop;

    private string? _currentMapFileName;
    private Entity? _hoveredEntity;

    public UIManager(
        IInputService inputService,
        ILogger logger,
        GraphicsDevice graphicsDevice,
        InteractionService? interactionService = null,
        GameStateService? gameStateService = null,
        SaveGameService? saveGameService = null,
        Camera2D? camera = null,
        IsometricTileMap? tileMap = null,
        Player? player = null,
        EntityRenderer? entityRenderer = null)
    {
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _interactionService = interactionService;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _camera = camera;
        _tileMap = tileMap;
        _player = player;
        _entityRenderer = entityRenderer;
    }

    /// <summary>
    /// Initializes the UI manager and creates all UI elements.
    /// </summary>
    public void Initialize(Desktop desktop, OptionsWindow? optionsWindow = null)
    {
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        _optionsWindow = optionsWindow;

        var rootPanel = new Panel
        {
            Width = Configuration.WindowWidth,
            Height = Configuration.WindowHeight
        };

        // Create debug label (top-left)
        _debugLabel = new Label
        {
            Text = "Input: Movement=(0, 0) | Actions: None",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextColor = Color.LightGreen,
            Margin = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
        };

        // Create interaction label (bottom-center)
        _interactionLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextColor = Color.Yellow,
            Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 50)
        };

        // Create hover name tag label
        _hoverNameTag = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextColor = Color.White,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 180)),
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
            Margin = new Myra.Graphics2D.Thickness(0, 10, 10, 0)
        };

        rootPanel.Widgets.Add(_debugLabel);
        rootPanel.Widgets.Add(_interactionLabel);
        rootPanel.Widgets.Add(_hoverNameTag);
        rootPanel.Widgets.Add(_mapInfoLabel);
        _desktop.Root = rootPanel;

        // Create title and pause screens
        CreateTitleScreenUI(rootPanel);
        CreatePauseMenuUI(rootPanel);
    }

    /// <summary>
    /// Updates all UI elements.
    /// </summary>
    public void Update(GameTime gameTime, bool isPausedByUI)
    {
        UpdateDebugLabel();

        if (_tileMap != null && _player != null && _camera != null)
        {
            UpdateMapInfoLabel();
        }

        // Update interaction label and hover detection only when in game and not paused by UI
        if (_gameStateService?.CurrentState == GameState.GameScreen && !isPausedByUI)
        {
            UpdateInteractionLabel();
            UpdateHoverDetection();
        }

        // Ensure title/pause panel visibility matches game state
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

        // Update Myra UI
        _desktop?.UpdateInput();
        _desktop?.UpdateLayout();
    }

    /// <summary>
    /// Sets the current map file name for display.
    /// </summary>
    public void SetMapFileName(string? fileName)
    {
        _currentMapFileName = fileName;
    }

    /// <summary>
    /// Gets the title screen panel for external visibility control.
    /// </summary>
    public Panel? TitleScreenPanel => _titleScreenPanel;

    /// <summary>
    /// Gets the pause menu panel for external visibility control.
    /// </summary>
    public Panel? PauseMenuPanel => _pauseMenuPanel;

    private void UpdateDebugLabel()
    {
        if (_debugLabel == null)
            return;

        // Get movement vector
        var movement = _inputService.GetMovementVector();
        var movementText = $"({movement.X:F2}, {movement.Y:F2})";

        // Get pressed actions
        var pressedActions = new List<string>();
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            if (_inputService.IsActionDown(action))
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

        _mapInfoLabel.Text = $"Map: {mapName}\n" +
                             $"Location: Tile ({tileX}, {tileY})\n" +
                             $"World: ({playerWorldPos.X:F1}, {playerWorldPos.Y:F1})\n" +
                             $"Camera: ({cameraWorldPos.X:F1}, {cameraWorldPos.Y:F1})";
    }

    private void UpdateInteractionLabel()
    {
        if (_interactionLabel == null || _player == null || _interactionService == null)
            return;

        var nearest = _interactionService.GetNearestInteractable(_player);
        if (nearest != null)
        {
            var interactable = _interactionService.TryInteract(_player);
            if (interactable != null)
            {
                // Player is facing the interactable and in range
                _interactionLabel.Text = $"[E] {nearest.Name}\n{nearest.Description}";
                _interactionLabel.TextColor = Color.Yellow;
            }
            else
            {
                // Interactable is nearby but player not facing it
                _interactionLabel.Text = $"{nearest.Name} (face to interact)";
                _interactionLabel.TextColor = Color.Gray;
            }
        }
        else
        {
            _interactionLabel.Text = "";
        }
    }

    private void UpdateHoverDetection()
    {
        if (_hoverNameTag == null || _camera == null || _entityRenderer == null)
            return;

        // Convert mouse position to world coordinates
        var mouseScreenPos = new Vector2(_inputService.MousePosition.X, _inputService.MousePosition.Y);
        var mouseWorldPos = _camera.ScreenToWorld(mouseScreenPos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);

        // Check for entities under the mouse cursor
        Entity? hoveredEntity = null;

        // Calculate hover radius based on tile size
        const int baseHoverTileWidth = 64;
        const float baseHoverRadius = GameConstants.Player.BaseHoverRadius;
        var hoverTileScale = _tileMap != null ? (float)_tileMap.TileWidth / baseHoverTileWidth : 1.0f;
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
            var enemyService = ServiceLocator.Get<EnemyService>();
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
            var entityScreenPos = _camera.WorldToScreen(hoveredEntity.Position, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            _hoverNameTag.Left = (int)(entityScreenPos.X - 50);
            _hoverNameTag.Top = (int)(entityScreenPos.Y - hoveredEntity.Size.Y / 2 - 30);
        }
        else
        {
            _hoverNameTag.Visible = false;
        }
    }

    private void CreateTitleScreenUI(Panel rootPanel)
    {
        _titleScreenPanel = new Panel
        {
            Width = Configuration.WindowWidth,
            Height = Configuration.WindowHeight,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 255)),
            Visible = true
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
            if (_saveGameService == null)
                return;

            // Find the most recent save slot (1-3)
            var bestSlot = GameCore.Save.SaveSlot.Slot1;
            var bestTime = DateTime.MinValue;
            bool anyFound = false;

            for (int i = 1; i <= 3; i++)
            {
                var slot = (GameCore.Save.SaveSlot)i;
                var path = Path.Combine(GameConstants.Paths.SavesDirectory, $"save{i}.json");
                if (File.Exists(path))
                {
                    anyFound = true;
                    var time = File.GetLastWriteTime(path);
                    if (time > bestTime)
                    {
                        bestTime = time;
                        bestSlot = slot;
                    }
                }
            }

            if (!anyFound)
                return;

            if (_saveGameService.LoadGame(bestSlot))
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
            var path = Path.Combine(GameConstants.Paths.SavesDirectory, $"save{i}.json");
            if (File.Exists(path))
            {
                hasAnySave = true;
                break;
            }
        }
        continueButton.Enabled = hasAnySave;

        var optionsButton = new TextButton { Text = "Options", Width = 200 };
        optionsButton.Click += (s, e) =>
        {
            if (_titleScreenPanel != null)
                _titleScreenPanel.Visible = false;
            _optionsWindow?.Show();
        };

        var quitButton = new TextButton { Text = "Quit", Width = 200 };
        quitButton.Click += (s, e) => { /* Exit handled by game */ };

        centerPanel.Widgets.Add(titleLabel);
        centerPanel.Widgets.Add(newGameButton);
        centerPanel.Widgets.Add(continueButton);
        centerPanel.Widgets.Add(optionsButton);
        centerPanel.Widgets.Add(quitButton);

        _titleScreenPanel.Widgets.Add(centerPanel);
        rootPanel.Widgets.Add(_titleScreenPanel);
    }

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
        saveSlot1Button.Click += (s, e) => _saveGameService?.SaveGame(GameCore.Save.SaveSlot.Slot1);

        var saveSlot2Button = new TextButton { Text = "Save Slot 2", Width = 200 };
        saveSlot2Button.Click += (s, e) => _saveGameService?.SaveGame(GameCore.Save.SaveSlot.Slot2);

        var saveSlot3Button = new TextButton { Text = "Save Slot 3", Width = 200 };
        saveSlot3Button.Click += (s, e) => _saveGameService?.SaveGame(GameCore.Save.SaveSlot.Slot3);

        // Load slots
        var loadSlot1Button = new TextButton { Text = "Load Slot 1", Width = 200 };
        loadSlot1Button.Click += (s, e) =>
        {
            if (_saveGameService != null && _saveGameService.LoadGame(GameCore.Save.SaveSlot.Slot1))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var loadSlot2Button = new TextButton { Text = "Load Slot 2", Width = 200 };
        loadSlot2Button.Click += (s, e) =>
        {
            if (_saveGameService != null && _saveGameService.LoadGame(GameCore.Save.SaveSlot.Slot2))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var loadSlot3Button = new TextButton { Text = "Load Slot 3", Width = 200 };
        loadSlot3Button.Click += (s, e) =>
        {
            if (_saveGameService != null && _saveGameService.LoadGame(GameCore.Save.SaveSlot.Slot3))
            {
                _gameStateService?.SetState(GameState.GameScreen);
                if (_pauseMenuPanel != null)
                    _pauseMenuPanel.Visible = false;
            }
        };

        var optionsButton = new TextButton { Text = "Options", Width = 200 };
        optionsButton.Click += (s, e) =>
        {
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
}

