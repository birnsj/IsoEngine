using GameCore;
using GameCore.Configuration;
using GameEditor.Services;

namespace GameEditor.Forms;

/// <summary>
/// Form that hosts the game window embedded in the editor.
/// All GameHostForm instances share the same game process via GameHostService.
/// </summary>
public partial class GameHostForm : Form
{
    private GameHostService? _gameService;
    private IntPtr _gameWindowHandle = IntPtr.Zero;
    private ToolStripButton? _launchButton;
    private ToolStripButton? _stopButton;

    public GameHostForm()
    {
        InitializeComponent();
        SetupUI();
        SetupGameService();
    }

    private void InitializeComponent()
    {
        Text = "Game View";
        MinimumSize = new Size(400, 300);

        // Add info label explaining the game runs in a separate window
        var infoLabel = new Label
        {
            Text = "Click 'Launch Game' to start the game in a separate, movable window.\n\nThe game will use the shared tiles.json library.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        Controls.Add(infoLabel);

        // Handle form closing - don't stop game, it's shared
        FormClosing += (s, e) =>
        {
            // Game window is floating, so no need to unparent
            _gameWindowHandle = IntPtr.Zero;
        };
    }

    private void SetupUI()
    {
        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top
        };

        _launchButton = new ToolStripButton("Launch Game", null, (s, e) => LaunchGame());
        _stopButton = new ToolStripButton("Stop Game", null, (s, e) => StopGame());
        _stopButton.Enabled = false;

        toolStrip.Items.Add(_launchButton);
        toolStrip.Items.Add(_stopButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        var infoLabel = new ToolStripLabel("Press F12 in-game to toggle editor mode");
        toolStrip.Items.Add(infoLabel);

        Controls.Add(toolStrip);
        
        UpdateButtonStates();
    }

    private void SetupGameService()
    {
        _gameService = GameHostService.Instance;
        
        // Subscribe to game service events
        _gameService.GameStarted += (s, e) =>
        {
            UpdateButtonStates();
        };
        
        _gameService.GameStopped += (s, e) =>
        {
            _gameWindowHandle = IntPtr.Zero;
            UpdateButtonStates();
        };
        
        // Update button states based on current game status
        UpdateButtonStates();
    }

    private void LaunchGame()
    {
        if (_gameService == null)
            return;
        
        // Launch game as a floating, movable window (not embedded)
        _gameService.LaunchGame(IntPtr.Zero, (gameHandle) =>
        {
            _gameWindowHandle = gameHandle;
        }, embedWindow: false);
    }

    private void StopGame()
    {
        if (_gameService == null)
            return;
        
        // Stop the shared game process (affects all game view windows)
        _gameService.StopGame();
        _gameWindowHandle = IntPtr.Zero;
    }

    private void UpdateButtonStates()
    {
        if (_gameService == null)
            return;
        
        bool isRunning = _gameService.IsGameRunning;
        
        if (_launchButton != null)
            _launchButton.Enabled = !isRunning;
        
        if (_stopButton != null)
            _stopButton.Enabled = isRunning;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        // When form becomes visible, check if game is running
        if (Visible && _gameService != null && _gameService.IsGameRunning)
        {
            var gameHandle = _gameService.GameWindowHandle;
            if (gameHandle != IntPtr.Zero)
            {
                _gameWindowHandle = gameHandle;
            }
        }
    }
}
