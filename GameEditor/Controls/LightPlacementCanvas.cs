using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GameEditor.Data;
using GameEditor.Utilities;
using System.Linq;

namespace GameEditor.Controls;

/// <summary>
/// Custom control for placing lights on a map.
/// </summary>
public class LightPlacementCanvas : Control
{
    private LightLayoutData? _lightLayout;
    private TileMapData? _tileMap;
    private LightData? _selectedLight;
    private float _zoom = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private bool _isDragging = false;
    private PointF _lastMousePos = PointF.Empty;
    private PointF _lastLightWorldPos = PointF.Empty;
    private float _gridSize = 0.25f;
    private bool _snapToGrid = true;
    private const float PositionChangeThreshold = 0.01f;
    private const int TileWidth = 64;
    private const int TileHeight = 32;
    private Bitmap[]? _tileImages;

    public LightLayoutData? LightLayout
    {
        get => _lightLayout;
        set
        {
            _lightLayout = value;
            Invalidate();
            
            if (_lightLayout != null && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    CenterOnLights();
                }));
            }
        }
    }

    public TileMapData? TileMap
    {
        get => _tileMap;
        set
        {
            DisposeTileImages();
            _tileMap = value;
            
            if (_tileMap != null)
            {
                RegenerateTileImages();
            }
            
            Invalidate();
        }
    }

    public LightData? SelectedLight
    {
        get => _selectedLight;
        set
        {
            _selectedLight = value;
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

    public float GridSize
    {
        get => _gridSize;
        set
        {
            _gridSize = Math.Max(0.0625f, Math.Min(4.0f, value));
            Invalidate();
        }
    }

    public bool SnapToGrid
    {
        get => _snapToGrid;
        set
        {
            _snapToGrid = value;
            Invalidate();
        }
    }

    public event EventHandler<LightSelectedEventArgs>? LightSelected;
    public event EventHandler<LightMovedEventArgs>? LightMoved;

    public LightPlacementCanvas()
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

        // Draw tile map as background
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
                    
                    if (_tileImages != null && tileIndex >= 0 && tileIndex < _tileImages.Length && _tileImages[tileIndex] != null)
                    {
                        var tileImage = _tileImages[tileIndex];
                        var imageRect = GetIsometricTileBounds(screenPos, tileWidth, tileHeight);
                        g.DrawImage(tileImage, imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height);
                    }
                    else
                    {
                        var tileColor = GetTileColor(tileIndex);
                        using (var brush = new SolidBrush(tileColor))
                        {
                            g.FillPolygon(brush, GetIsometricPolygon(screenPos));
                        }
                    }
                }
            }

            if (_tileMap.PlayerStartPosition != null)
            {
                var playerStartWorld = new PointF(_tileMap.PlayerStartPosition.X, _tileMap.PlayerStartPosition.Y);
                var playerStartScreen = WorldToScreen(playerStartWorld);
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
            
            DrawTileGrid(g, tileWidth, tileHeight);
        }
        else
        {
            DrawTileGrid(g, TileWidth, TileHeight);
        }

        if (_lightLayout == null)
            return;

        // Draw lights
        foreach (var light in _lightLayout.Lights)
        {
            var screenPos = WorldToScreen(new PointF(light.Position.X, light.Position.Y));
            var isSelected = light == _selectedLight;
            
            // Convert ColorData to System.Drawing.Color
            var lightColor = Color.FromArgb(light.Color.A, light.Color.R, light.Color.G, light.Color.B);
            
            // Calculate radius in screen coordinates
            // Radius in JSON is stored in screen pixels (matching game's Light class)
            // Use it directly without conversion
            var radiusScreen = light.Radius;
            
            // Draw light circle with semi-transparent fill
            var alpha = (int)(light.Intensity * 255);
            var fillColor = Color.FromArgb(alpha, lightColor);
            using (var brush = new SolidBrush(fillColor))
            {
                g.FillEllipse(brush, screenPos.X - radiusScreen, screenPos.Y - radiusScreen, radiusScreen * 2, radiusScreen * 2);
            }
            
            // Draw radius outline
            var outlineColor = isSelected ? Color.Yellow : Color.FromArgb(180, lightColor);
            using (var pen = new Pen(outlineColor, isSelected ? 3.0f : 1.5f))
            {
                g.DrawEllipse(pen, screenPos.X - radiusScreen, screenPos.Y - radiusScreen, radiusScreen * 2, radiusScreen * 2);
            }
            
            // Draw center point
            const float centerSize = 4.0f;
            using (var brush = new SolidBrush(lightColor))
            {
                g.FillEllipse(brush, screenPos.X - centerSize / 2, screenPos.Y - centerSize / 2, centerSize, centerSize);
            }
            
            // Draw light name if available
            if (!string.IsNullOrEmpty(light.Name))
            {
                using (var font = new Font("Arial", 8.0f))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    var text = light.Name;
                    var textSize = g.MeasureString(text, font);
                    g.DrawString(text, font, textBrush, screenPos.X - textSize.Width / 2, screenPos.Y - radiusScreen - 20);
                }
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_lightLayout == null)
            return;

        if (e.Button == MouseButtons.Left)
        {
            var worldPos = ScreenToWorld(e.Location);
            var clickedLight = FindLightAt(worldPos);

            if (clickedLight != null)
            {
                SelectLight(clickedLight);
                _isDragging = true;
                _lastMousePos = e.Location;
                if (_selectedLight != null)
                {
                    _lastLightWorldPos = new PointF(_selectedLight.Position.X, _selectedLight.Position.Y);
                }
            }
        }
        else if (e.Button == MouseButtons.Middle)
        {
            _isDragging = true;
            _lastMousePos = e.Location;
        }
        else if (e.Button == MouseButtons.Right)
        {
            var worldPos = ScreenToWorld(e.Location);
            var clickedLight = FindLightAt(worldPos);
            if (clickedLight != null)
            {
                SelectLight(clickedLight);
                // Context menu could be added here for delete/duplicate
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging && e.Button == MouseButtons.Left && _selectedLight != null)
        {
            var worldPos = ScreenToWorld(e.Location);
            if (_snapToGrid)
            {
                worldPos = SnapToGridPosition(worldPos);
            }
            
            var dx = Math.Abs(worldPos.X - _lastLightWorldPos.X);
            var dy = Math.Abs(worldPos.Y - _lastLightWorldPos.Y);
            
            if (dx > PositionChangeThreshold || dy > PositionChangeThreshold)
            {
                _selectedLight.Position = new PositionData { X = worldPos.X, Y = worldPos.Y };
                _lastLightWorldPos = worldPos;
                Invalidate();
            }
        }
        else if (_isDragging)
        {
            var delta = new PointF(e.Location.X - _lastMousePos.X, e.Location.Y - _lastMousePos.Y);
            _panOffset = new PointF(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastMousePos = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        
        if (_isDragging && e.Button == MouseButtons.Left && _selectedLight != null)
        {
            LightMoved?.Invoke(this, new LightMovedEventArgs(_selectedLight));
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

    private PointF WorldToScreen(PointF worldPos)
    {
        int tileWidth = _tileMap?.TileWidth ?? TileWidth;
        int tileHeight = _tileMap?.TileHeight ?? TileHeight;
        
        var screenX = (worldPos.X - worldPos.Y) * (tileWidth / 2.0f);
        var screenY = (worldPos.X + worldPos.Y) * (tileHeight / 2.0f);
        return new PointF(screenX, screenY);
    }

    private PointF ScreenToWorld(Point mousePos)
    {
        var adjustedX = (mousePos.X - _panOffset.X) / _zoom;
        var adjustedY = (mousePos.Y - _panOffset.Y) / _zoom;

        int tileWidth = _tileMap?.TileWidth ?? TileWidth;
        int tileHeight = _tileMap?.TileHeight ?? TileHeight;

        var worldX = (adjustedX / (tileWidth / 2.0f) + adjustedY / (tileHeight / 2.0f)) / 2.0f;
        var worldY = (adjustedY / (tileHeight / 2.0f) - adjustedX / (tileWidth / 2.0f)) / 2.0f;
        return new PointF(worldX, worldY);
    }

    private PointF[] GetIsometricPolygon(PointF center)
    {
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

    private PointF SnapToGridPosition(PointF worldPos)
    {
        if (!_snapToGrid)
            return worldPos;

        var snappedX = Math.Round(worldPos.X / _gridSize) * _gridSize;
        var snappedY = Math.Round(worldPos.Y / _gridSize) * _gridSize;
        return new PointF((float)snappedX, (float)snappedY);
    }

    private (int minTileX, int maxTileX, int minTileY, int maxTileY) GetVisibleTileRange()
    {
        var topLeft = ScreenToWorld(new Point(0, 0));
        var topRight = ScreenToWorld(new Point(Width, 0));
        var bottomLeft = ScreenToWorld(new Point(0, Height));
        var bottomRight = ScreenToWorld(new Point(Width, Height));
        
        var minWorldX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var maxWorldX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var minWorldY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var maxWorldY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        
        var padding = 5.0f;
        
        var minTileX = (int)Math.Floor(minWorldX - padding);
        var maxTileX = (int)Math.Ceiling(maxWorldX + padding);
        var minTileY = (int)Math.Floor(minWorldY - padding);
        var maxTileY = (int)Math.Ceiling(maxWorldY + padding);
        
        return (minTileX, maxTileX, minTileY, maxTileY);
    }

    private void DrawTileGrid(Graphics g, int tileWidth, int tileHeight)
    {
        var (minTileX, maxTileX, minTileY, maxTileY) = GetVisibleTileRange();
        
        using (var pen = new Pen(Color.LightBlue, 1.0f))
        {
            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    var screenPos = WorldToScreen(new PointF(tileX, tileY));
                    var polygon = GetIsometricPolygon(screenPos, tileWidth, tileHeight);
                    g.DrawPolygon(pen, polygon);
                }
            }
        }
    }

    private LightData? FindLightAt(PointF worldPos)
    {
        if (_lightLayout == null)
            return null;

        const float radius = 0.5f;

        foreach (var light in _lightLayout.Lights)
        {
            var dx = light.Position.X - worldPos.X;
            var dy = light.Position.Y - worldPos.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= radius)
            {
                return light;
            }
        }

        return null;
    }

    private Color GetTileColor(int tileIndex)
    {
        return tileIndex switch
        {
            0 => Color.FromArgb(100, 150, 100),
            1 => Color.FromArgb(80, 120, 80),
            2 => Color.FromArgb(150, 120, 100),
            3 => Color.FromArgb(120, 100, 80),
            4 => Color.FromArgb(100, 100, 150),
            5 => Color.FromArgb(80, 80, 120),
            6 => Color.FromArgb(120, 120, 120),
            7 => Color.FromArgb(100, 100, 100),
            _ => Color.Gray
        };
    }

    private RectangleF GetIsometricTileBounds(PointF center, int tileWidth, int tileHeight)
    {
        return new RectangleF(
            center.X - tileWidth / 2,
            center.Y - tileHeight / 2,
            tileWidth,
            tileHeight
        );
    }

    private void RegenerateTileImages()
    {
        DisposeTileImages();

        if (_tileMap == null)
            return;

        int numTiles = 8;
        if (_tileMap.TileGraphics != null && _tileMap.TileGraphics.Count > 0)
        {
            var maxIndex = _tileMap.TileGraphics.Keys.Max();
            numTiles = Math.Max(numTiles, maxIndex + 1);
        }
        
        _tileImages = new Bitmap[numTiles];

        for (int i = 0; i < numTiles; i++)
        {
            string? graphicPath = null;
            if (_tileMap.TileGraphics != null && _tileMap.TileGraphics.TryGetValue(i, out var path))
            {
                graphicPath = path;
            }

            _tileImages[i] = TileImageGenerator.GenerateTileImage(
                i, 
                _tileMap.TileWidth, 
                _tileMap.TileHeight, 
                new Size(_tileMap.TileWidth, _tileMap.TileHeight),
                graphicPath);
        }
    }

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

    private void SelectLight(LightData light)
    {
        _selectedLight = light;
        Invalidate();
        LightSelected?.Invoke(this, new LightSelectedEventArgs(light));
    }

    public void CenterOnLights()
    {
        if (_lightLayout == null || Width <= 0 || Height <= 0 || !IsHandleCreated)
            return;

        if (_lightLayout.Lights.Count == 0)
            return;

        PointF targetWorld;

        if (_lightLayout.Lights.Count == 1)
        {
            targetWorld = new PointF(_lightLayout.Lights[0].Position.X, _lightLayout.Lights[0].Position.Y);
        }
        else
        {
            float sumX = 0, sumY = 0;
            foreach (var light in _lightLayout.Lights)
            {
                sumX += light.Position.X;
                sumY += light.Position.Y;
            }
            targetWorld = new PointF(sumX / _lightLayout.Lights.Count, sumY / _lightLayout.Lights.Count);
        }

        var targetScreen = WorldToScreen(targetWorld);
        
        var viewportCenterX = Width / 2.0f;
        var viewportCenterY = Height / 2.0f;
        
        _panOffset = new PointF(
            viewportCenterX - targetScreen.X * _zoom,
            viewportCenterY - targetScreen.Y * _zoom
        );
        
        Invalidate();
    }
}

/// <summary>
/// Event arguments for light selection.
/// </summary>
public class LightSelectedEventArgs : EventArgs
{
    public LightData Light { get; }

    public LightSelectedEventArgs(LightData light)
    {
        Light = light;
    }
}

/// <summary>
/// Event arguments for light movement.
/// </summary>
public class LightMovedEventArgs : EventArgs
{
    public LightData Light { get; }

    public LightMovedEventArgs(LightData light)
    {
        Light = light;
    }
}

