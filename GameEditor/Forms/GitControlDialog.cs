using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GameEditor.Services;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for Git control operations - shows changed files and allows committing/pushing.
/// </summary>
public class GitControlDialog : Form
{
    private ListBox? _unstagedFilesListBox;
    private ListBox? _stagedFilesListBox;
    private ListBox? _committedListBox;
    private TextBox? _commitMessageTextBox;
    private Button? _refreshButton;
    private Button? _fetchButton;
    private Button? _pullButton;
    private Button? _stageAllButton;
    private Button? _commitButton;
    private Button? _pushButton;
    private Label? _statusLabel;
    private Label? _branchLabel;
    private Label? _remoteLabel;
    private Label? _committedLabel;
    private Label? _stagedLabel;
    private SplitContainer? _mainSplitContainer;

    public GitControlDialog()
    {
        InitializeComponent();
        RefreshFileList();
        RefreshStagedList();
        RefreshCommittedList();
        UpdateStatusInfo();
    }

    private void InitializeComponent()
    {
        Text = "Git Control";
        Size = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 600);

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };

        int yPos = 10;

        // Status info section
        var statusPanel = new Panel
        {
            Location = new Point(15, yPos),
            Size = new Size(950, 80),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _branchLabel = new Label
        {
            Text = "Branch: ",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 9f, FontStyle.Bold)
        };
        statusPanel.Controls.Add(_branchLabel);

        _remoteLabel = new Label
        {
            Text = "Remote: ",
            Location = new Point(10, 30),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 9f)
        };
        statusPanel.Controls.Add(_remoteLabel);

        _statusLabel = new Label
        {
            Text = "Status: Checking...",
            Location = new Point(10, 50),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 9f)
        };
        statusPanel.Controls.Add(_statusLabel);

        mainPanel.Controls.Add(statusPanel);
        yPos += 90;

        // Split container for three sections: Unstaged, Staged, and Committed
        _mainSplitContainer = new SplitContainer
        {
            Orientation = Orientation.Horizontal,
            Location = new Point(15, yPos),
            Size = new Size(950, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            SplitterDistance = 200
        };

        // Top panel: Unstaged files
        var unstagedFilesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        var unstagedLabel = new Label
        {
            Text = "Unstaged Changes:",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font(DefaultFont.FontFamily, 10f, FontStyle.Bold)
        };
        _unstagedFilesListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.MultiExtended
        };
        unstagedFilesPanel.Controls.Add(_unstagedFilesListBox);
        unstagedFilesPanel.Controls.Add(unstagedLabel);

        // Bottom split container for Staged and Committed
        var bottomSplitContainer = new SplitContainer
        {
            Orientation = Orientation.Horizontal,
            Dock = DockStyle.Fill,
            SplitterDistance = 180
        };

        // Middle panel: Staged files
        var stagedFilesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        _stagedLabel = new Label
        {
            Text = "Staged Files (Ready to Commit):",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font(DefaultFont.FontFamily, 10f, FontStyle.Bold)
        };
        _stagedFilesListBox = new ListBox
        {
            Dock = DockStyle.Fill
        };
        stagedFilesPanel.Controls.Add(_stagedFilesListBox);
        stagedFilesPanel.Controls.Add(_stagedLabel);

        // Bottom panel: Committed changes (ready to push)
        var committedPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        _committedLabel = new Label
        {
            Text = "Committed Changes (Ready to Push):",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font(DefaultFont.FontFamily, 10f, FontStyle.Bold)
        };
        _committedListBox = new ListBox
        {
            Dock = DockStyle.Fill
        };
        committedPanel.Controls.Add(_committedListBox);
        committedPanel.Controls.Add(_committedLabel);

        bottomSplitContainer.Panel1.Controls.Add(stagedFilesPanel);
        bottomSplitContainer.Panel2.Controls.Add(committedPanel);

        _mainSplitContainer.Panel1.Controls.Add(unstagedFilesPanel);
        _mainSplitContainer.Panel2.Controls.Add(bottomSplitContainer);

        mainPanel.Controls.Add(_mainSplitContainer);
        yPos += 410;

        // Commit message section
        var commitLabel = new Label
        {
            Text = "Commit Message:",
            Location = new Point(15, yPos),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 10f, FontStyle.Bold)
        };
        mainPanel.Controls.Add(commitLabel);

        yPos += 25;

        _commitMessageTextBox = new TextBox
        {
            Location = new Point(15, yPos),
            Size = new Size(950, 60),
            Multiline = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "Update from IsoEngine Editor"
        };
        mainPanel.Controls.Add(_commitMessageTextBox);

        yPos += 70;

        // Buttons panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50
        };

        _refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(100, 30),
            Location = new Point(10, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _refreshButton.Click += (s, e) => {
            RefreshFileList();
            RefreshStagedList();
            RefreshCommittedList();
            UpdateStatusInfo();
        };
        buttonPanel.Controls.Add(_refreshButton);

        _fetchButton = new Button
        {
            Text = "Fetch",
            Size = new Size(90, 30),
            Location = new Point(120, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _fetchButton.Click += (s, e) => FetchChanges();
        buttonPanel.Controls.Add(_fetchButton);

        _pullButton = new Button
        {
            Text = "Pull",
            Size = new Size(90, 30),
            Location = new Point(220, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _pullButton.Click += (s, e) => PullChanges();
        buttonPanel.Controls.Add(_pullButton);

        _stageAllButton = new Button
        {
            Text = "Stage All",
            Size = new Size(90, 30),
            Location = new Point(320, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _stageAllButton.Click += (s, e) => StageAllFiles();
        buttonPanel.Controls.Add(_stageAllButton);

        _commitButton = new Button
        {
            Text = "Commit",
            Size = new Size(90, 30),
            Location = new Point(420, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _commitButton.Click += (s, e) => CommitChanges();
        buttonPanel.Controls.Add(_commitButton);

        _pushButton = new Button
        {
            Text = "Push",
            Size = new Size(90, 30),
            Location = new Point(520, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _pushButton.Click += (s, e) => PushChanges();
        buttonPanel.Controls.Add(_pushButton);

        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(100, 30),
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        
        void UpdateButtonPositions()
        {
            closeButton.Location = new Point(Width - closeButton.Width - 20, 10);
        }
        UpdateButtonPositions();
        this.Resize += (s, e) => UpdateButtonPositions();
        buttonPanel.Controls.Add(closeButton);

        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);

        AcceptButton = _commitButton;
        CancelButton = closeButton;
    }

    private void UpdateStatusInfo()
    {
        if (!GitService.IsGitRepository())
        {
            _statusLabel!.Text = "Status: Not a Git repository";
            _branchLabel!.Text = "Branch: N/A";
            _remoteLabel!.Text = "Remote: N/A";
            return;
        }

        var (branchSuccess, branch, branchError) = GitService.GetCurrentBranch();
        if (branchSuccess)
        {
            _branchLabel!.Text = $"Branch: {branch}";
        }
        else
        {
            _branchLabel!.Text = "Branch: Unknown";
        }

        var (urlSuccess, url, urlError) = GitService.GetRemoteUrl();
        if (urlSuccess)
        {
            _remoteLabel!.Text = $"Remote: {url}";
        }
        else
        {
            _remoteLabel!.Text = "Remote: Not configured";
        }

        // Check commit status
        var (aheadSuccess, ahead, behind, _) = GitService.GetCommitAheadBehind();
        if (aheadSuccess)
        {
            var statusParts = new List<string>();
            if (ahead > 0)
                statusParts.Add($"{ahead} ahead");
            if (behind > 0)
                statusParts.Add($"{behind} behind");
            
            if (statusParts.Count > 0)
                _statusLabel!.Text = $"Status: {string.Join(", ", statusParts)}";
            else
                _statusLabel!.Text = "Status: Up to date";
        }
        else
        {
            _statusLabel!.Text = "Status: Ready";
        }
    }

    private void RefreshFileList()
    {
        if (_unstagedFilesListBox == null)
            return;

        _unstagedFilesListBox.Items.Clear();

        if (!GitService.IsGitRepository())
        {
            _unstagedFilesListBox.Items.Add("Not a Git repository");
            return;
        }

        var (success, files, error) = GitService.GetChangedFiles();
        if (!success)
        {
            _unstagedFilesListBox.Items.Add($"Error: {error}");
            return;
        }

        // Only show unstaged files
        var unstagedFiles = files.Where(f => f.IsUnstaged).ToList();

        if (unstagedFiles.Count == 0)
        {
            _unstagedFilesListBox.Items.Add("No unstaged changes");
            return;
        }

        foreach (var file in unstagedFiles.OrderBy(f => f.Status).ThenBy(f => f.FilePath))
        {
            var statusPrefix = file.Status switch
            {
                GitFileStatusType.Untracked => "[NEW]",
                GitFileStatusType.Added => "[ADDED]",
                GitFileStatusType.Modified => "[MODIFIED]",
                GitFileStatusType.Deleted => "[DELETED]",
                _ => "[?]"
            };

            _unstagedFilesListBox.Items.Add($"{statusPrefix} {file.FilePath}");
        }
    }

    private void RefreshStagedList()
    {
        if (_stagedFilesListBox == null)
            return;

        _stagedFilesListBox.Items.Clear();

        if (!GitService.IsGitRepository())
        {
            _stagedFilesListBox.Items.Add("Not a Git repository");
            return;
        }

        var (success, files, error) = GitService.GetStagedFiles();
        if (!success)
        {
            _stagedFilesListBox.Items.Add($"Error: {error}");
            return;
        }

        if (files.Count == 0)
        {
            _stagedFilesListBox.Items.Add("No staged files");
            _stagedLabel!.Text = "Staged Files (Ready to Commit):";
            return;
        }

        _stagedLabel!.Text = $"Staged Files (Ready to Commit): {files.Count} file(s)";

        foreach (var file in files.OrderBy(f => f.Status).ThenBy(f => f.FilePath))
        {
            var statusPrefix = file.Status switch
            {
                GitFileStatusType.Added => "[ADDED]",
                GitFileStatusType.Modified => "[MODIFIED]",
                GitFileStatusType.Deleted => "[DELETED]",
                _ => "[?]"
            };

            _stagedFilesListBox.Items.Add($"{statusPrefix} {file.FilePath}");
        }
    }

    private void RefreshCommittedList()
    {
        if (_committedListBox == null)
            return;

        _committedListBox.Items.Clear();

        if (!GitService.IsGitRepository())
        {
            _committedListBox.Items.Add("Not a Git repository");
            return;
        }

        // Get count of commits ahead
        var (aheadSuccess, ahead, behind, aheadError) = GitService.GetCommitAheadBehind();
        if (aheadSuccess && ahead > 0)
        {
            _committedLabel!.Text = $"Committed Changes (Ready to Push): {ahead} commit(s) ahead";
            
            // Get the actual commits
            var (success, commits, error) = GitService.GetUnpushedCommits();
            if (success && commits.Count > 0)
            {
                foreach (var commit in commits)
                {
                    var shortHash = commit.Hash.Length > 7 ? commit.Hash.Substring(0, 7) : commit.Hash;
                    _committedListBox.Items.Add($"{shortHash} - {commit.Message} ({commit.RelativeTime})");
                }
            }
            else
            {
                _committedListBox.Items.Add($"{ahead} commit(s) ready to push");
            }
        }
        else
        {
            _committedLabel!.Text = "Committed Changes (Ready to Push):";
            
            // Still check for commits even if ahead/behind doesn't show them
            var (success, commits, error) = GitService.GetUnpushedCommits();
            if (success && commits.Count > 0)
            {
                _committedLabel!.Text = $"Committed Changes (Ready to Push): {commits.Count} commit(s)";
                foreach (var commit in commits)
                {
                    var shortHash = commit.Hash.Length > 7 ? commit.Hash.Substring(0, 7) : commit.Hash;
                    _committedListBox.Items.Add($"{shortHash} - {commit.Message} ({commit.RelativeTime})");
                }
            }
            else
            {
                _committedListBox.Items.Add("No commits to push");
            }
        }
    }

    private void StageAllFiles()
    {
        var (success, output, error) = GitService.StageAllFiles();
        if (success)
        {
            MessageBox.Show("All files staged successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
            RefreshStagedList();
        }
        else
        {
            MessageBox.Show($"Error staging files:\n{error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CommitChanges()
    {
        var message = _commitMessageTextBox?.Text?.Trim() ?? "Update from IsoEngine Editor";
        
        if (string.IsNullOrWhiteSpace(message))
        {
            MessageBox.Show("Please enter a commit message.", "No Message", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Check if there are any staged changes to commit by checking git diff --cached
        var (hasStagedSuccess, stagedOutput, _) = RunGitCommand("diff --cached --name-only");
        var hasStagedChanges = hasStagedSuccess && !string.IsNullOrWhiteSpace(stagedOutput);
        
        if (!hasStagedChanges)
        {
            MessageBox.Show("No staged changes to commit. Stage files first using 'Stage All'.", "Nothing to Commit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var (success, output, error) = GitService.Commit(message);
        if (success)
        {
            MessageBox.Show("Changes committed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Small delay to ensure git has processed the commit
            System.Threading.Thread.Sleep(100);
            
            // Refresh all views
            RefreshFileList();
            RefreshStagedList();
            RefreshCommittedList();
            UpdateStatusInfo();
            
            // Reset commit message to default
            if (_commitMessageTextBox != null)
            {
                _commitMessageTextBox.Text = "Update from IsoEngine Editor";
            }
        }
        else
        {
            var errorMsg = string.IsNullOrEmpty(error) ? output : error;
            if (errorMsg.Contains("nothing to commit"))
            {
                MessageBox.Show("No changes to commit. Stage files first.", "Nothing to Commit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Error committing changes:\n{errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private (bool success, string output, string error) RunGitCommand(string arguments)
    {
        try
        {
            var repoRoot = GetRepositoryRoot();
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repoRoot,
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

    private string GetRepositoryRoot()
    {
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

        return currentDir;
    }

    private void FetchChanges()
    {
        var (success, output, error) = GitService.Fetch();
        if (success)
        {
            var message = "Fetch completed successfully!";
            if (!string.IsNullOrEmpty(output))
            {
                message += $"\n\n{output}";
            }
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
            RefreshStagedList();
            RefreshCommittedList();
            UpdateStatusInfo();
        }
        else
        {
            var errorMsg = string.IsNullOrEmpty(error) ? output : error;
            MessageBox.Show($"Error fetching changes:\n{errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PullChanges()
    {
        // Check if there are uncommitted changes that might conflict
        var (hasChanges, files, _) = GitService.GetChangedFiles();
        if (hasChanges && files.Count > 0)
        {
            var result = MessageBox.Show(
                $"You have {files.Count} uncommitted change(s). Pulling might cause conflicts.\n\n" +
                "Would you like to continue anyway?",
                "Uncommitted Changes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes)
                return;
        }

        var (success, output, error) = GitService.Pull();
        if (success)
        {
            var message = "Pull completed successfully!";
            if (!string.IsNullOrEmpty(output))
            {
                message += $"\n\n{output}";
            }
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
            RefreshStagedList();
            RefreshCommittedList();
            UpdateStatusInfo();
        }
        else
        {
            var errorMsg = string.IsNullOrEmpty(error) ? output : error;
            if (errorMsg.Contains("CONFLICT") || errorMsg.Contains("conflict"))
            {
                MessageBox.Show(
                    $"Merge conflicts detected during pull:\n\n{errorMsg}\n\n" +
                    "Please resolve the conflicts manually using Git commands or a Git client.",
                    "Merge Conflicts",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show($"Error pulling changes:\n{errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PushChanges()
    {
        // Check if there are commits to push
        var (aheadSuccess, ahead, behind, _) = GitService.GetCommitAheadBehind();
        if (!aheadSuccess || ahead == 0)
        {
            MessageBox.Show("No commits to push. Commit your changes first.", "Nothing to Push", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Check if remote has changes that need to be pulled first
        if (behind > 0)
        {
            var pullResult = MessageBox.Show(
                $"The remote repository has {behind} commit(s) that you don't have locally.\n\n" +
                "You need to pull and merge these changes before pushing.\n\n" +
                "Would you like to pull now?",
                "Pull Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (pullResult == DialogResult.Yes)
            {
                PullChanges();
                // After pulling, try push again if still ahead
                var (newAheadSuccess, newAhead, _, _) = GitService.GetCommitAheadBehind();
                if (newAheadSuccess && newAhead > 0)
                {
                    // Ask again to push after pull
                    var pushAfterPull = MessageBox.Show(
                        $"After pulling, you still have {newAhead} commit(s) to push.\n\nWould you like to push now?",
                        "Push After Pull",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (pushAfterPull == DialogResult.Yes)
                    {
                        // Continue with push below
                        ahead = newAhead;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        var (commitSuccess, commits, _) = GitService.GetUnpushedCommits();
        var commitList = commitSuccess && commits.Count > 0 
            ? string.Join("\n", commits.Take(5).Select(c => $"- {c.Hash.Substring(0, Math.Min(7, c.Hash.Length))}: {c.Message}"))
            : $"{ahead} commit(s)";
        
        if (commits.Count > 5)
        {
            commitList += $"\n... and {commits.Count - 5} more";
        }

        var result = MessageBox.Show(
            $"Push {ahead} committed change(s) to remote repository?\n\n{commitList}",
            "Confirm Push",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result != DialogResult.Yes)
            return;

        var (success, output, error) = GitService.Push();
        if (success)
        {
            MessageBox.Show("Committed changes pushed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
            RefreshStagedList();
            RefreshCommittedList();
            UpdateStatusInfo();
        }
        else
        {
            var errorMsg = string.IsNullOrEmpty(error) ? output : error;
            
            // Check for the specific "fetch first" error
            if (errorMsg.Contains("fetch first") || errorMsg.Contains("Updates were rejected"))
            {
                var pullResult = MessageBox.Show(
                    $"The remote repository has changes that you don't have locally.\n\n" +
                    $"Error: {errorMsg}\n\n" +
                    "Would you like to pull and merge the remote changes first?",
                    "Pull Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (pullResult == DialogResult.Yes)
                {
                    PullChanges();
                    // After pulling, try push again
                    var (retrySuccess, retryOutput, retryError) = GitService.Push();
                    if (retrySuccess)
                    {
                        MessageBox.Show("Committed changes pushed successfully after pull!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshFileList();
                        RefreshStagedList();
                        RefreshCommittedList();
                        UpdateStatusInfo();
                    }
                    else
                    {
                        MessageBox.Show($"Error pushing after pull:\n{retryError}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show($"Error pushing changes:\n{errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

