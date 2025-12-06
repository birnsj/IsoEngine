using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders visual effects for click-to-move indicators.
/// </summary>
public class ClickEffectRenderer : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _circleTexture;
    private readonly List<ClickEffect> _activeEffects = new();

    public ClickEffectRenderer(Camera2D camera)
    {
        _camera = camera;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update and remove expired effects
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeEffects[i];
            effect.Update(deltaTime);

            if (effect.IsExpired)
            {
                _activeEffects.RemoveAt(i);
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

        foreach (var effect in _activeEffects)
        {
            if (effect.IsExpired)
                continue;

            // Calculate fade based on lifetime
            var alpha = 1.0f - effect.NormalizedLifetime;
            var color = new Color(effect.Color.R, effect.Color.G, effect.Color.B, (byte)(255 * alpha));

            // Draw expanding circle
            var radius = effect.CurrentRadius;
            var position = effect.Position;
            var size = new Vector2(radius * 2, radius * 2);
            var sourceRect = new Rectangle(0, 0, _circleTexture.Width, _circleTexture.Height);
            var destRect = new Rectangle(
                (int)(position.X - radius),
                (int)(position.Y - radius),
                (int)size.X,
                (int)size.Y);

            _spriteBatch.Draw(_circleTexture, destRect, sourceRect, color);

            // Draw inner circle for ripple effect
            if (effect.NormalizedLifetime > 0.3f)
            {
                var innerRadius = radius * 0.5f;
                var innerAlpha = alpha * 0.5f;
                var innerColor = new Color(effect.Color.R, effect.Color.G, effect.Color.B, (byte)(255 * innerAlpha));
                var innerRect = new Rectangle(
                    (int)(position.X - innerRadius),
                    (int)(position.Y - innerRadius),
                    (int)(innerRadius * 2),
                    (int)(innerRadius * 2));

                _spriteBatch.Draw(_circleTexture, innerRect, sourceRect, innerColor);
            }
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Creates a click effect at the specified position.
    /// </summary>
    public void CreateClickEffect(Vector2 position, Color? color = null)
    {
        _activeEffects.Add(new ClickEffect
        {
            Position = position,
            Color = color ?? new Color(100, 150, 255, 255), // Light blue by default
            StartRadius = 5.0f,
            MaxRadius = 40.0f,
            Duration = 0.5f
        });
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a simple circle texture
        _circleTexture = CreateCircleTexture(graphicsDevice, 64);
    }

    /// <summary>
    /// Creates a circular texture for rendering effects.
    /// </summary>
    private Texture2D CreateCircleTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        var center = size / 2.0f;
        var radius = size / 2.0f - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= radius)
                {
                    // Create a smooth edge by fading out near the edge
                    var edgeDistance = radius - distance;
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
/// Represents a single click effect animation.
/// </summary>
internal class ClickEffect
{
    public Vector2 Position { get; set; }
    public Color Color { get; set; }
    public float StartRadius { get; set; }
    public float MaxRadius { get; set; }
    public float Duration { get; set; }
    public float Lifetime { get; private set; }

    public float NormalizedLifetime => Duration > 0 ? Lifetime / Duration : 1.0f;
    public float CurrentRadius => StartRadius + (MaxRadius - StartRadius) * NormalizedLifetime;
    public bool IsExpired => Lifetime >= Duration;

    public void Update(float deltaTime)
    {
        Lifetime += deltaTime;
    }
}

