using FluentAssertions;
using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Moq;
using Xunit;

namespace GameCore.Tests.Services;

public class InteractionServiceTests
{
    private readonly InteractionService _service;

    public InteractionServiceTests()
    {
        _service = new InteractionService();
        _service.Initialize();
    }

    [Fact]
    public void RegisterInteractable_ValidInteractable_AddsToService()
    {
        // Arrange
        var interactable = CreateTestInteractable(Vector2.Zero, "Test", "Description");

        // Act
        _service.RegisterInteractable(interactable);

        // Assert
        _service.GetAllInteractables().Should().Contain(interactable);
    }

    [Fact]
    public void RegisterInteractable_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.RegisterInteractable(null!));
    }

    [Fact]
    public void RegisterInteractable_Duplicate_DoesNotAddTwice()
    {
        // Arrange
        var interactable = CreateTestInteractable(Vector2.Zero, "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        _service.RegisterInteractable(interactable);

        // Assert
        _service.GetAllInteractables().Count(i => i == interactable).Should().Be(1);
    }

    [Fact]
    public void UnregisterInteractable_RegisteredInteractable_RemovesFromService()
    {
        // Arrange
        var interactable = CreateTestInteractable(Vector2.Zero, "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        _service.UnregisterInteractable(interactable);

        // Assert
        _service.GetAllInteractables().Should().NotContain(interactable);
    }

    [Fact]
    public void UnregisterInteractable_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.UnregisterInteractable(null!));
    }

    [Fact]
    public void GetNearestInteractable_WithinRange_ReturnsInteractable()
    {
        // Arrange
        var player = CreatePlayer(Vector2.Zero);
        var interactable = CreateTestInteractable(new Vector2(30, 0), "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        var nearest = _service.GetNearestInteractable(player);

        // Assert
        nearest.Should().NotBeNull();
        nearest!.Name.Should().Be("Test");
    }

    [Fact]
    public void GetNearestInteractable_OutOfRange_ReturnsNull()
    {
        // Arrange
        var player = CreatePlayer(Vector2.Zero);
        var interactable = CreateTestInteractable(new Vector2(1000, 0), "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        var nearest = _service.GetNearestInteractable(player);

        // Assert
        nearest.Should().BeNull();
    }

    [Fact]
    public void GetNearestInteractable_MultipleInteractables_ReturnsNearest()
    {
        // Arrange
        var player = CreatePlayer(Vector2.Zero);
        var farInteractable = CreateTestInteractable(new Vector2(40, 0), "Far", "Description");
        var nearInteractable = CreateTestInteractable(new Vector2(20, 0), "Near", "Description");
        _service.RegisterInteractable(farInteractable);
        _service.RegisterInteractable(nearInteractable);

        // Act
        var nearest = _service.GetNearestInteractable(player);

        // Assert
        nearest.Should().NotBeNull();
        nearest!.Name.Should().Be("Near");
    }

    [Fact]
    public void TryInteract_PlayerFacingInteractable_ReturnsInteractable()
    {
        // Arrange
        var player = CreatePlayer(Vector2.Zero);
        player.FacingDirection = Vector2.UnitX; // Face right
        var interactable = CreateTestInteractable(new Vector2(30, 0), "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        var result = _service.TryInteract(player);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public void TryInteract_PlayerNotFacingInteractable_ReturnsNull()
    {
        // Arrange
        var player = CreatePlayer(Vector2.Zero);
        player.FacingDirection = Vector2.UnitY; // Face down (away from interactable)
        var interactable = CreateTestInteractable(new Vector2(30, 0), "Test", "Description");
        _service.RegisterInteractable(interactable);

        // Act
        var result = _service.TryInteract(player);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryInteract_NullPlayer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.TryInteract(null!));
    }

    [Fact]
    public void GetNearestInteractable_NullPlayer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetNearestInteractable(null!));
    }

    private static Player CreatePlayer(Vector2 position)
    {
        return new Player(position);
    }

    private static IInteractable CreateTestInteractable(Vector2 position, string name, string description)
    {
        var entity = new Entity(position, new Vector2(32, 32));
        return new TestInteractableWrapper(entity, name, description);
    }

    private class TestInteractableWrapper : InteractiveObject
    {
        public TestInteractableWrapper(Entity entity, string name, string description)
            : base(entity.Position, entity.Size, name, description)
        {
        }

        public override void OnInteract(Player player)
        {
            // Test implementation
        }
    }
}








