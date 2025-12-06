namespace GameCore.Configuration;

/// <summary>
/// Configuration settings for visual effects system.
/// </summary>
public class VFXSettings
{
    /// <summary>
    /// Gets or sets whether particle effects are enabled.
    /// </summary>
    public bool EnableParticles { get; set; } = true;

    /// <summary>
    /// Gets or sets the particle quality level (0 = Low, 1 = Medium, 2 = High).
    /// </summary>
    public int ParticleQuality { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of active particles.
    /// </summary>
    public int MaxParticles { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether screen shake is enabled.
    /// </summary>
    public bool EnableScreenShake { get; set; } = true;

    /// <summary>
    /// Gets or sets the screen shake intensity multiplier (0.0 to 1.0).
    /// </summary>
    public float ScreenShakeIntensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether screen effects (flashes, fades) are enabled.
    /// </summary>
    public bool EnableScreenEffects { get; set; } = true;

    /// <summary>
    /// Gets or sets whether damage numbers are enabled.
    /// </summary>
    public bool EnableDamageNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether hit indicators are enabled.
    /// </summary>
    public bool EnableHitIndicators { get; set; } = true;

    /// <summary>
    /// Gets or sets whether health bar animations are enabled.
    /// </summary>
    public bool EnableHealthBarAnimations { get; set; } = true;

    /// <summary>
    /// Gets or sets whether death effects are enabled.
    /// </summary>
    public bool EnableDeathEffects { get; set; } = true;

    /// <summary>
    /// Gets or sets whether footstep particles are enabled.
    /// </summary>
    public bool EnableFootstepParticles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether trail effects are enabled.
    /// </summary>
    public bool EnableTrails { get; set; } = true;

    /// <summary>
    /// Gets or sets whether interaction highlights are enabled.
    /// </summary>
    public bool EnableInteractionHighlights { get; set; } = true;

    /// <summary>
    /// Gets or sets whether path indicators are enabled.
    /// </summary>
    public bool EnablePathIndicators { get; set; } = true;

    /// <summary>
    /// Gets the particle emission rate multiplier based on quality level.
    /// </summary>
    public float ParticleEmissionMultiplier
    {
        get
        {
            return ParticleQuality switch
            {
                0 => 0.5f,  // Low: 50% particles
                1 => 0.75f, // Medium: 75% particles
                2 => 1.0f,  // High: 100% particles
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Creates default VFX settings.
    /// </summary>
    public static VFXSettings Default => new VFXSettings();

    /// <summary>
    /// Creates low quality VFX settings for performance.
    /// </summary>
    public static VFXSettings LowQuality => new VFXSettings
    {
        EnableParticles = true,
        ParticleQuality = 0,
        MaxParticles = 500,
        EnableScreenShake = true,
        ScreenShakeIntensity = 0.5f,
        EnableScreenEffects = true,
        EnableDamageNumbers = true,
        EnableHitIndicators = true,
        EnableHealthBarAnimations = true,
        EnableDeathEffects = true,
        EnableFootstepParticles = false,
        EnableTrails = false,
        EnableInteractionHighlights = true,
        EnablePathIndicators = false
    };

    /// <summary>
    /// Creates high quality VFX settings.
    /// </summary>
    public static VFXSettings HighQuality => new VFXSettings
    {
        EnableParticles = true,
        ParticleQuality = 2,
        MaxParticles = 2000,
        EnableScreenShake = true,
        ScreenShakeIntensity = 1.0f,
        EnableScreenEffects = true,
        EnableDamageNumbers = true,
        EnableHitIndicators = true,
        EnableHealthBarAnimations = true,
        EnableDeathEffects = true,
        EnableFootstepParticles = true,
        EnableTrails = true,
        EnableInteractionHighlights = true,
        EnablePathIndicators = true
    };
}

