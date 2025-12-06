using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing player properties and sprite.
/// </summary>
public class PlayerEditorForm : Form
{
    private PlayerData _playerData;
    private PictureBox? _spritePreview;
    private TextBox? _nameTextBox;
    private NumericUpDown? _maxHealthNumeric;
    private NumericUpDown? _attackPowerNumeric;
    private NumericUpDown? _defenseNumeric;
    private NumericUpDown? _movementSpeedNumeric;
    private NumericUpDown? _spriteScaleNumeric;
    private NumericUpDown? _startPosXNumeric;
    private NumericUpDown? _startPosYNumeric;
    private TextBox? _spritePathTextBox;
    private Label? _statusLabel;

    public PlayerEditorForm()
    {
        _playerData = PlayerSerializer.Load();
        InitializeComponent();
        LoadDataToUI();
    }

    private void InitializeComponent()
    {
        Text = "Player Editor";
        AutoScroll = true;
        BackColor = Color.FromArgb(45, 45, 48);
        FormBorderStyle = FormBorderStyle.None; // No border when embedded in tab

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(20)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 250)); // Sprite preview area
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Properties area

        // === Sprite Preview Section ===
        var spritePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(10)
        };

        var spriteLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        spriteLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        spriteLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        spriteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        spriteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        spriteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));

        _spritePreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(60, 60, 60),
            BorderStyle = BorderStyle.FixedSingle
        };
        spriteLayout.Controls.Add(_spritePreview, 0, 0);
        spriteLayout.SetRowSpan(_spritePreview, 3);

        var spritePathLabel = CreateLabel("Sprite Path:");
        spriteLayout.Controls.Add(spritePathLabel, 1, 0);

        _spritePathTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _spritePathTextBox.TextChanged += (s, e) => OnPropertyChanged();
        spriteLayout.Controls.Add(_spritePathTextBox, 1, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        var browseButton = new Button
        {
            Text = "Browse...",
            Width = 80,
            Height = 28,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        browseButton.Click += BrowseSprite_Click;
        buttonPanel.Controls.Add(browseButton);

        var refreshButton = new Button
        {
            Text = "Refresh Preview",
            Width = 110,
            Height = 28,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        refreshButton.Click += (s, e) => RefreshSpritePreview();
        buttonPanel.Controls.Add(refreshButton);
        
        var addToPerforceButton = new Button
        {
            Text = "Add to Perforce",
            Width = 120,
            Height = 28,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        addToPerforceButton.Click += (s, e) => AddSpriteToPerforce();
        buttonPanel.Controls.Add(addToPerforceButton);

        spriteLayout.Controls.Add(buttonPanel, 1, 2);

        spritePanel.Controls.Add(spriteLayout);
        mainPanel.Controls.Add(spritePanel, 0, 0);

        // === Properties Section ===
        var propertiesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(37, 37, 38),
            Padding = new Padding(10),
            AutoScroll = true
        };

        var propsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(5)
        };
        propsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        propsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Name
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Name:"), 0, row);
        _nameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _nameTextBox.TextChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_nameTextBox, 1, row++);

        // Max Health
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Max Health:"), 0, row);
        _maxHealthNumeric = CreateNumericUpDown(1, 9999, 100);
        _maxHealthNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_maxHealthNumeric, 1, row++);

        // Attack Power
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Attack Power:"), 0, row);
        _attackPowerNumeric = CreateNumericUpDown(0, 999, 15);
        _attackPowerNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_attackPowerNumeric, 1, row++);

        // Defense
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Defense:"), 0, row);
        _defenseNumeric = CreateNumericUpDown(0, 999, 5);
        _defenseNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_defenseNumeric, 1, row++);

        // Movement Speed
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Movement Speed:"), 0, row);
        _movementSpeedNumeric = CreateNumericUpDown(10, 500, 150, 1);
        _movementSpeedNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_movementSpeedNumeric, 1, row++);

        // Sprite Scale
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Sprite Scale:"), 0, row);
        _spriteScaleNumeric = CreateNumericUpDown(0.01m, 2.0m, 0.15m, 2);
        _spriteScaleNumeric.Increment = 0.01m;
        _spriteScaleNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_spriteScaleNumeric, 1, row++);

        // Start Position X
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Start Position X:"), 0, row);
        _startPosXNumeric = CreateNumericUpDown(0, 1000, 10, 1);
        _startPosXNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_startPosXNumeric, 1, row++);

        // Start Position Y
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        propsLayout.Controls.Add(CreateLabel("Start Position Y:"), 0, row);
        _startPosYNumeric = CreateNumericUpDown(0, 1000, 10, 1);
        _startPosYNumeric.ValueChanged += (s, e) => OnPropertyChanged();
        propsLayout.Controls.Add(_startPosYNumeric, 1, row++);

        // Save Button
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var saveButton = new Button
        {
            Text = "Save Player",
            Width = 120,
            Height = 35,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Left
        };
        saveButton.Click += SaveButton_Click;
        propsLayout.Controls.Add(saveButton, 1, row++);

        // Status Label
        propsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.LightGreen,
            TextAlign = ContentAlignment.MiddleLeft
        };
        propsLayout.Controls.Add(_statusLabel, 1, row++);

        propertiesPanel.Controls.Add(propsLayout);
        mainPanel.Controls.Add(propertiesPanel, 0, 1);

        Controls.Add(mainPanel);
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private NumericUpDown CreateNumericUpDown(decimal min, decimal max, decimal value, int decimalPlaces = 0)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            DecimalPlaces = decimalPlaces,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White
        };
    }

    private void LoadDataToUI()
    {
        if (_nameTextBox != null) _nameTextBox.Text = _playerData.Name;
        if (_maxHealthNumeric != null) _maxHealthNumeric.Value = _playerData.MaxHealth;
        if (_attackPowerNumeric != null) _attackPowerNumeric.Value = _playerData.AttackPower;
        if (_defenseNumeric != null) _defenseNumeric.Value = _playerData.Defense;
        if (_movementSpeedNumeric != null) _movementSpeedNumeric.Value = (decimal)_playerData.MovementSpeed;
        if (_spriteScaleNumeric != null) _spriteScaleNumeric.Value = (decimal)_playerData.SpriteScale;
        if (_startPosXNumeric != null) _startPosXNumeric.Value = (decimal)_playerData.StartPositionX;
        if (_startPosYNumeric != null) _startPosYNumeric.Value = (decimal)_playerData.StartPositionY;
        if (_spritePathTextBox != null) _spritePathTextBox.Text = _playerData.SpritePath;

        RefreshSpritePreview();
    }

    private void SaveUIToData()
    {
        if (_nameTextBox != null) _playerData.Name = _nameTextBox.Text;
        if (_maxHealthNumeric != null) _playerData.MaxHealth = (int)_maxHealthNumeric.Value;
        if (_attackPowerNumeric != null) _playerData.AttackPower = (int)_attackPowerNumeric.Value;
        if (_defenseNumeric != null) _playerData.Defense = (int)_defenseNumeric.Value;
        if (_movementSpeedNumeric != null) _playerData.MovementSpeed = (float)_movementSpeedNumeric.Value;
        if (_spriteScaleNumeric != null) _playerData.SpriteScale = (float)_spriteScaleNumeric.Value;
        if (_startPosXNumeric != null) _playerData.StartPositionX = (float)_startPosXNumeric.Value;
        if (_startPosYNumeric != null) _playerData.StartPositionY = (float)_startPosYNumeric.Value;
        if (_spritePathTextBox != null) _playerData.SpritePath = _spritePathTextBox.Text;
    }

    private void OnPropertyChanged()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = "* Modified (unsaved)";
            _statusLabel.ForeColor = Color.Orange;
        }
    }

    private void BrowseSprite_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Player Sprite",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
            InitialDirectory = PathHelper.GetGameContentPath() ?? ""
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath != null && dialog.FileName.StartsWith(gameContentPath, StringComparison.OrdinalIgnoreCase))
            {
                // Store relative path
                var relativePath = dialog.FileName.Substring(gameContentPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (_spritePathTextBox != null)
                {
                    _spritePathTextBox.Text = relativePath.Replace('\\', '/');
                }
            }
            else
            {
                // Copy file to sprites folder
                var spritesDir = Path.Combine(gameContentPath ?? "", "sprites");
                Directory.CreateDirectory(spritesDir);
                
                var destPath = Path.Combine(spritesDir, Path.GetFileName(dialog.FileName));
                
                // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
                PerforceService.EnsureFileReadyForEdit(destPath);
                
                File.Copy(dialog.FileName, destPath, true);
                
                // Add file to Perforce if it's new
                PerforceService.AddFile(destPath);
                
                if (_spritePathTextBox != null)
                {
                    _spritePathTextBox.Text = $"sprites/{Path.GetFileName(dialog.FileName)}";
                }
            }

            RefreshSpritePreview();
        }
    }

    private void RefreshSpritePreview()
    {
        if (_spritePreview == null || _spritePathTextBox == null)
            return;

        try
        {
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
                return;

            var spritePath = Path.Combine(gameContentPath, _spritePathTextBox.Text.Replace('/', Path.DirectorySeparatorChar));
            
            if (File.Exists(spritePath))
            {
                // Load image without locking the file
                using var stream = new FileStream(spritePath, FileMode.Open, FileAccess.Read);
                var image = Image.FromStream(stream);
                
                // Dispose old image
                _spritePreview.Image?.Dispose();
                _spritePreview.Image = new Bitmap(image);
                image.Dispose();
            }
            else
            {
                _spritePreview.Image?.Dispose();
                _spritePreview.Image = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading sprite preview: {ex.Message}");
            _spritePreview.Image?.Dispose();
            _spritePreview.Image = null;
        }
    }

    private void AddSpriteToPerforce()
    {
        if (_spritePathTextBox == null || string.IsNullOrEmpty(_spritePathTextBox.Text))
        {
            MessageBox.Show("No sprite path is assigned.", "No Sprite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // Resolve the full path
            string fullPath = _spritePathTextBox.Text;
            if (!Path.IsPathRooted(fullPath))
            {
                var gameContentPath = PathHelper.GetGameContentPath();
                if (gameContentPath != null)
                {
                    fullPath = Path.Combine(gameContentPath, fullPath);
                }
            }

            if (!File.Exists(fullPath))
            {
                MessageBox.Show($"Sprite file not found:\n{fullPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
            PerforceService.EnsureFileReadyForEdit(fullPath);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(fullPath);
            
            MessageBox.Show($"File added to Perforce:\n{fullPath}", "Perforce", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding file to Perforce: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        Save();
    }

    /// <summary>
    /// Saves the player data to file.
    /// </summary>
    public void Save()
    {
        try
        {
            SaveUIToData();
            PlayerSerializer.Save(_playerData);
            
            if (_statusLabel != null)
            {
                _statusLabel.Text = "Saved successfully!";
                _statusLabel.ForeColor = Color.LightGreen;
            }
        }
        catch (Exception ex)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
            }
            MessageBox.Show($"Error saving player data: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

