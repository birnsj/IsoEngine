using System.Drawing;
using System.Drawing.Imaging;
using GameEditor.Utilities;
using GameEditor.Services;

namespace GameEditor.Utilities;

/// <summary>
/// Utility class for exporting tiles to PNG files.
/// </summary>
public static class TileExporter
{
    /// <summary>
    /// Exports all tiles to PNG files in the GameContent/tiles directory.
    /// Uses the same generation logic as the game and editor.
    /// </summary>
    /// <param name="tileWidth">Width of each tile in pixels (default 64).</param>
    /// <param name="tileHeight">Height of each tile in pixels (default 32).</param>
    /// <param name="numTiles">Number of tiles to export (default 8).</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    public static bool ExportTilesToPng(int tileWidth = 64, int tileHeight = 32, int numTiles = 8)
    {
        try
        {
            // Get or create the tiles directory
            var tilesDir = Path.Combine("GameContent", "tiles");
            if (!Directory.Exists(tilesDir))
            {
                Directory.CreateDirectory(tilesDir);
            }

            // Export each tile
            for (int tileIndex = 0; tileIndex < numTiles; tileIndex++)
            {
                // Generate tile image using the same logic as the game/editor
                var tileBitmap = GenerateTileImageFullSize(tileIndex, tileWidth, tileHeight);
                
                // Save as PNG
                var fileName = $"tile_{tileIndex:D2}.png";
                var filePath = Path.Combine(tilesDir, fileName);
                
                // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
                PerforceService.EnsureFileReadyForEdit(filePath);
                
                tileBitmap.Save(filePath, ImageFormat.Png);
                
                // Add file to Perforce if it's new
                PerforceService.AddFile(filePath);
                
                tileBitmap.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error exporting tiles: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates a full-size tile image matching the game's procedural generation exactly.
    /// </summary>
    private static Bitmap GenerateTileImageFullSize(int tileIndex, int tileWidth, int tileHeight)
    {
        // Define tile colors (matching GameClient/Rendering/TileMapRenderer.cs exactly)
        var tileColors = new[]
        {
            Color.FromArgb(100, 150, 100), // 0: Grass (light green)
            Color.FromArgb(80, 120, 80),   // 1: Dark grass
            Color.FromArgb(150, 120, 100), // 2: Dirt (brown)
            Color.FromArgb(120, 100, 80), // 3: Dark dirt
            Color.FromArgb(100, 100, 150), // 4: Water (blue)
            Color.FromArgb(80, 80, 120),   // 5: Deep water
            Color.FromArgb(120, 120, 120), // 6: Stone (gray)
            Color.FromArgb(100, 100, 100), // 7: Dark stone
        };

        var baseColor = tileColors[tileIndex % tileColors.Length];

        // Create bitmap for the tile
        var bitmap = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);
        
        // Fill with transparent first
        for (int y = 0; y < tileHeight; y++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                bitmap.SetPixel(x, y, Color.Transparent);
            }
        }

        // Create isometric diamond shape (matching game's logic exactly)
        var centerX = tileWidth / 2.0f;
        var centerY = tileHeight / 2.0f;

        for (int y = 0; y < tileHeight; y++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                var dx = Math.Abs(x - centerX);
                var dy = Math.Abs(y - centerY);

                // Diamond shape: |dx|/w + |dy|/h <= 1 (matching game's logic)
                if (dx / centerX + dy / centerY <= 1.0f)
                {
                    // Add variation for depth (matching game's procedural generation)
                    var variation = (float)(Math.Sin(x * 0.3f) * Math.Cos(y * 0.3f) * 0.1f);
                    var r = (byte)Math.Clamp(baseColor.R + variation * 255, 0, 255);
                    var g = (byte)Math.Clamp(baseColor.G + variation * 255, 0, 255);
                    var b = (byte)Math.Clamp(baseColor.B + variation * 255, 0, 255);

                    bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }
        }

        return bitmap;
    }
}


