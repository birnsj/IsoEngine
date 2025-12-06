using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using GameEditor.Utilities;

namespace GameEditor.Services;

/// <summary>
/// Service for interacting with Perforce version control.
/// Automatically checks out changed files and adds new files before saving.
/// </summary>
public static class PerforceService
{
    private static bool? _isAvailable;
    private static string? _p4Executable;

    /// <summary>
    /// Resets the availability check so it will recheck on next call to IsAvailable().
    /// </summary>
    public static void ResetAvailability()
    {
        _isAvailable = null;
        _p4Executable = null;
    }

    /// <summary>
    /// Checks if Perforce is available and configured.
    /// </summary>
    public static bool IsAvailable()
    {
        if (_isAvailable.HasValue)
            return _isAvailable.Value;

        // First check if a custom path is configured
        var customPath = EditorSettings.GetPerforceExecutablePath();
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            _p4Executable = customPath;
            _isAvailable = true;
            return true;
        }

        // Try to find p4.exe in common locations
        var possiblePaths = new List<string>
        {
            "p4", // In PATH
            @"C:\Program Files\Perforce\p4.exe",
            @"C:\Program Files (x86)\Perforce\p4.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Perforce", "p4.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Perforce", "p4.exe"),
            @"C:\Program Files\Perforce\Server\p4.exe",
            @"C:\Perforce\p4.exe"
        };

        // Also check common Helix Core (newer Perforce) locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFiles))
        {
            possiblePaths.Add(Path.Combine(programFiles, "Helix Core", "p4.exe"));
            possiblePaths.Add(Path.Combine(programFiles, "Perforce", "Helix Core", "p4.exe"));
        }
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            possiblePaths.Add(Path.Combine(programFilesX86, "Helix Core", "p4.exe"));
            possiblePaths.Add(Path.Combine(programFilesX86, "Perforce", "Helix Core", "p4.exe"));
        }

        // First pass: Check if file exists
        foreach (var path in possiblePaths)
        {
            try
            {
                if (path == "p4")
                {
                    // For PATH, try to run it
                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = path,
                                Arguments = "info",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        process.WaitForExit(2000);

                        if (process.ExitCode == 0)
                        {
                            _p4Executable = path;
                            _isAvailable = true;
                            return true;
                        }
                    }
                    catch
                    {
                        // Continue searching
                    }
                }
                else
                {
                    // For file paths, check if file exists first
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        // File exists, try to run it
                        try
                        {
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = fullPath,
                                    Arguments = "info",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    CreateNoWindow = true
                                }
                            };

                            process.Start();
                            process.WaitForExit(2000);

                            if (process.ExitCode == 0)
                            {
                                _p4Executable = fullPath;
                                _isAvailable = true;
                                return true;
                            }
                        }
                        catch
                        {
                            // File exists but can't run - might still be valid
                            // Store it anyway as it might work with proper credentials
                            _p4Executable = fullPath;
                            _isAvailable = true;
                            System.Diagnostics.Debug.WriteLine($"[PerforceService] Found p4.exe but couldn't test: {fullPath}");
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Continue searching
            }
        }

        _isAvailable = false;
        return false;
    }

    /// <summary>
    /// Ensures a file is checked out in Perforce before saving.
    /// If the file doesn't exist in Perforce, it will be added after creation.
    /// </summary>
    /// <param name="filePath">The file path to check out or add</param>
    /// <returns>True if successful or Perforce is not available, false on error</returns>
    public static bool EnsureFileReadyForEdit(string filePath)
    {
        // Check if Perforce integration is enabled
        if (!EditorSettings.GetEnablePerforceIntegration())
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Perforce integration is disabled");
            return true;
        }
        
        if (!IsAvailable())
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Perforce is not available");
            return true; // Perforce not available, but that's okay
        }

        if (string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Ensuring file ready for edit: {fullPath}");
            
            // Check if file exists
            bool fileExists = File.Exists(fullPath);
            System.Diagnostics.Debug.WriteLine($"[PerforceService] File exists: {fileExists}");
            
            // Check if file is in Perforce
            bool isInPerforce = IsFileInPerforce(fullPath);
            System.Diagnostics.Debug.WriteLine($"[PerforceService] File in Perforce: {isInPerforce}");

            if (fileExists && isInPerforce)
            {
                // File exists and is in Perforce - check it out
                return CheckOutFile(fullPath);
            }
            // If file doesn't exist or isn't in Perforce, we'll add it after creation
            // So return true to allow the save to proceed
            System.Diagnostics.Debug.WriteLine($"[PerforceService] File will be added to Perforce after save");

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error ensuring file ready: {ex.Message}");
            // Don't block saves if Perforce fails
            return true;
        }
    }

    /// <summary>
    /// Adds a new file to Perforce after it has been created.
    /// Also ensures parent directories are added to Perforce if needed.
    /// </summary>
    /// <param name="filePath">The file path to add</param>
    /// <returns>True if successful or Perforce is not available, false on error</returns>
    public static bool AddFile(string filePath)
    {
        // Check if Perforce integration is enabled
        if (!EditorSettings.GetEnablePerforceIntegration())
            return true;
        
        if (!IsAvailable())
            return true; // Perforce not available, but that's okay

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(filePath);

            // Check if already in Perforce
            if (IsFileInPerforce(fullPath))
            {
                System.Diagnostics.Debug.WriteLine($"[PerforceService] File already in Perforce: {fullPath}");
                return true; // Already added
            }

            // Ensure parent directories are added to Perforce first
            // Perforce requires directories to be added before files can be added
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                EnsureDirectoryInPerforce(directory);
            }

            System.Diagnostics.Debug.WriteLine($"[PerforceService] Adding file to Perforce: {fullPath}");
            var result = RunPerforceCommandWithOutput($"add \"{fullPath}\"");
            
            if (result.success)
            {
                System.Diagnostics.Debug.WriteLine($"[PerforceService] Successfully added file to Perforce: {fullPath}");
                return true;
            }
            else
            {
                // Check if error is because file is already added (which is okay)
                if (result.output.Contains("already opened") || result.output.Contains("already in file"))
                {
                    System.Diagnostics.Debug.WriteLine($"[PerforceService] File already in Perforce (detected from error): {fullPath}");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"[PerforceService] Failed to add file to Perforce: {fullPath}. Output: {result.output}");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error adding file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures a directory and its parent directories are added to Perforce.
    /// </summary>
    /// <param name="directoryPath">The directory path to ensure is in Perforce</param>
    private static void EnsureDirectoryInPerforce(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            
            // Check if directory is already in Perforce
            // Perforce tracks directories by checking if they exist in the depot
            var result = RunPerforceCommandWithOutput($"dirs \"{fullPath}\"");
            
            if (!result.success || result.output.Contains("no such file"))
            {
                // Directory not in Perforce, need to add it
                // First, ensure parent directory is added
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    EnsureDirectoryInPerforce(parentDir);
                }
                
                // Add the directory to Perforce
                System.Diagnostics.Debug.WriteLine($"[PerforceService] Adding directory to Perforce: {fullPath}");
                var addResult = RunPerforceCommandWithOutput($"add -d \"{fullPath}\"");
                
                if (addResult.success)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerforceService] Successfully added directory to Perforce: {fullPath}");
                }
                else if (!addResult.output.Contains("already opened") && !addResult.output.Contains("already in file"))
                {
                    System.Diagnostics.Debug.WriteLine($"[PerforceService] Failed to add directory to Perforce: {fullPath}. Output: {addResult.output}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error ensuring directory in Perforce: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a file is tracked in Perforce.
    /// </summary>
    private static bool IsFileInPerforce(string filePath)
    {
        try
        {
            var result = RunPerforceCommandWithOutput($"fstat \"{filePath}\"");
            // Check for various indicators that file is in Perforce
            if (result.success && !string.IsNullOrEmpty(result.output))
            {
                // File is in Perforce if output contains any of these indicators
                return result.output.Contains("headAction") || 
                       result.output.Contains("headRev") ||
                       result.output.Contains("depotFile");
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error checking if file in Perforce: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks out a file in Perforce.
    /// </summary>
    private static bool CheckOutFile(string filePath)
    {
        System.Diagnostics.Debug.WriteLine($"[PerforceService] Checking out file: {filePath}");
        var result = RunPerforceCommandWithOutput($"edit \"{filePath}\"");
        if (result.success)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Successfully checked out file: {filePath}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Failed to check out file: {filePath}. Output: {result.output}");
        }
        return result.success;
    }

    /// <summary>
    /// Gets the environment variables for Perforce commands based on settings.
    /// </summary>
    private static Dictionary<string, string> GetPerforceEnvironment()
    {
        var env = new Dictionary<string, string>();
        
        var server = EditorSettings.GetPerforceServer();
        if (!string.IsNullOrEmpty(server))
        {
            env["P4PORT"] = server;
        }
        
        var user = EditorSettings.GetPerforceUser();
        if (!string.IsNullOrEmpty(user))
        {
            env["P4USER"] = user;
        }
        
        var password = EditorSettings.GetPerforcePassword();
        if (!string.IsNullOrEmpty(password))
        {
            env["P4PASSWD"] = password;
        }
        
        var workspace = EditorSettings.GetPerforceWorkspace();
        if (!string.IsNullOrEmpty(workspace))
        {
            env["P4CLIENT"] = workspace;
        }
        
        return env;
    }

    /// <summary>
    /// Tests the Perforce connection with current settings.
    /// </summary>
    public static (bool success, string message) TestConnection()
    {
        if (!IsAvailable())
        {
            return (false, "Perforce executable (p4.exe) not found. Please ensure Perforce is installed.");
        }

        try
        {
            var result = RunPerforceCommandWithOutput("info");
            if (result.success)
            {
                return (true, "Successfully connected to Perforce server.\n\n" + result.output);
            }
            else
            {
                return (false, "Failed to connect to Perforce server.\n\n" + result.output);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error testing connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a Perforce command without capturing output.
    /// </summary>
    private static bool RunPerforceCommand(string arguments)
    {
        if (string.IsNullOrEmpty(_p4Executable))
            return false;

        try
        {
            var filePath = ExtractFilePathFromArguments(arguments);
            var workingDirectory = !string.IsNullOrEmpty(filePath) && File.Exists(filePath)
                ? Path.GetDirectoryName(Path.GetFullPath(filePath))
                : Environment.CurrentDirectory;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _p4Executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            // Set Perforce environment variables
            var env = GetPerforceEnvironment();
            foreach (var kvp in env)
            {
                processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();
            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error running p4 command: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs a Perforce command and captures output.
    /// </summary>
    private static (bool success, string output) RunPerforceCommandWithOutput(string arguments)
    {
        if (string.IsNullOrEmpty(_p4Executable))
            return (false, string.Empty);

        try
        {
            var filePath = ExtractFilePathFromArguments(arguments);
            var workingDirectory = !string.IsNullOrEmpty(filePath) && File.Exists(filePath)
                ? Path.GetDirectoryName(Path.GetFullPath(filePath))
                : Environment.CurrentDirectory;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _p4Executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            // Set Perforce environment variables
            var env = GetPerforceEnvironment();
            foreach (var kvp in env)
            {
                processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            
            // Combine output and error for better diagnostics
            var combinedOutput = output;
            if (!string.IsNullOrEmpty(error))
            {
                combinedOutput += (string.IsNullOrEmpty(combinedOutput) ? "" : "\n") + error;
            }
            
            return (process.ExitCode == 0, combinedOutput);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerforceService] Error running p4 command: {ex.Message}");
            return (false, string.Empty);
        }
    }

    /// <summary>
    /// Extracts file path from Perforce command arguments.
    /// </summary>
    private static string? ExtractFilePathFromArguments(string arguments)
    {
        // Look for quoted file paths in arguments
        var startIndex = arguments.IndexOf('"');
        if (startIndex >= 0)
        {
            var endIndex = arguments.IndexOf('"', startIndex + 1);
            if (endIndex > startIndex)
            {
                return arguments.Substring(startIndex + 1, endIndex - startIndex - 1);
            }
        }
        return null;
    }
}

