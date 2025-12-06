using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Defines how particles are emitted from a source.
/// </summary>
public enum EmitterType
{
    /// <summary>
    /// Emits from a single point.
    /// </summary>
    Point,

    /// <summary>
    /// Emits from a circular area.
    /// </summary>
    Circle,

    /// <summary>
    /// Emits from a rectangular area.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Emits along a line segment.
    /// </summary>
    Line
}

/// <summary>
/// Defines the emission mode for particles.
/// </summary>
public enum EmissionMode
{
    /// <summary>
    /// Emits all particles at once in a burst.
    /// </summary>
    Burst,

    /// <summary>
    /// Continuously emits particles over time.
    /// </summary>
    Continuous
}

/// <summary>
/// Configuration for a particle emitter.
/// </summary>
public class ParticleEmitter
{
    /// <summary>
    /// Gets or sets the emitter type (point, circle, rectangle, line).
    /// </summary>
    public EmitterType Type { get; set; } = EmitterType.Point;

    /// <summary>
    /// Gets or sets the emission mode (burst or continuous).
    /// </summary>
    public EmissionMode Mode { get; set; } = EmissionMode.Burst;

    /// <summary>
    /// Gets or sets the position of the emitter.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Gets or sets the size of the emission area (radius for circle, dimensions for rectangle).
    /// </summary>
    public Vector2 Size { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the direction of emission (in radians, 0 = right, PI/2 = down).
    /// </summary>
    public float Direction { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the spread angle for emission (in radians).
    /// </summary>
    public float Spread { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the number of particles to emit per burst.
    /// </summary>
    public int ParticlesPerBurst { get; set; } = 10;

    /// <summary>
    /// Gets or sets the emission rate for continuous mode (particles per second).
    /// </summary>
    public float EmissionRate { get; set; } = 10.0f;

    /// <summary>
    /// Gets or sets the duration for continuous emission (0 = infinite).
    /// </summary>
    public float EmissionDuration { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the initial velocity range (min, max).
    /// </summary>
    public Vector2 VelocityMin { get; set; } = Vector2.Zero;
    public Vector2 VelocityMax { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the acceleration applied to particles.
    /// </summary>
    public Vector2 Acceleration { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the initial color range.
    /// </summary>
    public Color StartColorMin { get; set; } = Color.White;
    public Color StartColorMax { get; set; } = Color.White;

    /// <summary>
    /// Gets or sets the end color range.
    /// </summary>
    public Color EndColorMin { get; set; } = Color.White;
    public Color EndColorMax { get; set; } = Color.White;

    /// <summary>
    /// Gets or sets the initial scale range.
    /// </summary>
    public Vector2 ScaleMin { get; set; } = new Vector2(1.0f, 1.0f);
    public Vector2 ScaleMax { get; set; } = new Vector2(1.0f, 1.0f);

    /// <summary>
    /// Gets or sets the end scale range.
    /// </summary>
    public Vector2 EndScaleMin { get; set; } = new Vector2(1.0f, 1.0f);
    public Vector2 EndScaleMax { get; set; } = new Vector2(1.0f, 1.0f);

    /// <summary>
    /// Gets or sets the lifetime range (min, max in seconds).
    /// </summary>
    public Vector2 LifetimeRange { get; set; } = new Vector2(1.0f, 1.0f);

    /// <summary>
    /// Gets or sets the rotation speed range (min, max in radians per second).
    /// </summary>
    public Vector2 RotationSpeedRange { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the initial rotation range (min, max in radians).
    /// </summary>
    public Vector2 InitialRotationRange { get; set; } = Vector2.Zero;

    private float _emissionTimer = 0.0f;
    private float _activeDuration = 0.0f;
    private static readonly Random _random = new Random();

    /// <summary>
    /// Resets the emitter's internal state.
    /// </summary>
    public void Reset()
    {
        _emissionTimer = 0.0f;
        _activeDuration = 0.0f;
    }

    /// <summary>
    /// Updates the emitter and returns the number of particles to emit this frame.
    /// </summary>
    public int Update(float deltaTime, out bool isActive)
    {
        isActive = true;

        if (Mode == EmissionMode.Burst)
        {
            // Burst mode: emit all particles immediately, then deactivate
            if (_emissionTimer == 0.0f)
            {
                _emissionTimer = float.MaxValue; // Mark as emitted
                return ParticlesPerBurst;
            }
            isActive = false;
            return 0;
        }
        else // Continuous
        {
            _activeDuration += deltaTime;

            // Check if emission duration has expired
            if (EmissionDuration > 0.0f && _activeDuration >= EmissionDuration)
            {
                isActive = false;
                return 0;
            }

            // Calculate particles to emit this frame
            _emissionTimer += deltaTime;
            var particlesToEmit = (int)(_emissionTimer * EmissionRate);
            _emissionTimer -= particlesToEmit / EmissionRate;

            return particlesToEmit;
        }
    }

    /// <summary>
    /// Generates a random emission position based on the emitter type.
    /// </summary>
    public Vector2 GetRandomEmissionPosition()
    {
        return Type switch
        {
            EmitterType.Point => Position,
            EmitterType.Circle => GetRandomCirclePosition(),
            EmitterType.Rectangle => GetRandomRectanglePosition(),
            EmitterType.Line => GetRandomLinePosition(),
            _ => Position
        };
    }

    /// <summary>
    /// Generates a random velocity based on direction and spread.
    /// </summary>
    public Vector2 GetRandomVelocity()
    {
        // Random angle within spread
        var angle = Direction + (_random.NextSingle() - 0.5f) * Spread;
        
        // Random speed within velocity range
        var speedMin = VelocityMin.Length();
        var speedMax = VelocityMax.Length();
        var speed = speedMin + (speedMax - speedMin) * _random.NextSingle();

        // Calculate velocity vector
        var velocity = new Vector2(
            MathF.Cos(angle) * speed,
            MathF.Sin(angle) * speed
        );

        // Add random variation to X and Y components if specified
        if (VelocityMin.X != VelocityMax.X)
        {
            velocity.X = VelocityMin.X + (VelocityMax.X - VelocityMin.X) * _random.NextSingle();
        }
        if (VelocityMin.Y != VelocityMax.Y)
        {
            velocity.Y = VelocityMin.Y + (VelocityMax.Y - VelocityMin.Y) * _random.NextSingle();
        }

        return velocity;
    }

    private Vector2 GetRandomCirclePosition()
    {
        var angle = _random.NextSingle() * MathF.PI * 2.0f;
        var radius = Size.X * _random.NextSingle();
        return Position + new Vector2(
            MathF.Cos(angle) * radius,
            MathF.Sin(angle) * radius
        );
    }

    private Vector2 GetRandomRectanglePosition()
    {
        return Position + new Vector2(
            (_random.NextSingle() - 0.5f) * Size.X,
            (_random.NextSingle() - 0.5f) * Size.Y
        );
    }

    private Vector2 GetRandomLinePosition()
    {
        var t = _random.NextSingle();
        var halfSize = Size * 0.5f;
        var start = Position - halfSize;
        var end = Position + halfSize;
        return Vector2.Lerp(start, end, t);
    }

    /// <summary>
    /// Gets a random value within the specified range.
    /// </summary>
    private static float RandomRange(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    /// <summary>
    /// Gets a random color within the specified range.
    /// </summary>
    private static Color RandomColor(Color min, Color max)
    {
        return new Color(
            (byte)RandomRange(min.R, max.R),
            (byte)RandomRange(min.G, max.G),
            (byte)RandomRange(min.B, max.B),
            (byte)RandomRange(min.A, max.A)
        );
    }

    /// <summary>
    /// Initializes a particle with random values based on this emitter's configuration.
    /// </summary>
    public void InitializeParticle(Particle particle)
    {
        // Position
        particle.Position = GetRandomEmissionPosition();

        // Velocity
        particle.Velocity = GetRandomVelocity();

        // Acceleration
        particle.Acceleration = Acceleration;

        // Colors
        particle.StartColor = RandomColor(StartColorMin, StartColorMax);
        particle.EndColor = RandomColor(EndColorMin, EndColorMax);
        particle.Color = particle.StartColor;

        // Scale
        var startScale = RandomRange(ScaleMin.X, ScaleMax.X);
        var endScale = RandomRange(EndScaleMin.X, EndScaleMax.X);
        particle.StartScale = startScale;
        particle.EndScale = endScale;
        particle.Scale = startScale;

        // Rotation
        particle.Rotation = RandomRange(InitialRotationRange.X, InitialRotationRange.Y);
        particle.RotationSpeed = RandomRange(RotationSpeedRange.X, RotationSpeedRange.Y);

        // Lifetime
        particle.MaxLifetime = RandomRange(LifetimeRange.X, LifetimeRange.Y);
        particle.Lifetime = 0.0f;
    }
}

