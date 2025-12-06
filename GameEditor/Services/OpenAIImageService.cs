using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using GameEditor.Utilities;

namespace GameEditor.Services;

/// <summary>
/// Service for generating tile images using OpenAI's DALL·E API.
/// </summary>
public class OpenAIImageService
{
    private HttpClient? _httpClient;
    private string? _apiKey;

    /// <summary>
    /// Gets or sets the OpenAI API key.
    /// </summary>
    public string? ApiKey
    {
        get => _apiKey;
        set
        {
            _apiKey = value;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
            else
            {
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }
    }

    /// <summary>
    /// Checks if the service is configured with an API key.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && _httpClient != null;

    /// <summary>
    /// Generates an image using DALL·E based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate.</param>
    /// <param name="size">The size of the image (default: 1024x1024).</param>
    /// <returns>The generated image as a byte array, or null if generation failed.</returns>
    public async Task<byte[]?> GenerateImageAsync(string prompt, string size = "1024x1024")
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("OpenAI API key is not configured. Please set the API key in settings.");
        }

        try
        {
            var requestBody = new
            {
                model = "dall-e-3",
                prompt = prompt,
                size = size,
                n = 1,
                response_format = "url"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync("https://api.openai.com/v1/images/generations", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<dynamic>(responseJson);

            if (responseObj?.data != null)
            {
                var dataArray = responseObj.data as Newtonsoft.Json.Linq.JArray;
                if (dataArray != null && dataArray.Count > 0)
                {
                    var firstData = dataArray[0] as Newtonsoft.Json.Linq.JObject;
                    var imageUrl = firstData?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        // Download the image from the URL
                        using var downloadClient = new HttpClient();
                        var imageBytes = await downloadClient.GetByteArrayAsync(imageUrl);
                        return imageBytes;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image and saves it to a file.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate.</param>
    /// <param name="outputPath">The path where the image should be saved.</param>
    /// <param name="size">The size string for generation (e.g., "1024x1024").</param>
    /// <param name="targetWidth">Target width for the final tile image (default: uses size parameter).</param>
    /// <param name="targetHeight">Target height for the final tile image (default: uses size parameter).</param>
    /// <param name="applyDiamondMask">Whether to apply a diamond mask to make the image isometric (default: true).</param>
    /// <returns>True if the image was generated and saved successfully, false otherwise.</returns>
    public async Task<bool> GenerateImageToFileAsync(string prompt, string outputPath, string size = "1024x1024", int? targetWidth = null, int? targetHeight = null, bool applyDiamondMask = true)
    {
        try
        {
            var imageBytes = await GenerateImageAsync(prompt, size);
            if (imageBytes != null)
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Parse size to get generation width and height
                var sizeParts = size.Split('x');
                int genWidth = 1024;
                int genHeight = 1024;
                if (sizeParts.Length == 2)
                {
                    int.TryParse(sizeParts[0], out genWidth);
                    int.TryParse(sizeParts[1], out genHeight);
                }

                // Use target dimensions if provided, otherwise use generation size
                int finalWidth = targetWidth ?? genWidth;
                int finalHeight = targetHeight ?? genHeight;

                using var imageStream = new MemoryStream(imageBytes);
                using var originalImage = new Bitmap(imageStream);
                
                // Resize to target tile dimensions if different from generated size
                Bitmap resizedImage;
                if (originalImage.Width != finalWidth || originalImage.Height != finalHeight)
                {
                    resizedImage = new Bitmap(finalWidth, finalHeight, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(resizedImage))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(originalImage, 0, 0, finalWidth, finalHeight);
                    }
                }
                else
                {
                    resizedImage = new Bitmap(originalImage);
                }
                
                Bitmap finalImage = resizedImage;
                
                // Apply diamond mask if requested
                if (applyDiamondMask)
                {
                    var mask = TileMaskUtility.GetOrCreateMask(finalWidth, finalHeight);
                    finalImage = TileMaskUtility.ApplyMask(resizedImage, mask);
                    mask.Dispose();
                    if (finalImage != resizedImage)
                    {
                        resizedImage.Dispose();
                    }
                }
                
                // Ensure file is ready for editing in Perforce (checkout if exists, or prepare for add)
                PerforceService.EnsureFileReadyForEdit(outputPath);
                
                // Save the final image at exact tile size
                finalImage.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                
                // Add file to Perforce if it's new
                PerforceService.AddFile(outputPath);
                
                if (finalImage != resizedImage)
                {
                    finalImage.Dispose();
                }
                
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a prompt for a specific tile type.
    /// </summary>
    /// <param name="tileIndex">The tile index (0-7).</param>
    /// <param name="customPrompt">Optional custom prompt. If not provided, a default prompt is generated.</param>
    /// <returns>A prompt string for image generation.</returns>
    public static string GetTilePrompt(int tileIndex, string? customPrompt = null)
    {
        if (!string.IsNullOrEmpty(customPrompt))
        {
            return customPrompt;
        }

        return tileIndex switch
        {
            0 => "isometric grass tile, pixel art style, top-down view, game asset, clean background",
            1 => "isometric dark grass tile, pixel art style, top-down view, game asset, clean background",
            2 => "isometric dirt tile, pixel art style, top-down view, game asset, clean background",
            3 => "isometric dark dirt tile, pixel art style, top-down view, game asset, clean background",
            4 => "isometric water tile, pixel art style, top-down view, game asset, clean background, blue water",
            5 => "isometric deep water tile, pixel art style, top-down view, game asset, clean background, dark blue water",
            6 => "isometric stone tile, pixel art style, top-down view, game asset, clean background, gray stone",
            7 => "isometric dark stone tile, pixel art style, top-down view, game asset, clean background, dark gray stone",
            _ => "isometric game tile, pixel art style, top-down view, game asset, clean background"
        };
    }
}

