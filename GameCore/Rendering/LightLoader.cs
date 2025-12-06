using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace GameCore.Rendering;

/// <summary>
/// Helper class for loading lights from JSON files.
/// </summary>
public static class LightLoader
{
    /// <summary>
    /// Serializable representation of a light layout for JSON (matches editor format).
    /// </summary>
    private class LightLayoutDataJson
    {
        public List<LightDataJson>? Lights { get; set; }
    }

    /// <summary>
    /// Serializable representation of a single light for JSON (matches editor format).
    /// </summary>
    private class LightDataJson
    {
        public string? Name { get; set; }
        public PositionDataJson? Position { get; set; }
        public ColorDataJson? Color { get; set; }
        public float Radius { get; set; }
        public float Intensity { get; set; }
    }

    private class PositionDataJson
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    private class ColorDataJson
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }
    }

    /// <summary>
    /// Loads lights from a JSON file and converts them to Light objects.
    /// Lights are stored in world coordinates and converted to screen coordinates using the tile map.
    /// </summary>
    /// <param name="filePath">Path to the lights JSON file.</param>
    /// <param name="tileMap">Tile map used for coordinate conversion from world to screen coordinates.</param>
    /// <returns>List of Light objects, or empty list if file doesn't exist or loading fails.</returns>
    public static List<Light> LoadFromJson(string filePath, IsometricTileMap? tileMap)
    {
        var lights = new List<Light>();

        if (!File.Exists(filePath))
            return lights;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<LightLayoutDataJson>(json);
            if (data?.Lights == null)
                return lights;

            foreach (var lightData in data.Lights)
            {
                if (lightData.Position == null || lightData.Color == null)
                    continue;

                // Convert world coordinates to screen coordinates
                Vector2 screenPosition;
                if (tileMap != null)
                {
                    var worldPos = new Vector2(lightData.Position.X, lightData.Position.Y);
                    screenPosition = tileMap.WorldToScreen(worldPos);
                }
                else
                {
                    // Fallback: use world coordinates directly if no tile map
                    screenPosition = new Vector2(lightData.Position.X, lightData.Position.Y);
                }

                // Convert ColorDataJson to XNA Color
                var color = new Color(
                    lightData.Color.R,
                    lightData.Color.G,
                    lightData.Color.B,
                    lightData.Color.A);

                var light = new Light(
                    screenPosition,
                    color,
                    lightData.Radius,
                    lightData.Intensity);

                lights.Add(light);
            }
        }
        catch
        {
            // Return empty list on error
        }

        return lights;
    }
}

