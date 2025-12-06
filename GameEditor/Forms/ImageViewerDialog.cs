using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for viewing full-size images.
/// </summary>
public class ImageViewerDialog : Form
{
    public ImageViewerDialog(Image image, string? title = null)
    {
        Text = title ?? "Image Viewer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Normal;
        MinimumSize = new Size(400, 300);

        // Create picture box with scrollable container
        var pictureBox = new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.AutoSize,
            Dock = DockStyle.Fill
        };

        // Create scrollable panel
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        panel.Controls.Add(pictureBox);

        // Add close button
        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
            Height = 35
        };
        closeButton.Click += (s, e) => Close();

        Controls.Add(panel);
        Controls.Add(closeButton);

        // Set form size to show image nicely (but not too large)
        var maxWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
        var maxHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
        
        var imageWidth = image.Width;
        var imageHeight = image.Height;

        // Add some padding for the form borders and button
        var formWidth = Math.Min(imageWidth + 40, maxWidth - 100);
        var formHeight = Math.Min(imageHeight + 80, maxHeight - 100);

        Size = new Size(formWidth, formHeight);

        // Center the image if it's smaller than the form
        if (imageWidth < formWidth - 40 && imageHeight < formHeight - 80)
        {
            pictureBox.Dock = DockStyle.None;
            pictureBox.Location = new Point(
                (formWidth - imageWidth) / 2 - 20,
                (formHeight - imageHeight) / 2 - 40
            );
            pictureBox.Size = new Size(imageWidth, imageHeight);
            panel.AutoScroll = false;
        }
    }
}





