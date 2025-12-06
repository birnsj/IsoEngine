using GameCore.Dialogs;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace GameClient.UI;

/// <summary>
/// Myra-based dialog window UI for NPC conversations.
/// </summary>
public class DialogWindow
{
    private Panel? _dialogPanel; // Custom panel instead of Myra Window
    private Label? _titleLabel;
    private TextButton? _closeButton;
    private Label? _dialogText;
    private VerticalStackPanel? _choicesPanel;
    private DialogService? _dialogService;
    private Desktop? _desktop;
    private bool _isVisible;
    private int _windowWidth = 700;
    private int _windowHeight = 400;

    public bool IsVisible
    {
        get => _dialogPanel?.Visible ?? _isVisible;
        set
        {
            _isVisible = value;
            if (_dialogPanel != null)
            {
                _dialogPanel.Visible = value;
            }
        }
    }

    public DialogWindow()
    {
    }

    /// <summary>
    /// Creates the dialog window UI using a custom Panel instead of Myra Window.
    /// </summary>
    public void CreateUI(Desktop desktop)
    {
        _desktop = desktop;
        _dialogService = ServiceLocator.Get<DialogService>();

        // Create custom panel that looks like a window - larger and centered
        _dialogPanel = new Panel
        {
            Width = _windowWidth,
            Height = _windowHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(30, 30, 30, 250)) // Darker, more opaque background for better visibility
        };

        // Create a vertical container for the entire dialog
        var dialogContainer = new VerticalStackPanel
        {
            Width = _windowWidth,
            Height = _windowHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Create title bar with title and close button - taller for better visibility
        var titleBar = new HorizontalStackPanel
        {
            Width = _windowWidth,
            Height = 40,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(50, 50, 50, 255))
        };

        _titleLabel = new Label
        {
            Text = "Conversation",
            TextColor = Color.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Myra.Graphics2D.Thickness(15, 0, 0, 0)
        };

        // Custom close button that we have full control over - larger and more visible
        _closeButton = new TextButton
        {
            Text = "×",
            Width = 40,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
        };
        _closeButton.Click += (s, e) =>
        {
            // Close button clicked - properly end the dialog and unlock controls
            if (_dialogService != null && _dialogService.IsDialogActive)
            {
                _dialogService.EndDialog();
            }
            IsVisible = false;
        };

        titleBar.Widgets.Add(_titleLabel);
        titleBar.Widgets.Add(_closeButton);

        // Main content panel - more padding and spacing for better readability
        var mainPanel = new VerticalStackPanel
        {
            Spacing = 15,
            Padding = new Myra.Graphics2D.Thickness(20, 20, 20, 20),
            Width = _windowWidth,
            Height = _windowHeight - 40 // Remaining space below title bar
        };

        // Dialog text label - larger text and better wrapping
        _dialogText = new Label
        {
            Text = "",
            TextColor = Color.White,
            Wrap = true,
            Width = _windowWidth - 40, // Account for padding
            Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 10)
        };

        mainPanel.Widgets.Add(_dialogText);

        // Choices panel - better spacing between choices
        _choicesPanel = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        mainPanel.Widgets.Add(_choicesPanel);

        // Add title bar and content to container
        dialogContainer.Widgets.Add(titleBar);
        dialogContainer.Widgets.Add(mainPanel);

        // Add container to dialog panel
        _dialogPanel.Widgets.Add(dialogContainer);

        // Add dialog panel to desktop root panel
        if (desktop.Root is Panel rootPanel)
        {
            rootPanel.Widgets.Add(_dialogPanel);
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
            newRoot.Widgets.Add(_dialogPanel);
            desktop.Root = newRoot;
        }
    }

    /// <summary>
    /// Updates the dialog window to show the current dialog state.
    /// </summary>
    public void UpdateDisplay()
    {
        if (_dialogService == null || _dialogText == null || _choicesPanel == null)
            return;

        if (!_dialogService.IsDialogActive)
        {
            IsVisible = false;
            return;
        }

        // Update dialog text
        _dialogText.Text = _dialogService.CurrentNodeText ?? "";

        // Clear existing choice buttons
        _choicesPanel.Widgets.Clear();

        // Add buttons for each available choice - larger and more visible
        var choices = _dialogService.GetAvailableChoices();
        foreach (var choice in choices)
        {
            // Use TextButton for proper mouse click handling - larger buttons for better visibility
            var button = new TextButton
            {
                Text = choice.Text,
                Width = _windowWidth - 40, // Account for padding
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Capture choice in closure
            var capturedChoice = choice;
            button.Click += (s, e) =>
            {
                if (_dialogService != null)
                {
                    var continues = _dialogService.SelectChoice(capturedChoice);
                    if (!continues)
                    {
                        // Dialog ended
                        IsVisible = false;
                    }
                    else
                    {
                        // Update display for next node
                        UpdateDisplay();
                    }
                }
            };

            _choicesPanel.Widgets.Add(button);
        }
    }

    /// <summary>
    /// Shows the dialog panel and updates its content.
    /// </summary>
    public void Show()
    {
        if (_dialogPanel != null)
        {
            // Ensure panel is added back to the desktop if it was removed
            if (_dialogPanel.Parent == null && _desktop != null)
            {
                if (_desktop.Root is Panel rootPanel)
                {
                    rootPanel.Widgets.Add(_dialogPanel);
                }
            }
            
            IsVisible = true;
            UpdateDisplay();
            // Bring panel to front
            if (_dialogPanel.Parent is Panel parentPanel)
            {
                parentPanel.Widgets.Remove(_dialogPanel);
                parentPanel.Widgets.Add(_dialogPanel);
            }
        }
    }
    
    /// <summary>
    /// Hides the dialog window and clears its content to prevent input blocking.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        
        // Clear the choices panel to prevent any lingering buttons from blocking input
        if (_choicesPanel != null)
        {
            _choicesPanel.Widgets.Clear();
        }
        
        // Clear dialog text
        if (_dialogText != null)
        {
            _dialogText.Text = "";
        }
        
    }
}

