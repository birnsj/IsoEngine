using GameCore.Combat;
using GameCore.Entities;
using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Service that handles combat between entities.
/// </summary>
public class CombatService : IGameService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the CombatService class.
    /// </summary>
    /// <param name="logger">The logger service. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
    public CombatService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Initialize()
    {
        // Initialization complete via constructor injection
    }

    public void Update(GameTime gameTime)
    {
        // Combat service doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        // Combat service doesn't need rendering
    }

    /// <summary>
    /// Performs a melee attack from attacker to target.
    /// </summary>
    /// <param name="attacker">The entity performing the attack.</param>
    /// <param name="target">The entity being attacked.</param>
    /// <returns>True if the attack was successful, false otherwise.</returns>
    public bool PerformMeleeAttack(Entity attacker, Entity target)
    {
        if (attacker == null || target == null)
        {
            _logger.Warning("Cannot perform attack: attacker or target is null");
            return false;
        }

        // Check if entities have stats components
        if (!attacker.HasComponent<StatsComponent>() || !target.HasComponent<StatsComponent>())
        {
            _logger.Warning("Cannot perform attack: attacker or target missing StatsComponent");
            return false;
        }

        var attackerStats = attacker.GetComponent<StatsComponent>();
        var targetStats = target.GetComponent<StatsComponent>();

        if (attackerStats == null || targetStats == null)
        {
            _logger.Warning("Cannot perform attack: failed to get StatsComponent");
            return false;
        }

        // Check if target is already dead
        if (targetStats.IsDead)
        {
            _logger.Debug("Cannot attack: target is already dead");
            return false;
        }

        // Calculate and apply damage
        var damage = attackerStats.AttackPower;
        var actualDamage = targetStats.ApplyDamage(damage);

        _logger.Info($"{attacker.GetType().Name} attacks {target.GetType().Name} for {actualDamage} damage (had {damage} attack power, target had {targetStats.Defense} defense)");

        // Trigger hit flash if target supports it
        if (target is IFlashable flashable)
        {
            flashable.TriggerHitFlash();
        }

        // Check if target died
        if (targetStats.IsDead)
        {
            _logger.Info($"{target.GetType().Name} has been defeated!");
        }

        return true;
    }

    /// <summary>
    /// Calculates the damage that would be dealt without applying it.
    /// </summary>
    /// <param name="attacker">The attacking entity.</param>
    /// <param name="target">The target entity.</param>
    /// <returns>The damage that would be dealt.</returns>
    public int CalculateDamage(Entity attacker, Entity target)
    {
        if (attacker == null || target == null)
            return 0;

        if (!attacker.HasComponent<StatsComponent>() || !target.HasComponent<StatsComponent>())
            return 0;

        var attackerStats = attacker.GetComponent<StatsComponent>();
        var targetStats = target.GetComponent<StatsComponent>();

        if (attackerStats == null || targetStats == null)
            return 0;

        return Math.Max(1, attackerStats.AttackPower - targetStats.Defense);
    }
}

