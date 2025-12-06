using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Manages full-screen visual effects like flashes, fades, and color tints.
/// </summary>
public class ScreenEffectRenderer : IGameService
{
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _pixelTexture;

    // Active effects
    private readonly List<ScreenEffect> _activeEffects = new();

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
        if (_spriteBatch == null || _graphicsDevice == null || _pixelTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;

        // Draw all active effects on top of the scene
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        foreach (var effect in _activeEffects)
        {
            if (effect.IsExpired)
                continue;

            var color = effect.GetCurrentColor();
            var rect = new Rectangle(0, 0, viewport.Width, viewport.Height);

            _spriteBatch.Draw(_pixelTexture, rect, color);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Creates a screen flash effect (white/red flash on damage/explosions).
    /// </summary>
    public void Flash(Color color, float duration = 0.1f)
    {
        _activeEffects.Add(new ScreenEffect
        {
            Type = ScreenEffectType.Flash,
            Color = color,
            Duration = duration,
            FadeInDuration = 0.0f,
            FadeOutDuration = duration
        });
    }

    /// <summary>
    /// Creates a fade in effect (from black/color to transparent).
    /// </summary>
    public void FadeIn(Color fromColor, float duration = 1.0f)
    {
        _activeEffects.Add(new ScreenEffect
        {
            Type = ScreenEffectType.Fade,
            Color = fromColor,
            Duration = duration,
            FadeInDuration = 0.0f,
            FadeOutDuration = duration,
            StartAlpha = 1.0f,
            EndAlpha = 0.0f
        });
    }

    /// <summary>
    /// Creates a fade out effect (from transparent to black/color).
    /// </summary>
    public void FadeOut(Color toColor, float duration = 1.0f)
    {
        _activeEffects.Add(new ScreenEffect
        {
            Type = ScreenEffectType.Fade,
            Color = toColor,
            Duration = duration,
            FadeInDuration = duration,
            FadeOutDuration = 0.0f,
            StartAlpha = 0.0f,
            EndAlpha = 1.0f
        });
    }

    /// <summary>
    /// Creates a color tint overlay (semi-transparent color over the screen).
    /// </summary>
    public void Tint(Color color, float duration = 0.0f)
    {
        _activeEffects.Add(new ScreenEffect
        {
            Type = ScreenEffectType.Tint,
            Color = color,
            Duration = duration > 0 ? duration : float.MaxValue, // Infinite if duration is 0
            FadeInDuration = 0.1f,
            FadeOutDuration = duration > 0 ? 0.1f : 0.0f,
            StartAlpha = color.A / 255.0f,
            EndAlpha = duration > 0 ? 0.0f : color.A / 255.0f
        });
    }

    /// <summary>
    /// Replaces any existing tint effects with a new color tint overlay.
    /// This prevents accumulating multiple tint overlays.
    /// </summary>
    public void SetTint(Color color, float duration = 0.0f)
    {
        // Remove all existing tint effects first
        _activeEffects.RemoveAll(e => e.Type == ScreenEffectType.Tint);
        
        // Add the new tint
        Tint(color, duration);
    }

    /// <summary>
    /// Removes all active effects.
    /// </summary>
    public void Clear()
    {
        _activeEffects.Clear();
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a 1x1 white pixel texture
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }
}

/// <summary>
/// Types of screen effects.
/// </summary>
internal enum ScreenEffectType
{
    Flash,  // Quick flash that fades out
    Fade,   // Smooth fade in/out
    Tint    // Persistent color overlay
}

/// <summary>
/// Represents a single screen effect.
/// </summary>
internal class ScreenEffect
{
    public ScreenEffectType Type { get; set; }
    public Color Color { get; set; }
    public float Duration { get; set; }
    public float FadeInDuration { get; set; }
    public float FadeOutDuration { get; set; }
    public float StartAlpha { get; set; } = 1.0f;
    public float EndAlpha { get; set; } = 0.0f;
    public float Lifetime { get; private set; }

    public float NormalizedLifetime => Duration > 0 && Duration < float.MaxValue ? Lifetime / Duration : 0.0f;
    public bool IsExpired => Duration > 0 && Duration < float.MaxValue && Lifetime >= Duration;

    public void Update(float deltaTime)
    {
        Lifetime += deltaTime;
    }

    public Color GetCurrentColor()
    {
        var alpha = CalculateAlpha();
        return new Color(Color.R, Color.G, Color.B, (byte)(255 * alpha));
    }

    private float CalculateAlpha()
    {
        if (Type == ScreenEffectType.Flash)
        {
            // Flash: quick fade out
            if (Lifetime < FadeOutDuration)
            {
                return 1.0f - (Lifetime / FadeOutDuration);
            }
            return 0.0f;
        }
        else if (Type == ScreenEffectType.Fade)
        {
            // Fade: smooth transition
            if (Lifetime < FadeInDuration)
            {
                // Fading in
                var t = Lifetime / FadeInDuration;
                return MathHelper.Lerp(StartAlpha, 1.0f, t);
            }
            else if (Lifetime < Duration - FadeOutDuration)
            {
                // At full opacity
                return 1.0f;
            }
            else
            {
                // Fading out
                var fadeOutStart = Duration - FadeOutDuration;
                var t = (Lifetime - fadeOutStart) / FadeOutDuration;
                return MathHelper.Lerp(1.0f, EndAlpha, t);
            }
        }
        else // Tint
        {
            // Tint: persistent with optional fade
            if (Duration >= float.MaxValue)
            {
                // Infinite tint
                return Color.A / 255.0f;
            }
            else
            {
                // Tint with duration
                if (Lifetime < FadeInDuration)
                {
                    var t = Lifetime / FadeInDuration;
                    return MathHelper.Lerp(StartAlpha, Color.A / 255.0f, t);
                }
                else if (Lifetime < Duration - FadeOutDuration)
                {
                    return Color.A / 255.0f;
                }
                else
                {
                    var fadeOutStart = Duration - FadeOutDuration;
                    var t = (Lifetime - fadeOutStart) / FadeOutDuration;
                    return MathHelper.Lerp(Color.A / 255.0f, EndAlpha, t);
                }
            }
        }
    }
}

