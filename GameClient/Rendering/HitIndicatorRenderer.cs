using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders hit indicator effects like impact rings and spark particles.
/// </summary>
public class HitIndicatorRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly ParticleSystem _particleSystem;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _circleTexture;
    private readonly List<ImpactRing> _activeRings = new();

    public HitIndicatorRenderer(Camera2D camera, ParticleSystem particleSystem)
    {
        _camera = camera;
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update and remove expired rings
        for (int i = _activeRings.Count - 1; i >= 0; i--)
        {
            var ring = _activeRings[i];
            ring.Update(deltaTime);

            if (ring.IsExpired)
            {
                _activeRings.RemoveAt(i);
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _circleTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        foreach (var ring in _activeRings)
        {
            if (ring.IsExpired)
                continue;

            DrawImpactRing(ring);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Creates a hit indicator at the specified position.
    /// </summary>
    public void CreateHitIndicator(Vector2 position, float intensity = 1.0f, Color? sparkColor = null)
    {
        // Create impact ring
        _activeRings.Add(new ImpactRing
        {
            Position = position,
            StartRadius = 5.0f,
            MaxRadius = 30.0f * intensity,
            Duration = 0.3f,
            Color = sparkColor ?? Color.White
        });

        // Create spark particles using particle system
        var sparkEmitter = ParticlePreset.HitSparks(position, intensity);
        sparkEmitter.StartColorMin = sparkColor ?? new Color(255, 200, 100);
        sparkEmitter.StartColorMax = sparkColor ?? new Color(255, 150, 50);
        _particleSystem.CreateEmitter(sparkEmitter);
    }

    /// <summary>
    /// Creates a critical hit indicator (larger, more intense).
    /// </summary>
    public void CreateCriticalHitIndicator(Vector2 position, float intensity = 1.5f)
    {
        // Larger impact ring
        _activeRings.Add(new ImpactRing
        {
            Position = position,
            StartRadius = 10.0f,
            MaxRadius = 50.0f * intensity,
            Duration = 0.4f,
            Color = Color.Yellow
        });

        // More intense spark particles
        var sparkEmitter = ParticlePreset.HitSparks(position, intensity);
        sparkEmitter.ParticlesPerBurst = (int)(25 * intensity);
        sparkEmitter.StartColorMin = Color.Yellow;
        sparkEmitter.StartColorMax = new Color(255, 200, 0);
        _particleSystem.CreateEmitter(sparkEmitter);
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create circle texture for impact rings
        _circleTexture = CreateCircleTexture(graphicsDevice, 64);
    }

    /// <summary>
    /// Draws an impact ring.
    /// </summary>
    private void DrawImpactRing(ImpactRing ring)
    {
        var radius = ring.CurrentRadius;
        var alpha = ring.CurrentAlpha;
        var color = new Color(ring.Color.R, ring.Color.G, ring.Color.B, (byte)(255 * alpha));

        var size = new Vector2(radius * 2, radius * 2);
        var sourceRect = new Rectangle(0, 0, _circleTexture!.Width, _circleTexture.Height);
        var destRect = new Rectangle(
            (int)(ring.Position.X - radius),
            (int)(ring.Position.Y - radius),
            (int)size.X,
            (int)size.Y);

        _spriteBatch!.Draw(_circleTexture, destRect, sourceRect, color);
    }

    /// <summary>
    /// Creates a circular texture for rendering impact rings.
    /// </summary>
    private Texture2D CreateCircleTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        var center = size / 2.0f;
        var outerRadius = size / 2.0f;
        var innerRadius = outerRadius - 3.0f; // Ring thickness

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance >= innerRadius && distance <= outerRadius)
                {
                    // Create a smooth edge
                    var edgeDistance = Math.Min(
                        distance - innerRadius,
                        outerRadius - distance
                    );
                    var alpha = Math.Min(1.0f, edgeDistance / 2.0f);
                    data[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
                else
                {
                    data[y * size + x] = Color.Transparent;
                }
            }
        }

        texture.SetData(data);
        return texture;
    }
}

/// <summary>
/// Represents a single impact ring animation.
/// </summary>
internal class ImpactRing
{
    public Vector2 Position { get; set; }
    public float StartRadius { get; set; }
    public float MaxRadius { get; set; }
    public float Duration { get; set; }
    public Color Color { get; set; }
    public float Lifetime { get; private set; }

    public float NormalizedLifetime => Duration > 0 ? Lifetime / Duration : 1.0f;
    public float CurrentRadius => StartRadius + (MaxRadius - StartRadius) * NormalizedLifetime;
    public float CurrentAlpha => 1.0f - NormalizedLifetime; // Fade out as it expands
    public bool IsExpired => Lifetime >= Duration;

    public void Update(float deltaTime)
    {
        Lifetime += deltaTime;
    }
}

