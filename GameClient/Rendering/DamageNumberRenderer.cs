using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders floating damage numbers and other combat text above entities.
/// </summary>
public class DamageNumberRenderer : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private SpriteFont? _font;
    private readonly List<DamageNumber> _activeNumbers = new();

    public DamageNumberRenderer(Camera2D camera)
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

        // Update and remove expired numbers
        for (int i = _activeNumbers.Count - 1; i >= 0; i--)
        {
            var number = _activeNumbers[i];
            number.Update(deltaTime);

            if (number.IsExpired)
            {
                _activeNumbers.RemoveAt(i);
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        foreach (var number in _activeNumbers)
        {
            if (number.IsExpired)
                continue;

            DrawDamageNumber(number);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Shows a damage number at the specified position.
    /// </summary>
    public void ShowDamage(Vector2 position, int amount, bool isCritical = false, bool isHealing = false)
    {
        var color = isHealing ? Color.Green : (isCritical ? Color.Yellow : Color.Red);
        var scale = isCritical ? 1.5f : 1.0f;

        _activeNumbers.Add(new DamageNumber
        {
            Position = position,
            Text = amount.ToString(),
            Color = color,
            StartColor = color,
            EndColor = Color.Transparent,
            Scale = scale,
            Duration = isCritical ? 1.5f : 1.0f,
            RiseDistance = isCritical ? 60.0f : 40.0f,
            StartOffset = Vector2.Zero
        });
    }

    /// <summary>
    /// Shows custom text at the specified position.
    /// </summary>
    public void ShowText(Vector2 position, string text, Color color, float duration = 1.0f)
    {
        _activeNumbers.Add(new DamageNumber
        {
            Position = position,
            Text = text,
            Color = color,
            StartColor = color,
            EndColor = Color.Transparent,
            Scale = 1.0f,
            Duration = duration,
            RiseDistance = 40.0f,
            StartOffset = Vector2.Zero
        });
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, SpriteFont? font = null)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _font = font;
    }

    /// <summary>
    /// Draws a single damage number.
    /// </summary>
    private void DrawDamageNumber(DamageNumber number)
    {
        if (_font == null || _spriteBatch == null)
        {
            // Fallback: draw as colored rectangle if no font available
            DrawFallbackNumber(number);
            return;
        }

        var currentPosition = number.CurrentPosition;
        var currentColor = number.CurrentColor;
        var currentScale = number.CurrentScale;

        // Draw text with outline for better visibility
        var outlineColor = new Color((byte)0, (byte)0, (byte)0, currentColor.A);
        var textSize = _font.MeasureString(number.Text) * currentScale;

        // Draw outline (8 directions)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                _spriteBatch.DrawString(
                    _font,
                    number.Text,
                    currentPosition + new Vector2(x, y),
                    outlineColor,
                    0.0f,
                    textSize * 0.5f,
                    currentScale,
                    SpriteEffects.None,
                    0.0f);
            }
        }

        // Draw main text
        _spriteBatch.DrawString(
            _font,
            number.Text,
            currentPosition,
            currentColor,
            0.0f,
            textSize * 0.5f,
            currentScale,
            SpriteEffects.None,
            0.0f);
    }

    /// <summary>
    /// Fallback rendering when no font is available.
    /// </summary>
    private void DrawFallbackNumber(DamageNumber number)
    {
        // Create a simple colored rectangle as placeholder
        // In a real implementation, you'd want to load a default font or use bitmap fonts
        var currentPosition = number.CurrentPosition;
        var currentColor = number.CurrentColor;
        var size = new Vector2(20, 20) * number.CurrentScale;

        // This is a placeholder - ideally you'd have a default font
        // For now, we'll just skip rendering if no font is available
    }

    /// <summary>
    /// Clears all active damage numbers.
    /// </summary>
    public void Clear()
    {
        _activeNumbers.Clear();
    }
}

/// <summary>
/// Represents a single floating damage number.
/// </summary>
internal class DamageNumber
{
    public Vector2 Position { get; set; }
    public Vector2 StartOffset { get; set; }
    public string Text { get; set; } = "";
    public Color Color { get; set; }
    public Color StartColor { get; set; }
    public Color EndColor { get; set; }
    public float Scale { get; set; } = 1.0f;
    public float Duration { get; set; } = 1.0f;
    public float RiseDistance { get; set; } = 40.0f;
    public float Lifetime { get; private set; }

    public float NormalizedLifetime => Duration > 0 ? Lifetime / Duration : 1.0f;
    public bool IsExpired => Lifetime >= Duration;

    public Vector2 CurrentPosition
    {
        get
        {
            var t = NormalizedLifetime;
            var riseOffset = new Vector2(0, -RiseDistance * t);
            // Add slight horizontal drift
            var driftOffset = new Vector2((t - 0.5f) * 20.0f, 0);
            return Position + StartOffset + riseOffset + driftOffset;
        }
    }

    public Color CurrentColor
    {
        get
        {
            var t = NormalizedLifetime;
            // Fade out in the last 30% of lifetime
            var fadeStart = 0.7f;
            if (t < fadeStart)
            {
                return Color.Lerp(StartColor, StartColor, t / fadeStart);
            }
            else
            {
                var fadeT = (t - fadeStart) / (1.0f - fadeStart);
                return Color.Lerp(StartColor, EndColor, fadeT);
            }
        }
    }

    public float CurrentScale
    {
        get
        {
            var t = NormalizedLifetime;
            // Scale up quickly, then scale down
            if (t < 0.2f)
            {
                return Scale * (1.0f + t * 2.0f); // Scale up to 1.4x
            }
            else
            {
                return Scale * (1.4f - (t - 0.2f) * 0.5f); // Scale down
            }
        }
    }

    public void Update(float deltaTime)
    {
        Lifetime += deltaTime;
    }
}

