using GameCore.Items;
using GameEditor.Services;
using GameEditor.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Linq;

namespace GameEditor.Forms;

/// <summary>
/// Form for editing items.
/// </summary>
public partial class ItemEditorForm : Form
{
    private Dictionary<string, Item>? _items;
    private ListBox? _itemList;
    private PropertyGrid? _propertyGrid;
    private PictureBox? _itemImagePreview;
    private Button? _loadImageButton;
    private string? _currentFilePath;
    private Item? _selectedItem;
    private ComboBox? _setDropdown;
    private string _currentSetName = "medieval";
    private List<string> _setPrompts = new List<string>();

    public ItemEditorForm()
    {
        InitializeComponent();
        
        // Validate GameContent path (ensures consistency with game)
        PathHelper.ValidateGameContentPath();
        
        // Load items immediately - works for both standalone and embedded forms
        LoadItems();
        
        // Load available sets after form is loaded
        Load += (s, e) => LoadAvailableSets();
    }

    private void InitializeComponent()
    {
        Text = "Item Editor";
        // No fixed size - will fill tab container
        MinimumSize = new Size(600, 400);

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top
        };

        var newButton = new ToolStripButton("New Item", null, (s, e) => NewItem());
        var openButton = new ToolStripButton("Open", null, (s, e) => OpenItems());
        var saveButton = new ToolStripButton("Save", null, (s, e) => SaveItems());
        var saveAsButton = new ToolStripButton("Save As", null, (s, e) => SaveItemsAs());
        toolStrip.Items.Add(newButton);
        toolStrip.Items.Add(openButton);
        toolStrip.Items.Add(saveButton);
        toolStrip.Items.Add(saveAsButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        var addItemButton = new ToolStripButton("Add Item", null, (s, e) => AddItem());
        var removeItemButton = new ToolStripButton("Remove Item", null, (s, e) => RemoveItem());
        toolStrip.Items.Add(addItemButton);
        toolStrip.Items.Add(removeItemButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        
        var loadSetButton = new ToolStripButton("Load Set", null, (s, e) => LoadSet());
        var saveSetButton = new ToolStripButton("Save Set", null, (s, e) => SaveSet());
        toolStrip.Items.Add(loadSetButton);
        toolStrip.Items.Add(saveSetButton);

        var mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        mainSplitContainer.Panel1MinSize = 200; // Minimum width for item list
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid crashes

        // Left panel: Item list
        var itemListPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 5, 5, 5) };
        
        // Set selection panel
        var setPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(5, 5, 5, 5) };
        var setLabel = new Label
        {
            Text = "Set:",
            Location = new Point(5, 8),
            AutoSize = true
        };
        _setDropdown = new ComboBox
        {
            Location = new Point(40, 5),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _setDropdown.SelectedIndexChanged += (s, e) => OnSetChanged();
        var addSetButton = new Button
        {
            Text = "Add Set",
            Location = new Point(200, 4),
            Width = 80,
            Height = 25
        };
        addSetButton.Click += (s, e) => AddNewSet();
        setPanel.Controls.Add(setLabel);
        setPanel.Controls.Add(_setDropdown);
        setPanel.Controls.Add(addSetButton);
        
        var itemListLabel = new Label
        {
            Text = "Items",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _itemList = new ListBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 5, 5, 5)
        };
        _itemList.SelectedIndexChanged += (s, e) => OnItemSelected();
        // Add controls in reverse order for Dock.Top (last added appears at top)
        itemListPanel.Controls.Add(_itemList);  // Fill - added first
        itemListPanel.Controls.Add(itemListLabel);  // Top - added second
        itemListPanel.Controls.Add(setPanel);  // Top - added last, appears at top

        // Right panel: Split into image preview and properties
        var rightSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        rightSplitContainer.Panel1MinSize = 150; // Minimum height for image preview
        // Note: Panel2MinSize and SplitterDistance set in Load event to avoid crashes

        // Image preview panel
        var imagePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 12) };
        var imageLabel = new Label
        {
            Text = "Item Image",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _itemImagePreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Margin = new Padding(5, 5, 5, 5)
        };
        _loadImageButton = new Button
        {
            Text = "Load Image...",
            Dock = DockStyle.Bottom,
            Height = 40,
            Margin = new Padding(5, 5, 5, 5)
        };
        _loadImageButton.Click += (s, e) => LoadItemImage();
        
        imagePanel.Controls.Add(_itemImagePreview);
        imagePanel.Controls.Add(_loadImageButton);
        imagePanel.Controls.Add(imageLabel);

        // Properties panel
        var propertyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 5, 5, 5) };
        var propertyLabel = new Label
        {
            Text = "Properties",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(5, 5, 5, 5)
        };
        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            ToolbarVisible = true,
            HelpVisible = true,
            Margin = new Padding(5, 5, 5, 5)
        };
        // Handle property changes to mark items as modified
        _propertyGrid.PropertyValueChanged += (s, e) =>
        {
            // Refresh the item list to show updated names
            RefreshItemList();
            // Update image preview if ImagePath changed
            if (e.ChangedItem?.PropertyDescriptor?.Name == "ImagePath")
            {
                UpdateImagePreview();
            }
            // Reselect the current item to update the display
            if (_itemList != null && _itemList.SelectedIndex >= 0)
            {
                var selectedText = _itemList.SelectedItem?.ToString();
                if (selectedText != null)
                {
                    var itemId = selectedText.Split(" - ")[0];
                    if (_items != null && _items.TryGetValue(itemId, out var item))
                    {
                        SelectItem(item);
                    }
                }
            }
        };
        propertyPanel.Controls.Add(propertyLabel);
        propertyPanel.Controls.Add(_propertyGrid);

        rightSplitContainer.Panel1.Controls.Add(imagePanel);
        rightSplitContainer.Panel2.Controls.Add(propertyPanel);

        mainSplitContainer.Panel1.Controls.Add(itemListPanel);
        mainSplitContainer.Panel2.Controls.Add(rightSplitContainer);

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
                var preferredDistance = 320;
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
            
            // Set right split container (image preview and properties)
            if (rightSplitContainer.Height > 0)
            {
                // Set Panel2MinSize dynamically
                var availableHeight = rightSplitContainer.Height;
                rightSplitContainer.Panel2MinSize = Math.Min(200, (int)(availableHeight * 0.3));
                
                // Set splitter distance
                var preferredRightDistance = 220;
                var maxRightDistance = rightSplitContainer.Height - rightSplitContainer.Panel2MinSize;
                var minRightDistance = rightSplitContainer.Panel1MinSize;
                
                if (maxRightDistance >= minRightDistance)
                {
                    rightSplitContainer.SplitterDistance = Math.Max(minRightDistance, Math.Min(preferredRightDistance, maxRightDistance));
                }
                else
                {
                    rightSplitContainer.SplitterDistance = minRightDistance;
                }
            }
        };
    }

    private void LoadItems()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadItems: Starting to load items");
            
            // Ensure _items is initialized
            if (_items == null)
            {
                _items = new Dictionary<string, Item>();
            }
            
            // Load the current set
            var setFilePath = GetSetFilePath(_currentSetName);
            System.Diagnostics.Debug.WriteLine($"LoadItems: Set file path = {setFilePath}, Exists = {setFilePath != null && File.Exists(setFilePath)}");
            
            if (setFilePath != null && File.Exists(setFilePath))
            {
                LoadItemsFromFile(setFilePath);
                _currentFilePath = setFilePath;
                LoadSetPrompts(_currentSetName);
                System.Diagnostics.Debug.WriteLine($"LoadItems: Loaded {_items.Count} items from set '{_currentSetName}'");
                Text = $"Item Editor - {_currentSetName}";
                return;
            }
            
            // If set file doesn't exist, try items.json for backward compatibility (medieval set)
            if (_currentSetName == "medieval")
            {
                var itemsPath = PathHelper.GetItemsPath();
                if (itemsPath != null && File.Exists(itemsPath))
                {
                    LoadItemsFromFile(itemsPath);
                    _currentFilePath = itemsPath;
                    LoadSetPrompts(_currentSetName);
                    System.Diagnostics.Debug.WriteLine($"LoadItems: Loaded {_items.Count} items from items.json (medieval set)");
                    Text = $"Item Editor - {_currentSetName}";
                    return;
                }
            }
            
            // No file exists, create empty items
            _items = new Dictionary<string, Item>();
            _currentFilePath = setFilePath;
            LoadSetPrompts(_currentSetName);
            RefreshItemList();
            Text = $"Item Editor - {_currentSetName} (New)";
            System.Diagnostics.Debug.WriteLine("LoadItems: File doesn't exist, created empty items");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadItems: Exception - {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Error loading items: {ex.Message}\n\nStack trace: {ex.StackTrace}", 
                "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // Initialize with empty items on error
            _items = new Dictionary<string, Item>();
            RefreshItemList();
            Text = $"Item Editor - {_currentSetName} (New)";
        }
    }

    private void NewItem()
    {
        _items = new Dictionary<string, Item>();
        _currentFilePath = null;
        RefreshItemList();
        Text = "Item Editor - New";
    }

    private void OpenItems()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        var initialDirectory = gameContentPath;
        
        if (initialDirectory == null || !Directory.Exists(initialDirectory))
        {
            initialDirectory = Application.StartupPath;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = initialDirectory,
            FileName = "items.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadItemsFromFile(dialog.FileName);
        }
    }

    private void LoadItemsFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            
            // Use ItemLoader for consistent deserialization with the game
            _items = ItemLoader.LoadItemsFromJson(json);
            
            if (_items == null)
            {
                _items = new Dictionary<string, Item>();
            }

            _currentFilePath = filePath;
            var fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrEmpty(fileName))
            {
                Text = $"Item Editor - {fileName}";
            }
            else
            {
                Text = "Item Editor";
            }
            // Force refresh of the UI
            RefreshItemList();
            
            // Select first item if available
            if (_itemList != null && _itemList.Items.Count > 0)
            {
                _itemList.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading items: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveItems()
    {
        // Save to current set file
        SaveSet();
    }

    /// <summary>
    /// Public method to save the items. Can be called from MainEditorForm's Save All.
    /// </summary>
    public void Save()
    {
        SaveItems();
    }

    private void SaveItemsAs()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        var initialDirectory = gameContentPath;
        
        if (initialDirectory == null || !Directory.Exists(initialDirectory))
        {
            initialDirectory = Application.StartupPath;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = initialDirectory,
            FileName = "items.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _currentFilePath = dialog.FileName;
            SaveToFile(_currentFilePath);
            Text = $"Item Editor - {Path.GetFileName(_currentFilePath)}";
        }
    }

    private void SaveToFile(string filePath)
    {
        if (_items == null)
            return;

        try
        {
            // Ensure file is ready for editing in Perforce
            PerforceService.EnsureFileReadyForEdit(filePath);
            
            var itemList = _items.Values.ToList();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(itemList, options);
            File.WriteAllText(filePath, json);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(filePath);
            
            MessageBox.Show("Items saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddItem()
    {
        if (_items == null)
        {
            _items = new Dictionary<string, Item>();
        }

        var dialog = new AddItemDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var item = new Item(
                dialog.ItemId,
                dialog.ItemName,
                dialog.Description,
                dialog.Stackable,
                dialog.MaxStack
            );

            _items[item.Id] = item;
            RefreshItemList();
            SelectItem(item);
        }
    }

    private void RemoveItem()
    {
        if (_itemList == null || _items == null || _itemList.SelectedIndex < 0)
            return;

        var selectedItem = _itemList.SelectedItem?.ToString();
        if (selectedItem != null && _items.ContainsKey(selectedItem))
        {
            if (MessageBox.Show($"Remove item '{selectedItem}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _items.Remove(selectedItem);
                RefreshItemList();
                if (_propertyGrid != null)
                {
                    _propertyGrid.SelectedObject = null;
                }
            }
        }
    }

    private void RefreshItemList()
    {
        if (_itemList == null)
        {
            System.Diagnostics.Debug.WriteLine("RefreshItemList: _itemList is null");
            return;
        }
        
        if (_items == null)
        {
            System.Diagnostics.Debug.WriteLine("RefreshItemList: _items is null");
            _items = new Dictionary<string, Item>();
        }

        try
        {
            _itemList.BeginUpdate();
            _itemList.Items.Clear();
            
            if (_items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("RefreshItemList: No items to display");
            }
            else
            {
                foreach (var item in _items.Values)
                {
                    if (item != null)
                    {
                        var itemId = item.Id ?? "unknown";
                        var itemName = item.Name ?? "Unnamed";
                        _itemList.Items.Add($"{itemId} - {itemName}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"RefreshItemList: Added {_itemList.Items.Count} items to list");
            }
            _itemList.EndUpdate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing item list: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Error refreshing item list: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnItemSelected()
    {
        if (_itemList == null || _items == null || _itemList.SelectedIndex < 0)
            return;

        var selectedText = _itemList.SelectedItem?.ToString();
        if (selectedText != null)
        {
            var itemId = selectedText.Split(" - ")[0];
            if (_items.TryGetValue(itemId, out var item))
            {
                SelectItem(item);
            }
        }
    }

    private void SelectItem(Item item)
    {
        if (item == null)
            return;

        _selectedItem = item;
        
        if (_propertyGrid != null)
        {
            _propertyGrid.SelectedObject = item;
            _propertyGrid.Refresh();
        }
        
        UpdateImagePreview();
    }

    private void UpdateImagePreview()
    {
        if (_itemImagePreview == null || _selectedItem == null)
        {
            if (_itemImagePreview != null)
            {
                _itemImagePreview.Image = null;
            }
            return;
        }

        try
        {
            string? imagePath = _selectedItem.ImagePath;
            if (string.IsNullOrEmpty(imagePath))
            {
                _itemImagePreview.Image = null;
                return;
            }

            // Resolve path - could be relative to GameContent or absolute
            var gameContentPath = PathHelper.GetGameContentPath();
            string fullPath;
            
            if (Path.IsPathRooted(imagePath))
            {
                fullPath = imagePath;
            }
            else if (gameContentPath != null)
            {
                fullPath = Path.Combine(gameContentPath, imagePath);
            }
            else
            {
                _itemImagePreview.Image = null;
                return;
            }

            if (File.Exists(fullPath))
            {
                // Dispose old image if any
                var oldImage = _itemImagePreview.Image;
                _itemImagePreview.Image = Image.FromFile(fullPath);
                oldImage?.Dispose();
            }
            else
            {
                _itemImagePreview.Image = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
            _itemImagePreview.Image = null;
        }
    }

    private void LoadItemImage()
    {
        if (_selectedItem == null)
        {
            MessageBox.Show("Please select an item first.", "No Item Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            Title = "Load Item Image"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                // Ensure GameContent/items directory exists
                var gameContentPath = PathHelper.GetGameContentPath();
                if (gameContentPath == null)
                {
                    MessageBox.Show("GameContent path not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var itemsDir = Path.Combine(gameContentPath, "items");
                if (!Directory.Exists(itemsDir))
                {
                    Directory.CreateDirectory(itemsDir);
                }

                // Copy image to items directory with item ID as filename
                var fileName = $"{_selectedItem.Id}.png";
                var destPath = Path.Combine(itemsDir, fileName);
                
                // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
                PerforceService.EnsureFileReadyForEdit(destPath);
                
                File.Copy(dialog.FileName, destPath, overwrite: true);
                
                // Add file to Perforce if it's new
                PerforceService.AddFile(destPath);

                // Set relative path in item
                _selectedItem.ImagePath = $"items/{fileName}";

                // Update preview
                UpdateImagePreview();
                
                // Refresh property grid
                if (_propertyGrid != null)
                {
                    _propertyGrid.Refresh();
                }

                MessageBox.Show($"Image loaded and saved to {destPath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AddImageToPerforce()
    {
        if (_selectedItem == null || string.IsNullOrEmpty(_selectedItem.ImagePath))
        {
            MessageBox.Show("No image is assigned to the selected item.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // Resolve the full path
            string fullPath = _selectedItem.ImagePath;
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
                MessageBox.Show($"Image file not found:\n{fullPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void LoadAvailableSets()
    {
        if (_setDropdown == null)
            return;

        try
        {
            _setDropdown.Items.Clear();
            
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
                return;

            var sets = new List<string>();
            
            // Check for items_*.json files
            var setFiles = Directory.GetFiles(gameContentPath, "items_*.json");
            foreach (var file in setFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("items_"))
                {
                    var setName = fileName.Substring(6); // Remove "items_" prefix
                    if (!string.IsNullOrEmpty(setName) && !sets.Contains(setName))
                    {
                        sets.Add(setName);
                    }
                }
            }
            
            // Check for items.json (treat as medieval set)
            var itemsPath = Path.Combine(gameContentPath, "items.json");
            if (File.Exists(itemsPath) && !sets.Contains("medieval"))
            {
                sets.Add("medieval");
            }
            
            // If no sets found, add medieval as default
            if (sets.Count == 0)
            {
                sets.Add("medieval");
            }
            
            sets.Sort();
            foreach (var setName in sets)
            {
                _setDropdown.Items.Add(setName);
            }
            
            // Select current set or default to medieval
            if (_setDropdown.Items.Count > 0)
            {
                var index = _setDropdown.Items.IndexOf(_currentSetName);
                if (index >= 0)
                {
                    _setDropdown.SelectedIndex = index;
                }
                else
                {
                    _setDropdown.SelectedIndex = 0;
                    _currentSetName = _setDropdown.SelectedItem?.ToString() ?? "medieval";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading available sets: {ex.Message}");
            MessageBox.Show($"Error loading available sets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSetChanged()
    {
        if (_setDropdown == null || _setDropdown.SelectedItem == null)
            return;

        var newSetName = _setDropdown.SelectedItem.ToString();
        if (newSetName == _currentSetName)
            return;

        // Save current items before switching
        if (_items != null && _items.Count > 0)
        {
            SaveSet();
        }

        // Store current items for merging
        var currentItems = _items != null ? new Dictionary<string, Item>(_items) : new Dictionary<string, Item>();

        // Load new set
        _currentSetName = newSetName;
        var setFilePath = GetSetFilePath(_currentSetName);
        
        if (setFilePath != null && File.Exists(setFilePath))
        {
            try
            {
                var json = File.ReadAllText(setFilePath);
                var newItems = ItemLoader.LoadItemsFromJson(json);
                
                if (newItems != null)
                {
                    // Merge: items from new set overwrite items with same ID
                    foreach (var item in newItems.Values)
                    {
                        currentItems[item.Id] = item;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading set for merge: {ex.Message}");
            }
        }
        
        // If medieval set, also check items.json
        if (_currentSetName == "medieval")
        {
            var itemsPath = PathHelper.GetItemsPath();
            if (itemsPath != null && File.Exists(itemsPath))
            {
                try
                {
                    var json = File.ReadAllText(itemsPath);
                    var medievalItems = ItemLoader.LoadItemsFromJson(json);
                    
                    if (medievalItems != null)
                    {
                        // Merge: items from items.json overwrite items with same ID
                        foreach (var item in medievalItems.Values)
                        {
                            currentItems[item.Id] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading items.json for merge: {ex.Message}");
                }
            }
        }

        // Update items and UI
        _items = currentItems;
        _currentFilePath = setFilePath;
        LoadSetPrompts(_currentSetName);
        RefreshItemList();
        Text = $"Item Editor - {_currentSetName}";
        
        // Select first item if available
        if (_itemList != null && _itemList.Items.Count > 0)
        {
            _itemList.SelectedIndex = 0;
        }
    }

    private void AddNewSet()
    {
        using var dialog = new InputDialog("Add New Set", "Enter set name:", "fantasy");
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var newSetName = dialog.InputText?.Trim().ToLower();
            if (string.IsNullOrEmpty(newSetName))
            {
                MessageBox.Show("Set name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Validate set name (no special characters)
            if (!System.Text.RegularExpressions.Regex.IsMatch(newSetName, @"^[a-z0-9_]+$"))
            {
                MessageBox.Show("Set name can only contain lowercase letters, numbers, and underscores.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if set already exists
            var setFilePath = GetSetFilePath(newSetName);
            if (setFilePath != null && File.Exists(setFilePath))
            {
                MessageBox.Show($"Set '{newSetName}' already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Add to dropdown
            if (_setDropdown != null)
            {
                _setDropdown.Items.Add(newSetName);
                _setDropdown.SelectedItem = newSetName;
            }

            // Switch to new set (will create empty items)
            _currentSetName = newSetName;
            _items = new Dictionary<string, Item>();
            _setPrompts = new List<string>();
            RefreshItemList();
            Text = $"Item Editor - {_currentSetName} (New)";
        }
    }

    private void LoadSet()
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        var initialDirectory = gameContentPath;
        
        if (initialDirectory == null || !Directory.Exists(initialDirectory))
        {
            initialDirectory = Application.StartupPath;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = initialDirectory,
            FileName = $"items_{_currentSetName}.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                if (fileName.StartsWith("items_"))
                {
                    var setName = fileName.Substring(6);
                    _currentSetName = setName;
                    
                    if (_setDropdown != null)
                    {
                        if (!_setDropdown.Items.Contains(setName))
                        {
                            _setDropdown.Items.Add(setName);
                        }
                        _setDropdown.SelectedItem = setName;
                    }
                }
                
                LoadItemsFromFile(dialog.FileName);
                LoadSetPrompts(_currentSetName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading set: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveSet()
    {
        if (_items == null)
            return;

        try
        {
            var setFilePath = GetSetFilePath(_currentSetName);
            if (setFilePath == null)
            {
                MessageBox.Show("Could not determine set file path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveToFile(setFilePath);
            
            // If medieval set, also save to items.json for backward compatibility
            if (_currentSetName == "medieval")
            {
                var itemsPath = PathHelper.GetItemsPath();
                if (itemsPath != null)
                {
                    SaveToFile(itemsPath);
                }
            }
            
            // Save prompts
            SaveSetPrompts(_currentSetName);
            
            Text = $"Item Editor - {_currentSetName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving set: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string? GetSetFilePath(string setName)
    {
        var gameContentPath = PathHelper.GetGameContentPath();
        if (gameContentPath == null)
            return null;

        return Path.Combine(gameContentPath, $"items_{setName}.json");
    }

    private void LoadSetPrompts(string setName)
    {
        try
        {
            _setPrompts.Clear();
            
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
                return;

            var promptsPath = Path.Combine(gameContentPath, $"items_{setName}_prompts.json");
            if (File.Exists(promptsPath))
            {
                var json = File.ReadAllText(promptsPath);
                var prompts = JsonSerializer.Deserialize<List<string>>(json);
                if (prompts != null)
                {
                    _setPrompts = prompts;
                }
            }
            else
            {
                // Initialize with default prompts for medieval set
                if (setName == "medieval")
                {
                    _setPrompts = new List<string>
                    {
                        "Medieval sword, pixel art, isometric game sprite, transparent background, fantasy RPG style",
                        "Medieval shield, pixel art, isometric game sprite, transparent background, fantasy RPG style",
                        "Health potion bottle, pixel art, isometric game sprite, transparent background, fantasy RPG style",
                        "Gold coin, pixel art, isometric game sprite, transparent background, fantasy RPG style",
                        "Medieval armor piece, pixel art, isometric game sprite, transparent background, fantasy RPG style"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading set prompts: {ex.Message}");
            _setPrompts = new List<string>();
        }
    }

    private void SaveSetPrompts(string setName)
    {
        try
        {
            var gameContentPath = PathHelper.GetGameContentPath();
            if (gameContentPath == null)
                return;

            var promptsPath = Path.Combine(gameContentPath, $"items_{setName}_prompts.json");
            
            // Ensure file is ready for editing in Perforce
            PerforceService.EnsureFileReadyForEdit(promptsPath);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_setPrompts, options);
            File.WriteAllText(promptsPath, json);
            
            // Add file to Perforce if it's new
            PerforceService.AddFile(promptsPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving set prompts: {ex.Message}");
        }
    }
}

/// <summary>
/// Simple input dialog for getting text input from user.
/// </summary>
public class InputDialog : Form
{
    private TextBox? _inputTextBox;
    public string? InputText => _inputTextBox?.Text;

    public InputDialog(string title, string labelText, string defaultValue = "")
    {
        Text = title;
        Size = new Size(400, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(10, 10, 10, 10);

        var label = new Label { Text = labelText, Location = new Point(20, 25), AutoSize = true };
        _inputTextBox = new TextBox { Location = new Point(20, 50), Width = 340, Height = 23, Text = defaultValue };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, 85), Width = 75, Height = 30 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(285, 85), Width = 75, Height = 30 };

        Controls.Add(label);
        Controls.Add(_inputTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}

/// <summary>
/// Dialog for adding a new item.
/// </summary>
public class AddItemDialog : Form
{
    private TextBox? _idTextBox;
    private TextBox? _nameTextBox;
    private TextBox? _descriptionTextBox;
    private CheckBox? _stackableCheckBox;
    private NumericUpDown? _maxStackNumeric;

    public string ItemId => _idTextBox?.Text ?? "";
    public string ItemName => _nameTextBox?.Text ?? "";
    public string Description => _descriptionTextBox?.Text ?? "";
    public bool Stackable => _stackableCheckBox?.Checked ?? true;
    public int MaxStack => (int)(_maxStackNumeric?.Value ?? 99);

    public AddItemDialog()
    {
        Text = "Add Item";
        Size = new Size(430, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(10, 10, 10, 10);

        var idLabel = new Label { Text = "ID:", Location = new Point(20, 25), AutoSize = true };
        _idTextBox = new TextBox { Location = new Point(110, 22), Width = 270, Height = 23 };

        var nameLabel = new Label { Text = "Name:", Location = new Point(20, 60), AutoSize = true };
        _nameTextBox = new TextBox { Location = new Point(110, 57), Width = 270, Height = 23 };

        var descLabel = new Label { Text = "Description:", Location = new Point(20, 95), AutoSize = true };
        _descriptionTextBox = new TextBox { Location = new Point(110, 92), Width = 270, Height = 65, Multiline = true };

        var stackableLabel = new Label { Text = "Stackable:", Location = new Point(20, 170), AutoSize = true };
        _stackableCheckBox = new CheckBox { Location = new Point(110, 168), Checked = true, Width = 20, Height = 20 };

        var maxStackLabel = new Label { Text = "Max Stack:", Location = new Point(20, 200), AutoSize = true };
        _maxStackNumeric = new NumericUpDown { Location = new Point(110, 197), Width = 120, Height = 23, Minimum = 1, Maximum = 9999, Value = 99 };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 240), Width = 75, Height = 30 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(305, 240), Width = 75, Height = 30 };

        Controls.Add(idLabel);
        Controls.Add(_idTextBox);
        Controls.Add(nameLabel);
        Controls.Add(_nameTextBox);
        Controls.Add(descLabel);
        Controls.Add(_descriptionTextBox);
        Controls.Add(stackableLabel);
        Controls.Add(_stackableCheckBox);
        Controls.Add(maxStackLabel);
        Controls.Add(_maxStackNumeric);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}



