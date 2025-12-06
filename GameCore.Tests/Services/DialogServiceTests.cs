using FluentAssertions;
using GameCore.Dialogs;
using GameCore.Entities;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Moq;
using Xunit;

namespace GameCore.Tests.Services;

public class DialogServiceTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly DialogService _service;
    private readonly Player _player;

    public DialogServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _service = new DialogService(_mockLogger.Object);
        _service.Initialize();
        _player = new Player(Vector2.Zero);
    }

    [Fact]
    public void StartDialog_ValidDialog_ReturnsTrue()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();

        // Act
        var result = _service.StartDialog(dialogGraph, _player);

        // Assert
        result.Should().BeTrue();
        _service.IsDialogActive.Should().BeTrue();
        _service.CurrentNodeText.Should().Be("Hello!");
    }

    [Fact]
    public void StartDialog_NullDialog_ReturnsFalse()
    {
        // Act
        var result = _service.StartDialog(null!, _player);

        // Assert
        result.Should().BeFalse();
        _service.IsDialogActive.Should().BeFalse();
        _mockLogger.Verify(x => x.Warning(It.Is<string>(s => s.Contains("null"))), Times.Once);
    }

    [Fact]
    public void StartDialog_NullPlayer_ReturnsFalse()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();

        // Act
        var result = _service.StartDialog(dialogGraph, null!);

        // Assert
        result.Should().BeFalse();
        _service.IsDialogActive.Should().BeFalse();
        _mockLogger.Verify(x => x.Warning(It.Is<string>(s => s.Contains("null"))), Times.Once);
    }

    [Fact]
    public void SelectChoice_ValidChoice_AdvancesDialog()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();
        _service.StartDialog(dialogGraph, _player);
        var choices = _service.GetAvailableChoices();
        var choice = choices.First();

        // Act
        var result = _service.SelectChoice(choice);

        // Assert
        result.Should().BeTrue();
        _service.IsDialogActive.Should().BeTrue();
    }

    [Fact]
    public void SelectChoice_EndingChoice_EndsDialog()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();
        _service.StartDialog(dialogGraph, _player);
        var choices = _service.GetAvailableChoices();
        var choice = choices.First(c => string.IsNullOrEmpty(c.NextNodeId));

        // Act
        var result = _service.SelectChoice(choice);

        // Assert
        result.Should().BeFalse();
        _service.IsDialogActive.Should().BeFalse();
    }

    [Fact]
    public void SelectChoice_SetsFlag_UpdatesPlayerFlag()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();
        _service.StartDialog(dialogGraph, _player);
        var choices = _service.GetAvailableChoices();
        var choice = choices.First(c => !string.IsNullOrEmpty(c.SetFlag));

        // Act
        _service.SelectChoice(choice);

        // Assert
        if (!string.IsNullOrEmpty(choice.SetFlag))
        {
            _player.GetDialogFlag(choice.SetFlag).Should().Be(choice.SetFlagValue);
        }
        _mockLogger.Verify(x => x.Info(It.Is<string>(s => choice.SetFlag != null && s.Contains(choice.SetFlag))), Times.Once);
    }

    [Fact]
    public void EndDialog_ActiveDialog_EndsDialog()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();
        _service.StartDialog(dialogGraph, _player);

        // Act
        _service.EndDialog();

        // Assert
        _service.IsDialogActive.Should().BeFalse();
        _service.CurrentNodeText.Should().BeNull();
    }

    [Fact]
    public void GetAvailableChoices_NoDialog_ReturnsEmpty()
    {
        // Act
        var choices = _service.GetAvailableChoices();

        // Assert
        choices.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableChoices_WithFlagRequirement_OnlyShowsMatching()
    {
        // Arrange
        var dialogGraph = CreateTestDialogGraph();
        _player.SetDialogFlag("hasKey", true);
        _service.StartDialog(dialogGraph, _player);

        // Act
        var choices = _service.GetAvailableChoices();

        // Assert
        choices.Should().Contain(c => c.RequiredFlag == "hasKey" && c.RequiredValue == true);
        choices.Should().NotContain(c => c.RequiredFlag == "hasKey" && c.RequiredValue == false);
    }

    private static DialogGraph CreateTestDialogGraph()
    {
        var startNode = new DialogNode
        {
            Id = "start",
            Text = "Hello!"
        };

        var nextNode = new DialogNode
        {
            Id = "next",
            Text = "How are you?"
        };

        startNode.Choices.Add(new DialogChoice
        {
            Text = "Good",
            NextNodeId = "next",
            SetFlag = "greeted",
            SetFlagValue = true
        });

        startNode.Choices.Add(new DialogChoice
        {
            Text = "Bye",
            NextNodeId = null // Ends dialog
        });

        nextNode.Choices.Add(new DialogChoice
        {
            Text = "Goodbye",
            NextNodeId = null
        });

        var dialogGraph = new DialogGraph
        {
            Id = "test_dialog",
            Name = "Test Dialog",
            StartNodeId = "start"
        };

        dialogGraph.Nodes.Add(startNode.Id, startNode);
        dialogGraph.Nodes.Add(nextNode.Id, nextNode);

        return dialogGraph;
    }
}

