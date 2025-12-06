using System.Collections.Generic;

namespace GameEditor.Data;

/// <summary>
/// Serializable data for editor layout and window state.
/// </summary>
public class EditorLayoutData
{
    /// <summary>
    /// Gets or sets the window state (Normal, Maximized, Minimized).
    /// </summary>
    public int WindowState { get; set; }

    /// <summary>
    /// Gets or sets the window position X coordinate.
    /// </summary>
    public int WindowX { get; set; }

    /// <summary>
    /// Gets or sets the window position Y coordinate.
    /// </summary>
    public int WindowY { get; set; }

    /// <summary>
    /// Gets or sets the window width.
    /// </summary>
    public int WindowWidth { get; set; }

    /// <summary>
    /// Gets or sets the window height.
    /// </summary>
    public int WindowHeight { get; set; }

    /// <summary>
    /// Gets or sets the list of open tabs (file paths).
    /// </summary>
    public List<string> OpenTabs { get; set; } = new();

    /// <summary>
    /// Gets or sets the selected tab index.
    /// </summary>
    public int SelectedTabIndex { get; set; }
}




