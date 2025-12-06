using GameEditor.Controls;
using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing tile maps.
/// </summary>
public partial class TileMapEditorForm : Form
{
    private TileMapCanvas? _canvas;
    private TileMapData? _tileMap;
    private Panel? _toolPanel;
    private TilePaletteListBox? _tilePalette;
    private CheckBox? _showGridCheckBox;
    private CheckBox? _showCollisionCheckBox;
    private CheckBox? _showBoundingBoxesCheckBox;
    private ToolStripTextBox? _widthTextBox;
    private ToolStripTextBox? _heightTextBox;
    private string? _currentFilePath;
    private string? _currentTileLibraryPath;
    private Panel? _tilePropertiesPanel;
    private Label? _tileNameLabel;
    private Label? _tileDescriptionLabel;
    private Panel? _tileColorPreview;
    private Label? _tileGraphicPathLabel;
    private Button? _editTileButton;
    private DockablePanel? _dockablePalette;
    private DockablePanel? _dockableProperties;
    private SplitContainer? _leftSplitContainer;
    private SplitContainer? _contentSplitContainer;
    private TileLibraryData? _tileLibrary;
    private TabControl? _tileSetTabs;
    private Dictionary<string, TileLibraryData> _loadedTileSets = new();
    private Dictionary<string, string> _tileSetPaths = new(); // Maps tab name to file path
    
    // AI Generation controls (placeholders for future implementation)
#pragma warning disable CS0649, CS0169 // Fields are never assigned/used - future AI features
    private ComboBox? _aiPromptTemplateComboBox;
    private TextBox? _aiPromptTextBox;
    private ComboBox? _aiProviderComboBox;
    private ComboBox? _aiImageSizeComboBox;
    private Button? _aiGenerateButton;
    private Button? _aiConfigureKeysButton;
    private FreeImageGenerationService? _freeImageService;
#pragma warning restore CS0649, CS0169
    private OpenAIImageService? _aiService;

    /// <summary>
    /// Gets the current file path for this editor.
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;

    /// <summary>
    /// Gets the canvas control for external access.
    /// </summary>
    public TileMapCanvas? Canvas => _canvas;

    /// <summary>
    /// Saves the current map. Public method for external access.
    /// If forceWorldJson is true, saves to world.json regardless of current file path.
    /// </summary>
    public void Save(bool forceWorldJson = false)
    {
        if (forceWorldJson)
        {
            // Force save to world.json (the file the game loads)
            var defaultMapPath = GetDefaultMapPath();
            if (defaultMapPath != null)
            {
                _currentFilePath = defaultMapPath;
                Text = $"Tile Map Editor - {Path.GetFileName(_currentFilePath)}";
                if (_canvas != null)
                {
                    _canvas.MapFileName = Path.GetFileName(_currentFilePath);
                }
            }
        }
        SaveMap();
    }
    
    /// <summary>
    /// Saves the tile library (tiles.json). Public method for external access.
    /// </summary>
    public void SaveTileLibraryPublic() => SaveTileLibrary();

    public TileMapEditorForm(string? filePath = null)
    {
        _currentFilePath = filePath;
        InitializeComponent();
        
        // Validate GameContent path (ensures consistency with game)
        PathHelper.ValidateGameContentPath();
        
        // Initialize tile library path to default
        _currentTileLibraryPath = PathHelper.GetTilesLibraryPath();
        
        // Load default tile library and add as first tab
        LoadTileLibrary();
        if (_tileLibrary != null && _currentTileLibraryPath != null)
        {
            var defaultName = Path.GetFileNameWithoutExtension(_currentTileLibraryPath);
            _loadedTileSets[defaultName] = _tileLibrary;
            _tileSetPaths[defaultName] = _currentTileLibraryPath;
            if (_tileSetTabs != null)
            {
                AddTileSetTab(defaultName);
            }
        }
        
        LoadTileMap(filePath);
        LoadEntityLayout(); // Load entities to show on map
        LoadLightLayout(); // Load lights to show on map
    }

    private void InitializeComponent()
    {
        Text = "Tile Map Editor";
        // No fixed size - will fill tab container
        MinimumSize = new Size(600, 400);

        // Main split container: palette on left, content on right
        _contentSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        _contentSplitContainer.Panel1MinSize = 150; // Minimum width for palette panel
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid issues when form is embedded in tabs

        // Tool panel at top of right side (above canvas only) - two rows
        _toolPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70  // Two rows of buttons
        };

        // Main toolbar - first row
        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Height = 35
        };
        
        // Second toolbar for additional buttons
        var toolStrip2 = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Height = 35
        };

        // File operations
        var newButton = new ToolStripButton("New", null, (s, e) => NewMap());
        var openButton = new ToolStripButton("Open", null, (s, e) => OpenMap());
        var saveButton = new ToolStripButton("Save", null, (s, e) => SaveMap());
        var saveAsButton = new ToolStripButton("Save As", null, (s, e) => SaveMapAs());
        toolStrip.Items.Add(newButton);
        toolStrip.Items.Add(openButton);
        toolStrip.Items.Add(saveButton);
        toolStrip.Items.Add(saveAsButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        
        // Reset Layout button
        var resetLayoutButton = new ToolStripButton("Reset Layout", null, (s, e) => ResetLayout());
        toolStrip.Items.Add(resetLayoutButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        // Player start button on second row for visibility
        var setPlayerStartButton = new ToolStripButton("Set Player Start", null, (s, e) => SetPlayerStartMode());
        var centerPlayerStartButton = new ToolStripButton("Center on Player Start", null, (s, e) => CenterOnPlayerStart());
        var mapPropertiesButton = new ToolStripButton("Map Properties", null, (s, e) => EditMapProperties());
        var editCollisionButton = new ToolStripButton("Edit Collision", null, (s, e) => ToggleCollisionEditMode());
        var exportTilesButton = new ToolStripButton("Export Tiles to PNG", null, (s, e) => ExportTiles());
        toolStrip2.Items.Add(setPlayerStartButton);
        toolStrip2.Items.Add(centerPlayerStartButton);
        toolStrip2.Items.Add(mapPropertiesButton);
        toolStrip2.Items.Add(new ToolStripSeparator());
        toolStrip2.Items.Add(editCollisionButton);
        toolStrip2.Items.Add(new ToolStripSeparator());
        toolStrip2.Items.Add(exportTilesButton);

        // Map properties
        var widthLabel = new ToolStripLabel("Width:");
        _widthTextBox = new ToolStripTextBox { Width = 45, Text = "20" };
        var heightLabel = new ToolStripLabel("H:");
        _heightTextBox = new ToolStripTextBox { Width = 45, Text = "20" };
        var resizeButton = new ToolStripButton("Resize", null, (s, e) => ResizeMap());
        toolStrip.Items.Add(widthLabel);
        toolStrip.Items.Add(_widthTextBox);
        toolStrip.Items.Add(heightLabel);
        toolStrip.Items.Add(_heightTextBox);
        toolStrip.Items.Add(resizeButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        // View options
        _showGridCheckBox = new CheckBox { Text = "Show Grid", Checked = true, AutoSize = true, Padding = new Padding(5, 0, 5, 0) };
        _showCollisionCheckBox = new CheckBox { Text = "Show Collision", AutoSize = true, Padding = new Padding(5, 0, 5, 0) };
        _showBoundingBoxesCheckBox = new CheckBox { Text = "Show Bounds", AutoSize = true, Padding = new Padding(5, 0, 5, 0) };
        _showGridCheckBox.CheckedChanged += (s, e) => 
        {
            if (_canvas != null) _canvas.ShowGrid = _showGridCheckBox.Checked;
        };
        _showCollisionCheckBox.CheckedChanged += (s, e) => 
        {
            if (_canvas != null) _canvas.ShowCollision = _showCollisionCheckBox.Checked;
        };
        _showBoundingBoxesCheckBox.CheckedChanged += (s, e) => 
        {
            if (_canvas != null) _canvas.ShowBoundingBoxes = _showBoundingBoxesCheckBox.Checked;
        };

        // Create a panel for checkboxes that fits in the toolbar
        var checkBoxContainer = new ToolStripControlHost(_showGridCheckBox);
        var checkBoxContainer2 = new ToolStripControlHost(_showCollisionCheckBox);
        var checkBoxContainer3 = new ToolStripControlHost(_showBoundingBoxesCheckBox);
        toolStrip.Items.Add(checkBoxContainer);
        toolStrip.Items.Add(checkBoxContainer2);
        toolStrip.Items.Add(checkBoxContainer3);

        // Add toolbars to panel (order matters - add bottom first, then top)
        _toolPanel.Controls.Add(toolStrip2);  // Second toolbar (bottom row)
        _toolPanel.Controls.Add(toolStrip);   // First toolbar (top row)

        // Tile palette panel
        var palettePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Width = 200
        };

        // Tile set tabs at top of palette
        _tileSetTabs = new TabControl
        {
            Dock = DockStyle.Top,
            Height = 30,
            Appearance = TabAppearance.Normal
        };
        _tileSetTabs.SelectedIndexChanged += (s, e) => SwitchToTileSet();

        // Panel for tile set controls at bottom of palette
        var tileSetControlsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 150, // Height for multiple buttons (increased for new button)
            Padding = new Padding(5)
        };

        // Add button to create new tile
        var addTileButton = new Button
        {
            Text = "+ New Tile",
            Dock = DockStyle.Top,
            Height = 30,
            UseVisualStyleBackColor = true
        };
        addTileButton.Click += (s, e) =>
        {
            if (_tilePalette != null)
            {
                // Ensure tile library is loaded (preserves existing tiles)
                if (_tileLibrary == null)
                {
                    LoadTileLibrary();
                }
                
                // Ensure tile library exists
                if (_tileLibrary == null)
                {
                    _tileLibrary = new TileLibraryData();
                }
                
                // Ensure TileGraphics dictionary exists
                if (_tileLibrary.TileGraphics == null)
                {
                    _tileLibrary.TileGraphics = new Dictionary<int, string>();
                }
                
                // Increment the tile count in the library
                var newTileIndex = _tileLibrary.NumTiles;
                _tileLibrary.NumTiles = newTileIndex + 1;
                
                // Update the palette to match the library
                _tilePalette.NumTiles = _tileLibrary.NumTiles;
                _tilePalette.SelectedIndex = newTileIndex; // Select the newly created tile
                
                // New tile starts with default procedural graphics (no custom graphic path)
                // It will be added to tiles.json when/if a custom graphic is assigned
                
                // Save the tile library to tiles.json (includes the new tile count)
                SaveTileLibrary();
                
                // Update the palette to show the new tile from the library
                if (_tileMap != null)
                {
                    var paletteTileMap = new TileMapData
                    {
                        Width = _tileMap.Width,
                        Height = _tileMap.Height,
                        TileWidth = _tileMap.TileWidth,
                        TileHeight = _tileMap.TileHeight,
                        TileGraphics = _tileLibrary.TileGraphics
                    };
                    _tilePalette.TileMap = paletteTileMap;
                    
                    // Also update the canvas TileMap to include the new tile count
                    // This ensures the canvas can place all tiles including newly created ones
                    // CRITICAL: Preserve existing tiles and player start position to prevent data loss!
                    var canvasTileMap = new TileMapData
                    {
                        Width = _tileMap.Width,
                        Height = _tileMap.Height,
                        TileWidth = _tileMap.TileWidth,
                        TileHeight = _tileMap.TileHeight,
                        Tiles = _tileMap.Tiles, // Preserve existing tiles - prevents data loss!
                        PlayerStartPosition = _tileMap.PlayerStartPosition, // Preserve player start position
                        TileGraphics = _tileLibrary.TileGraphics
                    };
                    if (_canvas != null)
                    {
                        _canvas.TileMap = canvasTileMap;
                    }
                }
            }
        };

        _tilePalette = new TilePaletteListBox
        {
            Dock = DockStyle.Fill,
            TileWidth = 64,  // Default tile dimensions
            TileHeight = 32,
            TilePreviewSize = new Size(48, 24),
            NumTiles = 8
        };

        _tilePalette.SelectedIndex = 0;
        _tilePalette.SelectedIndexChanged += (s, e) =>
        {
            if (_canvas != null && _tilePalette.SelectedIndex >= 0)
            {
                _canvas.SelectedTileIndex = _tilePalette.SelectedIndex;
            }
            UpdateTileProperties();
        };
        
        // Handle right-click to edit tile
        _tilePalette.TileEditRequested += (s, e) =>
        {
            EditTile(e.TileIndex);
        };

        // New Tile Set button
        var newTileSetButton = new Button
        {
            Text = "New Tile Set",
            Dock = DockStyle.Top,
            Height = 30,
            UseVisualStyleBackColor = true
        };
        newTileSetButton.Click += (s, e) => NewTileSet();

        // Add Tile Set button (loads and adds to tabs)
        var addTileSetButton = new Button
        {
            Text = "Add Tile Set",
            Dock = DockStyle.Top,
            Height = 30,
            UseVisualStyleBackColor = true
        };
        addTileSetButton.Click += (s, e) => AddTileSet();

        // Unload Tile Set button
        var unloadTileSetButton = new Button
        {
            Text = "Unload Tile Set",
            Dock = DockStyle.Top,
            Height = 30,
            UseVisualStyleBackColor = true
        };
        unloadTileSetButton.Click += (s, e) => UnloadTileSet();

        // Save Tile Set As button
        var saveTileSetAsButton = new Button
        {
            Text = "Save Tile Set As",
            Dock = DockStyle.Top,
            Height = 30,
            UseVisualStyleBackColor = true
        };
        saveTileSetAsButton.Click += (s, e) => SaveTileSetAs();

        // Add buttons to tile set controls panel (order matters - add bottom to top)
        tileSetControlsPanel.Controls.Add(addTileButton);
        tileSetControlsPanel.Controls.Add(saveTileSetAsButton);
        tileSetControlsPanel.Controls.Add(unloadTileSetButton);
        tileSetControlsPanel.Controls.Add(addTileSetButton);
        tileSetControlsPanel.Controls.Add(newTileSetButton);

        // Add controls in order: tabs first (top), then palette (fills space), then controls panel (bottom)
        // Add controls in order: tabs first (top), then palette (fills space), then controls panel (bottom)
        if (_tileSetTabs != null)
        {
            palettePanel.Controls.Add(_tileSetTabs);
        }
        palettePanel.Controls.Add(_tilePalette);
        palettePanel.Controls.Add(tileSetControlsPanel);
        
        // Properties panel
        _tilePropertiesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8, 8, 8, 8)
        };
        
        var propertiesLabel = new Label
        {
            Text = "Tile Properties",
            Dock = DockStyle.Top,
            Height = 25,
            Font = new Font(DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 5, 5, 5)
        };
        
        _tileNameLabel = new Label
        {
            Text = "Name: Tile 0",
            Dock = DockStyle.Top,
            Height = 25,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 2, 5, 2)
        };
        
        _tileDescriptionLabel = new Label
        {
            Text = "Description:",
            Dock = DockStyle.Top,
            Height = 50,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(5, 2, 5, 2)
        };
        
        var colorLabel = new Label
        {
            Text = "Color:",
            Dock = DockStyle.Top,
            Height = 25,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 2, 5, 2)
        };
        
        _tileColorPreview = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = GetDefaultTileColor(0),
            Margin = new Padding(5, 2, 5, 2)
        };
        
        _tileGraphicPathLabel = new Label
        {
            Text = "Graphic: None",
            Dock = DockStyle.Top,
            Height = 50,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(5, 2, 5, 2)
        };
        
        _editTileButton = new Button
        {
            Text = "Edit Tile Properties...",
            Dock = DockStyle.Bottom,
            Height = 35,
            Margin = new Padding(5, 5, 5, 5)
        };
        _editTileButton.Click += (s, e) =>
        {
            if (_tilePalette != null && _tilePalette.SelectedIndex >= 0)
            {
                EditTile(_tilePalette.SelectedIndex);
            }
        };
        
        _tilePropertiesPanel.Controls.Add(_editTileButton);
        _tilePropertiesPanel.Controls.Add(_tileGraphicPathLabel);
        _tilePropertiesPanel.Controls.Add(_tileColorPreview);
        _tilePropertiesPanel.Controls.Add(colorLabel);
        _tilePropertiesPanel.Controls.Add(_tileDescriptionLabel);
        _tilePropertiesPanel.Controls.Add(_tileNameLabel);
        _tilePropertiesPanel.Controls.Add(propertiesLabel);

        // Wrap palette in dockable panel
        _dockablePalette = new DockablePanel
        {
            Title = "Tile Palette",
            Dock = DockStyle.Fill,
            CanFloat = true,
            CanClose = false,
            ShowTitleBar = true
        };
        _dockablePalette.SetContent(palettePanel);

        // Wrap properties in dockable panel
        _dockableProperties = new DockablePanel
        {
            Title = "Tile Properties",
            Dock = DockStyle.Fill,
            CanFloat = true,
            CanClose = false,
            ShowTitleBar = true
        };
        _dockableProperties.SetContent(_tilePropertiesPanel);

        // Use split container to arrange palette and properties vertically
        _leftSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.Panel2
        };
        _leftSplitContainer.Panel1MinSize = 100; // Minimum height for palette panel
        // Note: Panel2MinSize set in Load event to avoid issues when form is embedded in tabs
        _leftSplitContainer.Panel1.Controls.Add(_dockablePalette);
        _leftSplitContainer.Panel2.Controls.Add(_dockableProperties);

        // Canvas on right
        _canvas = new TileMapCanvas
        {
            Dock = DockStyle.Fill
        };
        _canvas.TileChanged += (s, e) => { /* Handle tile change if needed */ };
        _canvas.PlayerStartSet += (s, e) =>
        {
            MessageBox.Show($"Player start position set to ({e.X:F1}, {e.Y:F1})", "Player Start", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        // Right side: toolbar at top, canvas below
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        rightPanel.Controls.Add(_toolPanel);
        rightPanel.Controls.Add(_canvas);

        // Left side: dockable palette and properties in split container
        _contentSplitContainer.Panel1.Controls.Add(_leftSplitContainer);
        // Right side: toolbar and canvas
        _contentSplitContainer.Panel2.Controls.Add(rightPanel);
        
        Controls.Add(_contentSplitContainer);
        
        // Ensure splitter distances are valid after form is loaded
        // Maximize canvas space by keeping palette narrow
        Load += (s, e) =>
        {
            // Set Panel2MinSize after control is sized to avoid crashes when embedded in tabs
            var availableWidth = _contentSplitContainer.Width;
            if (availableWidth > 0)
            {
                // Set minimum to 250 or 30% of available width, whichever is smaller
                _contentSplitContainer.Panel2MinSize = Math.Min(300, (int)(availableWidth * 0.3));
            }
            else
            {
                _contentSplitContainer.Panel2MinSize = 250; // Fallback minimum
            }
            
            if (_contentSplitContainer.Width > 0)
            {
                // Set palette to minimum width to maximize canvas
                var preferredPaletteWidth = 200;
                var maxDistance = _contentSplitContainer.Width - _contentSplitContainer.Panel2MinSize;
                var minDistance = _contentSplitContainer.Panel1MinSize;
                
                // Use preferred width if it fits, otherwise use minimum
                // Ensure we don't make canvas too small
                if (maxDistance >= minDistance)
                {
                    _contentSplitContainer.SplitterDistance = Math.Max(minDistance, Math.Min(preferredPaletteWidth, maxDistance));
                }
                else
                {
                    _contentSplitContainer.SplitterDistance = minDistance;
                }
            }
            
            // Set palette splitter position (properties panel takes ~200px)
            // Find the palette split container in the left panel
            var leftPanel = _contentSplitContainer.Panel1;
            foreach (Control control in leftPanel.Controls)
            {
                if (control is SplitContainer split && split.Orientation == Orientation.Horizontal)
                {
                    // Set Panel2MinSize after control is sized to avoid crashes
                    if (split.Height > 0)
                    {
                        // Set minimum to 250 or 30% of available height, whichever is smaller (larger for better readability)
                        split.Panel2MinSize = Math.Min(250, (int)(split.Height * 0.3));
                    }
                    else
                    {
                        split.Panel2MinSize = 220; // Fallback minimum (increased for better readability)
                    }
                    
                    if (split.Height > 0)
                    {
                        var propertiesHeight = 220; // Increased for better visibility
                        var maxPropertiesHeight = split.Height - split.Panel1MinSize;
                        var minPropertiesHeight = split.Panel2MinSize;
                        if (maxPropertiesHeight >= minPropertiesHeight)
                        {
                            var actualPropertiesHeight = Math.Max(minPropertiesHeight, Math.Min(propertiesHeight, maxPropertiesHeight));
                            split.SplitterDistance = split.Height - actualPropertiesHeight;
                        }
                        else
                        {
                            split.SplitterDistance = split.Height - minPropertiesHeight;
                        }
                    }
                    break;
                }
            }
            
            // Initialize tile properties display
            UpdateTileProperties();
        };
    }

    private void NewMap()
    {
        var dialog = new NewMapDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _tileMap = new TileMapData
            {
                Width = dialog.Width,
                Height = dialog.Height,
                TileWidth = dialog.TileWidth,
                TileHeight = dialog.TileHeight
            };
            _tileMap.InitializeTiles();
            // SolidTiles feature not currently implemented

            if (_canvas != null)
            {
                _canvas.TileMap = _tileMap;
            }

            // Update tile palette to match tile size
            if (_tilePalette != null)
            {
                _tilePalette.TileWidth = dialog.TileWidth;
                _tilePalette.TileHeight = dialog.TileHeight;
                // Calculate preview size, but cap it to fit in the browser (max 64px wide)
                var widthScale = (float)dialog.TileWidth / 64f;
                var heightScale = (float)dialog.TileHeight / 32f;
                var previewWidth = (int)(48 * widthScale);
                var previewHeight = (int)(24 * heightScale);
                
                // Cap at maximum size to fit in browser (maintain aspect ratio)
                const int maxPreviewWidth = 64;
                const int maxPreviewHeight = 32;
                if (previewWidth > maxPreviewWidth || previewHeight > maxPreviewHeight)
                {
                    var scaleFactor = Math.Min((float)maxPreviewWidth / previewWidth, (float)maxPreviewHeight / previewHeight);
                    previewWidth = (int)(previewWidth * scaleFactor);
                    previewHeight = (int)(previewHeight * scaleFactor);
                }
                
                _tilePalette.TilePreviewSize = new Size(previewWidth, previewHeight);
            }

            _currentFilePath = null;
            Text = "Tile Map Editor - New Map";
        }
    }

    private void OpenMap()
    {
        var mapsDir = PathHelper.GetMapsDirectory();
        if (mapsDir == null)
        {
            mapsDir = Path.Combine(Application.StartupPath, "..", "GameContent", "maps");
            mapsDir = Path.GetFullPath(mapsDir);
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = mapsDir
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadTileMap(dialog.FileName);
        }
    }

    private void LoadTileMap(string? filePath)
    {
        // If specific file path provided, try to load it
        if (filePath != null)
        {
            if (File.Exists(filePath))
            {
                _tileMap = TileMapSerializer.LoadFromJson(filePath);
                if (_tileMap != null)
                {
                    _currentFilePath = filePath;
                    Text = $"Tile Map Editor - {Path.GetFileName(filePath)}";
                    if (_canvas != null)
                    {
                        _canvas.MapFileName = Path.GetFileName(filePath);
                    }
                }
                else
                {
                    // File exists but failed to load - show error
                    MessageBox.Show($"Failed to load map file: {filePath}\n\nThe file may be corrupted or in an invalid format.", 
                        "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                // File path provided but doesn't exist - show error
                MessageBox.Show($"Map file not found: {filePath}", 
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        // Only create default map if no file was specified or loading failed
        if (_tileMap == null)
        {
            // Try to load default game map from GameContent/maps directory
            var defaultMapPath = GetDefaultMapPath();
            if (defaultMapPath != null && File.Exists(defaultMapPath))
            {
                _tileMap = TileMapSerializer.LoadFromJson(defaultMapPath);
                _currentFilePath = defaultMapPath;
                Text = $"Tile Map Editor - {Path.GetFileName(defaultMapPath)}";
            }
            else
            {
                // Create default map if no map file exists
                _tileMap = new TileMapData
                {
                    Width = 20,
                    Height = 20,
                    TileWidth = 64,
                    TileHeight = 32
                };
                _tileMap.InitializeTiles();
                // SolidTiles feature not currently implemented
                
                // Save to default location so game can load it
                if (defaultMapPath != null)
                {
                    try
                    {
                        var mapsDir = Path.GetDirectoryName(defaultMapPath);
                        if (mapsDir != null && !Directory.Exists(mapsDir))
                        {
                            Directory.CreateDirectory(mapsDir);
                        }
                        TileMapSerializer.SaveToJson(_tileMap, defaultMapPath);
                        _currentFilePath = defaultMapPath;
                        Text = $"Tile Map Editor - {Path.GetFileName(defaultMapPath)} (New)";
                        if (_canvas != null)
                        {
                            _canvas.MapFileName = Path.GetFileName(defaultMapPath);
                        }
                    }
                    catch
                    {
                        // If save fails, continue with unsaved map
                    }
                }
            }
        }

        if (_canvas != null && _tileMap != null)
        {
            // Create a TileMapData with graphics from library for the canvas
            var canvasTileMap = new TileMapData
            {
                Width = _tileMap.Width,
                Height = _tileMap.Height,
                TileWidth = _tileMap.TileWidth,
                TileHeight = _tileMap.TileHeight,
                Tiles = _tileMap.Tiles,
                PlayerStartPosition = _tileMap.PlayerStartPosition,
                TileGraphics = _tileLibrary?.TileGraphics
            };
            _canvas.TileMap = canvasTileMap;
            _canvas.MapFileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "New Map";
        }

        // Update tile palette with map's tile dimensions and custom graphics from library
        if (_tilePalette != null && _tileMap != null)
        {
            // Create a temporary TileMapData with graphics from library for the palette
            var paletteTileMap = new TileMapData
            {
                Width = _tileMap.Width,
                Height = _tileMap.Height,
                TileWidth = _tileMap.TileWidth,
                TileHeight = _tileMap.TileHeight,
                TileGraphics = _tileLibrary?.TileGraphics
            };
            _tilePalette.TileMap = paletteTileMap;
            _tilePalette.TileWidth = _tileMap.TileWidth;
            _tilePalette.TileHeight = _tileMap.TileHeight;
            
            // Set the number of tiles from the library
            if (_tileLibrary != null)
            {
                _tilePalette.NumTiles = _tileLibrary.NumTiles;
            }
            // Calculate preview size, but cap it to fit in the browser (max 64px wide)
            var widthScale = (float)_tileMap.TileWidth / 64f;
            var heightScale = (float)_tileMap.TileHeight / 32f;
            var previewWidth = (int)(48 * widthScale);
            var previewHeight = (int)(24 * heightScale);
            
            // Cap at maximum size to fit in browser (maintain aspect ratio)
            const int maxPreviewWidth = 64;
            const int maxPreviewHeight = 32;
            if (previewWidth > maxPreviewWidth || previewHeight > maxPreviewHeight)
            {
                var scaleFactor = Math.Min((float)maxPreviewWidth / previewWidth, (float)maxPreviewHeight / previewHeight);
                previewWidth = (int)(previewWidth * scaleFactor);
                previewHeight = (int)(previewHeight * scaleFactor);
            }
            
            _tilePalette.TilePreviewSize = new Size(previewWidth, previewHeight);
        }

        if (_widthTextBox != null && _heightTextBox != null && _tileMap != null)
        {
            _widthTextBox.TextBox.Text = _tileMap.Width.ToString();
            _heightTextBox.TextBox.Text = _tileMap.Height.ToString();
        }
    }

    private void LoadTileLibrary()
    {
        // Use custom path if set, otherwise use default
        var tilesLibraryPath = _currentTileLibraryPath ?? PathHelper.GetTilesLibraryPath();
        if (tilesLibraryPath != null)
        {
            _tileLibrary = TileLibrarySerializer.LoadFromJson(tilesLibraryPath);
            if (_tileLibrary == null)
            {
                // Create default library if it doesn't exist
                _tileLibrary = new TileLibraryData();
                SaveTileLibrary();
            }
            else
            {
                // Ensure NumTiles is at least as large as the highest tile index with a graphic
                if (_tileLibrary.TileGraphics != null && _tileLibrary.TileGraphics.Count > 0)
                {
                    var maxTileIndex = _tileLibrary.TileGraphics.Keys.Max();
                    if (_tileLibrary.NumTiles <= maxTileIndex)
                    {
                        _tileLibrary.NumTiles = maxTileIndex + 1;
                    }
                }
            }
            
            // Update tile palette if it exists
            if (_tilePalette != null && _tileLibrary != null && _tileMap != null)
            {
                var paletteTileMap = new TileMapData
                {
                    Width = _tileMap.Width,
                    Height = _tileMap.Height,
                    TileWidth = _tileMap.TileWidth,
                    TileHeight = _tileMap.TileHeight,
                    TileGraphics = _tileLibrary.TileGraphics
                };
                _tilePalette.TileMap = paletteTileMap;
                _tilePalette.NumTiles = _tileLibrary.NumTiles;
            }
        }
    }

    private void SaveTileLibrary()
    {
        if (_tileLibrary == null)
        {
            // Create a new library if it doesn't exist
            _tileLibrary = new TileLibraryData();
        }
        
        // Ensure TileGraphics dictionary exists
        if (_tileLibrary.TileGraphics == null)
        {
            _tileLibrary.TileGraphics = new Dictionary<int, string>();
        }
            
        // Use custom path if set, otherwise use default
        var tilesLibraryPath = _currentTileLibraryPath ?? PathHelper.GetTilesLibraryPath();
        if (tilesLibraryPath != null)
        {
            try
            {
                TileLibrarySerializer.SaveToJson(_tileLibrary, tilesLibraryPath);
                System.Diagnostics.Debug.WriteLine($"[TileMapEditor] Saved tile library to {tilesLibraryPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TileMapEditor] Error saving tile library: {ex.Message}");
                MessageBox.Show($"Error saving tile library: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
    
    private void LoadTileSet()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Load Tile Set",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = PathHelper.GetGameContentPath() ?? Environment.CurrentDirectory
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var loadedLibrary = TileLibrarySerializer.LoadFromJson(dialog.FileName);
            if (loadedLibrary != null)
            {
                _currentTileLibraryPath = dialog.FileName;
                _tileLibrary = loadedLibrary;
                
                // Ensure NumTiles is at least as large as the highest tile index with a graphic
                if (_tileLibrary.TileGraphics != null && _tileLibrary.TileGraphics.Count > 0)
                {
                    var maxTileIndex = _tileLibrary.TileGraphics.Keys.Max();
                    if (_tileLibrary.NumTiles <= maxTileIndex)
                    {
                        _tileLibrary.NumTiles = maxTileIndex + 1;
                    }
                }
                
                // Update tile palette
                if (_tilePalette != null && _tileMap != null)
                {
                    var paletteTileMap = new TileMapData
                    {
                        Width = _tileMap.Width,
                        Height = _tileMap.Height,
                        TileWidth = _tileMap.TileWidth,
                        TileHeight = _tileMap.TileHeight,
                        TileGraphics = _tileLibrary.TileGraphics
                    };
                    _tilePalette.TileMap = paletteTileMap;
                    _tilePalette.NumTiles = _tileLibrary.NumTiles;
                }
                
                // Update canvas to refresh tile graphics
                if (_canvas != null && _tileMap != null)
                {
                    var tilesLibraryPath = _currentTileLibraryPath;
                    if (tilesLibraryPath != null && _currentFilePath != null)
                    {
                        var tileGraphics = GameCore.Rendering.TileMapLoader.GetTileGraphics(_currentFilePath, tilesLibraryPath);
                        if (tileGraphics != null && tileGraphics.Count > 0)
                        {
                            _tileMap.TileGraphics = tileGraphics;
                        }
                    }
                    _canvas.TileMap = _tileMap;
                    _canvas.Invalidate();
                }
                
                MessageBox.Show($"Tile set loaded from:\n{dialog.FileName}", "Tile Set Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Failed to load tile set from:\n{dialog.FileName}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    
    private void NewTileSet()
    {
        // Ask user if they want to save current tile set before creating new one
        if (_tileLibrary != null && _tileLibrary.TileGraphics != null && _tileLibrary.TileGraphics.Count > 0)
        {
            var result = MessageBox.Show(
                "Create a new empty tile set? The current tile set will be replaced.\n\nDo you want to save the current tile set first?",
                "New Tile Set",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Cancel)
            {
                return;
            }
            
            if (result == DialogResult.Yes)
            {
                SaveTileSetAs();
            }
        }
        
        // Create new empty tile set
        _tileLibrary = new TileLibraryData
        {
            NumTiles = 8, // Start with 8 empty tiles
            TileGraphics = new Dictionary<int, string>()
        };
        
        // Reset tile library path (user can save it later)
        _currentTileLibraryPath = null;
        
        // Generate a unique name for the new tile set
        var newTileSetName = "New Tile Set";
        var counter = 1;
        while (_loadedTileSets.ContainsKey(newTileSetName))
        {
            newTileSetName = $"New Tile Set {counter}";
            counter++;
        }
        
        // Add to loaded sets and create tab
        _loadedTileSets[newTileSetName] = _tileLibrary;
        AddTileSetTab(newTileSetName);
        
        // Switch to the new tab
        if (_tileSetTabs != null)
        {
            foreach (TabPage tab in _tileSetTabs.TabPages)
            {
                if (tab.Text == newTileSetName)
                {
                    _tileSetTabs.SelectedTab = tab;
                    break;
                }
            }
        }
        
        // Update tile palette
        if (_tilePalette != null)
        {
            _tilePalette.NumTiles = _tileLibrary.NumTiles;
            if (_tileMap != null)
            {
                var paletteTileMap = new TileMapData
                {
                    Width = _tileMap.Width,
                    Height = _tileMap.Height,
                    TileWidth = _tileMap.TileWidth,
                    TileHeight = _tileMap.TileHeight,
                    TileGraphics = _tileLibrary.TileGraphics
                };
                _tilePalette.TileMap = paletteTileMap;
            }
            _tilePalette.SelectedIndex = 0;
        }
        
        // Update canvas
        if (_canvas != null && _tileMap != null)
        {
            _tileMap.TileGraphics = _tileLibrary.TileGraphics;
            _canvas.TileMap = _tileMap;
            _canvas.Invalidate();
        }
        
        MessageBox.Show("New empty tile set created. Use 'Save Tile Set As' to save it to a file.", "New Tile Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void SaveTileSetAs()
    {
        if (_tileLibrary == null)
        {
            MessageBox.Show("No tile library to save.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        using var dialog = new SaveFileDialog
        {
            Title = "Save Tile Set As",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = PathHelper.GetGameContentPath() ?? Environment.CurrentDirectory,
            FileName = "tiles.json"
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                TileLibrarySerializer.SaveToJson(_tileLibrary, dialog.FileName);
                _currentTileLibraryPath = dialog.FileName;
                
                // Update the tab name if this tile set is already loaded
                var tabName = Path.GetFileNameWithoutExtension(dialog.FileName);
                if (_tileSetTabs != null && _tileSetTabs.SelectedTab != null)
                {
                    _tileSetTabs.SelectedTab.Text = tabName;
                    _tileSetPaths[tabName] = dialog.FileName;
                }
                
                MessageBox.Show($"Tile set saved to:\n{dialog.FileName}", "Tile Set Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tile set: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    
    private void AddTileSet()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add Tile Set",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = PathHelper.GetGameContentPath() ?? Environment.CurrentDirectory
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var loadedLibrary = TileLibrarySerializer.LoadFromJson(dialog.FileName);
            if (loadedLibrary != null)
            {
                var tabName = Path.GetFileNameWithoutExtension(dialog.FileName);
                
                // Check if already loaded
                if (_loadedTileSets.ContainsKey(tabName))
                {
                    MessageBox.Show($"Tile set '{tabName}' is already loaded.", "Already Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Switch to it
                    if (_tileSetTabs != null)
                    {
                        foreach (TabPage tab in _tileSetTabs.TabPages)
                        {
                            if (tab.Text == tabName)
                            {
                                _tileSetTabs.SelectedTab = tab;
                                break;
                            }
                        }
                    }
                    return;
                }
                
                // Ensure NumTiles is at least as large as the highest tile index with a graphic
                if (loadedLibrary.TileGraphics != null && loadedLibrary.TileGraphics.Count > 0)
                {
                    var maxTileIndex = loadedLibrary.TileGraphics.Keys.Max();
                    if (loadedLibrary.NumTiles <= maxTileIndex)
                    {
                        loadedLibrary.NumTiles = maxTileIndex + 1;
                    }
                }
                
                // Add to loaded sets
                _loadedTileSets[tabName] = loadedLibrary;
                _tileSetPaths[tabName] = dialog.FileName;
                
                // Add tab
                AddTileSetTab(tabName);
                
                // Switch to the new tab
                if (_tileSetTabs != null)
                {
                    foreach (TabPage tab in _tileSetTabs.TabPages)
                    {
                        if (tab.Text == tabName)
                        {
                            _tileSetTabs.SelectedTab = tab;
                            break;
                        }
                    }
                }
                
                MessageBox.Show($"Tile set '{tabName}' added.", "Tile Set Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Failed to load tile set from:\n{dialog.FileName}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    
    private void UnloadTileSet()
    {
        if (_tileSetTabs == null || _tileSetTabs.SelectedTab == null)
        {
            MessageBox.Show("No tile set selected to unload.", "Unload Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        var tabName = _tileSetTabs.SelectedTab.Text;
        
        // Don't allow unloading if it's the only tab
        if (_tileSetTabs.TabPages.Count <= 1)
        {
            MessageBox.Show("Cannot unload the last tile set. At least one tile set must remain loaded.", "Unload Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        var result = MessageBox.Show(
            $"Unload tile set '{tabName}'?",
            "Unload Tile Set",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result == DialogResult.Yes)
        {
            // Remove from dictionaries
            _loadedTileSets.Remove(tabName);
            _tileSetPaths.Remove(tabName);
            
            // Remove tab
            var tabToRemove = _tileSetTabs.SelectedTab;
            _tileSetTabs.TabPages.Remove(tabToRemove);
            
            // Switch to first remaining tab
            if (_tileSetTabs.TabPages.Count > 0)
            {
                _tileSetTabs.SelectedTab = _tileSetTabs.TabPages[0];
            }
        }
    }
    
    private void AddTileSetTab(string tabName)
    {
        if (_tileSetTabs == null)
            return;
        
        var tab = new TabPage(tabName);
        _tileSetTabs.TabPages.Add(tab);
    }
    
    private void SwitchToTileSet()
    {
        if (_tileSetTabs == null || _tileSetTabs.SelectedTab == null)
            return;
        
        var tabName = _tileSetTabs.SelectedTab.Text;
        
        if (_loadedTileSets.TryGetValue(tabName, out var tileLibrary))
        {
            _tileLibrary = tileLibrary;
            _currentTileLibraryPath = _tileSetPaths.TryGetValue(tabName, out var path) ? path : null;
            
            // Update tile palette
            if (_tilePalette != null)
            {
                _tilePalette.NumTiles = _tileLibrary.NumTiles;
                if (_tileMap != null)
                {
                    var paletteTileMap = new TileMapData
                    {
                        Width = _tileMap.Width,
                        Height = _tileMap.Height,
                        TileWidth = _tileMap.TileWidth,
                        TileHeight = _tileMap.TileHeight,
                        TileGraphics = _tileLibrary.TileGraphics
                    };
                    _tilePalette.TileMap = paletteTileMap;
                }
                _tilePalette.SelectedIndex = 0;
            }
            
            // Update canvas
            if (_canvas != null && _tileMap != null)
            {
                var tilesLibraryPath = _currentTileLibraryPath;
                if (tilesLibraryPath != null && _currentFilePath != null)
                {
                    var tileGraphics = GameCore.Rendering.TileMapLoader.GetTileGraphics(_currentFilePath, tilesLibraryPath);
                    if (tileGraphics != null && tileGraphics.Count > 0)
                    {
                        _tileMap.TileGraphics = tileGraphics;
                    }
                }
                _canvas.TileMap = _tileMap;
                _canvas.Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets the default map file path that the game will load.
    /// </summary>
    private string? GetDefaultMapPath()
    {
        try
        {
            var mapsDir = PathHelper.GetMapsDirectory();
            if (mapsDir == null)
                return null;
            
            // Use shared method to get preferred map file (ensures consistency with game)
            var mapFiles = Directory.GetFiles(mapsDir, "*.json");
            if (mapFiles.Length > 0)
            {
                var preferredFile = GameCore.GameConstants.DefaultFiles.GetPreferredMapFile(mapFiles);
                if (preferredFile != null)
                    return preferredFile;
            }
            
            // Return path to world map file (will be created if doesn't exist)
            return Path.Combine(mapsDir, GameCore.GameConstants.DefaultFiles.WorldMap);
        }
        catch
        {
            return null;
        }
    }

    private void SaveMap()
    {
        // If no file path is set, default to world.json (same file the game loads)
        if (_currentFilePath == null)
        {
            var defaultMapPath = GetDefaultMapPath();
            if (defaultMapPath != null)
            {
                _currentFilePath = defaultMapPath;
                Text = $"Tile Map Editor - {Path.GetFileName(_currentFilePath)}";
                if (_canvas != null)
                {
                    _canvas.MapFileName = Path.GetFileName(_currentFilePath);
                }
            }
            else
            {
                SaveMapAs();
                return;
            }
        }
        
        SaveToFile(_currentFilePath);
    }

    private void SaveMapAs()
    {
        var mapsDir = PathHelper.GetMapsDirectory();
        if (mapsDir == null)
        {
            mapsDir = Path.Combine(Application.StartupPath, "..", "GameContent", "maps");
            mapsDir = Path.GetFullPath(mapsDir);
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = mapsDir,
            FileName = "map.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveToFile(_currentFilePath);
            Text = $"Tile Map Editor - {Path.GetFileName(_currentFilePath)}";
            if (_canvas != null)
            {
                _canvas.MapFileName = Path.GetFileName(_currentFilePath);
            }
        }
    }

    private void SaveToFile(string filePath)
    {
        if (_tileMap == null)
            return;

        try
        {
            TileMapSerializer.SaveToJson(_tileMap, filePath);
            
            // Also save tile library when saving map (ensures tiles.json is up to date)
            SaveTileLibrary();
            
            MessageBox.Show("Map saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResizeMap()
    {
        if (_tileMap == null || _widthTextBox == null || _heightTextBox == null)
            return;

        if (_widthTextBox != null && _heightTextBox != null &&
            int.TryParse(_widthTextBox.Text, out int width) && 
            int.TryParse(_heightTextBox.Text, out int height) &&
            width > 0 && height > 0)
        {
            var oldWidth = _tileMap.Width;
            var oldHeight = _tileMap.Height;

            _tileMap.Width = width;
            _tileMap.Height = height;

            // Reinitialize tiles, preserving existing ones where possible
            var oldTiles = new List<List<int>>();
            for (int y = 0; y < oldHeight; y++)
            {
                var row = new List<int>();
                for (int x = 0; x < oldWidth; x++)
                {
                    row.Add(_tileMap.GetTile(x, y));
                }
                oldTiles.Add(row);
            }

            _tileMap.InitializeTiles();

            // Restore old tiles
            for (int y = 0; y < Math.Min(oldHeight, height); y++)
            {
                for (int x = 0; x < Math.Min(oldWidth, width); x++)
                {
                    _tileMap.SetTile(x, y, oldTiles[y][x]);
                }
            }

            if (_canvas != null)
            {
                _canvas.TileMap = _tileMap;
            }
        }
        else
        {
            MessageBox.Show("Invalid width or height values.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditTile(int tileIndex)
    {
        if (_tileMap == null)
            return;

        // Check if tile is solid
        // Get default tile color
        var defaultColor = GetDefaultTileColor(tileIndex);
        
        // Get graphic path from tile library if exists
        string? graphicPath = null;
        if (_tileLibrary?.TileGraphics != null && _tileLibrary.TileGraphics.ContainsKey(tileIndex))
        {
            graphicPath = _tileLibrary.TileGraphics[tileIndex];
        }
        
        // Create and show edit dialog
        using var dialog = new TileEditDialog(tileIndex, $"Tile {tileIndex}", defaultColor, graphicPath, _tileMap.TileWidth, _tileMap.TileHeight);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            // Update tile graphics in library if a graphic path was set
            if (_tileLibrary == null)
            {
                _tileLibrary = new TileLibraryData();
            }
            
            if (!string.IsNullOrEmpty(dialog.GraphicPath))
            {
                if (_tileLibrary.TileGraphics == null)
                {
                    _tileLibrary.TileGraphics = new Dictionary<int, string>();
                }
                _tileLibrary.TileGraphics[tileIndex] = dialog.GraphicPath;
            }
            else if (_tileLibrary.TileGraphics != null && _tileLibrary.TileGraphics.ContainsKey(tileIndex))
            {
                // Remove graphic if it was cleared
                _tileLibrary.TileGraphics.Remove(tileIndex);
            }
            
            // Save tile library
            SaveTileLibrary();
            
            // Refresh canvas to show updated tile graphics
            if (_canvas != null)
            {
                // Create a temporary TileMapData with graphics from library
                var canvasTileMap = new TileMapData
                {
                    Width = _tileMap.Width,
                    Height = _tileMap.Height,
                    TileWidth = _tileMap.TileWidth,
                    TileHeight = _tileMap.TileHeight,
                    Tiles = _tileMap.Tiles,
                    PlayerStartPosition = _tileMap.PlayerStartPosition,
                    TileGraphics = _tileLibrary.TileGraphics
                };
                _canvas.TileMap = canvasTileMap;
                _canvas.Invalidate();
            }
            
            // Refresh tile palette to show updated tile graphics
            if (_tilePalette != null && _tileMap != null)
            {
                var paletteTileMap = new TileMapData
                {
                    Width = _tileMap.Width,
                    Height = _tileMap.Height,
                    TileWidth = _tileMap.TileWidth,
                    TileHeight = _tileMap.TileHeight,
                    TileGraphics = _tileLibrary.TileGraphics
                };
                _tilePalette.TileMap = paletteTileMap;
            }
            
            // Update properties panel
            UpdateTileProperties();
        }
    }

    private void ResetLayout()
    {
        if (_dockablePalette == null || _dockableProperties == null || 
            _leftSplitContainer == null || _contentSplitContainer == null)
            return;

        try
        {
            // Close any floating forms
            if (_dockablePalette.IsFloating)
            {
                _dockablePalette.DockPanelBack();
            }
            if (_dockableProperties.IsFloating)
            {
                _dockableProperties.DockPanelBack();
            }

            // Remove panels from current parents (may be in split containers or other parents)
            var paletteParent = _dockablePalette.Parent;
            var propertiesParent = _dockableProperties.Parent;
            
            // Remove from any split containers or other parents
            while (paletteParent != null && paletteParent != _leftSplitContainer.Panel1)
            {
                if (paletteParent is SplitContainer split)
                {
                    split.Panel1.Controls.Remove(_dockablePalette);
                    split.Panel2.Controls.Remove(_dockablePalette);
                }
                paletteParent?.Controls.Remove(_dockablePalette);
                paletteParent = _dockablePalette.Parent;
            }
            
            while (propertiesParent != null && propertiesParent != _leftSplitContainer.Panel2)
            {
                if (propertiesParent is SplitContainer split)
                {
                    split.Panel1.Controls.Remove(_dockableProperties);
                    split.Panel2.Controls.Remove(_dockableProperties);
                }
                propertiesParent?.Controls.Remove(_dockableProperties);
                propertiesParent = _dockableProperties.Parent;
            }

            // Clear and rebuild the left split container
            _leftSplitContainer.Panel1.Controls.Clear();
            _leftSplitContainer.Panel2.Controls.Clear();

            // Reset panel dock styles
            _dockablePalette.Dock = DockStyle.Fill;
            _dockableProperties.Dock = DockStyle.Fill;

            // Add panels back to their default positions
            _leftSplitContainer.Panel1.Controls.Add(_dockablePalette);
            _leftSplitContainer.Panel2.Controls.Add(_dockableProperties);

            // Reset splitter distances
            if (_leftSplitContainer.Width > 0 && _leftSplitContainer.Height > 0)
            {
                _leftSplitContainer.SplitterDistance = _leftSplitContainer.Height - 250; // Properties panel ~250px (larger for better readability)
            }
            if (_contentSplitContainer.Width > 0)
            {
                _contentSplitContainer.SplitterDistance = 150; // Left panel ~150px
            }

            // Refresh the layout
            _leftSplitContainer.Invalidate();
            _contentSplitContainer.Invalidate();
            Invalidate();
            
            MessageBox.Show("Layout has been reset to default positions.", "Reset Layout", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error resetting layout: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateTileProperties()
    {
        if (_tilePalette == null || _tileMap == null || _tilePropertiesPanel == null)
            return;
            
        var tileIndex = _tilePalette.SelectedIndex;
        if (tileIndex < 0)
            return;
        
        // Update name
        if (_tileNameLabel != null)
        {
            _tileNameLabel.Text = $"Name: Tile {tileIndex}";
        }
        
        // Update description (not stored, so show default)
        if (_tileDescriptionLabel != null)
        {
            _tileDescriptionLabel.Text = "Description: (Edit to add description)";
        }
        
        // Update color preview
        if (_tileColorPreview != null)
        {
            _tileColorPreview.BackColor = GetDefaultTileColor(tileIndex);
        }
        
        // Update graphic path
        if (_tileGraphicPathLabel != null)
        {
            string? graphicPath = null;
            if (_tileLibrary?.TileGraphics != null && _tileLibrary.TileGraphics.TryGetValue(tileIndex, out var path))
            {
                graphicPath = path;
            }
            
            if (!string.IsNullOrEmpty(graphicPath))
            {
                var fileName = Path.GetFileName(graphicPath);
                _tileGraphicPathLabel.Text = $"Graphic: {fileName}";
                _tileGraphicPathLabel.ForeColor = Color.Blue;
            }
            else
            {
                _tileGraphicPathLabel.Text = "Graphic: None";
                _tileGraphicPathLabel.ForeColor = Color.Black;
            }
        }
        
        // Update AI prompt based on selected tile
        if (_aiPromptTextBox != null)
        {
            var defaultPrompt = FreeImageGenerationService.GetTilePrompt(tileIndex);
            _aiPromptTextBox.Text = defaultPrompt;
        }
        
        if (_aiPromptTemplateComboBox != null)
        {
            _aiPromptTemplateComboBox.SelectedIndex = 0; // Reset to "Custom Prompt"
        }
    }

    private Color GetDefaultTileColor(int index)
    {
        // Match the colors from TileImageGenerator
        return index switch
        {
            0 => Color.FromArgb(100, 150, 100), // Grass (light green)
            1 => Color.FromArgb(80, 120, 80),   // Dark grass
            2 => Color.FromArgb(150, 120, 100), // Dirt (brown)
            3 => Color.FromArgb(120, 100, 80), // Dark dirt
            4 => Color.FromArgb(100, 100, 150), // Water (blue)
            5 => Color.FromArgb(80, 80, 120),   // Deep water
            6 => Color.FromArgb(120, 120, 120), // Stone (gray)
            7 => Color.FromArgb(100, 100, 100), // Dark stone
            _ => Color.Gray
        };
    }

    private void ShowApiKeyConfigurationDialog()
    {
        var dialog = new ApiKeyConfigurationDialog();
        dialog.ShowDialog();
        
        // Refresh services after configuration
        var apiKey = EditorSettings.GetOpenAIApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _aiService = new OpenAIImageService { ApiKey = apiKey };
        }
        
        var replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
        var deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
        if (!string.IsNullOrEmpty(replicateKey) && _freeImageService != null)
        {
            _freeImageService.ApiKey = replicateKey;
        }
        else if (!string.IsNullOrEmpty(deepAIKey) && _freeImageService != null)
        {
            _freeImageService.ApiKey = deepAIKey;
        }
    }

    private (int width, int height) GetSelectedImageSize()
    {
        if (_aiImageSizeComboBox == null || _tileMap == null)
            return (_tileMap?.TileWidth ?? 64, _tileMap?.TileHeight ?? 32);
        
        var selectedText = _aiImageSizeComboBox.SelectedItem?.ToString() ?? "";
        
        // Parse size from selected text
        if (selectedText.Contains("Current Tile Size"))
        {
            return (_tileMap.TileWidth, _tileMap.TileHeight);
        }
        else if (selectedText.Contains("64x32"))
        {
            return (64, 32);
        }
        else if (selectedText.Contains("128x64"))
        {
            return (128, 64);
        }
        else if (selectedText.Contains("256x128"))
        {
            return (256, 128);
        }
        else if (selectedText.Contains("512x256"))
        {
            return (512, 256);
        }
        else if (selectedText.Contains("1024x512"))
        {
            return (1024, 512);
        }
        
        return (_tileMap.TileWidth, _tileMap.TileHeight);
    }

    private async Task GenerateAIImageFromProperties()
    {
        if (_aiPromptTextBox == null || _aiProviderComboBox == null || _tilePalette == null || _tileMap == null)
            return;

        var tileIndex = _tilePalette.SelectedIndex;
        if (tileIndex < 0)
        {
            MessageBox.Show("Please select a tile first.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prompt = _aiPromptTextBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt for image generation.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Get selected size
        var (width, height) = GetSelectedImageSize();

        // Determine which service to use
        bool useOpenAI = _aiProviderComboBox.SelectedIndex == 4 && _aiService != null && _aiService.IsConfigured;
        bool useStableDiffusion = _aiProviderComboBox.SelectedIndex == 1;
        bool useOpenJourney = _aiProviderComboBox.SelectedIndex == 2;
        bool useDeepAI = _aiProviderComboBox.SelectedIndex == 3;
        bool usePollinations = _aiProviderComboBox.SelectedIndex == 0;

        // Handle OpenAI
        if (useOpenAI && _aiService != null)
        {
            try
            {
                var gameContentPath = PathHelper.GetGameContentPath();
                if (gameContentPath == null)
                {
                    MessageBox.Show("GameContent directory not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var tilesDir = Path.Combine(gameContentPath, "tiles");
                if (!Directory.Exists(tilesDir))
                {
                    Directory.CreateDirectory(tilesDir);
                }

                var fileName = $"tile_{tileIndex}_dalle_{DateTime.Now:yyyyMMddHHmmss}.png";
                var filePath = Path.Combine(tilesDir, fileName);

                var progressForm = new Form
                {
                    Text = "Generating Image...",
                    Size = new Size(350, 120),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                var progressLabel = new Label
                {
                    Text = $"Generating with OpenAI DALL·E...\nSize: {width}x{height}\nThis may take a minute...",
                    Location = new Point(20, 20),
                    AutoSize = true
                };
                progressForm.Controls.Add(progressLabel);
                progressForm.Show();
                progressForm.Refresh();

                var success = await _aiService.GenerateImageToFileAsync(prompt, filePath, $"{width}x{height}", width, height);

                progressForm.Close();

                if (success)
                {
                    var relativePath = Path.Combine("tiles", fileName);
                    if (_tileLibrary != null && _tileLibrary.TileGraphics == null)
                    {
                        _tileLibrary.TileGraphics = new Dictionary<int, string>();
                    }
                    if (_tileLibrary?.TileGraphics != null)
                        _tileLibrary.TileGraphics[tileIndex] = relativePath.Replace('\\', '/');
                    SaveTileLibrary();
                    UpdateTileProperties();
                    
                    var result = MessageBox.Show(
                        $"Image generated successfully using OpenAI DALL·E!\n\nSize: {width}x{height}\nSaved to: {filePath}\n\nReload the tile map to see the new graphic?",
                        "AI Generation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    
                    if (result == DialogResult.Yes)
                    {
                        LoadTileMap(_currentFilePath);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to generate image. Please check your API key and try again.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating image: {ex.Message}", "AI Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        // Handle free services
        if (_freeImageService == null)
            return;

        // Check API keys
        if (useDeepAI && string.IsNullOrEmpty(_freeImageService.ApiKey))
        {
            MessageBox.Show("DeepAI API key is required. Please configure it in the API Keys dialog.", "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        else if ((useStableDiffusion || useOpenJourney) && string.IsNullOrEmpty(_freeImageService.ApiKey))
        {
            MessageBox.Show("Replicate API key is required. Please configure it in the API Keys dialog.", "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
            {
                MessageBox.Show("GameContent directory not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var tilesDir = Path.Combine(gameContentPath, "tiles");
            if (!Directory.Exists(tilesDir))
            {
                Directory.CreateDirectory(tilesDir);
            }

            string providerName;
            string providerText;
            if (useStableDiffusion)
            {
                providerName = "sd";
                providerText = "Stable Diffusion (Replicate)";
            }
            else if (useOpenJourney)
            {
                providerName = "oj";
                providerText = "OpenJourney (Replicate)";
            }
            else if (useDeepAI)
            {
                providerName = "deepai";
                providerText = "DeepAI";
            }
            else
            {
                providerName = "free";
                providerText = "Pollinations.ai (Free)";
            }

            var fileName = $"tile_{tileIndex}_{providerName}_{DateTime.Now:yyyyMMddHHmmss}.png";
            var filePath = Path.Combine(tilesDir, fileName);

            var progressForm = new Form
            {
                Text = "Generating Image...",
                Size = new Size(350, 120),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var progressLabel = new Label
            {
                Text = $"Generating with {providerText}...\nSize: {width}x{height}\nThis may take a minute...",
                Location = new Point(20, 20),
                AutoSize = true
            };
            progressForm.Controls.Add(progressLabel);
            progressForm.Show();
            progressForm.Refresh();

            var success = await _freeImageService.GenerateImageToFileAsync(prompt, filePath, width, height);

            progressForm.Close();

            if (success)
            {
                var relativePath = Path.Combine("tiles", fileName);
                if (_tileLibrary != null && _tileLibrary.TileGraphics == null)
                {
                    _tileLibrary.TileGraphics = new Dictionary<int, string>();
                }
                if (_tileLibrary?.TileGraphics != null)
                    _tileLibrary.TileGraphics[tileIndex] = relativePath.Replace('\\', '/');
                SaveTileLibrary();
                UpdateTileProperties();
                
                var result = MessageBox.Show(
                    $"Image generated successfully using {providerText}!\n\nSize: {width}x{height}\nSaved to: {filePath}\n\nReload the tile map to see the new graphic?",
                    "AI Generation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                
                if (result == DialogResult.Yes)
                {
                    LoadTileMap(_currentFilePath);
                }
            }
            else
            {
                MessageBox.Show("Failed to generate image. Please try again.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating image: {ex.Message}", "AI Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetPlayerStartMode()
    {
        if (_canvas != null)
        {
            _canvas.IsSettingPlayerStart = true;
            MessageBox.Show("Click on the map to set the player start position.", "Set Player Start", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CenterOnPlayerStart()
    {
        if (_canvas != null)
        {
            _canvas.CenterOnPlayerStart();
        }
    }

    private void ToggleCollisionEditMode()
    {
        if (_canvas != null)
        {
            _canvas.IsEditingCollision = !_canvas.IsEditingCollision;
            
            // Update the checkbox to reflect collision visibility
            if (_showCollisionCheckBox != null)
            {
                _showCollisionCheckBox.Checked = _canvas.ShowCollision;
            }
            
            if (_canvas.IsEditingCollision)
            {
                MessageBox.Show(
                    "Collision editing mode enabled.\n\n" +
                    "Left-click to toggle collision cells.\n" +
                    "Right-click to exit collision editing mode.",
                    "Collision Editing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }

    private void EditMapProperties()
    {
        if (_tileMap == null)
            return;

        // Create properties dialog
        var dialog = new Form
        {
            Text = "Map Properties",
            Size = new Size(300, 250),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var widthLabel = new Label { Text = "Width:", Location = new Point(20, 20), AutoSize = true };
        var widthTextBox = new TextBox { Text = _tileMap.Width.ToString(), Location = new Point(100, 18), Width = 150 };

        var heightLabel = new Label { Text = "Height:", Location = new Point(20, 50), AutoSize = true };
        var heightTextBox = new TextBox { Text = _tileMap.Height.ToString(), Location = new Point(100, 48), Width = 150 };

        var tileSizeLabel = new Label { Text = "Tile Size:", Location = new Point(20, 80), AutoSize = true };
        var tileSizeComboBox = new ComboBox 
        { 
            Location = new Point(100, 78), 
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        tileSizeComboBox.Items.Add("64x32 (Standard)");
        tileSizeComboBox.Items.Add("128x64 (High Res)");
        tileSizeComboBox.Items.Add("256x128 (Extra High Res)");
        tileSizeComboBox.Items.Add("512x256 (Ultra High Res)");
        tileSizeComboBox.Items.Add("1024x512 (Maximum Res)");
        
        // Select current tile size
        if (_tileMap.TileWidth == 128 && _tileMap.TileHeight == 64)
            tileSizeComboBox.SelectedIndex = 1;
        else if (_tileMap.TileWidth == 256 && _tileMap.TileHeight == 128)
            tileSizeComboBox.SelectedIndex = 2;
        else if (_tileMap.TileWidth == 512 && _tileMap.TileHeight == 256)
            tileSizeComboBox.SelectedIndex = 3;
        else if (_tileMap.TileWidth == 1024 && _tileMap.TileHeight == 512)
            tileSizeComboBox.SelectedIndex = 4;
        else
            tileSizeComboBox.SelectedIndex = 0;

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(120, 160) };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(200, 160) };

        okButton.Click += (s, e) =>
        {
            if (int.TryParse(widthTextBox.Text, out int w) && int.TryParse(heightTextBox.Text, out int h) && w > 0 && h > 0)
            {
                // Update map dimensions if changed
                if (w != _tileMap.Width || h != _tileMap.Height)
                {
                    var oldWidth = _tileMap.Width;
                    var oldHeight = _tileMap.Height;

                    // Save existing tiles
                    var oldTiles = new List<List<int>>();
                    for (int y = 0; y < oldHeight; y++)
                    {
                        var row = new List<int>();
                        for (int x = 0; x < oldWidth; x++)
                        {
                            row.Add(_tileMap.GetTile(x, y));
                        }
                        oldTiles.Add(row);
                    }

                    _tileMap.Width = w;
                    _tileMap.Height = h;
                    _tileMap.InitializeTiles();

                    // Restore old tiles
                    for (int y = 0; y < Math.Min(oldHeight, h); y++)
                    {
                        for (int x = 0; x < Math.Min(oldWidth, w); x++)
                        {
                            _tileMap.SetTile(x, y, oldTiles[y][x]);
                        }
                    }
                }

                // Update tile size if changed
                int newTileWidth;
                int newTileHeight;
                if (tileSizeComboBox.SelectedIndex == 1)
                {
                    newTileWidth = 128;
                    newTileHeight = 64;
                }
                else if (tileSizeComboBox.SelectedIndex == 2)
                {
                    newTileWidth = 256;
                    newTileHeight = 128;
                }
                else if (tileSizeComboBox.SelectedIndex == 3)
                {
                    newTileWidth = 512;
                    newTileHeight = 256;
                }
                else if (tileSizeComboBox.SelectedIndex == 4)
                {
                    newTileWidth = 1024;
                    newTileHeight = 512;
                }
                else
                {
                    newTileWidth = 64;
                    newTileHeight = 32;
                }
                
                if (newTileWidth != _tileMap.TileWidth || newTileHeight != _tileMap.TileHeight)
                {
                    _tileMap.TileWidth = newTileWidth;
                    _tileMap.TileHeight = newTileHeight;
                    
                    // Update tile palette
                    if (_tilePalette != null)
                    {
                        _tilePalette.TileWidth = newTileWidth;
                        _tilePalette.TileHeight = newTileHeight;
                        // Calculate preview size, but cap it to fit in the browser (max 64px wide)
                        var widthScale = (float)newTileWidth / 64f;
                        var heightScale = (float)newTileHeight / 32f;
                        var previewWidth = (int)(48 * widthScale);
                        var previewHeight = (int)(24 * heightScale);
                        
                        // Cap at maximum size to fit in browser (maintain aspect ratio)
                        const int maxPreviewWidth = 64;
                        const int maxPreviewHeight = 32;
                        if (previewWidth > maxPreviewWidth || previewHeight > maxPreviewHeight)
                        {
                            var scaleFactor = Math.Min((float)maxPreviewWidth / previewWidth, (float)maxPreviewHeight / previewHeight);
                            previewWidth = (int)(previewWidth * scaleFactor);
                            previewHeight = (int)(previewHeight * scaleFactor);
                        }
                        
                        _tilePalette.TilePreviewSize = new Size(previewWidth, previewHeight);
                    }
                    
                    // Refresh canvas
                    if (_canvas != null)
                    {
                        _canvas.Invalidate();
                    }
                }

                // Update toolbar textboxes
                if (_widthTextBox != null)
                    _widthTextBox.TextBox.Text = w.ToString();
                if (_heightTextBox != null)
                    _heightTextBox.TextBox.Text = h.ToString();
            }
            else
            {
                MessageBox.Show("Invalid width or height.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dialog.DialogResult = DialogResult.None;
            }
        };

        dialog.Controls.Add(widthLabel);
        dialog.Controls.Add(widthTextBox);
        dialog.Controls.Add(heightLabel);
        dialog.Controls.Add(heightTextBox);
        dialog.Controls.Add(tileSizeLabel);
        dialog.Controls.Add(tileSizeComboBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);

        dialog.ShowDialog();
    }

    private void ExportTiles()
    {
        if (_tileMap == null)
        {
            MessageBox.Show("No map loaded. Please load or create a map first.", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var success = TileExporter.ExportTilesToPng(_tileMap.TileWidth, _tileMap.TileHeight, 8);
            if (success)
            {
                MessageBox.Show($"Tiles exported successfully to GameContent/tiles directory!", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to export tiles. Check the output for error messages.", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting tiles: {ex.Message}", "Export Tiles", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadEntityLayout()
    {
        // Try to load entities from GameContent/entities directory
        var entitiesDir = PathHelper.GetEntitiesDirectory();
        if (entitiesDir != null && Directory.Exists(entitiesDir))
        {
            var entityFiles = Directory.GetFiles(entitiesDir, "*.json");
            if (entityFiles.Length > 0)
            {
                // Prefer "world_entities.json" or "entities.json"
                var preferredFile = Array.Find(entityFiles, f => 
                    Path.GetFileName(f).Equals("world_entities.json", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(f).Equals("entities.json", StringComparison.OrdinalIgnoreCase));
                
                var entityFile = preferredFile ?? entityFiles[0];
                
                // Try loading with editor's serializer
                var loaded = EntitySerializer.LoadFromJson(entityFile);
                if (loaded != null && _canvas != null)
                {
                    _canvas.EntityLayout = loaded;
                }
            }
        }
    }

    private void LoadLightLayout()
    {
        // Try to load lights from world_lights.json file
        var lightsPath = PathHelper.GetWorldLightsPath();
        if (lightsPath != null && File.Exists(lightsPath))
        {
            var loaded = LightSerializer.LoadFromJson(lightsPath);
            if (loaded != null && _canvas != null)
            {
                _canvas.LightLayout = loaded;
            }
        }
    }
}

/// <summary>
/// Dialog for creating a new map.
/// </summary>
public class NewMapDialog : Form
{
    private TextBox? _widthTextBox;
    private TextBox? _heightTextBox;
    private ComboBox? _tileSizeComboBox;

    public new int Width { get; private set; } = 20;
    public new int Height { get; private set; } = 20;
    public int TileWidth { get; private set; } = 64;
    public int TileHeight { get; private set; } = 32;

    public NewMapDialog()
    {
        Text = "New Map";
        Size = new Size(300, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var widthLabel = new Label { Text = "Width:", Location = new Point(20, 20), AutoSize = true };
        _widthTextBox = new TextBox { Text = "20", Location = new Point(80, 18), Width = 100 };

        var heightLabel = new Label { Text = "Height:", Location = new Point(20, 50), AutoSize = true };
        _heightTextBox = new TextBox { Text = "20", Location = new Point(80, 48), Width = 100 };

        var tileSizeLabel = new Label { Text = "Tile Size:", Location = new Point(20, 80), AutoSize = true };
        _tileSizeComboBox = new ComboBox 
        { 
            Location = new Point(80, 78), 
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _tileSizeComboBox.Items.Add("64x32 (Standard)");
        _tileSizeComboBox.Items.Add("128x64 (High Res)");
        _tileSizeComboBox.Items.Add("256x128 (Extra High Res)");
        _tileSizeComboBox.Items.Add("512x256 (Ultra High Res)");
        _tileSizeComboBox.Items.Add("1024x512 (Maximum Res)");
        _tileSizeComboBox.SelectedIndex = 0;

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(120, 120) };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(200, 120) };

        okButton.Click += (s, e) =>
        {
            if (int.TryParse(_widthTextBox?.Text, out int w) && int.TryParse(_heightTextBox?.Text, out int h) && w > 0 && h > 0)
            {
                Width = w;
                Height = h;
                
                // Set tile size based on selection
                if (_tileSizeComboBox?.SelectedIndex == 1)
                {
                    TileWidth = 128;
                    TileHeight = 64;
                }
                else if (_tileSizeComboBox?.SelectedIndex == 2)
                {
                    TileWidth = 256;
                    TileHeight = 128;
                }
                else if (_tileSizeComboBox?.SelectedIndex == 3)
                {
                    TileWidth = 512;
                    TileHeight = 256;
                }
                else if (_tileSizeComboBox?.SelectedIndex == 4)
                {
                    TileWidth = 1024;
                    TileHeight = 512;
                }
                else
                {
                    TileWidth = 64;
                    TileHeight = 32;
                }
            }
            else
            {
                MessageBox.Show("Invalid width or height.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        };

        Controls.Add(widthLabel);
        Controls.Add(_widthTextBox);
        Controls.Add(heightLabel);
        Controls.Add(_heightTextBox);
        Controls.Add(tileSizeLabel);
        Controls.Add(_tileSizeComboBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}

