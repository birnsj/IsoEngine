using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameClient.Utilities;
using System.IO;
using System.Collections.Generic;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders isometric tile maps.
/// </summary>
public class TileMapRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly IsometricTileMap _tileMap;
    private Texture2D? _tilesetTexture;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private int _tilesPerRow = 8; // Number of tiles per row in the tileset
    private Dictionary<int, Texture2D> _customTileTextures = new Dictionary<int, Texture2D>();
    private bool _hasLoggedCustomTextures = false;
    private HashSet<int> _loggedMissingTiles = new HashSet<int>();

    /// <summary>
    /// Gets the camera used for rendering.
    /// </summary>
    public Camera2D Camera => _camera;

    /// <summary>
    /// Gets the tile map being rendered.
    /// </summary>
    public IsometricTileMap TileMap => _tileMap;

    public TileMapRenderer(Camera2D camera, IsometricTileMap tileMap)
    {
        _camera = camera;
        _tileMap = tileMap;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Tile map renderer doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _tilesetTexture == null || _graphicsDevice == null)
            return;
        
        // Debug: Log custom textures count on first draw
        if (_customTileTextures.Count > 0 && !_hasLoggedCustomTextures)
        {
            var logger = ServiceLocator.Get<ILogger>();
            logger?.Info($"TileMapRenderer: {_customTileTextures.Count} custom tile textures available for rendering");
            _hasLoggedCustomTextures = true;
        }

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp); // Point sampling for pixel-perfect tiles

        // Calculate visible tile range based on camera
        var cameraWorldPos = _camera.Position;
        var viewportWorldSize = new Vector2(viewport.Width / _camera.Zoom, viewport.Height / _camera.Zoom);

        // Convert camera position to tile coordinates
        var cameraTilePos = _tileMap.ScreenToWorld(cameraWorldPos);
        var startX = Math.Max(0, (int)Math.Floor(cameraTilePos.X - viewportWorldSize.X / _tileMap.TileWidth - 2));
        var endX = Math.Min(_tileMap.Width, (int)Math.Ceiling(cameraTilePos.X + viewportWorldSize.X / _tileMap.TileWidth + 2));
        var startY = Math.Max(0, (int)Math.Floor(cameraTilePos.Y - viewportWorldSize.Y / _tileMap.TileHeight - 2));
        var endY = Math.Min(_tileMap.Height, (int)Math.Ceiling(cameraTilePos.Y + viewportWorldSize.Y / _tileMap.TileHeight + 2));

        // Render tiles in isometric order (back to front)
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var tileIndex = _tileMap.GetTile(x, y);
                if (tileIndex < 0)
                    continue;

                // Convert tile coordinates to world position
                var worldPos = new Vector2(x, y);
                var screenPos = _tileMap.WorldToScreen(worldPos);

                // CRITICAL: Tiles must be drawn at their actual pixel dimensions from the map file
                // This ensures perfect visual consistency with the editor, which also uses the
                // tile dimensions directly from the map file (TileWidth x TileHeight).
                // The camera zoom is 1.0f, so tiles appear at their true size.
                var destinationRect = new Rectangle(
                    (int)(screenPos.X - _tileMap.TileWidth / 2.0f),
                    (int)(screenPos.Y - _tileMap.TileHeight / 2.0f),
                    _tileMap.TileWidth,
                    _tileMap.TileHeight);

                // Draw custom tile texture if available, otherwise use tileset
                // CRITICAL: Custom textures are drawn at tileWidth x tileHeight to match editor scale
                // The editor pre-scales custom graphics to tile dimensions, and the game scales them
                // at render time via destinationRect. Both ensure tiles appear at the same visual size.
                if (_customTileTextures.TryGetValue(tileIndex, out var customTexture))
                {
                    // Draw the entire custom texture scaled to tile dimensions
                    // This matches the editor's behavior where custom graphics are scaled to tileWidth x tileHeight
                    if (customTexture != null && !customTexture.IsDisposed)
                    {
                        _spriteBatch.Draw(
                            customTexture,
                            destinationRect,
                            Color.White);
                    }
                    else
                    {
                        // Texture is null or disposed - log and fall through to procedural
                        if (!_loggedMissingTiles.Contains(tileIndex))
                        {
                            var logger = ServiceLocator.Get<ILogger>();
                            logger?.Error($"TileMapRenderer: Custom texture for tile {tileIndex} is null or disposed!");
                            _loggedMissingTiles.Add(tileIndex);
                        }
                    }
                }
                else if (_tilesetTexture != null)
                {
                    // Debug: Log when falling back to procedural tileset (only once per tile index)
                    if (!_loggedMissingTiles.Contains(tileIndex))
                    {
                        var logger = ServiceLocator.Get<ILogger>();
                        logger?.Warning($"TileMapRenderer: No custom texture for tile index {tileIndex}, using procedural tileset. Custom textures available: {_customTileTextures.Count}");
                        _loggedMissingTiles.Add(tileIndex);
                    }
                    
                    // Calculate source rectangle in tileset
                    var tileX = tileIndex % _tilesPerRow;
                    var tileY = tileIndex / _tilesPerRow;
                    var sourceRect = new Rectangle(
                        tileX * _tileMap.TileWidth,
                        tileY * _tileMap.TileHeight,
                        _tileMap.TileWidth,
                        _tileMap.TileHeight);

                    // Draw the tile with explicit destination rectangle to ensure correct size
                    _spriteBatch.Draw(
                        _tilesetTexture,
                        destinationRect,
                        sourceRect,
                        Color.White);
                }
            }
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// Should be called from LoadContent.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a simple procedural tileset texture
        CreateProceduralTileset();
    }

    /// <summary>
    /// Loads custom tile graphics from the provided dictionary.
    /// </summary>
    public void LoadCustomTileGraphics(Dictionary<int, string>? tileGraphics)
    {
        var logger = ServiceLocator.Get<ILogger>();
        
        if (_graphicsDevice == null)
        {
            logger?.Warning("LoadCustomTileGraphics: _graphicsDevice is null!");
            return;
        }
        
        if (tileGraphics == null)
        {
            logger?.Warning("LoadCustomTileGraphics: tileGraphics dictionary is null!");
            return;
        }

        // Dispose existing custom textures
        foreach (var texture in _customTileTextures.Values)
        {
            texture?.Dispose();
        }
        _customTileTextures.Clear();
        _loggedMissingTiles.Clear(); // Reset logged missing tiles when reloading

        var loadedCount = 0;
        var failedCount = 0;

        logger?.Info($"LoadCustomTileGraphics: Attempting to load {tileGraphics.Count} tile graphics");

        // Load each custom tile graphic
        foreach (var kvp in tileGraphics)
        {
            var tileIndex = kvp.Key;
            var graphicPath = kvp.Value;

            try
            {
                var texture = LoadTileTexture(graphicPath);
                if (texture != null)
                {
                    _customTileTextures[tileIndex] = texture;
                    loadedCount++;
                    logger?.Info($"Loaded tile {tileIndex} from: {graphicPath}");
                }
                else
                {
                    failedCount++;
                    logger?.Warning($"Failed to load tile {tileIndex} from: {graphicPath} (file not found or invalid)");
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                logger?.Error($"Error loading tile {tileIndex} from {graphicPath}: {ex.Message}");
            }
        }

        logger?.Info($"Tile graphics loaded: {loadedCount} successful, {failedCount} failed out of {tileGraphics.Count} total");
        
        if (loadedCount == 0 && tileGraphics.Count > 0)
        {
            logger?.Error($"CRITICAL: No tile graphics were loaded! All {tileGraphics.Count} attempts failed. Check file paths and GameContent directory.");
        }
    }

    /// <summary>
    /// Loads a texture from a file path, resolving relative paths from GameContent.
    /// 
    /// NOTE: The texture is loaded at its original size. When drawn, it will be scaled to
    /// tileWidth x tileHeight via the destinationRect. This matches the editor's behavior
    /// where custom graphics are pre-scaled to tile dimensions using high-quality interpolation.
    /// The game uses PointClamp sampling for pixel-perfect rendering, which may differ slightly
    /// from the editor's bicubic interpolation, but both ensure tiles appear at the correct size.
    /// </summary>
    private Texture2D? LoadTileTexture(string graphicPath)
    {
        if (_graphicsDevice == null || string.IsNullOrEmpty(graphicPath))
            return null;

        try
        {
            string? fullPath = null;

            // Normalize path separators to the current OS format
            var normalizedPath = graphicPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // Try absolute path first
            if (Path.IsPathRooted(normalizedPath) && File.Exists(normalizedPath))
            {
                fullPath = normalizedPath;
            }
            else
            {
                // Try relative to GameContent directory
                var gameContentPath = GameContentPathHelper.GetGameContentPath();
                if (gameContentPath != null)
                {
                    // Remove "GameContent/" or "GameContent\" prefix if present
                    var relativePath = normalizedPath;
                    if (relativePath.StartsWith("GameContent" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring("GameContent".Length + 1);
                    }
                    
                    var candidatePath = Path.Combine(gameContentPath, relativePath);
                    var normalizedCandidate = Path.GetFullPath(candidatePath); // Normalize the path
                    
                    if (File.Exists(normalizedCandidate))
                    {
                        fullPath = normalizedCandidate;
                    }
                }

                // If still not found, try as-is relative to current directory
                if (fullPath == null && File.Exists(normalizedPath))
                {
                    fullPath = Path.GetFullPath(normalizedPath);
                }
            }

            if (fullPath == null || !File.Exists(fullPath))
            {
                var logger = ServiceLocator.Get<ILogger>();
                logger?.Warning($"Tile texture not found: {graphicPath} (tried: {fullPath ?? "multiple paths"})");
                return null;
            }

            // Load texture from file using FileStream
            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                return Texture2D.FromStream(_graphicsDevice, fileStream);
            }
        }
        catch (Exception ex)
        {
            var logger = ServiceLocator.Get<ILogger>();
            logger?.Error($"Error loading texture from {graphicPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a simple procedural tileset for testing.
    /// In a real game, this would load from a content file.
    /// </summary>
    private void CreateProceduralTileset()
    {
        if (_graphicsDevice == null)
            return;

        // Dispose existing texture if it exists
        if (_tilesetTexture != null)
        {
            _tilesetTexture.Dispose();
            _tilesetTexture = null;
        }

        // Use tile sizes from the loaded map to ensure they match what the editor set
        var tileWidth = _tileMap.TileWidth;
        var tileHeight = _tileMap.TileHeight;
        var tilesPerRow = _tilesPerRow;
        var numTiles = 8; // Create 8 different tile types

        // Create texture large enough for all tiles using the map's tile dimensions
        var textureWidth = tilesPerRow * tileWidth;
        var textureHeight = ((numTiles + tilesPerRow - 1) / tilesPerRow) * tileHeight;

        _tilesetTexture = new Texture2D(_graphicsDevice, textureWidth, textureHeight);

        // Create color data
        var colors = new Color[textureWidth * textureHeight];

        // CRITICAL: Tile colors must match exactly with GameEditor/Utilities/TileImageGenerator.cs
        // If you change these colors, you must update the editor's GenerateTileImage() method
        // to ensure visual consistency between editor and game.
        var tileColors = new[]
        {
            new Color(100, 150, 100), // 0: Grass (light green)
            new Color(80, 120, 80),   // 1: Dark grass
            new Color(150, 120, 100), // 2: Dirt (brown)
            new Color(120, 100, 80), // 3: Dark dirt
            new Color(100, 100, 150), // 4: Water (blue)
            new Color(80, 80, 120),   // 5: Deep water
            new Color(120, 120, 120), // 6: Stone (gray)
            new Color(100, 100, 100), // 7: Dark stone
        };

        // Fill tileset with colored tiles
        for (int tileIndex = 0; tileIndex < numTiles; tileIndex++)
        {
            var tileX = tileIndex % tilesPerRow;
            var tileY = tileIndex / tilesPerRow;
            var baseColor = tileColors[tileIndex % tileColors.Length];

            // Create isometric diamond shape for each tile
            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    var pixelX = tileX * tileWidth + x;
                    var pixelY = tileY * tileHeight + y;

                    // Check if pixel is within diamond shape
                    var centerX = tileWidth / 2.0f;
                    var centerY = tileHeight / 2.0f;
                    var dx = Math.Abs(x - centerX);
                    var dy = Math.Abs(y - centerY);

                    // Diamond shape: |dx|/w + |dy|/h <= 1
                    if (dx / centerX + dy / centerY <= 1.0f)
                    {
                        // Add some variation for depth
                        var variation = (float)(Math.Sin(x * 0.3f) * Math.Cos(y * 0.3f) * 0.1f);
                        var color = new Color(
                            (byte)Math.Clamp(baseColor.R + variation * 255, 0, 255),
                            (byte)Math.Clamp(baseColor.G + variation * 255, 0, 255),
                            (byte)Math.Clamp(baseColor.B + variation * 255, 0, 255));

                        colors[pixelY * textureWidth + pixelX] = color;
                    }
                    else
                    {
                        colors[pixelY * textureWidth + pixelX] = Color.Transparent;
                    }
                }
            }
        }

        _tilesetTexture.SetData(colors);
    }

    /// <summary>
    /// Regenerates the tileset texture using the current tile map's tile dimensions.
    /// This ensures the tileset matches the tile sizes set in the editor.
    /// </summary>
    public void RegenerateTileset()
    {
        CreateProceduralTileset();
    }
}

