using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using GameEditor.Data;
using GameEditor.Utilities;
using System.Linq;

namespace GameEditor.Controls;

/// <summary>
/// Custom control for placing entities on a map.
/// </summary>
public class EntityPlacementCanvas : Control
{
    private EntityLayoutData? _entityLayout;
    private TileMapData? _tileMap;
    private EntityData? _selectedEntity;
    private float _zoom = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private bool _isDragging = false;
    private PointF _lastMousePos = PointF.Empty;
    private PointF _lastEntityWorldPos = PointF.Empty; // Track last entity position to avoid redundant updates
    private float _gridSize = 0.25f; // Grid size in world coordinates (0.25 = quarter tile, 0.5 = half tile, 1.0 = full tile)
    private bool _snapToGrid = true;
    private bool _showBounds = false;
    private const float PositionChangeThreshold = 0.01f; // Only invalidate if position changed by at least this amount
    private const int TileWidth = 64;
    private const int TileHeight = 32;
    private const float PixelsPerZUnit = 16f; // How many pixels one Z unit raises the entity visually
    private Bitmap[]? _tileImages; // Cache of tile images
    private readonly Dictionary<string, Image?> _spriteCache = new(); // Cache of entity sprites

    public EntityLayoutData? EntityLayout
    {
        get => _entityLayout;
        set
        {
            _entityLayout = value;
            Invalidate();
            
            // Center on entities after layout is set
            if (_entityLayout != null && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    CenterOnEntities();
                }));
            }
        }
    }

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
        }
    }

    public EntityData? SelectedEntity
    {
        get => _selectedEntity;
        set
        {
            _selectedEntity = value;
            Invalidate();
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Max(0.1f, Math.Min(5.0f, value));
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the grid size for precise placement (in world coordinates).
    /// Common values: 0.125 (1/8 tile), 0.25 (1/4 tile), 0.5 (1/2 tile), 1.0 (full tile).
    /// </summary>
    public float GridSize
    {
        get => _gridSize;
        set
        {
            _gridSize = Math.Max(0.0625f, Math.Min(4.0f, value)); // Clamp between 1/16 and 4 tiles
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets whether entities should snap to the grid when placed or moved.
    /// </summary>
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set
        {
            _snapToGrid = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets whether to show placement bounds visualization.
    /// </summary>
    public bool ShowBounds
    {
        get => _showBounds;
        set
        {
            _showBounds = value;
            Invalidate();
        }
    }

    public event EventHandler<EntitySelectedEventArgs>? EntitySelected;
    public event EventHandler<EntityMovedEventArgs>? EntityMoved;

    public EntityPlacementCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.DarkBlue;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(_panOffset.X, _panOffset.Y);
        g.ScaleTransform(_zoom, _zoom);

        // Draw tile map as background if available
        if (_tileMap != null)
        {
            var tileWidth = _tileMap.TileWidth;
            var tileHeight = _tileMap.TileHeight;

            for (int y = 0; y < _tileMap.Height; y++)
            {
                for (int x = 0; x < _tileMap.Width; x++)
                {
                    var tileIndex = _tileMap.GetTile(x, y);
                    var screenPos = WorldToScreen(new PointF(x, y));
                    
                    // Draw tile image if available, otherwise fall back to colored polygon
                    if (_tileImages != null && tileIndex >= 0 && tileIndex < _tileImages.Length && _tileImages[tileIndex] != null)
                    {
                        // Draw the actual tile image
                        var tileImage = _tileImages[tileIndex];
                        var imageRect = GetIsometricTileBounds(screenPos, tileWidth, tileHeight);
                        
                        // Draw the bitmap with proper positioning
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
                            g.FillPolygon(brush, GetIsometricPolygon(screenPos));
                        }
                    }

                    // Grid lines will be drawn separately to extend beyond map boundaries
                }
            }

            // Draw player start position if set
            if (_tileMap.PlayerStartPosition != null)
            {
                var playerStartWorld = new PointF(_tileMap.PlayerStartPosition.X, _tileMap.PlayerStartPosition.Y);
                // Use actual tile map dimensions to match tile display
                var playerStartScreen = WorldToScreen(playerStartWorld);
                
                // Player start marker has fixed size and does not scale with tile size
                const float markerSize = 16.0f;
                var markerRadius = markerSize / 2.0f;
                
                using (var brush = new SolidBrush(Color.Red))
                {
                    g.FillEllipse(brush, playerStartScreen.X - markerRadius, playerStartScreen.Y - markerRadius, markerSize, markerSize);
                }
                using (var pen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(pen, playerStartScreen.X - markerRadius, playerStartScreen.Y - markerRadius, markerSize, markerSize);
                }
            }
            
            // Draw base tile grid (always at tile size, light blue)
            DrawTileGrid(g, tileWidth, tileHeight);
        }
        else
        {
            // Draw base tile grid (always at tile size, light blue)
            DrawTileGrid(g, TileWidth, TileHeight);
        }

        if (_entityLayout == null)
            return;

        // Entities have fixed size (20 pixels) and do not scale with tile size
        const float entitySize = 20.0f;
        var entityRadius = entitySize / 2.0f;
        const float selectionOutlineSize = entitySize + 4.0f;

        // Sort entities by render depth (Z + Y position) for proper draw order
        var sortedEntities = _entityLayout.Entities
            .OrderBy(e => e.Position.Y + e.Position.Z)
            .ToList();

        // Draw entities
        foreach (var entity in sortedEntities)
        {
            // Use actual tile map dimensions to match tile display
            var screenPos = WorldToScreen(new PointF(entity.Position.X, entity.Position.Y));
            
            // Apply visual Z offset: higher Z moves entity up on screen
            screenPos.Y -= entity.Position.Z * PixelsPerZUnit;
            
            var color = GetEntityColor(entity.Type);
            var isSelected = entity == _selectedEntity;

            // Only check collision when dragging (not just for display)
            var hasCollision = _isDragging && entity == _selectedEntity && 
                               CheckPlacementCollision(entity, new PointF(entity.Position.X, entity.Position.Y));

            // Draw placement bounds if enabled (3D isometric shaded box)
            if (_showBounds)
            {
                Draw3DBoundingBox(g, entity, new PointF(entity.Position.X, entity.Position.Y), hasCollision, isSelected);
            }

            // Try to draw entity sprite if available
            bool spriteDrawn = false;
            if (!string.IsNullOrEmpty(entity.SpritePath))
            {
                spriteDrawn = DrawEntitySprite(g, screenPos, entity.SpritePath, isSelected);
            }
            
            // Fall back to colored circle if no sprite
            if (!spriteDrawn)
            {
                var drawColor = color;
                using (var brush = new SolidBrush(drawColor))
                {
                    g.FillEllipse(brush, screenPos.X - entityRadius, screenPos.Y - entityRadius, entitySize, entitySize);
                }
            }

            if (isSelected)
            {
                // Draw selection outline around sprite or circle
                var outlineSize = spriteDrawn ? 40.0f : selectionOutlineSize;
                using (var pen = new Pen(Color.Yellow, 2))
                {
                    g.DrawRectangle(pen, screenPos.X - outlineSize / 2, screenPos.Y - outlineSize, outlineSize, outlineSize);
                }
            }

            // Draw entity name and Z level indicator
            using (var font = new Font("Arial", 8.0f))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var text = entity.Name;
                // Add Z level indicator if Z > 0
                if (entity.Position.Z > 0.01f)
                {
                    text += $" (Z:{entity.Position.Z:F1})";
                }
                var textSize = g.MeasureString(text, font);
                var textY = spriteDrawn ? screenPos.Y - 48 : screenPos.Y - 30;
                g.DrawString(text, font, textBrush, screenPos.X - textSize.Width / 2, textY);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_entityLayout == null)
            return;

        if (e.Button == MouseButtons.Left)
        {
            // Use actual tile map dimensions to match tile display
            var worldPos = ScreenToWorld(e.Location);
            var clickedEntity = FindEntityAt(worldPos);

            if (clickedEntity != null)
            {
                SelectEntity(clickedEntity);
                _isDragging = true;
                _lastMousePos = e.Location;
                // Initialize last entity position to current position for change tracking
                if (_selectedEntity != null)
                {
                    _lastEntityWorldPos = new PointF(_selectedEntity.Position.X, _selectedEntity.Position.Y);
                }
            }
        }
        else if (e.Button == MouseButtons.Middle)
        {
            // Middle mouse button - start dragging (works everywhere, including over entities)
            _isDragging = true;
            _lastMousePos = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging && e.Button == MouseButtons.Left && _selectedEntity != null)
        {
            // Use actual tile map dimensions to match tile display
            var worldPos = ScreenToWorld(e.Location);
            // Snap to grid if enabled
            if (_snapToGrid)
            {
                worldPos = SnapToGridPosition(worldPos);
            }
            
            // Only update if position changed significantly to avoid redundant invalidations
            var dx = Math.Abs(worldPos.X - _lastEntityWorldPos.X);
            var dy = Math.Abs(worldPos.Y - _lastEntityWorldPos.Y);
            
            if (dx > PositionChangeThreshold || dy > PositionChangeThreshold)
            {
                // Preserve Z when updating position
                _selectedEntity.Position = new PositionData { X = worldPos.X, Y = worldPos.Y, Z = _selectedEntity.Position.Z };
                _lastEntityWorldPos = worldPos;
                Invalidate();
                // Don't fire EntityMoved event during dragging - it will be fired on mouse up
            }
        }
        else if (_isDragging)
        {
            // Middle mouse button dragging - pan the map
            var delta = new PointF(e.Location.X - _lastMousePos.X, e.Location.Y - _lastMousePos.Y);
            _panOffset = new PointF(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastMousePos = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        
        // Fire EntityMoved event after drag completes (optimization: only once at the end)
        if (_isDragging && e.Button == MouseButtons.Left && _selectedEntity != null)
        {
            EntityMoved?.Invoke(this, new EntityMovedEventArgs(_selectedEntity));
        }
        
        _isDragging = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        
        var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        Zoom = _zoom * zoomFactor;
        Invalidate();
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates using isometric projection.
    /// 
    /// IMPORTANT: This formula must match exactly with IsometricTileMap.WorldToScreen()
    /// to ensure coordinate accuracy between editor and game.
    /// 
    /// Formula:
    ///   screenX = (worldX - worldY) * (tileWidth / 2)
    ///   screenY = (worldX + worldY) * (tileHeight / 2)
    /// </summary>
    private PointF WorldToScreen(PointF worldPos)
    {
        // Use tile map dimensions if available, otherwise use defaults
        int tileWidth = _tileMap?.TileWidth ?? TileWidth;
        int tileHeight = _tileMap?.TileHeight ?? TileHeight;
        
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

    /// <summary>
    /// Converts screen coordinates to world coordinates using isometric projection.
    /// 
    /// IMPORTANT: The core formula (after accounting for pan/zoom) must match exactly 
    /// with IsometricTileMap.ScreenToWorld() to ensure coordinate accuracy between editor and game.
    /// 
    /// Formula (after pan/zoom adjustment):
    ///   worldX = (adjustedX / (tileWidth/2) + adjustedY / (tileHeight/2)) / 2
    ///   worldY = (adjustedY / (tileHeight/2) - adjustedX / (tileWidth/2)) / 2
    /// </summary>
    private PointF ScreenToWorld(Point mousePos)
    {
        var adjustedX = (mousePos.X - _panOffset.X) / _zoom;
        var adjustedY = (mousePos.Y - _panOffset.Y) / _zoom;

        // Use tile map dimensions if available, otherwise use defaults
        int tileWidth = _tileMap?.TileWidth ?? TileWidth;
        int tileHeight = _tileMap?.TileHeight ?? TileHeight;

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
        var adjustedX = (mousePos.X - _panOffset.X) / _zoom;
        var adjustedY = (mousePos.Y - _panOffset.Y) / _zoom;

        // Fixed tile size for entity positioning (does not scale with actual tile size)
        const int FixedTileWidth = 64;
        const int FixedTileHeight = 32;

        var worldX = (adjustedX / (FixedTileWidth / 2.0f) + adjustedY / (FixedTileHeight / 2.0f)) / 2.0f;
        var worldY = (adjustedY / (FixedTileHeight / 2.0f) - adjustedX / (FixedTileWidth / 2.0f)) / 2.0f;
        return new PointF(worldX, worldY);
    }

    private PointF[] GetIsometricPolygon(PointF center)
    {
        // Use tile map dimensions if available, otherwise use defaults
        int tileWidth = _tileMap?.TileWidth ?? TileWidth;
        int tileHeight = _tileMap?.TileHeight ?? TileHeight;
        
        return GetIsometricPolygon(center, tileWidth, tileHeight);
    }

    private PointF[] GetIsometricPolygon(PointF center, float width, float height)
    {
        return new PointF[]
        {
            new PointF(center.X, center.Y - height / 2),
            new PointF(center.X + width / 2, center.Y),
            new PointF(center.X, center.Y + height / 2),
            new PointF(center.X - width / 2, center.Y)
        };
    }

    /// <summary>
    /// Snaps a world position to the grid.
    /// </summary>
    private PointF SnapToGridPosition(PointF worldPos)
    {
        if (!_snapToGrid)
            return worldPos;

        var snappedX = Math.Round(worldPos.X / _gridSize) * _gridSize;
        var snappedY = Math.Round(worldPos.Y / _gridSize) * _gridSize;
        return new PointF((float)snappedX, (float)snappedY);
    }

    /// <summary>
    /// Checks if two isometric bounds overlap in world space (XY plane).
    /// </summary>
    private bool IsometricBoundsOverlap(
        float x1, float y1, float w1, float h1,
        float x2, float y2, float w2, float h2)
    {
        // In isometric world space, bounds are axis-aligned rectangles
        // Check for overlap using standard AABB collision
        var halfW1 = w1 / 2f;
        var halfH1 = h1 / 2f;
        var halfW2 = w2 / 2f;
        var halfH2 = h2 / 2f;

        var left1 = x1 - halfW1;
        var right1 = x1 + halfW1;
        var top1 = y1 - halfH1;
        var bottom1 = y1 + halfH1;

        var left2 = x2 - halfW2;
        var right2 = x2 + halfW2;
        var top2 = y2 - halfH2;
        var bottom2 = y2 + halfH2;

        return left1 < right2 && right1 > left2 && top1 < bottom2 && bottom1 > top2;
    }

    /// <summary>
    /// Checks if an entity would collide with any other entity at the given position.
    /// Uses world-space isometric collision checking.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="position">The position to check (X, Y in world coordinates).</param>
    /// <returns>True if placement would overlap with another entity.</returns>
    public bool CheckPlacementCollision(EntityData entity, PointF position)
    {
        if (_entityLayout == null)
            return false;

        var (ex, ey, ew, eh) = GetEntityPlacementBoundsWorld(entity, position);
        var entityZ = entity.Position.Z;
        var entityZHeight = entity.PlacementBounds?.ZHeight ?? 1f;

        foreach (var other in _entityLayout.Entities)
        {
            if (other == entity)
                continue;

            var (ox, oy, ow, oh) = GetEntityPlacementBoundsWorld(other, new PointF(other.Position.X, other.Position.Y));
            var otherZ = other.Position.Z;
            var otherZHeight = other.PlacementBounds?.ZHeight ?? 1f;

            // Check XY overlap in world space
            if (!IsometricBoundsOverlap(ex, ey, ew, eh, ox, oy, ow, oh))
                continue;

            // Check Z overlap
            var entityBottom = entityZ;
            var entityTop = entityZ + entityZHeight;
            var otherBottom = otherZ;
            var otherTop = otherZ + otherZHeight;

            if (entityBottom < otherTop && entityTop > otherBottom)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the list of entities that would overlap with the given entity at the specified position.
    /// </summary>
    public List<EntityData> GetOverlappingEntities(EntityData entity, PointF position)
    {
        var overlapping = new List<EntityData>();
        if (_entityLayout == null)
            return overlapping;

        var (ex, ey, ew, eh) = GetEntityPlacementBoundsWorld(entity, position);
        var entityZ = entity.Position.Z;
        var entityZHeight = entity.PlacementBounds?.ZHeight ?? 1f;

        foreach (var other in _entityLayout.Entities)
        {
            if (other == entity)
                continue;

            var (ox, oy, ow, oh) = GetEntityPlacementBoundsWorld(other, new PointF(other.Position.X, other.Position.Y));
            var otherZ = other.Position.Z;
            var otherZHeight = other.PlacementBounds?.ZHeight ?? 1f;

            // Check XY overlap in world space
            if (!IsometricBoundsOverlap(ex, ey, ew, eh, ox, oy, ow, oh))
                continue;

            // Check Z overlap
            var entityBottom = entityZ;
            var entityTop = entityZ + entityZHeight;
            var otherBottom = otherZ;
            var otherTop = otherZ + otherZHeight;

            if (entityBottom < otherTop && entityTop > otherBottom)
                overlapping.Add(other);
        }

        return overlapping;
    }

    /// <summary>
    /// Gets the placement bounds in world coordinates for an entity.
    /// Returns (worldX, worldY, width, height) where width/height are in world units.
    /// </summary>
    private (float worldX, float worldY, float width, float height) GetEntityPlacementBoundsWorld(EntityData entity, PointF worldPos)
    {
        // Default bounds size (in world units - 1.0 = one tile)
        float width = 0.5f;
        float height = 0.5f;
        float offsetX = 0;
        float offsetY = 0;

        if (entity.PlacementBounds != null)
        {
            width = entity.PlacementBounds.Width;
            height = entity.PlacementBounds.Height;
            offsetX = entity.PlacementBounds.OffsetX;
            offsetY = entity.PlacementBounds.OffsetY;
        }

        return (worldPos.X + offsetX, worldPos.Y + offsetY, width, height);
    }

    /// <summary>
    /// Gets the placement bounds rectangle for an entity at a given position (in screen coordinates).
    /// Used for simple rectangular overlap checking.
    /// </summary>
    private RectangleF GetEntityPlacementBounds(EntityData entity, PointF worldPos)
    {
        var (wx, wy, ww, wh) = GetEntityPlacementBoundsWorld(entity, worldPos);
        
        // Convert world bounds to screen bounds
        // Get the four corners in world space and convert to screen
        var tileWidth = _tileMap?.TileWidth ?? TileWidth;
        var tileHeight = _tileMap?.TileHeight ?? TileHeight;
        
        // Convert world size to approximate screen size
        var screenWidth = ww * tileWidth;
        var screenHeight = wh * tileHeight;
        
        var screenPos = WorldToScreen(new PointF(wx, wy));
        
        return new RectangleF(
            screenPos.X - screenWidth / 2,
            screenPos.Y - screenHeight / 2,
            screenWidth,
            screenHeight);
    }

    /// <summary>
    /// Gets the isometric polygon for placement bounds visualization.
    /// </summary>
    private PointF[] GetIsometricBoundsPolygon(EntityData entity, PointF worldPos, float zOffset)
    {
        var (wx, wy, ww, wh) = GetEntityPlacementBoundsWorld(entity, worldPos);
        
        // Calculate the four corners in world space
        var halfW = ww / 2f;
        var halfH = wh / 2f;
        
        // Four corners in world coordinates (diamond shape in world space)
        var topWorld = new PointF(wx, wy - halfH);      // Top (north)
        var rightWorld = new PointF(wx + halfW, wy);    // Right (east)
        var bottomWorld = new PointF(wx, wy + halfH);   // Bottom (south)
        var leftWorld = new PointF(wx - halfW, wy);     // Left (west)
        
        // Convert to screen coordinates
        var topScreen = WorldToScreen(topWorld);
        var rightScreen = WorldToScreen(rightWorld);
        var bottomScreen = WorldToScreen(bottomWorld);
        var leftScreen = WorldToScreen(leftWorld);
        
        // Apply Z offset to all points
        topScreen.Y -= zOffset;
        rightScreen.Y -= zOffset;
        bottomScreen.Y -= zOffset;
        leftScreen.Y -= zOffset;
        
        return new PointF[] { topScreen, rightScreen, bottomScreen, leftScreen };
    }

    /// <summary>
    /// Draws a 3D shaded isometric bounding box for an entity.
    /// Uses proper isometric projection matching the Properties Editor preview.
    /// </summary>
    private void Draw3DBoundingBox(Graphics g, EntityData entity, PointF worldPos, bool hasCollision, bool isSelected)
    {
        // Get entity screen position as center point
        var screenCenter = WorldToScreen(worldPos);
        
        // Apply entity Z offset (entity elevation)
        float entityZOffset = entity.Position.Z * PixelsPerZUnit * _zoom;
        screenCenter.Y -= entityZOffset;
        
        // Get bounding box dimensions in pixels from PlacementBounds
        // W = width, H = height (vertical), D = depth
        float wPx = entity.PlacementBounds?.Width ?? 32f;
        float hPx = entity.PlacementBounds?.ZHeight ?? 64f;  // ZHeight is the vertical height
        float dPx = entity.PlacementBounds?.Height ?? 32f;   // Height in PlacementBounds is actually depth
        
        // Scale for display
        float scale = _zoom * 0.5f;  // Adjust scale factor as needed
        
        // Isometric projection (2:1 ratio)
        // X axis goes right-down, Z axis goes left-down, Y axis goes straight up
        float isoXx = 0.5f * scale;   // X component of width direction
        float isoXy = 0.25f * scale;  // Y component of width direction
        float isoZx = -0.5f * scale;  // X component of depth direction
        float isoZy = 0.25f * scale;  // Y component of depth direction
        
        // Calculate the 8 corners of the 3D box
        // Bottom face (y = 0)
        var b0 = new PointF(screenCenter.X, screenCenter.Y); // Origin (back corner)
        var b1 = new PointF(screenCenter.X + wPx * isoXx, screenCenter.Y + wPx * isoXy); // +X (right)
        var b2 = new PointF(screenCenter.X + wPx * isoXx + dPx * isoZx, screenCenter.Y + wPx * isoXy + dPx * isoZy); // +X +Z (front)
        var b3 = new PointF(screenCenter.X + dPx * isoZx, screenCenter.Y + dPx * isoZy); // +Z (left)

        // Top face (y = height)
        float yOffset = -hPx * scale;
        var t0 = new PointF(b0.X, b0.Y + yOffset);
        var t1 = new PointF(b1.X, b1.Y + yOffset);
        var t2 = new PointF(b2.X, b2.Y + yOffset);
        var t3 = new PointF(b3.X, b3.Y + yOffset);
        
        // Choose colors based on state
        int baseR, baseG, baseB;
        if (hasCollision)
        {
            baseR = 200; baseG = 50; baseB = 50;   // Red for collision
        }
        else if (isSelected)
        {
            baseR = 50; baseG = 100; baseB = 200;  // Blue for selected
        }
        else
        {
            baseR = 0; baseG = 180; baseB = 180;   // Cyan for normal
        }
        
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
        var edgeColor = hasCollision ? Color.Red : (isSelected ? Color.Blue : Color.Cyan);
        using (var pen = new Pen(Color.FromArgb(200, edgeColor), 1.5f))
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
    }

    /// <summary>
    /// Draws the placement grid overlay based on grid size.
    /// </summary>
    /// <summary>
    /// Calculates the visible tile range for grid drawing. Returns the min/max tile coordinates
    /// (integer boundaries) that cover the visible area plus padding.
    /// </summary>
    private (int minTileX, int maxTileX, int minTileY, int maxTileY) GetVisibleTileRange()
    {
        // Calculate visible world bounds by converting screen corners to world coordinates
        var topLeft = ScreenToWorld(new Point(0, 0));
        var topRight = ScreenToWorld(new Point(Width, 0));
        var bottomLeft = ScreenToWorld(new Point(0, Height));
        var bottomRight = ScreenToWorld(new Point(Width, Height));
        
        // Find the min/max world coordinates to determine grid range
        var minWorldX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var maxWorldX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var minWorldY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var maxWorldY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        
        // Add padding to extend grid beyond visible area
        var padding = 5.0f;
        
        // Always align to tile boundaries (integer coordinates)
        var minTileX = (int)Math.Floor(minWorldX - padding);
        var maxTileX = (int)Math.Ceiling(maxWorldX + padding);
        var minTileY = (int)Math.Floor(minWorldY - padding);
        var maxTileY = (int)Math.Ceiling(maxWorldY + padding);
        
        return (minTileX, maxTileX, minTileY, maxTileY);
    }

    /// <summary>
    /// Draws the base tile grid that is always present at tile size (1x1) in light blue.
    /// This grid extends beyond the map boundaries to cover the visible area.
    /// </summary>
    private void DrawTileGrid(Graphics g, int tileWidth, int tileHeight)
    {
        // Get the visible tile range - shared with entity placement grid for perfect alignment
        var (minTileX, maxTileX, minTileY, maxTileY) = GetVisibleTileRange();
        
        // Draw base tile grid - always at tile size (1x1), light blue
        using (var pen = new Pen(Color.LightBlue, 1.0f))
        {
            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    // Draw grid at tile boundaries (integer coordinates)
                    var screenPos = WorldToScreen(new PointF(tileX, tileY));
                    var polygon = GetIsometricPolygon(screenPos, tileWidth, tileHeight);
                    g.DrawPolygon(pen, polygon);
                }
            }
        }
    }

    private EntityData? FindEntityAt(PointF worldPos)
    {
        if (_entityLayout == null)
            return null;

        const float radius = 0.5f; // Search radius in world coordinates

        foreach (var entity in _entityLayout.Entities)
        {
            var dx = entity.Position.X - worldPos.X;
            var dy = entity.Position.Y - worldPos.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= radius)
            {
                return entity;
            }
        }

        return null;
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
    private bool DrawEntitySprite(Graphics g, PointF screenPos, string spritePath, bool isSelected)
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
                var spriteWidth = cachedImage.Width;
                var spriteHeight = cachedImage.Height;
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

    private Color GetTileColor(int tileIndex)
    {
        // Match the colors from TileImageGenerator
        return tileIndex switch
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

    /// <summary>
    /// Gets the bounding rectangle for a tile image at the specified screen position.
    /// </summary>
    private RectangleF GetIsometricTileBounds(PointF center, int tileWidth, int tileHeight)
    {
        return new RectangleF(
            center.X - tileWidth / 2,
            center.Y - tileHeight / 2,
            tileWidth,
            tileHeight
        );
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
            // Get custom graphic path if available (same as TileMapCanvas)
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
                graphicPath); // Pass custom graphic path
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeTileImages();
        }
        base.Dispose(disposing);
    }

    private void SelectEntity(EntityData entity)
    {
        _selectedEntity = entity;
        Invalidate();
        EntitySelected?.Invoke(this, new EntitySelectedEventArgs(entity));
    }

    /// <summary>
    /// Centers the view on all entities, or on the first entity if only one exists.
    /// </summary>
    public void CenterOnEntities()
    {
        if (_entityLayout == null || Width <= 0 || Height <= 0 || !IsHandleCreated)
            return;

        if (_entityLayout.Entities.Count == 0)
            return;

        PointF targetWorld;

        if (_entityLayout.Entities.Count == 1)
        {
            // Center on the single entity
            targetWorld = new PointF(_entityLayout.Entities[0].Position.X, _entityLayout.Entities[0].Position.Y);
        }
        else
        {
            // Calculate center of all entities
            float sumX = 0, sumY = 0;
            foreach (var entity in _entityLayout.Entities)
            {
                sumX += entity.Position.X;
                sumY += entity.Position.Y;
            }
            targetWorld = new PointF(sumX / _entityLayout.Entities.Count, sumY / _entityLayout.Entities.Count);
        }

        // Convert to screen coordinates using actual tile map dimensions
        var targetScreen = WorldToScreen(targetWorld);
        
        // Calculate center of viewport
        var viewportCenterX = Width / 2.0f;
        var viewportCenterY = Height / 2.0f;
        
        // Adjust pan offset to center the target
        _panOffset = new PointF(
            viewportCenterX - targetScreen.X * _zoom,
            viewportCenterY - targetScreen.Y * _zoom
        );
        
        Invalidate();
    }

    /// <summary>
    /// Centers the view on a specific entity.
    /// </summary>
    public void CenterOnEntity(EntityData entity)
    {
        if (entity == null || Width <= 0 || Height <= 0 || !IsHandleCreated)
            return;

        var targetWorld = new PointF(entity.Position.X, entity.Position.Y);

        // Convert to screen coordinates using actual tile map dimensions
        var targetScreen = WorldToScreen(targetWorld);
        
        // Calculate center of viewport
        var viewportCenterX = Width / 2.0f;
        var viewportCenterY = Height / 2.0f;
        
        // Adjust pan offset to center the target
        _panOffset = new PointF(
            viewportCenterX - targetScreen.X * _zoom,
            viewportCenterY - targetScreen.Y * _zoom
        );
        
        Invalidate();
    }
}

/// <summary>
/// Event arguments for entity selection.
/// </summary>
public class EntitySelectedEventArgs : EventArgs
{
    public EntityData Entity { get; }

    public EntitySelectedEventArgs(EntityData entity)
    {
        Entity = entity;
    }
}

/// <summary>
/// Event arguments for entity movement.
/// </summary>
public class EntityMovedEventArgs : EventArgs
{
    public EntityData Entity { get; }

    public EntityMovedEventArgs(EntityData entity)
    {
        Entity = entity;
    }
}

