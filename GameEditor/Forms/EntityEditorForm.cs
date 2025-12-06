using GameEditor.Controls;
using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;
using System.IO;
using GameCore.Rendering;
using System.Threading;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing entity placements on maps.
/// </summary>
public partial class EntityEditorForm : Form
{
    private EntityPlacementCanvas? _canvas;
    private EntityLayoutData? _entityLayout;
    private Panel? _toolPanel;
    private ListBox? _entityList;
    private PropertyGrid? _propertyGrid;
    private string? _currentFilePath;
    private EntityData? _selectedEntity;
    private FileSystemWatcher? _tilesLibraryFileWatcher;
    private FileSystemWatcher? _mapFileWatcher;
    private volatile bool _reloadTilesPending = false;
    private System.Windows.Forms.Timer? _reloadDebounceTimer;

    /// <summary>
    /// Gets the current file path for this editor.
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;

    public EntityEditorForm(string? filePath = null)
    {
        InitializeComponent();
        
        // Validate GameContent path (ensures consistency with game)
        PathHelper.ValidateGameContentPath();
        
        LoadTileMap(); // Load tile map for background
        SetupFileWatchers(); // Watch for tile changes
        if (filePath != null && File.Exists(filePath))
        {
            LoadEntityLayout(filePath);
        }
        else
        {
            LoadEntityLayout(null);
            AutoLoadWorldEntities();
        }
    }
    
    private void SetupFileWatchers()
    {
        // Watch for changes to tiles.json
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
        
        // Watch for changes to world.json
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
        
        // Setup debounce timer
        _reloadDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = 500, // 500ms debounce
            Enabled = false
        };
        _reloadDebounceTimer.Tick += (s, e) =>
        {
            _reloadDebounceTimer.Stop();
            if (_reloadTilesPending)
            {
                _reloadTilesPending = false;
                // Reload on UI thread
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

    private void AutoLoadWorldEntities()
    {
        // Always use the same file path that tile map editor uses
        var entityFile = PathHelper.GetWorldEntitiesPath();
        if (entityFile != null && File.Exists(entityFile))
        {
            // Try loading with editor's serializer first (works with exported files)
            var loaded = EntitySerializer.LoadFromJson(entityFile);
            if (loaded != null && loaded.Entities.Count > 0)
            {
                _entityLayout = loaded;
                _currentFilePath = entityFile;
                Text = $"Entity Editor - {Path.GetFileName(entityFile)}";
                
                if (_canvas != null)
                {
                    _canvas.EntityLayout = _entityLayout;
                }
                
                RefreshEntityList();
                return;
            }
            
            // Fallback: try GameCore's loader if editor format doesn't work
            LoadEntitiesFromGameCore(entityFile);
        }
    }

    private void LoadEntitiesFromGameCore(string filePath)
    {
        try
        {
            var loadedEntities = GameCore.Entities.EntityLayoutLoader.LoadFromJson(filePath);
            if (loadedEntities.Count == 0)
                return;

            // Convert GameCore entities to editor format
            _entityLayout = new EntityLayoutData();
            foreach (var loadedEntity in loadedEntities)
            {
                var entityData = new EntityData
                {
                    Type = loadedEntity.Type,
                    Name = loadedEntity.Name,
                    Description = loadedEntity.Description,
                    Position = new PositionData
                    {
                        X = loadedEntity.Position.X,
                        Y = loadedEntity.Position.Y,
                        Z = loadedEntity.Z
                    },
                    DialogId = loadedEntity.DialogId,
                    ItemId = loadedEntity.ItemId,
                    Quantity = loadedEntity.Quantity
                };

                if (loadedEntity.Stats != null)
                {
                    entityData.EnemyStats = new EnemyStatsData
                    {
                        MaxHealth = loadedEntity.Stats.MaxHealth,
                        AttackPower = loadedEntity.Stats.AttackPower,
                        Defense = loadedEntity.Stats.Defense
                    };
                }

                if (loadedEntity.Bounds != null)
                {
                    entityData.PlacementBounds = new PlacementBoundsData
                    {
                        Width = loadedEntity.Bounds.Width,
                        Height = loadedEntity.Bounds.Height,
                        ZHeight = loadedEntity.Bounds.ZHeight,
                        OffsetX = loadedEntity.Bounds.OffsetX,
                        OffsetY = loadedEntity.Bounds.OffsetY
                    };
                }

                _entityLayout.Entities.Add(entityData);
            }

            _currentFilePath = filePath;
            Text = $"Entity Editor - {Path.GetFileName(filePath)}";
            
            if (_canvas != null)
            {
                _canvas.EntityLayout = _entityLayout;
            }
            
            RefreshEntityList();
        }
        catch
        {
            // Silently fail - entity file might not exist yet
        }
    }

    private void InitializeComponent()
    {
        Text = "Entity Editor";
        // No fixed size - will fill tab container
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

        // Entity operations
        var addEntityButton = new ToolStripButton("Add Entity", null, (s, e) => AddEntity());
        var removeEntityButton = new ToolStripButton("Remove Entity", null, (s, e) => RemoveSelectedEntity());
        var gotoSelectedButton = new ToolStripButton("Go to Selected", null, (s, e) => GotoSelectedEntity());
        var centerEntitiesButton = new ToolStripButton("Center on Entities", null, (s, e) => CenterOnEntities());
        var exportEntitiesButton = new ToolStripButton("Export Entity Icons to PNG", null, (s, e) => ExportEntities());
        toolStrip.Items.Add(addEntityButton);
        toolStrip.Items.Add(removeEntityButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(gotoSelectedButton);
        toolStrip.Items.Add(centerEntitiesButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(exportEntitiesButton);

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

        var showBoundsCheckBox = new CheckBox 
        { 
            Text = "Show Bounds", 
            Checked = true, 
            AutoSize = true, 
            Padding = new Padding(5, 0, 5, 0) 
        };
        showBoundsCheckBox.CheckedChanged += (s, e) =>
        {
            if (_canvas != null)
                _canvas.ShowBounds = showBoundsCheckBox.Checked;
        };
        var showBoundsContainer = new ToolStripControlHost(showBoundsCheckBox);
        toolStrip2.Items.Add(showBoundsContainer);

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
        // Set minimum sizes - but don't set SplitterDistance until Load event
        // Match tile palette size - minimum should allow icon + some text (icon is 32px + padding)
        mainSplitContainer.Panel1MinSize = 150; // Increased for better visibility
        // Note: Panel2MinSize set in Load event to avoid issues when form is embedded in tabs

        // Left panel: Entity list and properties
        // Match tile browser size - the whole left panel should be ~150px like tile palette
        // So we need smaller minimums for the nested panels
        var leftPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        // Smaller minimums to fit within 150px total - allow entity list to show icon + truncated text
        leftPanel.Panel1MinSize = 120;  // Entity list - icon (32px) + padding + some text (increased)
        // Note: Panel2MinSize set in Load event to avoid crashes when form is embedded in tabs

        var entityListPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 5, 5, 5) };
        var entityListLabel = new Label
        {
            Text = "Entities",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _entityList = new EntityListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _entityList.SelectedIndexChanged += (s, e) => OnEntitySelected();
        
        // Context menu for entity list
        var entityContextMenu = new ContextMenuStrip();
        var editPropertiesItem = new ToolStripMenuItem("Edit Properties...", null, (s, e) => EditSelectedEntityProperties());
        editPropertiesItem.Font = new Font(editPropertiesItem.Font, FontStyle.Bold); // Make default action bold
        var duplicateItem = new ToolStripMenuItem("Duplicate", null, (s, e) => DuplicateSelectedEntity());
        var deleteItem = new ToolStripMenuItem("Delete", null, (s, e) => RemoveSelectedEntity());
        deleteItem.ShortcutKeys = Keys.Delete;
        entityContextMenu.Items.Add(editPropertiesItem);
        entityContextMenu.Items.Add(new ToolStripSeparator());
        entityContextMenu.Items.Add(duplicateItem);
        entityContextMenu.Items.Add(deleteItem);
        
        // Select the item under the cursor when right-clicking
        _entityList.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                var index = _entityList.IndexFromPoint(e.Location);
                if (index >= 0 && index < _entityList.Items.Count)
                {
                    _entityList.SelectedIndex = index;
                }
            }
        };
        
        // Only show context menu if something is selected
        entityContextMenu.Opening += (s, e) =>
        {
            if (_selectedEntity == null)
            {
                e.Cancel = true;
            }
        };
        
        _entityList.ContextMenuStrip = entityContextMenu;
        _entityList.MouseDoubleClick += (s, e) => EditSelectedEntityProperties(); // Double-click to edit
        
        entityListPanel.Controls.Add(entityListLabel);
        entityListPanel.Controls.Add(_entityList);

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
        propertyPanel.Controls.Add(propertyLabel);
        propertyPanel.Controls.Add(_propertyGrid);

        leftPanel.Panel1.Controls.Add(entityListPanel);
        leftPanel.Panel2.Controls.Add(propertyPanel);

        // Right panel: Canvas
        _canvas = new EntityPlacementCanvas
        {
            Dock = DockStyle.Fill,
            ShowBounds = true  // Default to showing bounds
        };
        _canvas.EntitySelected += (s, e) => SelectEntity(e.Entity);
        _canvas.EntityMoved += (s, e) => RefreshEntityList();

        mainSplitContainer.Panel1.Controls.Add(leftPanel);
        mainSplitContainer.Panel2.Controls.Add(_canvas);

        Controls.Add(_toolPanel);
        Controls.Add(mainSplitContainer);
        
        // Ensure splitter distances are valid after form is loaded
        Load += (s, e) =>
        {
            // Set Panel2MinSize after control is sized to avoid crashes when embedded in tabs
            // Use a reasonable minimum that won't exceed available space
            var availableWidth = mainSplitContainer.Width;
            if (availableWidth > 0)
            {
                // Set minimum to 200 or 30% of available width, whichever is smaller
                mainSplitContainer.Panel2MinSize = Math.Min(250, (int)(availableWidth * 0.3));
            }
            else
            {
                mainSplitContainer.Panel2MinSize = 200; // Fallback minimum
            }
            
            if (mainSplitContainer.Width > 0)
            {
                // Match tile palette size - prefer 180px for better visibility
                var preferredDistance = 180;
                var maxDistance = mainSplitContainer.Width - mainSplitContainer.Panel2MinSize;
                var minDistance = mainSplitContainer.Panel1MinSize;
                
                // Ensure we have a valid range
                if (maxDistance >= minDistance)
                {
                    // Set to preferred if it fits, otherwise use a valid value
                    mainSplitContainer.SplitterDistance = Math.Max(minDistance, Math.Min(preferredDistance, maxDistance));
                }
                else
                {
                    // Fallback: use minimum if control is too small
                    mainSplitContainer.SplitterDistance = minDistance;
                }
            }
            
            // Set leftPanel Panel2MinSize after control is sized
            if (leftPanel.Width > 0)
            {
                // Set Panel2MinSize dynamically
                var availableLeftWidth = leftPanel.Width;
                leftPanel.Panel2MinSize = Math.Min(150, (int)(availableLeftWidth * 0.3));
            }
            else
            {
                leftPanel.Panel2MinSize = 120; // Fallback minimum
            }
            
            if (leftPanel.Width > 0)
            {
                // Split 50/50 when total width is 150px to match tile browser size
                // Entity list on left, Properties on right
                var preferredLeftDistance = leftPanel.Width / 2;
                var maxLeftDistance = leftPanel.Width - leftPanel.Panel2MinSize;
                var minLeftDistance = leftPanel.Panel1MinSize;
                
                // Ensure we have a valid range
                if (maxLeftDistance >= minLeftDistance)
                {
                    // Split evenly, but respect minimums
                    leftPanel.SplitterDistance = Math.Max(minLeftDistance, Math.Min(preferredLeftDistance, maxLeftDistance));
                }
                else
                {
                    // Fallback: use minimum if control is too small
                    leftPanel.SplitterDistance = minLeftDistance;
                }
            }
        };
    }

    private void NewLayout()
    {
        _entityLayout = new EntityLayoutData();
        _currentFilePath = null;
        RefreshEntityList();
        if (_canvas != null)
        {
            _canvas.EntityLayout = _entityLayout;
        }
        Text = "Entity Editor - New Layout";
    }

    private void OpenLayout()
    {
        var entitiesDir = PathHelper.GetEntitiesDirectory();
        if (entitiesDir == null)
        {
            entitiesDir = Path.Combine(Application.StartupPath, "..", "GameContent", "entities");
            entitiesDir = Path.GetFullPath(entitiesDir);
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = entitiesDir
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadEntityLayout(dialog.FileName);
        }
    }

    private void LoadEntityLayout(string? filePath)
    {
        if (filePath != null)
        {
            if (File.Exists(filePath))
            {
                _entityLayout = EntitySerializer.LoadFromJson(filePath);
                if (_entityLayout != null)
                {
                    _currentFilePath = filePath;
                    Text = $"Entity Editor - {Path.GetFileName(filePath)}";
                }
                else
                {
                    // File exists but failed to load - show error
                    MessageBox.Show($"Failed to load entity file: {filePath}\n\nThe file may be corrupted or in an invalid format.", 
                        "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _entityLayout = new EntityLayoutData();
                }
            }
            else
            {
                // File path provided but doesn't exist - show error
                MessageBox.Show($"Entity file not found: {filePath}", 
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _entityLayout = new EntityLayoutData();
            }
        }
        else if (_entityLayout == null)
        {
            _entityLayout = new EntityLayoutData();
        }

        if (_canvas != null && _entityLayout != null)
        {
            _canvas.EntityLayout = _entityLayout;
        }

        RefreshEntityList();
    }

    private void SaveLayout()
    {
        if (_entityLayout == null)
            return;
            
        // Always save to the default world_entities.json file unless explicitly using Save As
        if (_currentFilePath == null)
        {
            _currentFilePath = PathHelper.GetWorldEntitiesPath();
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
    /// Public method to save the entity layout. Can be called from MainEditorForm's Save All.
    /// </summary>
    public void Save()
    {
        SaveLayout();
    }

    private void SaveLayoutAs()
    {
        var entitiesDir = PathHelper.GetEntitiesDirectory();
        if (entitiesDir == null)
        {
            entitiesDir = Path.Combine(Application.StartupPath, "..", "GameContent", "entities");
            entitiesDir = Path.GetFullPath(entitiesDir);
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = entitiesDir,
            FileName = "entities.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveToFile(_currentFilePath);
            Text = $"Entity Editor - {Path.GetFileName(_currentFilePath)}";
        }
    }

    private void SaveToFile(string filePath)
    {
        if (_entityLayout == null)
            return;

        try
        {
            EntitySerializer.SaveToJson(_entityLayout, filePath);
            MessageBox.Show("Entity layout saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving entity layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddEntity()
    {
        if (_entityLayout == null)
        {
            _entityLayout = new EntityLayoutData();
            if (_canvas != null)
            {
                _canvas.EntityLayout = _entityLayout;
            }
        }

        // Get the first entity's sprite as default for new entities
        string? defaultSprite = _entityLayout.Entities.FirstOrDefault()?.SpritePath;
        
        var dialog = new AddEntityDialog(defaultSprite);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var entity = new EntityData
            {
                Type = dialog.EntityType,
                Name = dialog.EntityName,
                Description = dialog.Description,
                SpritePath = dialog.SpritePath != "(None)" ? dialog.SpritePath : null,
                Position = new PositionData { X = 10, Y = 10 },
                PlacementBounds = new PlacementBoundsData
                {
                    Width = 32,
                    Height = 32,
                    ZHeight = 64
                }
            };

            if (dialog.EntityType == "Enemy" || dialog.EntityType == "RangedEnemy")
            {
                entity.EnemyStats = new EnemyStatsData
                {
                    MaxHealth = 50,
                    AttackPower = 8,
                    Defense = 1
                };
            }

            _entityLayout.Entities.Add(entity);
            RefreshEntityList();
            SelectEntity(entity);
        }
    }

    private void RemoveSelectedEntity()
    {
        if (_selectedEntity != null && _entityLayout != null)
        {
            _entityLayout.Entities.Remove(_selectedEntity);
            _selectedEntity = null;
            RefreshEntityList();
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

    private void EditSelectedEntityProperties()
    {
        if (_selectedEntity == null)
            return;

        try
        {
            using var dialog = new EntityPropertiesDialog(_selectedEntity);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Properties were applied to the original entity in the dialog
                RefreshEntityList();
                if (_propertyGrid != null)
                {
                    _propertyGrid.Refresh();
                }
                if (_canvas != null)
                {
                    _canvas.Invalidate();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening entity properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DuplicateSelectedEntity()
    {
        if (_selectedEntity == null || _entityLayout == null)
            return;

        var duplicate = new EntityData
        {
            Type = _selectedEntity.Type,
            Name = _selectedEntity.Name + " (Copy)",
            Description = _selectedEntity.Description,
            DialogId = _selectedEntity.DialogId,
            ItemId = _selectedEntity.ItemId,
            Quantity = _selectedEntity.Quantity,
            SpritePath = _selectedEntity.SpritePath,
            Position = new PositionData
            {
                X = (_selectedEntity.Position?.X ?? 0) + 1, // Offset slightly
                Y = (_selectedEntity.Position?.Y ?? 0) + 1,
                Z = _selectedEntity.Position?.Z ?? 0
            }
        };

        if (_selectedEntity.EnemyStats != null)
        {
            duplicate.EnemyStats = new EnemyStatsData
            {
                MaxHealth = _selectedEntity.EnemyStats.MaxHealth,
                AttackPower = _selectedEntity.EnemyStats.AttackPower,
                Defense = _selectedEntity.EnemyStats.Defense
            };
        }

        if (_selectedEntity.PlacementBounds != null)
        {
            duplicate.PlacementBounds = new PlacementBoundsData
            {
                Width = _selectedEntity.PlacementBounds.Width,
                Height = _selectedEntity.PlacementBounds.Height,
                ZHeight = _selectedEntity.PlacementBounds.ZHeight,
                OffsetX = _selectedEntity.PlacementBounds.OffsetX,
                OffsetY = _selectedEntity.PlacementBounds.OffsetY
            };
        }

        _entityLayout.Entities.Add(duplicate);
        RefreshEntityList();
        SelectEntity(duplicate);
    }

    private void RefreshEntityList()
    {
        if (_entityList == null || _entityLayout == null)
            return;

        _entityList.Items.Clear();
        // Add EntityData objects directly so the custom listbox can draw icons
        foreach (var entity in _entityLayout.Entities)
        {
            _entityList.Items.Add(entity);
        }
    }

    private void OnEntitySelected()
    {
        if (_entityList == null || _entityLayout == null || _entityList.SelectedIndex < 0)
            return;

        var selectedItem = _entityList.SelectedItem;
        if (selectedItem is EntityData entity)
        {
            SelectEntity(entity);
        }
        else
        {
            // Fallback: use index
            var index = _entityList.SelectedIndex;
            if (index >= 0 && index < _entityLayout.Entities.Count)
            {
                SelectEntity(_entityLayout.Entities[index]);
            }
        }
    }

    private void SelectEntity(EntityData entity)
    {
        _selectedEntity = entity;
        if (_propertyGrid != null)
        {
            _propertyGrid.SelectedObject = entity;
        }
        if (_canvas != null)
        {
            _canvas.SelectedEntity = entity;
            _canvas.Invalidate();
        }
    }

    private void CenterOnEntities()
    {
        if (_canvas != null)
        {
            _canvas.CenterOnEntities();
        }
    }

    private void GotoSelectedEntity()
    {
        if (_selectedEntity == null || _canvas == null)
        {
            MessageBox.Show("No entity selected.", "Go to Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        _canvas.CenterOnEntity(_selectedEntity);
    }

    private void ExportEntities()
    {
        try
        {
            var success = EntityExporter.ExportEntitiesToPng(64);
            if (success)
            {
                MessageBox.Show($"Entity icons exported successfully to GameContent/entities directory!", "Export Entities", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to export entity icons. Check the output for error messages.", "Export Entities", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting entity icons: {ex.Message}", "Export Entities", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadTileMap()
    {
        // Try to load the world map to show as background
        // Use the same file selection logic as the game to ensure consistency
        var mapsDir = PathHelper.GetMapsDirectory();
        if (mapsDir != null && Directory.Exists(mapsDir))
        {
            var mapFiles = Directory.GetFiles(mapsDir, "*.json");
            if (mapFiles.Length > 0)
            {
                // Use shared method to get preferred map file (ensures consistency with game)
                var mapFile = GameCore.GameConstants.DefaultFiles.GetPreferredMapFile(mapFiles);
                if (mapFile == null)
                    return;
                
                var tileMap = TileMapSerializer.LoadFromJson(mapFile);
                if (tileMap != null && _canvas != null)
                {
                    // Load tile graphics from tiles.json library (same as tile map editor)
                    var tilesLibraryPath = PathHelper.GetTilesLibraryPath();
                    if (tilesLibraryPath != null && File.Exists(tilesLibraryPath))
                    {
                        var tileGraphics = GameCore.Rendering.TileMapLoader.GetTileGraphics(mapFile, tilesLibraryPath);
                        if (tileGraphics != null && tileGraphics.Count > 0)
                        {
                            // Update the tile map with graphics from the library
                            tileMap.TileGraphics = tileGraphics;
                        }
                    }
                    
                    // Always set the TileMap to trigger regeneration of tile images
                    _canvas.TileMap = tileMap;
                    _canvas.Invalidate(); // Force redraw
                }
            }
        }
    }
    
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Clean up file watchers
        _tilesLibraryFileWatcher?.Dispose();
        _mapFileWatcher?.Dispose();
        _reloadDebounceTimer?.Dispose();
        base.OnFormClosed(e);
    }
}

/// <summary>
/// Dialog for adding a new entity.
/// </summary>
public class AddEntityDialog : Form
{
    private ComboBox? _typeComboBox;
    private TextBox? _nameTextBox;
    private TextBox? _descriptionTextBox;
    private ComboBox? _spriteComboBox;
    private PictureBox? _spritePreview;
    private string? _defaultSprite;

    public string EntityType => _typeComboBox?.SelectedItem?.ToString() ?? "NPC";
    public string EntityName => _nameTextBox?.Text ?? "";
    public string Description => _descriptionTextBox?.Text ?? "";
    public string? SpritePath => _spriteComboBox?.SelectedItem?.ToString();

    public AddEntityDialog(string? defaultSprite = null)
    {
        _defaultSprite = defaultSprite;
        
        Text = "Add Entity";
        Size = new Size(400, 280);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var typeLabel = new Label { Text = "Type:", Location = new Point(20, 20), AutoSize = true };
        _typeComboBox = new ComboBox
        {
            Location = new Point(100, 18),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.AddRange(new[] { "NPC", "Enemy", "RangedEnemy", "GroundItem", "Interactable", "Chest" });
        _typeComboBox.SelectedIndex = 0;

        var nameLabel = new Label { Text = "Name:", Location = new Point(20, 50), AutoSize = true };
        _nameTextBox = new TextBox { Location = new Point(100, 48), Width = 200 };

        var descLabel = new Label { Text = "Description:", Location = new Point(20, 80), AutoSize = true };
        _descriptionTextBox = new TextBox { Location = new Point(100, 78), Width = 200 };

        var spriteLabel = new Label { Text = "Sprite:", Location = new Point(20, 110), AutoSize = true };
        _spriteComboBox = new ComboBox
        {
            Location = new Point(100, 108),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _spriteComboBox.SelectedIndexChanged += (s, e) => UpdateSpritePreview();
        
        // Populate sprites from the sprites/entities folder
        PopulateSprites();
        
        _spritePreview = new PictureBox
        {
            Location = new Point(310, 100),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, 200) };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, 200) };

        Controls.Add(typeLabel);
        Controls.Add(_typeComboBox);
        Controls.Add(nameLabel);
        Controls.Add(_nameTextBox);
        Controls.Add(spriteLabel);
        Controls.Add(_spriteComboBox);
        Controls.Add(_spritePreview);
        Controls.Add(descLabel);
        Controls.Add(_descriptionTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void PopulateSprites()
    {
        if (_spriteComboBox == null) return;
        
        _spriteComboBox.Items.Clear();
        _spriteComboBox.Items.Add("(None)");
        
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null) return;
        
        // Look for sprites in multiple locations
        var spriteFolders = new[]
        {
            Path.Combine(gameContentPath, "sprites", "entities"),
            Path.Combine(gameContentPath, "sprites"),
            Path.Combine(gameContentPath, "entities")
        };

        var foundSprites = new HashSet<string>();
        
        foreach (var folder in spriteFolders)
        {
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder, "*.png")
                    .Concat(Directory.GetFiles(folder, "*.jpg"))
                    .Concat(Directory.GetFiles(folder, "*.jpeg"));
                    
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(gameContentPath, file).Replace('\\', '/');
                    if (foundSprites.Add(relativePath))
                    {
                        _spriteComboBox.Items.Add(relativePath);
                    }
                }
            }
        }
        
        // Select default sprite if provided
        if (!string.IsNullOrEmpty(_defaultSprite))
        {
            var index = _spriteComboBox.Items.IndexOf(_defaultSprite);
            if (index >= 0)
            {
                _spriteComboBox.SelectedIndex = index;
            }
            else
            {
                _spriteComboBox.SelectedIndex = 0;
            }
        }
        else
        {
            // Select first actual sprite if available
            _spriteComboBox.SelectedIndex = _spriteComboBox.Items.Count > 1 ? 1 : 0;
        }
        
        UpdateSpritePreview();
    }

    private void UpdateSpritePreview()
    {
        if (_spritePreview == null || _spriteComboBox == null) return;
        
        _spritePreview.Image?.Dispose();
        _spritePreview.Image = null;
        
        var spritePath = _spriteComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(spritePath) || spritePath == "(None)") return;
        
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null) return;
        
        var fullPath = Path.Combine(gameContentPath, spritePath);
        if (File.Exists(fullPath))
        {
            try
            {
                using var tempImage = Image.FromFile(fullPath);
                _spritePreview.Image = new Bitmap(tempImage, 48, 48);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sprite preview: {ex.Message}");
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _spritePreview?.Image?.Dispose();
        base.OnFormClosing(e);
    }
}

