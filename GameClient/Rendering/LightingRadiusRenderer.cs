using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Service that renders debug visualization of lighting radii for lights.
/// </summary>
public class LightingRadiusRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly LightingRenderer _lightingRenderer;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _circleTexture;
    private bool _isVisible = false;

    /// <summary>
    /// Gets or sets whether the lighting radius visualization is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public LightingRadiusRenderer(Camera2D camera, LightingRenderer lightingRenderer)
    {
        _camera = camera;
        _lightingRenderer = lightingRenderer;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Lighting radius renderer doesn't need per-frame updates
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

        // Draw lighting radius circles for all lights
        foreach (var light in _lightingRenderer.Lights)
        {
            var radius = light.Radius;
            var position = light.Position;
            
            // Use the light's color for the circle outline, with transparency
            var circleColor = new Color(light.Color.R, light.Color.G, light.Color.B, (byte)150); // Semi-transparent
            
            // Draw circle outline
            DrawCircle(_spriteBatch, position, radius, circleColor);
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

            // Draw line segment
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

