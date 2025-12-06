using GameCore.Entities;
using GameCore.Items;
using GameCore.Services;
using GameCore.Input;
using GameClient.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using GameClient.Utilities;
using System.IO;

namespace GameClient.UI;

/// <summary>
/// Myra-based enemy inventory window UI (smaller version of player inventory).
/// </summary>
public class EnemyInventoryWindow
{
    private Panel? _inventoryPanel;
    private Label? _titleLabel;
    private TextButton? _closeButton;
    private TextButton? _takeAllButton;
    private Widget? _inventoryGrid;
    private Enemy? _currentEnemy;
    private RangedEnemy? _currentRangedEnemy;
    private Chest? _currentChest;
    private readonly Player _player;
    private Desktop? _desktop;
    private bool _isVisible;
    private readonly List<Panel> _slotPanels = new List<Panel>(); // Store slot panels for click handling
    private readonly Dictionary<Panel, int> _slotPanelMap = new Dictionary<Panel, int>(); // Map panels to slot indices
    
    // Item drag and drop state
    private bool _isDraggingItem;
    private int _draggedSlotIndex = -1;
    private Item? _draggedItem;
    private Texture2D? _draggedItemTexture;
    private GraphicsDevice? _graphicsDevice;
    private SpriteBatch? _spriteBatch;
    private InventoryWindow? _playerInventoryWindow; // Reference to player inventory for cross-window drag

    public bool IsVisible
    {
        get => _inventoryPanel?.Visible ?? _isVisible;
        set
        {
            _isVisible = value;
            if (_inventoryPanel != null)
            {
                _inventoryPanel.Visible = value;
            }
            // Clear current enemy/chest when closing
            if (!value)
            {
                _currentEnemy = null;
                _currentChest = null;
            }
        }
    }

    public EnemyInventoryWindow(Player player)
    {
        _player = player;
    }

    /// <summary>
    /// Sets the graphics device and sprite batch for rendering dragged item icons.
    /// </summary>
    public void SetGraphics(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
    }

    /// <summary>
    /// Sets the reference to the player inventory window for cross-window drag and drop.
    /// </summary>
    public void SetPlayerInventoryWindow(InventoryWindow playerInventoryWindow)
    {
        _playerInventoryWindow = playerInventoryWindow;
    }

    /// <summary>
    /// Gets the current inventory (enemy or chest).
    /// </summary>
    public Inventory? GetCurrentEnemyInventory()
    {
        return _currentEnemy?.EnemyInventory ?? _currentRangedEnemy?.EnemyInventory ?? _currentChest?.ChestInventory;
    }

    /// <summary>
    /// Opens the inventory window for a specific enemy.
    /// </summary>
    public void OpenForEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        // Check if enemy inventory is empty - don't open if empty
        if (IsInventoryEmpty(enemy.EnemyInventory))
            return;

        _currentEnemy = enemy;
        _currentRangedEnemy = null; // Clear ranged enemy reference
        _currentChest = null; // Clear chest reference
        if (_titleLabel != null) _titleLabel.Text = "Enemy Loot";
        IsVisible = true;
        UpdateDisplay();
        
        // Bring panel to front
        if (_inventoryPanel != null && _inventoryPanel.Parent is Panel parentPanel)
        {
            parentPanel.Widgets.Remove(_inventoryPanel);
            parentPanel.Widgets.Add(_inventoryPanel);
        }
    }

    /// <summary>
    /// Opens the inventory window for a specific ranged enemy.
    /// </summary>
    public void OpenForRangedEnemy(RangedEnemy enemy)
    {
        if (enemy == null)
            return;

        // Check if enemy inventory is empty - don't open if empty
        if (IsInventoryEmpty(enemy.EnemyInventory))
            return;

        _currentRangedEnemy = enemy;
        _currentEnemy = null; // Clear regular enemy reference
        _currentChest = null; // Clear chest reference
        if (_titleLabel != null) _titleLabel.Text = "Enemy Loot";
        IsVisible = true;
        UpdateDisplay();
        
        // Bring panel to front
        if (_inventoryPanel != null && _inventoryPanel.Parent is Panel parentPanel)
        {
            parentPanel.Widgets.Remove(_inventoryPanel);
            parentPanel.Widgets.Add(_inventoryPanel);
        }
    }

    /// <summary>
    /// Opens the inventory window for a specific chest.
    /// </summary>
    public void OpenForChest(Chest chest)
    {
        if (chest == null)
            return;

        // Check if chest inventory is empty - don't open if empty
        if (IsInventoryEmpty(chest.ChestInventory))
            return;

        _currentChest = chest;
        _currentEnemy = null; // Clear enemy reference
        if (_titleLabel != null) _titleLabel.Text = "Chest Contents";
        IsVisible = true;
        UpdateDisplay();
        
        // Bring panel to front
        if (_inventoryPanel != null && _inventoryPanel.Parent is Panel parentPanel)
        {
            parentPanel.Widgets.Remove(_inventoryPanel);
            parentPanel.Widgets.Add(_inventoryPanel);
        }
    }

    /// <summary>
    /// Checks if an enemy inventory is empty.
    /// </summary>
    private bool IsInventoryEmpty(Inventory inventory)
    {
        if (inventory == null)
            return true;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (!inventory[i].IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates the inventory window UI using a custom Panel.
    /// </summary>
    public void CreateUI(Desktop desktop)
    {
        _desktop = desktop;

        // Create custom panel that looks like a window (smaller than player inventory)
        _inventoryPanel = new Panel
        {
            Width = 220, // Width for 3 columns of 60px slots
            Height = 320, // Height to fit 3 rows + buttons + title
            Left = 100,
            Top = 100,
            Visible = false,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(40, 40, 40, 240)) // Dark semi-transparent background
        };

        // Create a vertical container for the entire inventory
        var inventoryContainer = new VerticalStackPanel
        {
            Width = 220,
            Height = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Create title bar with title and close button
        var titleBar = new HorizontalStackPanel
        {
            Width = 220,
            Height = 30,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(60, 60, 60, 255))
        };

        _titleLabel = new Label
        {
            Text = "Enemy Loot",
            TextColor = Color.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Myra.Graphics2D.Thickness(10, 0, 0, 0)
        };

        // Custom close button
        _closeButton = new TextButton
        {
            Text = "×",
            Width = 30,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _closeButton.Click += (s, e) =>
        {
            IsVisible = false;
        };

        titleBar.Widgets.Add(_titleLabel);
        titleBar.Widgets.Add(_closeButton);

        // Create inventory grid (3x3) - same slot size as player inventory
        var mainPanel = new VerticalStackPanel
        {
            Spacing = 4,
            Padding = new Myra.Graphics2D.Thickness(4),
            Width = 220, // 3 columns * 60px + spacing
            Height = 220 // Space for 3 rows of slots
        };

        const int columns = 3;
        const int rows = 3;

        // Create slot panels in a grid layout
        for (int row = 0; row < rows; row++)
        {
            var rowPanel = new HorizontalStackPanel
            {
                Spacing = 4
            };

            for (int col = 0; col < columns; col++)
            {
                var slotIndex = row * columns + col;
                
                // Create a panel for the slot - same size as player inventory
                var panel = new Panel
                {
                    Width = 60, // Same as player inventory
                    Height = 60 // Same as player inventory
                };

                var label = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = Color.White
                };
                
                // Create a clickable button overlay that fills the panel (no visible highlight)
                var slotButton = new TextButton
                {
                    Text = "",
                    Width = 60,
                    Height = 60,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new Myra.Graphics2D.Brushes.SolidBrush(Color.Transparent),
                    OverBackground = new Myra.Graphics2D.Brushes.SolidBrush(Color.Transparent),
                    PressedBackground = new Myra.Graphics2D.Brushes.SolidBrush(Color.Transparent),
                    Border = null,
                    BorderThickness = new Myra.Graphics2D.Thickness(0)
                };
                
                // Store slot index and handle click
                var capturedSlotIndex = slotIndex;
                slotButton.Click += (s, e) =>
                {
                    TakeItemFromSlot(capturedSlotIndex);
                };
                
                panel.Widgets.Add(label);
                panel.Widgets.Add(slotButton); // Button on top for clicks (transparent so label shows through)
                _slotPanels.Add(panel); // Store for later reference
                _slotPanelMap[panel] = slotIndex; // Store mapping for drag and drop
                rowPanel.Widgets.Add(panel);
            }

            mainPanel.Widgets.Add(rowPanel);
        }

        _inventoryGrid = mainPanel;

        // Create button panel with "Take All" and "Close" buttons
        var buttonPanel = new HorizontalStackPanel
        {
            Spacing = 8,
            Padding = new Myra.Graphics2D.Thickness(4),
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _takeAllButton = new TextButton
        {
            Text = "Take All",
            Width = 120,
            Height = 30
        };
        _takeAllButton.Click += (s, e) =>
        {
            TakeAllItems();
        };

        var closeButton2 = new TextButton
        {
            Text = "Close",
            Width = 120,
            Height = 30
        };
        closeButton2.Click += (s, e) =>
        {
            IsVisible = false;
        };

        buttonPanel.Widgets.Add(_takeAllButton);
        buttonPanel.Widgets.Add(closeButton2);

        // Add all components to container
        inventoryContainer.Widgets.Add(titleBar);
        inventoryContainer.Widgets.Add(mainPanel);
        inventoryContainer.Widgets.Add(buttonPanel);

        // Add container to inventory panel
        _inventoryPanel.Widgets.Add(inventoryContainer);
        
        // Add inventory panel to desktop root panel
        if (desktop.Root is Panel rootPanel)
        {
            rootPanel.Widgets.Add(_inventoryPanel);
        }
        else
        {
            // If root is not a panel, create one and add both
            var newRoot = new Panel
            {
                Width = desktop.Root?.Width ?? GameCore.Configuration.Configuration.WindowWidth,
                Height = desktop.Root?.Height ?? GameCore.Configuration.Configuration.WindowHeight
            };
            if (desktop.Root != null)
            {
                newRoot.Widgets.Add(desktop.Root);
            }
            newRoot.Widgets.Add(_inventoryPanel);
            desktop.Root = newRoot;
        }
    }

    /// <summary>
    /// Checks if the player is within range of the current enemy/chest and closes the window if not.
    /// </summary>
    /// <param name="closeRadius">The maximum distance the player can be from the enemy/chest before the window closes.</param>
    public void CheckDistanceAndClose(float closeRadius = 80.0f)
    {
        if (!IsVisible || _player == null)
            return;

        // Get the position of the current target (enemy or chest)
        Vector2? targetPosition = null;
        if (_currentEnemy != null)
            targetPosition = _currentEnemy.Position;
        else if (_currentRangedEnemy != null)
            targetPosition = _currentRangedEnemy.Position;
        else if (_currentChest != null)
            targetPosition = _currentChest.Position;
        
        if (targetPosition == null)
            return;

        // Calculate distance between player and target
        var distance = Vector2.Distance(_player.Position, targetPosition.Value);

        // Close window if player is too far away
        if (distance > closeRadius)
        {
            IsVisible = false;
        }
    }

    /// <summary>
    /// Updates the inventory display to reflect current enemy/chest inventory state.
    /// </summary>
    public void UpdateDisplay()
    {
        var inventory = GetCurrentEnemyInventory();
        if (_inventoryGrid == null || inventory == null)
            return;

        // Access panels through the stack panel structure
        if (_inventoryGrid is not VerticalStackPanel mainPanel)
            return;

        int slotIndex = 0;
        foreach (var rowWidget in mainPanel.Widgets)
        {
            if (rowWidget is not HorizontalStackPanel rowPanel)
                continue;

            foreach (var panelWidget in rowPanel.Widgets)
            {
                if (slotIndex >= inventory.SlotCount)
                    return;

                var panel = panelWidget as Panel;
                if (panel == null || panel.Widgets.Count == 0)
                    continue;

                var label = panel.Widgets[0] as Label;
                if (label == null)
                    continue;

                var stack = inventory[slotIndex];
                if (!stack.IsEmpty && stack.Item != null)
                {
                    var quantityText = stack.Quantity > 1 ? $" x{stack.Quantity}" : "";
                    label.Text = $"{stack.Item.Name}{quantityText}";
                    var xnaColor = new Color(100, 150, 100, 255);
                    panel.Background = new Myra.Graphics2D.Brushes.SolidBrush(xnaColor);
                }
                else
                {
                    label.Text = "";
                    var xnaColor = new Color(50, 50, 50, 255);
                    panel.Background = new Myra.Graphics2D.Brushes.SolidBrush(xnaColor);
                }

                slotIndex++;
            }
        }
    }

    /// <summary>
    /// Takes all items from the enemy/chest inventory and adds them to the player inventory.
    /// </summary>
    private void TakeAllItems()
    {
        var sourceInventory = GetCurrentEnemyInventory();
        if (sourceInventory == null || _player?.Inventory?.Inventory == null)
            return;

        var playerInventory = _player.Inventory.Inventory;
        var logger = GameCore.Services.ServiceLocator.Get<ILogger>();

        // Iterate through all slots and transfer items
        for (int i = 0; i < sourceInventory.SlotCount; i++)
        {
            var stack = sourceInventory[i];
            if (stack.IsEmpty || stack.Item == null)
                continue;

            var item = stack.Item;
            var quantity = stack.Quantity;

            // Transfer all items to player inventory
            var added = playerInventory.AddItem(item, quantity);
            if (added > 0)
            {
                logger?.Info($"Took {added}x {item.Name} from inventory");
                // Remove from source inventory
                sourceInventory.RemoveItem(item.Id, added);
            }
            else
            {
                logger?.Warning($"Player inventory full! Could not take {item.Name}");
            }
        }

        // Remove all items from source inventory (already removed above, but clear any remaining)
        for (int i = 0; i < sourceInventory.SlotCount; i++)
        {
            var stack = sourceInventory[i];
            if (!stack.IsEmpty && stack.Item != null)
            {
                sourceInventory.RemoveItem(stack.Item.Id, stack.Quantity);
            }
        }

        // Update display and close if empty
        UpdateDisplay();

        // Check if inventory is empty and close window
        if (IsInventoryEmpty(sourceInventory))
        {
            logger?.Info("Inventory is empty");
            // Close the window automatically when empty
            IsVisible = false;
        }
    }

    /// <summary>
    /// Takes a single item from a specific slot and transfers it to the player inventory.
    /// </summary>
    private void TakeItemFromSlot(int slotIndex)
    {
        var sourceInventory = GetCurrentEnemyInventory();
        if (sourceInventory == null || _player?.Inventory?.Inventory == null)
            return;

        if (slotIndex < 0 || slotIndex >= sourceInventory.SlotCount)
            return;

        var playerInventory = _player.Inventory.Inventory;
        var logger = GameCore.Services.ServiceLocator.Get<ILogger>();

        var stack = sourceInventory[slotIndex];
        if (stack.IsEmpty || stack.Item == null)
            return;

        var item = stack.Item;
        var quantity = stack.Quantity;

        // Transfer item to player inventory
        var added = playerInventory.AddItem(item, quantity);
        if (added > 0)
        {
            logger?.Info($"Took {added}x {item.Name} from inventory");
            // Remove from source inventory
            sourceInventory.RemoveItem(item.Id, added);
        }
        else
        {
            logger?.Warning($"Player inventory full! Could not take {item.Name}");
        }

        // Update display
        UpdateDisplay();

        // Check if inventory is empty
        if (IsInventoryEmpty(sourceInventory))
        {
            logger?.Info("Inventory is empty");
            // Close window when empty
            IsVisible = false;
        }
    }

    /// <summary>
    /// Updates the enemy/chest inventory window, handling item drag and drop.
    /// </summary>
    public void Update()
    {
        var inventory = GetCurrentEnemyInventory();
        if (!IsVisible || _inventoryPanel == null || inventory == null)
        {
            _isDraggingItem = false;
            _draggedSlotIndex = -1;
            return;
        }

        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService == null)
            return;

        var mousePos = inputService.MousePosition;
        var currentMousePos = new Point(mousePos.X, mousePos.Y);

        // Handle item drag and drop
        HandleItemDragAndDrop(inputService, currentMousePos);
    }

    /// <summary>
    /// Handles item drag and drop within the enemy/chest inventory.
    /// </summary>
    private void HandleItemDragAndDrop(IInputService inputService, Point mousePos)
    {
        var inventory = GetCurrentEnemyInventory();
        if (inventory == null)
            return;

        // Check if mouse is over a slot
        int? hoveredSlotIndex = GetSlotIndexAtPoint(mousePos);

        // Handle drag start
        if (inputService.IsLeftClickPressed && hoveredSlotIndex.HasValue)
        {
            var slotIndex = hoveredSlotIndex.Value;
            if (slotIndex >= 0 && slotIndex < inventory.SlotCount && !inventory[slotIndex].IsEmpty)
            {
                _isDraggingItem = true;
                _draggedSlotIndex = slotIndex;
                _draggedItem = inventory[slotIndex].Item;
                
                // Load item texture if available
                if (_draggedItem != null && _graphicsDevice != null)
                {
                    _draggedItemTexture = LoadItemTexture(_draggedItem);
                }
            }
        }

        // Handle drop
        if (_isDraggingItem && !inputService.IsLeftMouseButtonDown && _draggedSlotIndex >= 0)
        {
            // Check if dropping on player inventory window
            if (_playerInventoryWindow != null && _playerInventoryWindow.IsVisible && 
                _playerInventoryWindow.IsPointOverWindow(mousePos))
            {
                var playerSlotIndex = _playerInventoryWindow.GetSlotIndexAtPoint(mousePos);
                if (playerSlotIndex.HasValue)
                {
                    // Transfer item to player inventory
                    TransferItemToPlayerInventory(_draggedSlotIndex, playerSlotIndex.Value);
                }
            }
            else if (hoveredSlotIndex.HasValue)
            {
                var targetSlotIndex = hoveredSlotIndex.Value;
                if (targetSlotIndex >= 0 && targetSlotIndex < inventory.SlotCount && targetSlotIndex != _draggedSlotIndex)
                {
                    // Swap the items within enemy inventory
                    inventory.SwapSlots(_draggedSlotIndex, targetSlotIndex);
                    UpdateDisplay();
                }
            }

            // Reset drag state
            _isDraggingItem = false;
            _draggedSlotIndex = -1;
            _draggedItem = null;
            _draggedItemTexture = null;
        }
    }

    /// <summary>
    /// Draws the dragged item icon at the cursor position.
    /// </summary>
    public void DrawDragCursor(IInputService inputService)
    {
        if (!_isDraggingItem || _draggedItem == null || _spriteBatch == null || _graphicsDevice == null)
            return;

        var mousePos = inputService.MousePosition;
        var cursorPos = new Vector2(mousePos.X, mousePos.Y);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        if (_draggedItemTexture != null)
        {
            // Draw item texture at cursor position (full size, no shrinking)
            const int iconSize = 60; // Same size as inventory slot
            var destRect = new Rectangle((int)cursorPos.X - iconSize / 2, (int)cursorPos.Y - iconSize / 2, iconSize, iconSize);
            _spriteBatch.Draw(_draggedItemTexture, destRect, Color.White);
        }
        else
        {
            // Fallback: draw a colored rectangle with item name
            const int iconSize = 60; // Same size as inventory slot
            var destRect = new Rectangle((int)cursorPos.X - iconSize / 2, (int)cursorPos.Y - iconSize / 2, iconSize, iconSize);
            
            // Create a simple colored texture if we don't have one
            if (_placeholderTexture == null)
            {
                _placeholderTexture = new Texture2D(_graphicsDevice, 1, 1);
                _placeholderTexture.SetData(new[] { Color.White });
            }
            
            _spriteBatch.Draw(_placeholderTexture, destRect, new Color(100, 150, 100, 200));
        }

        _spriteBatch.End();
    }

    private Texture2D? _placeholderTexture;

    /// <summary>
    /// Loads an item texture from its ImagePath.
    /// </summary>
    private Texture2D? LoadItemTexture(Item item)
    {
        if (_graphicsDevice == null || string.IsNullOrEmpty(item.ImagePath))
            return null;

        try
        {
            string? fullPath = null;
            var normalizedPath = item.ImagePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // Try absolute path first
            if (Path.IsPathRooted(normalizedPath) && File.Exists(normalizedPath))
            {
                fullPath = normalizedPath;
            }
            else
            {
                // Try relative to GameContent directory
                var gameContentPath = GameContentPathHelper.GetGameContentPath();
                if (gameContentPath != null)
                {
                    var relativePath = normalizedPath;
                    if (relativePath.StartsWith("GameContent" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring("GameContent".Length + 1);
                    }
                    
                    var candidatePath = Path.Combine(gameContentPath, relativePath);
                    var normalizedCandidate = Path.GetFullPath(candidatePath);
                    
                    if (File.Exists(normalizedCandidate))
                    {
                        fullPath = normalizedCandidate;
                    }
                }

                // If still not found, try as-is relative to current directory
                if (fullPath == null && File.Exists(normalizedPath))
                {
                    fullPath = Path.GetFullPath(normalizedPath);
                }
            }

            if (fullPath == null || !File.Exists(fullPath))
                return null;

            // Load texture from file
            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                return Texture2D.FromStream(_graphicsDevice, fileStream);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the slot index at a given point, or null if not over a slot.
    /// </summary>
    public int? GetSlotIndexAtPoint(Point point)
    {
        if (_inventoryPanel == null || _inventoryGrid == null)
            return null;

        // Get the absolute position of the inventory panel
        var panelX = _inventoryPanel.Left;
        var panelY = _inventoryPanel.Top;

        // Check each slot panel
        foreach (var kvp in _slotPanelMap)
        {
            var panel = kvp.Key;
            var slotIndex = kvp.Value;

            // Calculate absolute position of the slot
            if (_inventoryGrid is VerticalStackPanel mainPanel)
            {
                int slotRow = slotIndex / 3; // 3 columns
                int slotCol = slotIndex % 3;

                // Approximate position (accounting for padding and spacing)
                const int slotWidth = 60; // Same as player inventory
                const int slotHeight = 60; // Same as player inventory
                const int spacing = 4;
                const int padding = 4;

                var slotX = panelX + padding + slotCol * (slotWidth + spacing);
                var slotY = panelY + 30 + padding + slotRow * (slotHeight + spacing); // 30 for title bar

                if (point.X >= slotX && point.X < slotX + slotWidth &&
                    point.Y >= slotY && point.Y < slotY + slotHeight)
                {
                    return slotIndex;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a point is over this inventory window.
    /// </summary>
    public bool IsPointOverWindow(Point point)
    {
        if (_inventoryPanel == null)
            return false;

        var panelX = _inventoryPanel.Left;
        var panelY = _inventoryPanel.Top;
        var panelWidth = _inventoryPanel.Width ?? 220;
        var panelHeight = _inventoryPanel.Height ?? 320;

        return point.X >= panelX && point.X < panelX + panelWidth &&
               point.Y >= panelY && point.Y < panelY + panelHeight;
    }

    /// <summary>
    /// Transfers an item from enemy/chest inventory to player inventory.
    /// </summary>
    private void TransferItemToPlayerInventory(int sourceSlotIndex, int playerSlotIndex)
    {
        var sourceInventory = GetCurrentEnemyInventory();
        if (sourceInventory == null || _player?.Inventory?.Inventory == null || _playerInventoryWindow == null)
            return;

        var playerInventory = _player.Inventory.Inventory;

        if (sourceSlotIndex < 0 || sourceSlotIndex >= sourceInventory.SlotCount)
            return;

        if (playerSlotIndex < 0 || playerSlotIndex >= playerInventory.SlotCount)
            return;

        var sourceStack = sourceInventory[sourceSlotIndex];
        if (sourceStack.IsEmpty || sourceStack.Item == null)
            return;

        var playerStack = playerInventory[playerSlotIndex];

        // If player slot is empty, move the entire stack
        if (playerStack.IsEmpty)
        {
            playerStack.Item = sourceStack.Item;
            playerStack.Quantity = sourceStack.Quantity;
            sourceStack.Clear();
        }
        // If same item and stackable, try to merge
        else if (playerStack.Item != null && playerStack.Item.Id == sourceStack.Item.Id && sourceStack.Item.Stackable)
        {
            var added = playerStack.TryAdd(sourceStack.Item, sourceStack.Quantity);
            sourceStack.Remove(added);
        }
        // Otherwise, swap
        else
        {
            var tempItem = sourceStack.Item;
            var tempQuantity = sourceStack.Quantity;

            sourceStack.Item = playerStack.Item;
            sourceStack.Quantity = playerStack.Quantity;

            playerStack.Item = tempItem;
            playerStack.Quantity = tempQuantity;
        }

        UpdateDisplay();
        _playerInventoryWindow.UpdateDisplay();

        // Check if source inventory is empty and close window
        if (IsInventoryEmpty(sourceInventory))
        {
            IsVisible = false;
        }
    }
}

