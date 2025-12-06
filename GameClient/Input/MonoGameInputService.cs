using GameCore.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GameClient.Input;

/// <summary>
/// MonoGame implementation of the input service.
/// Handles keyboard and mouse input with proper state tracking for pressed vs held transitions.
/// </summary>
public class MonoGameInputService : IInputService
{
    private KeyboardState _currentKeyboardState;
    private KeyboardState _previousKeyboardState;
    private MouseState _currentMouseState;
    private MouseState _previousMouseState;

    public Point MousePosition => _currentMouseState.Position;

    public bool IsLeftClickPressed { get; private set; }
    public bool IsLeftMouseButtonDown => _currentMouseState.LeftButton == ButtonState.Pressed;
    public bool IsRightClickPressed { get; private set; }
    public bool IsMiddleMouseButtonDown => _currentMouseState.MiddleButton == ButtonState.Pressed;
    public int MouseWheelDelta { get; private set; }

    public void Initialize()
    {
        _currentKeyboardState = Keyboard.GetState();
        _previousKeyboardState = _currentKeyboardState;
        _currentMouseState = Mouse.GetState();
        _previousMouseState = _currentMouseState;
    }

    public void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Store previous states
        _previousKeyboardState = _currentKeyboardState;
        _previousMouseState = _currentMouseState;

        // Get current states
        _currentKeyboardState = Keyboard.GetState();
        _currentMouseState = Mouse.GetState();

        // Update mouse button pressed states (transition detection)
        IsLeftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed &&
                            _previousMouseState.LeftButton == ButtonState.Released;

        IsRightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed &&
                             _previousMouseState.RightButton == ButtonState.Released;

        // Update mouse wheel delta
        MouseWheelDelta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
    }

    public void Draw(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Input service doesn't need rendering
    }

    public Vector2 GetMovementVector()
    {
        var movement = Vector2.Zero;

        // Check movement actions
        if (IsActionDown(GameAction.MoveUp))
            movement.Y -= 1.0f;
        if (IsActionDown(GameAction.MoveDown))
            movement.Y += 1.0f;
        if (IsActionDown(GameAction.MoveLeft))
            movement.X -= 1.0f;
        if (IsActionDown(GameAction.MoveRight))
            movement.X += 1.0f;

        // Normalize diagonal movement
        if (movement.LengthSquared() > 0)
        {
            movement.Normalize();
        }

        return movement;
    }

    public bool IsActionDown(GameAction action)
    {
        if (!InputConfiguration.ActionKeys.TryGetValue(action, out var keys))
            return false;

        // Check if any of the mapped keys are currently held down
        foreach (var key in keys)
        {
            if (_currentKeyboardState.IsKeyDown(key))
                return true;
        }

        return false;
    }

    public bool IsActionPressed(GameAction action)
    {
        if (!InputConfiguration.ActionKeys.TryGetValue(action, out var keys))
            return false;

        // Check if any of the mapped keys transitioned from not pressed to pressed
        foreach (var key in keys)
        {
            if (_currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key))
                return true;
        }

        return false;
    }
}

