using GameCore.Entities;
using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Manages and renders particle effects in the game.
/// Supports object pooling for performance and multiple emitter types.
/// </summary>
public class ParticleSystem : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _defaultParticleTexture;

    // Active particles and emitters
    private readonly List<Particle> _activeParticles = new();
    private readonly List<EmitterInstance> _activeEmitters = new();
    
    // Track blood particles by enemy (for fading on death)
    private readonly Dictionary<Entity, List<Particle>> _bloodParticlesByEnemy = new();

    // Object pooling
    private readonly Queue<Particle> _particlePool = new();
    private const int InitialPoolSize = 100;
    private const int MaxPoolSize = 1000;

    // Default textures for different particle types
    private Texture2D? _circleTexture;
    private Texture2D? _squareTexture;

    /// <summary>
    /// Gets or sets the default particle texture to use when none is specified.
    /// </summary>
    public Texture2D? DefaultParticleTexture
    {
        get => _defaultParticleTexture ?? _circleTexture;
        set => _defaultParticleTexture = value;
    }

    public ParticleSystem(Camera2D camera)
    {
        _camera = camera;
    }

    public void Initialize()
    {
        // Pre-allocate particle pool
        for (int i = 0; i < InitialPoolSize; i++)
        {
            _particlePool.Enqueue(new Particle());
        }
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update emitters and spawn new particles
        for (int i = _activeEmitters.Count - 1; i >= 0; i--)
        {
            var emitterInstance = _activeEmitters[i];
            var particlesToEmit = emitterInstance.Emitter.Update(deltaTime, out bool isActive);

            // Emit new particles
            for (int j = 0; j < particlesToEmit; j++)
            {
                var particle = GetPooledParticle();
                emitterInstance.Emitter.InitializeParticle(particle);
                _activeParticles.Add(particle);
                
                // Track blood particles by associated entity
                if (emitterInstance.AssociatedEntity != null)
                {
                    if (!_bloodParticlesByEnemy.TryGetValue(emitterInstance.AssociatedEntity, out var particleList))
                    {
                        particleList = new List<Particle>();
                        _bloodParticlesByEnemy[emitterInstance.AssociatedEntity] = particleList;
                    }
                    particleList.Add(particle);
                }
            }

            // Remove inactive emitters
            if (!isActive)
            {
                _activeEmitters.RemoveAt(i);
            }
        }

        // Update particles and return expired ones to pool
        for (int i = _activeParticles.Count - 1; i >= 0; i--)
        {
            var particle = _activeParticles[i];
            particle.Update(deltaTime);

            if (particle.IsExpired)
            {
                _activeParticles.RemoveAt(i);
                ReturnParticleToPool(particle);
                
                // Remove from blood particle tracking
                var keysToRemove = new List<Entity>();
                foreach (var kvp in _bloodParticlesByEnemy)
                {
                    if (kvp.Value.Remove(particle))
                    {
                        if (kvp.Value.Count == 0)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }
                foreach (var key in keysToRemove)
                {
                    _bloodParticlesByEnemy.Remove(key);
                }
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || DefaultParticleTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            sortMode: SpriteSortMode.FrontToBack);

        foreach (var particle in _activeParticles)
        {
            if (particle.IsExpired)
                continue;

            var texture = DefaultParticleTexture;
            var sourceRect = particle.SourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            var color = particle.CurrentColor;
            var scale = particle.CurrentScale;
            var origin = new Vector2(sourceRect.Width / 2.0f, sourceRect.Height / 2.0f);
            var position = particle.Position;

            _spriteBatch.Draw(
                texture,
                position,
                sourceRect,
                color,
                particle.Rotation,
                origin,
                scale,
                SpriteEffects.None,
                0.0f);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Creates a particle emitter at the specified position.
    /// </summary>
    /// <param name="emitter">The emitter configuration.</param>
    /// <param name="associatedEntity">Optional entity to associate with these particles (for death fading).</param>
    /// <returns>The emitter instance ID (for future reference if needed).</returns>
    public int CreateEmitter(ParticleEmitter emitter, Entity? associatedEntity = null)
    {
        emitter.Reset();
        var instance = new EmitterInstance
        {
            Emitter = emitter,
            Id = _activeEmitters.Count,
            AssociatedEntity = associatedEntity
        };
        _activeEmitters.Add(instance);
        return instance.Id;
    }
    
    /// <summary>
    /// Stops all particles associated with a dead enemy (freezes them in place).
    /// </summary>
    /// <param name="enemy">The enemy that died.</param>
    public void StopParticlesForEnemy(Entity enemy)
    {
        // Stop all particles associated with this enemy
        if (_bloodParticlesByEnemy.TryGetValue(enemy, out var particles))
        {
            // Stop movement for all particles (freeze them in place)
            foreach (var particle in particles)
            {
                // Stop velocity and acceleration so particles freeze in place
                particle.Velocity = Vector2.Zero;
                particle.Acceleration = Vector2.Zero;
                particle.RotationSpeed = 0.0f; // Also stop rotation
            }
            
            // Keep particles in the tracking list so they continue to render
            // They will naturally expire based on their lifetime
        }
        
        // Stop any active emitters associated with this enemy
        for (int i = _activeEmitters.Count - 1; i >= 0; i--)
        {
            if (_activeEmitters[i].AssociatedEntity == enemy)
            {
                _activeEmitters.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Immediately removes all particles associated with a dead enemy.
    /// </summary>
    /// <param name="enemy">The enemy that died.</param>
    public void RemoveParticlesForEnemy(Entity enemy)
    {
        // Remove all particles associated with this enemy
        if (_bloodParticlesByEnemy.TryGetValue(enemy, out var particles))
        {
            // Immediately expire all particles for this enemy
            foreach (var particle in particles.ToList()) // ToList to avoid modification during iteration
            {
                // Mark particle as expired so it will be removed in the next Update
                particle.Lifetime = particle.MaxLifetime; // Set to max lifetime to mark as expired
                
                // Also remove from active particles list immediately
                _activeParticles.Remove(particle);
                ReturnParticleToPool(particle);
            }
            
            // Clear the tracking list
            _bloodParticlesByEnemy.Remove(enemy);
        }
        
        // Also remove any active emitters associated with this enemy
        for (int i = _activeEmitters.Count - 1; i >= 0; i--)
        {
            if (_activeEmitters[i].AssociatedEntity == enemy)
            {
                _activeEmitters.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Fades out all blood particles associated with a dead enemy.
    /// Call this each frame for dead enemies to continuously fade particles.
    /// </summary>
    /// <param name="enemy">The enemy that died.</param>
    /// <param name="deltaTime">Time elapsed since last frame.</param>
    public void FadeBloodParticlesForEnemy(Entity enemy, float deltaTime)
    {
        if (!_bloodParticlesByEnemy.TryGetValue(enemy, out var particles))
            return;
            
        // Fade out all blood particles for this enemy (fade speed: 2.0 per second)
        var fadeSpeed = 2.0f; // How fast to fade (multiplier reduction per second)
        
        foreach (var particle in particles.ToList()) // ToList to avoid modification during iteration
        {
            // Gradually fade out over time
            particle.FadeMultiplier = Math.Max(0.0f, particle.FadeMultiplier - fadeSpeed * deltaTime);
        }
    }

    /// <summary>
    /// Creates a simple burst of particles at the specified position.
    /// </summary>
    public void EmitBurst(Vector2 position, int count, Color color, float speed = 100.0f, float lifetime = 1.0f)
    {
        var emitter = new ParticleEmitter
        {
            Type = EmitterType.Point,
            Mode = EmissionMode.Burst,
            Position = position,
            ParticlesPerBurst = count,
            Direction = 0.0f,
            Spread = MathF.PI * 2.0f, // Full circle
            VelocityMin = new Vector2(-speed, -speed),
            VelocityMax = new Vector2(speed, speed),
            StartColorMin = color,
            StartColorMax = color,
            EndColorMin = Color.Transparent,
            EndColorMax = Color.Transparent,
            ScaleMin = new Vector2(0.5f, 0.5f),
            ScaleMax = new Vector2(1.0f, 1.0f),
            EndScaleMin = new Vector2(0.0f, 0.0f),
            EndScaleMax = new Vector2(0.0f, 0.0f),
            LifetimeRange = new Vector2(lifetime, lifetime)
        };

        CreateEmitter(emitter);
    }

    /// <summary>
    /// Sets up the particle system with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create default particle textures
        _circleTexture = CreateCircleTexture(graphicsDevice, 32);
        _squareTexture = CreateSquareTexture(graphicsDevice, 32);
        _defaultParticleTexture = _circleTexture;
    }

    /// <summary>
    /// Gets a particle from the pool or creates a new one.
    /// </summary>
    private Particle GetPooledParticle()
    {
        if (_particlePool.Count > 0)
        {
            var particle = _particlePool.Dequeue();
            particle.Reset();
            return particle;
        }

        // Pool is empty, create new particle
        return new Particle();
    }

    /// <summary>
    /// Returns a particle to the pool for reuse.
    /// </summary>
    private void ReturnParticleToPool(Particle particle)
    {
        if (_particlePool.Count < MaxPoolSize)
        {
            particle.Reset();
            _particlePool.Enqueue(particle);
        }
    }

    /// <summary>
    /// Creates a circular texture for particles.
    /// </summary>
    private Texture2D CreateCircleTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        var center = size / 2.0f;
        var radius = size / 2.0f - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance <= radius)
                {
                    // Smooth edge fade
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

    /// <summary>
    /// Creates a square texture for particles.
    /// </summary>
    private Texture2D CreateSquareTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Create a square with soft edges
                var edgeDistance = Math.Min(
                    Math.Min(x, size - 1 - x),
                    Math.Min(y, size - 1 - y)
                );
                var alpha = Math.Min(1.0f, edgeDistance / 2.0f);
                data[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
            }
        }

        texture.SetData(data);
        return texture;
    }

    /// <summary>
    /// Clears all active particles and emitters.
    /// </summary>
    public void Clear()
    {
        foreach (var particle in _activeParticles)
        {
            ReturnParticleToPool(particle);
        }
        _activeParticles.Clear();
        _activeEmitters.Clear();
    }

    /// <summary>
    /// Gets the number of active particles.
    /// </summary>
    public int ActiveParticleCount => _activeParticles.Count;

    /// <summary>
    /// Gets the number of active emitters.
    /// </summary>
    public int ActiveEmitterCount => _activeEmitters.Count;
}

/// <summary>
/// Internal class to track emitter instances.
/// </summary>
internal class EmitterInstance
{
    public ParticleEmitter Emitter { get; set; } = null!;
    public int Id { get; set; }
    public Entity? AssociatedEntity { get; set; }
}

