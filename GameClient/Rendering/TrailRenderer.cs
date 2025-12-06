using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders trails for fast-moving entities and projectiles.
/// </summary>
public class TrailRenderer : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _pixelTexture;
    private readonly Dictionary<object, Trail> _trails = new();

    public TrailRenderer(Camera2D camera)
    {
        _camera = camera;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update all trails
        foreach (var trail in _trails.Values)
        {
            trail.Update(deltaTime);
        }

        // Remove expired trails
        var expiredTrails = _trails.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
        foreach (var key in expiredTrails)
        {
            _trails.Remove(key);
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _pixelTexture == null)
            return;

        var viewport = _graphicsDevice.Viewport;
        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        foreach (var trail in _trails.Values)
        {
            if (trail.IsExpired || trail.Points.Count < 2)
                continue;

            DrawTrail(trail);
        }

        _spriteBatch.End();
    }

    /// <summary>
    /// Updates or creates a trail for an entity.
    /// </summary>
    public void UpdateTrail(object entityId, Vector2 position, float maxLength = 50.0f, float fadeRate = 2.0f, Color? color = null)
    {
        if (!_trails.ContainsKey(entityId))
        {
            _trails[entityId] = new Trail
            {
                MaxLength = maxLength,
                FadeRate = fadeRate,
                Color = color ?? Color.White
            };
        }

        var trail = _trails[entityId];
        trail.AddPoint(position);
    }

    /// <summary>
    /// Removes a trail for an entity.
    /// </summary>
    public void RemoveTrail(object entityId)
    {
        _trails.Remove(entityId);
    }

    /// <summary>
    /// Clears all trails.
    /// </summary>
    public void Clear()
    {
        _trails.Clear();
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create a 1x1 white pixel texture
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    /// <summary>
    /// Draws a trail as a series of connected line segments.
    /// </summary>
    private void DrawTrail(Trail trail)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        for (int i = 0; i < trail.Points.Count - 1; i++)
        {
            var point = trail.Points[i];
            var nextPoint = trail.Points[i + 1];

            var direction = nextPoint.Position - point.Position;
            var length = direction.Length();
            if (length < 0.1f)
                continue;

            direction.Normalize();

            var color = point.Color;
            var thickness = point.Thickness;

            // Draw line segment as a rotated rectangle
            var angle = MathF.Atan2(direction.Y, direction.X);
            var origin = new Vector2(0, 0.5f);
            var scale = new Vector2(length, thickness);

            _spriteBatch.Draw(
                _pixelTexture,
                point.Position,
                null,
                color,
                angle,
                origin,
                scale,
                SpriteEffects.None,
                0.0f);
        }
    }
}

/// <summary>
/// Represents a trail of points for an entity.
/// </summary>
internal class Trail
{
    private readonly List<TrailPoint> _points = new();
    public float MaxLength { get; set; } = 50.0f;
    public float FadeRate { get; set; } = 2.0f;
    public Color Color { get; set; } = Color.White;
    public float Thickness { get; set; } = 2.0f;

    public IReadOnlyList<TrailPoint> Points => _points;

    public void AddPoint(Vector2 position)
    {
        _points.Add(new TrailPoint
        {
            Position = position,
            Color = Color,
            Thickness = Thickness,
            Age = 0.0f
        });

        // Remove old points to maintain max length
        var totalLength = 0.0f;
        for (int i = _points.Count - 1; i >= 0; i--)
        {
            if (i > 0)
            {
                var segmentLength = Vector2.Distance(_points[i].Position, _points[i - 1].Position);
                totalLength += segmentLength;

                if (totalLength > MaxLength)
                {
                    _points.RemoveAt(i);
                }
            }
        }
    }

    public void Update(float deltaTime)
    {
        // Update age and fade out points
        for (int i = _points.Count - 1; i >= 0; i--)
        {
            var point = _points[i];
            point.Age += deltaTime;

            // Fade out based on age
            var alpha = Math.Max(0.0f, 1.0f - point.Age * FadeRate);
            point.Color = new Color(
                Color.R,
                Color.G,
                Color.B,
                (byte)(255 * alpha)
            );

            // Remove fully faded points
            if (alpha <= 0.0f)
            {
                _points.RemoveAt(i);
            }
        }
    }

    public bool IsExpired => _points.Count == 0;
}

/// <summary>
/// Represents a single point in a trail.
/// </summary>
internal class TrailPoint
{
    public Vector2 Position { get; set; }
    public Color Color { get; set; }
    public float Thickness { get; set; }
    public float Age { get; set; }
}

