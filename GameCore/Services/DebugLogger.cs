using System;
using System.Diagnostics;
using System.IO;

namespace GameCore.Services;

/// <summary>
/// Simple logger implementation that writes to System.Diagnostics.Debug, Console, and a log file.
/// </summary>
public class DebugLogger : ILogger, IGameService
{
    private const string DebugPrefix = "[DEBUG]";
    private const string InfoPrefix = "[INFO]";
    private const string WarningPrefix = "[WARNING]";
    private const string ErrorPrefix = "[ERROR]";
    private readonly string _logFilePath;

    public DebugLogger()
    {
        // Create log file in the game's directory
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    private void WriteLog(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] {level} {message}";
        
        // Write to Debug output (Visual Studio Output window)
        System.Diagnostics.Debug.WriteLine(logMessage);
        
        // Write to Console
        Console.WriteLine(logMessage);
        
        // Write to log file
        try
        {
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Silently fail if we can't write to log file
        }
    }

    public void Debug(string message)
    {
        WriteLog(DebugPrefix, message);
    }

    public void Info(string message)
    {
        WriteLog(InfoPrefix, message);
    }

    public void Warning(string message)
    {
        WriteLog(WarningPrefix, message);
    }

    public void Error(string message)
    {
        WriteLog(ErrorPrefix, message);
    }

    // IGameService implementation - logger doesn't need update/draw
    public void Initialize()
    {
        Info("DebugLogger initialized.");
    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Logger doesn't need per-frame updates
    }

    public void Draw(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Logger doesn't need rendering
    }
}

