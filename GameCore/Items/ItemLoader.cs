using System.Text.Json;

namespace GameCore.Items;

/// <summary>
/// Utility class for loading items from JSON files.
/// </summary>
public static class ItemLoader
{
    /// <summary>
    /// Loads items from a JSON file.
    /// </summary>
    /// <param name="jsonContent">The JSON content as a string.</param>
    /// <returns>A dictionary of items keyed by their ID.</returns>
    public static Dictionary<string, Item> LoadItemsFromJson(string jsonContent)
    {
        var items = new Dictionary<string, Item>();

        try
        {
            var jsonItems = JsonSerializer.Deserialize<JsonItem[]>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonItems != null)
            {
                foreach (var jsonItem in jsonItems)
                {
                    var item = new Item(
                        jsonItem.Id,
                        jsonItem.Name,
                        jsonItem.Description,
                        jsonItem.Stackable,
                        jsonItem.MaxStack,
                        jsonItem.ImagePath
                    );
                    items[item.Id] = item;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error if logger is available
            System.Diagnostics.Debug.WriteLine($"Error loading items: {ex.Message}");
        }

        return items;
    }

    private class JsonItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Stackable { get; set; } = true;
        public int MaxStack { get; set; } = 99;
        public string? ImagePath { get; set; }
    }
}

