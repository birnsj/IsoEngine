using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using GameEditor.Data;
using GameEditor.Services;
using GameEditor.Utilities;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for editing all properties of an entity.
/// </summary>
public class EntityPropertiesDialog : Form
{
    private readonly EntityData _entity;
    private readonly EntityData _originalEntity;

    // Basic properties
    private ComboBox _typeComboBox = null!;
    private TextBox _nameTextBox = null!;
    private TextBox _descriptionTextBox = null!;

    // Position
    private NumericUpDown _posXNumeric = null!;
    private NumericUpDown _posYNumeric = null!;
    private NumericUpDown _posZNumeric = null!;

    // Sprite
    private TextBox _spritePathTextBox = null!;
    private Button _browseSpriteButton = null!;
    private PictureBox _spritePreviewBox = null!;

    // AI Generation
    private TextBox _aiPromptTextBox = null!;
    private ComboBox _aiPromptPresetsComboBox = null!;
    private Button _aiGenerateButton = null!;
    private FreeImageGenerationService? _aiService;

    // Bounding Box
    private NumericUpDown _boundsWidthNumeric = null!;
    private NumericUpDown _boundsHeightNumeric = null!;
    private NumericUpDown _boundsZHeightNumeric = null!;
    private NumericUpDown _boundsOffsetXNumeric = null!;
    private NumericUpDown _boundsOffsetYNumeric = null!;
    private BoundingBoxPreviewPanel _boundsPreviewPanel = null!;

    // Enemy Stats (shown only for Enemy/RangedEnemy types)
    private GroupBox _statsGroupBox = null!;
    private NumericUpDown _healthNumeric = null!;
    private NumericUpDown _attackNumeric = null!;
    private NumericUpDown _defenseNumeric = null!;

    // Other properties
    private TextBox _dialogIdTextBox = null!;
    private TextBox _itemIdTextBox = null!;
    private NumericUpDown _quantityNumeric = null!;

    public EntityData Entity => _entity;

    public EntityPropertiesDialog(EntityData entity)
    {
        // Create a copy to edit
        _originalEntity = entity;
        _entity = CloneEntity(entity);
        InitializeComponent();
        LoadEntityData();
    }

    private EntityData CloneEntity(EntityData source)
    {
        var clone = new EntityData
        {
            Type = source.Type,
            Name = source.Name,
            Description = source.Description,
            DialogId = source.DialogId,
            ItemId = source.ItemId,
            Quantity = source.Quantity,
            SpritePath = source.SpritePath,
            Position = new PositionData
            {
                X = source.Position?.X ?? 0,
                Y = source.Position?.Y ?? 0,
                Z = source.Position?.Z ?? 0
            }
        };

        if (source.EnemyStats != null)
        {
            clone.EnemyStats = new EnemyStatsData
            {
                MaxHealth = source.EnemyStats.MaxHealth,
                AttackPower = source.EnemyStats.AttackPower,
                Defense = source.EnemyStats.Defense
            };
        }

        if (source.PlacementBounds != null)
        {
            clone.PlacementBounds = new PlacementBoundsData
            {
                Width = source.PlacementBounds.Width,
                Height = source.PlacementBounds.Height,
                ZHeight = source.PlacementBounds.ZHeight,
                OffsetX = source.PlacementBounds.OffsetX,
                OffsetY = source.PlacementBounds.OffsetY
            };
        }

        return clone;
    }

    private void InitializeComponent()
    {
        Text = "Entity Properties";
        Size = new Size(620, 1000);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(580, 800);
        MaximizeBox = false;
        MinimizeBox = false;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10)
        };

        int yPos = 10;
        const int labelWidth = 110;
        const int rowHeight = 28;
        const int groupPadding = 12;
        const int groupBoxWidth = 560;

        // === Basic Properties Group ===
        var basicGroup = new GroupBox
        {
            Text = "Basic Properties",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 140),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        int basicY = 20;
        basicGroup.Controls.Add(new Label { Text = "Type:", Location = new Point(groupPadding, basicY + 3), AutoSize = true });
        _typeComboBox = new ComboBox
        {
            Location = new Point(labelWidth, basicY),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.AddRange(new[] { "NPC", "Enemy", "RangedEnemy", "GroundItem", "Interactable", "Chest" });
        _typeComboBox.SelectedIndexChanged += (s, e) => UpdateStatsVisibility();
        basicGroup.Controls.Add(_typeComboBox);

        basicY += rowHeight;
        basicGroup.Controls.Add(new Label { Text = "Name:", Location = new Point(groupPadding, basicY + 3), AutoSize = true });
        _nameTextBox = new TextBox { Location = new Point(labelWidth, basicY), Width = 400 };
        basicGroup.Controls.Add(_nameTextBox);

        basicY += rowHeight;
        var descLabel = new Label { Text = "Description:", Location = new Point(groupPadding, basicY + 3), AutoSize = true };
        basicGroup.Controls.Add(descLabel);
        _descriptionTextBox = new TextBox 
        { 
            Location = new Point(labelWidth, basicY), 
            Width = 400,
            Height = 50,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };
        basicGroup.Controls.Add(_descriptionTextBox);
        basicGroup.Height = basicY + 60;

        mainPanel.Controls.Add(basicGroup);
        yPos += basicGroup.Height + 10;

        // === Position Group ===
        var posGroup = new GroupBox
        {
            Text = "Position",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 65),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        posGroup.Controls.Add(new Label { Text = "X:", Location = new Point(groupPadding, 25), AutoSize = true });
        _posXNumeric = new NumericUpDown { Location = new Point(35, 22), Width = 120, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };
        posGroup.Controls.Add(_posXNumeric);

        posGroup.Controls.Add(new Label { Text = "Y:", Location = new Point(170, 25), AutoSize = true });
        _posYNumeric = new NumericUpDown { Location = new Point(195, 22), Width = 120, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };
        posGroup.Controls.Add(_posYNumeric);

        posGroup.Controls.Add(new Label { Text = "Z:", Location = new Point(330, 25), AutoSize = true });
        _posZNumeric = new NumericUpDown { Location = new Point(355, 22), Width = 120, DecimalPlaces = 2, Minimum = -100, Maximum = 100 };
        posGroup.Controls.Add(_posZNumeric);

        mainPanel.Controls.Add(posGroup);
        yPos += posGroup.Height + 10;

        // === Sprite Group ===
        var spriteGroup = new GroupBox
        {
            Text = "Sprite / Graphic",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 110),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        spriteGroup.Controls.Add(new Label { Text = "Path:", Location = new Point(groupPadding, 25), AutoSize = true });
        
        // Calculate button positions from right edge
        const int buttonSpacing = 5;
        const int addToPerforceWidth = 150;
        const int browseWidth = 80;
        const int totalButtonWidth = addToPerforceWidth + browseWidth + buttonSpacing;
        const int textboxRightEdge = groupBoxWidth - totalButtonWidth - groupPadding;
        
        _spritePathTextBox = new TextBox 
        { 
            Location = new Point(55, 22), 
            Width = textboxRightEdge - 55,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        spriteGroup.Controls.Add(_spritePathTextBox);

        var addToPerforceButton = new Button 
        { 
            Text = "Add to Perforce", 
            Location = new Point(groupBoxWidth - addToPerforceWidth - groupPadding, 21), 
            Width = addToPerforceWidth,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        addToPerforceButton.Click += (s, e) => AddSpriteToPerforce();
        spriteGroup.Controls.Add(addToPerforceButton);
        
        _browseSpriteButton = new Button 
        { 
            Text = "Browse...", 
            Location = new Point(groupBoxWidth - totalButtonWidth - groupPadding, 21), 
            Width = browseWidth,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _browseSpriteButton.Click += BrowseSpriteButton_Click;
        spriteGroup.Controls.Add(_browseSpriteButton);

        _spritePreviewBox = new PictureBox
        {
            Location = new Point(groupPadding, 52),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        spriteGroup.Controls.Add(_spritePreviewBox);

        // Add preview update on text change
        _spritePathTextBox.TextChanged += (s, e) => LoadSpritePreview(_spritePathTextBox.Text);

        mainPanel.Controls.Add(spriteGroup);
        yPos += spriteGroup.Height + 10;

        // === AI Generation Group ===
        var aiGroup = new GroupBox
        {
            Text = "🎨 AI Sprite Generation (Free)",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 145),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        int aiY = 20;
        aiGroup.Controls.Add(new Label { Text = "Preset:", Location = new Point(groupPadding, aiY + 3), AutoSize = true });
        _aiPromptPresetsComboBox = new ComboBox
        {
            Location = new Point(60, aiY),
            Width = groupBoxWidth - 70,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _aiPromptPresetsComboBox.SelectedIndexChanged += (s, e) => ApplyPromptPreset();
        aiGroup.Controls.Add(_aiPromptPresetsComboBox);

        aiY += rowHeight + 2;
        aiGroup.Controls.Add(new Label { Text = "Prompt:", Location = new Point(groupPadding, aiY + 3), AutoSize = true });
        _aiPromptTextBox = new TextBox
        {
            Location = new Point(60, aiY),
            Width = groupBoxWidth - 70,
            Height = 45,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        aiGroup.Controls.Add(_aiPromptTextBox);

        aiY += 50;
        _aiGenerateButton = new Button
        {
            Text = "🖼️ Generate Sprite with AI",
            Location = new Point(groupPadding, aiY),
            Width = 200,
            Height = 28
        };
        _aiGenerateButton.Click += async (s, e) => await GenerateAISprite();
        aiGroup.Controls.Add(_aiGenerateButton);

        var aiHelpLabel = new Label
        {
            Text = "Uses Pollinations.ai (free, no API key)",
            Location = new Point(220, aiY + 6),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        aiGroup.Controls.Add(aiHelpLabel);

        mainPanel.Controls.Add(aiGroup);
        yPos += aiGroup.Height + 10;

        // === Bounding Box Group ===
        var boundsGroup = new GroupBox
        {
            Text = "Bounding Box",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 210),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        int boundsY = 22;
        boundsGroup.Controls.Add(new Label { Text = "W (px):", Location = new Point(groupPadding, boundsY + 3), AutoSize = true });
        _boundsWidthNumeric = new NumericUpDown { Location = new Point(65, boundsY), Width = 80, DecimalPlaces = 0, Minimum = 1, Maximum = 512, Value = 32 };
        _boundsWidthNumeric.ValueChanged += (s, e) => _boundsPreviewPanel?.Invalidate();
        boundsGroup.Controls.Add(_boundsWidthNumeric);

        boundsGroup.Controls.Add(new Label { Text = "H (px):", Location = new Point(160, boundsY + 3), AutoSize = true });
        _boundsHeightNumeric = new NumericUpDown { Location = new Point(200, boundsY), Width = 80, DecimalPlaces = 0, Minimum = 1, Maximum = 512, Value = 64 };
        _boundsHeightNumeric.ValueChanged += (s, e) => _boundsPreviewPanel?.Invalidate();
        boundsGroup.Controls.Add(_boundsHeightNumeric);

        boundsGroup.Controls.Add(new Label { Text = "D (px):", Location = new Point(295, boundsY + 3), AutoSize = true });
        _boundsZHeightNumeric = new NumericUpDown { Location = new Point(335, boundsY), Width = 80, DecimalPlaces = 0, Minimum = 1, Maximum = 512, Value = 32 };
        _boundsZHeightNumeric.ValueChanged += (s, e) => _boundsPreviewPanel?.Invalidate();
        boundsGroup.Controls.Add(_boundsZHeightNumeric);

        boundsY += rowHeight + 5;
        boundsGroup.Controls.Add(new Label { Text = "Offset X (px):", Location = new Point(groupPadding, boundsY + 3), AutoSize = true });
        _boundsOffsetXNumeric = new NumericUpDown { Location = new Point(100, boundsY), Width = 80, DecimalPlaces = 0, Minimum = -256, Maximum = 256, Value = 0 };
        _boundsOffsetXNumeric.ValueChanged += (s, e) => _boundsPreviewPanel?.Invalidate();
        boundsGroup.Controls.Add(_boundsOffsetXNumeric);

        boundsGroup.Controls.Add(new Label { Text = "Offset Y (px):", Location = new Point(195, boundsY + 3), AutoSize = true });
        _boundsOffsetYNumeric = new NumericUpDown { Location = new Point(280, boundsY), Width = 80, DecimalPlaces = 0, Minimum = -256, Maximum = 256, Value = 0 };
        _boundsOffsetYNumeric.ValueChanged += (s, e) => _boundsPreviewPanel?.Invalidate();
        boundsGroup.Controls.Add(_boundsOffsetYNumeric);

        // Bounding Box Preview
        boundsY += rowHeight + 10;
        _boundsPreviewPanel = new BoundingBoxPreviewPanel(
            () => (float)_boundsWidthNumeric.Value,
            () => (float)_boundsHeightNumeric.Value,
            () => (float)_boundsZHeightNumeric.Value)
        {
            Location = new Point(groupPadding, boundsY),
            Size = new Size(groupBoxWidth - (groupPadding * 2), 105),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(30, 30, 35)
        };
        boundsGroup.Controls.Add(_boundsPreviewPanel);

        mainPanel.Controls.Add(boundsGroup);
        yPos += boundsGroup.Height + 10;

        // === Enemy Stats Group ===
        _statsGroupBox = new GroupBox
        {
            Text = "Enemy Stats",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 65),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _statsGroupBox.Controls.Add(new Label { Text = "Health:", Location = new Point(groupPadding, 25), AutoSize = true });
        _healthNumeric = new NumericUpDown { Location = new Point(75, 22), Width = 120, Minimum = 1, Maximum = 10000, Value = 50 };
        _statsGroupBox.Controls.Add(_healthNumeric);

        _statsGroupBox.Controls.Add(new Label { Text = "Attack:", Location = new Point(210, 25), AutoSize = true });
        _attackNumeric = new NumericUpDown { Location = new Point(265, 22), Width = 120, Minimum = 0, Maximum = 1000, Value = 10 };
        _statsGroupBox.Controls.Add(_attackNumeric);

        _statsGroupBox.Controls.Add(new Label { Text = "Defense:", Location = new Point(400, 25), AutoSize = true });
        _defenseNumeric = new NumericUpDown { Location = new Point(470, 22), Width = 120, Minimum = 0, Maximum = 500, Value = 2 };
        _statsGroupBox.Controls.Add(_defenseNumeric);

        mainPanel.Controls.Add(_statsGroupBox);
        yPos += _statsGroupBox.Height + 10;

        // === Other Properties Group ===
        var otherGroup = new GroupBox
        {
            Text = "Other Properties",
            Location = new Point(10, yPos),
            Size = new Size(groupBoxWidth, 105),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        int otherY = 22;
        otherGroup.Controls.Add(new Label { Text = "Dialog ID:", Location = new Point(groupPadding, otherY + 3), AutoSize = true });
        _dialogIdTextBox = new TextBox { Location = new Point(90, otherY), Width = 200 };
        otherGroup.Controls.Add(_dialogIdTextBox);

        otherY += rowHeight;
        otherGroup.Controls.Add(new Label { Text = "Item ID:", Location = new Point(groupPadding, otherY + 3), AutoSize = true });
        _itemIdTextBox = new TextBox { Location = new Point(90, otherY), Width = 200 };
        otherGroup.Controls.Add(_itemIdTextBox);

        otherGroup.Controls.Add(new Label { Text = "Quantity:", Location = new Point(310, otherY + 3), AutoSize = true });
        _quantityNumeric = new NumericUpDown { Location = new Point(380, otherY), Width = 100, Minimum = 1, Maximum = 999, Value = 1 };
        otherGroup.Controls.Add(_quantityNumeric);

        mainPanel.Controls.Add(otherGroup);
        yPos += otherGroup.Height + 10;

        // === Buttons ===
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50
        };

        var applyButton = new Button
        {
            Text = "Apply",
            Size = new Size(90, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        applyButton.Click += ApplyButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(90, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        okButton.Click += OkButton_Click;

        // Update button positions when form resizes
        void UpdateButtonPositions()
        {
            applyButton.Location = new Point(Width - applyButton.Width - 20, 10);
            cancelButton.Location = new Point(applyButton.Left - cancelButton.Width - 10, 10);
            okButton.Location = new Point(cancelButton.Left - okButton.Width - 10, 10);
        }
        UpdateButtonPositions();
        this.Resize += (s, e) => UpdateButtonPositions();

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(applyButton);

        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void LoadEntityData()
    {
        // Basic properties
        var typeIndex = _typeComboBox.Items.IndexOf(_entity.Type);
        _typeComboBox.SelectedIndex = typeIndex >= 0 ? typeIndex : 0;
        _nameTextBox.Text = _entity.Name ?? "";
        _descriptionTextBox.Text = _entity.Description ?? "";

        // Position
        _posXNumeric.Value = (decimal)(_entity.Position?.X ?? 0);
        _posYNumeric.Value = (decimal)(_entity.Position?.Y ?? 0);
        _posZNumeric.Value = (decimal)(_entity.Position?.Z ?? 0);

        // Sprite
        _spritePathTextBox.Text = _entity.SpritePath ?? "";
        LoadSpritePreview(_entity.SpritePath);

        // Initialize AI service and populate presets
        _aiService = new FreeImageGenerationService();
        PopulateAIPromptPresets();

        // Bounding Box
        if (_entity.PlacementBounds != null)
        {
            _boundsWidthNumeric.Value = (decimal)_entity.PlacementBounds.Width;
            _boundsHeightNumeric.Value = (decimal)_entity.PlacementBounds.Height;
            _boundsZHeightNumeric.Value = (decimal)_entity.PlacementBounds.ZHeight;
            _boundsOffsetXNumeric.Value = (decimal)_entity.PlacementBounds.OffsetX;
            _boundsOffsetYNumeric.Value = (decimal)_entity.PlacementBounds.OffsetY;
        }

        // Enemy Stats
        if (_entity.EnemyStats != null)
        {
            _healthNumeric.Value = _entity.EnemyStats.MaxHealth;
            _attackNumeric.Value = _entity.EnemyStats.AttackPower;
            _defenseNumeric.Value = _entity.EnemyStats.Defense;
        }

        // Other properties
        _dialogIdTextBox.Text = _entity.DialogId ?? "";
        _itemIdTextBox.Text = _entity.ItemId ?? "";
        _quantityNumeric.Value = _entity.Quantity ?? 1;

        UpdateStatsVisibility();
    }

    private void SaveEntityData()
    {
        // Basic properties
        _entity.Type = _typeComboBox.SelectedItem?.ToString() ?? "NPC";
        _entity.Name = _nameTextBox.Text;
        _entity.Description = _descriptionTextBox.Text;

        // Position
        if (_entity.Position == null)
            _entity.Position = new PositionData();
        _entity.Position.X = (float)_posXNumeric.Value;
        _entity.Position.Y = (float)_posYNumeric.Value;
        _entity.Position.Z = (float)_posZNumeric.Value;

        // Sprite
        _entity.SpritePath = string.IsNullOrWhiteSpace(_spritePathTextBox.Text) ? null : _spritePathTextBox.Text;

        // Bounding Box
        if (_entity.PlacementBounds == null)
            _entity.PlacementBounds = new PlacementBoundsData();
        _entity.PlacementBounds.Width = (float)_boundsWidthNumeric.Value;
        _entity.PlacementBounds.Height = (float)_boundsHeightNumeric.Value;
        _entity.PlacementBounds.ZHeight = (float)_boundsZHeightNumeric.Value;
        _entity.PlacementBounds.OffsetX = (float)_boundsOffsetXNumeric.Value;
        _entity.PlacementBounds.OffsetY = (float)_boundsOffsetYNumeric.Value;

        // Enemy Stats (only for Enemy/RangedEnemy types)
        var entityType = _typeComboBox.SelectedItem?.ToString() ?? "";
        if (entityType == "Enemy" || entityType == "RangedEnemy")
        {
            if (_entity.EnemyStats == null)
                _entity.EnemyStats = new EnemyStatsData();
            _entity.EnemyStats.MaxHealth = (int)_healthNumeric.Value;
            _entity.EnemyStats.AttackPower = (int)_attackNumeric.Value;
            _entity.EnemyStats.Defense = (int)_defenseNumeric.Value;
        }
        else
        {
            _entity.EnemyStats = null;
        }

        // Other properties
        _entity.DialogId = string.IsNullOrWhiteSpace(_dialogIdTextBox.Text) ? null : _dialogIdTextBox.Text;
        _entity.ItemId = string.IsNullOrWhiteSpace(_itemIdTextBox.Text) ? null : _itemIdTextBox.Text;
        _entity.Quantity = (int)_quantityNumeric.Value;
    }

    private void CopyToOriginal()
    {
        _originalEntity.Type = _entity.Type;
        _originalEntity.Name = _entity.Name;
        _originalEntity.Description = _entity.Description;
        _originalEntity.DialogId = _entity.DialogId;
        _originalEntity.ItemId = _entity.ItemId;
        _originalEntity.Quantity = _entity.Quantity;
        _originalEntity.SpritePath = _entity.SpritePath;

        if (_originalEntity.Position == null)
            _originalEntity.Position = new PositionData();
        _originalEntity.Position.X = _entity.Position.X;
        _originalEntity.Position.Y = _entity.Position.Y;
        _originalEntity.Position.Z = _entity.Position.Z;

        if (_entity.PlacementBounds != null)
        {
            if (_originalEntity.PlacementBounds == null)
                _originalEntity.PlacementBounds = new PlacementBoundsData();
            _originalEntity.PlacementBounds.Width = _entity.PlacementBounds.Width;
            _originalEntity.PlacementBounds.Height = _entity.PlacementBounds.Height;
            _originalEntity.PlacementBounds.ZHeight = _entity.PlacementBounds.ZHeight;
            _originalEntity.PlacementBounds.OffsetX = _entity.PlacementBounds.OffsetX;
            _originalEntity.PlacementBounds.OffsetY = _entity.PlacementBounds.OffsetY;
        }

        if (_entity.EnemyStats != null)
        {
            if (_originalEntity.EnemyStats == null)
                _originalEntity.EnemyStats = new EnemyStatsData();
            _originalEntity.EnemyStats.MaxHealth = _entity.EnemyStats.MaxHealth;
            _originalEntity.EnemyStats.AttackPower = _entity.EnemyStats.AttackPower;
            _originalEntity.EnemyStats.Defense = _entity.EnemyStats.Defense;
        }
        else
        {
            _originalEntity.EnemyStats = null;
        }
    }

    private void UpdateStatsVisibility()
    {
        var entityType = _typeComboBox.SelectedItem?.ToString() ?? "";
        _statsGroupBox.Enabled = entityType == "Enemy" || entityType == "RangedEnemy";
    }

    private void BrowseSpriteButton_Click(object? sender, EventArgs e)
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        var spritesDir = gameContentPath != null ? Path.Combine(gameContentPath, "sprites") : null;
        
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All Files|*.*",
            InitialDirectory = spritesDir ?? gameContentPath ?? Environment.CurrentDirectory
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            // Get path relative to GameContent
            if (gameContentPath != null && openFileDialog.FileName.StartsWith(gameContentPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = openFileDialog.FileName.Substring(gameContentPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _spritePathTextBox.Text = relativePath.Replace('\\', '/');
            }
            else
            {
                // Outside GameContent, use full path
                _spritePathTextBox.Text = openFileDialog.FileName;
            }
            LoadSpritePreview(_spritePathTextBox.Text);
        }
    }

    private void LoadSpritePreview(string? spritePath)
    {
        if (_spritePreviewBox == null) return;

        _spritePreviewBox.Image?.Dispose();
        _spritePreviewBox.Image = null;

        if (string.IsNullOrEmpty(spritePath)) return;

        var gameContentPath = PathHelper.GetGameContentPath();
        string? fullPath = null;
        
        // Check if it's a relative path (try to find in GameContent)
        if (gameContentPath != null && !Path.IsPathRooted(spritePath))
        {
            fullPath = Path.Combine(gameContentPath, spritePath);
        }
        else if (Path.IsPathRooted(spritePath))
        {
            fullPath = spritePath;
        }

        if (fullPath != null && File.Exists(fullPath))
        {
            try
            {
                // Use Image.FromFile with a copy to avoid file locking
                using var tempImage = Image.FromFile(fullPath);
                _spritePreviewBox.Image = new Bitmap(tempImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sprite preview: {ex.Message}");
            }
        }
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        SaveEntityData();
        CopyToOriginal();
    }

    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        SaveEntityData();
        CopyToOriginal();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _spritePreviewBox?.Image?.Dispose();
        base.OnFormClosing(e);
    }

    private void PopulateAIPromptPresets()
    {
        _aiPromptPresetsComboBox.Items.Clear();
        
        var entityType = _typeComboBox.SelectedItem?.ToString() ?? "NPC";
        var entityName = _nameTextBox.Text;
        var entityDesc = _descriptionTextBox.Text;

        // Add custom prompt option first
        _aiPromptPresetsComboBox.Items.Add("[Custom - Edit Below]");

        // Generate presets based on entity type and description
        var baseStyle = "pixel art, isometric game sprite, transparent background, fantasy RPG style";
        
        switch (entityType)
        {
            case "NPC":
                _aiPromptPresetsComboBox.Items.Add($"Friendly NPC villager, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Medieval merchant, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Village elder with staff, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Blacksmith with hammer, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Innkeeper, welcoming pose, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, {baseStyle}");
                break;
                
            case "Enemy":
            case "RangedEnemy":
                _aiPromptPresetsComboBox.Items.Add($"Goblin warrior with sword, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Skeleton archer, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Dark knight in armor, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Giant spider creature, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Orc berserker, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Undead zombie, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Fire mage casting spell, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, hostile, {baseStyle}");
                break;
                
            case "GroundItem":
                _aiPromptPresetsComboBox.Items.Add($"Treasure chest, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Gold coins pile, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Magic potion bottle, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Ancient scroll, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Glowing sword on ground, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, item, {baseStyle}");
                break;
                
            case "Interactable":
                _aiPromptPresetsComboBox.Items.Add($"Wooden door, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Lever switch mechanism, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Treasure chest, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Magic portal, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Ancient stone tablet, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, interactive object, {baseStyle}");
                break;
                
            case "Chest":
                _aiPromptPresetsComboBox.Items.Add($"Wooden treasure chest, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Golden ornate chest, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Rusty iron chest, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Magic glowing chest, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, chest, {baseStyle}");
                break;
                
            default:
                _aiPromptPresetsComboBox.Items.Add($"Fantasy character, {baseStyle}");
                _aiPromptPresetsComboBox.Items.Add($"Mysterious figure, {baseStyle}");
                if (!string.IsNullOrEmpty(entityDesc))
                    _aiPromptPresetsComboBox.Items.Add($"{entityDesc}, {baseStyle}");
                break;
        }

        // Add description-based preset if we have a name
        if (!string.IsNullOrEmpty(entityName) && entityName != "Unknown")
        {
            _aiPromptPresetsComboBox.Items.Add($"{entityName}, {baseStyle}");
        }

        _aiPromptPresetsComboBox.SelectedIndex = 0;
    }

    private void ApplyPromptPreset()
    {
        if (_aiPromptPresetsComboBox.SelectedIndex <= 0)
            return; // Custom option selected, don't change the text

        var selectedPreset = _aiPromptPresetsComboBox.SelectedItem?.ToString();
        if (!string.IsNullOrEmpty(selectedPreset))
        {
            _aiPromptTextBox.Text = selectedPreset;
        }
    }

    private async System.Threading.Tasks.Task GenerateAISprite()
    {
        if (_aiService == null)
        {
            MessageBox.Show("AI service not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var prompt = _aiPromptTextBox.Text;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            MessageBox.Show("Please enter a prompt or select a preset.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Determine sprite size (use 64x64 for entity sprites)
        int width = 64;
        int height = 64;

        // Create filename based on entity name or type
        var entityName = _nameTextBox.Text;
        var entityType = _typeComboBox.SelectedItem?.ToString() ?? "entity";
        var safeName = string.IsNullOrEmpty(entityName) ? entityType : entityName;
        safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{safeName}_{timestamp}.png";

        var gameContentPath = PathHelper.GetGameContentPath();
        var spritesDir = gameContentPath != null ? Path.Combine(gameContentPath, "sprites", "entities") : null;
        
        if (spritesDir == null)
        {
            MessageBox.Show("Could not find sprites directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Directory.CreateDirectory(spritesDir);
        var filePath = Path.Combine(spritesDir, filename);

        // Show progress
        _aiGenerateButton.Enabled = false;
        _aiGenerateButton.Text = "⏳ Generating...";

        try
        {
            var success = await _aiService.GenerateImageToFileAsync(prompt, filePath, width, height);

            if (success && File.Exists(filePath))
            {
                // Update sprite path
                var relativePath = $"sprites/entities/{filename}";
                _spritePathTextBox.Text = relativePath;
                LoadSpritePreview(relativePath);

                MessageBox.Show(
                    $"Sprite generated successfully!\n\nSaved to: {relativePath}",
                    "AI Generation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to generate image. Please try again.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating image: {ex.Message}", "AI Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _aiGenerateButton.Enabled = true;
            _aiGenerateButton.Text = "🖼️ Generate Sprite with AI";
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
}

/// <summary>
/// Custom panel that renders a 3D isometric bounding box preview.
/// </summary>
public class BoundingBoxPreviewPanel : Panel
{
    private readonly Func<float> _getWidth;
    private readonly Func<float> _getHeight;  // This is the vertical height (H)
    private readonly Func<float> _getDepth;   // This is the depth (D)

    public BoundingBoxPreviewPanel(Func<float> getWidth, Func<float> getHeight, Func<float> getDepth)
    {
        _getWidth = getWidth;
        _getHeight = getHeight;
        _getDepth = getDepth;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Get current dimensions (in pixels)
        // W = width (X axis - goes right-down in isometric)
        // H = height (Y axis - goes straight up)
        // D = depth (Z axis - goes left-down in isometric)
        float wPx = _getWidth();
        float hPx = _getHeight();
        float dPx = _getDepth();

        // Scale for preview (fit in panel)
        float maxDim = Math.Max(Math.Max(wPx, hPx), dPx);
        float scale = Math.Min(0.8f, 70f / maxDim);
        
        // Isometric projection angles (2:1 ratio)
        // X axis goes right-down at 26.57 degrees
        // Z axis goes left-down at 26.57 degrees
        // Y axis goes straight up
        float isoXx = 0.5f * scale;   // X component of width direction
        float isoXy = 0.25f * scale;  // Y component of width direction
        float isoZx = -0.5f * scale;  // X component of depth direction
        float isoZy = 0.25f * scale;  // Y component of depth direction

        // Center of the panel, shifted up to make room for the box
        float centerX = Width / 2f;
        float centerY = Height / 2f + (hPx * scale * 0.3f);

        // Calculate the 8 corners of the box
        // Bottom face (y = 0)
        var b0 = new PointF(centerX, centerY); // Origin (back corner)
        var b1 = new PointF(centerX + wPx * isoXx, centerY + wPx * isoXy); // +X (right)
        var b2 = new PointF(centerX + wPx * isoXx + dPx * isoZx, centerY + wPx * isoXy + dPx * isoZy); // +X +Z (front)
        var b3 = new PointF(centerX + dPx * isoZx, centerY + dPx * isoZy); // +Z (left)

        // Top face (y = height)
        float yOffset = -hPx * scale;
        var t0 = new PointF(b0.X, b0.Y + yOffset);
        var t1 = new PointF(b1.X, b1.Y + yOffset);
        var t2 = new PointF(b2.X, b2.Y + yOffset);
        var t3 = new PointF(b3.X, b3.Y + yOffset);

        // Draw faces from back to front for proper occlusion
        
        // Back face (b0, b1, t1, t0) - darkest
        var backFace = new PointF[] { b0, b1, t1, t0 };
        using (var brush = new SolidBrush(Color.FromArgb(60, 0, 140, 140)))
        {
            g.FillPolygon(brush, backFace);
        }
        
        // Left face (b0, b3, t3, t0) - medium dark
        var leftFace = new PointF[] { b0, b3, t3, t0 };
        using (var brush = new SolidBrush(Color.FromArgb(70, 0, 160, 160)))
        {
            g.FillPolygon(brush, leftFace);
        }

        // Bottom face (b0, b1, b2, b3) - dark
        var bottomFace = new PointF[] { b0, b1, b2, b3 };
        using (var brush = new SolidBrush(Color.FromArgb(40, 0, 100, 100)))
        {
            g.FillPolygon(brush, bottomFace);
        }
        
        // Right face (b1, b2, t2, t1) - medium bright
        var rightFace = new PointF[] { b1, b2, t2, t1 };
        using (var brush = new SolidBrush(Color.FromArgb(90, 0, 190, 190)))
        {
            g.FillPolygon(brush, rightFace);
        }
        
        // Front face (b3, b2, t2, t3) - bright
        var frontFace = new PointF[] { b3, b2, t2, t3 };
        using (var brush = new SolidBrush(Color.FromArgb(110, 0, 210, 210)))
        {
            g.FillPolygon(brush, frontFace);
        }
        
        // Top face (t0, t1, t2, t3) - brightest
        var topFace = new PointF[] { t0, t1, t2, t3 };
        using (var brush = new SolidBrush(Color.FromArgb(130, 0, 230, 230)))
        {
            g.FillPolygon(brush, topFace);
        }
        
        // Draw all edges for definition
        using (var pen = new Pen(Color.FromArgb(220, Color.Cyan), 1.5f))
        {
            // Bottom edges
            g.DrawLine(pen, b0, b1);
            g.DrawLine(pen, b1, b2);
            g.DrawLine(pen, b2, b3);
            g.DrawLine(pen, b3, b0);
            
            // Top edges
            g.DrawLine(pen, t0, t1);
            g.DrawLine(pen, t1, t2);
            g.DrawLine(pen, t2, t3);
            g.DrawLine(pen, t3, t0);
            
            // Vertical edges
            g.DrawLine(pen, b0, t0);
            g.DrawLine(pen, b1, t1);
            g.DrawLine(pen, b2, t2);
            g.DrawLine(pen, b3, t3);
        }

        // Draw dimensions text
        using (var font = new Font("Segoe UI", 8f))
        using (var brush = new SolidBrush(Color.White))
        {
            var dimText = $"W:{wPx:F0} × H:{hPx:F0} × D:{dPx:F0} px";
            var textSize = g.MeasureString(dimText, font);
            g.DrawString(dimText, font, brush, Width - textSize.Width - 5, 5);
        }
    }
}

