using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GameEditor.Utilities;

/// <summary>
/// Utility class for generating tile images that match the game's procedural tileset.
/// </summary>
public static class TileImageGenerator
{
    /// <summary>
    /// Generates a bitmap image for a single tile.
    /// </summary>
    /// <param name="tileIndex">The index of the tile (0-7).</param>
    /// <param name="tileWidth">Width of the tile in pixels.</param>
    /// <param name="tileHeight">Height of the tile in pixels.</param>
    /// <param name="previewSize">Size to scale the preview to (for palette display).</param>
    /// <param name="customGraphicPath">Optional path to a custom graphic file for this tile.</param>
    /// <returns>A bitmap image of the tile.</returns>
    public static Bitmap GenerateTileImage(int tileIndex, int tileWidth, int tileHeight, Size previewSize, string? customGraphicPath = null)
    {
        Bitmap? bitmap = null;

        // Try to load custom graphic if provided
        if (!string.IsNullOrEmpty(customGraphicPath))
        {
            try
            {
                string fullPath = customGraphicPath;
                // If path is relative, try to resolve it relative to GameContent
                if (!Path.IsPathRooted(fullPath))
                {
                    var gameContentPath = PathHelper.GetGameContentPath();
                    if (gameContentPath != null)
                    {
                        fullPath = Path.Combine(gameContentPath, fullPath);
                    }
                }

                if (File.Exists(fullPath))
                {
                    // CRITICAL: Scale custom graphic to exact tile dimensions to match game rendering
                    // The game loads textures at original size and scales via destinationRect,
                    // but we pre-scale here using high-quality interpolation for better quality.
                    // Both approaches ensure tiles appear at tileWidth x tileHeight.
                    var originalImage = Image.FromFile(fullPath);
                    bitmap = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.Clear(Color.Transparent);
                        g.DrawImage(originalImage, 0, 0, tileWidth, tileHeight);
                    }
                    originalImage.Dispose();
                }
            }
            catch
            {
                // Fall through to default generation if image loading fails
            }
        }

        // Generate default procedural tile if no custom graphic or loading failed
        if (bitmap == null)
        {
            // CRITICAL: Tile colors must match exactly with GameClient/Rendering/TileMapRenderer.cs
            // If you change these colors, you must update the game's CreateProceduralTileset() method
            // to ensure visual consistency between editor and game.
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
            bitmap = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Create isometric diamond shape
                var centerX = tileWidth / 2.0f;
                var centerY = tileHeight / 2.0f;

                // Draw diamond polygon
                var points = new PointF[]
                {
                    new PointF(centerX, 0),                    // Top
                    new PointF(tileWidth, centerY),            // Right
                    new PointF(centerX, tileHeight),          // Bottom
                    new PointF(0, centerY)                     // Left
                };

                using (var brush = new SolidBrush(baseColor))
                {
                    g.FillPolygon(brush, points);
                }

                // Add some variation for depth (similar to game's procedural generation)
                // Note: We already filled the polygon, so we'll overlay the variation
                for (int y = 0; y < tileHeight; y++)
                {
                    for (int x = 0; x < tileWidth; x++)
                    {
                        var dx = Math.Abs(x - centerX);
                        var dy = Math.Abs(y - centerY);

                        // Check if pixel is within diamond shape
                        if (dx / centerX + dy / centerY <= 1.0f)
                        {
                            // Add variation for depth (variation is in 0-1 range, applied to byte values)
                            var variation = (float)(Math.Sin(x * 0.3f) * Math.Cos(y * 0.3f) * 0.1f);
                            var r = (byte)Math.Clamp(baseColor.R + (int)(variation * 255), 0, 255);
                            var gColor = (byte)Math.Clamp(baseColor.G + (int)(variation * 255), 0, 255);
                            var b = (byte)Math.Clamp(baseColor.B + (int)(variation * 255), 0, 255);

                            bitmap.SetPixel(x, y, Color.FromArgb(r, gColor, b));
                        }
                    }
                }
            }
        }

        // Scale to preview size if needed
        if (previewSize.Width != tileWidth || previewSize.Height != tileHeight)
        {
            var scaledBitmap = new Bitmap(previewSize.Width, previewSize.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaledBitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, previewSize.Width, previewSize.Height);
            }
            bitmap.Dispose();
            return scaledBitmap;
        }

        return bitmap;
    }

    /// <summary>
    /// Generates all tile images for the palette.
    /// </summary>
    /// <param name="tileWidth">Width of each tile in pixels.</param>
    /// <param name="tileHeight">Height of each tile in pixels.</param>
    /// <param name="previewSize">Size to scale previews to.</param>
    /// <param name="numTiles">Number of tiles to generate (default 8).</param>
    /// <returns>Array of tile bitmaps.</returns>
    public static Bitmap[] GenerateAllTileImages(int tileWidth, int tileHeight, Size previewSize, int numTiles = 8)
    {
        var images = new Bitmap[numTiles];
        for (int i = 0; i < numTiles; i++)
        {
            images[i] = GenerateTileImage(i, tileWidth, tileHeight, previewSize);
        }
        return images;
    }
}

