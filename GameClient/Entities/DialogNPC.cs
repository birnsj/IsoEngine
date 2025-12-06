using GameCore;
using GameCore.Dialogs;
using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Services;
using Microsoft.Xna.Framework;

namespace GameClient.Entities;

/// <summary>
/// An NPC that can engage in dialog conversations with the player.
/// </summary>
public class DialogNPC : InteractiveObject
{
    private readonly DialogGraph _dialogGraph;

    /// <summary>
    /// Initializes a new instance of the DialogNPC class.
    /// </summary>
    /// <param name="position">Position in screen coordinates.</param>
    /// <param name="name">Name of the NPC.</param>
    /// <param name="description">Description shown when nearby.</param>
    /// <param name="dialogGraph">The dialog graph for this NPC's conversations.</param>
    public DialogNPC(Vector2 position, string name, string description, DialogGraph dialogGraph)
        : base(position, new Vector2(GameConstants.Player.DefaultSize, GameConstants.Player.DefaultSize), name, description)
    {
        _dialogGraph = dialogGraph;
    }

    public override void OnInteract(Player player)
    {
        var dialogService = ServiceLocator.Get<DialogService>();
        if (dialogService != null)
        {
            dialogService.StartDialog(_dialogGraph, player);
            
            // Notify the UI to show the dialog window
            // This will be handled by the game's Update method checking DialogService.IsDialogActive
            var logger = ServiceLocator.Get<ILogger>();
            logger?.Info($"Started dialog with {Name}");
        }
    }
}

