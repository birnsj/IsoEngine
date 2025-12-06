using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using GameEditor.Data;
using GameEditor.Utilities;

namespace GameEditor.Controls;

/// <summary>
/// Custom ListBox that displays entity icons instead of just text.
/// </summary>
public class EntityListBox : ListBox
{
    private const int IconSize = 32;
    private const int IconPadding = 4;
    private readonly Dictionary<string, Image?> _spriteCache = new();

    public EntityListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = IconSize + IconPadding * 2;
        BorderStyle = BorderStyle.FixedSingle;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearSpriteCache();
        }
        base.Dispose(disposing);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);

        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        e.DrawBackground();

        var item = Items[e.Index];
        EntityData? entity = null;

        // Try to get EntityData from the item
        if (item is EntityData entityData)
        {
            entity = entityData;
        }

        // Draw entity icon
        var iconRect = new Rectangle(
            e.Bounds.X + IconPadding,
            e.Bounds.Y + IconPadding,
            IconSize,
            IconSize);

        var itemText = item?.ToString() ?? "";
        var entityType = entity?.Type ?? GetEntityTypeFromText(itemText);
        var entityColor = GetEntityColor(entityType);
        
        // Try to draw sprite if entity has one
        bool spriteDrawn = false;
        if (entity != null && !string.IsNullOrEmpty(entity.SpritePath))
        {
            spriteDrawn = DrawEntitySprite(e.Graphics, iconRect, entity.SpritePath);
        }
        
        // Fall back to generic icon if no sprite
        if (!spriteDrawn)
        {
            DrawEntityIcon(e.Graphics, iconRect, entityType, entityColor);
        }

        // Draw entity text
        var textX = iconRect.Right + IconPadding;
        var textY = e.Bounds.Y + (e.Font != null ? (e.Bounds.Height - e.Font.Height) / 2 : 0);
        var textRect = new Rectangle(textX, textY, e.Bounds.Width - textX - IconPadding, e.Bounds.Height);

        if (e.Font != null)
        {
            using (var brush = new SolidBrush(e.ForeColor))
            {
                var displayText = entity != null 
                    ? $"{entity.Type}: {entity.Name} ({entity.Position.X:F1}, {entity.Position.Y:F1})"
                    : item?.ToString() ?? "";
                e.Graphics.DrawString(displayText, e.Font, brush, textRect);
            }
        }

        // Draw selection indicator
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
        {
            using (var pen = new Pen(Color.Yellow, 2))
            {
                e.Graphics.DrawRectangle(pen, e.Bounds);
            }
        }
    }

    private bool DrawEntitySprite(Graphics g, Rectangle rect, string spritePath)
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
                    cachedImage = new Bitmap(tempImage, IconSize, IconSize);
                }
                else
                {
                    cachedImage = null;
                }
                
                _spriteCache[spritePath] = cachedImage;
            }

            if (cachedImage != null)
            {
                g.DrawImage(cachedImage, rect);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading sprite {spritePath}: {ex.Message}");
        }

        return false;
    }

    private void DrawEntityIcon(Graphics g, Rectangle rect, string entityType, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (entityType.ToLower())
        {
            case "npc":
                // Draw as a person icon (circle with triangle)
                using (var brush = new SolidBrush(color))
                {
                    // Body (circle)
                    g.FillEllipse(brush, rect.X + rect.Width / 4, rect.Y + rect.Height / 2, rect.Width / 2, rect.Height / 2);
                    // Head (smaller circle)
                    g.FillEllipse(brush, rect.X + rect.Width / 3, rect.Y, rect.Width / 3, rect.Height / 3);
                }
                break;

            case "enemy":
                // Draw as a diamond/shield shape
                var points = new PointF[]
                {
                    new PointF(rect.X + rect.Width / 2, rect.Y), // Top
                    new PointF(rect.X + rect.Width, rect.Y + rect.Height / 2), // Right
                    new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height), // Bottom
                    new PointF(rect.X, rect.Y + rect.Height / 2) // Left
                };
                using (var brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, points);
                }
                // Add a small X or cross for enemy
                using (var pen = new Pen(Color.DarkRed, 2))
                {
                    g.DrawLine(pen, rect.X + rect.Width / 4, rect.Y + rect.Height / 4, 
                                     rect.X + rect.Width * 3 / 4, rect.Y + rect.Height * 3 / 4);
                    g.DrawLine(pen, rect.X + rect.Width * 3 / 4, rect.Y + rect.Height / 4, 
                                     rect.X + rect.Width / 4, rect.Y + rect.Height * 3 / 4);
                }
                break;

            case "grounditem":
                // Draw as a box/chest
                using (var brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, rect.X + rect.Width / 4, rect.Y + rect.Height / 3, 
                                          rect.Width / 2, rect.Height / 2);
                }
                // Add a lid
                using (var pen = new Pen(color, 2))
                {
                    g.DrawLine(pen, rect.X + rect.Width / 4, rect.Y + rect.Height / 3,
                                     rect.X + rect.Width / 2, rect.Y + rect.Height / 4);
                    g.DrawLine(pen, rect.X + rect.Width / 2, rect.Y + rect.Height / 4,
                                     rect.X + rect.Width * 3 / 4, rect.Y + rect.Height / 3);
                }
                break;

            case "interactable":
                // Draw as a gear/cog
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, rect.X + rect.Width / 4, rect.Y + rect.Height / 4, 
                                        rect.Width / 2, rect.Height / 2);
                }
                // Add gear teeth
                using (var pen = new Pen(color, 2))
                {
                    // Top
                    g.DrawLine(pen, rect.X + rect.Width / 2, rect.Y, 
                                     rect.X + rect.Width / 2, rect.Y + rect.Height / 4);
                    // Bottom
                    g.DrawLine(pen, rect.X + rect.Width / 2, rect.Y + rect.Height * 3 / 4, 
                                     rect.X + rect.Width / 2, rect.Y + rect.Height);
                    // Left
                    g.DrawLine(pen, rect.X, rect.Y + rect.Height / 2, 
                                     rect.X + rect.Width / 4, rect.Y + rect.Height / 2);
                    // Right
                    g.DrawLine(pen, rect.X + rect.Width * 3 / 4, rect.Y + rect.Height / 2, 
                                     rect.X + rect.Width, rect.Y + rect.Height / 2);
                }
                break;

            default:
                // Default: simple circle
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, rect);
                }
                break;
        }
    }

    private Color GetEntityColor(string entityType)
    {
        return entityType.ToLower() switch
        {
            "npc" => Color.Cyan,
            "enemy" => Color.Orange,
            "grounditem" => Color.Gold,
            "interactable" => Color.LightBlue,
            _ => Color.Yellow
        };
    }

    private string GetEntityTypeFromText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var parts = text.Split(':');
        if (parts.Length > 0)
        {
            return parts[0].Trim();
        }
        return "";
    }
}

