using GameCore;
using GameCore.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;

namespace GameClient.UI;

/// <summary>
/// Simple options window with master volume and fullscreen toggle.
/// </summary>
public class OptionsWindow
{
    private readonly GraphicsDeviceManager _graphics;
    private Window? _window;

    public bool IsVisible => _window?.Visible ?? false;


    public OptionsWindow(GraphicsDeviceManager graphics)
    {
        _graphics = graphics;
    }

    public void CreateUI(Desktop desktop)
    {
        _window = new Window
        {
            Title = "Options",
            Width = 400,
            Height = 220,
            Visible = false
        };

        var panel = new VerticalStackPanel
        {
            Spacing = 8,
            Padding = new Myra.Graphics2D.Thickness(8)
        };

        // Master volume
        var volumeLabel = new Label
        {
            Text = "Master Volume",
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var volumeSlider = new HorizontalSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = (int)(Configuration.MasterVolume * 100),
            Width = 300
        };

        volumeSlider.ValueChanged += (s, e) =>
        {
            Configuration.MasterVolume = volumeSlider.Value / 100f;
        };

        // Fullscreen toggle
        var fullscreenCheckBox = new CheckBox
        {
            Text = "Fullscreen",
            IsChecked = Configuration.IsFullscreen
        };

        fullscreenCheckBox.PressedChanged += (s, e) =>
        {
            Configuration.IsFullscreen = fullscreenCheckBox.IsChecked;
            _graphics.IsFullScreen = Configuration.IsFullscreen;
            _graphics.ApplyChanges();
        };

        panel.Widgets.Add(volumeLabel);
        panel.Widgets.Add(volumeSlider);
        panel.Widgets.Add(fullscreenCheckBox);

        _window.Content = panel;

        if (desktop.Root is Panel rootPanel)
        {
            rootPanel.Widgets.Add(_window);
        }
        else
        {
            desktop.Root = _window;
        }
    }

    public void Show()
    {
        if (_window == null) return;
        _window.Visible = true;
    }

    public void Hide()
    {
        if (_window == null) return;
        _window.Visible = false;
    }
}


