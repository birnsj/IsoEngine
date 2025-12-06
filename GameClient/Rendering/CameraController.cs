using GameCore.Entities;
using GameCore.Input;
using GameCore.Rendering;
using GameCore.Services;
using GameCore.State;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that handles camera movement and zoom based on input.
/// </summary>
public class CameraController : IGameService
{
    private readonly Camera2D _camera;
    private GraphicsDevice? _graphicsDevice;
    private const float CameraMoveSpeed = 200.0f; // Pixels per second (for manual scrolling)
    private const float ZoomSpeed = 0.1f; // Zoom change per scroll unit
    private const float CameraFollowSmoothing = 8.0f; // Smoothing factor for camera movement (higher = smoother)
    
    private bool _isFollowingPlayer = true; // Start with camera following player
    private bool _isPanning = false; // Track if we're panning with middle mouse button
    private Vector2 _lastMousePos; // Last mouse position for incremental panning (like editor)

    public CameraController(Camera2D camera)
    {
        _camera = camera;
    }
    
    /// <summary>
    /// Sets the graphics device for viewport access.
    /// </summary>
    public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void Initialize()
    {
        // Camera controller doesn't need special initialization
    }

    public void Update(GameTime gameTime)
    {
        var inputService = ServiceLocator.Get<IInputService>();
        if (inputService == null)
            return;

        var timeManager = ServiceLocator.Get<GameTimeManager>();
        var deltaTime = timeManager?.DeltaTime ?? (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Handle middle mouse button panning (works in all states)
        // Use same incremental approach as editor for smooth panning
        if (inputService.IsMiddleMouseButtonDown)
        {
            if (!_isPanning)
            {
                // Start panning - store initial mouse position
                _isPanning = true;
                _lastMousePos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
                _isFollowingPlayer = false; // Stop following player when panning
            }
            else
            {
                // Continue panning - calculate incremental delta (like editor)
                var currentMousePos = new Vector2(inputService.MousePosition.X, inputService.MousePosition.Y);
                var mouseDelta = currentMousePos - _lastMousePos;
                
                // Convert screen delta to world delta
                if (_graphicsDevice != null)
                {
                    // Convert both positions to world space and calculate delta
                    var lastWorldPos = _camera.ScreenToWorld(_lastMousePos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
                    var currentWorldPos = _camera.ScreenToWorld(currentMousePos, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
                    var worldDelta = lastWorldPos - currentWorldPos;
                    
                    // Apply incremental world delta to camera position
                    _camera.Position += worldDelta;
                }
                else
                {
                    // Fallback: use simple screen delta conversion
                    var panDelta = new Vector2(-mouseDelta.X, -mouseDelta.Y) / _camera.Zoom;
                    _camera.Position += panDelta;
                }
                
                // Update last mouse position for next frame
                _lastMousePos = currentMousePos;
            }
        }
        else
        {
            // Stop panning when middle mouse button is released
            if (_isPanning)
            {
                _isPanning = false;
            }
        }

        // Check if we're in the game screen
        var gameStateService = ServiceLocator.Get<GameStateService>();
        bool isInGame = gameStateService?.CurrentState == GameState.GameScreen;

        if (isInGame)
        {
            var player = ServiceLocator.Get<Player>();
            bool isPlayerMoving = player != null && player.Velocity.LengthSquared() > 0.1f;
            
            // Check for Space key to return camera to player
            if (inputService.IsActionPressed(GameAction.Jump))
            {
                _isFollowingPlayer = true; // Enable following to return to player
            }
            
            // Check for WASD/Arrow keys to manually scroll camera (always available when not panning)
            if (!_isPanning)
            {
                var movement = inputService.GetMovementVector();
                if (movement.LengthSquared() > 0)
                {
                    // User is manually scrolling - stop following player
                    _isFollowingPlayer = false;
                    var moveSpeed = CameraMoveSpeed / _camera.Zoom; // Adjust speed based on zoom
                    _camera.Position += movement * moveSpeed * deltaTime;
                }
            }
            
            // Always follow player smoothly when walking (unless panning or manually scrolling)
            // Camera automatically follows when player is moving, and respects manual override when not moving
            bool shouldFollowPlayer = (!_isPanning && player != null && _isFollowingPlayer) && 
                                      (isPlayerMoving || _isFollowingPlayer);
            
            // Follow player smoothly with improved interpolation (only if not panning)
            if (shouldFollowPlayer && player != null)
            {
                var targetPosition = player.Position;
                var currentPosition = _camera.Position;
                
                // Calculate distance to target
                var distance = Vector2.Distance(currentPosition, targetPosition);
                
                // Use smooth lerp with adaptive speed based on distance and player movement
                // When player is moving, camera follows more closely for smoother scrolling
                float smoothingFactor = CameraFollowSmoothing;
                
                if (isPlayerMoving)
                {
                    // When player is walking, follow more closely for smoother scrolling
                    smoothingFactor = CameraFollowSmoothing * 1.2f;
                    
                    // If camera is far behind, catch up faster
                    if (distance > 30.0f)
                    {
                        smoothingFactor *= 1.5f;
                    }
                }
                else
                {
                    // When player is stationary, use normal smoothing
                    if (distance > 100.0f)
                    {
                        smoothingFactor = CameraFollowSmoothing * 2.0f; // Catch up faster if far away
                    }
                }
                
                // Use smooth interpolation for camera following
                // Frame-rate independent smooth following
                var followAmount = 1.0f - MathF.Pow(1.0f - (smoothingFactor / 100.0f), deltaTime * 60.0f);
                followAmount = Math.Clamp(followAmount, 0.0f, 1.0f);
                
                _camera.Position = Vector2.Lerp(currentPosition, targetPosition, followAmount);
            }
        }

        // Zoom with mouse wheel (always available)
        var wheelDelta = inputService.MouseWheelDelta;
        if (wheelDelta != 0)
        {
            var zoomChange = wheelDelta > 0 ? ZoomSpeed : -ZoomSpeed;
            _camera.Zoom = Math.Clamp(_camera.Zoom + zoomChange, 0.25f, 3.0f);
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Camera controller doesn't need rendering
    }
}

