using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders projectiles (bullets) on the screen.
/// </summary>
public class ProjectileRenderer : IGameService
{
    private readonly Camera2D _camera;
    private readonly ProjectileService _projectileService;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _projectileTexture;

    public ProjectileRenderer(Camera2D camera, ProjectileService projectileService)
    {
        _camera = camera;
        _projectileService = projectileService;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Projectile renderer doesn't need per-frame updates
    }

    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a simple circular texture for projectiles
        _projectileTexture = CreateProjectileTexture(graphicsDevice, 8);
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _projectileTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp);

        // Draw all active projectiles
        foreach (var projectile in _projectileService.GetActiveProjectiles())
        {
            if (projectile.IsExpired)
                continue;

            var projectileRect = new Rectangle(
                (int)(projectile.Position.X - projectile.Size),
                (int)(projectile.Position.Y - projectile.Size),
                (int)(projectile.Size * 2),
                (int)(projectile.Size * 2));

            // Draw projectile as a bright yellow/orange circle
            _spriteBatch.Draw(_projectileTexture, projectileRect, Color.Yellow);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Creates a simple circular texture for projectiles.
    /// </summary>
    private Texture2D CreateProjectileTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        var center = size / 2.0f;
        var radius = size / 2.0f - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= radius)
                {
                    // Bright yellow/orange for visibility
                    data[y * size + x] = Color.Yellow;
                }
                else
                {
                    data[y * size + x] = Color.Transparent;
                }
            }
        }

        texture.SetData(data);
        return texture;
    }
}

