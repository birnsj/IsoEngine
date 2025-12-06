using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using GameEditor.Utilities;

namespace GameEditor.Services;

/// <summary>
/// Service for generating tile images using free image generation APIs.
/// Supports multiple providers including Pollinations.ai (completely free, no API key).
/// </summary>
public class FreeImageGenerationService
{
    private readonly HttpClient _httpClient;

    public enum Provider
    {
        Pollinations,      // Completely free, no API key (uses Stable Diffusion Flux)
        StableDiffusion,   // Stable Diffusion via Replicate API (requires API key, but has free tier)
        OpenJourney,       // OpenJourney via Replicate API (requires API key, but has free tier)
        DeepAI,            // DeepAI API (requires API key, free tier available)
        Prodia,            // Free tier available
        Craiyon            // Free tier available
    }

    public Provider CurrentProvider { get; set; } = Provider.Pollinations;
    public string? ApiKey { get; set; } // Only needed for Prodia

    public FreeImageGenerationService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Generates an image using the selected free provider.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate.</param>
    /// <param name="width">Width of the image (default: 1024).</param>
    /// <param name="height">Height of the image (default: 1024).</param>
    /// <returns>The generated image as a byte array, or null if generation failed.</returns>
    public async Task<byte[]?> GenerateImageAsync(string prompt, int width = 1024, int height = 1024)
    {
        return CurrentProvider switch
        {
            Provider.Pollinations => await GenerateWithPollinationsAsync(prompt, width, height),
            Provider.StableDiffusion => await GenerateWithStableDiffusionAsync(prompt, width, height),
            Provider.OpenJourney => await GenerateWithOpenJourneyAsync(prompt, width, height),
            Provider.DeepAI => await GenerateWithDeepAIAsync(prompt, width, height),
            Provider.Prodia => await GenerateWithProdiaAsync(prompt, width, height),
            Provider.Craiyon => await GenerateWithCraiyonAsync(prompt),
            _ => null
        };
    }

    /// <summary>
    /// Generates an image using Pollinations.ai (completely free, no API key required).
    /// </summary>
    private async Task<byte[]?> GenerateWithPollinationsAsync(string prompt, int width, int height)
    {
        try
        {
            // Pollinations.ai API - completely free, no API key needed
            var url = $"https://image.pollinations.ai/prompt/{Uri.EscapeDataString(prompt)}?width={width}&height={height}&model=flux&nologo=true";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            return imageBytes;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with Pollinations.ai: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image using OpenJourney via Replicate API.
    /// Requires API key but has a free tier.
    /// OpenJourney is trained on Midjourney v4 images for artistic, high-quality results.
    /// </summary>
    private async Task<byte[]?> GenerateWithOpenJourneyAsync(string prompt, int width, int height)
    {
        try
        {
            // Replicate API for OpenJourney
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new InvalidOperationException("Replicate API key is required for OpenJourney. Get one at https://replicate.com/account/api-tokens");
            }

            // Use OpenJourney v4 model - try using model owner/name format first, fallback to version hash
            var request = new
            {
                model = "prompthero/openjourney-v4", // Use model owner/name format (preferred)
                input = new
                {
                    prompt = prompt,
                    width = width,
                    height = height,
                    num_outputs = 1
                }
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {ApiKey}");

            // Create prediction
            var createResponse = await _httpClient.PostAsync("https://api.replicate.com/v1/predictions", content);
            
            if (!createResponse.IsSuccessStatusCode)
            {
                var errorContent = await createResponse.Content.ReadAsStringAsync();
                throw new Exception($"Replicate API error: {createResponse.StatusCode} - {errorContent}");
            }

            var createJson = await createResponse.Content.ReadAsStringAsync();
            var createObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(createJson);
            
            // Check for errors in response
            if (createObj.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = errorElement.GetString() ?? "Unknown error";
                throw new Exception($"OpenJourney API error: {errorMessage}");
            }
            
            var predictionId = createObj.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(predictionId))
            {
                throw new Exception("Failed to get prediction ID from Replicate API");
            }

            // Poll for completion
            for (int i = 0; i < 120; i++) // Max 120 attempts (about 4 minutes)
            {
                await Task.Delay(2000); // Wait 2 seconds

                var statusResponse = await _httpClient.GetAsync($"https://api.replicate.com/v1/predictions/{predictionId}");
                
                if (!statusResponse.IsSuccessStatusCode)
                {
                    var errorContent = await statusResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Replicate API status check error: {statusResponse.StatusCode} - {errorContent}");
                }

                var statusJson = await statusResponse.Content.ReadAsStringAsync();
                var statusObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(statusJson);
                
                // Check for errors
                if (statusObj.TryGetProperty("error", out var statusErrorElement))
                {
                    var errorMessage = statusErrorElement.GetString() ?? "Unknown error";
                    throw new Exception($"OpenJourney generation error: {errorMessage}");
                }
                
                var status = statusObj.GetProperty("status").GetString();

                if (status == "succeeded")
                {
                    var output = statusObj.GetProperty("output");
                    if (output.ValueKind == System.Text.Json.JsonValueKind.Array && output.GetArrayLength() > 0)
                    {
                        var imageUrl = output[0].GetString();
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            var imageResponse = await _httpClient.GetAsync(imageUrl);
                            imageResponse.EnsureSuccessStatusCode();
                            return await imageResponse.Content.ReadAsByteArrayAsync();
                        }
                    }
                    else if (output.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        // Sometimes output is a direct string URL
                        var imageUrl = output.GetString();
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            var imageResponse = await _httpClient.GetAsync(imageUrl);
                            imageResponse.EnsureSuccessStatusCode();
                            return await imageResponse.Content.ReadAsByteArrayAsync();
                        }
                    }
                    throw new Exception("OpenJourney succeeded but no image URL found in output");
                }
                else if (status == "failed" || status == "canceled")
                {
                    // Try to get error details
                    string errorDetails = "";
                    if (statusObj.TryGetProperty("error", out var errorDetailsElement))
                    {
                        errorDetails = errorDetailsElement.GetString() ?? "";
                    }
                    throw new Exception($"OpenJourney generation failed: {status}" + (string.IsNullOrEmpty(errorDetails) ? "" : $" - {errorDetails}"));
                }
            }

            throw new Exception("OpenJourney generation timed out after 4 minutes");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with OpenJourney: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image using DeepAI's text2img API.
    /// Requires API key but has a free tier.
    /// </summary>
    private async Task<byte[]?> GenerateWithDeepAIAsync(string prompt, int width, int height)
    {
        try
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new InvalidOperationException("DeepAI API key is required. Get one at https://deepai.org/profile/i-want-an-api-key");
            }

            // DeepAI text2img endpoint
            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text", prompt)
            });

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", ApiKey);

            var response = await _httpClient.PostAsync("https://api.deepai.org/api/text2img", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            
            // DeepAI returns the image URL in 'output_url' field
            if (jsonObj.TryGetProperty("output_url", out var outputUrlElement))
            {
                var imageUrl = outputUrlElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    // Download the image from the URL
                    var imageResponse = await _httpClient.GetAsync(imageUrl);
                    imageResponse.EnsureSuccessStatusCode();
                    return await imageResponse.Content.ReadAsByteArrayAsync();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with DeepAI: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image using Stable Diffusion via Replicate API.
    /// Requires API key but has a free tier.
    /// </summary>
    private async Task<byte[]?> GenerateWithStableDiffusionAsync(string prompt, int width, int height)
    {
        try
        {
            // Replicate API for Stable Diffusion
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new InvalidOperationException("Replicate API key is required for Stable Diffusion. Get one at https://replicate.com/account/api-tokens");
            }

            // Use Stable Diffusion XL model
            var request = new
            {
                version = "39ed52f2a78e934b3ba6e2a89f5b1c712de7dfea535525255b1aa35c5565e08b", // SDXL model
                input = new
                {
                    prompt = prompt,
                    width = width,
                    height = height,
                    num_outputs = 1
                }
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {ApiKey}");

            // Create prediction
            var createResponse = await _httpClient.PostAsync("https://api.replicate.com/v1/predictions", content);
            createResponse.EnsureSuccessStatusCode();

            var createJson = await createResponse.Content.ReadAsStringAsync();
            var createObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(createJson);
            var predictionId = createObj.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(predictionId))
            {
                return null;
            }

            // Poll for completion
            for (int i = 0; i < 120; i++) // Max 120 attempts (about 4 minutes)
            {
                await Task.Delay(2000); // Wait 2 seconds

                var statusResponse = await _httpClient.GetAsync($"https://api.replicate.com/v1/predictions/{predictionId}");
                statusResponse.EnsureSuccessStatusCode();

                var statusJson = await statusResponse.Content.ReadAsStringAsync();
                var statusObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(statusJson);
                var status = statusObj.GetProperty("status").GetString();

                if (status == "succeeded")
                {
                    var output = statusObj.GetProperty("output");
                    if (output.ValueKind == System.Text.Json.JsonValueKind.Array && output.GetArrayLength() > 0)
                    {
                        var imageUrl = output[0].GetString();
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            var imageResponse = await _httpClient.GetAsync(imageUrl);
                            imageResponse.EnsureSuccessStatusCode();
                            return await imageResponse.Content.ReadAsByteArrayAsync();
                        }
                    }
                }
                else if (status == "failed" || status == "canceled")
                {
                    throw new Exception($"Stable Diffusion generation failed: {status}");
                }
            }

            throw new Exception("Stable Diffusion generation timed out");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with Stable Diffusion: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image using Prodia API (free tier available).
    /// </summary>
    private async Task<byte[]?> GenerateWithProdiaAsync(string prompt, int width, int height)
    {
        try
        {
            // Prodia API - requires API key but has free tier
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new InvalidOperationException("Prodia API key is required. Get one at https://prodia.com/api");
            }

            // First, create a job
            var createRequest = new
            {
                model = "dreamshaper_8.safetensors [879db523c3]",
                prompt = prompt,
                width = width,
                height = height
            };

            using var createContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Prodia-Key", ApiKey);

            var createResponse = await _httpClient.PostAsync("https://api.prodia.com/v1/sd/generate", createContent);
            createResponse.EnsureSuccessStatusCode();

            var createJson = await createResponse.Content.ReadAsStringAsync();
            var createObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(createJson);
            var jobId = createObj.GetProperty("job").GetString();

            if (string.IsNullOrEmpty(jobId))
            {
                return null;
            }

            // Poll for completion
            for (int i = 0; i < 60; i++) // Max 60 attempts (about 2 minutes)
            {
                await Task.Delay(2000); // Wait 2 seconds

                var statusResponse = await _httpClient.GetAsync($"https://api.prodia.com/v1/job/{jobId}");
                statusResponse.EnsureSuccessStatusCode();

                var statusJson = await statusResponse.Content.ReadAsStringAsync();
                var statusObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(statusJson);
                var status = statusObj.GetProperty("status").GetString();

                if (status == "succeeded")
                {
                    var imageUrl = statusObj.GetProperty("imageUrl").GetString();
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        var imageResponse = await _httpClient.GetAsync(imageUrl);
                        imageResponse.EnsureSuccessStatusCode();
                        return await imageResponse.Content.ReadAsByteArrayAsync();
                    }
                }
                else if (status == "failed")
                {
                    throw new Exception("Image generation failed on Prodia server");
                }
            }

            throw new Exception("Image generation timed out");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with Prodia: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image using Craiyon API (free tier available).
    /// </summary>
    private async Task<byte[]?> GenerateWithCraiyonAsync(string prompt)
    {
        try
        {
            // Craiyon API - free tier, simple POST request
            var request = new
            {
                prompt = prompt,
                token = "" // Empty token for free tier
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();

            var response = await _httpClient.PostAsync("https://api.craiyon.com/v3", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var obj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            
            if (obj.TryGetProperty("images", out var images) && images.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                // Get first image (base64 encoded)
                var firstImage = images[0].GetString();
                if (!string.IsNullOrEmpty(firstImage))
                {
                    // Remove data URL prefix if present
                    if (firstImage.StartsWith("data:image"))
                    {
                        var base64Index = firstImage.IndexOf(",") + 1;
                        firstImage = firstImage.Substring(base64Index);
                    }

                    return Convert.FromBase64String(firstImage);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate image with Craiyon: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates an image and saves it to a file.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate.</param>
    /// <param name="outputPath">The path where the image should be saved.</param>
    /// <param name="targetWidth">Target width for the final tile image (default: 1024).</param>
    /// <param name="targetHeight">Target height for the final tile image (default: 1024).</param>
    /// <param name="applyDiamondMask">Whether to apply a diamond mask to make the image isometric (default: true).</param>
    /// <returns>True if the image was generated and saved successfully, false otherwise.</returns>
    public async Task<bool> GenerateImageToFileAsync(string prompt, string outputPath, int targetWidth = 1024, int targetHeight = 1024, bool applyDiamondMask = true)
    {
        try
        {
            // Generate at a higher resolution for better quality, then resize to target size
            // Use at least 512x512 for generation, or 2x the target size, whichever is larger
            int genWidth = Math.Max(512, targetWidth * 2);
            int genHeight = Math.Max(512, targetHeight * 2);
            
            var imageBytes = await GenerateImageAsync(prompt, genWidth, genHeight);
            if (imageBytes != null)
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var imageStream = new MemoryStream(imageBytes);
                using var originalImage = new Bitmap(imageStream);
                
                // Resize to target tile dimensions
                var resizedImage = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(resizedImage))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    g.DrawImage(originalImage, 0, 0, targetWidth, targetHeight);
                }
                
                Bitmap finalImage = resizedImage;
                
                // Apply diamond mask if requested
                if (applyDiamondMask)
                {
                    var mask = TileMaskUtility.GetOrCreateMask(targetWidth, targetHeight);
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
                finalImage.Save(outputPath, ImageFormat.Png);
                
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
    public static string GetTilePrompt(int tileIndex, string? customPrompt = null)
    {
        if (!string.IsNullOrEmpty(customPrompt))
        {
            return customPrompt;
        }

        return tileIndex switch
        {
            0 => "Isometric grass tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape",
            1 => "Isometric dark grass tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape",
            2 => "Isometric dirt tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape, brown earth",
            3 => "Isometric dark dirt tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape",
            4 => "Isometric water tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape, blue water",
            5 => "Isometric deep water tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape, dark blue water",
            6 => "Isometric stone tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape, gray stone",
            7 => "Isometric dark stone tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape, dark gray stone",
            _ => "Isometric game tile, seamless, 16 bit, jrpg style, pixel art, isometric view, top-down view, game asset, clean background, diamond shape"
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

