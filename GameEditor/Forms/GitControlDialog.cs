using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GameEditor.Services;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for Git control operations - shows changed files and allows committing/pushing.
/// </summary>
public class GitControlDialog : Form
{
    private ListBox? _changedFilesListBox;
    private TextBox? _commitMessageTextBox;
    private Button? _refreshButton;
    private Button? _stageAllButton;
    private Button? _commitButton;
    private Button? _pushButton;
    private Label? _statusLabel;
    private Label? _branchLabel;
    private Label? _remoteLabel;

    public GitControlDialog()
    {
        InitializeComponent();
        RefreshFileList();
        UpdateStatusInfo();
    }

    private void InitializeComponent()
    {
        Text = "Git Control";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(700, 500);

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
            Size = new Size(750, 80),
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

        // Changed files list
        var filesLabel = new Label
        {
            Text = "Changed Files:",
            Location = new Point(15, yPos),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 10f, FontStyle.Bold)
        };
        mainPanel.Controls.Add(filesLabel);

        yPos += 25;

        _changedFilesListBox = new ListBox
        {
            Location = new Point(15, yPos),
            Size = new Size(750, 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            SelectionMode = SelectionMode.MultiExtended
        };
        mainPanel.Controls.Add(_changedFilesListBox);

        yPos += 260;

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
            Size = new Size(750, 60),
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
        _refreshButton.Click += (s, e) => RefreshFileList();
        buttonPanel.Controls.Add(_refreshButton);

        _stageAllButton = new Button
        {
            Text = "Stage All",
            Size = new Size(100, 30),
            Location = new Point(120, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _stageAllButton.Click += (s, e) => StageAllFiles();
        buttonPanel.Controls.Add(_stageAllButton);

        _commitButton = new Button
        {
            Text = "Commit",
            Size = new Size(100, 30),
            Location = new Point(230, 10),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _commitButton.Click += (s, e) => CommitChanges();
        buttonPanel.Controls.Add(_commitButton);

        _pushButton = new Button
        {
            Text = "Push",
            Size = new Size(100, 30),
            Location = new Point(340, 10),
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

        _statusLabel!.Text = "Status: Ready";
    }

    private void RefreshFileList()
    {
        if (_changedFilesListBox == null)
            return;

        _changedFilesListBox.Items.Clear();

        if (!GitService.IsGitRepository())
        {
            _changedFilesListBox.Items.Add("Not a Git repository");
            return;
        }

        var (success, files, error) = GitService.GetChangedFiles();
        if (!success)
        {
            _changedFilesListBox.Items.Add($"Error: {error}");
            return;
        }

        if (files.Count == 0)
        {
            _changedFilesListBox.Items.Add("No changes detected");
            return;
        }

        foreach (var file in files.OrderBy(f => f.Status).ThenBy(f => f.FilePath))
        {
            var statusPrefix = file.Status switch
            {
                GitFileStatusType.Untracked => "[NEW]",
                GitFileStatusType.Added => "[ADDED]",
                GitFileStatusType.Modified => "[MODIFIED]",
                GitFileStatusType.Deleted => "[DELETED]",
                _ => "[?]"
            };

            _changedFilesListBox.Items.Add($"{statusPrefix} {file.FilePath}");
        }

        _statusLabel!.Text = $"Status: {files.Count} file(s) changed";
    }

    private void StageAllFiles()
    {
        var (success, output, error) = GitService.StageAllFiles();
        if (success)
        {
            MessageBox.Show("All files staged successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
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

        var (success, output, error) = GitService.Commit(message);
        if (success)
        {
            MessageBox.Show("Changes committed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
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

    private void PushChanges()
    {
        var result = MessageBox.Show("Push changes to remote repository?", "Confirm Push", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
            return;

        var (success, output, error) = GitService.Push();
        if (success)
        {
            MessageBox.Show("Changes pushed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFileList();
        }
        else
        {
            var errorMsg = string.IsNullOrEmpty(error) ? output : error;
            MessageBox.Show($"Error pushing changes:\n{errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

