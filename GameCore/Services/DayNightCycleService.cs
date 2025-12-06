using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Service that manages the day/night cycle, tracking time of day and providing ambient lighting colors.
/// </summary>
public class DayNightCycleService : IGameService
{
    private readonly GameTimeManager _timeManager;
    private bool _isPaused;

    /// <summary>
    /// Gets or sets the current time of day (0.0 to 1.0, where 0.0 = midnight, 0.5 = noon, 1.0 = midnight).
    /// </summary>
    public float CurrentTimeOfDay { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the day duration in seconds (time for a full cycle from midnight to midnight).
    /// </summary>
    public float DayDuration { get; set; } = GameConstants.DayNight.DefaultDayDuration;

    /// <summary>
    /// Gets or sets whether time progression is paused.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        set => _isPaused = value;
    }

    /// <summary>
    /// Gets or sets the dawn start time (0.0 to 1.0).
    /// </summary>
    public float DawnStart { get; set; } = GameConstants.DayNight.DawnStart;

    /// <summary>
    /// Gets or sets the dawn end time (0.0 to 1.0).
    /// </summary>
    public float DawnEnd { get; set; } = GameConstants.DayNight.DawnEnd;

    /// <summary>
    /// Gets or sets the dusk start time (0.0 to 1.0).
    /// </summary>
    public float DuskStart { get; set; } = GameConstants.DayNight.DuskStart;

    /// <summary>
    /// Gets or sets the dusk end time (0.0 to 1.0).
    /// </summary>
    public float DuskEnd { get; set; } = GameConstants.DayNight.DuskEnd;

    public DayNightCycleService(GameTimeManager timeManager)
    {
        _timeManager = timeManager ?? throw new ArgumentNullException(nameof(timeManager));
    }

    public void Initialize()
    {
        // No special initialization needed
    }

    public void Update(GameTime gameTime)
    {
        if (_isPaused)
            return;

        // Advance time (normalized 0.0 to 1.0)
        CurrentTimeOfDay += _timeManager.DeltaTime / DayDuration;
        if (CurrentTimeOfDay >= 1.0f)
            CurrentTimeOfDay -= 1.0f;
    }

    public void Draw(GameTime gameTime)
    {
        // No rendering needed
    }

    /// <summary>
    /// Gets the ambient light color based on the current time of day.
    /// Uses smooth interpolation across the entire day/night cycle to prevent white screen.
    /// </summary>
    public Color GetAmbientColor()
    {
        float normalizedTime = CurrentTimeOfDay;
        
        // Define colors for different times of day
        // Night: blue (R:38, G:38, B:53) - 25% darker than previous
        Color nightColor = new Color(38, 38, 53, 255);
        
        // Day: neutral bright (R:61, G:61, B:61) - 15% darker than before (72 * 0.85 = 61)
        Color dayColor = new Color(61, 61, 61, 255);
        
        // Dawn/Dusk: warm orange (R:100, G:70, B:50)
        Color dawnDuskColor = new Color(100, 70, 50, 255);
        
        // Handle wrap-around (night at end of cycle)
        if (normalizedTime > DuskEnd)
        {
            // Late night (0.8-1.0) - darker blue
            return nightColor;
        }
        else if (normalizedTime < DawnStart)
        {
            // Early night (0.0-0.2) - darker blue
            return nightColor;
        }
        else if (normalizedTime >= DawnStart && normalizedTime <= DawnEnd)
        {
            // Dawn transition: night (blue) -> orange -> day (white)
            var t = (normalizedTime - DawnStart) / (DawnEnd - DawnStart);
            // Smooth curve for better transition (ease-in-out)
            t = SmoothStep(t);
            
            // First half: night to orange
            if (t < 0.5f)
            {
                var t1 = t * 2.0f;
                return Color.Lerp(nightColor, dawnDuskColor, t1);
            }
            // Second half: orange to day
            else
            {
                var t2 = (t - 0.5f) * 2.0f;
                return Color.Lerp(dawnDuskColor, dayColor, t2);
            }
        }
        else if (normalizedTime > DawnEnd && normalizedTime < DuskStart)
        {
            // Day time - neutral
            return dayColor;
        }
        else // Dusk
        {
            // Dusk transition: day (white) -> orange -> night (blue)
            var t = (normalizedTime - DuskStart) / (DuskEnd - DuskStart);
            // Smooth curve for better transition (ease-in-out)
            t = SmoothStep(t);
            
            // First half: day to orange
            if (t < 0.5f)
            {
                var t1 = t * 2.0f;
                return Color.Lerp(dayColor, dawnDuskColor, t1);
            }
            // Second half: orange to night
            else
            {
                var t2 = (t - 0.5f) * 2.0f;
                return Color.Lerp(dawnDuskColor, nightColor, t2);
            }
        }
    }
    
    /// <summary>
    /// Smooth step function for easing transitions (ease-in-out curve).
    /// </summary>
    private float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    /// <summary>
    /// Gets the sky color tint for screen overlay effects.
    /// Returns colors with proper hues for visual effects.
    /// </summary>
    public Color GetSkyTint()
    {
        if (IsDay())
            return Color.White;
        else if (IsDawn())
        {
            var t = (CurrentTimeOfDay - DawnStart) / (DawnEnd - DawnStart);
            t = SmoothStep(t);
            // Dawn: blue night -> orange -> white day
            if (t < 0.5f)
            {
                var t1 = t * 2.0f;
                return Color.Lerp(new Color(90, 90, 120, 255), new Color(255, 180, 120, 255), t1);
            }
            else
            {
                var t2 = (t - 0.5f) * 2.0f;
                return Color.Lerp(new Color(255, 180, 120, 255), Color.White, t2);
            }
        }
        else if (IsDusk())
        {
            var t = (CurrentTimeOfDay - DuskStart) / (DuskEnd - DuskStart);
            t = SmoothStep(t);
            // Dusk: white day -> orange -> blue night
            if (t < 0.5f)
            {
                var t1 = t * 2.0f;
                return Color.Lerp(Color.White, new Color(255, 180, 120, 255), t1);
            }
            else
            {
                var t2 = (t - 0.5f) * 2.0f;
                return Color.Lerp(new Color(255, 180, 120, 255), new Color(90, 90, 120, 255), t2);
            }
        }
        else
            return new Color(90, 90, 120, 255); // 25% darker blue night tint
    }

    /// <summary>
    /// Returns true if it's currently day time.
    /// </summary>
    public bool IsDay() => CurrentTimeOfDay > DawnEnd && CurrentTimeOfDay < DuskStart;

    /// <summary>
    /// Returns true if it's currently night time.
    /// </summary>
    public bool IsNight() => CurrentTimeOfDay < DawnStart || CurrentTimeOfDay > DuskEnd;

    /// <summary>
    /// Returns true if it's currently dawn.
    /// </summary>
    public bool IsDawn() => CurrentTimeOfDay >= DawnStart && CurrentTimeOfDay <= DawnEnd;

    /// <summary>
    /// Returns true if it's currently dusk.
    /// </summary>
    public bool IsDusk() => CurrentTimeOfDay >= DuskStart && CurrentTimeOfDay <= DuskEnd;

    /// <summary>
    /// Gets the current time formatted as hours and minutes (24-hour format).
    /// </summary>
    public string GetTimeString()
    {
        var hours = (int)(CurrentTimeOfDay * 24.0f);
        var minutes = (int)((CurrentTimeOfDay * 24.0f - hours) * 60.0f);
        return $"{hours:D2}:{minutes:D2}";
    }
}

