using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Represents a single particle in the particle system.
/// </summary>
public class Particle
{
    // Position and movement
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; set; }

    // Visual properties
    public Color Color { get; set; }
    public Color StartColor { get; set; }
    public Color EndColor { get; set; }
    public float Scale { get; set; }
    public float StartScale { get; set; }
    public float EndScale { get; set; }
    public float Rotation { get; set; }
    public float RotationSpeed { get; set; }

    // Lifetime
    public float Lifetime { get; set; }
    public float MaxLifetime { get; set; }
    
    // Fade multiplier for death effects (0.0 = fully faded, 1.0 = normal)
    public float FadeMultiplier { get; set; } = 1.0f;

    // Texture/sprite properties
    public Rectangle? SourceRectangle { get; set; }

    /// <summary>
    /// Gets the normalized lifetime (0.0 to 1.0).
    /// </summary>
    public float NormalizedLifetime => MaxLifetime > 0 ? Lifetime / MaxLifetime : 1.0f;

    /// <summary>
    /// Gets whether the particle has expired.
    /// </summary>
    public bool IsExpired => Lifetime >= MaxLifetime;

    /// <summary>
    /// Gets the current interpolated color based on lifetime, with fade multiplier applied.
    /// </summary>
    public Color CurrentColor
    {
        get
        {
            var t = NormalizedLifetime;
            var color = Color.Lerp(StartColor, EndColor, t);
            // Apply fade multiplier to alpha
            return new Color(color.R, color.G, color.B, (byte)(color.A * FadeMultiplier));
        }
    }

    /// <summary>
    /// Gets the current interpolated scale based on lifetime.
    /// </summary>
    public float CurrentScale
    {
        get
        {
            var t = NormalizedLifetime;
            return StartScale + (EndScale - StartScale) * t;
        }
    }

    /// <summary>
    /// Resets the particle to default state for object pooling.
    /// </summary>
    public void Reset()
    {
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        Color = Color.White;
        StartColor = Color.White;
        EndColor = Color.White;
        Scale = 1.0f;
        StartScale = 1.0f;
        EndScale = 1.0f;
        Rotation = 0.0f;
        RotationSpeed = 0.0f;
        Lifetime = 0.0f;
        MaxLifetime = 1.0f;
        FadeMultiplier = 1.0f;
        SourceRectangle = null;
    }

    /// <summary>
    /// Updates the particle's state.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Update velocity based on acceleration
        Velocity += Acceleration * deltaTime;

        // Update position based on velocity
        Position += Velocity * deltaTime;

        // Update rotation
        Rotation += RotationSpeed * deltaTime;

        // Update lifetime
        Lifetime += deltaTime;
    }
}

