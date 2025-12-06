using GameClient.Services;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace GameClient.UI;

/// <summary>
/// Weather control window that allows toggling weather and viewing current weather.
/// </summary>
public class WeatherWindow
{
    private Panel? _weatherPanel;
    private Label? _currentWeatherLabel;
    private CheckBox? _weatherEnabledCheckBox;
    private HorizontalSlider? _timeOfDaySlider;
    private Label? _timeOfDayLabel;
    private Desktop? _desktop;
    private WeatherSystem? _weatherSystem;
    private DayNightCycleService? _dayNightCycle;
    private bool _isVisible;
    private bool _dayNightCycleWasPausedBeforeOpen;

    public bool IsVisible
    {
        get => _weatherPanel?.Visible ?? _isVisible;
        set
        {
            if (value == _isVisible)
                return; // No change needed

            _isVisible = value;
            if (_weatherPanel != null)
            {
                _weatherPanel.Visible = value;
            }

            // Handle pause/unpause when visibility changes
            if (value)
            {
                // Opening window - pause the day/night cycle
                if (_dayNightCycle != null)
                {
                    _dayNightCycleWasPausedBeforeOpen = _dayNightCycle.IsPaused;
                    _dayNightCycle.IsPaused = true;
                }
            }
            else
            {
                // Closing window - restore previous pause state
                if (_dayNightCycle != null)
                {
                    _dayNightCycle.IsPaused = _dayNightCycleWasPausedBeforeOpen;
                }
            }
        }
    }

    public WeatherWindow()
    {
    }

    /// <summary>
    /// Creates the weather window UI.
    /// </summary>
    public void CreateUI(Desktop desktop)
    {
        _desktop = desktop;
        _weatherSystem = ServiceLocator.Get<WeatherSystem>();
        _dayNightCycle = ServiceLocator.Get<DayNightCycleService>();

        // Create custom panel that looks like a window
        _weatherPanel = new Panel
        {
            Width = 350,
            Height = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(40, 40, 40, 250))
        };

        // Create a vertical container
        var container = new VerticalStackPanel
        {
            Width = 350,
            Height = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Myra.Graphics2D.Thickness(15)
        };

        // Title bar with just close button
        var titleBar = new HorizontalStackPanel
        {
            Width = 350,
            Height = 35,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(60, 60, 60, 255))
        };

        var closeButton = new TextButton
        {
            Text = "×",
            Width = 35,
            Height = 35,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (s, e) =>
        {
            Toggle(); // Use Toggle to ensure proper pause/unpause handling
        };

        titleBar.Widgets.Add(closeButton);

        // Content area
        var contentPanel = new VerticalStackPanel
        {
            Spacing = 15,
            Padding = new Myra.Graphics2D.Thickness(10, 15, 10, 10)
        };

        // Time of Day Section
        var timeOfDayContainer = new VerticalStackPanel
        {
            Spacing = 6
        };

        var timeOfDayTitleLabel = new Label
        {
            Text = "Time of Day",
            TextColor = Color.LightSkyBlue,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var timeDisplayContainer = new HorizontalStackPanel
        {
            Spacing = 10
        };

        _timeOfDayLabel = new Label
        {
            Text = _dayNightCycle?.GetTimeString() ?? "00:00",
            TextColor = Color.Yellow,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 60
        };

        _timeOfDaySlider = new HorizontalSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = _dayNightCycle != null ? (int)(_dayNightCycle.CurrentTimeOfDay * 100) : 0,
            Width = 250,
            Enabled = true
        };

        _timeOfDaySlider.ValueChanged += (s, e) =>
        {
            if (_dayNightCycle != null)
            {
                // Pause the day/night cycle while dragging the slider
                _dayNightCycle.IsPaused = true;
                
                // Update time of day based on slider value
                _dayNightCycle.CurrentTimeOfDay = _timeOfDaySlider.Value / 100.0f;
                if (_dayNightCycle.CurrentTimeOfDay < 0.0f)
                    _dayNightCycle.CurrentTimeOfDay = 0.0f;
                if (_dayNightCycle.CurrentTimeOfDay > 1.0f)
                    _dayNightCycle.CurrentTimeOfDay = 1.0f;

                // Update the time label immediately as slider moves
                if (_timeOfDayLabel != null)
                {
                    _timeOfDayLabel.Text = _dayNightCycle.GetTimeString();
                }
            }
        };

        timeDisplayContainer.Widgets.Add(_timeOfDayLabel);
        timeDisplayContainer.Widgets.Add(_timeOfDaySlider);

        timeOfDayContainer.Widgets.Add(timeOfDayTitleLabel);
        timeOfDayContainer.Widgets.Add(timeDisplayContainer);

        // Current weather display
        var currentWeatherContainer = new HorizontalStackPanel
        {
            Spacing = 10
        };

        var currentWeatherTitleLabel = new Label
        {
            Text = "Current Weather:",
            TextColor = Color.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 140
        };

        _currentWeatherLabel = new Label
        {
            Text = GetWeatherDisplayName(),
            TextColor = Color.Yellow,
            VerticalAlignment = VerticalAlignment.Center
        };

        currentWeatherContainer.Widgets.Add(currentWeatherTitleLabel);
        currentWeatherContainer.Widgets.Add(_currentWeatherLabel);

        // Weather toggle checkbox
        _weatherEnabledCheckBox = new CheckBox
        {
            Text = "Weather Enabled",
            IsChecked = _weatherSystem?.RandomWeatherEnabled ?? true,
            Enabled = true
        };

        _weatherEnabledCheckBox.PressedChanged += (s, e) =>
        {
            if (_weatherSystem != null)
            {
                _weatherSystem.RandomWeatherEnabled = _weatherEnabledCheckBox.IsChecked;
            }
        };

        contentPanel.Widgets.Add(timeOfDayContainer);
        contentPanel.Widgets.Add(currentWeatherContainer);
        contentPanel.Widgets.Add(_weatherEnabledCheckBox);

        container.Widgets.Add(titleBar);
        container.Widgets.Add(contentPanel);
        _weatherPanel.Widgets.Add(container);

        // Add weather panel to desktop root panel
        if (desktop.Root is Panel rootPanel)
        {
            rootPanel.Widgets.Add(_weatherPanel);
        }
        else
        {
            var newRoot = new Panel
            {
                Width = desktop.Root?.Width ?? GameCore.Configuration.Configuration.WindowWidth,
                Height = desktop.Root?.Height ?? GameCore.Configuration.Configuration.WindowHeight
            };
            if (desktop.Root != null)
            {
                newRoot.Widgets.Add(desktop.Root);
            }
            newRoot.Widgets.Add(_weatherPanel);
            desktop.Root = newRoot;
        }
    }

    /// <summary>
    /// Updates the weather display to show current weather and time of day.
    /// </summary>
    public void UpdateDisplay()
    {
        if (_currentWeatherLabel != null && _weatherSystem != null)
        {
            _currentWeatherLabel.Text = GetWeatherDisplayName();
        }

        if (_timeOfDayLabel != null && _dayNightCycle != null)
        {
            _timeOfDayLabel.Text = _dayNightCycle.GetTimeString();
            
            // Update slider value if cycle is running
            if (_timeOfDaySlider != null && !_dayNightCycle.IsPaused)
            {
                var sliderValue = (int)(_dayNightCycle.CurrentTimeOfDay * 100.0f);
                if (Math.Abs(_timeOfDaySlider.Value - sliderValue) <= 2)
                {
                    _timeOfDaySlider.Value = sliderValue;
                }
            }
        }
    }

    /// <summary>
    /// Toggles the weather window visibility.
    /// Pauses the game when opening and unpauses when closing.
    /// </summary>
    public void Toggle()
    {
        var wasVisible = IsVisible;
        IsVisible = !IsVisible;

        if (IsVisible && !wasVisible)
        {
            // Window is opening - ensure panel is added and brought to front
            // Pause/unpause is handled by IsVisible setter

            // Ensure panel is added back to the desktop if it was removed
            if (_weatherPanel != null && _weatherPanel.Parent == null && _desktop != null)
            {
                if (_desktop.Root is Panel rootPanel)
                {
                    rootPanel.Widgets.Add(_weatherPanel);
                }
            }

            // Bring panel to front
            if (_weatherPanel != null && _weatherPanel.Parent is Panel parentPanel)
            {
                parentPanel.Widgets.Remove(_weatherPanel);
                parentPanel.Widgets.Add(_weatherPanel);
            }

            UpdateDisplay();
            
            // Initialize slider to current time of day value
            if (_timeOfDaySlider != null && _dayNightCycle != null)
            {
                _timeOfDaySlider.Value = (int)(_dayNightCycle.CurrentTimeOfDay * 100.0f);
            }
        }
        // Closing pause/unpause is handled by IsVisible setter
    }

    private string GetWeatherDisplayName()
    {
        if (_weatherSystem == null)
            return "Unknown";

        return _weatherSystem.CurrentWeather switch
        {
            WeatherType.Clear => "Clear",
            WeatherType.LightRain => "Light Rain",
            WeatherType.HeavyRain => "Heavy Rain",
            WeatherType.LightningRain => "Lightning Rain",
            WeatherType.Snow => "Snow",
            WeatherType.Blizzard => "Blizzard",
            WeatherType.Fog => "Fog",
            _ => "Unknown"
        };
    }
}

