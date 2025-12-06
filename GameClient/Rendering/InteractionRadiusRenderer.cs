using GameCore.Entities;
using GameCore.Interactions;
using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders debug visualization of interaction radii for interactable entities.
/// </summary>
public class InteractionRadiusRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly InteractionService _interactionService;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _circleTexture;
    private bool _isVisible = false;

    /// <summary>
    /// Gets or sets whether the interaction radius visualization is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public InteractionRadiusRenderer(Camera2D camera, InteractionService interactionService)
    {
        _camera = camera;
        _interactionService = interactionService;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Interaction radius renderer doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        if (!_isVisible || _spriteBatch == null || _graphicsDevice == null || _circleTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        // Draw interaction radius circles for all interactable entities
        foreach (var interactable in _interactionService.GetAllInteractables())
        {
            if (interactable is not Entity entity)
                continue;

            var interactionRadius = interactable.InteractionRadius > 0 
                ? interactable.InteractionRadius 
                : 50.0f; // Default radius (matches InteractionService default)

            // Draw circle at entity position
            var radius = interactionRadius;
            var diameter = (int)(radius * 2);
            var position = entity.Position;
            
            // Draw circle outline
            DrawCircle(_spriteBatch, position, radius, new Color(0, 255, 255, 100)); // Cyan, semi-transparent
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Draws a circle outline at the specified position.
    /// </summary>
    private void DrawCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        var segments = 32; // Number of segments for the circle
        var angleStep = MathHelper.TwoPi / segments;

        for (int i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var point1 = center + new Vector2(
                (float)Math.Cos(angle1) * radius,
                (float)Math.Sin(angle1) * radius);
            var point2 = center + new Vector2(
                (float)Math.Cos(angle2) * radius,
                (float)Math.Sin(angle2) * radius);

            // Draw line segment (we'll use a simple approach with the circle texture)
            var direction = point2 - point1;
            var length = direction.Length();
            if (length > 0)
            {
                var rotation = (float)Math.Atan2(direction.Y, direction.X);
                spriteBatch.Draw(
                    _circleTexture,
                    point1,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 2), // Thin line
                    SpriteEffects.None,
                    0f);
            }
        }
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
        _circleTexture = new Texture2D(graphicsDevice, 1, 1);
        _circleTexture.SetData(new[] { Color.White });
    }
}

