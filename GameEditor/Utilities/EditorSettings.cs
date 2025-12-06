using System;
using System.Collections.Generic;
using System.IO;

namespace GameEditor.Utilities;

/// <summary>
/// Manages editor settings including OpenAI API key.
/// </summary>
public static class EditorSettings
{
    private static string GetSettingsPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "IsoEngineEditor");
        if (!Directory.Exists(settingsDir))
        {
            Directory.CreateDirectory(settingsDir);
        }
        return Path.Combine(settingsDir, "settings.txt");
    }

    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    public static string? GetSetting(string key)
    {
        try
        {
            var settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                var lines = File.ReadAllLines(settingsPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith($"{key}=", StringComparison.Ordinal))
                    {
                        return line.Substring(key.Length + 1);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    public static void SetSetting(string key, string value)
    {
        try
        {
            var settingsPath = GetSettingsPath();
            var settingsDir = Path.GetDirectoryName(settingsPath);
            if (settingsDir != null && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            var lines = new List<string>();
            if (File.Exists(settingsPath))
            {
                lines.AddRange(File.ReadAllLines(settingsPath));
                // Remove existing setting if present
                lines.RemoveAll(l => l.StartsWith($"{key}=", StringComparison.Ordinal));
            }

            lines.Add($"{key}={value}");
            File.WriteAllLines(settingsPath, lines);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Gets the OpenAI API key from settings.
    /// </summary>
    public static string? GetOpenAIApiKey()
    {
        return GetSetting("OpenAIApiKey");
    }

    /// <summary>
    /// Sets the OpenAI API key in settings.
    /// </summary>
    public static void SetOpenAIApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            SetSetting("OpenAIApiKey", "");
        }
        else
        {
            SetSetting("OpenAIApiKey", apiKey);
        }
    }

    /// <summary>
    /// Gets the last selected graphic directory.
    /// </summary>
    public static string? GetLastGraphicDirectory()
    {
        return GetSetting("LastGraphicDirectory");
    }

    /// <summary>
    /// Sets the last selected graphic directory.
    /// </summary>
    public static void SetLastGraphicDirectory(string directory)
    {
        SetSetting("LastGraphicDirectory", directory);
    }

    /// <summary>
    /// Gets whether Perforce integration is enabled.
    /// </summary>
    public static bool GetEnablePerforceIntegration()
    {
        var value = GetSetting("EnablePerforceIntegration");
        if (string.IsNullOrEmpty(value))
            return true; // Default to enabled
        return bool.TryParse(value, out var result) && result;
    }

    /// <summary>
    /// Sets whether Perforce integration is enabled.
    /// </summary>
    public static void SetEnablePerforceIntegration(bool enabled)
    {
        SetSetting("EnablePerforceIntegration", enabled.ToString());
    }

    /// <summary>
    /// Gets the Perforce server/port (e.g., "perforce:1666").
    /// </summary>
    public static string? GetPerforceServer()
    {
        return GetSetting("PerforceServer");
    }

    /// <summary>
    /// Sets the Perforce server/port.
    /// </summary>
    public static void SetPerforceServer(string? server)
    {
        SetSetting("PerforceServer", server ?? "");
    }

    /// <summary>
    /// Gets the Perforce username.
    /// </summary>
    public static string? GetPerforceUser()
    {
        return GetSetting("PerforceUser");
    }

    /// <summary>
    /// Sets the Perforce username.
    /// </summary>
    public static void SetPerforceUser(string? user)
    {
        SetSetting("PerforceUser", user ?? "");
    }

    /// <summary>
    /// Gets the Perforce password.
    /// </summary>
    public static string? GetPerforcePassword()
    {
        return GetSetting("PerforcePassword");
    }

    /// <summary>
    /// Sets the Perforce password.
    /// </summary>
    public static void SetPerforcePassword(string? password)
    {
        SetSetting("PerforcePassword", password ?? "");
    }

    /// <summary>
    /// Gets the Perforce workspace/client name.
    /// </summary>
    public static string? GetPerforceWorkspace()
    {
        return GetSetting("PerforceWorkspace");
    }

    /// <summary>
    /// Sets the Perforce workspace/client name.
    /// </summary>
    public static void SetPerforceWorkspace(string? workspace)
    {
        SetSetting("PerforceWorkspace", workspace ?? "");
    }

    /// <summary>
    /// Gets the custom path to p4.exe executable (if manually configured).
    /// </summary>
    public static string? GetPerforceExecutablePath()
    {
        return GetSetting("PerforceExecutablePath");
    }

    /// <summary>
    /// Sets the custom path to p4.exe executable.
    /// </summary>
    public static void SetPerforceExecutablePath(string? path)
    {
        SetSetting("PerforceExecutablePath", path ?? "");
    }
}

