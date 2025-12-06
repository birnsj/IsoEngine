using GameCore.Rendering;
using GameCore.Services;
using GameCore.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameClient.Editor;

/// <summary>
/// Service for in-game editing mode.
/// </summary>
public class InGameEditorService : IGameService
{
    private bool _isEditorMode = false;
    private IsometricTileMap? _tileMap;
    private int _selectedTileIndex = 0;
    private Camera2D? _camera;
    private GraphicsDevice? _graphicsDevice;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;

    public bool IsEditorMode
    {
        get => _isEditorMode;
        set
        {
            _isEditorMode = value;
            if (!_isEditorMode)
            {
                // Save changes when exiting editor mode
                SaveTileMap();
            }
        }
    }

    public void SetTileMap(IsometricTileMap tileMap)
    {
        _tileMap = tileMap;
    }

    public void SetCamera(Camera2D camera)
    {
        _camera = camera;
    }

    public void SetGraphicsDevice(GraphicsDevice device)
    {
        _graphicsDevice = device;
    }

    public void SetSpriteBatch(SpriteBatch spriteBatch)
    {
        _spriteBatch = spriteBatch;
    }

    public void SetFont(SpriteFont font)
    {
        _font = font;
    }

    public void Initialize()
    {
        // No initialization needed
    }

    public void Update(GameTime gameTime)
    {
        if (!_isEditorMode || _tileMap == null || _camera == null)
            return;

        var keyboardState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // Toggle editor mode with F12
        if (keyboardState.IsKeyDown(Keys.F12) && !keyboardState.IsKeyDown(Keys.LeftShift))
        {
            IsEditorMode = false;
            return;
        }

        // Tile selection with number keys (0-7)
        for (int i = 0; i <= 7; i++)
        {
            if (keyboardState.IsKeyDown((Keys)((int)Keys.D0 + i)))
            {
                _selectedTileIndex = i;
            }
        }

        // Place tile with left mouse button
        if (mouseState.LeftButton == ButtonState.Pressed && _graphicsDevice != null)
        {
            var mousePos = new Vector2(mouseState.X, mouseState.Y);
            var worldPos = _camera.ScreenToWorld(mousePos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            var tilePos = _tileMap.ScreenToWorld(worldPos);
            var tileX = (int)Math.Floor(tilePos.X);
            var tileY = (int)Math.Floor(tilePos.Y);

            if (tileX >= 0 && tileX < _tileMap.Width && tileY >= 0 && tileY < _tileMap.Height)
            {
                _tileMap.SetTile(tileX, tileY, _selectedTileIndex);
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (!_isEditorMode || _spriteBatch == null || _tileMap == null)
            return;

        _spriteBatch.Begin();

        // Draw editor UI overlay (if font is available)
        if (_font != null)
        {
            var editorText = $"EDITOR MODE - Selected Tile: {_selectedTileIndex}";
            _spriteBatch.DrawString(_font, editorText, new Vector2(10, 10), Color.Yellow);
            _spriteBatch.DrawString(_font, "Press F12 to exit editor mode", new Vector2(10, 30), Color.Yellow);
            _spriteBatch.DrawString(_font, "Press 0-7 to select tile", new Vector2(10, 50), Color.Yellow);
            _spriteBatch.DrawString(_font, "Left click to place tile", new Vector2(10, 70), Color.Yellow);
        }

        _spriteBatch.End();
    }

    private void SaveTileMap()
    {
        if (_tileMap == null)
            return;

        try
        {
            // Save tile map using GameEditor serializer (if available at runtime)
            // For now, just log that save was attempted
            var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
            logger?.Info("Tile map changes made in editor mode. Use the standalone editor to save changes.");
            
            // Note: In a production system, you might want to use reflection or a shared library
            // to access the editor's serialization, or implement serialization directly here
        }
        catch (Exception ex)
        {
            // Log error if logger is available
            var logger = GameCore.Services.ServiceLocator.Get<GameCore.Services.ILogger>();
            logger?.Error($"Error saving tile map in editor: {ex.Message}");
        }
    }
}

