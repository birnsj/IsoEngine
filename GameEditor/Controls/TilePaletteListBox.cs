using System.Drawing;
using GameEditor.Data;
using GameEditor.Utilities;

namespace GameEditor.Controls;

/// <summary>
/// Custom ListBox that displays tile images instead of text.
/// </summary>
public class TilePaletteListBox : ListBox
{
    private Bitmap[]? _tileImages;
    private Size _tilePreviewSize = new Size(48, 24); // Preview size for palette
    private int _tileWidth = 64;
    private int _tileHeight = 32;
    private int _numTiles = 8;
    private TileMapData? _tileMap;

    /// <summary>
    /// Gets or sets the tile width used for generating previews.
    /// </summary>
    public int TileWidth
    {
        get => _tileWidth;
        set
        {
            if (_tileWidth != value)
            {
                _tileWidth = value;
                RegenerateTileImages();
            }
        }
    }

    /// <summary>
    /// Gets or sets the tile height used for generating previews.
    /// </summary>
    public int TileHeight
    {
        get => _tileHeight;
        set
        {
            if (_tileHeight != value)
            {
                _tileHeight = value;
                RegenerateTileImages();
            }
        }
    }

    /// <summary>
    /// Gets or sets the preview size for tiles in the palette.
    /// </summary>
    public Size TilePreviewSize
    {
        get => _tilePreviewSize;
        set
        {
            if (_tilePreviewSize != value)
            {
                _tilePreviewSize = value;
                RegenerateTileImages();
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of tiles to display.
    /// </summary>
    public int NumTiles
    {
        get => _numTiles;
        set
        {
            if (_numTiles != value)
            {
                _numTiles = value;
                RegenerateTileImages();
            }
        }
    }

    /// <summary>
    /// Gets or sets the tile map data to use for custom tile graphics.
    /// </summary>
    public TileMapData? TileMap
    {
        get => _tileMap;
        set
        {
            if (_tileMap != value)
            {
                _tileMap = value;
                RegenerateTileImages();
            }
        }
    }

    /// <summary>
    /// Event raised when a tile is requested to be edited (right-click).
    /// </summary>
    public event EventHandler<TileEditEventArgs>? TileEditRequested;

    public TilePaletteListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = Math.Max(_tilePreviewSize.Height + 16, 50); // Better padding, minimum 50px
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.FromArgb(240, 240, 240); // Light gray background
        ScrollAlwaysVisible = true; // Always show scrollbar when needed
        IntegralHeight = false; // Allow partial items to be shown, enables proper scrolling
        
        // Add context menu for right-click
        var contextMenu = new ContextMenuStrip();
        var editMenuItem = new ToolStripMenuItem("Edit Tile...");
        editMenuItem.Click += (s, e) => OnEditTileRequested();
        contextMenu.Items.Add(editMenuItem);
        ContextMenuStrip = contextMenu;
        
        RegenerateTileImages();
    }
    
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        // Ensure scrollbar is properly updated after layout
        UpdateScrollbar();
    }
    
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Ensure scrollbar updates when control is resized
        UpdateScrollbar();
    }
    
    private void UpdateScrollbar()
    {
        if (Items.Count > 0 && Height > 0)
        {
            // Calculate if scrollbar is needed
            var totalHeight = Items.Count * ItemHeight;
            var needsScrollbar = totalHeight > Height;
            
            if (needsScrollbar)
            {
                ScrollAlwaysVisible = true;
            }
            
            // Force refresh to update scrollbar
            Refresh();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        // Handle right-click to show context menu and trigger edit
        if (e.Button == MouseButtons.Right)
        {
            var index = IndexFromPoint(e.Location);
            if (index >= 0 && index < Items.Count)
            {
                SelectedIndex = index;
                OnEditTileRequested();
            }
        }
    }

    private void OnEditTileRequested()
    {
        if (SelectedIndex >= 0)
        {
            TileEditRequested?.Invoke(this, new TileEditEventArgs(SelectedIndex));
        }
    }

    private void RegenerateTileImages()
    {
        // Dispose old images
        if (_tileImages != null)
        {
            foreach (var img in _tileImages)
            {
                img?.Dispose();
            }
        }

        // Generate new images with custom graphics from map if available
        _tileImages = new Bitmap[_numTiles];
        for (int i = 0; i < _numTiles; i++)
        {
            // Get custom graphic path from map if available
            string? graphicPath = null;
            if (_tileMap?.TileGraphics != null && _tileMap.TileGraphics.TryGetValue(i, out var path))
            {
                graphicPath = path;
            }

            _tileImages[i] = TileImageGenerator.GenerateTileImage(
                i,
                _tileWidth,
                _tileHeight,
                _tilePreviewSize,
                graphicPath);
        }

        // Update items
        var previousSelectedIndex = SelectedIndex;
        Items.Clear();
        for (int i = 0; i < _numTiles; i++)
        {
            Items.Add($"Tile {i}");
        }
        
        // Restore selection if valid
        if (previousSelectedIndex >= 0 && previousSelectedIndex < Items.Count)
        {
            SelectedIndex = previousSelectedIndex;
        }
        else if (Items.Count > 0)
        {
            SelectedIndex = 0;
        }

        // Force update of scrollbar
        UpdateScrollbar();
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);

        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        // Draw background with better styling
        var bgColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected 
            ? Color.FromArgb(51, 153, 255) // Bright blue for selection
            : (e.Index % 2 == 0 ? Color.White : Color.FromArgb(250, 250, 250)); // Alternating rows
        
        using (var bgBrush = new SolidBrush(bgColor))
        {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        if (_tileImages == null || e.Index >= _tileImages.Length)
            return;

        var tileImage = _tileImages[e.Index];
        if (tileImage == null)
            return;

        // Calculate position for tile image (centered vertically)
        var imageY = e.Bounds.Y + (e.Bounds.Height - _tilePreviewSize.Height) / 2;
        var imageX = e.Bounds.X + 8; // More padding
        var imageRect = new Rectangle(imageX, imageY, _tilePreviewSize.Width, _tilePreviewSize.Height);

        // Draw border around tile image
        using (var borderPen = new Pen(Color.Gray, 1))
        {
            e.Graphics.DrawRectangle(borderPen, imageRect);
        }

        // Draw tile image
        e.Graphics.DrawImage(tileImage, imageRect);

        // Draw tile index and info text with better formatting
        if (e.Font != null)
        {
            var textX = imageRect.Right + 12;
            var textY = e.Bounds.Y + 6;
            
            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var textColor = isSelected ? Color.White : Color.Black;
            
            using (var titleFont = new Font(e.Font.FontFamily, e.Font.Size, FontStyle.Bold))
            using (var textBrush = new SolidBrush(textColor))
            {
                // Draw tile number
                e.Graphics.DrawString($"Tile {e.Index}", titleFont, textBrush, textX, textY);
                
                // Draw tile size info below
                var infoY = textY + titleFont.Height + 2;
                using (var infoFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f))
                using (var infoBrush = new SolidBrush(isSelected ? Color.LightGray : Color.Gray))
                {
                    e.Graphics.DrawString($"{_tileWidth}x{_tileHeight}px", infoFont, infoBrush, textX, infoY);
                }
            }
        }

        // Draw selection indicator (more prominent)
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
        {
            using (var selectionPen = new Pen(Color.DarkBlue, 3))
            {
                e.Graphics.DrawRectangle(selectionPen, e.Bounds);
            }
        }
        else
        {
            // Draw subtle separator line
            using (var separatorPen = new Pen(Color.LightGray, 1))
            {
                e.Graphics.DrawLine(separatorPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _tileImages != null)
        {
            foreach (var img in _tileImages)
            {
                img?.Dispose();
            }
            _tileImages = null;
        }
        base.Dispose(disposing);
    }
}

