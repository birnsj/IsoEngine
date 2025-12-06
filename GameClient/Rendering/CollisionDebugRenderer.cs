using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders debug visualization of collision tiles.
/// </summary>
public class CollisionDebugRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly IsometricTileMap _tileMap;
    private readonly CollisionService _collisionService;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _debugTexture;
    private Texture2D? _diamondTexture;

    public CollisionDebugRenderer(Camera2D camera, IsometricTileMap tileMap, CollisionService collisionService)
    {
        _camera = camera;
        _tileMap = tileMap;
        _collisionService = collisionService;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Debug renderer doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || (_debugTexture == null && _diamondTexture == null))
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        // Draw collision grid cells
        var collisionGrid = _collisionService.GetCollisionGrid();
        if (collisionGrid != null)
        {
            // Draw each solid cell in the collision grid
            for (int gridY = 0; gridY < collisionGrid.GridHeight; gridY++)
            {
                for (int gridX = 0; gridX < collisionGrid.GridWidth; gridX++)
                {
                    if (collisionGrid.IsCellSolid(gridX, gridY))
                    {
                        // Get world position of cell center
                        var worldPos = collisionGrid.GridToWorld(gridX, gridY);
                        // Convert to screen coordinates
                        var screenPos = _tileMap.WorldToScreen(worldPos);
                        
                        // Draw a small rectangle at the cell position
                        var cellSize = collisionGrid.CellSize;
                        var screenSize = _tileMap.WorldToScreen(new Vector2(cellSize, cellSize));
                        var sizeX = Math.Abs(screenSize.X);
                        var sizeY = Math.Abs(screenSize.Y);
                        
                        _spriteBatch.Draw(_debugTexture, 
                            new Rectangle(
                                (int)(screenPos.X - sizeX / 2),
                                (int)(screenPos.Y - sizeY / 2),
                                (int)sizeX,
                                (int)sizeY),
                            new Color(255, 0, 0, 128));
                    }
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

        // Create a simple 1x1 white texture for fallback drawing
        _debugTexture = new Texture2D(graphicsDevice, 1, 1);
        _debugTexture.SetData(new[] { Color.White });

        // Generate a diamond-shaped texture for isometric collision visualization
        // Use the exact tile dimensions to match the tile shape perfectly
        GenerateDiamondTexture(graphicsDevice, _tileMap.TileWidth, _tileMap.TileHeight);
    }

    /// <summary>
    /// Generates a diamond-shaped texture for isometric collision visualization.
    /// Uses the exact tile dimensions to match the tile shape perfectly.
    /// </summary>
    private void GenerateDiamondTexture(GraphicsDevice graphicsDevice, int tileWidth, int tileHeight)
    {
        // Create texture data for diamond shape matching tile dimensions exactly
        var colors = new Color[tileWidth * tileHeight];
        var centerX = tileWidth / 2.0f;
        var centerY = tileHeight / 2.0f;

        for (int y = 0; y < tileHeight; y++)
        {
            for (int x = 0; x < tileWidth; x++)
            {
                var dx = Math.Abs(x - centerX);
                var dy = Math.Abs(y - centerY);

                // Diamond shape: |dx|/centerX + |dy|/centerY <= 1 (matches tile rendering exactly)
                if ((dx / centerX) + (dy / centerY) <= 1.0f)
                {
                    colors[y * tileWidth + x] = Color.White; // Will be tinted red when drawn
                }
                else
                {
                    colors[y * tileWidth + x] = Color.Transparent;
                }
            }
        }

        // Create texture from the diamond shape data
        _diamondTexture = new Texture2D(graphicsDevice, tileWidth, tileHeight);
        _diamondTexture.SetData(colors);
    }
}

