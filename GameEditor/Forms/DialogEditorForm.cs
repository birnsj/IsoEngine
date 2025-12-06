using GameCore.Dialogs;
using GameEditor.Services;
using GameEditor.Utilities;
using System.IO;
using System.Text.Json;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing dialog graphs.
/// </summary>
public partial class DialogEditorForm : Form
{
    private DialogGraph? _dialogGraph;
    private ListBox? _dialogList;
    private ListBox? _nodeList;
    private TextBox? _nodeIdTextBox;
    private TextBox? _nodeTextTextBox;
    private ListBox? _choicesList;
    private string? _currentFilePath;
    private DialogNode? _selectedNode;

    public DialogEditorForm()
    {
        InitializeComponent();
        
        // Validate GameContent path (ensures consistency with game)
        PathHelper.ValidateGameContentPath();
        
        LoadDialogs();
        AutoLoadDefaultDialog();
    }

    private void InitializeComponent()
    {
        Text = "Dialog Editor";
        // No fixed size - will fill tab container
        MinimumSize = new Size(600, 400);

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top
        };

        var newButton = new ToolStripButton("New Dialog", null, (s, e) => NewDialog());
        var openButton = new ToolStripButton("Open", null, (s, e) => OpenDialog());
        var saveButton = new ToolStripButton("Save", null, (s, e) => SaveDialog());
        var saveAsButton = new ToolStripButton("Save As", null, (s, e) => SaveDialogAs());
        toolStrip.Items.Add(newButton);
        toolStrip.Items.Add(openButton);
        toolStrip.Items.Add(saveButton);
        toolStrip.Items.Add(saveAsButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        var addNodeButton = new ToolStripButton("Add Node", null, (s, e) => AddNode());
        var removeNodeButton = new ToolStripButton("Remove Node", null, (s, e) => RemoveNode());
        toolStrip.Items.Add(addNodeButton);
        toolStrip.Items.Add(removeNodeButton);

        var mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        mainSplitContainer.Panel1MinSize = 200; // Minimum width for left panel
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid crashes

        // Left panel: Dialog and node list
        var leftPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        leftPanel.Panel1MinSize = 150; // Minimum height for dialog list
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid crashes

        var dialogListPanel = new Panel { Dock = DockStyle.Fill };
        var dialogListLabel = new Label
        {
            Text = "Dialogs",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _dialogList = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _dialogList.SelectedIndexChanged += (s, e) => LoadSelectedDialog();
        dialogListPanel.Controls.Add(dialogListLabel);
        dialogListPanel.Controls.Add(_dialogList);

        var nodeListPanel = new Panel { Dock = DockStyle.Fill };
        var nodeListLabel = new Label
        {
            Text = "Nodes",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _nodeList = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _nodeList.SelectedIndexChanged += (s, e) => LoadSelectedNode();
        nodeListPanel.Controls.Add(nodeListLabel);
        nodeListPanel.Controls.Add(_nodeList);

        leftPanel.Panel1.Controls.Add(dialogListPanel);
        leftPanel.Panel2.Controls.Add(nodeListPanel);

        // Right panel: Node editor - use proper docking for scaling
        var rightPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        rightPanel.Panel1MinSize = 200; // Minimum height for node editor
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid crashes

        // Top panel: Node ID and Text
        var nodeEditorPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 12) };
        
        // Node ID section
        var nodeIdPanel = new Panel { Dock = DockStyle.Top, Height = 45 };
        var nodeIdLabel = new Label { Text = "Node ID:", Dock = DockStyle.Left, Width = 90, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 0, 5, 0) };
        _nodeIdTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 8, 5, 8) };
        _nodeIdTextBox.TextChanged += (s, e) => UpdateNodeId();
        nodeIdPanel.Controls.Add(nodeIdLabel);
        nodeIdPanel.Controls.Add(_nodeIdTextBox);

        // Node Text section
        var nodeTextPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        var nodeTextLabel = new Label { Text = "Text:", Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 2, 5, 2) };
        _nodeTextTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Margin = new Padding(5, 2, 5, 2)
        };
        _nodeTextTextBox.TextChanged += (s, e) => UpdateNodeText();
        nodeTextPanel.Controls.Add(nodeTextLabel);
        nodeTextPanel.Controls.Add(_nodeTextTextBox);

        nodeEditorPanel.Controls.Add(nodeTextPanel);
        nodeEditorPanel.Controls.Add(nodeIdPanel);

        // Bottom panel: Choices
        var choicesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 12) };
        var choicesLabel = new Label { Text = "Choices:", Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 5, 5, 5) };
        
        var choicesListPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        _choicesList = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 5, 0)
        };
        _choicesList.SelectedIndexChanged += (s, e) => LoadSelectedChoice();
        choicesListPanel.Controls.Add(_choicesList);

        var choicesButtonPanel = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5, 5, 5, 5) };
        var addChoiceButton = new Button { Text = "Add Choice", Dock = DockStyle.Left, Width = 130, Margin = new Padding(0, 0, 8, 0), Height = 30 };
        addChoiceButton.Click += (s, e) => AddChoice();
        var removeChoiceButton = new Button { Text = "Remove Choice", Dock = DockStyle.Left, Width = 130, Height = 30 };
        removeChoiceButton.Click += (s, e) => RemoveChoice();
        choicesButtonPanel.Controls.Add(removeChoiceButton);
        choicesButtonPanel.Controls.Add(addChoiceButton);

        choicesPanel.Controls.Add(choicesButtonPanel);
        choicesPanel.Controls.Add(choicesListPanel);
        choicesPanel.Controls.Add(choicesLabel);

        rightPanel.Panel1.Controls.Add(nodeEditorPanel);
        rightPanel.Panel2.Controls.Add(choicesPanel);

        mainSplitContainer.Panel1.Controls.Add(leftPanel);
        mainSplitContainer.Panel2.Controls.Add(rightPanel);

        Controls.Add(toolStrip);
        Controls.Add(mainSplitContainer);
        
        // Set splitter distances and minimum sizes after form is loaded to avoid crashes
        Load += (s, e) =>
        {
            // Set main split container
            if (mainSplitContainer.Width > 0)
            {
                // Set Panel2MinSize dynamically
                var availableWidth = mainSplitContainer.Width;
                mainSplitContainer.Panel2MinSize = Math.Min(300, (int)(availableWidth * 0.3));
                
                // Set splitter distance
                var preferredDistance = 280;
                var maxDistance = mainSplitContainer.Width - mainSplitContainer.Panel2MinSize;
                var minDistance = mainSplitContainer.Panel1MinSize;
                
                if (maxDistance >= minDistance)
                {
                    mainSplitContainer.SplitterDistance = Math.Max(minDistance, Math.Min(preferredDistance, maxDistance));
                }
                else
                {
                    mainSplitContainer.SplitterDistance = minDistance;
                }
            }
            
            // Set left panel (dialog and node list)
            if (leftPanel.Width > 0)
            {
                // Set Panel2MinSize dynamically
                var availableHeight = leftPanel.Height;
                leftPanel.Panel2MinSize = Math.Min(150, (int)(availableHeight * 0.3));
                
                // Set splitter distance
                var preferredLeftDistance = 280;
                var maxLeftDistance = leftPanel.Height - leftPanel.Panel2MinSize;
                var minLeftDistance = leftPanel.Panel1MinSize;
                
                if (maxLeftDistance >= minLeftDistance)
                {
                    leftPanel.SplitterDistance = Math.Max(minLeftDistance, Math.Min(preferredLeftDistance, maxLeftDistance));
                }
                else
                {
                    leftPanel.SplitterDistance = minLeftDistance;
                }
            }
            
            // Set right panel (node editor and choices)
            if (rightPanel.Height > 0)
            {
                // Set Panel2MinSize dynamically
                var availableHeight = rightPanel.Height;
                rightPanel.Panel2MinSize = Math.Min(150, (int)(availableHeight * 0.25));
                
                // Set splitter distance
                var preferredRightDistance = 450;
                var maxRightDistance = rightPanel.Height - rightPanel.Panel2MinSize;
                var minRightDistance = rightPanel.Panel1MinSize;
                
                if (maxRightDistance >= minRightDistance)
                {
                    rightPanel.SplitterDistance = Math.Max(minRightDistance, Math.Min(preferredRightDistance, maxRightDistance));
                }
                else
                {
                    rightPanel.SplitterDistance = minRightDistance;
                }
            }
        };
    }

    private void LoadDialogs()
    {
        if (_dialogList == null)
            return;

        _dialogList.Items.Clear();

        var dialogsPath = PathHelper.GetDialogsDirectory();
        if (dialogsPath == null)
        {
            dialogsPath = Path.Combine(Application.StartupPath, "..", "GameContent", "dialogs");
        }

        if (Directory.Exists(dialogsPath))
        {
            var dialogFiles = Directory.GetFiles(dialogsPath, "*.json");
            foreach (var file in dialogFiles)
            {
                _dialogList.Items.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
    }

    /// <summary>
    /// Automatically loads the first/default dialog file found.
    /// </summary>
    private void AutoLoadDefaultDialog()
    {
        var dialogsPath = PathHelper.GetDialogsDirectory();
        if (dialogsPath == null)
        {
            dialogsPath = Path.Combine(Application.StartupPath, "..", "GameContent", "dialogs");
        }

        if (!Directory.Exists(dialogsPath))
            return;

        var dialogFiles = Directory.GetFiles(dialogsPath, "*.json");
        if (dialogFiles.Length == 0)
            return;

        // Prefer "guard_dialog.json" if it exists, otherwise use the first file found
        var defaultDialog = Array.Find(dialogFiles, f => 
            Path.GetFileName(f).Equals("guard_dialog.json", StringComparison.OrdinalIgnoreCase));
        
        var dialogFile = defaultDialog ?? dialogFiles[0];
        
        if (File.Exists(dialogFile))
        {
            LoadDialogFromFile(dialogFile);
        }
    }

    private void NewDialog()
    {
        var dialog = new NewDialogDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _dialogGraph = new DialogGraph
            {
                Id = dialog.DialogId,
                Name = dialog.DialogName,
                StartNodeId = ""
            };

            // Create initial node
            var startNode = new DialogNode
            {
                Id = "start",
                Text = "Enter dialog text here..."
            };
            _dialogGraph.Nodes["start"] = startNode;
            _dialogGraph.StartNodeId = "start";

            _currentFilePath = null;
            RefreshNodeList();
        }
    }

    private void OpenDialog()
    {
        var dialogsPath = PathHelper.GetDialogsDirectory();
        if (dialogsPath == null)
        {
            dialogsPath = Path.Combine(Application.StartupPath, "..", "GameContent", "dialogs");
            if (!Directory.Exists(dialogsPath))
            {
                Directory.CreateDirectory(dialogsPath);
            }
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = dialogsPath
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadDialogFromFile(dialog.FileName);
        }
    }

    private void LoadDialogFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            _dialogGraph = DialogLoader.LoadDialogFromJson(json);
            _currentFilePath = filePath;
            Text = $"Dialog Editor - {Path.GetFileName(filePath)}";
            RefreshNodeList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadSelectedDialog()
    {
        if (_dialogList == null || _dialogList.SelectedIndex < 0)
            return;

        var dialogsPath = PathHelper.GetDialogsDirectory();
        if (dialogsPath == null)
        {
            dialogsPath = Path.Combine(Application.StartupPath, "..", "GameContent", "dialogs");
        }

        var fileName = _dialogList.SelectedItem?.ToString() + ".json";
        var filePath = Path.Combine(dialogsPath, fileName);

        if (File.Exists(filePath))
        {
            LoadDialogFromFile(filePath);
        }
    }

    private void SaveDialog()
    {
        if (_currentFilePath != null)
        {
            SaveToFile(_currentFilePath);
        }
        else
        {
            SaveDialogAs();
        }
    }

    /// <summary>
    /// Public method to save the dialog. Can be called from MainEditorForm's Save All.
    /// </summary>
    public void Save()
    {
        SaveDialog();
    }

    private void SaveDialogAs()
    {
        var dialogsPath = PathHelper.GetDialogsDirectory();
        if (dialogsPath == null)
        {
            dialogsPath = Path.Combine(Application.StartupPath, "..", "GameContent", "dialogs");
            if (!Directory.Exists(dialogsPath))
            {
                Directory.CreateDirectory(dialogsPath);
            }
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = dialogsPath,
            FileName = _dialogGraph?.Id + ".json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveToFile(_currentFilePath);
            Text = $"Dialog Editor - {Path.GetFileName(_currentFilePath)}";
            LoadDialogs();
        }
    }

    private void SaveToFile(string filePath)
    {
        if (_dialogGraph == null)
            return;

        try
        {
            // Ensure file is ready for editing in Perforce
            PerforceService.EnsureFileReadyForEdit(filePath);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_dialogGraph, options);
            File.WriteAllText(filePath, json);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(filePath);
            
            MessageBox.Show("Dialog saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshNodeList()
    {
        if (_nodeList == null || _dialogGraph == null)
            return;

        _nodeList.Items.Clear();
        foreach (var node in _dialogGraph.Nodes.Values)
        {
            _nodeList.Items.Add(node.Id);
        }
    }

    private void LoadSelectedNode()
    {
        if (_nodeList == null || _dialogGraph == null || _nodeList.SelectedIndex < 0)
            return;

        var nodeId = _nodeList.SelectedItem?.ToString();
        if (nodeId != null && _dialogGraph.Nodes.TryGetValue(nodeId, out var node))
        {
            _selectedNode = node;
            if (_nodeIdTextBox != null) _nodeIdTextBox.Text = node.Id;
            if (_nodeTextTextBox != null) _nodeTextTextBox.Text = node.Text;
            RefreshChoicesList();
        }
    }

    private void UpdateNodeId()
    {
        if (_selectedNode == null || _dialogGraph == null || _nodeIdTextBox == null)
            return;

        var newId = _nodeIdTextBox.Text;
        if (newId != _selectedNode.Id && !string.IsNullOrEmpty(newId))
        {
            // Remove old node
            _dialogGraph.Nodes.Remove(_selectedNode.Id);
            
            // Update node ID
            _selectedNode.Id = newId;
            
            // Add with new ID
            _dialogGraph.Nodes[newId] = _selectedNode;

            // Update start node if needed
            if (_dialogGraph.StartNodeId == _selectedNode.Id)
            {
                _dialogGraph.StartNodeId = newId;
            }

            RefreshNodeList();
        }
    }

    private void UpdateNodeText()
    {
        if (_selectedNode != null && _nodeTextTextBox != null)
        {
            _selectedNode.Text = _nodeTextTextBox.Text;
        }
    }

    private void RefreshChoicesList()
    {
        if (_choicesList == null || _selectedNode == null)
            return;

        _choicesList.Items.Clear();
        foreach (var choice in _selectedNode.Choices)
        {
            _choicesList.Items.Add($"{choice.Text} -> {choice.NextNodeId ?? "END"}");
        }
    }

    private void AddNode()
    {
        if (_dialogGraph == null)
            return;

        var dialog = new AddNodeDialog();
        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dialog.NodeId))
        {
            var node = new DialogNode
            {
                Id = dialog.NodeId,
                Text = dialog.NodeText
            };
            _dialogGraph.Nodes[dialog.NodeId] = node;

            if (string.IsNullOrEmpty(_dialogGraph.StartNodeId))
            {
                _dialogGraph.StartNodeId = dialog.NodeId;
            }

            RefreshNodeList();
        }
    }

    private void RemoveNode()
    {
        if (_selectedNode == null || _dialogGraph == null)
            return;

        if (MessageBox.Show($"Remove node '{_selectedNode.Id}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _dialogGraph.Nodes.Remove(_selectedNode.Id);
            if (_dialogGraph.StartNodeId == _selectedNode.Id)
            {
                _dialogGraph.StartNodeId = _dialogGraph.Nodes.Keys.FirstOrDefault() ?? "";
            }
            _selectedNode = null;
            RefreshNodeList();
            if (_nodeIdTextBox != null) _nodeIdTextBox.Text = "";
            if (_nodeTextTextBox != null) _nodeTextTextBox.Text = "";
            RefreshChoicesList();
        }
    }

    private void AddChoice()
    {
        if (_selectedNode == null)
            return;

        var dialog = new AddChoiceDialog(_dialogGraph);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var choice = new DialogChoice
            {
                Text = dialog.ChoiceText,
                NextNodeId = dialog.NextNodeId
            };
            _selectedNode.Choices.Add(choice);
            RefreshChoicesList();
        }
    }

    private void RemoveChoice()
    {
        if (_selectedNode == null || _choicesList == null || _choicesList.SelectedIndex < 0)
            return;

        var index = _choicesList.SelectedIndex;
        if (index >= 0 && index < _selectedNode.Choices.Count)
        {
            _selectedNode.Choices.RemoveAt(index);
            RefreshChoicesList();
        }
    }

    private void LoadSelectedChoice()
    {
        // Could open a choice editor dialog here
    }
}

/// <summary>
/// Dialog for creating a new dialog.
/// </summary>
public class NewDialogDialog : Form
{
    private TextBox? _idTextBox;
    private TextBox? _nameTextBox;

    public string DialogId => _idTextBox?.Text ?? "";
    public string DialogName => _nameTextBox?.Text ?? "";

    public NewDialogDialog()
    {
        Text = "New Dialog";
        Size = new Size(380, 180);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(10, 10, 10, 10);

        var idLabel = new Label { Text = "Dialog ID:", Location = new Point(20, 25), AutoSize = true };
        _idTextBox = new TextBox { Location = new Point(110, 22), Width = 230, Height = 23 };

        var nameLabel = new Label { Text = "Name:", Location = new Point(20, 60), AutoSize = true };
        _nameTextBox = new TextBox { Location = new Point(110, 57), Width = 230, Height = 23 };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(180, 110), Width = 75, Height = 30 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Width = 75, Height = 30 };

        Controls.Add(idLabel);
        Controls.Add(_idTextBox);
        Controls.Add(nameLabel);
        Controls.Add(_nameTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for adding a new node.
/// </summary>
public class AddNodeDialog : Form
{
    private TextBox? _idTextBox;
    private TextBox? _textTextBox;

    public string NodeId => _idTextBox?.Text ?? "";
    public string NodeText => _textTextBox?.Text ?? "";

    public AddNodeDialog()
    {
        Text = "Add Node";
        Size = new Size(380, 180);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(10, 10, 10, 10);

        var idLabel = new Label { Text = "Node ID:", Location = new Point(20, 25), AutoSize = true };
        _idTextBox = new TextBox { Location = new Point(110, 22), Width = 230, Height = 23 };

        var textLabel = new Label { Text = "Text:", Location = new Point(20, 60), AutoSize = true };
        _textTextBox = new TextBox { Location = new Point(110, 57), Width = 230, Height = 23 };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(180, 110), Width = 75, Height = 30 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Width = 75, Height = 30 };

        Controls.Add(idLabel);
        Controls.Add(_idTextBox);
        Controls.Add(textLabel);
        Controls.Add(_textTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for adding a choice.
/// </summary>
public class AddChoiceDialog : Form
{
    private TextBox? _textTextBox;
    private ComboBox? _nextNodeComboBox;

    public string ChoiceText => _textTextBox?.Text ?? "";
    public string? NextNodeId => _nextNodeComboBox?.SelectedItem?.ToString() == "END" ? null : _nextNodeComboBox?.SelectedItem?.ToString();

    public AddChoiceDialog(DialogGraph? dialogGraph)
    {
        Text = "Add Choice";
        Size = new Size(380, 180);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(10, 10, 10, 10);

        var textLabel = new Label { Text = "Choice Text:", Location = new Point(20, 25), AutoSize = true };
        _textTextBox = new TextBox { Location = new Point(110, 22), Width = 230, Height = 23 };

        var nextLabel = new Label { Text = "Next Node:", Location = new Point(20, 60), AutoSize = true };
        _nextNodeComboBox = new ComboBox { Location = new Point(110, 57), Width = 230, Height = 23, DropDownStyle = ComboBoxStyle.DropDownList };
        _nextNodeComboBox.Items.Add("END");
        if (dialogGraph != null)
        {
            foreach (var nodeId in dialogGraph.Nodes.Keys)
            {
                _nextNodeComboBox.Items.Add(nodeId);
            }
        }
        _nextNodeComboBox.SelectedIndex = 0;

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(180, 110), Width = 75, Height = 30 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Width = 75, Height = 30 };

        Controls.Add(textLabel);
        Controls.Add(_textTextBox);
        Controls.Add(nextLabel);
        Controls.Add(_nextNodeComboBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}



