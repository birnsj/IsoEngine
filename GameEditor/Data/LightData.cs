using System.Collections.Generic;

namespace GameEditor.Data;

/// <summary>
/// Serializable representation of light placement data.
/// </summary>
public class LightLayoutData
{
    /// <summary>
    /// Gets or sets the list of lights in this layout.
    /// </summary>
    public List<LightData> Lights { get; set; } = new();
}

/// <summary>
/// Serializable representation of a single light.
/// </summary>
public class LightData
{
    /// <summary>
    /// Gets or sets the name of the light (optional, for identification).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the world position of the light (in tile coordinates).
    /// </summary>
    public PositionData Position { get; set; } = new();

    /// <summary>
    /// Gets or sets the color of the light.
    /// </summary>
    public ColorData Color { get; set; } = new();

    /// <summary>
    /// Gets or sets the radius of the light in world units.
    /// </summary>
    public float Radius { get; set; } = 100.0f;

    /// <summary>
    /// Gets or sets the intensity of the light (0.0 to 1.0).
    /// </summary>
    public float Intensity { get; set; } = 1.0f;
}

/// <summary>
/// Serializable representation of a color (RGBA).
/// </summary>
public class ColorData
{
    /// <summary>
    /// Gets or sets the red component (0-255).
    /// </summary>
    public byte R { get; set; } = 255;

    /// <summary>
    /// Gets or sets the green component (0-255).
    /// </summary>
    public byte G { get; set; } = 255;

    /// <summary>
    /// Gets or sets the blue component (0-255).
    /// </summary>
    public byte B { get; set; } = 255;

    /// <summary>
    /// Gets or sets the alpha component (0-255).
    /// </summary>
    public byte A { get; set; } = 255;
}

