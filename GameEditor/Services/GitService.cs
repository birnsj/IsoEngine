using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GameEditor.Services;

/// <summary>
/// Service for Git operations.
/// </summary>
public static class GitService
{
    /// <summary>
    /// Gets the list of changed files (new, modified, deleted).
    /// </summary>
    public static (bool success, List<GitFileStatus> files, string errorMessage) GetChangedFiles()
    {
        var files = new List<GitFileStatus>();
        var (success, output, error) = RunGitCommand("status --porcelain");
        
        if (!success)
        {
            return (false, files, error);
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length < 3)
                continue;

            var statusCode = line.Substring(0, 2).Trim();
            var filePath = line.Substring(2).Trim().Trim('"');

            var fileStatus = new GitFileStatus
            {
                FilePath = filePath,
                Status = GetStatusFromCode(statusCode)
            };

            files.Add(fileStatus);
        }

        return (true, files, string.Empty);
    }

    /// <summary>
    /// Gets the list of untracked files.
    /// </summary>
    public static (bool success, List<string> files, string errorMessage) GetUntrackedFiles()
    {
        var files = new List<string>();
        var (success, output, error) = RunGitCommand("ls-files --others --exclude-standard");
        
        if (!success)
        {
            return (false, files, error);
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        files.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        return (true, files, string.Empty);
    }

    /// <summary>
    /// Stages all files (new and modified).
    /// </summary>
    public static (bool success, string output, string errorMessage) StageAllFiles()
    {
        return RunGitCommand("add -A");
    }

    /// <summary>
    /// Stages specific files.
    /// </summary>
    public static (bool success, string output, string errorMessage) StageFiles(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0)
            return (true, string.Empty, string.Empty);

        var filesArg = string.Join(" ", filePaths.Select(f => $"\"{f}\""));
        return RunGitCommand($"add {filesArg}");
    }

    /// <summary>
    /// Commits staged changes.
    /// </summary>
    public static (bool success, string output, string errorMessage) Commit(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "Update from IsoEngine Editor";

        var escapedMessage = message.Replace("\"", "\\\"");
        return RunGitCommand($"commit -m \"{escapedMessage}\"");
    }

    /// <summary>
    /// Pushes changes to remote repository.
    /// </summary>
    public static (bool success, string output, string errorMessage) Push()
    {
        var (success, branchOutput, branchError) = RunGitCommand("rev-parse --abbrev-ref HEAD");
        if (!success)
        {
            return (false, string.Empty, $"Failed to get current branch: {branchError}");
        }

        var branch = branchOutput.Trim();
        return RunGitCommand($"push origin {branch}");
    }

    /// <summary>
    /// Checks if the current directory is a git repository.
    /// </summary>
    public static bool IsGitRepository()
    {
        var (success, _, _) = RunGitCommand("rev-parse --git-dir");
        return success;
    }

    /// <summary>
    /// Gets the current branch name.
    /// </summary>
    public static (bool success, string branch, string errorMessage) GetCurrentBranch()
    {
        var (success, output, error) = RunGitCommand("rev-parse --abbrev-ref HEAD");
        if (!success)
        {
            return (false, string.Empty, error);
        }
        return (true, output.Trim(), string.Empty);
    }

    /// <summary>
    /// Gets the remote URL.
    /// </summary>
    public static (bool success, string url, string errorMessage) GetRemoteUrl()
    {
        var (success, output, error) = RunGitCommand("config --get remote.origin.url");
        if (!success)
        {
            return (false, string.Empty, error);
        }
        return (true, output.Trim(), string.Empty);
    }

    private static GitFileStatusType GetStatusFromCode(string code)
    {
        return code switch
        {
            "??" => GitFileStatusType.Untracked,
            "A " or "A\t" => GitFileStatusType.Added,
            "M " or "M\t" => GitFileStatusType.Modified,
            "D " or "D\t" => GitFileStatusType.Deleted,
            "MM" or "M\tM" => GitFileStatusType.Modified, // Modified in index and working tree
            "AM" or "A\tM" => GitFileStatusType.Added,    // Added to index, modified in working tree
            _ => GitFileStatusType.Modified
        };
    }

    private static (bool success, string output, string error) RunGitCommand(string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = GetRepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return (false, string.Empty, "Failed to start git process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                return (false, output, error);
            }

            return (true, output, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    private static string GetRepositoryRoot()
    {
        // Try to find .git directory starting from current directory
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback to current directory
        return currentDir;
    }
}

/// <summary>
/// Represents the status of a file in Git.
/// </summary>
public class GitFileStatus
{
    public string FilePath { get; set; } = string.Empty;
    public GitFileStatusType Status { get; set; }
}

/// <summary>
/// File status types in Git.
/// </summary>
public enum GitFileStatusType
{
    Untracked,
    Added,
    Modified,
    Deleted
}

