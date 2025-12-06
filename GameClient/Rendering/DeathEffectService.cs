using GameCore.Entities;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Manages death effects for entities (particle bursts, fade out animations).
/// </summary>
public class DeathEffectService : IGameService
{
    private readonly ParticleSystem _particleSystem;
    private readonly Dictionary<Entity, DeathEffectState> _dyingEntities = new();

    public DeathEffectService(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
    }

    public void Initialize()
    {
        // No initialization needed
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update death effects
        var expiredEntities = new List<Entity>();
        foreach (var kvp in _dyingEntities)
        {
            var entity = kvp.Key;
            var state = kvp.Value;

            state.Lifetime += deltaTime;

            // Fade out entity
            if (state.FadeDuration > 0)
            {
                state.CurrentAlpha = Math.Max(0.0f, 1.0f - (state.Lifetime / state.FadeDuration));
            }

            // Remove when effect is complete
            if (state.Lifetime >= state.TotalDuration)
            {
                expiredEntities.Add(entity);
            }
        }

        // Clean up expired effects
        foreach (var entity in expiredEntities)
        {
            _dyingEntities.Remove(entity);
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Death effects don't need their own draw - they modify entity rendering
    }

    /// <summary>
    /// Triggers a death effect for an entity.
    /// </summary>
    public void TriggerDeathEffect(Entity entity, Color? entityColor = null)
    {
        if (entity == null)
            return;

        // Determine entity color if not provided
        var color = entityColor ?? GetEntityColor(entity);

        // Create particle burst
        var deathEmitter = ParticlePreset.DeathEffect(entity.Position, color, 1.0f);
        _particleSystem.CreateEmitter(deathEmitter);

        // Track entity for fade out
        _dyingEntities[entity] = new DeathEffectState
        {
            FadeDuration = 0.5f,
            TotalDuration = 0.5f,
            CurrentAlpha = 1.0f
        };
    }

    /// <summary>
    /// Gets the current alpha for a dying entity (for fade out effect).
    /// </summary>
    public float GetEntityAlpha(Entity entity)
    {
        if (_dyingEntities.TryGetValue(entity, out var state))
        {
            return state.CurrentAlpha;
        }
        return 1.0f;
    }

    /// <summary>
    /// Checks if an entity is currently dying (has active death effect).
    /// </summary>
    public bool IsEntityDying(Entity entity)
    {
        return _dyingEntities.ContainsKey(entity);
    }

    /// <summary>
    /// Gets the color associated with an entity type.
    /// </summary>
    private Color GetEntityColor(Entity entity)
    {
        return entity switch
        {
            GameCore.Entities.Player => Color.Red,
            GameClient.Entities.Enemy => Color.Orange,
            _ => Color.White
        };
    }
}

/// <summary>
/// Tracks the death effect state for an entity.
/// </summary>
internal class DeathEffectState
{
    public float Lifetime { get; set; }
    public float FadeDuration { get; set; }
    public float TotalDuration { get; set; }
    public float CurrentAlpha { get; set; } = 1.0f;
}

