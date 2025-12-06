using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using GameEditor.Services;

namespace GameEditor.Utilities;

/// <summary>
/// Utility class for exporting entity icons to PNG files.
/// </summary>
public static class EntityExporter
{
    /// <summary>
    /// Exports all entity types to PNG files in the GameContent/entities directory.
    /// Uses the same visual representation as the editor.
    /// </summary>
    /// <param name="iconSize">Size of each entity icon in pixels (default 64x64).</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    public static bool ExportEntitiesToPng(int iconSize = 64)
    {
        try
        {
            // Get or create the entities directory
            var entitiesDir = Path.Combine("GameContent", "entities");
            if (!Directory.Exists(entitiesDir))
            {
                Directory.CreateDirectory(entitiesDir);
            }

            // Define entity types to export
            var entityTypes = new[] { "NPC", "Enemy", "GroundItem", "Interactable" };

            // Export each entity type
            foreach (var entityType in entityTypes)
            {
                var entityBitmap = GenerateEntityIcon(entityType, iconSize);
                
                // Save as PNG
                var fileName = $"entity_{entityType.ToLower()}.png";
                var filePath = Path.Combine(entitiesDir, fileName);
                
                // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
                PerforceService.EnsureFileReadyForEdit(filePath);
                
                entityBitmap.Save(filePath, ImageFormat.Png);
                
                // Add file to Perforce if it's new
                PerforceService.AddFile(filePath);
                
                entityBitmap.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error exporting entities: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates an entity icon matching the editor's visual representation.
    /// </summary>
    private static Bitmap GenerateEntityIcon(string entityType, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Get entity color (matching EntityPlacementCanvas.GetEntityColor)
            var entityColor = GetEntityColor(entityType);
            
            // Draw entity circle (matching editor's 20x20 circle, scaled to icon size)
            var circleSize = size * 0.6f; // Make circle 60% of icon size
            var circleX = (size - circleSize) / 2.0f;
            var circleY = (size - circleSize) / 2.0f;
            
            using (var brush = new SolidBrush(entityColor))
            {
                g.FillEllipse(brush, circleX, circleY, circleSize, circleSize);
            }
            
            // Draw white border (matching editor)
            using (var pen = new Pen(Color.White, 2))
            {
                g.DrawEllipse(pen, circleX, circleY, circleSize, circleSize);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Gets the color for an entity type (matching EntityPlacementCanvas).
    /// </summary>
    private static Color GetEntityColor(string entityType)
    {
        return entityType switch
        {
            "NPC" => Color.Blue,
            "Enemy" => Color.Red,
            "GroundItem" => Color.Gold,
            "Interactable" => Color.Green,
            _ => Color.White
        };
    }
}


