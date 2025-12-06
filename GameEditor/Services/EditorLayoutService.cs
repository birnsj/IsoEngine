using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using GameEditor.Data;

namespace GameEditor.Services;

/// <summary>
/// Service for saving and loading editor layout/state.
/// </summary>
public static class EditorLayoutService
{
    private const string LayoutFileName = "editor_layout.json";

    /// <summary>
    /// Gets the path to the layout file.
    /// </summary>
    private static string GetLayoutFilePath()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IsoEngineEditor");
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }
        return Path.Combine(appDataPath, LayoutFileName);
    }

    /// <summary>
    /// Gets the default layout file path for user selection.
    /// </summary>
    public static string GetDefaultLayoutFilePath()
    {
        return GetLayoutFilePath();
    }

    /// <summary>
    /// Saves the layout to a specific file path.
    /// </summary>
    public static void SaveLayoutToFile(Form mainForm, TabControl? tabControl, string filePath)
    {
        try
        {
            var layout = CreateLayoutData(mainForm, tabControl);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(layout, options);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save layout to file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads the layout from a specific file path.
    /// </summary>
    public static EditorLayoutData? LoadLayoutFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<EditorLayoutData>(json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load layout from file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates layout data from the current form state.
    /// </summary>
    private static EditorLayoutData CreateLayoutData(Form mainForm, TabControl? tabControl)
    {
        var layout = new EditorLayoutData
        {
            WindowState = (int)mainForm.WindowState,
            WindowX = mainForm.Location.X,
            WindowY = mainForm.Location.Y,
            WindowWidth = mainForm.Size.Width,
            WindowHeight = mainForm.Size.Height,
            SelectedTabIndex = tabControl?.SelectedIndex ?? 0
        };

        // Save open tabs (file paths)
        if (tabControl != null)
        {
            foreach (TabPage tab in tabControl.TabPages)
            {
                // Try to get the file path from the form in the tab
                if (tab.Controls.Count > 0 && tab.Controls[0] is Form form)
                {
                    // Check if it's a TileMapEditorForm or EntityEditorForm
                    if (form is Forms.TileMapEditorForm tileMapForm)
                    {
                        var filePath = tileMapForm.CurrentFilePath;
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            layout.OpenTabs.Add($"map:{filePath}");
                        }
                    }
                    else if (form is Forms.EntityEditorForm entityForm)
                    {
                        var filePath = entityForm.CurrentFilePath;
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            layout.OpenTabs.Add($"entity:{filePath}");
                        }
                    }
                }
            }
        }

        return layout;
    }

    /// <summary>
    /// Saves the editor layout to disk (default location).
    /// </summary>
    public static void SaveLayout(Form mainForm, TabControl? tabControl)
    {
        try
        {
            var layout = CreateLayoutData(mainForm, tabControl);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(layout, options);
            File.WriteAllText(GetLayoutFilePath(), json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving editor layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the editor layout from disk.
    /// </summary>
    public static EditorLayoutData? LoadLayout()
    {
        try
        {
            var layoutPath = GetLayoutFilePath();
            if (!File.Exists(layoutPath))
                return null;

            var json = File.ReadAllText(layoutPath);
            return JsonSerializer.Deserialize<EditorLayoutData>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading editor layout: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies the loaded layout to the main form.
    /// </summary>
    public static void ApplyLayout(Form mainForm, EditorLayoutData? layout)
    {
        if (layout == null)
            return;

        try
        {
            // Restore window size and position
            if (layout.WindowWidth > 0 && layout.WindowHeight > 0)
            {
                mainForm.Size = new System.Drawing.Size(layout.WindowWidth, layout.WindowHeight);
            }

            // Restore window position (only if it's on screen)
            if (layout.WindowX >= 0 && layout.WindowY >= 0)
            {
                var screenBounds = Screen.GetBounds(new System.Drawing.Point(layout.WindowX, layout.WindowY));
                if (screenBounds.Contains(layout.WindowX, layout.WindowY))
                {
                    mainForm.Location = new System.Drawing.Point(layout.WindowX, layout.WindowY);
                }
            }

            // Restore window state
            if (layout.WindowState >= 0 && layout.WindowState <= 2)
            {
                mainForm.WindowState = (FormWindowState)layout.WindowState;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying editor layout: {ex.Message}");
        }
    }
}

