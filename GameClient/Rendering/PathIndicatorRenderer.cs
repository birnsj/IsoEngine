using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders path indicators showing movement preview for entities.
/// </summary>
public class PathIndicatorRenderer : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _circleTexture;
    private List<Vector2>? _currentPath;
    private Vector2? _pathStart;

    public PathIndicatorRenderer(Camera2D camera)
    {
        _camera = camera;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Path indicator doesn't need per-frame updates
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _circleTexture == null)
            return;

        if (_currentPath == null || _currentPath.Count == 0)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        DrawPath();

        _spriteBatch.End();
    }

    /// <summary>
    /// Sets the path to display.
    /// </summary>
    public void SetPath(Vector2 start, List<Vector2> path)
    {
        _pathStart = start;
        _currentPath = path;
    }

    /// <summary>
    /// Clears the current path.
    /// </summary>
    public void ClearPath()
    {
        _currentPath = null;
        _pathStart = null;
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create circle texture for path dots
        _circleTexture = CreateCircleTexture(graphicsDevice, 16);
    }

    /// <summary>
    /// Draws the path as a series of connected dots.
    /// </summary>
    private void DrawPath()
    {
        if (_spriteBatch == null || _circleTexture == null || _currentPath == null || _currentPath.Count == 0)
            return;

        var pathPoints = new List<Vector2>();
        if (_pathStart.HasValue)
        {
            pathPoints.Add(_pathStart.Value);
        }
        pathPoints.AddRange(_currentPath);

        // Draw path as line segments with dots
        for (int i = 0; i < pathPoints.Count; i++)
        {
            var point = pathPoints[i];

            // Calculate fade based on distance from start
            var totalDistance = 0.0f;
            for (int j = 1; j <= i; j++)
            {
                totalDistance += Vector2.Distance(pathPoints[j - 1], pathPoints[j]);
            }

            // Fade out along the path (more distant = more transparent)
            var maxDistance = 200.0f; // Maximum distance to show
            var alpha = Math.Max(0.3f, 1.0f - (totalDistance / maxDistance));

            var color = new Color((byte)100, (byte)150, (byte)255, (byte)(255 * alpha));
            var size = 4.0f * alpha; // Smaller dots further away

            var destRect = new Rectangle(
                (int)(point.X - size / 2),
                (int)(point.Y - size / 2),
                (int)size,
                (int)size);

            var sourceRect = new Rectangle(0, 0, _circleTexture.Width, _circleTexture.Height);
            _spriteBatch.Draw(_circleTexture, destRect, sourceRect, color);

            // Draw line to next point
            if (i < pathPoints.Count - 1)
            {
                var nextPoint = pathPoints[i + 1];
                DrawLine(point, nextPoint, color, 2.0f * alpha);
            }
        }
    }

    /// <summary>
    /// Draws a line between two points.
    /// </summary>
    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        if (_spriteBatch == null || _circleTexture == null)
            return;

        var direction = end - start;
        var length = direction.Length();
        if (length < 0.1f)
            return;

        direction.Normalize();
        var angle = MathF.Atan2(direction.Y, direction.X);

        _spriteBatch.Draw(
            _circleTexture,
            start,
            null,
            color,
            angle,
            new Vector2(0, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0.0f);
    }

    /// <summary>
    /// Creates a circular texture for path dots.
    /// </summary>
    private Texture2D CreateCircleTexture(GraphicsDevice device, int size)
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
                var distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance <= radius)
                {
                    var edgeDistance = radius - distance;
                    var alpha = Math.Min(1.0f, edgeDistance / 2.0f);
                    data[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
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

