using Microsoft.Xna.Framework;

namespace GameCore.Rendering;

/// <summary>
/// Represents a point light in the 2D lighting system.
/// </summary>
public class Light
{
    /// <summary>
    /// Gets or sets the position of the light in world coordinates.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Gets or sets the color of the light.
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// Gets or sets the radius of the light in world units.
    /// </summary>
    public float Radius { get; set; }

    /// <summary>
    /// Gets or sets the intensity of the light (0.0 to 1.0).
    /// </summary>
    public float Intensity { get; set; } = 1.0f;

    /// <summary>
    /// Initializes a new instance of the Light class.
    /// </summary>
    public Light()
    {
        Position = Vector2.Zero;
        Color = Color.White;
        Radius = 100.0f;
        Intensity = 1.0f;
    }

    /// <summary>
    /// Initializes a new instance of the Light class with specified properties.
    /// </summary>
    public Light(Vector2 position, Color color, float radius, float intensity = 1.0f)
    {
        Position = position;
        Color = color;
        Radius = radius;
        Intensity = intensity;
    }
}



