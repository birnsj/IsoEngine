using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;
using System.Drawing;
using System.Windows.Forms;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing day/night cycle and weather system properties.
/// </summary>
public partial class WeatherCycleEditorForm : Form
{
    private TabControl? _tabControl;
    private WeatherCycleData _data = new();
    private bool _isUpdating = false;

    // Day/Night Cycle Tab Controls
    private NumericUpDown? _dayDurationNumeric;
    private TrackBar? _timeOfDayTrackBar;
    private Label? _timeOfDayLabel;
    private CheckBox? _pauseTimeCheckBox;
    private Button? _resetToMidnightButton;
    private Panel? _ambientColorPreview;
    private NumericUpDown? _dawnStartNumeric;
    private NumericUpDown? _dawnEndNumeric;
    private NumericUpDown? _duskStartNumeric;
    private NumericUpDown? _duskEndNumeric;

    // Weather System Tab Controls
    private ComboBox? _currentWeatherComboBox;
    private TrackBar? _weatherIntensityTrackBar;
    private Label? _weatherIntensityLabel;
    private Button? _changeWeatherRandomlyButton;
    private NumericUpDown? _minChangeIntervalNumeric;
    private NumericUpDown? _maxChangeIntervalNumeric;
    private NumericUpDown? _probabilityClearNumeric;
    private NumericUpDown? _probabilityLightRainNumeric;
    private NumericUpDown? _probabilityHeavyRainNumeric;
    private NumericUpDown? _probabilitySnowNumeric;
    private NumericUpDown? _probabilityFogNumeric;
    private Label? _probabilitySumLabel;
    private Button? _normalizeProbabilitiesButton;

    public WeatherCycleEditorForm()
    {
        InitializeComponent();
        PathHelper.ValidateGameContentPath();
        LoadData();
    }

    private void InitializeComponent()
    {
        Text = "Weather Cycle Editor";
        MinimumSize = new Size(800, 600);

        // Toolbar
        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top
        };

        var saveButton = new ToolStripButton("Save", null, (s, e) => SaveData());
        var reloadButton = new ToolStripButton("Reload", null, (s, e) => ReloadData());
        toolStrip.Items.Add(saveButton);
        toolStrip.Items.Add(reloadButton);

        // Tab control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // Day/Night Cycle Tab
        var dayNightTab = new TabPage("Day/Night Cycle");
        dayNightTab.Controls.Add(CreateDayNightTab());
        _tabControl.TabPages.Add(dayNightTab);

        // Weather System Tab
        var weatherTab = new TabPage("Weather System");
        weatherTab.Controls.Add(CreateWeatherTab());
        _tabControl.TabPages.Add(weatherTab);

        Controls.Add(_tabControl);
        Controls.Add(toolStrip);
    }

    private Panel CreateDayNightTab()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true
        };

        int yPos = 10;

        // Day Duration
        var dayDurationLabel = new Label
        {
            Text = "Day Duration (seconds):",
            Location = new Point(10, yPos),
            Width = 200
        };
        _dayDurationNumeric = new NumericUpDown
        {
            Location = new Point(220, yPos),
            Width = 150,
            Minimum = 60,
            Maximum = 3600,
            DecimalPlaces = 1,
            Increment = 10
        };
        _dayDurationNumeric.ValueChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(dayDurationLabel);
        panel.Controls.Add(_dayDurationNumeric);
        yPos += 35;

        // Current Time of Day
        var timeOfDayLabel = new Label
        {
            Text = "Current Time of Day:",
            Location = new Point(10, yPos),
            Width = 200
        };
        _timeOfDayLabel = new Label
        {
            Text = "00:00",
            Location = new Point(220, yPos),
            Width = 100,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _timeOfDayTrackBar = new TrackBar
        {
            Location = new Point(330, yPos),
            Width = 300,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10
        };
        _timeOfDayTrackBar.ValueChanged += (s, e) => UpdateTimeOfDay();
        panel.Controls.Add(timeOfDayLabel);
        panel.Controls.Add(_timeOfDayLabel);
        panel.Controls.Add(_timeOfDayTrackBar);
        yPos += 50;

        // Pause Time
        _pauseTimeCheckBox = new CheckBox
        {
            Text = "Pause Time",
            Location = new Point(10, yPos),
            Width = 150
        };
        _pauseTimeCheckBox.CheckedChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(_pauseTimeCheckBox);
        yPos += 35;

        // Reset to Midnight
        _resetToMidnightButton = new Button
        {
            Text = "Reset to Midnight",
            Location = new Point(10, yPos),
            Width = 150
        };
        _resetToMidnightButton.Click += (s, e) =>
        {
            if (_timeOfDayTrackBar != null)
            {
                _timeOfDayTrackBar.Value = 0;
                UpdateTimeOfDay();
            }
        };
        panel.Controls.Add(_resetToMidnightButton);
        yPos += 45;

        // Ambient Color Preview
        var ambientColorLabel = new Label
        {
            Text = "Ambient Color:",
            Location = new Point(10, yPos),
            Width = 200
        };
        _ambientColorPreview = new Panel
        {
            Location = new Point(220, yPos),
            Width = 150,
            Height = 30,
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.Controls.Add(ambientColorLabel);
        panel.Controls.Add(_ambientColorPreview);
        yPos += 45;

        // Time Period Boundaries
        var boundariesLabel = new Label
        {
            Text = "Time Period Boundaries (0.0 to 1.0):",
            Location = new Point(10, yPos),
            Width = 300,
            Font = new Font(DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(boundariesLabel);
        yPos += 30;

        // Dawn Start
        var dawnStartLabel = new Label
        {
            Text = "Dawn Start:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _dawnStartNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 2,
            Increment = 0.01m
        };
        _dawnStartNumeric.ValueChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(dawnStartLabel);
        panel.Controls.Add(_dawnStartNumeric);
        yPos += 30;

        // Dawn End
        var dawnEndLabel = new Label
        {
            Text = "Dawn End:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _dawnEndNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 2,
            Increment = 0.01m
        };
        _dawnEndNumeric.ValueChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(dawnEndLabel);
        panel.Controls.Add(_dawnEndNumeric);
        yPos += 30;

        // Dusk Start
        var duskStartLabel = new Label
        {
            Text = "Dusk Start:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _duskStartNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 2,
            Increment = 0.01m
        };
        _duskStartNumeric.ValueChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(duskStartLabel);
        panel.Controls.Add(_duskStartNumeric);
        yPos += 30;

        // Dusk End
        var duskEndLabel = new Label
        {
            Text = "Dusk End:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _duskEndNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 1,
            DecimalPlaces = 2,
            Increment = 0.01m
        };
        _duskEndNumeric.ValueChanged += (s, e) => UpdateDayNightData();
        panel.Controls.Add(duskEndLabel);
        panel.Controls.Add(_duskEndNumeric);

        return panel;
    }

    private Panel CreateWeatherTab()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true
        };

        int yPos = 10;

        // Current Weather
        var currentWeatherLabel = new Label
        {
            Text = "Current Weather:",
            Location = new Point(10, yPos),
            Width = 200
        };
        _currentWeatherComboBox = new ComboBox
        {
            Location = new Point(220, yPos),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _currentWeatherComboBox.Items.AddRange(new[] { "Clear", "Light Rain", "Heavy Rain", "Snow", "Fog" });
        _currentWeatherComboBox.SelectedIndexChanged += (s, e) => UpdateWeatherData();
        panel.Controls.Add(currentWeatherLabel);
        panel.Controls.Add(_currentWeatherComboBox);
        yPos += 35;

        // Weather Intensity
        var weatherIntensityLabel = new Label
        {
            Text = "Weather Intensity:",
            Location = new Point(10, yPos),
            Width = 200
        };
        _weatherIntensityLabel = new Label
        {
            Text = "0%",
            Location = new Point(220, yPos),
            Width = 100,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _weatherIntensityTrackBar = new TrackBar
        {
            Location = new Point(330, yPos),
            Width = 300,
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10
        };
        _weatherIntensityTrackBar.ValueChanged += (s, e) =>
        {
            if (_weatherIntensityLabel != null && _weatherIntensityTrackBar != null)
            {
                _weatherIntensityLabel.Text = $"{_weatherIntensityTrackBar.Value}%";
            }
            UpdateWeatherData();
        };
        panel.Controls.Add(weatherIntensityLabel);
        panel.Controls.Add(_weatherIntensityLabel);
        panel.Controls.Add(_weatherIntensityTrackBar);
        yPos += 50;

        // Change Weather Randomly
        _changeWeatherRandomlyButton = new Button
        {
            Text = "Change Weather Randomly",
            Location = new Point(10, yPos),
            Width = 200
        };
        _changeWeatherRandomlyButton.Click += (s, e) =>
        {
            // This would trigger a random change in the game, but for editor we just show a message
            MessageBox.Show("Random weather change will occur in-game based on probabilities.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        panel.Controls.Add(_changeWeatherRandomlyButton);
        yPos += 45;

        // Change Intervals
        var intervalsLabel = new Label
        {
            Text = "Weather Change Intervals (seconds):",
            Location = new Point(10, yPos),
            Width = 300,
            Font = new Font(DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(intervalsLabel);
        yPos += 30;

        var minIntervalLabel = new Label
        {
            Text = "Min Interval:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _minChangeIntervalNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 1,
            Maximum = 600,
            DecimalPlaces = 1,
            Increment = 5
        };
        _minChangeIntervalNumeric.ValueChanged += (s, e) => UpdateWeatherData();
        panel.Controls.Add(minIntervalLabel);
        panel.Controls.Add(_minChangeIntervalNumeric);
        yPos += 30;

        var maxIntervalLabel = new Label
        {
            Text = "Max Interval:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _maxChangeIntervalNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 1,
            Maximum = 600,
            DecimalPlaces = 1,
            Increment = 5
        };
        _maxChangeIntervalNumeric.ValueChanged += (s, e) => UpdateWeatherData();
        panel.Controls.Add(maxIntervalLabel);
        panel.Controls.Add(_maxChangeIntervalNumeric);
        yPos += 45;

        // Weather Probabilities
        var probabilitiesLabel = new Label
        {
            Text = "Weather Probabilities (must sum to 100%):",
            Location = new Point(10, yPos),
            Width = 300,
            Font = new Font(DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(probabilitiesLabel);
        yPos += 30;

        var clearLabel = new Label
        {
            Text = "Clear:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _probabilityClearNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 1
        };
        _probabilityClearNumeric.ValueChanged += (s, e) => UpdateWeatherProbabilities();
        panel.Controls.Add(clearLabel);
        panel.Controls.Add(_probabilityClearNumeric);
        yPos += 30;

        var lightRainLabel = new Label
        {
            Text = "Light Rain:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _probabilityLightRainNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 1
        };
        _probabilityLightRainNumeric.ValueChanged += (s, e) => UpdateWeatherProbabilities();
        panel.Controls.Add(lightRainLabel);
        panel.Controls.Add(_probabilityLightRainNumeric);
        yPos += 30;

        var heavyRainLabel = new Label
        {
            Text = "Heavy Rain:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _probabilityHeavyRainNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 1
        };
        _probabilityHeavyRainNumeric.ValueChanged += (s, e) => UpdateWeatherProbabilities();
        panel.Controls.Add(heavyRainLabel);
        panel.Controls.Add(_probabilityHeavyRainNumeric);
        yPos += 30;

        var snowLabel = new Label
        {
            Text = "Snow:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _probabilitySnowNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 1
        };
        _probabilitySnowNumeric.ValueChanged += (s, e) => UpdateWeatherProbabilities();
        panel.Controls.Add(snowLabel);
        panel.Controls.Add(_probabilitySnowNumeric);
        yPos += 30;

        var fogLabel = new Label
        {
            Text = "Fog:",
            Location = new Point(20, yPos),
            Width = 150
        };
        _probabilityFogNumeric = new NumericUpDown
        {
            Location = new Point(180, yPos),
            Width = 150,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 1
        };
        _probabilityFogNumeric.ValueChanged += (s, e) => UpdateWeatherProbabilities();
        panel.Controls.Add(fogLabel);
        panel.Controls.Add(_probabilityFogNumeric);
        yPos += 35;

        // Probability Sum Label
        _probabilitySumLabel = new Label
        {
            Text = "Sum: 0%",
            Location = new Point(20, yPos),
            Width = 200,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(_probabilitySumLabel);
        yPos += 35;

        // Normalize Probabilities Button
        _normalizeProbabilitiesButton = new Button
        {
            Text = "Normalize Probabilities",
            Location = new Point(20, yPos),
            Width = 200
        };
        _normalizeProbabilitiesButton.Click += (s, e) => NormalizeProbabilities();
        panel.Controls.Add(_normalizeProbabilitiesButton);

        return panel;
    }

    private void LoadData()
    {
        _data = WeatherCycleSerializer.LoadWeatherCycleData();
        UpdateUI();
    }

    private void ReloadData()
    {
        LoadData();
        MessageBox.Show("Data reloaded from file.", "Reload", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveData()
    {
        UpdateDataFromUI();
        WeatherCycleSerializer.SaveWeatherCycleData(_data);
        MessageBox.Show("Data saved successfully!", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateUI()
    {
        _isUpdating = true;

        // Day/Night Cycle
        if (_dayDurationNumeric != null)
            _dayDurationNumeric.Value = (decimal)_data.DayNight.DayDuration;
        if (_dawnStartNumeric != null)
            _dawnStartNumeric.Value = (decimal)_data.DayNight.DawnStart;
        if (_dawnEndNumeric != null)
            _dawnEndNumeric.Value = (decimal)_data.DayNight.DawnEnd;
        if (_duskStartNumeric != null)
            _duskStartNumeric.Value = (decimal)_data.DayNight.DuskStart;
        if (_duskEndNumeric != null)
            _duskEndNumeric.Value = (decimal)_data.DayNight.DuskEnd;

        // Weather System
        if (_minChangeIntervalNumeric != null)
            _minChangeIntervalNumeric.Value = (decimal)_data.Weather.MinChangeInterval;
        if (_maxChangeIntervalNumeric != null)
            _maxChangeIntervalNumeric.Value = (decimal)_data.Weather.MaxChangeInterval;
        if (_probabilityClearNumeric != null)
            _probabilityClearNumeric.Value = (decimal)_data.Weather.Probabilities.Clear;
        if (_probabilityLightRainNumeric != null)
            _probabilityLightRainNumeric.Value = (decimal)_data.Weather.Probabilities.LightRain;
        if (_probabilityHeavyRainNumeric != null)
            _probabilityHeavyRainNumeric.Value = (decimal)_data.Weather.Probabilities.HeavyRain;
        if (_probabilitySnowNumeric != null)
            _probabilitySnowNumeric.Value = (decimal)_data.Weather.Probabilities.Snow;
        if (_probabilityFogNumeric != null)
            _probabilityFogNumeric.Value = (decimal)_data.Weather.Probabilities.Fog;

        UpdateWeatherProbabilities();
        UpdateAmbientColorPreview();

        _isUpdating = false;
    }

    private void UpdateDataFromUI()
    {
        // Day/Night Cycle
        if (_dayDurationNumeric != null)
            _data.DayNight.DayDuration = (float)_dayDurationNumeric.Value;
        if (_dawnStartNumeric != null)
            _data.DayNight.DawnStart = (float)_dawnStartNumeric.Value;
        if (_dawnEndNumeric != null)
            _data.DayNight.DawnEnd = (float)_dawnEndNumeric.Value;
        if (_duskStartNumeric != null)
            _data.DayNight.DuskStart = (float)_duskStartNumeric.Value;
        if (_duskEndNumeric != null)
            _data.DayNight.DuskEnd = (float)_duskEndNumeric.Value;

        // Weather System
        if (_minChangeIntervalNumeric != null)
            _data.Weather.MinChangeInterval = (float)_minChangeIntervalNumeric.Value;
        if (_maxChangeIntervalNumeric != null)
            _data.Weather.MaxChangeInterval = (float)_maxChangeIntervalNumeric.Value;
        if (_probabilityClearNumeric != null)
            _data.Weather.Probabilities.Clear = (float)_probabilityClearNumeric.Value;
        if (_probabilityLightRainNumeric != null)
            _data.Weather.Probabilities.LightRain = (float)_probabilityLightRainNumeric.Value;
        if (_probabilityHeavyRainNumeric != null)
            _data.Weather.Probabilities.HeavyRain = (float)_probabilityHeavyRainNumeric.Value;
        if (_probabilitySnowNumeric != null)
            _data.Weather.Probabilities.Snow = (float)_probabilitySnowNumeric.Value;
        if (_probabilityFogNumeric != null)
            _data.Weather.Probabilities.Fog = (float)_probabilityFogNumeric.Value;
    }

    private void UpdateDayNightData()
    {
        if (_isUpdating) return;
        UpdateDataFromUI();
        UpdateAmbientColorPreview();
    }

    private void UpdateTimeOfDay()
    {
        if (_isUpdating || _timeOfDayTrackBar == null || _timeOfDayLabel == null) return;

        var timeOfDay = _timeOfDayTrackBar.Value / 100.0f;
        var hours = (int)(timeOfDay * 24.0f);
        var minutes = (int)((timeOfDay * 24.0f - hours) * 60.0f);
        _timeOfDayLabel.Text = $"{hours:D2}:{minutes:D2}";
        UpdateAmbientColorPreview();
    }

    private void UpdateAmbientColorPreview()
    {
        if (_ambientColorPreview == null || _timeOfDayTrackBar == null) return;

        // Calculate ambient color based on current time of day
        var timeOfDay = _timeOfDayTrackBar.Value / 100.0f;
        var dawnStart = _dawnStartNumeric != null ? (float)_dawnStartNumeric.Value : 0.2f;
        var dawnEnd = _dawnEndNumeric != null ? (float)_dawnEndNumeric.Value : 0.25f;
        var duskStart = _duskStartNumeric != null ? (float)_duskStartNumeric.Value : 0.75f;
        var duskEnd = _duskEndNumeric != null ? (float)_duskEndNumeric.Value : 0.8f;

        Color ambientColor;
        if (timeOfDay > dawnEnd && timeOfDay < duskStart)
            ambientColor = Color.FromArgb(200, 200, 200); // Day
        else if (timeOfDay >= dawnStart && timeOfDay <= dawnEnd)
        {
            var t = (timeOfDay - dawnStart) / (dawnEnd - dawnStart);
            ambientColor = Color.FromArgb(
                (int)(30 + (255 - 30) * t),
                (int)(30 + (200 - 30) * t),
                (int)(50 + (150 - 50) * t));
        }
        else if (timeOfDay >= duskStart && timeOfDay <= duskEnd)
        {
            var t = (timeOfDay - duskStart) / (duskEnd - duskStart);
            ambientColor = Color.FromArgb(
                (int)(200 - (200 - 30) * t),
                (int)(150 - (150 - 30) * t),
                (int)(100 - (100 - 50) * t));
        }
        else
            ambientColor = Color.FromArgb(30, 30, 50); // Night

        _ambientColorPreview.BackColor = ambientColor;
    }

    private void UpdateWeatherData()
    {
        if (_isUpdating) return;
        UpdateDataFromUI();
    }

    private void UpdateWeatherProbabilities()
    {
        if (_isUpdating) return;

        var sum = 0.0f;
        if (_probabilityClearNumeric != null)
            sum += (float)_probabilityClearNumeric.Value;
        if (_probabilityLightRainNumeric != null)
            sum += (float)_probabilityLightRainNumeric.Value;
        if (_probabilityHeavyRainNumeric != null)
            sum += (float)_probabilityHeavyRainNumeric.Value;
        if (_probabilitySnowNumeric != null)
            sum += (float)_probabilitySnowNumeric.Value;
        if (_probabilityFogNumeric != null)
            sum += (float)_probabilityFogNumeric.Value;

        if (_probabilitySumLabel != null)
        {
            _probabilitySumLabel.Text = $"Sum: {sum:F1}%";
            _probabilitySumLabel.ForeColor = Math.Abs(sum - 100.0f) < 0.1f ? Color.Black : Color.Red;
        }
    }

    private void NormalizeProbabilities()
    {
        if (_probabilityClearNumeric == null || _probabilityLightRainNumeric == null ||
            _probabilityHeavyRainNumeric == null || _probabilitySnowNumeric == null ||
            _probabilityFogNumeric == null) return;

        var sum = (float)(_probabilityClearNumeric.Value + _probabilityLightRainNumeric.Value +
                         _probabilityHeavyRainNumeric.Value + _probabilitySnowNumeric.Value +
                         _probabilityFogNumeric.Value);

        if (sum <= 0.0f)
        {
            // Set default values
            _probabilityClearNumeric.Value = 40;
            _probabilityLightRainNumeric.Value = 25;
            _probabilityHeavyRainNumeric.Value = 20;
            _probabilitySnowNumeric.Value = 10;
            _probabilityFogNumeric.Value = 5;
            UpdateWeatherProbabilities();
            return;
        }

        var scale = 100.0f / sum;
        _probabilityClearNumeric.Value = (decimal)((float)_probabilityClearNumeric.Value * scale);
        _probabilityLightRainNumeric.Value = (decimal)((float)_probabilityLightRainNumeric.Value * scale);
        _probabilityHeavyRainNumeric.Value = (decimal)((float)_probabilityHeavyRainNumeric.Value * scale);
        _probabilitySnowNumeric.Value = (decimal)((float)_probabilitySnowNumeric.Value * scale);
        _probabilityFogNumeric.Value = (decimal)((float)_probabilityFogNumeric.Value * scale);

        UpdateWeatherProbabilities();
        UpdateDataFromUI();
    }
}

