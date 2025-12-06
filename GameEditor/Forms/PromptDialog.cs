using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for entering image generation prompts.
/// </summary>
public class PromptDialog : Form
{
    private TextBox? _promptTextBox;
    public string Prompt { get; private set; } = "";

    public PromptDialog(string? initialPrompt = null)
    {
        Text = "Enter Image Generation Prompt";
        Size = new Size(600, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "Enter your prompt for image generation:",
            Location = new Point(20, 20),
            AutoSize = true
        };

        _promptTextBox = new TextBox
        {
            Location = new Point(20, 45),
            Width = 540,
            Height = 150,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = initialPrompt ?? "",
            Font = new Font(DefaultFont.FontFamily, 10)
        };
        _promptTextBox.Select();

        // Add some example prompts
        var examplesLabel = new Label
        {
            Text = "Example prompts:",
            Location = new Point(20, 205),
            AutoSize = true,
            Font = new Font(DefaultFont, FontStyle.Bold)
        };

        var example1 = new LinkLabel
        {
            Text = "• isometric grass tile, pixel art style, top-down view",
            Location = new Point(30, 225),
            AutoSize = true
        };
        example1.Click += (s, e) => _promptTextBox.Text = example1.Text.Substring(2);

        var example2 = new LinkLabel
        {
            Text = "• isometric stone tile, game asset, clean background",
            Location = new Point(30, 245),
            AutoSize = true
        };
        example2.Click += (s, e) => _promptTextBox.Text = example2.Text.Substring(2);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(420, 200),
            Width = 75
        };
        okButton.Click += (s, e) =>
        {
            Prompt = _promptTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(Prompt))
            {
                MessageBox.Show("Please enter a prompt.", "Prompt Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(500, 200),
            Width = 75
        };

        Controls.Add(label);
        Controls.Add(_promptTextBox);
        Controls.Add(examplesLabel);
        Controls.Add(example1);
        Controls.Add(example2);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        // Set focus to textbox
        _promptTextBox.Focus();
    }
}





