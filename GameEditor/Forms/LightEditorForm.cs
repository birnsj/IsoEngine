using GameEditor.Controls;
using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;
using System.IO;
using GameCore.Rendering;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing light placements on maps.
/// </summary>
public partial class LightEditorForm : Form
{
    private LightPlacementCanvas? _canvas;
    private LightLayoutData? _lightLayout;
    private Panel? _toolPanel;
    private ListBox? _lightList;
    private PropertyGrid? _propertyGrid;
    private string? _currentFilePath;
    private LightData? _selectedLight;
    private FileSystemWatcher? _tilesLibraryFileWatcher;
    private FileSystemWatcher? _mapFileWatcher;
    private volatile bool _reloadTilesPending = false;
    private System.Windows.Forms.Timer? _reloadDebounceTimer;

    /// <summary>
    /// Gets the current file path for this editor.
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;

    public LightEditorForm(string? filePath = null)
    {
        InitializeComponent();
        
        PathHelper.ValidateGameContentPath();
        
        LoadTileMap();
        SetupFileWatchers();
        if (filePath != null && File.Exists(filePath))
        {
            LoadLightLayout(filePath);
        }
        else
        {
            LoadLightLayout(null);
            AutoLoadWorldLights();
        }
    }
    
    private void SetupFileWatchers()
    {
        var tilesLibraryPath = PathHelper.GetTilesLibraryPath();
        if (tilesLibraryPath != null && File.Exists(tilesLibraryPath))
        {
            var tilesDir = Path.GetDirectoryName(tilesLibraryPath);
            var tilesFileName = Path.GetFileName(tilesLibraryPath);
            
            if (tilesDir != null)
            {
                _tilesLibraryFileWatcher = new FileSystemWatcher
                {
                    Path = tilesDir,
                    Filter = tilesFileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                
                _tilesLibraryFileWatcher.Changed += (s, e) => OnTilesLibraryFileChanged();
            }
        }
        
        var worldMapPath = PathHelper.GetWorldMapPath();
        if (worldMapPath != null && File.Exists(worldMapPath))
        {
            var mapsDir = Path.GetDirectoryName(worldMapPath);
            var mapFileName = Path.GetFileName(worldMapPath);
            
            if (mapsDir != null)
            {
                _mapFileWatcher = new FileSystemWatcher
                {
                    Path = mapsDir,
                    Filter = mapFileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                
                _mapFileWatcher.Changed += (s, e) => OnMapFileChanged();
            }
        }
        
        _reloadDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = 500,
            Enabled = false
        };
        _reloadDebounceTimer.Tick += (s, e) =>
        {
            _reloadDebounceTimer.Stop();
            if (_reloadTilesPending)
            {
                _reloadTilesPending = false;
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => LoadTileMap()));
                }
            }
        };
    }
    
    private void OnTilesLibraryFileChanged()
    {
        _reloadTilesPending = true;
        _reloadDebounceTimer?.Start();
    }
    
    private void OnMapFileChanged()
    {
        _reloadTilesPending = true;
        _reloadDebounceTimer?.Start();
    }

    private void AutoLoadWorldLights()
    {
        var lightFile = PathHelper.GetWorldLightsPath();
        if (lightFile != null && File.Exists(lightFile))
        {
            var loaded = LightSerializer.LoadFromJson(lightFile);
            if (loaded != null && loaded.Lights.Count > 0)
            {
                _lightLayout = loaded;
                _currentFilePath = lightFile;
                Text = $"Light Editor - {Path.GetFileName(lightFile)}";
                
                if (_canvas != null)
                {
                    _canvas.LightLayout = _lightLayout;
                }
                
                RefreshLightList();
                return;
            }
        }
        
        // If file doesn't exist or is empty, create empty layout
        _lightLayout = new LightLayoutData();
        if (_canvas != null)
        {
            _canvas.LightLayout = _lightLayout;
        }
        RefreshLightList();
    }

    private void InitializeComponent()
    {
        Text = "Light Editor";
        MinimumSize = new Size(600, 400);

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        // File operations
        var newButton = new ToolStripButton("New", null, (s, e) => NewLayout());
        var openButton = new ToolStripButton("Open", null, (s, e) => OpenLayout());
        var saveButton = new ToolStripButton("Save", null, (s, e) => SaveLayout());
        var saveAsButton = new ToolStripButton("Save As", null, (s, e) => SaveLayoutAs());
        toolStrip.Items.Add(newButton);
        toolStrip.Items.Add(openButton);
        toolStrip.Items.Add(saveButton);
        toolStrip.Items.Add(saveAsButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        // Light operations
        var addLightButton = new ToolStripButton("Add Light", null, (s, e) => AddLight());
        var removeLightButton = new ToolStripButton("Remove Light", null, (s, e) => RemoveSelectedLight());
        var centerLightsButton = new ToolStripButton("Center on Lights", null, (s, e) => CenterOnLights());
        toolStrip.Items.Add(addLightButton);
        toolStrip.Items.Add(removeLightButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(centerLightsButton);

        // Second toolbar for grid controls
        var toolStrip2 = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        var snapToGridCheckBox = new CheckBox 
        { 
            Text = "Snap to Grid", 
            Checked = true, 
            AutoSize = true, 
            Padding = new Padding(5, 0, 5, 0) 
        };
        snapToGridCheckBox.CheckedChanged += (s, e) =>
        {
            if (_canvas != null)
                _canvas.SnapToGrid = snapToGridCheckBox.Checked;
        };
        var checkBoxContainer = new ToolStripControlHost(snapToGridCheckBox);
        toolStrip2.Items.Add(checkBoxContainer);

        _toolPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = toolStrip.Height + toolStrip2.Height
        };
        _toolPanel.Controls.Add(toolStrip2);
        _toolPanel.Controls.Add(toolStrip);

        var mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        mainSplitContainer.Panel1MinSize = 150; // Increased for better visibility
        // Note: Panel2MinSize set in Load event to avoid issues when form is embedded in tabs

        var leftPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        leftPanel.Panel1MinSize = 120; // Increased for better visibility
        leftPanel.Panel2MinSize = 150; // Increased for better visibility

        var lightListPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 5, 5, 5) };
        var lightListLabel = new Label
        {
            Text = "Lights",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _lightList = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _lightList.SelectedIndexChanged += (s, e) => OnLightSelected();
        lightListPanel.Controls.Add(lightListLabel);
        lightListPanel.Controls.Add(_lightList);

        var propertyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 5, 5, 5) };
        var propertyLabel = new Label
        {
            Text = "Properties",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _propertyGrid.PropertyValueChanged += (s, e) =>
        {
            if (_canvas != null)
            {
                _canvas.Invalidate();
            }
            RefreshLightList();
        };
        propertyPanel.Controls.Add(propertyLabel);
        propertyPanel.Controls.Add(_propertyGrid);

        leftPanel.Panel1.Controls.Add(lightListPanel);
        leftPanel.Panel2.Controls.Add(propertyPanel);

        _canvas = new LightPlacementCanvas
        {
            Dock = DockStyle.Fill
        };
        _canvas.LightSelected += (s, e) => SelectLight(e.Light);
        _canvas.LightMoved += (s, e) => RefreshLightList();

        mainSplitContainer.Panel1.Controls.Add(leftPanel);
        mainSplitContainer.Panel2.Controls.Add(_canvas);

        Controls.Add(_toolPanel);
        Controls.Add(mainSplitContainer);
        
        Load += (s, e) =>
        {
            // Set Panel2MinSize after control is sized to avoid crashes when embedded in tabs
            // Use a reasonable minimum that won't exceed available space
            var availableWidth = mainSplitContainer.Width;
            if (availableWidth > 0)
            {
                // Set minimum to 250 or 30% of available width, whichever is smaller
                mainSplitContainer.Panel2MinSize = Math.Min(250, (int)(availableWidth * 0.3));
            }
            else
            {
                mainSplitContainer.Panel2MinSize = 200; // Fallback minimum
            }
            
            if (mainSplitContainer.Width > 0)
            {
                var preferredDistance = 180; // Increased for better visibility
                var maxDistance = mainSplitContainer.Width - mainSplitContainer.Panel2MinSize;
                var minDistance = mainSplitContainer.Panel1MinSize;
                
                if (maxDistance >= minDistance)
                {
                    mainSplitContainer.SplitterDistance = Math.Max(minDistance, Math.Min(preferredDistance, maxDistance));
                }
                else
                {
                    mainSplitContainer.SplitterDistance = minDistance;
                }
            }
            
            if (leftPanel.Width > 0)
            {
                var preferredLeftDistance = leftPanel.Width / 2;
                var maxLeftDistance = leftPanel.Width - leftPanel.Panel2MinSize;
                var minLeftDistance = leftPanel.Panel1MinSize;
                
                if (maxLeftDistance >= minLeftDistance)
                {
                    leftPanel.SplitterDistance = Math.Max(minLeftDistance, Math.Min(preferredLeftDistance, maxLeftDistance));
                }
                else
                {
                    leftPanel.SplitterDistance = minLeftDistance;
                }
            }
        };
    }

    private void NewLayout()
    {
        _lightLayout = new LightLayoutData();
        _currentFilePath = null;
        RefreshLightList();
        if (_canvas != null)
        {
            _canvas.LightLayout = _lightLayout;
        }
        Text = "Light Editor - New Layout";
    }

    private void OpenLayout()
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
            LoadLightLayout(dialog.FileName);
        }
    }

    private void LoadLightLayout(string? filePath)
    {
        if (filePath != null)
        {
            if (File.Exists(filePath))
            {
                _lightLayout = LightSerializer.LoadFromJson(filePath);
                if (_lightLayout != null)
                {
                    _currentFilePath = filePath;
                    Text = $"Light Editor - {Path.GetFileName(filePath)}";
                }
                else
                {
                    MessageBox.Show($"Failed to load light file: {filePath}\n\nThe file may be corrupted or in an invalid format.", 
                        "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _lightLayout = new LightLayoutData();
                }
            }
            else
            {
                MessageBox.Show($"Light file not found: {filePath}", 
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _lightLayout = new LightLayoutData();
            }
        }
        else if (_lightLayout == null)
        {
            _lightLayout = new LightLayoutData();
        }

        if (_canvas != null && _lightLayout != null)
        {
            _canvas.LightLayout = _lightLayout;
        }

        RefreshLightList();
    }

    private void SaveLayout()
    {
        if (_lightLayout == null)
            return;
            
        if (_currentFilePath == null)
        {
            _currentFilePath = PathHelper.GetWorldLightsPath();
        }
        
        if (_currentFilePath != null)
        {
            SaveToFile(_currentFilePath);
        }
        else
        {
            SaveLayoutAs();
        }
    }

    /// <summary>
    /// Public method to save the light layout. Can be called from MainEditorForm's Save All.
    /// </summary>
    public void Save()
    {
        SaveLayout();
    }

    private void SaveLayoutAs()
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
            FileName = "world_lights.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveToFile(_currentFilePath);
            Text = $"Light Editor - {Path.GetFileName(_currentFilePath)}";
        }
    }

    private void SaveToFile(string filePath)
    {
        if (_lightLayout == null)
            return;

        try
        {
            LightSerializer.SaveToJson(_lightLayout, filePath);
            MessageBox.Show("Light layout saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving light layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddLight()
    {
        if (_lightLayout == null)
        {
            _lightLayout = new LightLayoutData();
            if (_canvas != null)
            {
                _canvas.LightLayout = _lightLayout;
            }
        }

        var light = new LightData
        {
            Name = $"Light {_lightLayout.Lights.Count + 1}",
            Position = new PositionData { X = 10, Y = 10 },
            Color = new ColorData { R = 255, G = 255, B = 255, A = 255 },
            Radius = 100.0f,
            Intensity = 1.0f
        };

        _lightLayout.Lights.Add(light);
        RefreshLightList();
        SelectLight(light);
    }

    private void RemoveSelectedLight()
    {
        if (_selectedLight != null && _lightLayout != null)
        {
            _lightLayout.Lights.Remove(_selectedLight);
            _selectedLight = null;
            RefreshLightList();
            if (_propertyGrid != null)
            {
                _propertyGrid.SelectedObject = null;
            }
            if (_canvas != null)
            {
                _canvas.Invalidate();
            }
        }
    }

    private void RefreshLightList()
    {
        if (_lightList == null || _lightLayout == null)
            return;

        _lightList.Items.Clear();
        foreach (var light in _lightLayout.Lights)
        {
            var displayName = string.IsNullOrEmpty(light.Name) ? $"Light {_lightLayout.Lights.IndexOf(light) + 1}" : light.Name;
            _lightList.Items.Add(displayName);
        }
    }

    private void OnLightSelected()
    {
        if (_lightList == null || _lightLayout == null || _lightList.SelectedIndex < 0)
            return;

        var index = _lightList.SelectedIndex;
        if (index >= 0 && index < _lightLayout.Lights.Count)
        {
            SelectLight(_lightLayout.Lights[index]);
        }
    }

    private void SelectLight(LightData light)
    {
        _selectedLight = light;
        if (_propertyGrid != null)
        {
            _propertyGrid.SelectedObject = light;
        }
        if (_canvas != null)
        {
            _canvas.SelectedLight = light;
            _canvas.Invalidate();
        }
    }

    private void CenterOnLights()
    {
        if (_canvas != null)
        {
            _canvas.CenterOnLights();
        }
    }

    private void LoadTileMap()
    {
        var mapsDir = PathHelper.GetMapsDirectory();
        if (mapsDir != null && Directory.Exists(mapsDir))
        {
            var mapFiles = Directory.GetFiles(mapsDir, "*.json");
            if (mapFiles.Length > 0)
            {
                var mapFile = GameCore.GameConstants.DefaultFiles.GetPreferredMapFile(mapFiles);
                if (mapFile == null)
                    return;
                
                var tileMap = TileMapSerializer.LoadFromJson(mapFile);
                if (tileMap != null && _canvas != null)
                {
                    var tilesLibraryPath = PathHelper.GetTilesLibraryPath();
                    if (tilesLibraryPath != null && File.Exists(tilesLibraryPath))
                    {
                        var tileGraphics = GameCore.Rendering.TileMapLoader.GetTileGraphics(mapFile, tilesLibraryPath);
                        if (tileGraphics != null && tileGraphics.Count > 0)
                        {
                            tileMap.TileGraphics = tileGraphics;
                        }
                    }
                    
                    _canvas.TileMap = tileMap;
                    _canvas.Invalidate();
                }
            }
        }
    }
    
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _tilesLibraryFileWatcher?.Dispose();
        _mapFileWatcher?.Dispose();
        _reloadDebounceTimer?.Dispose();
        base.OnFormClosed(e);
    }
}

