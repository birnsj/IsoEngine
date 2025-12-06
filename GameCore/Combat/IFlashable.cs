namespace GameCore.Combat;

/// <summary>
/// Interface for entities that can flash when hit.
/// </summary>
public interface IFlashable
{
    /// <summary>
    /// Triggers a hit flash effect.
    /// </summary>
    void TriggerHitFlash();
}

