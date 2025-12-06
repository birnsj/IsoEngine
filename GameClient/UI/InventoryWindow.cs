using GameCore.Entities;
using GameCore.Items;
using GameCore.Input;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using GameClient.Utilities;
using System.IO;

namespace GameClient.UI;

/// <summary>
/// Myra-based inventory window UI.
/// </summary>
public class InventoryWindow
{
    private Panel? _inventoryPanel; // Custom panel instead of Myra Window
    private Label? _titleLabel;
    private TextButton? _closeButton;
    private Widget? _inventoryGrid; // The original inventory grid panel
    private HorizontalStackPanel? _titleBar; // Store reference to title bar for dragging
    private readonly Player _player;
    private Desktop? _desktop;
    private bool _isVisible;
    
    // Window dragging state
    private bool _isDragging;
    private Point _dragOffset;
    private Point _lastMousePosition;
    
    // Item drag and drop state
    private bool _isDraggingItem;
    private int _draggedSlotIndex = -1;
    private readonly Dictionary<Panel, int> _slotPanels = new Dictionary<Panel, int>(); // Map panels to slot indices
    private Item? _draggedItem;
    private Texture2D? _draggedItemTexture;
    private GraphicsDevice? _graphicsDevice;
    private SpriteBatch? _spriteBatch;
    private EnemyInventoryWindow? _enemyInventoryWindow; // Reference to enemy inventory for cross-window drag

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
        }
    }

    public InventoryWindow(Player player)
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
    /// Sets the reference to the enemy inventory window for cross-window drag and drop.
    /// </summary>
    public void SetEnemyInventoryWindow(EnemyInventoryWindow enemyInventoryWindow)
    {
        _enemyInventoryWindow = enemyInventoryWindow;
    }

    /// <summary>
    /// Creates the inventory window UI using a custom Panel instead of Myra Window.
    /// </summary>
    public void CreateUI(Desktop desktop)
    {
        _desktop = desktop;

        // Create custom panel that looks like a window
        _inventoryPanel = new Panel
        {
            Width = 400,
            Height = 300,
            Left = 100,
            Top = 100,
            Visible = false,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(40, 40, 40, 240)) // Dark semi-transparent background
        };

        // Create a vertical container for the entire inventory
        var inventoryContainer = new VerticalStackPanel
        {
            Width = 400,
            Height = 300,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Create title bar with title and close button
        _titleBar = new HorizontalStackPanel
        {
            Width = 400,
            Height = 30,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(60, 60, 60, 255))
        };
        var titleBar = _titleBar;

        _titleLabel = new Label
        {
            Text = "Inventory",
            TextColor = Color.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Myra.Graphics2D.Thickness(10, 0, 0, 0)
        };

        // Custom close button that we have full control over
        _closeButton = new TextButton
        {
            Text = "×",
            Width = 30,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _closeButton.Click += (s, e) =>
        {
            // Close button clicked - hide the inventory
            IsVisible = false;
        };

        titleBar.Widgets.Add(_titleLabel);
        titleBar.Widgets.Add(_closeButton);

        // Use VerticalStackPanel with HorizontalStackPanels for a more reliable grid layout
        var mainPanel = new VerticalStackPanel
        {
            Spacing = 4,
            Padding = new Myra.Graphics2D.Thickness(4),
            Width = 400,
            Height = 230 // Reduced to make room for button panel at bottom
        };

        // Create 6 columns x 4 rows = 24 slots
        const int columns = 6;
        const int rows = 4;

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
                var panel = new Panel
                {
                    Width = 60,
                    Height = 60
                };

                var label = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = Color.White
                };

                // Create a transparent button overlay for drag and drop (no visible highlight)
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

                var capturedSlotIndex = slotIndex;
                slotButton.Click += (s, e) =>
                {
                    // Handle item drag start on click
                    HandleSlotClick(capturedSlotIndex);
                };

                panel.Widgets.Add(label);
                panel.Widgets.Add(slotButton); // Button on top for clicks
                _slotPanels[panel] = slotIndex; // Store mapping
                rowPanel.Widgets.Add(panel);
            }

            mainPanel.Widgets.Add(rowPanel);
        }

        _inventoryGrid = mainPanel;

        // Create button panel with "Close" button at the bottom
        var buttonPanel = new HorizontalStackPanel
        {
            Spacing = 8,
            Padding = new Myra.Graphics2D.Thickness(4),
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var closeButtonBottom = new TextButton
        {
            Text = "Close",
            Width = 120,
            Height = 30
        };
        closeButtonBottom.Click += (s, e) =>
        {
            IsVisible = false;
        };

        buttonPanel.Widgets.Add(closeButtonBottom);

        // Add title bar, content, and button panel to container
        inventoryContainer.Widgets.Add(titleBar);
        inventoryContainer.Widgets.Add(mainPanel);
        inventoryContainer.Widgets.Add(buttonPanel);

        // Add container to inventory panel
        _inventoryPanel.Widgets.Add(inventoryContainer);
        
        // Add inventory panel to desktop root panel so it can be toggled properly
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
    /// Updates the inventory display to reflect current inventory state.
    /// </summary>
    public void UpdateDisplay()
    {
        if (_inventoryGrid == null || _player?.Inventory == null)
            return;

        var inventory = _player.Inventory.Inventory;

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
                // Use XNA Color directly - Myra should handle the conversion
                var xnaColor = new Color(100, 150, 100, 255);
                panel.Background = new Myra.Graphics2D.Brushes.SolidBrush(xnaColor);
            }
            else
            {
                label.Text = "";
                // Use XNA Color directly - Myra should handle the conversion
                var xnaColor = new Color(50, 50, 50, 255);
                panel.Background = new Myra.Graphics2D.Brushes.SolidBrush(xnaColor);
                }

                slotIndex++;
            }
        }
    }

    /// <summary>
    /// Toggles the inventory panel visibility.
    /// </summary>
    public void Toggle()
    {
        // Always toggle based on current state
        IsVisible = !IsVisible;
        
        if (IsVisible)
        {
            // Ensure panel is added back to the desktop if it was removed
            if (_inventoryPanel != null && _inventoryPanel.Parent == null && _desktop != null)
            {
                if (_desktop.Root is Panel rootPanel)
                {
                    rootPanel.Widgets.Add(_inventoryPanel);
                }
            }
            
            // Bring panel to front
            if (_inventoryPanel != null && _inventoryPanel.Parent is Panel parentPanel)
            {
                parentPanel.Widgets.Remove(_inventoryPanel);
                parentPanel.Widgets.Add(_inventoryPanel);
            }
            
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Updates the inventory window, handling dragging.
    /// </summary>
    public void Update()
    {
        if (!IsVisible || _inventoryPanel == null || _titleBar == null)
        {
            _isDragging = false;
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

        // Check if mouse is over the title bar (excluding close button area)
        bool isMouseOverTitleBar = IsPointOverTitleBar(currentMousePos);

        // Handle window drag start (only if not dragging an item)
        if (!_isDraggingItem && inputService.IsLeftClickPressed && isMouseOverTitleBar && !_isDragging)
        {
            // Check if click is not on the close button
            if (!IsPointOverCloseButton(currentMousePos))
            {
                _isDragging = true;
                // Calculate offset from mouse position to panel top-left corner
                _dragOffset = new Point(
                    currentMousePos.X - _inventoryPanel.Left,
                    currentMousePos.Y - _inventoryPanel.Top
                );
                _lastMousePosition = currentMousePos;
            }
        }

        // Handle window dragging
        if (_isDragging && !_isDraggingItem && inputService.IsLeftMouseButtonDown)
        {
            // Calculate new position
            var newX = currentMousePos.X - _dragOffset.X;
            var newY = currentMousePos.Y - _dragOffset.Y;

            // Clamp to screen bounds (with some padding)
            var screenWidth = GameCore.Configuration.Configuration.WindowWidth;
            var screenHeight = GameCore.Configuration.Configuration.WindowHeight;
            var panelWidth = _inventoryPanel.Width ?? 400;
            var panelHeight = _inventoryPanel.Height ?? 300;
            newX = Math.Max(0, Math.Min(newX, screenWidth - panelWidth));
            newY = Math.Max(0, Math.Min(newY, screenHeight - panelHeight));

            _inventoryPanel.Left = newX;
            _inventoryPanel.Top = newY;
            _lastMousePosition = currentMousePos;
        }
        else if (_isDragging && !inputService.IsLeftMouseButtonDown)
        {
            // Stop dragging when mouse button is released
            _isDragging = false;
        }
    }

    /// <summary>
    /// Handles item drag and drop within the inventory.
    /// </summary>
    private void HandleItemDragAndDrop(IInputService inputService, Point mousePos)
    {
        if (_player?.Inventory?.Inventory == null)
            return;

        var inventory = _player.Inventory.Inventory;

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
            // Check if dropping on enemy inventory window
            if (_enemyInventoryWindow != null && _enemyInventoryWindow.IsVisible && 
                _enemyInventoryWindow.IsPointOverWindow(mousePos))
            {
                var enemySlotIndex = _enemyInventoryWindow.GetSlotIndexAtPoint(mousePos);
                if (enemySlotIndex.HasValue)
                {
                    // Transfer item to enemy inventory
                    TransferItemToEnemyInventory(_draggedSlotIndex, enemySlotIndex.Value);
                }
            }
            else if (hoveredSlotIndex.HasValue)
            {
                var targetSlotIndex = hoveredSlotIndex.Value;
                if (targetSlotIndex >= 0 && targetSlotIndex < inventory.SlotCount && targetSlotIndex != _draggedSlotIndex)
                {
                    // Swap the items within player inventory
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
    /// Handles a slot click (for starting drag).
    /// </summary>
    private void HandleSlotClick(int slotIndex)
    {
        // This is handled in Update() via HandleItemDragAndDrop
        // But we can use this for immediate feedback if needed
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
        foreach (var kvp in _slotPanels)
        {
            var panel = kvp.Key;
            var slotIndex = kvp.Value;

            // Calculate absolute position of the slot
            // We need to traverse the widget tree to get the actual position
            // For now, use a simpler approach: calculate based on grid layout
            if (_inventoryGrid is VerticalStackPanel mainPanel)
            {
                int slotRow = slotIndex / 6; // 6 columns
                int slotCol = slotIndex % 6;

                // Approximate position (accounting for padding and spacing)
                const int slotWidth = 60;
                const int slotHeight = 60;
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
    /// Checks if a point is over the title bar area.
    /// </summary>
    private bool IsPointOverTitleBar(Point point)
    {
        if (_inventoryPanel == null || _titleBar == null)
            return false;

        // Get the absolute position of the title bar
        var panelX = _inventoryPanel.Left;
        var panelY = _inventoryPanel.Top;
        var titleBarX = panelX;
        var titleBarY = panelY;

        return point.X >= titleBarX && point.X < titleBarX + _titleBar.Width &&
               point.Y >= titleBarY && point.Y < titleBarY + _titleBar.Height;
    }

    /// <summary>
    /// Checks if a point is over the close button.
    /// </summary>
    private bool IsPointOverCloseButton(Point point)
    {
        if (_inventoryPanel == null || _closeButton == null)
            return false;

        // Get the absolute position of the close button
        var panelX = _inventoryPanel.Left;
        var panelY = _inventoryPanel.Top;
        var closeButtonX = panelX + _inventoryPanel.Width - _closeButton.Width;
        var closeButtonY = panelY;

        return point.X >= closeButtonX && point.X < closeButtonX + _closeButton.Width &&
               point.Y >= closeButtonY && point.Y < closeButtonY + _closeButton.Height;
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
        var panelWidth = _inventoryPanel.Width ?? 400;
        var panelHeight = _inventoryPanel.Height ?? 300;

        return point.X >= panelX && point.X < panelX + panelWidth &&
               point.Y >= panelY && point.Y < panelY + panelHeight;
    }


    /// <summary>
    /// Transfers an item from player inventory to enemy inventory.
    /// </summary>
    private void TransferItemToEnemyInventory(int playerSlotIndex, int enemySlotIndex)
    {
        if (_player?.Inventory?.Inventory == null || _enemyInventoryWindow == null)
            return;

        var playerInventory = _player.Inventory.Inventory;
        var enemyInventory = _enemyInventoryWindow.GetCurrentEnemyInventory();
        
        if (enemyInventory == null)
            return;

        if (playerSlotIndex < 0 || playerSlotIndex >= playerInventory.SlotCount)
            return;

        if (enemySlotIndex < 0 || enemySlotIndex >= enemyInventory.SlotCount)
            return;

        var playerStack = playerInventory[playerSlotIndex];
        if (playerStack.IsEmpty || playerStack.Item == null)
            return;

        var enemyStack = enemyInventory[enemySlotIndex];

        // If enemy slot is empty, move the entire stack
        if (enemyStack.IsEmpty)
        {
            enemyStack.Item = playerStack.Item;
            enemyStack.Quantity = playerStack.Quantity;
            playerStack.Clear();
        }
        // If same item and stackable, try to merge
        else if (enemyStack.Item != null && enemyStack.Item.Id == playerStack.Item.Id && playerStack.Item.Stackable)
        {
            var added = enemyStack.TryAdd(playerStack.Item, playerStack.Quantity);
            playerStack.Remove(added);
        }
        // Otherwise, swap
        else
        {
            var tempItem = playerStack.Item;
            var tempQuantity = playerStack.Quantity;

            playerStack.Item = enemyStack.Item;
            playerStack.Quantity = enemyStack.Quantity;

            enemyStack.Item = tempItem;
            enemyStack.Quantity = tempQuantity;
        }

        UpdateDisplay();
        _enemyInventoryWindow.UpdateDisplay();
    }
}
