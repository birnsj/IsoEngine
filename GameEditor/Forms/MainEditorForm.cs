using GameEditor.Forms;
using GameEditor.Utilities;
using GameEditor.Services;
using GameEditor.Data;
using System.Linq;
using System.IO;

namespace GameEditor.Forms;

/// <summary>
/// Main form for the game editor application.
/// </summary>
public partial class MainEditorForm : Form
{
    private TabControl? _tabControl;
    private Dictionary<string, ToolStripMenuItem>? _windowMenuItems;
    private Dictionary<string, TabPage>? _openWindows;

    public MainEditorForm()
    {
        InitializeComponent();
        
        // Validate GameContent path (ensures consistency with game)
        PathHelper.ValidateGameContentPath();
        
        SetupUI();
    }

    private void InitializeComponent()
    {
        Text = "IsoEditor";
        // Start maximized to use full screen
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1024, 768);  // Reasonable minimum for modern screens

        var menuStrip = new MenuStrip();
        
        // File Menu
        var fileMenu = new ToolStripMenuItem("File");
        var saveAllItem = new ToolStripMenuItem("Save All", null, (s, e) => SaveAllGameFiles());
        var saveLayoutItem = new ToolStripMenuItem("Save Layout...", null, (s, e) => SaveLayout());
        var loadLayoutItem = new ToolStripMenuItem("Load Layout...", null, (s, e) => LoadLayout());
        fileMenu.DropDownItems.Add(saveAllItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(saveLayoutItem);
        fileMenu.DropDownItems.Add(loadLayoutItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => Close());
        fileMenu.DropDownItems.Add(exitItem);

        // Editors Menu
        var editorsMenu = new ToolStripMenuItem("Editors");
        var worldEditorItem = new ToolStripMenuItem("World Editor", null, (s, e) => OpenWorldEditor());
        var playerEditorItem = new ToolStripMenuItem("Player Editor", null, (s, e) => OpenPlayerEditor());
        var dialogItem = new ToolStripMenuItem("Dialog Editor", null, (s, e) => OpenDialogEditor());
        var itemItem = new ToolStripMenuItem("Item Editor", null, (s, e) => OpenItemEditor());
        var weatherCycleItem = new ToolStripMenuItem("Weather Cycle Editor", null, (s, e) => OpenWeatherCycleEditor());
        
        editorsMenu.DropDownItems.Add(worldEditorItem);
        editorsMenu.DropDownItems.Add(playerEditorItem);
        editorsMenu.DropDownItems.Add(new ToolStripSeparator());
        editorsMenu.DropDownItems.Add(dialogItem);
        editorsMenu.DropDownItems.Add(itemItem);
        editorsMenu.DropDownItems.Add(weatherCycleItem);

        // View Menu
        var viewMenu = new ToolStripMenuItem("View");
        var gameViewItem = new ToolStripMenuItem("Game View", null, (s, e) => OpenGameView());
        viewMenu.DropDownItems.Add(gameViewItem);

        // Window Menu
        var windowMenu = new ToolStripMenuItem("Window");
        var worldEditorWindowItem = new ToolStripMenuItem("World Editor", null, (s, e) => ToggleWindow("WorldEditor"));
        var playerEditorWindowItem = new ToolStripMenuItem("Player Editor", null, (s, e) => ToggleWindow("Player"));
        var dialogWindowItem = new ToolStripMenuItem("Dialog Editor", null, (s, e) => ToggleWindow("Dialog"));
        var itemWindowItem = new ToolStripMenuItem("Item Editor", null, (s, e) => ToggleWindow("Item"));
        var weatherCycleWindowItem = new ToolStripMenuItem("Weather Cycle Editor", null, (s, e) => ToggleWindow("WeatherCycle"));
        var gameViewWindowItem = new ToolStripMenuItem("Game View", null, (s, e) => ToggleWindow("GameView"));
        
        windowMenu.DropDownItems.Add(worldEditorWindowItem);
        windowMenu.DropDownItems.Add(playerEditorWindowItem);
        windowMenu.DropDownItems.Add(dialogWindowItem);
        windowMenu.DropDownItems.Add(itemWindowItem);
        windowMenu.DropDownItems.Add(weatherCycleWindowItem);
        windowMenu.DropDownItems.Add(new ToolStripSeparator());
        windowMenu.DropDownItems.Add(gameViewWindowItem);
        windowMenu.DropDownItems.Add(new ToolStripSeparator());
        
        var closeAllItem = new ToolStripMenuItem("Close All", null, (s, e) => CloseAllWindows());
        windowMenu.DropDownItems.Add(closeAllItem);

        // Settings Menu
        var settingsMenu = new ToolStripMenuItem("Settings");
        var settingsItem = new ToolStripMenuItem("Editor Settings...", null, (s, e) => OpenSettings());
        var configureApisItem = new ToolStripMenuItem("Configure APIs...", null, (s, e) => OpenApiConfiguration());
        var gitControlItem = new ToolStripMenuItem("Git Control...", null, (s, e) => OpenGitControl());
        settingsMenu.DropDownItems.Add(settingsItem);
        settingsMenu.DropDownItems.Add(new ToolStripSeparator());
        settingsMenu.DropDownItems.Add(configureApisItem);
        settingsMenu.DropDownItems.Add(gitControlItem);

        // About Menu
        var aboutMenu = new ToolStripMenuItem("About");
        var versionItem = new ToolStripMenuItem("Version", null, (s, e) => ShowVersionDialog());
        aboutMenu.DropDownItems.Add(versionItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(editorsMenu);
        menuStrip.Items.Add(viewMenu);
        menuStrip.Items.Add(windowMenu);
        menuStrip.Items.Add(settingsMenu);
        menuStrip.Items.Add(aboutMenu);
        
        // Store window menu items for updating checkmarks
        _windowMenuItems = new Dictionary<string, ToolStripMenuItem>
        {
            { "WorldEditor", worldEditorWindowItem },
            { "Player", playerEditorWindowItem },
            { "Dialog", dialogWindowItem },
            { "Item", itemWindowItem },
            { "WeatherCycle", weatherCycleWindowItem },
            { "GameView", gameViewWindowItem }
        };
        
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };
        
        _openWindows = new Dictionary<string, TabPage>();
        
        // Save layout when tab selection changes
        _tabControl.SelectedIndexChanged += (s, e) =>
        {
            EditorLayoutService.SaveLayout(this, _tabControl);
            UpdateWindowMenuCheckmarks();
        };
        
        // Track when tabs are removed
        _tabControl.ControlRemoved += (s, e) =>
        {
            if (e.Control is TabPage removedTab && _openWindows != null)
            {
                // Find and remove from tracking
                var keyToRemove = _openWindows.FirstOrDefault(kvp => kvp.Value == removedTab).Key;
                if (keyToRemove != null)
                {
                    _openWindows.Remove(keyToRemove);
                }
            }
            UpdateWindowMenuCheckmarks();
        };
        
        // Add tab control - with MainMenuStrip set, Dock.Fill will automatically
        // fill the client area below the menu strip
        Controls.Add(_tabControl);
    }

    private void SetupUI()
    {
        // Load saved layout
        Load += (s, e) =>
        {
            var layout = EditorLayoutService.LoadLayout();
            EditorLayoutService.ApplyLayout(this, layout);
            
            // Restore open tabs from layout
            if (layout != null && layout.OpenTabs.Count > 0)
            {
                RestoreTabsFromLayout(layout);
            }
            else
            {
                // If no saved layout, auto-load all game content
                AutoLoadWorldMap();
            }
        };

        // Save layout when form is closing
        FormClosing += (s, e) =>
        {
            EditorLayoutService.SaveLayout(this, _tabControl);
        };

        // Save layout when window state changes
        ResizeEnd += (s, e) =>
        {
            EditorLayoutService.SaveLayout(this, _tabControl);
        };

        Move += (s, e) =>
        {
            if (WindowState == FormWindowState.Normal)
            {
                EditorLayoutService.SaveLayout(this, _tabControl);
            }
        };
    }

    private void RestoreTabsFromLayout(Data.EditorLayoutData layout)
    {
        if (_tabControl == null)
            return;

        foreach (var tabInfo in layout.OpenTabs)
        {
            var parts = tabInfo.Split(':', 2);
            if (parts.Length == 2)
            {
                var tabType = parts[0];
                var filePath = parts[1];

                if (File.Exists(filePath))
                {
                    if (tabType == "map")
                    {
                        OpenWorldEditor(filePath, null);
                    }
                    else if (tabType == "entity")
                    {
                        OpenWorldEditor(null, filePath);
                    }
                }
            }
        }

        // Restore selected tab
        if (layout.SelectedTabIndex >= 0 && layout.SelectedTabIndex < _tabControl.TabPages.Count)
        {
            _tabControl.SelectedIndex = layout.SelectedTabIndex;
        }
    }

    private void AutoLoadWorldMap()
    {
        // Load all maps from GameContent/maps directory
        var mapsDir = PathHelper.GetMapsDirectory();
        if (mapsDir != null && Directory.Exists(mapsDir))
        {
            var mapFiles = Directory.GetFiles(mapsDir, "*.json");
            if (mapFiles.Length > 0)
            {
                // Sort files: prefer world.json first, then alphabetically (using shared constants)
                var sortedFiles = mapFiles.OrderBy(f =>
                {
                    var fileName = Path.GetFileName(f);
                    if (fileName.Equals(GameCore.GameConstants.DefaultFiles.WorldMap, StringComparison.OrdinalIgnoreCase))
                        return 0;
                    if (fileName.Equals(GameCore.GameConstants.DefaultFiles.WorldMapAlt, StringComparison.OrdinalIgnoreCase))
                        return 1;
                    return 2;
                }).ThenBy(f => Path.GetFileName(f));

                // Find entity file to pair with map file
                string? entityFile = null;
                var entitiesDir = PathHelper.GetEntitiesDirectory();
                if (entitiesDir != null && Directory.Exists(entitiesDir))
                {
                    var entityFiles = Directory.GetFiles(entitiesDir, "*.json");
                    var preferredEntityFile = Array.Find(entityFiles, f => 
                        Path.GetFileName(f).Equals(GameCore.GameConstants.DefaultFiles.WorldEntities, StringComparison.OrdinalIgnoreCase));
                    if (preferredEntityFile != null)
                    {
                        entityFile = preferredEntityFile;
                    }
                    else if (entityFiles.Length > 0)
                    {
                        entityFile = entityFiles[0];
                    }
                }

                // Open world editor with first map file and entity file
                if (sortedFiles.Any())
                {
                    var mapFile = sortedFiles.First();
                    if (File.Exists(mapFile))
                    {
                        OpenWorldEditor(mapFile, entityFile);
                    }
                }
            }
            else
            {
                // No map files found - show message
                System.Diagnostics.Debug.WriteLine($"No map files found in: {mapsDir}");
            }
        }
        else
        {
            // Maps directory not found - show message
            var gameContentPath = PathHelper.GetGameContentPath();
            System.Diagnostics.Debug.WriteLine($"Maps directory not found. GameContent path: {gameContentPath ?? "null"}");
        }

        // If no files found, still open empty world editor
        if (_tabControl == null || _tabControl.TabPages.Count == 0)
        {
            OpenWorldEditor();
        }
    }

    private void OpenWorldEditor(string? mapFilePath = null, string? entityFilePath = null)
    {
        // Check if already open
        if (_openWindows != null && _openWindows.TryGetValue("WorldEditor", out var existingTab))
        {
            if (_tabControl != null && _tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.SelectedTab = existingTab;
                return;
            }
        }
        
        var editor = new WorldEditorForm(mapFilePath, entityFilePath);
        var tabTitle = mapFilePath != null ? $"World: {Path.GetFileNameWithoutExtension(mapFilePath)}" : "World Editor";
        AddTabPage(tabTitle, editor, "WorldEditor");
    }

    private void OpenTileMapEditor(string? filePath = null)
    {
        // Redirect to World Editor
        OpenWorldEditor(filePath, null);
    }

    private void OpenEntityEditor(string? filePath = null)
    {
        // Redirect to World Editor
        OpenWorldEditor(null, filePath);
    }

    private void OpenPlayerEditor()
    {
        // Check if already open
        if (_openWindows != null && _openWindows.TryGetValue("Player", out var existingTab))
        {
            if (_tabControl != null && _tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.SelectedTab = existingTab;
                return;
            }
        }
        
        var editor = new PlayerEditorForm();
        AddTabPage("Player Editor", editor, "Player");
    }

    private void OpenDialogEditor()
    {
        // Check if already open
        if (_openWindows != null && _openWindows.TryGetValue("Dialog", out var existingTab))
        {
            if (_tabControl != null && _tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.SelectedTab = existingTab;
                return;
            }
        }
        
        var editor = new DialogEditorForm();
        AddTabPage("Dialog Editor", editor, "Dialog");
    }

    private void OpenItemEditor()
    {
        // Check if already open
        if (_openWindows != null && _openWindows.TryGetValue("Item", out var existingTab))
        {
            if (_tabControl != null && _tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.SelectedTab = existingTab;
                return;
            }
        }
        
        var editor = new ItemEditorForm();
        AddTabPage("Item Editor", editor, "Item");
    }

    private void OpenWeatherCycleEditor()
    {
        // Check if already open
        if (_openWindows != null && _openWindows.TryGetValue("WeatherCycle", out var existingTab))
        {
            if (_tabControl != null && _tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.SelectedTab = existingTab;
                return;
            }
        }
        
        var editor = new WeatherCycleEditorForm();
        AddTabPage("Weather Cycle Editor", editor, "WeatherCycle");
    }

    private void AddTabPage(string title, Form form, string? windowKey = null)
    {
        if (_tabControl == null)
            return;

        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;

        var tabPage = new TabPage(title)
        {
            UseVisualStyleBackColor = true
        };
        tabPage.Controls.Add(form);
        
        _tabControl.TabPages.Add(tabPage);
        _tabControl.SelectedTab = tabPage;
        
        // Track window if key provided
        if (windowKey != null && _openWindows != null)
        {
            _openWindows[windowKey] = tabPage;
        }
        
        form.Show();
        
        // Update window menu checkmarks
        UpdateWindowMenuCheckmarks();
        
        // Save layout after adding tab
        EditorLayoutService.SaveLayout(this, _tabControl);
    }
    
    private void ToggleWindow(string windowKey)
    {
        if (_tabControl == null || _openWindows == null)
            return;
            
        // Check if window is already open
        if (_openWindows.TryGetValue(windowKey, out var existingTab))
        {
            // Close the window
            if (_tabControl.TabPages.Contains(existingTab))
            {
                _tabControl.TabPages.Remove(existingTab);
            }
            _openWindows.Remove(windowKey);
        }
        else
        {
            // Open the window
            switch (windowKey)
            {
                case "WorldEditor":
                    OpenWorldEditor();
                    break;
                case "Dialog":
                    OpenDialogEditor();
                    break;
                case "Item":
                    OpenItemEditor();
                    break;
                case "WeatherCycle":
                    OpenWeatherCycleEditor();
                    break;
                case "GameView":
                    OpenGameView();
                    break;
            }
        }
        
        UpdateWindowMenuCheckmarks();
    }
    
    private void CloseAllWindows()
    {
        if (_tabControl == null || _openWindows == null)
            return;
            
        _tabControl.TabPages.Clear();
        _openWindows.Clear();
        UpdateWindowMenuCheckmarks();
        EditorLayoutService.SaveLayout(this, _tabControl);
    }
    
    private void UpdateWindowMenuCheckmarks()
    {
        if (_windowMenuItems == null || _openWindows == null || _tabControl == null)
            return;
            
        foreach (var kvp in _windowMenuItems)
        {
            var isOpen = _openWindows.ContainsKey(kvp.Key) && 
                        _tabControl.TabPages.Contains(_openWindows[kvp.Key]);
            kvp.Value.Checked = isOpen;
        }
    }

    private void OpenGameView()
    {
        // Check if game view tab already exists
        if (_tabControl != null)
        {
            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab.Text == "Game View" && tab.Controls.Count > 0 && tab.Controls[0] is GameHostForm)
                {
                    _tabControl.SelectedTab = tab;
                    return;
                }
            }
        }

        // Create new game view tab
        var gameHost = new GameHostForm();
        AddTabPage("Game View", gameHost, "GameView");
    }

    private void SaveLayout()
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Layout files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "editor_layout.json",
                InitialDirectory = Path.GetDirectoryName(EditorLayoutService.GetDefaultLayoutFilePath())
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                EditorLayoutService.SaveLayoutToFile(this, _tabControl, dialog.FileName);
                MessageBox.Show("Layout saved successfully!", "Save Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadLayout()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Layout files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(EditorLayoutService.GetDefaultLayoutFilePath())
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var layout = EditorLayoutService.LoadLayoutFromFile(dialog.FileName);
                if (layout == null)
                {
                    MessageBox.Show("Failed to load layout file.", "Load Layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Apply window layout
                EditorLayoutService.ApplyLayout(this, layout);

                // Clear existing tabs
                if (_tabControl != null)
                {
                    _tabControl.TabPages.Clear();
                }

                // Restore tabs from layout
                RestoreTabsFromLayout(layout);

                MessageBox.Show("Layout loaded successfully!", "Load Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Saves all open game info files (maps, entities, dialogs, items, tiles.json).
    /// </summary>
    private void SaveAllGameFiles()
    {
        if (_tabControl == null)
            return;

        var savedCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        var tilesLibrarySaved = false;

        // Iterate through all open tabs and save each editor
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab.Controls.Count == 0)
                continue;

            var control = tab.Controls[0];
            
            try
            {
                // Check if it's a WorldEditorForm (contains map, entity, and light editors)
                if (control is WorldEditorForm worldEditor)
                {
                    worldEditor.SaveAll(); // Save map, entities, and lights
                    savedCount += 3; // Map + Entity + Lights
                    
                    // Save tiles library from the tile map editor
                    if (!tilesLibrarySaved)
                    {
                        SaveTilesLibraryFromWorldEditor(worldEditor);
                        tilesLibrarySaved = true;
                        savedCount++; // tiles.json
                    }
                }
                // Check if it's a TileMapEditorForm (standalone tile map editor)
                else if (control is TileMapEditorForm tileMapEditor)
                {
                    // Force save to world.json (the default file the game loads)
                    tileMapEditor.Save(forceWorldJson: true);
                    savedCount++; // world.json
                    
                    // Save tiles library
                    if (!tilesLibrarySaved)
                    {
                        tileMapEditor.SaveTileLibraryPublic();
                        tilesLibrarySaved = true;
                        savedCount++; // tiles.json
                    }
                }
                // Check if it's a DialogEditorForm
                else if (control is DialogEditorForm dialogEditor)
                {
                    dialogEditor.Save();
                    savedCount++;
                }
                // Check if it's an ItemEditorForm
                else if (control is ItemEditorForm itemEditor)
                {
                    itemEditor.Save();
                    savedCount++;
                }
                // Check if it's a PlayerEditorForm
                else if (control is PlayerEditorForm playerEditor)
                {
                    playerEditor.Save();
                    savedCount++;
                }
                // Skip GameHostForm (no save needed)
                else if (control is GameHostForm)
                {
                    // Game view doesn't need saving
                    continue;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errors.Add($"{tab.Text}: {ex.Message}");
            }
        }

        // Show result message
        if (errorCount == 0)
        {
            MessageBox.Show($"Successfully saved {savedCount} file(s)!", "Save All", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            var errorMsg = $"Saved {savedCount} file(s) successfully.\n\nErrors occurred while saving {errorCount} file(s):\n\n" + 
                          string.Join("\n", errors);
            MessageBox.Show(errorMsg, "Save All - Partial Success", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    
    /// <summary>
    /// Saves the tiles library from a WorldEditorForm.
    /// </summary>
    private void SaveTilesLibraryFromWorldEditor(WorldEditorForm worldEditor)
    {
        try
        {
            // Use reflection to access the private _tileMapEditor field
            var field = typeof(WorldEditorForm).GetField("_tileMapEditor", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var tileMapEditor = field.GetValue(worldEditor) as TileMapEditorForm;
                if (tileMapEditor != null)
                {
                    tileMapEditor.SaveTileLibraryPublic();
                }
            }
        }
        catch
        {
            // Ignore reflection errors
        }
    }

    private void ShowVersionDialog()
    {
        using var dialog = new VersionDialog();
        dialog.ShowDialog(this);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsDialog();
        dialog.ShowDialog(this);
    }

    private void OpenApiConfiguration()
    {
        using var dialog = new ApiKeyConfigurationDialog();
        dialog.ShowDialog(this);
    }

    private void OpenGitControl()
    {
        using var dialog = new GitControlDialog();
        dialog.ShowDialog(this);
    }
}

/// <summary>
/// Dialog for editor settings.
/// </summary>
public class SettingsDialog : Form
{
    private CheckBox? _perforceCheckBox;
    private Label? _perforceStatusLabel;
    private TextBox? _perforceServerTextBox;
    private TextBox? _perforceUserTextBox;
    private TextBox? _perforcePasswordTextBox;
    private TextBox? _perforceWorkspaceTextBox;
    private TextBox? _perforceExecutablePathTextBox;

    public SettingsDialog()
    {
        Text = "Editor Settings";
        Size = new Size(550, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // Perforce Integration Section
        var perforceLabel = new Label
        {
            Text = "Version Control:",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold)
        };

        _perforceCheckBox = new CheckBox
        {
            Text = "Enable Perforce Integration",
            Location = new Point(20, 45),
            AutoSize = true,
            Checked = EditorSettings.GetEnablePerforceIntegration()
        };
        _perforceCheckBox.CheckedChanged += (s, e) => UpdatePerforceStatus();

        _perforceStatusLabel = new Label
        {
            Location = new Point(40, 70),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        // Perforce Server
        var serverLabel = new Label
        {
            Text = "Server:",
            Location = new Point(20, 100),
            AutoSize = true
        };
        _perforceServerTextBox = new TextBox
        {
            Location = new Point(120, 97),
            Width = 400,
            Text = EditorSettings.GetPerforceServer() ?? ""
        };
        var serverHint = new Label
        {
            Text = "e.g., perforce:1666 or perforce.company.com:1666",
            Location = new Point(120, 120),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font(Font.FontFamily, Font.Size * 0.85f)
        };

        // Perforce Username
        var userLabel = new Label
        {
            Text = "Username:",
            Location = new Point(20, 145),
            AutoSize = true
        };
        _perforceUserTextBox = new TextBox
        {
            Location = new Point(120, 142),
            Width = 400,
            Text = EditorSettings.GetPerforceUser() ?? ""
        };

        // Perforce Password
        var passwordLabel = new Label
        {
            Text = "Password:",
            Location = new Point(20, 170),
            AutoSize = true
        };
        _perforcePasswordTextBox = new TextBox
        {
            Location = new Point(120, 167),
            Width = 400,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetPerforcePassword() ?? ""
        };

        // Perforce Workspace
        var workspaceLabel = new Label
        {
            Text = "Workspace:",
            Location = new Point(20, 195),
            AutoSize = true
        };
        _perforceWorkspaceTextBox = new TextBox
        {
            Location = new Point(120, 192),
            Width = 400,
            Text = EditorSettings.GetPerforceWorkspace() ?? ""
        };
        var workspaceHint = new Label
        {
            Text = "Your Perforce client/workspace name",
            Location = new Point(120, 215),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font(Font.FontFamily, Font.Size * 0.85f)
        };

        // Perforce Executable Path
        var executableLabel = new Label
        {
            Text = "p4.exe Path:",
            Location = new Point(20, 240),
            AutoSize = true
        };
        _perforceExecutablePathTextBox = new TextBox
        {
            Location = new Point(120, 237),
            Width = 350,
            Text = EditorSettings.GetPerforceExecutablePath() ?? ""
        };
        var browseButton = new Button
        {
            Text = "Browse...",
            Location = new Point(480, 235),
            Width = 60
        };
        browseButton.Click += (s, e) =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select p4.exe",
                FileName = "p4.exe"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _perforceExecutablePathTextBox!.Text = dialog.FileName;
                // Reset availability check so it rechecks with new path
                PerforceService.ResetAvailability();
                UpdatePerforceStatus();
            }
        };
        var executableHint = new Label
        {
            Text = "Leave empty to auto-detect (optional)",
            Location = new Point(120, 260),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font(Font.FontFamily, Font.Size * 0.85f)
        };

        var infoLabel = new Label
        {
            Text = "When enabled, the editor will automatically check out files and add new files to Perforce when saving.",
            Location = new Point(20, 285),
            AutoSize = true,
            MaximumSize = new Size(500, 0)
        };

        var testButton = new Button
        {
            Text = "Test Connection",
            Location = new Point(20, 315),
            Width = 120
        };
        testButton.Click += (s, e) => TestPerforceConnection();

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(370, 315),
            Width = 75
        };
        saveButton.Click += (s, e) =>
        {
            EditorSettings.SetEnablePerforceIntegration(_perforceCheckBox?.Checked ?? true);
            EditorSettings.SetPerforceServer(_perforceServerTextBox?.Text?.Trim());
            EditorSettings.SetPerforceUser(_perforceUserTextBox?.Text?.Trim());
            EditorSettings.SetPerforcePassword(_perforcePasswordTextBox?.Text?.Trim());
            EditorSettings.SetPerforceWorkspace(_perforceWorkspaceTextBox?.Text?.Trim());
            EditorSettings.SetPerforceExecutablePath(_perforceExecutablePathTextBox?.Text?.Trim());
            // Reset availability check so it rechecks with new path
            PerforceService.ResetAvailability();
            MessageBox.Show("Settings saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Close dialog after saving
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(455, 315),
            Width = 75
        };

        Controls.Add(perforceLabel);
        Controls.Add(_perforceCheckBox);
        Controls.Add(_perforceStatusLabel);
        Controls.Add(serverLabel);
        Controls.Add(_perforceServerTextBox);
        Controls.Add(serverHint);
        Controls.Add(userLabel);
        Controls.Add(_perforceUserTextBox);
        Controls.Add(passwordLabel);
        Controls.Add(_perforcePasswordTextBox);
        Controls.Add(workspaceLabel);
        Controls.Add(_perforceWorkspaceTextBox);
        Controls.Add(workspaceHint);
        Controls.Add(executableLabel);
        Controls.Add(_perforceExecutablePathTextBox);
        Controls.Add(browseButton);
        Controls.Add(executableHint);
        Controls.Add(infoLabel);
        Controls.Add(testButton);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        // Update status when settings change (after all controls are created)
        _perforceServerTextBox.TextChanged += (s, e) => UpdatePerforceStatus();
        _perforceUserTextBox.TextChanged += (s, e) => UpdatePerforceStatus();
        _perforceExecutablePathTextBox.TextChanged += (s, e) => UpdatePerforceStatus();
        
        UpdatePerforceStatus();
    }

    private void TestPerforceConnection()
    {
        var server = _perforceServerTextBox?.Text?.Trim();
        var user = _perforceUserTextBox?.Text?.Trim();
        var password = _perforcePasswordTextBox?.Text?.Trim();
        var workspace = _perforceWorkspaceTextBox?.Text?.Trim();
        var executablePath = _perforceExecutablePathTextBox?.Text?.Trim();

        if (string.IsNullOrEmpty(server))
        {
            MessageBox.Show("Please enter a Perforce server address.", "Missing Server", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Temporarily set settings to test
            var originalServer = EditorSettings.GetPerforceServer();
            var originalUser = EditorSettings.GetPerforceUser();
            var originalPassword = EditorSettings.GetPerforcePassword();
            var originalWorkspace = EditorSettings.GetPerforceWorkspace();
            var originalExecutablePath = EditorSettings.GetPerforceExecutablePath();

            EditorSettings.SetPerforceServer(server);
            EditorSettings.SetPerforceUser(user);
            EditorSettings.SetPerforcePassword(password);
            EditorSettings.SetPerforceWorkspace(workspace);
            EditorSettings.SetPerforceExecutablePath(executablePath);

            // Reset availability check
            PerforceService.ResetAvailability();

            // Test connection
            var result = PerforceService.TestConnection();
            
            // Restore original settings
            EditorSettings.SetPerforceServer(originalServer);
            EditorSettings.SetPerforceUser(originalUser);
            EditorSettings.SetPerforcePassword(originalPassword);
            EditorSettings.SetPerforceWorkspace(originalWorkspace);
            EditorSettings.SetPerforceExecutablePath(originalExecutablePath);

            // Reset availability check again
            PerforceService.ResetAvailability();

            if (result.success)
            {
                MessageBox.Show($"Connection successful!\n\n{result.message}", "Connection Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdatePerforceStatus();
            }
            else
            {
                MessageBox.Show($"Connection failed:\n\n{result.message}", "Connection Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdatePerforceStatus()
    {
        if (_perforceStatusLabel == null)
            return;

        if (!(_perforceCheckBox?.Checked ?? false))
        {
            _perforceStatusLabel.Text = "Perforce integration is disabled.";
            _perforceStatusLabel.ForeColor = Color.Gray;
        }
        else
        {
            // Reset and check availability
            PerforceService.ResetAvailability();
            var isAvailable = PerforceService.IsAvailable();
            
            if (!isAvailable)
            {
                var customPath = _perforceExecutablePathTextBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(customPath))
                {
                    if (File.Exists(customPath))
                    {
                        _perforceStatusLabel.Text = "Custom p4.exe path set. Click 'Test Connection' to verify.";
                        _perforceStatusLabel.ForeColor = Color.Blue;
                    }
                    else
                    {
                        _perforceStatusLabel.Text = "Custom p4.exe path not found. Please check the path.";
                        _perforceStatusLabel.ForeColor = Color.Red;
                    }
                }
                else
                {
                    _perforceStatusLabel.Text = "Perforce executable (p4.exe) not found. Specify path manually or install Perforce.";
                    _perforceStatusLabel.ForeColor = Color.Orange;
                }
            }
            else
            {
                var server = _perforceServerTextBox?.Text?.Trim();
                var user = _perforceUserTextBox?.Text?.Trim();
                
                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user))
                {
                    _perforceStatusLabel.Text = "Perforce found! Enter server and username to configure.";
                    _perforceStatusLabel.ForeColor = Color.Blue;
                }
                else
                {
                    _perforceStatusLabel.Text = "Perforce is configured. Click 'Test Connection' to verify.";
                    _perforceStatusLabel.ForeColor = Color.Green;
                }
            }
        }
    }
}

/// <summary>
/// Dialog that displays the application version.
/// </summary>
public class VersionDialog : Form
{
    public VersionDialog()
    {
        Text = "Version";
        Size = new Size(300, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var versionLabel = new Label
        {
            Text = "IsoEditor\nVersion: 002",
            AutoSize = false,
            Size = new Size(260, 60),
            Location = new Point(20, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 12)
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 30),
            Location = new Point(110, 80)
        };

        Controls.Add(versionLabel);
        Controls.Add(okButton);
        AcceptButton = okButton;
    }
}

