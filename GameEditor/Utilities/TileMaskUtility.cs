using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GameEditor.Utilities;

/// <summary>
/// Utility for creating and applying diamond masks to tile images.
/// </summary>
public static class TileMaskUtility
{
    /// <summary>
    /// Creates a diamond mask for isometric tiles.
    /// </summary>
    public static Bitmap CreateDiamondMask(int width, int height)
    {
        var mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        
        var centerX = width / 2.0f;
        var centerY = height / 2.0f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var dx = Math.Abs(x - centerX);
                var dy = Math.Abs(y - centerY);
                
                // Diamond shape: |dx|/centerX + |dy|/centerY <= 1
                if (dx / centerX + dy / centerY <= 1.0f)
                {
                    mask.SetPixel(x, y, Color.White); // Inside diamond
                }
                else
                {
                    mask.SetPixel(x, y, Color.Transparent); // Outside diamond
                }
            }
        }
        
        return mask;
    }
    
    /// <summary>
    /// Extracts a diamond mask from an existing tile image.
    /// </summary>
    public static Bitmap? ExtractMaskFromTile(string tilePath, int width, int height)
    {
        try
        {
            if (!File.Exists(tilePath))
                return null;
                
            using var tileImage = new Bitmap(tilePath);
            
            // Create mask based on non-transparent pixels
            var mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            
            // Scale tile image if needed
            using var scaledTile = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaledTile))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(tileImage, 0, 0, width, height);
            }
            
            // Extract mask from alpha channel
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = scaledTile.GetPixel(x, y);
                    if (pixel.A > 128) // Non-transparent
                    {
                        mask.SetPixel(x, y, Color.White);
                    }
                    else
                    {
                        mask.SetPixel(x, y, Color.Transparent);
                    }
                }
            }
            
            return mask;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Gets a mask from existing tiles in the directory, or creates a new one.
    /// </summary>
    public static Bitmap GetOrCreateMask(int width, int height)
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null)
        {
            return CreateDiamondMask(width, height);
        }
        
        var tilesDir = Path.Combine(gameContentPath, "tiles");
        if (!Directory.Exists(tilesDir))
        {
            return CreateDiamondMask(width, height);
        }
        
        // Try to find an existing tile to extract mask from
        var tileFiles = Directory.GetFiles(tilesDir, "*.png");
        foreach (var tileFile in tileFiles)
        {
            var mask = ExtractMaskFromTile(tileFile, width, height);
            if (mask != null)
            {
                return mask;
            }
        }
        
        // Fallback to creating a new mask
        return CreateDiamondMask(width, height);
    }
    
    /// <summary>
    /// Applies a mask to an image, making pixels outside the mask transparent.
    /// </summary>
    public static Bitmap ApplyMask(Bitmap image, Bitmap mask)
    {
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            // Resize mask to match image
            var resizedMask = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(resizedMask))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(mask, 0, 0, image.Width, image.Height);
            }
            mask.Dispose();
            mask = resizedMask;
        }
        
        var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var imagePixel = image.GetPixel(x, y);
                var maskPixel = mask.GetPixel(x, y);
                
                // If mask is white/opaque, keep the pixel; otherwise make transparent
                if (maskPixel.A > 128)
                {
                    result.SetPixel(x, y, imagePixel);
                }
                else
                {
                    result.SetPixel(x, y, Color.Transparent);
                }
            }
        }
        
        return result;
    }
}





