using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Provides screen shake effects for camera impacts, explosions, and combat events.
/// </summary>
public class ScreenShakeService : IGameService
{
    private readonly Camera2D _camera;
    private Vector2 _basePosition;
    private float _shakeIntensity = 0.0f;
    private float _shakeDuration = 0.0f;
    private float _shakeTimer = 0.0f;
    private float _shakeFrequency = 20.0f; // Shakes per second
    private static readonly Random _random = new Random();

    /// <summary>
    /// Gets or sets the base camera position (before shake is applied).
    /// </summary>
    public Vector2 BasePosition
    {
        get => _basePosition;
        set
        {
            _basePosition = value;
            UpdateCameraPosition();
        }
    }

    public ScreenShakeService(Camera2D camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _basePosition = camera.Position;
    }

    public void Initialize()
    {
        _basePosition = _camera.Position;
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // If not shaking, update base position to track camera movement
        if (_shakeTimer <= 0.0f)
        {
            _basePosition = _camera.Position;
        }
        else
        {
            _shakeTimer -= deltaTime;

            // Update camera position with shake
            UpdateCameraPosition();

            // Decay shake intensity over time
            if (_shakeTimer <= 0.0f)
            {
                _shakeIntensity = 0.0f;
                _shakeTimer = 0.0f;
                _camera.Position = _basePosition;
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Screen shake doesn't need draw logic
    }

    /// <summary>
    /// Applies a screen shake effect.
    /// </summary>
    /// <param name="intensity">The intensity of the shake (in pixels).</param>
    /// <param name="duration">The duration of the shake (in seconds).</param>
    /// <param name="frequency">Optional frequency override (shakes per second).</param>
    public void Shake(float intensity, float duration, float? frequency = null)
    {
        // If new shake is stronger or longer, update
        if (intensity > _shakeIntensity || duration > _shakeDuration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = duration;
        }

        if (frequency.HasValue)
        {
            _shakeFrequency = frequency.Value;
        }
    }

    /// <summary>
    /// Applies a quick impact shake (short, sharp shake).
    /// </summary>
    public void ImpactShake(float intensity = 5.0f)
    {
        Shake(intensity, 0.1f, 30.0f);
    }

    /// <summary>
    /// Applies an explosion shake (longer, more intense shake).
    /// </summary>
    public void ExplosionShake(float intensity = 10.0f)
    {
        Shake(intensity, 0.3f, 15.0f);
    }

    /// <summary>
    /// Applies a combat hit shake (medium intensity, short duration).
    /// </summary>
    public void HitShake(float intensity = 3.0f)
    {
        Shake(intensity, 0.15f, 25.0f);
    }

    /// <summary>
    /// Stops all active shake effects immediately.
    /// </summary>
    public void StopShake()
    {
        _shakeIntensity = 0.0f;
        _shakeTimer = 0.0f;
        _camera.Position = _basePosition;
    }

    /// <summary>
    /// Updates the camera position with current shake offset.
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (_shakeTimer <= 0.0f || _shakeIntensity <= 0.0f)
        {
            _camera.Position = _basePosition;
            return;
        }

        // Calculate shake offset using Perlin-like noise or random values
        // Using a simple approach with sine waves for smooth shake
        var time = _shakeDuration - _shakeTimer;
        var shakeAmount = _shakeIntensity * (1.0f - (_shakeTimer / _shakeDuration)); // Decay over time

        // Generate shake offset using multiple sine waves for natural feel
        var offsetX = MathF.Sin(time * _shakeFrequency * MathF.PI * 2.0f) * shakeAmount;
        var offsetY = MathF.Cos(time * _shakeFrequency * MathF.PI * 2.0f * 1.3f) * shakeAmount;

        // Add some randomness for more chaotic feel
        var randomX = (_random.NextSingle() - 0.5f) * shakeAmount * 0.3f;
        var randomY = (_random.NextSingle() - 0.5f) * shakeAmount * 0.3f;

        _camera.Position = _basePosition + new Vector2(offsetX + randomX, offsetY + randomY);
    }
}

