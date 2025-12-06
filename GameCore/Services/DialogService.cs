using GameCore.Dialogs;
using GameCore.Entities;
using Microsoft.Xna.Framework;

namespace GameCore.Services;

/// <summary>
/// Service that manages dialog conversations with NPCs.
/// </summary>
public class DialogService : IGameService
{
    private DialogGraph? _currentDialog;
    private DialogNode? _currentNode;
    private Player? _dialogPlayer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the DialogService class.
    /// </summary>
    /// <param name="logger">The logger service. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
    public DialogService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether a dialog is currently active.
    /// </summary>
    public bool IsDialogActive => _currentDialog != null && _currentNode != null;

    /// <summary>
    /// Gets the current dialog node text.
    /// </summary>
    public string? CurrentNodeText => _currentNode?.Text;

    /// <summary>
    /// Gets the available choices for the current node.
    /// </summary>
    public List<DialogChoice> GetAvailableChoices()
    {
        if (_currentNode == null || _dialogPlayer == null)
            return new List<DialogChoice>();

        var availableChoices = new List<DialogChoice>();

        foreach (var choice in _currentNode.Choices)
        {
            // Check if choice is available based on required flag
            if (string.IsNullOrEmpty(choice.RequiredFlag))
            {
                // No requirement, always available
                availableChoices.Add(choice);
                _logger.Debug($"Choice '{choice.Text}' is available (no flag requirement)");
            }
            else
            {
                // Check if player has the required flag value
                var playerFlagValue = _dialogPlayer.GetDialogFlag(choice.RequiredFlag);
                _logger.Debug($"Choice '{choice.Text}' requires flag '{choice.RequiredFlag}'={choice.RequiredValue}, player has {playerFlagValue}");
                
                if (playerFlagValue == choice.RequiredValue)
                {
                    availableChoices.Add(choice);
                    _logger.Debug($"Choice '{choice.Text}' is available (flag matches)");
                }
                else
                {
                    _logger.Debug($"Choice '{choice.Text}' is NOT available (flag doesn't match)");
                }
            }
        }

        return availableChoices;
    }

    public void Initialize()
    {
        // Initialization complete via constructor injection
    }

    public void Update(GameTime gameTime)
    {
        // Dialog service doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        // Dialog service doesn't need rendering
    }

    /// <summary>
    /// Starts a dialog with the given dialog graph and player.
    /// </summary>
    /// <param name="dialogGraph">The dialog graph to use.</param>
    /// <param name="player">The player participating in the dialog.</param>
    /// <returns>True if the dialog was started successfully, false otherwise.</returns>
    public bool StartDialog(DialogGraph dialogGraph, Player player)
    {
        if (dialogGraph == null || player == null)
        {
            _logger.Warning("Cannot start dialog: dialog graph or player is null");
            return false;
        }

        _currentDialog = dialogGraph;
        _dialogPlayer = player;
        _currentNode = dialogGraph.GetStartNode();

        if (_currentNode == null)
        {
            _logger.Error($"Cannot start dialog: start node '{dialogGraph.StartNodeId}' not found");
            _currentDialog = null;
            _dialogPlayer = null;
            return false;
        }

        _logger.Info($"Started dialog '{dialogGraph.Name}' with node '{_currentNode.Id}'");
        return true;
    }

    /// <summary>
    /// Selects a choice and advances the dialog.
    /// </summary>
    /// <param name="choice">The choice to select.</param>
    /// <returns>True if the dialog continues, false if it ends.</returns>
    public bool SelectChoice(DialogChoice choice)
    {
        if (choice == null || _currentDialog == null || _dialogPlayer == null)
        {
            _logger.Warning("Cannot select choice: choice, dialog, or player is null");
            return false;
        }

        // Apply choice effects (set flags)
        if (!string.IsNullOrEmpty(choice.SetFlag))
        {
            _dialogPlayer.SetDialogFlag(choice.SetFlag, choice.SetFlagValue);
            _logger.Info($"Set dialog flag '{choice.SetFlag}' to {choice.SetFlagValue}");
        }

        // Check if dialog should end
        if (string.IsNullOrEmpty(choice.NextNodeId))
        {
            _logger.Info("Dialog ended (no next node)");
            EndDialog();
            return false;
        }

        // Move to next node
        _currentNode = _currentDialog.GetNode(choice.NextNodeId);
        if (_currentNode == null)
        {
            _logger.Warning($"Next node '{choice.NextNodeId}' not found, ending dialog");
            EndDialog();
            return false;
        }

        _logger.Info($"Advanced to dialog node '{_currentNode.Id}'");
        return true;
    }

    /// <summary>
    /// Ends the current dialog.
    /// </summary>
    public void EndDialog()
    {
        if (_currentDialog != null)
        {
            _logger.Info($"Ended dialog '{_currentDialog.Name}'");
        }

        _currentDialog = null;
        _currentNode = null;
        _dialogPlayer = null;
    }
}

