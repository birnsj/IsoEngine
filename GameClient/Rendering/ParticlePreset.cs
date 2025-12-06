using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Provides predefined particle effect configurations for common VFX.
/// </summary>
public static class ParticlePreset
{
    /// <summary>
    /// Creates a hit sparks effect (small, fast, orange/yellow particles).
    /// </summary>
    public static ParticleEmitter HitSparks(Vector2 position, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Point,
            Mode = EmissionMode.Burst,
            Position = position,
            ParticlesPerBurst = (int)(15 * intensity),
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f,
            VelocityMin = new Vector2(-150 * intensity, -150 * intensity),
            VelocityMax = new Vector2(150 * intensity, 150 * intensity),
            Acceleration = new Vector2(0, 200), // Gravity
            StartColorMin = new Color(255, 200, 100),
            StartColorMax = new Color(255, 150, 50),
            EndColorMin = new Color(255, 100, 0, 0),
            EndColorMax = new Color(255, 150, 50, 0),
            ScaleMin = new Vector2(0.3f, 0.3f),
            ScaleMax = new Vector2(0.6f, 0.6f),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.1f, 0.1f),
            LifetimeRange = new Vector2(0.2f, 0.4f),
            RotationSpeedRange = new Vector2(-5.0f, 5.0f)
        };
    }

    /// <summary>
    /// Creates an explosion effect (radial burst, multiple sizes).
    /// </summary>
    public static ParticleEmitter Explosion(Vector2 position, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Circle,
            Mode = EmissionMode.Burst,
            Position = position,
            Size = new Vector2(10 * intensity, 10 * intensity),
            ParticlesPerBurst = (int)(50 * intensity),
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f,
            VelocityMin = new Vector2(-200 * intensity, -200 * intensity),
            VelocityMax = new Vector2(200 * intensity, 200 * intensity),
            Acceleration = new Vector2(0, 100), // Slight gravity
            StartColorMin = new Color(255, 200, 100),
            StartColorMax = new Color(255, 100, 0),
            EndColorMin = new Color(100, 50, 0, 0),
            EndColorMax = new Color(150, 75, 0, 0),
            ScaleMin = new Vector2(0.5f * intensity, 0.5f * intensity),
            ScaleMax = new Vector2(1.5f * intensity, 1.5f * intensity),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.2f, 0.2f),
            LifetimeRange = new Vector2(0.5f, 1.0f),
            RotationSpeedRange = new Vector2(-3.0f, 3.0f)
        };
    }

    /// <summary>
    /// Creates a blood splatter effect (red particles with gravity).
    /// </summary>
    public static ParticleEmitter BloodSplatter(Vector2 position, Vector2 direction, float intensity = 1.0f)
    {
        var angle = MathF.Atan2(direction.Y, direction.X);
        return new ParticleEmitter
        {
            Type = EmitterType.Point,
            Mode = EmissionMode.Burst,
            Position = position,
            ParticlesPerBurst = (int)(20 * intensity),
            Direction = angle,
            Spread = MathF.PI * 0.5f, // 90 degree spread
            VelocityMin = new Vector2(50 * intensity, 50 * intensity),
            VelocityMax = new Vector2(200 * intensity, 200 * intensity),
            Acceleration = new Vector2(0, 300), // Strong gravity
            StartColorMin = new Color(150, 0, 0),
            StartColorMax = new Color(200, 0, 0),
            EndColorMin = new Color(100, 0, 0, 0),
            EndColorMax = new Color(150, 0, 0, 0),
            ScaleMin = new Vector2(0.4f, 0.4f),
            ScaleMax = new Vector2(0.8f, 0.8f),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.1f, 0.1f),
            LifetimeRange = new Vector2(0.3f, 0.6f),
            RotationSpeedRange = new Vector2(-2.0f, 2.0f)
        };
    }

    /// <summary>
    /// Creates a dust cloud effect (brown/gray, slow fade).
    /// </summary>
    public static ParticleEmitter DustCloud(Vector2 position, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Circle,
            Mode = EmissionMode.Burst,
            Position = position,
            Size = new Vector2(5 * intensity, 5 * intensity),
            ParticlesPerBurst = (int)(30 * intensity),
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f,
            VelocityMin = new Vector2(-30 * intensity, -30 * intensity),
            VelocityMax = new Vector2(30 * intensity, 30 * intensity),
            Acceleration = new Vector2(0, -20), // Slight upward drift
            StartColorMin = new Color(139, 126, 102),
            StartColorMax = new Color(160, 150, 130),
            EndColorMin = new Color(100, 90, 70, 0),
            EndColorMax = new Color(120, 110, 90, 0),
            ScaleMin = new Vector2(0.8f, 0.8f),
            ScaleMax = new Vector2(1.5f, 1.5f),
            EndScaleMin = new Vector2(1.2f, 1.2f),
            EndScaleMax = new Vector2(2.0f, 2.0f), // Expand as it fades
            LifetimeRange = new Vector2(1.0f, 2.0f),
            RotationSpeedRange = new Vector2(-0.5f, 0.5f)
        };
    }

    /// <summary>
    /// Creates a magic orb effect (glowing, pulsing particles).
    /// </summary>
    public static ParticleEmitter MagicOrb(Vector2 position, Color color, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Circle,
            Mode = EmissionMode.Continuous,
            Position = position,
            Size = new Vector2(5 * intensity, 5 * intensity),
            EmissionRate = 10 * intensity,
            EmissionDuration = 0.0f, // Infinite
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f,
            VelocityMin = new Vector2(-20 * intensity, -20 * intensity),
            VelocityMax = new Vector2(20 * intensity, 20 * intensity),
            Acceleration = new Vector2(0, -10), // Slight upward
            StartColorMin = color,
            StartColorMax = Color.Lerp(color, Color.White, 0.3f),
            EndColorMin = Color.Transparent,
            EndColorMax = Color.Transparent,
            ScaleMin = new Vector2(0.5f * intensity, 0.5f * intensity),
            ScaleMax = new Vector2(1.0f * intensity, 1.0f * intensity),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.0f, 0.0f),
            LifetimeRange = new Vector2(0.5f, 1.0f),
            RotationSpeedRange = new Vector2(-2.0f, 2.0f)
        };
    }

    /// <summary>
    /// Creates an energy trail effect (elongated particles following a path).
    /// </summary>
    public static ParticleEmitter EnergyTrail(Vector2 start, Vector2 end, Color color, float intensity = 1.0f)
    {
        var direction = end - start;
        var length = direction.Length();
        var angle = MathF.Atan2(direction.Y, direction.X);

        return new ParticleEmitter
        {
            Type = EmitterType.Line,
            Mode = EmissionMode.Continuous,
            Position = (start + end) * 0.5f,
            Size = new Vector2(length, 0),
            EmissionRate = 20 * intensity,
            EmissionDuration = 0.5f,
            Direction = angle,
            Spread = 0.1f, // Tight spread along the line
            VelocityMin = new Vector2(50 * intensity, 0),
            VelocityMax = new Vector2(100 * intensity, 0),
            Acceleration = Vector2.Zero,
            StartColorMin = color,
            StartColorMax = Color.Lerp(color, Color.White, 0.5f),
            EndColorMin = Color.Transparent,
            EndColorMax = Color.Transparent,
            ScaleMin = new Vector2(0.3f * intensity, 0.8f * intensity), // Elongated
            ScaleMax = new Vector2(0.5f * intensity, 1.2f * intensity),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.0f, 0.0f),
            LifetimeRange = new Vector2(0.3f, 0.5f),
            RotationSpeedRange = Vector2.Zero
        };
    }

    /// <summary>
    /// Creates a footstep dust effect (small dust puffs).
    /// </summary>
    public static ParticleEmitter FootstepDust(Vector2 position, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Point,
            Mode = EmissionMode.Burst,
            Position = position,
            ParticlesPerBurst = (int)(8 * intensity),
            Direction = MathF.PI * 0.5f, // Upward
            Spread = MathF.PI * 0.3f, // Small upward cone
            VelocityMin = new Vector2(-20 * intensity, -40 * intensity),
            VelocityMax = new Vector2(20 * intensity, -60 * intensity),
            Acceleration = new Vector2(0, 50), // Gravity
            StartColorMin = new Color(139, 126, 102),
            StartColorMax = new Color(160, 150, 130),
            EndColorMin = new Color(100, 90, 70, 0),
            EndColorMax = new Color(120, 110, 90, 0),
            ScaleMin = new Vector2(0.3f, 0.3f),
            ScaleMax = new Vector2(0.6f, 0.6f),
            EndScaleMin = new Vector2(0.5f, 0.5f),
            EndScaleMax = new Vector2(0.8f, 0.8f), // Slight expansion
            LifetimeRange = new Vector2(0.3f, 0.5f),
            RotationSpeedRange = new Vector2(-1.0f, 1.0f)
        };
    }

    /// <summary>
    /// Creates a death effect (fade out with particle burst).
    /// </summary>
    public static ParticleEmitter DeathEffect(Vector2 position, Color entityColor, float intensity = 1.0f)
    {
        return new ParticleEmitter
        {
            Type = EmitterType.Circle,
            Mode = EmissionMode.Burst,
            Position = position,
            Size = new Vector2(5 * intensity, 5 * intensity),
            ParticlesPerBurst = (int)(25 * intensity),
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f,
            VelocityMin = new Vector2(-80 * intensity, -80 * intensity),
            VelocityMax = new Vector2(80 * intensity, 80 * intensity),
            Acceleration = new Vector2(0, 50), // Slight gravity
            StartColorMin = entityColor,
            StartColorMax = Color.Lerp(entityColor, Color.White, 0.3f),
            EndColorMin = Color.Transparent,
            EndColorMax = Color.Transparent,
            ScaleMin = new Vector2(0.4f * intensity, 0.4f * intensity),
            ScaleMax = new Vector2(0.8f * intensity, 0.8f * intensity),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.0f, 0.0f),
            LifetimeRange = new Vector2(0.5f, 1.0f),
            RotationSpeedRange = new Vector2(-2.0f, 2.0f)
        };
    }
}

