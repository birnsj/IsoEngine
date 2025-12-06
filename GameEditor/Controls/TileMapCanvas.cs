using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GameEditor.Data;
using GameEditor.Utilities;
using GameEditor.Services;
using GameCore.Rendering;
using GameCore.Collision;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Linq;

namespace GameEditor.Controls;

/// <summary>
/// Custom control for editing isometric tile maps.
/// </summary>
public class TileMapCanvas : Control
{
    private TileMapData? _tileMap;
    private int _selectedTileIndex = 0;
    // CRITICAL: Zoom must always be 1.0f to match game's camera zoom
    // The game uses Camera2D with Zoom = 1.0f, and tiles are rendered at their
    // actual dimensions from the map file. This ensures perfect visual consistency.
    private float _zoom = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private bool _isDragging = false;
    private PointF _lastMousePos = PointF.Empty;
    private bool _showGrid = true;
    private bool _showCollision = false;
    private bool _showBoundingBoxes = false;
    private string? _mapFileName;
    private string? _mapFilePath;
    private bool _isSettingPlayerStart = false;
    private bool _isEditingCollision = false;
    private bool _isCentering = false;
    private EntityLayoutData? _entityLayout;
    private LightLayoutData? _lightLayout;
    private Bitmap[]? _tileImages; // Cache of tile images
    private CollisionGrid? _collisionGrid;
    private readonly Dictionary<string, Image?> _spriteCache = new(); // Cache of entity sprites

    public TileMapData? TileMap
    {
        get => _tileMap;
        set
        {
            // Dispose old tile images
            DisposeTileImages();
            
            _tileMap = value;
            
            // Generate tile images for the new map
            if (_tileMap != null)
            {
                RegenerateTileImages();
            }
            
            Invalidate();
            
            // Center map after control is ready
            if (_tileMap != null && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!_isCentering && _tileMap != null)
                    {
                        // Set initial zoom to match game scale (game uses zoom 1.0 by default)
                        // Ensure editor matches the visual appearance in game
                        CalculateInitialZoom();
                        CenterMap();
                    }
                }));
            }
        }
    }

    public int SelectedTileIndex
    {
        get => _selectedTileIndex;
        set
        {
            _selectedTileIndex = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the zoom level. 
    /// NOTE: Default should be 1.0f to match game scale, but users can zoom in/out for editing.
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Max(0.1f, Math.Min(5.0f, value));
            Invalidate();
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            Invalidate();
        }
    }

    public bool ShowCollision
    {
        get => _showCollision;
        set
        {
            _showCollision = value;
            if (value && _collisionGrid == null)
            {
                LoadCollisionGrid();
            }
            Invalidate();
        }
    }

    public bool ShowBoundingBoxes
    {
        get => _showBoundingBoxes;
        set
        {
            _showBoundingBoxes = value;
            Invalidate();
        }
    }

    public string? MapFileName
    {
        get => _mapFileName;
        set
        {
            _mapFileName = value;
            Invalidate();
        }
    }

    public string? MapFilePath
    {
        get => _mapFilePath;
        set
        {
            _mapFilePath = value;
            // Load collision grid when map file path changes
            if (_showCollision)
            {
                LoadCollisionGrid();
            }
            Invalidate();
        }
    }

    public EntityLayoutData? EntityLayout
    {
        get => _entityLayout;
        set
        {
            _entityLayout = value;
            Invalidate();
        }
    }

    public LightLayoutData? LightLayout
    {
        get => _lightLayout;
        set
        {
            _lightLayout = value;
            Invalidate();
        }
    }

    public event EventHandler<TileChangedEventArgs>? TileChanged;
    public event EventHandler<PlayerStartSetEventArgs>? PlayerStartSet;
    
    public bool IsSettingPlayerStart
    {
        get => _isSettingPlayerStart;
        set
        {
            _isSettingPlayerStart = value;
            Invalidate();
        }
    }

    public bool IsEditingCollision
    {
        get => _isEditingCollision;
        set
        {
            _isEditingCollision = value;
            if (value)
            {
                // Initialize collision grid when entering edit mode
                InitializeCollisionGridForEditing();
                // Enable collision display to show existing collision data
                _showCollision = true;
            }
            else
            {
                // Save collision grid when exiting edit mode
                SaveCollisionGrid();
            }
            Invalidate();
        }
    }

    public event EventHandler<TileEditRequestedEventArgs>? TileEditRequested;

    public TileMapCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.DarkBlue;
        TabStop = true; // Allow keyboard focus
        SetStyle(ControlStyles.Selectable, true); // Make control selectable for keyboard input
        
        // Create context menu for right-click on tiles
        var contextMenu = new ContextMenuStrip();
        var editTileMenuItem = new ToolStripMenuItem("Edit Tile Properties...");
        editTileMenuItem.Click += (s, e) => OnEditTileRequested();
        contextMenu.Items.Add(editTileMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        var openLocationMenuItem = new ToolStripMenuItem("Open Location");
        openLocationMenuItem.Click += (s, e) => OpenMapLocation();
        contextMenu.Items.Add(openLocationMenuItem);
        ContextMenuStrip = contextMenu;
    }

    private int _rightClickedTileX = -1;
    private int _rightClickedTileY = -1;

    private void OnEditTileRequested()
    {
        if (_rightClickedTileX >= 0 && _rightClickedTileY >= 0 && _tileMap != null)
        {
            var tileIndex = _tileMap.GetTile(_rightClickedTileX, _rightClickedTileY);
            TileEditRequested?.Invoke(this, new TileEditRequestedEventArgs(tileIndex, _rightClickedTileX, _rightClickedTileY));
        }
    }

    /// <summary>
    /// Calculates an appropriate initial zoom level to match the game's visual scale.
    /// The game uses zoom 1.0 by default, so we ensure the editor matches that.
    /// Both editor and game should display tiles at the same visual size.
    /// 
    /// IMPORTANT: This must always return 1.0f to match the game's camera zoom.
    /// The game uses Camera2D with Zoom = 1.0f, and tiles are rendered at their
    /// actual pixel dimensions (TileWidth x TileHeight) from the map file.
    /// Any deviation from 1.0f will cause a scale mismatch between editor and game.
    /// </summary>
    private void CalculateInitialZoom()
    {
        if (_tileMap == null || Width <= 0 || Height <= 0)
            return;

        // CRITICAL: Always use 1.0f to match game's camera zoom
        // The game's Camera2D uses Zoom = 1.0f, and tiles are rendered at their
        // actual dimensions from the map file (TileWidth x TileHeight).
        // This ensures perfect visual consistency between editor and game.
        _zoom = 1.0f;
    }

    /// <summary>
    /// Centers the map in the viewport. If player start position exists, centers on that instead.
    /// </summary>
    public void CenterMap()
    {
        if (_isCentering || _tileMap == null || Width <= 0 || Height <= 0 || !IsHandleCreated)
            return;

        try
        {
            _isCentering = true;

            var tileWidth = _tileMap.TileWidth;
            var tileHeight = _tileMap.TileHeight;

            if (tileWidth <= 0 || tileHeight <= 0)
                return;

            PointF targetWorld;
            
            // If player start position exists, center on that, otherwise center on map
            if (_tileMap.PlayerStartPosition != null)
            {
                targetWorld = new PointF(_tileMap.PlayerStartPosition.X, _tileMap.PlayerStartPosition.Y);
            }
            else
            {
                // Calculate center of map in world coordinates
                targetWorld = new PointF(_tileMap.Width / 2.0f, _tileMap.Height / 2.0f);
            }
            
            // Convert to screen coordinates (without pan/zoom)
            var targetScreen = WorldToScreen(targetWorld, tileWidth, tileHeight);
            
            // Calculate center of viewport
            var viewportCenterX = Width / 2.0f;
            var viewportCenterY = Height / 2.0f;
            
            // Adjust pan offset to center the target
            // The screen position needs to account for zoom
            _panOffset = new PointF(
                viewportCenterX - targetScreen.X * _zoom,
                viewportCenterY - targetScreen.Y * _zoom
            );
            
            Invalidate();
        }
        finally
        {
            _isCentering = false;
        }
    }

    /// <summary>
    /// Centers the view on the player start position.
    /// </summary>
    public void CenterOnPlayerStart()
    {
        if (_isCentering || _tileMap == null || Width <= 0 || Height <= 0 || !IsHandleCreated)
            return;

        if (_tileMap.PlayerStartPosition == null)
            return;

        try
        {
            _isCentering = true;

            var tileWidth = _tileMap.TileWidth;
            var tileHeight = _tileMap.TileHeight;

            if (tileWidth <= 0 || tileHeight <= 0)
                return;

            var playerStartWorld = new PointF(_tileMap.PlayerStartPosition.X, _tileMap.PlayerStartPosition.Y);
            
            // Convert to screen coordinates using actual tile map dimensions to match display
            var playerStartScreen = WorldToScreen(playerStartWorld, tileWidth, tileHeight);
            
            // Calculate center of viewport
            var viewportCenterX = Width / 2.0f;
            var viewportCenterY = Height / 2.0f;
            
            // Adjust pan offset to center the player start
            _panOffset = new PointF(
                viewportCenterX - playerStartScreen.X * _zoom,
                viewportCenterY - playerStartScreen.Y * _zoom
            );
            
            Invalidate();
        }
        finally
        {
            _isCentering = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_tileMap == null)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(_panOffset.X, _panOffset.Y);
        g.ScaleTransform(_zoom, _zoom);

        var tileWidth = _tileMap.TileWidth;
        var tileHeight = _tileMap.TileHeight;

        // Draw tiles
        for (int y = 0; y < _tileMap.Height; y++)
        {
            for (int x = 0; x < _tileMap.Width; x++)
            {
                var tileIndex = _tileMap.GetTile(x, y);
                var screenPos = WorldToScreen(new PointF(x, y), tileWidth, tileHeight);

                // Draw tile image if available, otherwise fall back to colored polygon
                if (_tileImages != null && tileIndex >= 0 && tileIndex < _tileImages.Length && _tileImages[tileIndex] != null)
                {
                    // CRITICAL: Tiles must be drawn at their actual pixel dimensions from the map file
                    // This ensures perfect visual consistency with the game, which also uses the
                    // tile dimensions directly from the map file (TileWidth x TileHeight).
                    // The zoom is 1.0f, so tiles appear at their true size.
                    var tileImage = _tileImages[tileIndex];
                    var imageRect = GetTileBounds(screenPos, tileWidth, tileHeight);
                    
                    // Draw the bitmap with proper positioning (center of image at screen position)
                    // Using actual tile dimensions ensures consistency with game rendering
                    g.DrawImage(tileImage, 
                        imageRect.X, 
                        imageRect.Y, 
                        imageRect.Width, 
                        imageRect.Height);
                }
                else
                {
                    // Fallback to colored polygon if images aren't loaded
                    var tileColor = GetTileColor(tileIndex);
                    using (var brush = new SolidBrush(tileColor))
                    {
                        g.FillPolygon(brush, GetIsometricPolygon(screenPos, tileWidth, tileHeight));
                    }
                }

                // Draw tile index number
                if (_zoom > 0.5f)
                {
                    using (var font = new Font("Arial", 8))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        var text = tileIndex.ToString();
                        var textSize = g.MeasureString(text, font);
                        g.DrawString(text, font, textBrush, 
                            screenPos.X - textSize.Width / 2, 
                            screenPos.Y - textSize.Height / 2);
                    }
                }

            }
        }

        // Draw collision grid if enabled
        if (_showCollision && _collisionGrid != null)
        {
            DrawCollisionGrid(g, tileWidth, tileHeight);
        }

        // Draw collision editing grid overlay (3x3 cells per tile, light green)
        if (_isEditingCollision)
        {
            if (_collisionGrid == null)
            {
                // Ensure collision grid is initialized
                InitializeCollisionGridForEditing();
            }
            if (_collisionGrid != null)
            {
                DrawCollisionEditingGrid(g, tileWidth, tileHeight);
            }
        }

        // Draw grid - extend beyond map boundaries to cover visible area
        if (_showGrid)
        {
            // Calculate visible world bounds by converting screen corners to world coordinates
            var topLeft = ScreenToWorld(new Point(0, 0), tileWidth, tileHeight);
            var topRight = ScreenToWorld(new Point(Width, 0), tileWidth, tileHeight);
            var bottomLeft = ScreenToWorld(new Point(0, Height), tileWidth, tileHeight);
            var bottomRight = ScreenToWorld(new Point(Width, Height), tileWidth, tileHeight);
            
            // Find the min/max world coordinates to determine grid range
            var minWorldX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var maxWorldX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            var minWorldY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var maxWorldY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
            
            // Add padding to extend grid beyond visible area
            var padding = 5.0f;
            var minX = Math.Floor(minWorldX - padding);
            var maxX = Math.Ceiling(maxWorldX + padding);
            var minY = Math.Floor(minWorldY - padding);
            var maxY = Math.Ceiling(maxWorldY + padding);
            
            using (var pen = new Pen(Color.LightBlue, 1))
            {
                for (int y = (int)minY; y <= (int)maxY; y++)
                {
                    for (int x = (int)minX; x <= (int)maxX; x++)
                    {
                        var screenPos = WorldToScreen(new PointF(x, y), tileWidth, tileHeight);
                        var polygon = GetIsometricPolygon(screenPos, tileWidth, tileHeight);
                        g.DrawPolygon(pen, polygon);
                    }
                }
            }
        }

        // Draw entities on top of the map
        if (_entityLayout != null)
        {
            // Entities have fixed size (20 pixels) and do not scale with tile size
            const float entitySize = 20.0f;
            var entityRadius = entitySize / 2.0f;

            // Sort entities by Y position for proper draw order
            var sortedEntities = _entityLayout.Entities
                .OrderBy(e => e.Position.Y + e.Position.Z)
                .ToList();

            foreach (var entity in sortedEntities)
            {
                var entityWorld = new PointF(entity.Position.X, entity.Position.Y);
                // Use actual tile map dimensions to match EntityPlacementCanvas positioning
                var entityScreen = WorldToScreen(entityWorld, tileWidth, tileHeight);
                var entityColor = GetEntityColor(entity.Type);
                
                // Apply visual Z offset
                const float PixelsPerZUnit = 16f;
                entityScreen.Y -= entity.Position.Z * PixelsPerZUnit * _zoom;
                
                // Try to draw entity sprite if available
                bool spriteDrawn = false;
                if (!string.IsNullOrEmpty(entity.SpritePath))
                {
                    spriteDrawn = DrawEntitySprite(g, entityScreen, entity.SpritePath);
                }
                
                // Fall back to colored circle if no sprite
                if (!spriteDrawn)
                {
                    using (var brush = new SolidBrush(entityColor))
                    {
                        g.FillEllipse(brush, entityScreen.X - entityRadius, entityScreen.Y - entityRadius, entitySize, entitySize);
                    }
                    
                    // Draw entity border
                    using (var pen = new Pen(Color.White, 1))
                    {
                        g.DrawEllipse(pen, entityScreen.X - entityRadius, entityScreen.Y - entityRadius, entitySize, entitySize);
                    }
                }
                
                // Draw entity name
                if (_zoom > 0.5f)
                {
                    using (var font = new Font("Arial", 8.0f))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        var text = entity.Name;
                        var textSize = g.MeasureString(text, font);
                        var textY = spriteDrawn ? entityScreen.Y - 48 : entityScreen.Y - 30;
                        g.DrawString(text, font, textBrush, 
                            entityScreen.X - textSize.Width / 2, 
                            textY);
                    }
                }
            }
        }

        // Draw lights on top of entities
        if (_lightLayout != null)
        {
            foreach (var light in _lightLayout.Lights)
            {
                var lightWorld = new PointF(light.Position.X, light.Position.Y);
                var lightScreen = WorldToScreen(lightWorld, tileWidth, tileHeight);
                
                // Convert ColorData to System.Drawing.Color
                var lightColor = Color.FromArgb(light.Color.A, light.Color.R, light.Color.G, light.Color.B);
                
                // Calculate radius in screen coordinates
                // Radius in JSON is stored in screen pixels (matching game's Light class)
                // Use it directly without conversion
                var radiusScreen = light.Radius;
                
                // Draw light circle with semi-transparent fill
                var alpha = (int)(light.Intensity * 255);
                var fillColor = Color.FromArgb(Math.Min(alpha, 100), lightColor); // Cap alpha for visibility
                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillEllipse(brush, lightScreen.X - radiusScreen, lightScreen.Y - radiusScreen, radiusScreen * 2, radiusScreen * 2);
                }
                
                // Draw radius outline
                var outlineColor = Color.FromArgb(180, lightColor);
                using (var pen = new Pen(outlineColor, 1.5f))
                {
                    g.DrawEllipse(pen, lightScreen.X - radiusScreen, lightScreen.Y - radiusScreen, radiusScreen * 2, radiusScreen * 2);
                }
                
                // Draw center point
                const float centerSize = 6.0f;
                using (var brush = new SolidBrush(lightColor))
                {
                    g.FillEllipse(brush, lightScreen.X - centerSize / 2, lightScreen.Y - centerSize / 2, centerSize, centerSize);
                }
                
                // Draw light name if available and zoomed in enough
                if (_zoom > 0.5f && !string.IsNullOrEmpty(light.Name))
                {
                    using (var font = new Font("Arial", 8.0f))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        var text = light.Name;
                        var textSize = g.MeasureString(text, font);
                        g.DrawString(text, font, textBrush, 
                            lightScreen.X - textSize.Width / 2, 
                            lightScreen.Y - radiusScreen - 20);
                    }
                }
            }
        }

        // Draw bounding boxes if enabled
        if (_showBoundingBoxes)
        {
            DrawBoundingBoxes(g, tileWidth, tileHeight);
        }

        // Draw player start position marker
        if (_tileMap.PlayerStartPosition != null)
        {
            var playerStartWorld = new PointF(_tileMap.PlayerStartPosition.X, _tileMap.PlayerStartPosition.Y);
            // Use actual tile map dimensions to match coordinate system
            var playerStartScreen = WorldToScreen(playerStartWorld, tileWidth, tileHeight);
            
            // Player start marker has fixed size and does not scale with tile size
            const float markerSize = 16.0f;
            var markerRadius = markerSize / 2.0f;
            const float arrowLength = 4.0f;
            const float arrowWidth = 3.0f;
            
            // Draw a red circle/arrow marker for player start (fixed size)
            using (var brush = new SolidBrush(Color.Red))
            {
                g.FillEllipse(brush, playerStartScreen.X - markerRadius, playerStartScreen.Y - markerRadius, markerSize, markerSize);
            }
            using (var pen = new Pen(Color.White, 2))
            {
                g.DrawEllipse(pen, playerStartScreen.X - markerRadius, playerStartScreen.Y - markerRadius, markerSize, markerSize);
                // Draw arrow pointing down (default facing)
                g.DrawLine(pen, playerStartScreen.X, playerStartScreen.Y - arrowLength, playerStartScreen.X, playerStartScreen.Y + arrowLength);
                g.DrawLine(pen, playerStartScreen.X - arrowWidth, playerStartScreen.Y + arrowLength * 0.25f, playerStartScreen.X, playerStartScreen.Y + arrowLength);
                g.DrawLine(pen, playerStartScreen.X + arrowWidth, playerStartScreen.Y + arrowLength * 0.25f, playerStartScreen.X, playerStartScreen.Y + arrowLength);
            }
        }

        // Draw cursor/hover highlight
        var mousePos = PointToClient(MousePosition);
        
        if (_isEditingCollision && _collisionGrid != null)
        {
            // Collision editing mode: show light green outline for collision cells
            // Use fixed 64x32 coordinate system to match grid drawing
            const int fixedTileWidth = 64;
            const int fixedTileHeight = 32;
            const int fixedCellPixelWidth = 64;
            const int fixedCellPixelHeight = 32;
            
            var fixedWorldPos = ScreenToWorld(mousePos, fixedTileWidth, fixedTileHeight);
            float cellSize = _collisionGrid.CellSize;
            
            // Snap to collision cell grid
            var snappedX = (float)(Math.Floor(fixedWorldPos.X / cellSize) * cellSize + cellSize / 2.0f);
            var snappedY = (float)(Math.Floor(fixedWorldPos.Y / cellSize) * cellSize + cellSize / 2.0f);
            var snappedWorldPos = new PointF(snappedX, snappedY);
            
            // Get screen position using fixed tile dimensions
            var cellScreenPos = WorldToScreen(snappedWorldPos, fixedTileWidth, fixedTileHeight);
            var polygon = GetIsometricPolygon(cellScreenPos, fixedCellPixelWidth, fixedCellPixelHeight);
            
            // Check if this cell has collision
            var worldVector = new Microsoft.Xna.Framework.Vector2(snappedX, snappedY);
            bool hasCollision = _collisionGrid.IsWorldPositionSolid(worldVector);
            
            // Draw hover highlight - always show, regardless of map bounds
            using (var brush = new SolidBrush(Color.FromArgb(150, Color.LightGreen)))
            {
                g.FillPolygon(brush, polygon);
            }
            using (var pen = new Pen(Color.LightGreen, 2))
            {
                g.DrawPolygon(pen, polygon);
            }
        }
        else
        {
            // Normal tile mode hover
            // Convert mouse position to world coordinates and snap to nearest tile
            var worldPos = ScreenToWorld(mousePos, tileWidth, tileHeight);
            // Snap to nearest tile grid position
            var snappedWorldX = (float)Math.Round(worldPos.X);
            var snappedWorldY = (float)Math.Round(worldPos.Y);
            var snappedWorldPos = new PointF(snappedWorldX, snappedWorldY);
            
            if (snappedWorldPos.X >= 0 && snappedWorldPos.X < _tileMap.Width && 
                snappedWorldPos.Y >= 0 && snappedWorldPos.Y < _tileMap.Height)
            {
                // Convert snapped world position back to screen for preview
                var hoverScreenPos = WorldToScreen(snappedWorldPos, tileWidth, tileHeight);
                
                if (_isSettingPlayerStart)
                {
                    // Player start mode: show lime outline
                    using (var pen = new Pen(Color.Lime, 2))
                    {
                        g.DrawPolygon(pen, GetIsometricPolygon(hoverScreenPos, tileWidth, tileHeight));
                    }
                }
                else
            {
                // Tile edit mode: show selected tile preview
                var previewRect = GetTileBounds(hoverScreenPos, tileWidth, tileHeight);
                
                // Draw the actual tile image with transparency overlay
                if (_tileImages != null && _selectedTileIndex >= 0 && _selectedTileIndex < _tileImages.Length && _tileImages[_selectedTileIndex] != null)
                {
                    // Create a semi-transparent version of the tile for preview
                    var tileImage = _tileImages[_selectedTileIndex];
                    var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.75f, 0}, // 75% opacity
                        new float[] {0, 0, 0, 0, 1}
                    }));
                    
                    g.DrawImage(tileImage, 
                        Rectangle.Round(previewRect),
                        0, 0, tileImage.Width, tileImage.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                    
                    attributes.Dispose();
                }
                else
                {
                    // Fallback to colored polygon
                    var selectedTileColor = GetTileColor(_selectedTileIndex);
                    var previewColor = Color.FromArgb(192, selectedTileColor.R, selectedTileColor.G, selectedTileColor.B);
                    using (var brush = new SolidBrush(previewColor))
                    {
                        g.FillPolygon(brush, GetIsometricPolygon(hoverScreenPos, tileWidth, tileHeight));
                    }
                }
                
                // Draw outline in brighter color for better visibility
                using (var pen = new Pen(Color.Yellow, 3))
                {
                    g.DrawPolygon(pen, GetIsometricPolygon(hoverScreenPos, tileWidth, tileHeight));
                }
                
                // Draw cursor at bottom center of diamond (where tile will be placed)
                var cursorY = hoverScreenPos.Y + tileHeight / 2.0f;
                using (var cursorPen = new Pen(Color.White, 2))
                using (var cursorBrush = new SolidBrush(Color.White))
                {
                    // Draw a small crosshair at the bottom center
                    const float cursorSize = 6.0f;
                    g.FillEllipse(cursorBrush, hoverScreenPos.X - cursorSize / 2, cursorY - cursorSize / 2, cursorSize, cursorSize);
                    g.DrawEllipse(cursorPen, hoverScreenPos.X - cursorSize / 2, cursorY - cursorSize / 2, cursorSize, cursorSize);
                    // Draw crosshair lines
                    g.DrawLine(cursorPen, hoverScreenPos.X - cursorSize, cursorY, hoverScreenPos.X + cursorSize, cursorY);
                    g.DrawLine(cursorPen, hoverScreenPos.X, cursorY - cursorSize, hoverScreenPos.X, cursorY + cursorSize);
                }
                
                // Draw tile index number on preview with better contrast
                if (_zoom > 0.5f)
                {
                    using (var font = new Font("Arial", 9, FontStyle.Bold))
                    using (var shadowBrush = new SolidBrush(Color.Black))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        var text = _selectedTileIndex.ToString();
                        var textSize = g.MeasureString(text, font);
                        var textX = hoverScreenPos.X - textSize.Width / 2;
                        var textY = hoverScreenPos.Y - textSize.Height / 2;
                        
                        // Draw text shadow for better readability
                        g.DrawString(text, font, shadowBrush, textX + 1, textY + 1);
                        g.DrawString(text, font, textBrush, textX, textY);
                    }
                }
                }
            }
        }

        // Draw map info overlay (top-right)
        g.ResetTransform(); // Reset to screen coordinates
        var mapName = _mapFileName ?? "Untitled Map";
        var mousePosForInfo = PointToClient(MousePosition);
        var worldPosForInfo = _isEditingCollision ? ScreenToWorld(mousePosForInfo, 64, 32) : ScreenToWorld(mousePosForInfo, tileWidth, tileHeight);
        var tileX = worldPosForInfo.X >= 0 && worldPosForInfo.X < _tileMap.Width ? (int)Math.Floor(worldPosForInfo.X) : -1;
        var tileY = worldPosForInfo.Y >= 0 && worldPosForInfo.Y < _tileMap.Height ? (int)Math.Floor(worldPosForInfo.Y) : -1;
        var infoText = $"Map: {mapName}\n" +
                       $"Location: Tile ({tileX}, {tileY})\n" +
                       $"World: ({worldPosForInfo.X:F1}, {worldPosForInfo.Y:F1})\n" +
                       $"Zoom: {_zoom:F2}x";
        
        using (var font = new Font("Arial", 9, FontStyle.Bold))
        using (var textBrush = new SolidBrush(Color.Cyan))
        using (var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) // Semi-transparent black
        {
            var textSize = g.MeasureString(infoText, font);
            var textRect = new System.Drawing.RectangleF(Width - textSize.Width - 10, 10, textSize.Width + 10, textSize.Height + 10);
            
            // Draw background
            g.FillRectangle(bgBrush, textRect);
            
            // Draw text
            g.DrawString(infoText, font, textBrush, Width - textSize.Width - 5, 15);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_tileMap == null)
            return;

        if (e.Button == MouseButtons.Left)
        {
            var tileWidth = _tileMap.TileWidth;
            var tileHeight = _tileMap.TileHeight;
            
            if (_isEditingCollision && _collisionGrid != null)
            {
                // Toggle collision at this position - use fixed 64x32 coordinate system
                const int fixedTileWidth = 64;
                const int fixedTileHeight = 32;
                var fixedWorldPos = ScreenToWorld(e.Location, fixedTileWidth, fixedTileHeight);
                
                float cellSize = _collisionGrid.CellSize;
                var snappedX = (float)(Math.Floor(fixedWorldPos.X / cellSize) * cellSize + cellSize / 2.0f);
                var snappedY = (float)(Math.Floor(fixedWorldPos.Y / cellSize) * cellSize + cellSize / 2.0f);
                var worldVector = new Microsoft.Xna.Framework.Vector2(snappedX, snappedY);
                bool isSolid = _collisionGrid.IsWorldPositionSolid(worldVector);
                _collisionGrid.SetWorldPosition(worldVector, !isSolid);
                Invalidate();
            }
            else if (_isSettingPlayerStart)
            {
                // Set player start position using actual tile map dimensions to match coordinate system
                var playerStartWorldPos = ScreenToWorld(e.Location, tileWidth, tileHeight);
                _tileMap.PlayerStartPosition = new PositionData
                {
                    X = playerStartWorldPos.X,
                    Y = playerStartWorldPos.Y
                };
                Invalidate();
                PlayerStartSet?.Invoke(this, new PlayerStartSetEventArgs(playerStartWorldPos.X, playerStartWorldPos.Y));
                _isSettingPlayerStart = false; // Exit player start mode after setting
            }
            else if (_selectedTileIndex >= 0)
            {
                // Normal tile placement - convert click to world coordinates and snap to grid
                // First, convert the raw click position to world coordinates
                var worldPos = ScreenToWorld(e.Location, tileWidth, tileHeight);
                
                // Snap to nearest tile grid position (round to nearest integer)
                int tileX = (int)Math.Round(worldPos.X);
                int tileY = (int)Math.Round(worldPos.Y);

                if (tileX >= 0 && tileX < _tileMap.Width && tileY >= 0 && tileY < _tileMap.Height)
                {
                    _tileMap.SetTile(tileX, tileY, _selectedTileIndex);
                    Invalidate();
                    TileChanged?.Invoke(this, new TileChangedEventArgs(tileX, tileY, _selectedTileIndex));
                }
            }
        }
        else if (e.Button == MouseButtons.Middle)
        {
            // Middle mouse button - start dragging (works everywhere, including over tiles)
            _isDragging = true;
            _lastMousePos = e.Location;
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (_isEditingCollision)
            {
                // Exit collision edit mode on right click
                _isEditingCollision = false;
                SaveCollisionGrid();
                Invalidate();
            }
            else
            {
                // Right click deselects the tile
                _selectedTileIndex = -1;
                Invalidate();
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging)
        {
            var delta = new PointF(e.Location.X - _lastMousePos.X, e.Location.Y - _lastMousePos.Y);
            _panOffset = new PointF(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastMousePos = e.Location;
            Invalidate();
        }
        else
        {
            Invalidate(); // Update hover highlight
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        
        var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        var oldZoom = _zoom;
        Zoom = _zoom * zoomFactor;

        // Zoom towards mouse position
        var mousePos = e.Location;
        var worldPos = ScreenToWorld(mousePos, _tileMap?.TileWidth ?? 64, _tileMap?.TileHeight ?? 32);
        var newScreenPos = WorldToScreen(worldPos, _tileMap?.TileWidth ?? 64, _tileMap?.TileHeight ?? 32);
        _panOffset = new PointF(
            _panOffset.X + (mousePos.X - newScreenPos.X * _zoom) - (mousePos.X - newScreenPos.X * oldZoom),
            _panOffset.Y + (mousePos.Y - newScreenPos.Y * _zoom) - (mousePos.Y - newScreenPos.Y * oldZoom)
        );

        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Focus(); // Automatically focus when mouse enters to enable keyboard input
    }

    protected override bool IsInputKey(Keys keyData)
    {
        // Allow arrow keys and WASD to be processed
        switch (keyData)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
            case Keys.W:
            case Keys.A:
            case Keys.S:
            case Keys.D:
                return true;
            default:
                return base.IsInputKey(keyData);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        const float panSpeed = 10.0f; // Pixels to pan per key press
        
        switch (keyData)
        {
            case Keys.W:
            case Keys.Up:
                _panOffset = new PointF(_panOffset.X, _panOffset.Y + panSpeed);
                Invalidate();
                return true;
            case Keys.S:
            case Keys.Down:
                _panOffset = new PointF(_panOffset.X, _panOffset.Y - panSpeed);
                Invalidate();
                return true;
            case Keys.A:
            case Keys.Left:
                _panOffset = new PointF(_panOffset.X + panSpeed, _panOffset.Y);
                Invalidate();
                return true;
            case Keys.D:
            case Keys.Right:
                _panOffset = new PointF(_panOffset.X - panSpeed, _panOffset.Y);
                Invalidate();
                return true;
        }
        
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private PointF WorldToScreen(PointF worldPos, int tileWidth, int tileHeight)
    {
        // Isometric projection
        var screenX = (worldPos.X - worldPos.Y) * (tileWidth / 2.0f);
        var screenY = (worldPos.X + worldPos.Y) * (tileHeight / 2.0f);
        return new PointF(screenX, screenY);
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates using fixed tile size for entities.
    /// This ensures entity positions don't reposition when tile size changes.
    /// Uses base tile size of 64x32 pixels.
    /// </summary>
    private PointF WorldToScreenFixed(PointF worldPos)
    {
        // Fixed tile size for entity positioning (does not scale with actual tile size)
        const int FixedTileWidth = 64;
        const int FixedTileHeight = 32;
        
        var screenX = (worldPos.X - worldPos.Y) * (FixedTileWidth / 2.0f);
        var screenY = (worldPos.X + worldPos.Y) * (FixedTileHeight / 2.0f);
        return new PointF(screenX, screenY);
    }

    private PointF ScreenToWorld(Point mousePos, int tileWidth, int tileHeight)
    {
        // Adjust for pan and zoom
        var adjustedX = (mousePos.X - _panOffset.X) / _zoom;
        var adjustedY = (mousePos.Y - _panOffset.Y) / _zoom;

        // Inverse isometric projection
        var worldX = (adjustedX / (tileWidth / 2.0f) + adjustedY / (tileHeight / 2.0f)) / 2.0f;
        var worldY = (adjustedY / (tileHeight / 2.0f) - adjustedX / (tileWidth / 2.0f)) / 2.0f;
        return new PointF(worldX, worldY);
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates using fixed tile size for entities.
    /// This ensures entity positions don't reposition when tile size changes.
    /// Uses base tile size of 64x32 pixels.
    /// </summary>
    private PointF ScreenToWorldFixed(Point mousePos)
    {
        // Adjust for pan and zoom
        var adjustedX = (mousePos.X - _panOffset.X) / _zoom;
        var adjustedY = (mousePos.Y - _panOffset.Y) / _zoom;

        // Fixed tile size for entity positioning (does not scale with actual tile size)
        const int FixedTileWidth = 64;
        const int FixedTileHeight = 32;

        // Inverse isometric projection using fixed tile dimensions
        var worldX = (adjustedX / (FixedTileWidth / 2.0f) + adjustedY / (FixedTileHeight / 2.0f)) / 2.0f;
        var worldY = (adjustedY / (FixedTileHeight / 2.0f) - adjustedX / (FixedTileWidth / 2.0f)) / 2.0f;
        return new PointF(worldX, worldY);
    }

    private PointF[] GetIsometricPolygon(PointF center, int tileWidth, int tileHeight)
    {
        return new PointF[]
        {
            new PointF(center.X, center.Y - tileHeight / 2), // Top
            new PointF(center.X + tileWidth / 2, center.Y), // Right
            new PointF(center.X, center.Y + tileHeight / 2), // Bottom
            new PointF(center.X - tileWidth / 2, center.Y)  // Left
        };
    }

    /// <summary>
    /// Creates an isometric diamond polygon with custom width and height.
    /// </summary>
    private PointF[] GetIsometricPolygonScaled(PointF center, float width, float height)
    {
        return new PointF[]
        {
            new PointF(center.X, center.Y - height / 2), // Top
            new PointF(center.X + width / 2, center.Y), // Right
            new PointF(center.X, center.Y + height / 2), // Bottom
            new PointF(center.X - width / 2, center.Y)  // Left
        };
    }

    /// <summary>
    /// Draws a 3D shaded isometric bounding box for an entity (matching Entity Editor style).
    /// Uses proper isometric projection based on pixel values from PlacementBounds.
    /// </summary>
    private void Draw3DEntityBoundingBox(Graphics g, EntityData entity, int tileWidth, int tileHeight)
    {
        // Get entity screen position as center point
        var entityWorld = new PointF(entity.Position.X, entity.Position.Y);
        var screenCenter = WorldToScreen(entityWorld, tileWidth, tileHeight);
        
        // Apply entity Z offset (entity elevation)
        const float PixelsPerZUnit = 16f;
        float entityZOffset = entity.Position.Z * PixelsPerZUnit * _zoom;
        screenCenter.Y -= entityZOffset;
        
        // Get bounding box dimensions in pixels from PlacementBounds
        // W = width, H = height (vertical), D = depth
        float wPx = entity.PlacementBounds?.Width ?? 32f;
        float hPx = entity.PlacementBounds?.ZHeight ?? 64f;  // ZHeight is the vertical height
        float dPx = entity.PlacementBounds?.Height ?? 32f;   // Height in PlacementBounds is actually depth
        
        // Scale for display
        float scale = _zoom * 0.5f;
        
        // Isometric projection (2:1 ratio)
        float isoXx = 0.5f * scale;
        float isoXy = 0.25f * scale;
        float isoZx = -0.5f * scale;
        float isoZy = 0.25f * scale;
        
        // Calculate the 8 corners of the 3D box
        // Bottom face (y = 0)
        var b0 = new PointF(screenCenter.X, screenCenter.Y);
        var b1 = new PointF(screenCenter.X + wPx * isoXx, screenCenter.Y + wPx * isoXy);
        var b2 = new PointF(screenCenter.X + wPx * isoXx + dPx * isoZx, screenCenter.Y + wPx * isoXy + dPx * isoZy);
        var b3 = new PointF(screenCenter.X + dPx * isoZx, screenCenter.Y + dPx * isoZy);

        // Top face (y = height)
        float yOffset = -hPx * scale;
        var t0 = new PointF(b0.X, b0.Y + yOffset);
        var t1 = new PointF(b1.X, b1.Y + yOffset);
        var t2 = new PointF(b2.X, b2.Y + yOffset);
        var t3 = new PointF(b3.X, b3.Y + yOffset);
        
        // Cyan color for all entities in tile map view
        int baseR = 0, baseG = 180, baseB = 180;
        
        // Draw faces from back to front for proper occlusion
        // Back face (b0, b1, t1, t0) - darkest
        var backFace = new PointF[] { b0, b1, t1, t0 };
        using (var brush = new SolidBrush(Color.FromArgb(60, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, backFace);
        }
        
        // Left face (b0, b3, t3, t0) - medium dark
        var leftFace = new PointF[] { b0, b3, t3, t0 };
        using (var brush = new SolidBrush(Color.FromArgb(70, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, leftFace);
        }

        // Bottom face (b0, b1, b2, b3) - dark
        var bottomFace = new PointF[] { b0, b1, b2, b3 };
        using (var brush = new SolidBrush(Color.FromArgb(40, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, bottomFace);
        }
        
        // Right face (b1, b2, t2, t1) - medium bright
        var rightFace = new PointF[] { b1, b2, t2, t1 };
        using (var brush = new SolidBrush(Color.FromArgb(90, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, rightFace);
        }
        
        // Front face (b3, b2, t2, t3) - bright
        var frontFace = new PointF[] { b3, b2, t2, t3 };
        using (var brush = new SolidBrush(Color.FromArgb(110, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, frontFace);
        }
        
        // Top face (t0, t1, t2, t3) - brightest
        var topFace = new PointF[] { t0, t1, t2, t3 };
        using (var brush = new SolidBrush(Color.FromArgb(130, baseR, baseG, baseB)))
        {
            g.FillPolygon(brush, topFace);
        }
        
        // Draw edges
        using (var pen = new Pen(Color.FromArgb(200, Color.Cyan), 1.5f))
        {
            // Bottom edges
            g.DrawLine(pen, b0, b1);
            g.DrawLine(pen, b1, b2);
            g.DrawLine(pen, b2, b3);
            g.DrawLine(pen, b3, b0);
            
            // Top edges
            g.DrawLine(pen, t0, t1);
            g.DrawLine(pen, t1, t2);
            g.DrawLine(pen, t2, t3);
            g.DrawLine(pen, t3, t0);
            
            // Vertical edges
            g.DrawLine(pen, b0, t0);
            g.DrawLine(pen, b1, t1);
            g.DrawLine(pen, b2, t2);
            g.DrawLine(pen, b3, t3);
        }
        
        // Draw entity type label above the box
        using (var font = new Font("Arial", 7.0f))
        using (var textBrush = new SolidBrush(Color.Cyan))
        {
            var typeText = $"{entity.Type ?? "Unknown"}";
            if (!string.IsNullOrEmpty(entity.Name))
                typeText = entity.Name;
            var textSize = g.MeasureString(typeText, font);
            g.DrawString(typeText, font, textBrush, 
                t0.X - textSize.Width / 2 + (wPx * isoXx / 2) + (dPx * isoZx / 2), 
                t0.Y + yOffset / 2 - textSize.Height - 2);
        }
    }

    private System.Drawing.RectangleF GetTileBounds(PointF center, int tileWidth, int tileHeight)
    {
        return new System.Drawing.RectangleF(
            center.X - tileWidth / 2,
            center.Y - tileHeight / 2,
            tileWidth,
            tileHeight
        );
    }

    private Color GetTileColor(int tileIndex)
    {
        // Simple color mapping for different tile types
        return tileIndex switch
        {
            0 => Color.LightGreen,      // Grass
            1 => Color.DarkGreen,       // Dark grass
            2 => Color.SaddleBrown,     // Dirt/Road
            3 => Color.Brown,           // Dark dirt
            4 => Color.Blue,            // Water
            5 => Color.DarkBlue,        // Deep water
            6 => Color.Gray,            // Stone
            7 => Color.DarkGray,        // Dark stone
            _ => Color.DarkSlateGray    // Unknown
        };
    }

    private Color GetEntityColor(string entityType)
    {
        return entityType switch
        {
            "NPC" => Color.Blue,
            "Enemy" => Color.Red,
            "RangedEnemy" => Color.OrangeRed,
            "GroundItem" => Color.Gold,
            "Interactable" => Color.Green,
            "Chest" => Color.SaddleBrown,
            _ => Color.White
        };
    }

    /// <summary>
    /// Draws an entity sprite at the specified screen position.
    /// </summary>
    private bool DrawEntitySprite(Graphics g, PointF screenPos, string spritePath)
    {
        try
        {
            // Check cache first
            if (!_spriteCache.TryGetValue(spritePath, out var cachedImage))
            {
                // Load and cache the sprite
                var gameContentPath = PathHelper.GetGameContentPath();
                string? fullPath = null;
                
                if (gameContentPath != null && !Path.IsPathRooted(spritePath))
                {
                    fullPath = Path.Combine(gameContentPath, spritePath);
                }
                else if (Path.IsPathRooted(spritePath))
                {
                    fullPath = spritePath;
                }

                if (fullPath != null && File.Exists(fullPath))
                {
                    using var tempImage = Image.FromFile(fullPath);
                    // Scale sprite to a reasonable size (32x32 pixels)
                    cachedImage = new Bitmap(tempImage, 32, 32);
                }
                else
                {
                    cachedImage = null;
                }
                
                _spriteCache[spritePath] = cachedImage;
            }

            if (cachedImage != null)
            {
                // Draw sprite centered at position, with bottom at the screen position
                var spriteWidth = (int)(cachedImage.Width * _zoom);
                var spriteHeight = (int)(cachedImage.Height * _zoom);
                var drawX = screenPos.X - spriteWidth / 2;
                var drawY = screenPos.Y - spriteHeight;
                
                g.DrawImage(cachedImage, drawX, drawY, spriteWidth, spriteHeight);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading entity sprite {spritePath}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Clears the sprite cache. Call this when entities are reloaded.
    /// </summary>
    public void ClearSpriteCache()
    {
        foreach (var img in _spriteCache.Values)
        {
            img?.Dispose();
        }
        _spriteCache.Clear();
    }

    private void LoadCollisionGrid()
    {
        _collisionGrid = null;

        if (string.IsNullOrEmpty(_mapFilePath) || _tileMap == null)
            return;

        // Determine collision file path (same directory as map file, with _collision suffix)
        var directory = Path.GetDirectoryName(_mapFilePath);
        var fileName = Path.GetFileNameWithoutExtension(_mapFilePath);
        var extension = Path.GetExtension(_mapFilePath);
        var collisionFilePath = Path.Combine(directory ?? "", $"{fileName}_collision{extension}");

        if (File.Exists(collisionFilePath))
        {
            _collisionGrid = CollisionGridSerializer.LoadFromJson(collisionFilePath);
            Invalidate();
        }
    }

    private void InitializeCollisionGridForEditing()
    {
        if (_tileMap == null)
            return;

        // Load existing collision grid or create new one
        if (_collisionGrid == null)
        {
            LoadCollisionGrid();
        }

        // If still no collision grid, create a new one
        // Use fixed 64x32 pixel cell size for collision cells
        // Calculate world cell size based on tile size to maintain fixed pixel dimensions
        if (_collisionGrid == null)
        {
            const int fixedCellPixelWidth = 64;
            const int fixedCellPixelHeight = 32;
            
            // Calculate how many world units correspond to fixed pixel size
            // If tile is 64x32 pixels and represents 1x1 world units, then 64x32 pixels = 1x1 world
            // So 64x32 pixel cell = (64/tileWidth) x (32/tileHeight) world units
            float cellSizeX = (float)fixedCellPixelWidth / _tileMap.TileWidth;
            float cellSizeY = (float)fixedCellPixelHeight / _tileMap.TileHeight;
            
            // Use the smaller cell size to maintain square cells
            float cellSize = Math.Min(cellSizeX, cellSizeY);
            
            _collisionGrid = new CollisionGrid(_tileMap.Width, _tileMap.Height, cellSize);
        }
    }

    private void SaveCollisionGrid()
    {
        if (_collisionGrid == null || string.IsNullOrEmpty(_mapFilePath))
            return;

        // Determine collision file path (same directory as map file, with _collision suffix)
        var directory = Path.GetDirectoryName(_mapFilePath);
        var fileName = Path.GetFileNameWithoutExtension(_mapFilePath);
        var extension = Path.GetExtension(_mapFilePath);
        var collisionFilePath = Path.Combine(directory ?? "", $"{fileName}_collision{extension}");

        // Ensure directory exists
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
        PerforceService.EnsureFileReadyForEdit(collisionFilePath);
        
        CollisionGridSerializer.SaveToJson(_collisionGrid, collisionFilePath);
        
        // Add file to Perforce if it's new
        PerforceService.AddFile(collisionFilePath);
    }

    private void DrawCollisionGrid(Graphics g, int tileWidth, int tileHeight)
    {
        if (_collisionGrid == null || _tileMap == null)
            return;

        // Draw collision cells that are solid
        for (int y = 0; y < _tileMap.Height; y++)
        {
            for (int x = 0; x < _tileMap.Width; x++)
            {
                // Check if this tile position has collision
                var worldPos = new Microsoft.Xna.Framework.Vector2(x, y);
                if (_collisionGrid.IsWorldPositionSolid(worldPos))
                {
                    var screenPos = WorldToScreen(new PointF(x, y), tileWidth, tileHeight);
                    var polygon = GetIsometricPolygon(screenPos, tileWidth, tileHeight);

                    // Draw collision cell as semi-transparent red overlay
                    using (var brush = new SolidBrush(Color.FromArgb(180, Color.Red)))
                    {
                        g.FillPolygon(brush, polygon);
                    }

                    using (var pen = new Pen(Color.DarkRed, 2))
                    {
                        g.DrawPolygon(pen, polygon);
                    }
                }
            }
        }
    }

    private void DrawBoundingBoxes(Graphics g, int tileWidth, int tileHeight)
    {
        // Draw entity bounding boxes as 3D isometric boxes (matching Entity Editor style)
        if (_entityLayout != null)
        {
            foreach (var entity in _entityLayout.Entities)
            {
                Draw3DEntityBoundingBox(g, entity, tileWidth, tileHeight);
            }
        }
        
        // Draw light bounding circles
        if (_lightLayout != null)
        {
            foreach (var light in _lightLayout.Lights)
            {
                var lightWorld = new PointF(light.Position.X, light.Position.Y);
                var lightScreen = WorldToScreen(lightWorld, tileWidth, tileHeight);
                var radiusScreen = light.Radius * _zoom;
                
                // Draw bounding circle outline
                using (var pen = new Pen(Color.Yellow, 2))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawEllipse(pen, 
                        lightScreen.X - radiusScreen, 
                        lightScreen.Y - radiusScreen, 
                        radiusScreen * 2, 
                        radiusScreen * 2);
                }
            }
        }
        
        // Draw tile bounding boxes (one per tile)
        if (_tileMap != null)
        {
            using (var pen = new Pen(Color.FromArgb(100, Color.Magenta), 1))
            {
                for (int y = 0; y < _tileMap.Height; y++)
                {
                    for (int x = 0; x < _tileMap.Width; x++)
                    {
                        var screenPos = WorldToScreen(new PointF(x, y), tileWidth, tileHeight);
                        var polygon = GetIsometricPolygon(screenPos, tileWidth, tileHeight);
                        g.DrawPolygon(pen, polygon);
                    }
                }
            }
        }
    }

    private void DrawCollisionEditingGrid(Graphics g, int tileWidth, int tileHeight)
    {
        if (_collisionGrid == null || _tileMap == null)
            return;

        // Use fixed 64x32 pixel cell size for collision cells (independent of tile size)
        const int fixedCellPixelWidth = 64;
        const int fixedCellPixelHeight = 32;
        
        // Get the cell size from the collision grid (in world units)
        float cellSize = _collisionGrid.CellSize;
        
        // Collision cells are always drawn at fixed pixel size (scaled by zoom)
        var collisionCellWidth = fixedCellPixelWidth * _zoom;
        var collisionCellHeight = fixedCellPixelHeight * _zoom;

        // Fixed tile dimensions for coordinate conversion (must match collision grid)
        const int fixedTileWidth = 64;
        const int fixedTileHeight = 32;
        
        // Convert screen coordinates to world coordinates accounting for pan/zoom transform
        // The graphics transform has already been applied, so we need to reverse it for ScreenToWorld
        var screenCorners = new[]
        {
            ScreenToWorldTransformed(new Point(0, 0), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(Width, 0), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(0, Height), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(Width, Height), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(Width / 2, 0), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(Width / 2, Height), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(0, Height / 2), fixedTileWidth, fixedTileHeight),
            ScreenToWorldTransformed(new Point(Width, Height / 2), fixedTileWidth, fixedTileHeight)
        };
        
        var minWorldX = screenCorners.Min(p => p.X);
        var maxWorldX = screenCorners.Max(p => p.X);
        var minWorldY = screenCorners.Min(p => p.Y);
        var maxWorldY = screenCorners.Max(p => p.Y);
        
        // Calculate grid cell range to cover visible area with padding
        int minGridCellX = (int)Math.Floor(minWorldX / cellSize) - 2;
        int maxGridCellX = (int)Math.Ceiling(maxWorldX / cellSize) + 2;
        int minGridCellY = (int)Math.Floor(minWorldY / cellSize) - 2;
        int maxGridCellY = (int)Math.Ceiling(maxWorldY / cellSize) + 2;

        // Bright light green color for the grid - make it very visible
        var lightGreen = Color.FromArgb(255, 144, 238, 144); // Bright light green, fully opaque
        using (var pen = new Pen(lightGreen, Math.Max(1f, 2f / _zoom))) // Scale pen width with zoom
        {
            // Draw continuous grid - 3x3 cells per tile
            for (int gridCellY = minGridCellY; gridCellY <= maxGridCellY; gridCellY++)
            {
                for (int gridCellX = minGridCellX; gridCellX <= maxGridCellX; gridCellX++)
                {
                    // Calculate world position of this collision cell (center of the cell)
                    var cellWorldX = gridCellX * cellSize + cellSize / 2.0f;
                    var cellWorldY = gridCellY * cellSize + cellSize / 2.0f;
                    
                    // Get screen position for the center of this collision cell
                    // Use fixed 64x32 tile dimensions to ensure collision cells align correctly
                    var cellScreenPos = WorldToScreen(new PointF(cellWorldX, cellWorldY), fixedTileWidth, fixedTileHeight);
                    
                    // Draw the grid cell outline at fixed pixel size (always 64x32, scaled by zoom)
                    var polygon = GetIsometricPolygon(cellScreenPos, fixedCellPixelWidth, fixedCellPixelHeight);
                    g.DrawPolygon(pen, polygon);
                    
                    // If this cell has collision, fill it with semi-transparent light green
                    // Only check collision if within map bounds (but draw grid everywhere)
                    var worldPos = new PointF(cellWorldX, cellWorldY);
                    if (worldPos.X >= 0 && worldPos.X <= _tileMap.Width && 
                        worldPos.Y >= 0 && worldPos.Y <= _tileMap.Height)
                    {
                        var worldVector = new Microsoft.Xna.Framework.Vector2(cellWorldX, cellWorldY);
                        if (_collisionGrid.IsWorldPositionSolid(worldVector))
                        {
                            using (var brush = new SolidBrush(Color.FromArgb(180, lightGreen)))
                            {
                                g.FillPolygon(brush, polygon);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates, accounting for the graphics transform (pan/zoom).
    /// </summary>
    private PointF ScreenToWorldTransformed(Point screenPos, int tileWidth, int tileHeight)
    {
        // Reverse the graphics transform (pan and zoom)
        var adjustedX = (screenPos.X - _panOffset.X) / _zoom;
        var adjustedY = (screenPos.Y - _panOffset.Y) / _zoom;
        
        // Now convert to world coordinates using the fixed tile dimensions
        return ScreenToWorld(new Point((int)adjustedX, (int)adjustedY), tileWidth, tileHeight);
    }

    /// <summary>
    /// Generates tile images for the current tile map.
    /// </summary>
    private void RegenerateTileImages()
    {
        DisposeTileImages();

        if (_tileMap == null)
            return;

        // Calculate number of tiles dynamically:
        // 1. Check if TileGraphics has entries (use max index + 1)
        // 2. Otherwise default to 8 for backward compatibility
        int numTiles = 8;
        if (_tileMap.TileGraphics != null && _tileMap.TileGraphics.Count > 0)
        {
            // Find the maximum tile index in TileGraphics and add 1
            // This ensures all tiles with graphics are included
            var maxIndex = _tileMap.TileGraphics.Keys.Max();
            numTiles = Math.Max(numTiles, maxIndex + 1);
            
            // Also check if there are gaps - we want to include all tiles up to the max
            // For example, if we have tiles 0-7 and then add tile 10, we want 11 tiles total
        }
        
        _tileImages = new Bitmap[numTiles];

        // Generate full-size tile images matching the map's tile dimensions
        for (int i = 0; i < numTiles; i++)
        {
            // Get custom graphic path if available
            string? graphicPath = null;
            if (_tileMap.TileGraphics != null && _tileMap.TileGraphics.TryGetValue(i, out var path))
            {
                graphicPath = path;
            }

            _tileImages[i] = TileImageGenerator.GenerateTileImage(
                i, 
                _tileMap.TileWidth, 
                _tileMap.TileHeight, 
                new Size(_tileMap.TileWidth, _tileMap.TileHeight), // Full size, no scaling
                graphicPath);
        }
    }

    /// <summary>
    /// Disposes of cached tile images.
    /// </summary>
    private void DisposeTileImages()
    {
        if (_tileImages != null)
        {
            foreach (var image in _tileImages)
            {
                image?.Dispose();
            }
            _tileImages = null;
        }
    }

    /// <summary>
    /// Opens the map file's directory location in Windows Explorer.
    /// </summary>
    private void OpenMapLocation()
    {
        if (string.IsNullOrEmpty(_mapFilePath))
        {
            MessageBox.Show(
                "No map file path available. Please save the map first.",
                "Open Location",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_mapFilePath);
            if (directory != null && Directory.Exists(directory))
            {
                // Open the directory in Windows Explorer and select the file
                Process.Start("explorer.exe", $"/select,\"{_mapFilePath}\"");
            }
            else
            {
                MessageBox.Show(
                    $"Directory not found: {directory ?? "null"}",
                    "Open Location",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error opening location: {ex.Message}",
                "Open Location",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeTileImages();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Event arguments for tile change events.
/// </summary>
public class TileChangedEventArgs : EventArgs
{
    public int X { get; }
    public int Y { get; }
    public int TileIndex { get; }

    public TileChangedEventArgs(int x, int y, int tileIndex)
    {
        X = x;
        Y = y;
        TileIndex = tileIndex;
    }
}

/// <summary>
/// Event arguments for tile edit requests.
/// </summary>
public class TileEditRequestedEventArgs : EventArgs
{
    public int TileIndex { get; }
    public int X { get; }
    public int Y { get; }

    public TileEditRequestedEventArgs(int tileIndex, int x, int y)
    {
        TileIndex = tileIndex;
        X = x;
        Y = y;
    }
}

