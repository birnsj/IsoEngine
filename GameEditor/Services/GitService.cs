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
    /// Includes both staged and unstaged changes.
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

            var statusCode = line.Substring(0, 2);
            var filePath = line.Substring(2).Trim().Trim('"');

            // Check if this is an untracked file (status code is "??")
            var isUntracked = statusCode == "??";
            
            // Parse both staged (first char) and unstaged (second char) status
            var stagedChar = statusCode.Length > 0 ? statusCode[0] : ' ';
            var unstagedChar = statusCode.Length > 1 ? statusCode[1] : ' ';

            // If file is staged (first char is not space and not ?), it will be included in commit
            // If file is unstaged (second char is not space and not ?), it's a working tree change
            // Untracked files (??) are always unstaged
            var hasStagedChanges = !isUntracked && stagedChar != ' ' && stagedChar != '?';
            var hasUnstagedChanges = isUntracked || (unstagedChar != ' ' && unstagedChar != '?');

            // Include file if it has either staged or unstaged changes (including untracked)
            // If it has both, we show it as unstaged (needs to be re-staged after commit)
            if (hasStagedChanges || hasUnstagedChanges)
            {
                var fileStatus = new GitFileStatus
                {
                    FilePath = filePath,
                    Status = GetStatusFromCode(statusCode),
                    IsStaged = hasStagedChanges && !hasUnstagedChanges,  // Only staged if NOT also unstaged
                    IsUnstaged = hasUnstagedChanges  // Always unstaged if it has unstaged changes (including untracked)
                };

                files.Add(fileStatus);
            }
        }

        return (true, files, string.Empty);
    }

    /// <summary>
    /// Gets the list of staged files (ready to commit).
    /// </summary>
    public static (bool success, List<GitFileStatus> files, string errorMessage) GetStagedFiles()
    {
        var files = new List<GitFileStatus>();
        var (success, output, error) = RunGitCommand("diff --cached --name-status");
        
        if (!success)
        {
            return (false, files, error);
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return (true, files, string.Empty);
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length < 2)
                continue;

            var statusChar = line[0];
            var filePath = line.Substring(1).Trim().Trim('\t', ' ');

            var fileStatus = new GitFileStatus
            {
                FilePath = filePath,
                Status = statusChar switch
                {
                    'A' => GitFileStatusType.Added,
                    'M' => GitFileStatusType.Modified,
                    'D' => GitFileStatusType.Deleted,
                    _ => GitFileStatusType.Modified
                },
                IsStaged = true,
                IsUnstaged = false
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
    /// Fetches changes from remote repository without merging.
    /// </summary>
    public static (bool success, string output, string errorMessage) Fetch()
    {
        return RunGitCommand("fetch origin");
    }

    /// <summary>
    /// Pulls changes from remote repository (fetch + merge).
    /// </summary>
    public static (bool success, string output, string errorMessage) Pull()
    {
        var (success, branchOutput, branchError) = RunGitCommand("rev-parse --abbrev-ref HEAD");
        if (!success)
        {
            return (false, string.Empty, $"Failed to get current branch: {branchError}");
        }

        var branch = branchOutput.Trim();
        return RunGitCommand($"pull origin {branch}");
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

    /// <summary>
    /// Gets the list of commits that are ahead of the remote (committed but not pushed).
    /// </summary>
    public static (bool success, List<GitCommitInfo> commits, string errorMessage) GetUnpushedCommits()
    {
        var commits = new List<GitCommitInfo>();
        
        // First, try to get the remote branch name
        var (branchSuccess, branch, branchError) = GetCurrentBranch();
        if (!branchSuccess)
        {
            return (false, commits, branchError);
        }

        // Check if there's a remote tracking branch
        var (trackSuccess, trackOutput, _) = RunGitCommand($"rev-parse --abbrev-ref --symbolic-full-name @{{u}}");
        if (!trackSuccess)
        {
            // No remote tracking branch - check if remote exists, if not, all commits are unpushed
            var (remoteExists, _, _) = RunGitCommand("ls-remote --heads origin");
            if (remoteExists)
            {
                // Remote exists but no tracking branch - try to get commits vs origin/main
                var (fetchSuccess, _, _) = RunGitCommand("fetch origin --quiet");
                var (success, output, error) = RunGitCommand($"log origin/{branch}..HEAD --oneline --pretty=format:\"%h|%s|%an|%ar\"");
                if (success && !string.IsNullOrEmpty(output))
                {
                    ParseCommits(output, commits);
                }
                else
                {
                    // origin/branch doesn't exist, all local commits are unpushed
                    var (allSuccess, allOutput, _) = RunGitCommand($"log --oneline --pretty=format:\"%h|%s|%an|%ar\" -20");
                    if (allSuccess && !string.IsNullOrEmpty(allOutput))
                    {
                        ParseCommits(allOutput, commits);
                    }
                }
            }
            else
            {
                // No remote - all local commits are unpushed
                var (allSuccess, allOutput, _) = RunGitCommand($"log --oneline --pretty=format:\"%h|%s|%an|%ar\" -20");
                if (allSuccess && !string.IsNullOrEmpty(allOutput))
                {
                    ParseCommits(allOutput, commits);
                }
            }
            return (true, commits, string.Empty);
        }

        // Fetch to ensure we have latest remote info (quietly)
        RunGitCommand("fetch origin --quiet");
        
        // Get commits ahead of remote
        var (success2, output2, error2) = RunGitCommand($"log origin/{branch}..HEAD --oneline --pretty=format:\"%h|%s|%an|%ar\"");
        if (!success2 || string.IsNullOrEmpty(output2))
        {
            // If origin/branch doesn't exist yet, all local commits are unpushed
            var (allSuccess, allOutput, _) = RunGitCommand($"log --oneline --pretty=format:\"%h|%s|%an|%ar\" -20");
            if (allSuccess && !string.IsNullOrEmpty(allOutput))
            {
                ParseCommits(allOutput, commits);
            }
            return (true, commits, string.Empty);
        }

        ParseCommits(output2, commits);
        return (true, commits, string.Empty);
    }

    /// <summary>
    /// Gets the count of commits ahead and behind the remote.
    /// </summary>
    public static (bool success, int ahead, int behind, string errorMessage) GetCommitAheadBehind()
    {
        var (branchSuccess, branch, branchError) = GetCurrentBranch();
        if (!branchSuccess)
        {
            return (false, 0, 0, branchError);
        }

        var (trackSuccess, _, _) = RunGitCommand($"rev-parse --abbrev-ref --symbolic-full-name @{{u}}");
        if (!trackSuccess)
        {
            // No remote tracking branch - check if there are any local commits
            var (hasCommits, commitCount, _) = RunGitCommand("rev-list --count HEAD");
            if (hasCommits && int.TryParse(commitCount.Trim(), out var count) && count > 0)
            {
                // Check if remote exists
                var (remoteExists, _, _) = RunGitCommand("ls-remote --heads origin");
                if (remoteExists)
                {
                    // Remote exists but no tracking - all commits are ahead
                    return (true, count, 0, string.Empty);
                }
                else
                {
                    // No remote - all commits are unpushed
                    return (true, count, 0, string.Empty);
                }
            }
            return (true, 0, 0, string.Empty);
        }

        var (success, output, error) = RunGitCommand($"rev-list --left-right --count origin/{branch}...HEAD");
        if (!success)
        {
            return (false, 0, 0, error);
        }

        var parts = output.Trim().Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (true, 0, 0, string.Empty);
        }

        if (int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
        {
            return (true, ahead, behind, string.Empty);
        }

        return (true, 0, 0, string.Empty);
    }

    private static void ParseCommits(string output, List<GitCommitInfo> commits)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('|');
            if (parts.Length >= 4)
            {
                commits.Add(new GitCommitInfo
                {
                    Hash = parts[0],
                    Message = parts[1],
                    Author = parts[2],
                    RelativeTime = parts[3]
                });
            }
        }
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
    public bool IsStaged { get; set; }
    public bool IsUnstaged { get; set; }
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

/// <summary>
/// Represents information about a Git commit.
/// </summary>
public class GitCommitInfo
{
    public string Hash { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string RelativeTime { get; set; } = string.Empty;
}

