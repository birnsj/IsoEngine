using System.Text.Json;

namespace GameCore.Dialogs;

/// <summary>
/// Utility class for loading dialog graphs from JSON files.
/// </summary>
public static class DialogLoader
{
    /// <summary>
    /// Loads a dialog graph from JSON content.
    /// </summary>
    /// <param name="jsonContent">The JSON content as a string.</param>
    /// <returns>A DialogGraph instance.</returns>
    public static DialogGraph LoadDialogFromJson(string jsonContent)
    {
        try
        {
            var jsonDialog = JsonSerializer.Deserialize<JsonDialog>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonDialog == null)
            {
                throw new InvalidOperationException("Failed to deserialize dialog JSON");
            }

            var graph = new DialogGraph
            {
                Id = jsonDialog.Id,
                Name = jsonDialog.Name,
                StartNodeId = jsonDialog.StartNodeId
            };

            // Convert JSON nodes to DialogNode objects
            if (jsonDialog.Nodes != null)
            {
                foreach (var jsonNode in jsonDialog.Nodes)
                {
                    var node = new DialogNode
                    {
                        Id = jsonNode.Id,
                        Text = jsonNode.Text,
                        Choices = new List<DialogChoice>()
                    };

                    if (jsonNode.Choices != null)
                    {
                        foreach (var jsonChoice in jsonNode.Choices)
                        {
                            node.Choices.Add(new DialogChoice
                            {
                                Text = jsonChoice.Text,
                                NextNodeId = jsonChoice.NextNodeId,
                                RequiredFlag = jsonChoice.RequiredFlag,
                                RequiredValue = jsonChoice.RequiredValue ?? true,
                                SetFlag = jsonChoice.SetFlag,
                                SetFlagValue = jsonChoice.SetFlagValue ?? true
                            });
                        }
                    }

                    graph.Nodes[node.Id] = node;
                }
            }

            return graph;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error loading dialog: {ex.Message}", ex);
        }
    }

    // JSON deserialization classes
    private class JsonDialog
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string StartNodeId { get; set; } = string.Empty;
        public List<JsonNode>? Nodes { get; set; }
    }

    private class JsonNode
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<JsonChoice>? Choices { get; set; }
    }

    private class JsonChoice
    {
        public string Text { get; set; } = string.Empty;
        public string? NextNodeId { get; set; }
        public string? RequiredFlag { get; set; }
        public bool? RequiredValue { get; set; }
        public string? SetFlag { get; set; }
        public bool? SetFlagValue { get; set; }
    }
}

