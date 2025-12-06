using Microsoft.Xna.Framework;

namespace GameClient.Rendering;

/// <summary>
/// Represents a projectile (bullet) fired by the player.
/// </summary>
public class Projectile
{
    /// <summary>
    /// Gets or sets the position of the projectile in world coordinates.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Gets or sets the velocity of the projectile.
    /// </summary>
    public Vector2 Velocity { get; set; }

    /// <summary>
    /// Gets or sets the damage the projectile deals.
    /// </summary>
    public int Damage { get; set; }

    /// <summary>
    /// Gets or sets the maximum distance the projectile can travel before being removed.
    /// </summary>
    public float MaxDistance { get; set; }

    /// <summary>
    /// Gets or sets the starting position (used to calculate distance traveled).
    /// </summary>
    public Vector2 StartPosition { get; set; }

    /// <summary>
    /// Gets or sets whether the projectile has hit something and should be removed.
    /// </summary>
    public bool HasHit { get; set; }

    /// <summary>
    /// Gets the distance traveled from the start position.
    /// </summary>
    public float DistanceTraveled => Vector2.Distance(StartPosition, Position);

    /// <summary>
    /// Gets whether the projectile has exceeded its maximum range.
    /// </summary>
    public bool IsExpired => HasHit || DistanceTraveled >= MaxDistance;

    /// <summary>
    /// Gets or sets the size of the projectile for collision detection.
    /// </summary>
    public float Size { get; set; } = 8.0f;

    /// <summary>
    /// Gets whether this projectile was fired by an enemy (hits player) or by the player (hits enemies).
    /// </summary>
    public bool IsEnemyProjectile { get; set; }

    public Projectile(Vector2 position, Vector2 velocity, int damage, float maxDistance, bool isEnemyProjectile = false)
    {
        Position = position;
        StartPosition = position;
        Velocity = velocity;
        Damage = damage;
        MaxDistance = maxDistance;
        HasHit = false;
        IsEnemyProjectile = isEnemyProjectile;
    }
}

