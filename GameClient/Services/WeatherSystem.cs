using GameCore;
using GameCore.Rendering;
using GameCore.Services;
using GameClient.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameClient.Utilities;

namespace GameClient.Services;

/// <summary>
/// Types of weather conditions.
/// </summary>
public enum WeatherType
{
    Clear,
    LightRain,
    HeavyRain,
    LightningRain,
    Snow,
    Blizzard,
    Fog
}

/// <summary>
/// Service that manages weather conditions with random changes and visual effects.
/// </summary>
public class WeatherSystem : IGameService
{
    private readonly GameTimeManager _timeManager;
    private readonly ParticleSystem _particleSystem;
    private readonly ScreenEffectRenderer _screenEffects;
    private readonly Camera2D _camera;
    private readonly GraphicsDevice _graphicsDevice;
    private IsometricTileMap? _tileMap;

    private WeatherType _currentWeather = WeatherType.Clear;
    private WeatherType _previousWeather = WeatherType.Clear; // Track previous weather to detect changes
    private float _weatherChangeTimer = 0.0f;
    private float _nextWeatherChangeTime;
    private readonly Random _random = new Random();
    private bool _randomWeatherEnabled = true;

    // Weather probabilities (must sum to 100)
    private float _probabilityClear = GameConstants.Weather.ProbabilityClear;
    private float _probabilityLightRain = GameConstants.Weather.ProbabilityLightRain;
    private float _probabilityHeavyRain = GameConstants.Weather.ProbabilityHeavyRain;
    private float _probabilitySnow = GameConstants.Weather.ProbabilitySnow;
    private float _probabilityBlizzard = 5.0f; // Default blizzard probability
    private float _probabilityLightningRain = 5.0f; // Default lightning rain probability
    private float _probabilityFog = GameConstants.Weather.ProbabilityFog;

    // Weather change intervals
    private float _minChangeInterval = GameConstants.Weather.MinWeatherChangeInterval;
    private float _maxChangeInterval = GameConstants.Weather.MaxWeatherChangeInterval;

    // Particle spawning
    private float _particleSpawnTimer = 0.0f;
    private const float ParticleSpawnInterval = 0.05f; // Spawn particles more frequently for better coverage

    // Lightning flash timing
    private float _lightningTimer = 0.0f;
    private float _nextLightningTime = 0.0f;

    // Rain splash timing
    private float _splashTimer = 0.0f;
    private const float SplashSpawnInterval = 0.02f; // Spawn splashes frequently for continuous effect

    /// <summary>
    /// Gets or sets the current weather type.
    /// </summary>
    public WeatherType CurrentWeather
    {
        get => _currentWeather;
        set
        {
            if (_currentWeather != value)
            {
                TransitionToWeather(value);
            }
        }
    }

    /// <summary>
    /// Gets the current weather intensity (0.0 to 1.0).
    /// </summary>
    public float WeatherIntensity { get; private set; } = 0.0f;

    /// <summary>
    /// Gets or sets whether random weather changes are enabled.
    /// When false, weather will only change when manually set via CurrentWeather property.
    /// </summary>
    public bool RandomWeatherEnabled
    {
        get => _randomWeatherEnabled;
        set => _randomWeatherEnabled = value;
    }

    /// <summary>
    /// Gets or sets the minimum weather change interval in seconds.
    /// </summary>
    public float MinChangeInterval
    {
        get => _minChangeInterval;
        set => _minChangeInterval = Math.Max(1.0f, value);
    }

    /// <summary>
    /// Gets or sets the maximum weather change interval in seconds.
    /// </summary>
    public float MaxChangeInterval
    {
        get => _maxChangeInterval;
        set => _maxChangeInterval = Math.Max(_minChangeInterval, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for clear weather (0-100).
    /// </summary>
    public float ProbabilityClear
    {
        get => _probabilityClear;
        set => _probabilityClear = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for light rain (0-100).
    /// </summary>
    public float ProbabilityLightRain
    {
        get => _probabilityLightRain;
        set => _probabilityLightRain = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for heavy rain (0-100).
    /// </summary>
    public float ProbabilityHeavyRain
    {
        get => _probabilityHeavyRain;
        set => _probabilityHeavyRain = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for snow (0-100).
    /// </summary>
    public float ProbabilitySnow
    {
        get => _probabilitySnow;
        set => _probabilitySnow = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for blizzard (0-100).
    /// </summary>
    public float ProbabilityBlizzard
    {
        get => _probabilityBlizzard;
        set => _probabilityBlizzard = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for fog (0-100).
    /// </summary>
    public float ProbabilityFog
    {
        get => _probabilityFog;
        set => _probabilityFog = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Gets or sets the probability weight for lightning rain (0-100).
    /// </summary>
    public float ProbabilityLightningRain
    {
        get => _probabilityLightningRain;
        set => _probabilityLightningRain = Math.Max(0.0f, value);
    }

    public WeatherSystem(
        GameTimeManager timeManager,
        ParticleSystem particleSystem,
        ScreenEffectRenderer screenEffects,
        Camera2D camera,
        GraphicsDevice graphicsDevice)
    {
        _timeManager = timeManager ?? throw new ArgumentNullException(nameof(timeManager));
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
        _screenEffects = screenEffects ?? throw new ArgumentNullException(nameof(screenEffects));
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        // Initialize next weather change time
        _nextWeatherChangeTime = _minChangeInterval + (float)_random.NextDouble() * (_maxChangeInterval - _minChangeInterval);
    }

    public void Initialize()
    {
        // Get tile map from TileMapRenderer if available
        var tileMapRenderer = ServiceLocator.Get<TileMapRenderer>();
        if (tileMapRenderer != null)
        {
            _tileMap = tileMapRenderer.TileMap;
        }
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = _timeManager.DeltaTime;

        // Update weather change timer only if random weather is enabled
        if (_randomWeatherEnabled)
        {
            _weatherChangeTimer += deltaTime;

            // Randomly change weather
            if (_weatherChangeTimer >= _nextWeatherChangeTime)
            {
                ChangeWeatherRandomly();
                _weatherChangeTimer = 0.0f;
                _nextWeatherChangeTime = _minChangeInterval + (float)_random.NextDouble() * (_maxChangeInterval - _minChangeInterval);
            }
        }

        // Update weather effects
        UpdateWeatherEffects(deltaTime);

        // Handle lightning flashes for lightning rain - more random timing and intensity
        if (_currentWeather == WeatherType.LightningRain)
        {
            _lightningTimer += deltaTime;
            if (_lightningTimer >= _nextLightningTime)
            {
                // More random flash intensity (0.2 to 0.95 for more variation)
                var flashIntensity = (float)(_random.NextDouble() * 0.75 + 0.2);
                var flashColor = new Color((byte)(255 * flashIntensity), (byte)(255 * flashIntensity), (byte)(255 * flashIntensity), (byte)(255 * flashIntensity));
                
                // Random flash duration (0.05 to 0.2 seconds - some quick, some longer)
                var flashDuration = (float)(_random.NextDouble() * 0.15 + 0.05);
                _screenEffects.Flash(flashColor, flashDuration);
                
                // More random timing - longer gaps possible (0.5 to 10 seconds)
                // Use exponential distribution for more natural randomness
                var baseTime = (float)(_random.NextDouble() * 9.5 + 0.5);
                // Occasionally have rapid flashes (10% chance of very short gap)
                if (_random.NextDouble() < 0.1)
                {
                    baseTime = (float)(_random.NextDouble() * 0.5 + 0.1); // 0.1 to 0.6 seconds
                }
                
                _lightningTimer = 0.0f;
                _nextLightningTime = baseTime;
            }
        }
        else
        {
            // Reset lightning timer when not in lightning rain
            _lightningTimer = 0.0f;
            _nextLightningTime = 0.0f;
        }

        // Spawn weather particles
        _particleSpawnTimer += deltaTime;
        if (_particleSpawnTimer >= ParticleSpawnInterval)
        {
            SpawnWeatherParticles();
            _particleSpawnTimer = 0.0f;
        }

        // Spawn rain splash particles at ground level
        if (_currentWeather == WeatherType.LightRain || _currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain)
        {
            _splashTimer += deltaTime;
            if (_splashTimer >= SplashSpawnInterval)
            {
                SpawnRainSplashes();
                _splashTimer = 0.0f;
            }
        }
        else
        {
            _splashTimer = 0.0f;
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Weather effects are handled by ParticleSystem and ScreenEffectRenderer
    }

    /// <summary>
    /// Changes weather randomly based on probability weights.
    /// </summary>
    public void ChangeWeatherRandomly()
    {
        // Normalize probabilities to sum to 100
        var total = _probabilityClear + _probabilityLightRain + _probabilityHeavyRain + 
                   _probabilityLightningRain + _probabilitySnow + _probabilityBlizzard + _probabilityFog;
        
        if (total <= 0.0f)
        {
            // Fallback to default probabilities
            total = 100.0f;
        }

        var roll = (float)_random.NextDouble() * total;
        WeatherType newWeather;

        if (roll < _probabilityClear)
            newWeather = WeatherType.Clear;
        else if (roll < _probabilityClear + _probabilityLightRain)
            newWeather = WeatherType.LightRain;
        else if (roll < _probabilityClear + _probabilityLightRain + _probabilityHeavyRain)
            newWeather = WeatherType.HeavyRain;
        else if (roll < _probabilityClear + _probabilityLightRain + _probabilityHeavyRain + _probabilityLightningRain)
            newWeather = WeatherType.LightningRain;
        else if (roll < _probabilityClear + _probabilityLightRain + _probabilityHeavyRain + _probabilityLightningRain + _probabilitySnow)
            newWeather = WeatherType.Snow;
        else if (roll < _probabilityClear + _probabilityLightRain + _probabilityHeavyRain + _probabilityLightningRain + _probabilitySnow + _probabilityBlizzard)
            newWeather = WeatherType.Blizzard;
        else
            newWeather = WeatherType.Fog;

        TransitionToWeather(newWeather);
    }

    /// <summary>
    /// Transitions to a new weather type, resetting intensity for smooth fade-in.
    /// </summary>
    private void TransitionToWeather(WeatherType newWeather)
    {
        WeatherIntensity = 0.0f;
        _previousWeather = _currentWeather;
        _currentWeather = newWeather;
        
        // Clear screen effects when switching away from weather types that use them
        if (_previousWeather == WeatherType.Fog || newWeather == WeatherType.Clear)
        {
            _screenEffects.Clear();
        }
    }

    /// <summary>
    /// Updates weather effects like screen tints and particle intensity.
    /// </summary>
    private void UpdateWeatherEffects(float deltaTime)
    {
        // Fade in weather intensity
        if (WeatherIntensity < 1.0f)
        {
            WeatherIntensity = Math.Min(1.0f, WeatherIntensity + deltaTime * 0.5f);
        }

        // Apply weather-specific screen effects
        switch (_currentWeather)
        {
            case WeatherType.Clear:
                // Clear any tint effects when switching to clear weather
                if (_previousWeather != WeatherType.Clear)
                {
                    _screenEffects.Clear();
                }
                break;

            case WeatherType.Fog:
                // Update fog tint based on intensity - use darker, more subtle fog color
                var fogAlpha = (byte)(50 * WeatherIntensity);
                // Use darker gray-blue fog color to avoid white screen
                var fogColor = new Color((byte)80, (byte)85, (byte)90, Math.Min(fogAlpha, (byte)60));
                _screenEffects.SetTint(fogColor, 0.0f); // 0.0f = infinite duration
                break;

            case WeatherType.LightRain:
                // Subtle darkening for light rain
                var lightRainAlpha = (byte)(15 * WeatherIntensity);
                var lightRainColor = new Color((byte)50, (byte)60, (byte)70, lightRainAlpha);
                _screenEffects.SetTint(lightRainColor, 0.0f);
                break;

            case WeatherType.HeavyRain:
                // More pronounced darkening for heavy rain
                var heavyRainAlpha = (byte)(35 * WeatherIntensity);
                var heavyRainColor = new Color((byte)40, (byte)50, (byte)60, heavyRainAlpha);
                _screenEffects.SetTint(heavyRainColor, 0.0f);
                break;

            case WeatherType.Snow:
                // No screen tint for snow - let the particles provide the visual effect
                // Clear any tints when switching to snow
                if (_previousWeather != WeatherType.Snow)
                {
                    _screenEffects.Clear();
                }
                break;

            case WeatherType.Blizzard:
                // Add fog effect for blizzard - more atmospheric, less white
                var blizzardAlpha = (byte)(15 * WeatherIntensity); // Reduced from 25 to 15
                var blizzardColor = new Color((byte)150, (byte)160, (byte)170, Math.Min(blizzardAlpha, (byte)25)); // Darker color, lower max alpha
                _screenEffects.SetTint(blizzardColor, 0.0f);
                break;

            case WeatherType.LightningRain:
                // Dark screen for storm atmosphere (lightning flashes handled separately)
                var lightningRainAlpha = (byte)(45 * WeatherIntensity);
                var lightningRainColor = new Color((byte)30, (byte)35, (byte)40, lightningRainAlpha);
                _screenEffects.SetTint(lightningRainColor, 0.0f);
                break;
        }
    }

    /// <summary>
    /// Spawns weather particles (rain or snow) based on current weather.
    /// </summary>
    private void SpawnWeatherParticles()
    {
        if (_currentWeather == WeatherType.LightRain || _currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain)
        {
            SpawnRain();
        }
        else if (_currentWeather == WeatherType.Snow)
        {
            SpawnSnow();
        }
        else if (_currentWeather == WeatherType.Blizzard)
        {
            SpawnBlizzard();
        }
    }

    /// <summary>
    /// Spawns rain particles across the visible screen area as vertical blue lines.
    /// </summary>
    private void SpawnRain()
    {
        var viewport = _graphicsDevice.Viewport;
        
        // Calculate visible area in world space
        var topLeft = _camera.ScreenToWorld(Vector2.Zero, viewport.Width, viewport.Height);
        var bottomRight = _camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height), viewport.Width, viewport.Height);
        
        // Expand spawn area to cover whole screen plus margins (50% wider on each side)
        var baseWidth = bottomRight.X - topLeft.X;
        var spawnWidth = baseWidth * 2.0f; // Double width for full coverage
        var spawnHeight = 600.0f; // Larger spawn area above screen to ensure full coverage
        
        // Increased particle count to cover whole screen - more for heavy rain and lightning rain
        int particleCount = _currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain ? 100 : 70;
        particleCount = (int)(particleCount * WeatherIntensity);

        for (int i = 0; i < particleCount; i++)
        {
            // Distribute across expanded area (centered on screen)
            var x = topLeft.X - baseWidth * 0.5f + (float)_random.NextDouble() * spawnWidth;
            var y = topLeft.Y - 50.0f - (float)_random.NextDouble() * spawnHeight;

            // Heavy rain: angled and faster, light rain: vertical
            float direction;
            Vector2 velocityMin, velocityMax;
            Vector2 acceleration;
            Vector2 scaleMin, scaleMax, endScaleMin, endScaleMax;
            Vector2 lifetimeRange;

            // Rain falls straight down vertically on screen (not isometric diagonal)
            // This creates the traditional vertical rain effect
            if (_currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain)
            {
                // Heavy rain: angled and faster
                direction = MathF.PI / 2.0f + 0.25f; // Slight angle to the right
                // Faster fall speed with slight horizontal component
                velocityMin = new Vector2(50, 550);
                velocityMax = new Vector2(80, 750);
                acceleration = new Vector2(20, 200); // Faster acceleration with slight horizontal drift
                // Longer lines for heavy rain
                scaleMin = new Vector2(0.15f, 15.0f);
                scaleMax = new Vector2(0.25f, 20.0f);
                endScaleMin = new Vector2(0.15f, 15.0f);
                endScaleMax = new Vector2(0.25f, 20.0f);
                lifetimeRange = new Vector2(1.5f, 2.5f);
            }
            else
            {
                // Light rain: perfectly vertical
                direction = MathF.PI / 2.0f;
                velocityMin = new Vector2(0, 350);
                velocityMax = new Vector2(0, 450);
                acceleration = new Vector2(0, 150);
                scaleMin = new Vector2(0.1f, 8.0f);
                scaleMax = new Vector2(0.2f, 12.0f);
                endScaleMin = new Vector2(0.1f, 8.0f);
                endScaleMax = new Vector2(0.2f, 12.0f);
                lifetimeRange = new Vector2(2.0f, 3.5f);
            }

            var emitter = new ParticleEmitter
            {
                Type = EmitterType.Point,
                Mode = EmissionMode.Burst,
                Position = new Vector2(x, y),
                ParticlesPerBurst = 1,
                Direction = direction,
                Spread = (_currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain) ? 0.1f : 0.0f, // Slight spread for heavy rain and lightning rain
                VelocityMin = velocityMin,
                VelocityMax = velocityMax,
                Acceleration = acceleration,
                StartColorMin = new Color(100, 150, 255, 240), // Bright blue
                StartColorMax = new Color(120, 170, 255, 255),
                EndColorMin = new Color(100, 150, 255, 0),
                EndColorMax = new Color(120, 170, 255, 0),
                ScaleMin = scaleMin,
                ScaleMax = scaleMax,
                EndScaleMin = endScaleMin,
                EndScaleMax = endScaleMax,
                LifetimeRange = lifetimeRange
            };

            _particleSystem.CreateEmitter(emitter);
        }
    }

    /// <summary>
    /// Spawns snow particles across the visible screen area.
    /// </summary>
    private void SpawnSnow()
    {
        var viewport = _graphicsDevice.Viewport;
        
        // Calculate visible area in world space
        var topLeft = _camera.ScreenToWorld(Vector2.Zero, viewport.Width, viewport.Height);
        var bottomRight = _camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height), viewport.Width, viewport.Height);
        
        // Expand spawn area to cover whole screen plus margins (50% wider on each side)
        var baseWidth = bottomRight.X - topLeft.X;
        var spawnWidth = baseWidth * 2.0f; // Double width for full coverage
        var spawnHeight = 500.0f; // Larger spawn area to ensure full coverage
        
        // Increased particle count to cover whole screen
        int particleCount = (int)(60 * WeatherIntensity);

        for (int i = 0; i < particleCount; i++)
        {
            // Random position across expanded area (centered on screen)
            var x = topLeft.X - baseWidth * 0.5f + (float)_random.NextDouble() * spawnWidth;
            var y = topLeft.Y - 50.0f - (float)_random.NextDouble() * spawnHeight;

            // Add horizontal drift for more natural snow movement
            var horizontalDrift = (float)(_random.NextDouble() * 0.6 - 0.3); // -0.3 to 0.3 radians

            var emitter = new ParticleEmitter
            {
                Type = EmitterType.Point,
                Mode = EmissionMode.Burst,
                Position = new Vector2(x, y),
                ParticlesPerBurst = 1,
                Direction = MathF.PI / 2.0f + horizontalDrift, // Slight horizontal drift
                Spread = 0.4f, // Wider spread for more natural snow
                VelocityMin = new Vector2(-40, 40), // Slower vertical, more horizontal variation
                VelocityMax = new Vector2(40, 90),
                Acceleration = new Vector2(horizontalDrift * 10, 15), // Gentle horizontal drift, light gravity
                StartColorMin = new Color(245, 248, 255, 230), // Bright white with slight blue tint
                StartColorMax = new Color(255, 255, 255, 255),
                EndColorMin = new Color(245, 248, 255, 0),
                EndColorMax = new Color(255, 255, 255, 0),
                // Snowflakes are 8x8 pixels (32 * 0.25 = 8)
                ScaleMin = new Vector2(0.25f, 0.25f), // 8x8 pixels
                ScaleMax = new Vector2(0.25f, 0.25f),
                EndScaleMin = new Vector2(0.25f, 0.25f),
                EndScaleMax = new Vector2(0.25f, 0.25f),
                LifetimeRange = new Vector2(8.0f, 12.0f), // Extended lifetime to cover full screen and draw over all tiles
                RotationSpeedRange = new Vector2(-1.5f, 1.5f) // Gentle rotation for snowflakes
            };

            _particleSystem.CreateEmitter(emitter);
        }
    }

    /// <summary>
    /// Spawns blizzard particles - intense snow with strong wind effects.
    /// </summary>
    private void SpawnBlizzard()
    {
        var viewport = _graphicsDevice.Viewport;
        
        // Calculate visible area in world space
        var topLeft = _camera.ScreenToWorld(Vector2.Zero, viewport.Width, viewport.Height);
        var bottomRight = _camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height), viewport.Width, viewport.Height);
        
        // Expand spawn area to cover whole screen plus margins (100% wider on each side for blizzard)
        var baseWidth = bottomRight.X - topLeft.X;
        var spawnWidth = baseWidth * 3.0f; // Triple width for full coverage with wind effects
        var spawnHeight = 600.0f; // Larger spawn area for blizzard
        
        // Much more particles for blizzard - intense effect with more chaos (reduced by 20%)
        int particleCount = (int)(180 * WeatherIntensity); // Increased to ensure full coverage

        for (int i = 0; i < particleCount; i++)
        {
            // More chaotic positioning - wider spawn area including far sides (centered on screen)
            var x = topLeft.X - baseWidth * 1.0f + (float)_random.NextDouble() * spawnWidth;
            var y = topLeft.Y - 100.0f - (float)_random.NextDouble() * (spawnHeight + 200.0f);

            // More chaotic wind - wider range and more variation
            var windDirection = (float)(_random.NextDouble() * 2.4 - 1.2); // -1.2 to 1.2 radians (very chaotic)
            var windStrength = (float)(_random.NextDouble() * 0.5 + 0.3); // 0.3 to 0.8 for variable wind
            var windVariation = (float)(_random.NextDouble() * 0.4 - 0.2); // Additional random variation

            // More chaotic velocity ranges
            var baseVelocityX = windDirection * 100.0f;
            var baseVelocityY = 80.0f + (float)_random.NextDouble() * 100.0f; // 80-180 vertical

            var emitter = new ParticleEmitter
            {
                Type = EmitterType.Point,
                Mode = EmissionMode.Burst,
                Position = new Vector2(x, y),
                ParticlesPerBurst = 1,
                Direction = MathF.PI / 2.0f + windDirection * windStrength + windVariation, // Very chaotic direction
                Spread = 1.2f, // Even wider spread for maximum chaos
                VelocityMin = new Vector2(baseVelocityX - 80, baseVelocityY - 30), // More varied horizontal
                VelocityMax = new Vector2(baseVelocityX + 80, baseVelocityY + 50),
                Acceleration = new Vector2(windDirection * 40 + windVariation * 20, 30 + (float)_random.NextDouble() * 20), // Chaotic acceleration
                StartColorMin = new Color(235, 240, 255, 220), // Slightly varied white
                StartColorMax = new Color(255, 255, 255, 255),
                EndColorMin = new Color(235, 240, 255, 0),
                EndColorMax = new Color(255, 255, 255, 0),
                // Vary particle sizes slightly for more chaos
                ScaleMin = new Vector2(0.2f, 0.2f),
                ScaleMax = new Vector2(0.3f, 0.3f),
                EndScaleMin = new Vector2(0.2f, 0.2f),
                EndScaleMax = new Vector2(0.3f, 0.3f),
                LifetimeRange = new Vector2(6.0f, 10.0f), // Extended lifetime to cover full screen and draw over all tiles
                RotationSpeedRange = new Vector2(-5.0f, 5.0f) // Much faster rotation for chaos
            };

            _particleSystem.CreateEmitter(emitter);
        }
    }

    /// <summary>
    /// Normalizes weather probabilities to sum to 100.
    /// </summary>
    public void NormalizeProbabilities()
    {
        var total = _probabilityClear + _probabilityLightRain + _probabilityHeavyRain + 
                   _probabilitySnow + _probabilityBlizzard + _probabilityFog;
        
        if (total <= 0.0f)
        {
            // Set default values
            _probabilityClear = GameConstants.Weather.ProbabilityClear;
            _probabilityLightRain = GameConstants.Weather.ProbabilityLightRain;
            _probabilityHeavyRain = GameConstants.Weather.ProbabilityHeavyRain;
            _probabilityLightningRain = 5.0f;
            _probabilitySnow = GameConstants.Weather.ProbabilitySnow;
            _probabilityBlizzard = 5.0f;
            _probabilityFog = GameConstants.Weather.ProbabilityFog;
            return;
        }

        // Scale all probabilities to sum to 100
        var scale = 100.0f / total;
        _probabilityClear *= scale;
        _probabilityLightRain *= scale;
        _probabilityHeavyRain *= scale;
        _probabilityLightningRain *= scale;
        _probabilitySnow *= scale;
        _probabilityBlizzard *= scale;
        _probabilityFog *= scale;
    }

    /// <summary>
    /// Spawns splash particles at the bottom of the screen where rain hits the ground.
    /// Splashes are aligned to tile positions in isometric space.
    /// </summary>
    private void SpawnRainSplashes()
    {
        if (_tileMap == null)
            return;
            
        var viewport = _graphicsDevice.Viewport;
        
        // Get screen coordinates for bottom of viewport
        var bottomLeftScreen = new Vector2(0, viewport.Height);
        var bottomRightScreen = new Vector2(viewport.Width, viewport.Height);
        
        // Convert screen to camera world space, then to tile coordinates
        var bottomLeftWorld = _camera.ScreenToWorld(bottomLeftScreen, viewport.Width, viewport.Height);
        var bottomRightWorld = _camera.ScreenToWorld(bottomRightScreen, viewport.Width, viewport.Height);
        
        // Convert camera world positions to tile coordinates (isometric)
        var bottomLeftTile = _tileMap.ScreenToWorld(bottomLeftWorld);
        var bottomRightTile = _tileMap.ScreenToWorld(bottomRightWorld);
        
        // Ground level is at the bottom of visible tiles
        var groundTileY = (int)Math.Floor(Math.Max(bottomLeftTile.Y, bottomRightTile.Y));
        var minTileX = (int)Math.Floor(Math.Min(bottomLeftTile.X, bottomRightTile.X));
        var maxTileX = (int)Math.Ceiling(Math.Max(bottomLeftTile.X, bottomRightTile.X));
        
        // Spawn splashes based on rain intensity - more for heavy/lightning rain
        int splashCount = _currentWeather == WeatherType.HeavyRain || _currentWeather == WeatherType.LightningRain 
            ? (int)(8 * WeatherIntensity) 
            : (int)(4 * WeatherIntensity);
        
        float tileWidth = _tileMap.TileWidth;
        float tileHeight = _tileMap.TileHeight;
        
        for (int i = 0; i < splashCount; i++)
        {
            // Random tile X position across visible tiles
            var tileX = minTileX + _random.Next(Math.Max(1, maxTileX - minTileX + 1));
            var tileY = groundTileY;
            
            // Convert tile position to screen position (isometric)
            var tileScreenPos = _tileMap.WorldToScreen(new Vector2(tileX, tileY));
            
            // Convert screen position to camera world space for particle positioning
            var particleWorldPos = _camera.ScreenToWorld(tileScreenPos, viewport.Width, viewport.Height);
            
            // Small random offset for variation
            var x = particleWorldPos.X + (float)(_random.NextDouble() * 20.0 - 10.0);
            var y = particleWorldPos.Y + (float)(_random.NextDouble() * 10.0 - 5.0);
            
            // Create splash particles - small particles that spread outward in isometric space
            // Splash goes "up" in isometric space (decreasing worldY)
            float splashWorldY = -2.0f; // Upward in isometric space
            float splashWorldX = (float)(_random.NextDouble() * 2.0 - 1.0); // Random horizontal
            
            // Convert isometric world velocity to screen velocity, then to camera world velocity
            var splashScreenVelX = -tileWidth / 2.0f * splashWorldY - tileWidth / 2.0f * splashWorldX;
            var splashScreenVelY = tileHeight / 2.0f * splashWorldY - tileHeight / 2.0f * splashWorldX;
            
            // Convert screen velocity to camera world velocity (account for zoom)
            var splashVelX = splashScreenVelX / _camera.Zoom;
            var splashVelY = splashScreenVelY / _camera.Zoom;
            
            var emitter = new ParticleEmitter
            {
                Type = EmitterType.Point,
                Mode = EmissionMode.Burst,
                Position = new Vector2(x, y),
                ParticlesPerBurst = 3, // 3 small particles per splash
                Direction = MathF.PI * 1.5f, // Direction doesn't matter, velocities handle it
                Spread = 1.0f, // Wide spread for splash effect
                VelocityMin = new Vector2(splashVelX - 10, splashVelY - 10),
                VelocityMax = new Vector2(splashVelX + 10, splashVelY + 10),
                Acceleration = new Vector2(0, tileHeight / 2.0f * 2.0f / _camera.Zoom), // Gravity in isometric space
                StartColorMin = new Color(100, 150, 255, 200), // Blue water color
                StartColorMax = new Color(120, 170, 255, 255),
                EndColorMin = new Color(100, 150, 255, 0),
                EndColorMax = new Color(120, 170, 255, 0),
                ScaleMin = new Vector2(0.1f, 0.1f), // Small splash particles
                ScaleMax = new Vector2(0.15f, 0.15f),
                EndScaleMin = new Vector2(0.05f, 0.05f),
                EndScaleMax = new Vector2(0.1f, 0.1f),
                LifetimeRange = new Vector2(0.1f, 0.2f) // Quick fade for splash effect
            };
            
            _particleSystem.CreateEmitter(emitter);
        }
    }
}

