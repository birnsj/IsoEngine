namespace GameCore.Combat;

/// <summary>
/// Component that manages entity statistics like health, attack, and defense.
/// </summary>
public class StatsComponent
{
    /// <summary>
    /// Gets or sets the maximum health.
    /// </summary>
    public int MaxHealth { get; set; }

    /// <summary>
    /// Gets or sets the current health.
    /// </summary>
    public int CurrentHealth { get; set; }

    /// <summary>
    /// Gets or sets the attack power (damage dealt).
    /// </summary>
    public int AttackPower { get; set; }

    /// <summary>
    /// Gets or sets the defense (damage reduction).
    /// </summary>
    public int Defense { get; set; }

    /// <summary>
    /// Gets whether the entity is dead (health <= 0).
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    /// <summary>
    /// Gets the health percentage (0.0 to 1.0).
    /// </summary>
    public float HealthPercentage => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0.0f;

    /// <summary>
    /// Initializes a new instance of the StatsComponent class.
    /// </summary>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="attackPower">Attack power.</param>
    /// <param name="defense">Defense value.</param>
    public StatsComponent(int maxHealth, int attackPower = 10, int defense = 0)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        AttackPower = attackPower;
        Defense = defense;
    }

    /// <summary>
    /// Applies damage to this entity.
    /// </summary>
    /// <param name="amount">The amount of damage to apply (before defense).</param>
    /// <returns>The actual damage dealt after defense.</returns>
    public int ApplyDamage(int amount)
    {
        // Calculate damage after defense (minimum 1 damage)
        var actualDamage = Math.Max(1, amount - Defense);
        CurrentHealth = Math.Max(0, CurrentHealth - actualDamage);
        return actualDamage;
    }

    /// <summary>
    /// Heals the entity.
    /// </summary>
    /// <param name="amount">The amount of health to restore.</param>
    /// <returns>The actual amount healed.</returns>
    public int Heal(int amount)
    {
        var oldHealth = CurrentHealth;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        return CurrentHealth - oldHealth;
    }

    /// <summary>
    /// Fully restores health.
    /// </summary>
    public void RestoreFullHealth()
    {
        CurrentHealth = MaxHealth;
    }
}

