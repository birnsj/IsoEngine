using FluentAssertions;
using GameCore.Items;
using Xunit;

namespace GameCore.Tests.Items;

public class InventoryTests
{
    [Fact]
    public void AddItem_StackableItem_AddsToExistingStack()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "A shiny gold coin.", true, 999);
        
        // Act
        var added1 = inventory.AddItem(item, 5);
        var added2 = inventory.AddItem(item, 3);
        
        // Assert
        added1.Should().Be(5);
        added2.Should().Be(3);
        inventory.GetItemCount("gold_coin").Should().Be(8);
    }

    [Fact]
    public void AddItem_NonStackableItem_CreatesNewSlots()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("sword", "Iron Sword", "A basic sword.", false, 1);
        
        // Act
        var added1 = inventory.AddItem(item, 1);
        var added2 = inventory.AddItem(item, 1);
        
        // Assert
        added1.Should().Be(1);
        added2.Should().Be(1);
        inventory.GetItemCount("sword").Should().Be(2);
    }

    [Fact]
    public void AddItem_ExceedsMaxStack_SplitsAcrossSlots()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "", true, 10); // Max stack of 10
        
        // Act
        var added = inventory.AddItem(item, 25);
        
        // Assert
        added.Should().Be(25);
        inventory.GetItemCount("gold_coin").Should().Be(25);
    }

    [Fact]
    public void RemoveItem_ExistingItem_RemovesCorrectQuantity()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "", true, 999);
        inventory.AddItem(item, 10);
        
        // Act
        var removed = inventory.RemoveItem("gold_coin", 7);
        
        // Assert
        removed.Should().Be(7);
        inventory.GetItemCount("gold_coin").Should().Be(3);
    }

    [Fact]
    public void RemoveItem_MoreThanAvailable_RemovesOnlyAvailable()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "", true, 999);
        inventory.AddItem(item, 5);
        
        // Act
        var removed = inventory.RemoveItem("gold_coin", 10);
        
        // Assert
        removed.Should().Be(5);
        inventory.GetItemCount("gold_coin").Should().Be(0);
    }

    [Fact]
    public void HasSpace_EmptyInventory_ReturnsTrue()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "", true, 999);
        
        // Act
        var hasSpace = inventory.HasSpace(item, 100);
        
        // Assert
        hasSpace.Should().BeTrue();
    }

    [Fact]
    public void HasSpace_FullInventory_ReturnsFalse()
    {
        // Arrange
        var inventory = new Inventory(2);
        var item = new Item("sword", "Sword", "", false, 1);
        inventory.AddItem(item, 1);
        inventory.AddItem(item, 1);
        
        // Act
        var hasSpace = inventory.HasSpace(item, 1);
        
        // Assert
        hasSpace.Should().BeFalse();
    }

    [Fact]
    public void AddItem_ZeroQuantity_ReturnsZero()
    {
        // Arrange
        var inventory = new Inventory(24);
        var item = new Item("gold_coin", "Gold Coin", "", true, 999);
        
        // Act
        var added = inventory.AddItem(item, 0);
        
        // Assert
        added.Should().Be(0);
        inventory.GetItemCount("gold_coin").Should().Be(0);
    }

    [Fact]
    public void RemoveItem_NonExistentItem_ReturnsZero()
    {
        // Arrange
        var inventory = new Inventory(24);
        
        // Act
        var removed = inventory.RemoveItem("nonexistent", 5);
        
        // Assert
        removed.Should().Be(0);
    }
}








