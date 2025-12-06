using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using GameEditor.Utilities;
using GameEditor.Services;

namespace GameEditor.Forms;

/// <summary>
/// Dialog for editing tile properties.
/// </summary>
public class TileEditDialog : Form
{
    private TextBox? _nameTextBox;
    private TextBox? _descriptionTextBox;
    private PictureBox? _tilePreview;
    private Button? _colorButton;
    private TextBox? _graphicPathTextBox;
    private Button? _browseGraphicButton;
    private Button? _clearGraphicButton;
    private TextBox? _aiPromptTextBox;
    private ComboBox? _promptCategoryComboBox;
    private ComboBox? _promptTemplateComboBox;
    private ComboBox? _imageSizeComboBox;
    private ComboBox? _providerComboBox;
    private Color _tileColor;
    private int _tileIndex;
    private int _tileWidth;
    private int _tileHeight;
    private string? _graphicPath;
    private OpenAIImageService? _aiService;
    private FreeImageGenerationService? _freeImageService;

    public string TileName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Color TileColor { get; private set; }
    public string? GraphicPath { get; private set; }
    public int TileIndex => _tileIndex;

    public TileEditDialog(int tileIndex, string? tileName = null, Color? tileColor = null, string? graphicPath = null, int tileWidth = 64, int tileHeight = 32)
    {
        _tileIndex = tileIndex;
        TileName = tileName ?? $"Tile {tileIndex}";
        Description = string.Empty;
        TileColor = tileColor ?? GetDefaultTileColor(tileIndex);
        _graphicPath = graphicPath;
        GraphicPath = graphicPath;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;

        InitializeComponent();
    }

    private Color GetDefaultTileColor(int index)
    {
        // Match the colors from TileImageGenerator
        return index switch
        {
            0 => Color.FromArgb(100, 150, 100), // Grass (light green)
            1 => Color.FromArgb(80, 120, 80),   // Dark grass
            2 => Color.FromArgb(150, 120, 100), // Dirt (brown)
            3 => Color.FromArgb(120, 100, 80), // Dark dirt
            4 => Color.FromArgb(100, 100, 150), // Water (blue)
            5 => Color.FromArgb(80, 80, 120),   // Deep water
            6 => Color.FromArgb(120, 120, 120), // Stone (gray)
            7 => Color.FromArgb(100, 100, 100), // Dark stone
            _ => Color.Gray
        };
    }

    private void InitializeComponent()
    {
        Text = $"Edit Tile {_tileIndex}";
        Size = new Size(1220, 880);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1100, 750);
        MaximizeBox = false;
        MinimizeBox = false;

        // Initialize AI services
        var apiKey = EditorSettings.GetOpenAIApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _aiService = new OpenAIImageService { ApiKey = apiKey };
        }
        
        // Initialize free service (defaults to Pollinations, no API key needed)
        _freeImageService = new FreeImageGenerationService();
        
        // Check if API keys are set and configure service
        var replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
        var deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
        if (!string.IsNullOrEmpty(replicateKey))
        {
            _freeImageService.ApiKey = replicateKey;
            // Default to Stable Diffusion if key is set (user can change in dropdown)
            _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.StableDiffusion;
        }
        else if (!string.IsNullOrEmpty(deepAIKey))
        {
            _freeImageService.ApiKey = deepAIKey;
            _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.DeepAI;
        }

        // Use a main panel with scrolling
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(15)
        };

        int yPos = 15;
        const int controlStartX = 150;
        const int formWidth = 1220;
        const int controlWidth = formWidth - controlStartX - 40;

        var nameLabel = new Label 
        { 
            Text = "Name:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _nameTextBox = new TextBox 
        { 
            Location = new Point(controlStartX, yPos), 
            Width = controlWidth,
            Height = 30,
            Font = new Font(DefaultFont.FontFamily, 10.5f),
            Text = TileName,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        yPos += 40;
        var descLabel = new Label 
        { 
            Text = "Description:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _descriptionTextBox = new TextBox 
        { 
            Location = new Point(controlStartX, yPos), 
            Width = controlWidth,
            Height = 80,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font(DefaultFont.FontFamily, 10.5f),
            Text = Description,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        yPos += 90;
        var colorLabel = new Label 
        { 
            Text = "Color:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _colorButton = new Button
        {
            Location = new Point(controlStartX, yPos),
            Width = 200,
            Height = 35,
            BackColor = TileColor,
            Text = "Change Color",
            Font = new Font(DefaultFont.FontFamily, 10f),
            UseVisualStyleBackColor = false
        };
        _colorButton.Click += (s, e) => ChangeColor();

        yPos += 50;
        var graphicLabel = new Label 
        { 
            Text = "Graphic:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _graphicPathTextBox = new TextBox
        {
            Location = new Point(controlStartX, yPos),
            Width = controlWidth - 400,
            Height = 28,
            ReadOnly = true,
            Font = new Font(DefaultFont.FontFamily, 10f),
            Text = _graphicPath ?? "",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _browseGraphicButton = new Button
        {
            Location = new Point(controlStartX + controlWidth - 390, yPos),
            Width = 90,
            Height = 28,
            Text = "Browse...",
            Font = new Font(DefaultFont.FontFamily, 10f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _browseGraphicButton.Click += (s, e) => BrowseGraphic();
        _clearGraphicButton = new Button
        {
            Location = new Point(controlStartX + controlWidth - 290, yPos),
            Width = 75,
            Height = 28,
            Text = "Clear",
            Font = new Font(DefaultFont.FontFamily, 10f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _clearGraphicButton.Click += (s, e) => ClearGraphic();
        
        var addToPerforceButton = new Button
        {
            Location = new Point(controlStartX + controlWidth - 200, yPos),
            Width = 170,
            Height = 28,
            Text = "Add to Perforce",
            Font = new Font(DefaultFont.FontFamily, 10f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        addToPerforceButton.Click += (s, e) => AddGraphicToPerforce();

        yPos += 45;
        // AI Generation section with better grouping
        var aiSectionLabel = new Label 
        { 
            Text = "AI Image Generation", 
            Location = new Point(15, yPos), 
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 12f, FontStyle.Bold)
        };
        
        yPos += 35;
        // Prompt category dropdown
        var promptCategoryLabel = new Label 
        { 
            Text = "Category:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _promptCategoryComboBox = new ComboBox
        {
            Location = new Point(controlStartX, yPos),
            Width = 250,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _promptCategoryComboBox.Items.Add("Medieval");
        _promptCategoryComboBox.Items.Add("SciFi");
        _promptCategoryComboBox.SelectedIndex = 0; // Default to Medieval
        
        // Prompt template dropdown
        var promptTemplateLabel = new Label 
        { 
            Text = "Prompt Template:", 
            Location = new Point(controlStartX + 270, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _promptTemplateComboBox = new ComboBox
        {
            Location = new Point(controlStartX + 420, yPos),
            Width = controlWidth - 420,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        // Populate prompts based on selected category
        _promptCategoryComboBox.SelectedIndexChanged += (s, e) => UpdatePromptTemplates();
        UpdatePromptTemplates();
        
        // Set default based on tile index
        var defaultPrompt = FreeImageGenerationService.GetTilePrompt(_tileIndex);
        int defaultIndex = 0;
        for (int i = 0; i < _promptTemplateComboBox.Items.Count; i++)
        {
            var item = _promptTemplateComboBox.Items[i]?.ToString();
            if (item != null && item.Contains("isometric", StringComparison.OrdinalIgnoreCase) && 
                defaultPrompt.Contains(item.Split(',')[0].Replace("Isometric ", "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                defaultIndex = i;
                break;
            }
        }
        if (defaultIndex < _promptTemplateComboBox.Items.Count)
        {
            _promptTemplateComboBox.SelectedIndex = defaultIndex;
        }
        
        _promptTemplateComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (_promptTemplateComboBox.SelectedIndex > 0 && _aiPromptTextBox != null)
            {
                var selectedPrompt = _promptTemplateComboBox.Items[_promptTemplateComboBox.SelectedIndex]?.ToString();
                if (selectedPrompt != null)
                {
                    _aiPromptTextBox.Text = selectedPrompt;
                }
            }
        };
        
        yPos += 35;
        var aiLabel = new Label 
        { 
            Text = "Prompt:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _aiPromptTextBox = new TextBox
        {
            Location = new Point(controlStartX, yPos),
            Width = controlWidth - 155,
            ReadOnly = false,
            Font = new Font(DefaultFont.FontFamily, 10f),
            Text = FreeImageGenerationService.GetTilePrompt(_tileIndex),
            BackColor = SystemColors.Window,
            Multiline = true,
            Height = 70,
            ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        // Update prompt when template is selected
        if (_promptTemplateComboBox.SelectedIndex > 0)
        {
            var selectedPrompt = _promptTemplateComboBox.Items[_promptTemplateComboBox.SelectedIndex]?.ToString();
            if (selectedPrompt != null)
            {
                _aiPromptTextBox.Text = selectedPrompt;
            }
        }
        
        var editPromptButton = new Button
        {
            Location = new Point(controlStartX + controlWidth - 145, yPos),
            Width = 130,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            Text = "Edit Prompt...",
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        editPromptButton.Click += (s, e) =>
        {
            var promptDialog = new PromptDialog(_aiPromptTextBox.Text);
            if (promptDialog.ShowDialog() == DialogResult.OK)
            {
                _aiPromptTextBox.Text = promptDialog.Prompt;
                _promptTemplateComboBox.SelectedIndex = 0; // Reset to "Custom Prompt"
            }
        };
        
        yPos += 80;
        // Provider selection (more prominent)
        var providerLabel = new Label 
        { 
            Text = "Provider:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _providerComboBox = new ComboBox
        {
            Location = new Point(controlStartX, yPos),
            Width = 450,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _providerComboBox.Items.Add("Pollinations.ai (Free - No API Key)");
        _providerComboBox.Items.Add("Stable Diffusion (Replicate API Key)");
        _providerComboBox.Items.Add("OpenJourney (Replicate API Key)");
        _providerComboBox.Items.Add("DeepAI (DeepAI API Key)");
        _providerComboBox.Items.Add("OpenAI DALL·E (OpenAI API Key)");
        
        // API Key configuration button
        var apiKeyButton = new Button
        {
            Location = new Point(controlStartX + 470, yPos),
            Width = 220,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            Text = "Configure API Keys...",
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        apiKeyButton.Click += (s, e) => ShowApiKeyConfigurationDialog();
        
        // Set default based on what's configured
        if (_freeImageService != null && _freeImageService.CurrentProvider == FreeImageGenerationService.Provider.StableDiffusion)
        {
            _providerComboBox.SelectedIndex = 1;
        }
        else if (_freeImageService != null && _freeImageService.CurrentProvider == FreeImageGenerationService.Provider.OpenJourney)
        {
            _providerComboBox.SelectedIndex = 2;
        }
        else if (_freeImageService != null && _freeImageService.CurrentProvider == FreeImageGenerationService.Provider.DeepAI)
        {
            _providerComboBox.SelectedIndex = 3;
        }
        else if (_aiService != null && _aiService.IsConfigured)
        {
            _providerComboBox.SelectedIndex = 4;
        }
        else
        {
            _providerComboBox.SelectedIndex = 0; // Default to Pollinations
        }
        
        _providerComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (_freeImageService == null) return;
            
            switch (_providerComboBox.SelectedIndex)
            {
                case 0: // Pollinations
                    _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.Pollinations;
                    _freeImageService.ApiKey = null;
                    break;
                case 1: // Stable Diffusion
                    _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.StableDiffusion;
                    var replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
                    if (string.IsNullOrEmpty(replicateKey))
                    {
                        var result = MessageBox.Show(
                            "Replicate API key is not configured. Would you like to configure it now?\n\nGet your free API key at: https://replicate.com/account/api-tokens",
                            "Replicate API Key Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        
                        if (result == DialogResult.Yes)
                        {
                            var keyDialog = new ReplicateApiKeyDialog();
                            if (keyDialog.ShowDialog() == DialogResult.OK)
                            {
                                replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
                            }
                        }
                    }
                    _freeImageService.ApiKey = replicateKey;
                    break;
                case 2: // OpenJourney
                    _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.OpenJourney;
                    var openjourneyKey = EditorSettings.GetSetting("ReplicateApiKey");
                    if (string.IsNullOrEmpty(openjourneyKey))
                    {
                        var result = MessageBox.Show(
                            "Replicate API key is not configured. Would you like to configure it now?\n\nGet your free API key at: https://replicate.com/account/api-tokens",
                            "Replicate API Key Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        
                        if (result == DialogResult.Yes)
                        {
                            var keyDialog = new ReplicateApiKeyDialog();
                            if (keyDialog.ShowDialog() == DialogResult.OK)
                            {
                                openjourneyKey = EditorSettings.GetSetting("ReplicateApiKey");
                            }
                        }
                    }
                    _freeImageService.ApiKey = openjourneyKey;
                    break;
                case 3: // DeepAI
                    _freeImageService.CurrentProvider = FreeImageGenerationService.Provider.DeepAI;
                    var deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
                    if (string.IsNullOrEmpty(deepAIKey))
                    {
                        var result = MessageBox.Show(
                            "DeepAI API key is not configured. Would you like to configure it now?\n\nGet your free API key at: https://deepai.org/profile/i-want-an-api-key",
                            "DeepAI API Key Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        
                        if (result == DialogResult.Yes)
                        {
                            var keyDialog = new DeepAIApiKeyDialog();
                            if (keyDialog.ShowDialog() == DialogResult.OK)
                            {
                                deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
                            }
                        }
                    }
                    _freeImageService.ApiKey = deepAIKey;
                    break;
            }
        };
        
        yPos += 35;
        var sizeLabel = new Label 
        { 
            Text = "Image Size:", 
            Location = new Point(15, yPos + 3), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _imageSizeComboBox = new ComboBox
        {
            Location = new Point(controlStartX, yPos),
            Width = 320,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 10f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        
        // Add common tile sizes
        _imageSizeComboBox.Items.Add($"{_tileWidth}x{_tileHeight} (Current Tile Size)");
        _imageSizeComboBox.Items.Add("64x32 (Standard)");
        _imageSizeComboBox.Items.Add("128x64 (High Res)");
        _imageSizeComboBox.Items.Add("256x128 (Extra High Res)");
        _imageSizeComboBox.Items.Add("512x256 (Ultra High Res)");
        _imageSizeComboBox.Items.Add("1024x512 (Maximum Res)");
        _imageSizeComboBox.SelectedIndex = 0; // Default to current tile size
        
        // Generate button (single button that works for all providers)
        var generateButton = new Button
        {
            Location = new Point(controlStartX + 340, yPos),
            Width = 260,
            Height = 28,
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold),
            Text = "Generate Image",
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        generateButton.Click += async (s, e) => await GenerateFreeImage();
        
        // Update button text based on provider selection
        _providerComboBox.SelectedIndexChanged += (s, e) =>
        {
            if (generateButton != null && _providerComboBox != null)
            {
                switch (_providerComboBox.SelectedIndex)
                {
                    case 0: // Pollinations
                        generateButton.Text = "Generate (Free)";
                        break;
                    case 1: // Stable Diffusion
                        generateButton.Text = "Generate (SD)";
                        break;
                    case 2: // OpenJourney
                        generateButton.Text = "Generate (OJ)";
                        break;
                    case 3: // DeepAI
                        generateButton.Text = "Generate (DeepAI)";
                        break;
                    case 4: // OpenAI
                        generateButton.Text = "Generate (DALL·E)";
                        break;
                }
            }
        };
        
        // Set initial button text
        if (_providerComboBox.SelectedIndex == 0)
            generateButton.Text = "Generate (Free)";
        else if (_providerComboBox.SelectedIndex == 1)
            generateButton.Text = "Generate (SD)";
        else if (_providerComboBox.SelectedIndex == 2)
            generateButton.Text = "Generate (OJ)";
        else if (_providerComboBox.SelectedIndex == 3)
            generateButton.Text = "Generate (DeepAI)";
        else if (_providerComboBox.SelectedIndex == 4)
            generateButton.Text = "Generate (DALL·E)";

        yPos += 35;
        var previewLabel = new Label 
        { 
            Text = "Preview:", 
            Location = new Point(15, yPos), 
            AutoSize = true, 
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold) 
        };
        _tilePreview = new PictureBox
        {
            Location = new Point(controlStartX, yPos),
            Size = new Size(320, 160),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _tilePreview.DoubleClick += (s, e) => ShowFullSizeGraphic();
        UpdatePreview();

        // Button panel at bottom
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55
        };

        var okButton = new Button 
        { 
            Text = "OK", 
            DialogResult = DialogResult.OK, 
            Size = new Size(120, 35),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold)
        };
        okButton.Click += (s, e) => ApplyChanges();

        var cancelButton = new Button 
        { 
            Text = "Cancel", 
            DialogResult = DialogResult.Cancel, 
            Size = new Size(120, 35),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Font = new Font(DefaultFont.FontFamily, 11f)
        };

        // Position buttons
        void UpdateButtonPositions()
        {
            cancelButton.Location = new Point(Width - cancelButton.Width - 20, 10);
            okButton.Location = new Point(cancelButton.Left - okButton.Width - 10, 10);
        }
        UpdateButtonPositions();
        this.Resize += (s, e) => UpdateButtonPositions();

        // Add all controls to main panel
        mainPanel.Controls.Add(nameLabel);
        mainPanel.Controls.Add(_nameTextBox);
        mainPanel.Controls.Add(descLabel);
        mainPanel.Controls.Add(_descriptionTextBox);
        mainPanel.Controls.Add(colorLabel);
        mainPanel.Controls.Add(_colorButton);
        mainPanel.Controls.Add(graphicLabel);
        mainPanel.Controls.Add(_graphicPathTextBox);
        mainPanel.Controls.Add(_browseGraphicButton);
        mainPanel.Controls.Add(_clearGraphicButton);
        mainPanel.Controls.Add(addToPerforceButton);
        mainPanel.Controls.Add(aiSectionLabel);
        mainPanel.Controls.Add(promptCategoryLabel);
        mainPanel.Controls.Add(_promptCategoryComboBox);
        mainPanel.Controls.Add(promptTemplateLabel);
        mainPanel.Controls.Add(_promptTemplateComboBox);
        mainPanel.Controls.Add(aiLabel);
        mainPanel.Controls.Add(_aiPromptTextBox);
        mainPanel.Controls.Add(editPromptButton);
        mainPanel.Controls.Add(providerLabel);
        mainPanel.Controls.Add(_providerComboBox);
        mainPanel.Controls.Add(apiKeyButton);
        mainPanel.Controls.Add(sizeLabel);
        mainPanel.Controls.Add(_imageSizeComboBox);
        mainPanel.Controls.Add(generateButton);
        mainPanel.Controls.Add(previewLabel);
        mainPanel.Controls.Add(_tilePreview);

        // Add buttons to button panel
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        // Add panels to form
        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);
    }

    private void ChangeColor()
    {
        using var colorDialog = new ColorDialog
        {
            Color = TileColor,
            FullOpen = true
        };

        if (colorDialog.ShowDialog() == DialogResult.OK)
        {
            TileColor = colorDialog.Color;
            _tileColor = colorDialog.Color;
            if (_colorButton != null)
            {
                _colorButton.BackColor = TileColor;
            }
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (_tilePreview == null)
            return;

        Bitmap? bitmap = null;

        // Try to load custom graphic if available
        if (!string.IsNullOrEmpty(_graphicPath))
        {
            try
            {
                string fullPath = _graphicPath;
                // If path is relative, try to resolve it relative to GameContent
                if (!Path.IsPathRooted(fullPath))
                {
                    var gameContentPath = PathHelper.GetGameContentPath();
                    if (gameContentPath != null)
                    {
                        fullPath = Path.Combine(gameContentPath, fullPath);
                    }
                }

                if (File.Exists(fullPath))
                {
                    var originalImage = Image.FromFile(fullPath);
                    bitmap = new Bitmap(_tilePreview.Width, _tilePreview.Height);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(originalImage, 0, 0, _tilePreview.Width, _tilePreview.Height);
                    }
                    originalImage.Dispose();
                }
            }
            catch
            {
                // Fall through to default preview if image loading fails
            }
        }

        // Fallback to colored polygon if no graphic or loading failed
        if (bitmap == null)
        {
            bitmap = new Bitmap(_tilePreview.Width, _tilePreview.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var centerX = _tilePreview.Width / 2.0f;
                var centerY = _tilePreview.Height / 2.0f;

                var points = new PointF[]
                {
                    new PointF(centerX, 0),
                    new PointF(_tilePreview.Width, centerY),
                    new PointF(centerX, _tilePreview.Height),
                    new PointF(0, centerY)
                };

                using (var brush = new SolidBrush(TileColor))
                {
                    g.FillPolygon(brush, points);
                }
            }
        }

        if (_tilePreview.Image != null)
        {
            _tilePreview.Image.Dispose();
        }
        _tilePreview.Image = bitmap;
    }

    private void UpdatePromptTemplates()
    {
        if (_promptTemplateComboBox == null || _promptCategoryComboBox == null)
            return;
        
        _promptTemplateComboBox.Items.Clear();
        _promptTemplateComboBox.Items.Add("Custom Prompt (Edit below)");
        
        var category = _promptCategoryComboBox.SelectedItem?.ToString() ?? "Medieval";
        
        if (category == "Medieval")
        {
            // Medieval prompts
            _promptTemplateComboBox.Items.Add("Isometric grass tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric dark grass tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric lush green grass tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric dirt tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, brown earth");
            _promptTemplateComboBox.Items.Add("Isometric dark dirt tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric mud tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, wet mud");
            _promptTemplateComboBox.Items.Add("Isometric sand tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, beige sand");
            _promptTemplateComboBox.Items.Add("Isometric desert sand tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric water tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, blue water");
            _promptTemplateComboBox.Items.Add("Isometric deep water tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, dark blue water");
            _promptTemplateComboBox.Items.Add("Isometric shallow water tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, light blue");
            _promptTemplateComboBox.Items.Add("Isometric stone tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, gray stone");
            _promptTemplateComboBox.Items.Add("Isometric dark stone tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, dark gray");
            _promptTemplateComboBox.Items.Add("Isometric cobblestone tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, gray cobblestone");
            _promptTemplateComboBox.Items.Add("Isometric brick tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, red brick");
            _promptTemplateComboBox.Items.Add("Isometric wooden floor tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, brown wood");
            _promptTemplateComboBox.Items.Add("Isometric snow tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, white snow");
            _promptTemplateComboBox.Items.Add("Isometric ice tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, blue ice");
            _promptTemplateComboBox.Items.Add("Isometric lava tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, red orange lava");
            _promptTemplateComboBox.Items.Add("Isometric gravel tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, gray gravel");
            _promptTemplateComboBox.Items.Add("Isometric moss tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, green moss");
            _promptTemplateComboBox.Items.Add("Isometric forest floor tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, brown green");
            _promptTemplateComboBox.Items.Add("Isometric path tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, dirt path");
            _promptTemplateComboBox.Items.Add("Isometric road tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, gray road");
            _promptTemplateComboBox.Items.Add("Isometric cracked earth tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, brown cracked");
            _promptTemplateComboBox.Items.Add("Isometric marble tile, seamless, 16 bit, jrpg style, pixel art, isometric view, game asset, clean background, diamond shape, white marble");
            _promptTemplateComboBox.Items.Add("Isometric gold tile, seamless, 16 bit, jrpg style, pixel art, top-down view, game asset, clean background, diamond shape, yellow gold");
        }
        else if (category == "SciFi")
        {
            // SciFi prompts
            _promptTemplateComboBox.Items.Add("Isometric metallic floor tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, silver metal");
            _promptTemplateComboBox.Items.Add("Isometric dark metal tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, dark gray metal");
            _promptTemplateComboBox.Items.Add("Isometric energy grass tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, glowing green");
            _promptTemplateComboBox.Items.Add("Isometric alien soil tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, purple alien earth");
            _promptTemplateComboBox.Items.Add("Isometric dark alien soil tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric toxic sludge tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, green toxic");
            _promptTemplateComboBox.Items.Add("Isometric crystal sand tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, blue crystal");
            _promptTemplateComboBox.Items.Add("Isometric alien desert tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape");
            _promptTemplateComboBox.Items.Add("Isometric energy water tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, blue energy");
            _promptTemplateComboBox.Items.Add("Isometric deep energy water tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, dark blue energy");
            _promptTemplateComboBox.Items.Add("Isometric shallow energy pool tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, light blue energy");
            _promptTemplateComboBox.Items.Add("Isometric carbon fiber tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, black carbon");
            _promptTemplateComboBox.Items.Add("Isometric dark carbon tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, dark carbon");
            _promptTemplateComboBox.Items.Add("Isometric hexagon panel tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, hexagonal pattern");
            _promptTemplateComboBox.Items.Add("Isometric plasma brick tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, red plasma");
            _promptTemplateComboBox.Items.Add("Isometric alloy floor tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, silver alloy");
            _promptTemplateComboBox.Items.Add("Isometric frozen crystal tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, white crystal");
            _promptTemplateComboBox.Items.Add("Isometric ice crystal tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, blue crystal ice");
            _promptTemplateComboBox.Items.Add("Isometric plasma lava tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, red orange plasma");
            _promptTemplateComboBox.Items.Add("Isometric debris tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, gray debris");
            _promptTemplateComboBox.Items.Add("Isometric bioluminescent moss tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, glowing green");
            _promptTemplateComboBox.Items.Add("Isometric alien forest floor tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, purple green");
            _promptTemplateComboBox.Items.Add("Isometric energy path tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, glowing path");
            _promptTemplateComboBox.Items.Add("Isometric hover road tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, gray hover road");
            _promptTemplateComboBox.Items.Add("Isometric cracked alien earth tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, purple cracked");
            _promptTemplateComboBox.Items.Add("Isometric quantum marble tile, seamless, sci-fi style, futuristic, pixel art, isometric view, game asset, clean background, diamond shape, white quantum");
            _promptTemplateComboBox.Items.Add("Isometric energy crystal tile, seamless, sci-fi style, futuristic, pixel art, top-down view, game asset, clean background, diamond shape, yellow energy");
        }
        
        _promptTemplateComboBox.SelectedIndex = 0; // Reset to "Custom Prompt"
    }

    private void BrowseGraphic()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        var initialDirectory = gameContentPath;

        // Try to get last selected directory from settings
        var lastGraphicDir = GetLastGraphicDirectory();
        if (!string.IsNullOrEmpty(lastGraphicDir) && Directory.Exists(lastGraphicDir))
        {
            initialDirectory = lastGraphicDir;
        }
        else if (gameContentPath != null)
        {
            initialDirectory = gameContentPath;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            InitialDirectory = initialDirectory,
            Title = "Select Tile Graphic"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            // Store the directory for next time
            var selectedDir = Path.GetDirectoryName(dialog.FileName);
            if (selectedDir != null)
            {
                SaveLastGraphicDirectory(selectedDir);
            }

            // Convert to relative path if within GameContent
            string relativePath = dialog.FileName;
            if (gameContentPath != null && dialog.FileName.StartsWith(gameContentPath, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = Path.GetRelativePath(gameContentPath, dialog.FileName);
            }

            _graphicPath = relativePath;
            if (_graphicPathTextBox != null)
            {
                _graphicPathTextBox.Text = relativePath;
            }
            UpdatePreview();
        }
    }

    private void ClearGraphic()
    {
        _graphicPath = null;
        if (_graphicPathTextBox != null)
        {
            _graphicPathTextBox.Text = "";
        }
        UpdatePreview();
    }

    private void AddGraphicToPerforce()
    {
        if (string.IsNullOrEmpty(_graphicPath))
        {
            MessageBox.Show("No graphic is assigned to this tile.", "No Graphic", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // Resolve the full path
            string fullPath = _graphicPath;
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
                MessageBox.Show($"Graphic file not found:\n{fullPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void ShowFullSizeGraphic()
    {
        if (string.IsNullOrEmpty(_graphicPath))
        {
            MessageBox.Show("No graphic is assigned to this tile.", "No Graphic", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // Resolve the full path
            string fullPath = _graphicPath;
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
                MessageBox.Show($"Graphic file not found:\n{fullPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Load and display the full-size image
            using var image = Image.FromFile(fullPath);
            var viewer = new ImageViewerDialog(image, Path.GetFileName(fullPath));
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetLastGraphicDirectory()
    {
        return EditorSettings.GetLastGraphicDirectory() ?? string.Empty;
    }

    private static void SaveLastGraphicDirectory(string directory)
    {
        EditorSettings.SetLastGraphicDirectory(directory);
    }

    private (int width, int height) GetSelectedImageSize()
    {
        if (_imageSizeComboBox == null || _imageSizeComboBox.SelectedIndex < 0)
        {
            return (_tileWidth, _tileHeight);
        }

        var selectedText = _imageSizeComboBox.SelectedItem?.ToString() ?? "";
        
        // Parse the size from the selected item
        if (selectedText.Contains("Current Tile Size"))
        {
            return (_tileWidth, _tileHeight);
        }
        else if (selectedText.Contains("64x32"))
        {
            return (64, 32);
        }
        else if (selectedText.Contains("128x64"))
        {
            return (128, 64);
        }
        else if (selectedText.Contains("256x128"))
        {
            return (256, 128);
        }
        else if (selectedText.Contains("512x256"))
        {
            return (512, 256);
        }
        else if (selectedText.Contains("1024x512"))
        {
            return (1024, 512);
        }

        return (_tileWidth, _tileHeight);
    }

    private async Task GenerateAIImage()
    {
        if (_aiService == null || !_aiService.IsConfigured)
        {
            ShowApiKeyDialog();
            return;
        }

        if (_aiPromptTextBox == null)
            return;

        var prompt = _aiPromptTextBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt for image generation.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Get selected size
        var (width, height) = GetSelectedImageSize();
        var sizeString = $"{width}x{height}";

        try
        {
            // Get GameContent path for saving
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
            {
                MessageBox.Show("GameContent directory not found. Cannot save generated image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create tiles directory if it doesn't exist
            var tilesDir = Path.Combine(gameContentPath, "tiles");
            if (!Directory.Exists(tilesDir))
            {
                Directory.CreateDirectory(tilesDir);
            }

            // Generate filename based on tile index and timestamp
            var fileName = $"tile_{_tileIndex}_dalle_{DateTime.Now:yyyyMMddHHmmss}.png";
            var filePath = Path.Combine(tilesDir, fileName);

            // Show progress
            var progressForm = new Form
            {
                Text = "Generating Image...",
                Size = new Size(350, 120),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var progressLabel = new Label
            {
                Text = $"Generating with OpenAI DALL·E...\nSize: {width}x{height}\nThis may take a minute...",
                Location = new Point(20, 20),
                AutoSize = true
            };
            progressForm.Controls.Add(progressLabel);
            progressForm.Show();
            progressForm.Refresh();

            // Generate image with selected size, resizing to exact tile dimensions
            var success = await _aiService.GenerateImageToFileAsync(prompt, filePath, sizeString, width, height);

            progressForm.Close();

            if (success)
            {
                // Set as the graphic path (relative to GameContent)
                _graphicPath = Path.Combine("tiles", fileName);
                GraphicPath = _graphicPath; // Update the public property immediately
                if (_graphicPathTextBox != null)
                {
                    _graphicPathTextBox.Text = _graphicPath;
                }
                UpdatePreview();
                
                // Ask user if they want to apply the new graphic immediately
                var result = MessageBox.Show(
                    $"Image generated successfully using OpenAI DALL·E!\n\nSize: {width}x{height}\nSaved to: {filePath}\n\nApply this graphic to Tile {_tileIndex} now?",
                    "AI Generation", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Information);
                
                if (result == DialogResult.Yes)
                {
                    // Apply changes and close dialog
                    ApplyChanges();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            else
            {
                MessageBox.Show("Failed to generate image. Please check your API key and try again.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating image: {ex.Message}", "AI Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task GenerateFreeImage()
    {
        if (_aiPromptTextBox == null || _providerComboBox == null)
            return;

        var prompt = _aiPromptTextBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt for image generation.", "AI Generation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Get selected size
        var (width, height) = GetSelectedImageSize();
        var sizeString = $"{width}x{height}";

        // Determine which service to use based on provider selection
        bool useOpenAI = _providerComboBox.SelectedIndex == 4 && _aiService != null && _aiService.IsConfigured;
        bool useStableDiffusion = _providerComboBox.SelectedIndex == 1;
        bool useOpenJourney = _providerComboBox.SelectedIndex == 2;
        bool useDeepAI = _providerComboBox.SelectedIndex == 3;
        bool usePollinations = _providerComboBox.SelectedIndex == 0;

        // Handle OpenAI
        if (useOpenAI)
        {
            await GenerateAIImage();
            return;
        }

        // Handle Stable Diffusion or Pollinations
        if (_freeImageService == null)
            return;

        // Check if provider needs API key
        if (useDeepAI && string.IsNullOrEmpty(_freeImageService.ApiKey))
        {
            var result = MessageBox.Show(
                "DeepAI API key is required. Would you like to configure it now?\n\nGet your free API key at: https://deepai.org/profile/i-want-an-api-key",
                "DeepAI API Key Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                var keyDialog = new DeepAIApiKeyDialog();
                if (keyDialog.ShowDialog() == DialogResult.OK)
                {
                    var deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
                    _freeImageService.ApiKey = deepAIKey;
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
        else if ((useStableDiffusion || useOpenJourney) && string.IsNullOrEmpty(_freeImageService.ApiKey))
        {
            var providerName = useStableDiffusion ? "Stable Diffusion" : "OpenJourney";
            var result = MessageBox.Show(
                $"Replicate API key is required for {providerName}. Would you like to configure it now?\n\nGet your free API key at: https://replicate.com/account/api-tokens",
                "Replicate API Key Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                var keyDialog = new ReplicateApiKeyDialog();
                if (keyDialog.ShowDialog() == DialogResult.OK)
                {
                    var replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
                    _freeImageService.ApiKey = replicateKey;
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

        try
        {
            // Get GameContent path for saving
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
            {
                MessageBox.Show("GameContent directory not found. Cannot save generated image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create tiles directory if it doesn't exist
            var tilesDir = Path.Combine(gameContentPath, "tiles");
            if (!Directory.Exists(tilesDir))
            {
                Directory.CreateDirectory(tilesDir);
            }

            // Generate filename based on tile index and timestamp
            string providerName;
            string providerText;
            if (useStableDiffusion)
            {
                providerName = "sd";
                providerText = "Stable Diffusion (Replicate)";
            }
            else if (useOpenJourney)
            {
                providerName = "oj";
                providerText = "OpenJourney (Replicate)";
            }
            else if (useDeepAI)
            {
                providerName = "deepai";
                providerText = "DeepAI";
            }
            else
            {
                providerName = "free";
                providerText = "Pollinations.ai (Free)";
            }
            
            var fileName = $"tile_{_tileIndex}_{providerName}_{DateTime.Now:yyyyMMddHHmmss}.png";
            var filePath = Path.Combine(tilesDir, fileName);

            // Show progress
            
            var progressForm = new Form
            {
                Text = "Generating Image...",
                Size = new Size(350, 120),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var progressLabel = new Label
            {
                Text = $"Generating with {providerText}...\nSize: {width}x{height}\nThis may take a minute...",
                Location = new Point(20, 20),
                AutoSize = true
            };
            progressForm.Controls.Add(progressLabel);
            progressForm.Show();
            progressForm.Refresh();

            // Generate image with selected size
            var success = await _freeImageService.GenerateImageToFileAsync(prompt, filePath, width, height);

            progressForm.Close();

            if (success)
            {
                // Set as the graphic path (relative to GameContent)
                _graphicPath = Path.Combine("tiles", fileName);
                GraphicPath = _graphicPath; // Update the public property immediately
                if (_graphicPathTextBox != null)
                {
                    _graphicPathTextBox.Text = _graphicPath;
                }
                UpdatePreview();
                
                // Ask user if they want to apply the new graphic immediately
                var result = MessageBox.Show(
                    $"Image generated successfully using {providerText}!\n\nSize: {width}x{height}\nSaved to: {filePath}\n\nApply this graphic to Tile {_tileIndex} now?",
                    "AI Generation", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Information);
                
                if (result == DialogResult.Yes)
                {
                    // Apply changes and close dialog
                    ApplyChanges();
                    DialogResult = DialogResult.OK;
                    Close();
                }
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
    }

    private void ShowApiKeyConfigurationDialog()
    {
        var dialog = new ApiKeyConfigurationDialog();
        dialog.ShowDialog();
        
        // Refresh services after configuration
        var apiKey = EditorSettings.GetOpenAIApiKey();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _aiService = new OpenAIImageService { ApiKey = apiKey };
        }
        
        var replicateKey = EditorSettings.GetSetting("ReplicateApiKey");
        var deepAIKey = EditorSettings.GetSetting("DeepAIApiKey");
        if (!string.IsNullOrEmpty(replicateKey) && _freeImageService != null)
        {
            _freeImageService.ApiKey = replicateKey;
            if (_freeImageService.CurrentProvider == FreeImageGenerationService.Provider.StableDiffusion && _providerComboBox != null)
            {
                _providerComboBox.SelectedIndex = 1;
            }
            else if (_freeImageService.CurrentProvider == FreeImageGenerationService.Provider.OpenJourney && _providerComboBox != null)
            {
                _providerComboBox.SelectedIndex = 2;
            }
        }
        else if (!string.IsNullOrEmpty(deepAIKey) && _freeImageService != null)
        {
            _freeImageService.ApiKey = deepAIKey;
            if (_freeImageService.CurrentProvider == FreeImageGenerationService.Provider.DeepAI && _providerComboBox != null)
            {
                _providerComboBox.SelectedIndex = 3;
            }
        }
    }

    private void ShowApiKeyDialog()
    {
        var dialog = new ApiKeySettingsDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var apiKey = EditorSettings.GetOpenAIApiKey();
            if (!string.IsNullOrEmpty(apiKey))
            {
                _aiService = new OpenAIImageService { ApiKey = apiKey };
            }
        }
    }

    private void ApplyChanges()
    {
        if (_nameTextBox != null)
        {
            TileName = _nameTextBox.Text;
        }
        if (_descriptionTextBox != null)
        {
            Description = _descriptionTextBox.Text;
        }
        GraphicPath = _graphicPath;
    }
}

/// <summary>
/// Dialog for configuring all API keys (OpenAI and Replicate).
/// </summary>
public class ApiKeyConfigurationDialog : Form
{
    private TextBox? _openAIKeyTextBox;
    private TextBox? _replicateKeyTextBox;
    private TextBox? _deepAIKeyTextBox;
    private TextBox? _prodiaKeyTextBox;

    public ApiKeyConfigurationDialog()
    {
        Text = "API Key Configuration";
        Size = new Size(550, 450);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var titleLabel = new Label
        {
            Text = "Configure API Keys for Image Generation",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font(DefaultFont.FontFamily, 11f, FontStyle.Bold)
        };

        // OpenAI section
        var openAILabel = new Label
        {
            Text = "OpenAI API Key (for DALL·E):",
            Location = new Point(20, 45),
            AutoSize = true
        };

        _openAIKeyTextBox = new TextBox
        {
            Location = new Point(20, 65),
            Width = 490,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetOpenAIApiKey() ?? ""
        };

        var openAILink = new Label
        {
            Text = "Get your API key from: https://platform.openai.com/api-keys",
            Location = new Point(20, 90),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        openAILink.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://platform.openai.com/api-keys",
            UseShellExecute = true
        });

        // Replicate section
        var replicateLabel = new Label
        {
            Text = "Replicate API Key (for Stable Diffusion/OpenJourney):",
            Location = new Point(20, 120),
            AutoSize = true
        };

        _replicateKeyTextBox = new TextBox
        {
            Location = new Point(20, 140),
            Width = 490,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetSetting("ReplicateApiKey") ?? ""
        };

        var replicateLink = new Label
        {
            Text = "Get your free API key from: https://replicate.com/account/api-tokens",
            Location = new Point(20, 165),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        replicateLink.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://replicate.com/account/api-tokens",
            UseShellExecute = true
        });

        // DeepAI section
        var deepAILabel = new Label
        {
            Text = "DeepAI API Key:",
            Location = new Point(20, 195),
            AutoSize = true
        };

        _deepAIKeyTextBox = new TextBox
        {
            Location = new Point(20, 215),
            Width = 490,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetSetting("DeepAIApiKey") ?? ""
        };

        var deepAILink = new Label
        {
            Text = "Get your free API key from: https://deepai.org/profile/i-want-an-api-key",
            Location = new Point(20, 240),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        deepAILink.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://deepai.org/profile/i-want-an-api-key",
            UseShellExecute = true
        });

        // Prodia section
        var prodiaLabel = new Label
        {
            Text = "Prodia API Key:",
            Location = new Point(20, 270),
            AutoSize = true
        };

        _prodiaKeyTextBox = new TextBox
        {
            Location = new Point(20, 290),
            Width = 490,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetSetting("ProdiaApiKey") ?? ""
        };

        var prodiaLink = new Label
        {
            Text = "Get your free API key from: https://prodia.com/api",
            Location = new Point(20, 315),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        prodiaLink.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://prodia.com/api",
            UseShellExecute = true
        });

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(360, 380),
            Width = 75
        };
        saveButton.Click += (s, e) =>
        {
            var openAIKey = _openAIKeyTextBox?.Text?.Trim() ?? "";
            var replicateKey = _replicateKeyTextBox?.Text?.Trim() ?? "";
            var deepAIKey = _deepAIKeyTextBox?.Text?.Trim() ?? "";
            var prodiaKey = _prodiaKeyTextBox?.Text?.Trim() ?? "";
            
            // Save all API keys to editor settings
            EditorSettings.SetOpenAIApiKey(openAIKey);
            EditorSettings.SetSetting("ReplicateApiKey", replicateKey);
            EditorSettings.SetSetting("DeepAIApiKey", deepAIKey);
            EditorSettings.SetSetting("ProdiaApiKey", prodiaKey);
            
            MessageBox.Show("All API keys saved successfully to editor settings!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Close dialog after saving
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(445, 380),
            Width = 75
        };

        Controls.Add(titleLabel);
        Controls.Add(openAILabel);
        Controls.Add(_openAIKeyTextBox);
        Controls.Add(openAILink);
        Controls.Add(replicateLabel);
        Controls.Add(_replicateKeyTextBox);
        Controls.Add(replicateLink);
        Controls.Add(deepAILabel);
        Controls.Add(_deepAIKeyTextBox);
        Controls.Add(deepAILink);
        Controls.Add(prodiaLabel);
        Controls.Add(_prodiaKeyTextBox);
        Controls.Add(prodiaLink);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for setting DeepAI API key.
/// </summary>
public class DeepAIApiKeyDialog : Form
{
    private TextBox? _apiKeyTextBox;

    public DeepAIApiKeyDialog()
    {
        Text = "DeepAI API Settings";
        Size = new Size(500, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "DeepAI API Key:",
            Location = new Point(20, 25),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(20, 45),
            Width = 440,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetSetting("DeepAIApiKey") ?? ""
        };

        var infoLabel = new Label
        {
            Text = "Get your free API key from: https://deepai.org/profile/i-want-an-api-key",
            Location = new Point(20, 75),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        infoLabel.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://deepai.org/profile/i-want-an-api-key",
            UseShellExecute = true
        });

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(320, 100),
            Width = 75
        };
        saveButton.Click += (s, e) =>
        {
            var apiKey = _apiKeyTextBox?.Text?.Trim() ?? "";
            EditorSettings.SetSetting("DeepAIApiKey", apiKey);
            MessageBox.Show("DeepAI API key saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Close dialog after saving
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(400, 100),
            Width = 75
        };

        Controls.Add(label);
        Controls.Add(_apiKeyTextBox);
        Controls.Add(infoLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for setting Replicate API key (for Stable Diffusion).
/// </summary>
public class ReplicateApiKeyDialog : Form
{
    private TextBox? _apiKeyTextBox;

    public ReplicateApiKeyDialog()
    {
        Text = "Replicate API Settings";
        Size = new Size(500, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "Replicate API Key:",
            Location = new Point(20, 25),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(20, 45),
            Width = 440,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetSetting("ReplicateApiKey") ?? ""
        };

        var infoLabel = new Label
        {
            Text = "Get your free API key from: https://replicate.com/account/api-tokens",
            Location = new Point(20, 75),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        infoLabel.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://replicate.com/account/api-tokens",
            UseShellExecute = true
        });

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(320, 100),
            Width = 75
        };
        saveButton.Click += (s, e) =>
        {
            var apiKey = _apiKeyTextBox?.Text?.Trim() ?? "";
            EditorSettings.SetSetting("ReplicateApiKey", apiKey);
            MessageBox.Show("Replicate API key saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Close dialog after saving
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(400, 100),
            Width = 75
        };

        Controls.Add(label);
        Controls.Add(_apiKeyTextBox);
        Controls.Add(infoLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for setting OpenAI API key.
/// </summary>
public class ApiKeySettingsDialog : Form
{
    private TextBox? _apiKeyTextBox;

    public ApiKeySettingsDialog()
    {
        Text = "OpenAI API Settings";
        Size = new Size(500, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "OpenAI API Key:",
            Location = new Point(20, 25),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(20, 45),
            Width = 440,
            UseSystemPasswordChar = true,
            Text = EditorSettings.GetOpenAIApiKey() ?? ""
        };

        var infoLabel = new Label
        {
            Text = "Get your API key from: https://platform.openai.com/api-keys",
            Location = new Point(20, 75),
            AutoSize = true,
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        infoLabel.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://platform.openai.com/api-keys",
            UseShellExecute = true
        });

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(320, 100),
            Width = 75
        };
        saveButton.Click += (s, e) =>
        {
            var apiKey = _apiKeyTextBox?.Text?.Trim() ?? "";
            EditorSettings.SetOpenAIApiKey(apiKey);
            MessageBox.Show("OpenAI API key saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Close dialog after saving
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(400, 100),
            Width = 75
        };

        Controls.Add(label);
        Controls.Add(_apiKeyTextBox);
        Controls.Add(infoLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }
}


