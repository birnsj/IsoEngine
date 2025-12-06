using FluentAssertions;
using GameCore.Combat;
using GameCore.Entities;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Moq;
using Xunit;

namespace GameCore.Tests.Services;

public class CombatServiceTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly CombatService _combatService;

    public CombatServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _combatService = new CombatService(_mockLogger.Object);
    }

    [Fact]
    public void PerformMeleeAttack_ValidAttack_DealsDamage()
    {
        // Arrange
        var attacker = CreateEntityWithStats(100, 20, 5); // 20 attack power
        var target = CreateEntityWithStats(100, 10, 3); // 3 defense
        
        // Act
        var result = _combatService.PerformMeleeAttack(attacker, target);
        
        // Assert
        result.Should().BeTrue();
        var targetStats = target.GetComponent<StatsComponent>();
        targetStats.Should().NotBeNull();
        targetStats!.CurrentHealth.Should().BeLessThan(100); // Should have taken damage
    }

    [Fact]
    public void PerformMeleeAttack_AttackerNull_ReturnsFalse()
    {
        // Arrange
        var target = CreateEntityWithStats(100, 10, 3);
        
        // Act
        var result = _combatService.PerformMeleeAttack(null!, target);
        
        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.Warning(It.Is<string>(s => s.Contains("null"))), Times.Once);
    }

    [Fact]
    public void PerformMeleeAttack_TargetNull_ReturnsFalse()
    {
        // Arrange
        var attacker = CreateEntityWithStats(100, 20, 5);
        
        // Act
        var result = _combatService.PerformMeleeAttack(attacker, null!);
        
        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.Warning(It.Is<string>(s => s.Contains("null"))), Times.Once);
    }

    [Fact]
    public void PerformMeleeAttack_TargetDead_ReturnsFalse()
    {
        // Arrange
        var attacker = CreateEntityWithStats(100, 20, 5);
        var target = CreateEntityWithStats(100, 10, 3);
        var targetStats = target.GetComponent<StatsComponent>();
        targetStats!.ApplyDamage(100); // Kill target
        
        // Act
        var result = _combatService.PerformMeleeAttack(attacker, target);
        
        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("dead"))), Times.Once);
    }

    [Fact]
    public void PerformMeleeAttack_NoStatsComponent_ReturnsFalse()
    {
        // Arrange
        var attacker = new Entity(Vector2.Zero, Vector2.One);
        var target = new Entity(Vector2.Zero, Vector2.One);
        
        // Act
        var result = _combatService.PerformMeleeAttack(attacker, target);
        
        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.Warning(It.Is<string>(s => s.Contains("StatsComponent"))), Times.Once);
    }

    [Fact]
    public void CalculateDamage_ValidAttack_ReturnsCorrectDamage()
    {
        // Arrange
        var attacker = CreateEntityWithStats(100, 20, 5); // 20 attack
        var target = CreateEntityWithStats(100, 10, 3); // 3 defense
        
        // Act
        var damage = _combatService.CalculateDamage(attacker, target);
        
        // Assert
        damage.Should().Be(17); // 20 - 3 = 17
    }

    [Fact]
    public void CalculateDamage_DefenseExceedsAttack_ReturnsMinimumOne()
    {
        // Arrange
        var attacker = CreateEntityWithStats(100, 5, 2); // 5 attack
        var target = CreateEntityWithStats(100, 10, 10); // 10 defense
        
        // Act
        var damage = _combatService.CalculateDamage(attacker, target);
        
        // Assert
        damage.Should().Be(1); // Minimum 1 damage
    }

    [Fact]
    public void CalculateDamage_NullEntities_ReturnsZero()
    {
        // Act
        var damage = _combatService.CalculateDamage(null!, null!);
        
        // Assert
        damage.Should().Be(0);
    }

    private static Entity CreateEntityWithStats(int maxHealth, int attackPower, int defense)
    {
        var entity = new Entity(Vector2.Zero, Vector2.One);
        var stats = new StatsComponent(maxHealth, attackPower, defense);
        entity.AddComponent(stats);
        return entity;
    }
}








