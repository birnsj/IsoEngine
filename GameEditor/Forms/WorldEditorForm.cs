using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GameEditor.Forms;

/// <summary>
/// Combined form for editing tile maps and entities with tabs.
/// </summary>
public partial class WorldEditorForm : Form
{
    private TabControl? _tabControl;
    private TileMapEditorForm? _tileMapEditor;
    private EntityEditorForm? _entityEditor;
    private LightEditorForm? _lightEditor;
    private string? _currentMapFilePath;
    private string? _currentEntityFilePath;
    private string? _currentLightsFilePath;

    /// <summary>
    /// Gets the current map file path.
    /// </summary>
    public string? CurrentMapFilePath => _currentMapFilePath;

    /// <summary>
    /// Gets the current entity file path.
    /// </summary>
    public string? CurrentEntityFilePath => _currentEntityFilePath;

    /// <summary>
    /// Gets the current lights file path.
    /// </summary>
    public string? CurrentLightsFilePath => _currentLightsFilePath;

    public WorldEditorForm(string? mapFilePath = null, string? entityFilePath = null, string? lightsFilePath = null)
    {
        _currentMapFilePath = mapFilePath;
        _currentEntityFilePath = entityFilePath;
        _currentLightsFilePath = lightsFilePath;
        InitializeComponent();
        LoadTileMap(_currentMapFilePath);
        LoadEntityLayout(_currentEntityFilePath);
        LoadLightLayout(_currentLightsFilePath);
        
        // Reload entities when tile map editor becomes visible
        if (_tileMapEditor != null)
        {
            _tileMapEditor.VisibleChanged += (s, e) =>
            {
                if (_tileMapEditor.Visible)
                {
                    LoadEntityLayout(_currentEntityFilePath);
                    if (_tileMapEditor.Canvas != null)
                    {
                        _tileMapEditor.Canvas.Invalidate();
                    }
                }
            };
        }
    }

    private void InitializeComponent()
    {
        Text = "World Editor";
        MinimumSize = new Size(600, 400);

        // Create tab control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // Tile Map Editor Tab
        var tileMapTab = new TabPage("Tile Map");
        _tileMapEditor = new TileMapEditorForm(_currentMapFilePath);
        _tileMapEditor.TopLevel = false;
        _tileMapEditor.FormBorderStyle = FormBorderStyle.None;
        _tileMapEditor.Dock = DockStyle.Fill;
        tileMapTab.Controls.Add(_tileMapEditor);
        _tileMapEditor.Show();
        _tabControl.TabPages.Add(tileMapTab);

        // Entity Editor Tab
        var entityTab = new TabPage("Entity");
        _entityEditor = new EntityEditorForm(_currentEntityFilePath);
        _entityEditor.TopLevel = false;
        _entityEditor.FormBorderStyle = FormBorderStyle.None;
        _entityEditor.Dock = DockStyle.Fill;
        entityTab.Controls.Add(_entityEditor);
        _entityEditor.Show();
        _tabControl.TabPages.Add(entityTab);

        // Light Editor Tab
        var lightTab = new TabPage("Lights");
        _lightEditor = new LightEditorForm(_currentLightsFilePath);
        _lightEditor.TopLevel = false;
        _lightEditor.FormBorderStyle = FormBorderStyle.None;
        _lightEditor.Dock = DockStyle.Fill;
        lightTab.Controls.Add(_lightEditor);
        _lightEditor.Show();
        _tabControl.TabPages.Add(lightTab);

        Controls.Add(_tabControl);
    }

    private void LoadTileMap(string? filePath)
    {
        _currentMapFilePath = filePath;
    }

    private void LoadEntityLayout(string? filePath)
    {
        _currentEntityFilePath = filePath;
        
        // If no file path provided, use default
        if (_currentEntityFilePath == null)
        {
            _currentEntityFilePath = PathHelper.GetWorldEntitiesPath();
        }
    }

    private void LoadLightLayout(string? filePath)
    {
        _currentLightsFilePath = filePath;
        
        // If no file path provided, use default
        if (_currentLightsFilePath == null)
        {
            _currentLightsFilePath = PathHelper.GetWorldLightsPath();
        }
    }

    /// <summary>
    /// Public method to save all game files (map and entities). Can be called from MainEditorForm's Save All.
    /// </summary>
    public void Save()
    {
        SaveTileMap();
        SaveEntityLayout();
    }

    /// <summary>
    /// Saves both the tile map and entity layout files.
    /// </summary>
    public void SaveAll()
    {
        Save();
        SaveLightLayout();
    }

    /// <summary>
    /// Saves the tile map file.
    /// </summary>
    private void SaveTileMap()
    {
        if (_tileMapEditor != null)
        {
            try
            {
                // Force save to world.json (the file the game loads)
                _tileMapEditor.Save(forceWorldJson: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tile map: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// Saves the entity layout file.
    /// </summary>
    private void SaveEntityLayout()
    {
        if (_entityEditor != null)
        {
            try
            {
                _entityEditor.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving entity layout: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// Saves the light layout file.
    /// </summary>
    private void SaveLightLayout()
    {
        if (_lightEditor != null)
        {
            try
            {
                _lightEditor.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving light layout: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

