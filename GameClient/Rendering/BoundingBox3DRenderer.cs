using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Collision;
using GameCore.Entities;
using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders wireframe 3D bounding boxes for debugging.
/// </summary>
public class BoundingBox3DRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly IsometricTileMap? _tileMap;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _pixelTexture;
    private bool _isEnabled = false;

    /// <summary>
    /// Gets or sets whether the debug renderer is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public BoundingBox3DRenderer(Camera2D camera, IsometricTileMap? tileMap = null)
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
        // No updates needed
    }

    public void Draw(GameTime gameTime)
    {
        if (!_isEnabled || _spriteBatch == null || _graphicsDevice == null || _pixelTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp);

        // Get all entities from EntityRenderer
        var entityRenderer = ServiceLocator.Get<EntityRenderer>();
        if (entityRenderer != null)
        {
            // Note: EntityRenderer doesn't expose entities list, so we'll need to get entities another way
            // For now, we'll draw walls from tile map if available
        }

        // Draw walls' 3D bounding boxes if tile map is available
        // TODO: GetAllWalls() method not yet implemented in IsometricTileMap
        // if (_tileMap != null)
        // {
        //     var walls = _tileMap.GetAllWalls();
        //     foreach (var wallEntry in walls)
        //     {
        //         var tilePos = wallEntry.Key;
        //         var wallData = wallEntry.Value;
        //
        //         // Convert tile position to world position
        //         var worldPos = new Vector2(tilePos.X + 0.5f, tilePos.Y + 0.5f);
        //         var screenPos = _tileMap.WorldToScreen(worldPos);
        //
        //         // Create wall bounding box
        //         var wallBox = new BoundingBox3D(
        //             worldPos.X - 0.5f,
        //             worldPos.Y - 0.5f,
        //             0.0f,
        //             1.0f,
        //             1.0f,
        //             wallData.Depth);
        //
        //         DrawWireframeBox(wallBox, screenPos, Color.Magenta);
        //     }
        // }

        _spriteBatch.End();
    }

    /// <summary>
    /// Draws a wireframe 3D bounding box.
    /// </summary>
    private void DrawWireframeBox(BoundingBox3D box, Vector2 screenPos, Color color)
    {
        if (_spriteBatch == null || _pixelTexture == null || _tileMap == null)
            return;

        // For isometric view, we'll draw the box as a 2D projection
        // Draw the base rectangle (ground level)
        var baseLeft = _tileMap.WorldToScreen(new Vector2(box.Left, box.Top));
        var baseRight = _tileMap.WorldToScreen(new Vector2(box.Right, box.Top));
        var baseBottomLeft = _tileMap.WorldToScreen(new Vector2(box.Left, box.Bottom));
        var baseBottomRight = _tileMap.WorldToScreen(new Vector2(box.Right, box.Bottom));

        // Draw base rectangle edges
        DrawLine(baseLeft, baseRight, color);
        DrawLine(baseRight, baseBottomRight, color);
        DrawLine(baseBottomRight, baseBottomLeft, color);
        DrawLine(baseBottomLeft, baseLeft, color);

        // Draw top rectangle (at height)
        var topLeft = _tileMap.WorldToScreen(new Vector2(box.Left, box.Top));
        topLeft.Y -= box.Back * 0.5f; // Adjust for height in isometric
        var topRight = _tileMap.WorldToScreen(new Vector2(box.Right, box.Top));
        topRight.Y -= box.Back * 0.5f;
        var topBottomLeft = _tileMap.WorldToScreen(new Vector2(box.Left, box.Bottom));
        topBottomLeft.Y -= box.Back * 0.5f;
        var topBottomRight = _tileMap.WorldToScreen(new Vector2(box.Right, box.Bottom));
        topBottomRight.Y -= box.Back * 0.5f;

        // Draw top rectangle edges
        DrawLine(topLeft, topRight, color);
        DrawLine(topRight, topBottomRight, color);
        DrawLine(topBottomRight, topBottomLeft, color);
        DrawLine(topBottomLeft, topLeft, color);

        // Draw vertical edges connecting base to top
        DrawLine(baseLeft, topLeft, color);
        DrawLine(baseRight, topRight, color);
        DrawLine(baseBottomLeft, topBottomLeft, color);
        DrawLine(baseBottomRight, topBottomRight, color);
    }

    /// <summary>
    /// Draws a line between two points.
    /// </summary>
    private void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        var distance = Vector2.Distance(start, end);
        var angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);

        _spriteBatch.Draw(
            _pixelTexture,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(distance, 1.0f),
            SpriteEffects.None,
            0.0f);
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// Should be called from LoadContent.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a simple 1x1 white texture for drawing lines
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }
}

