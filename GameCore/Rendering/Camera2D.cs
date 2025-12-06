using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameCore.Rendering;

/// <summary>
/// 2D camera for controlling the viewport and rendering transformations.
/// </summary>
public class Camera2D
{
    private Vector2 _position;
    private float _zoom = 1.0f;
    private Matrix? _viewMatrix;
    private bool _viewMatrixDirty = true;

    /// <summary>
    /// Gets or sets the camera position in world coordinates.
    /// </summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                _viewMatrixDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the camera zoom level. Must be greater than 0.
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            var newZoom = Math.Max(0.1f, Math.Min(5.0f, value)); // Clamp between 0.1 and 5.0
            if (Math.Abs(_zoom - newZoom) > float.Epsilon)
            {
                _zoom = newZoom;
                _viewMatrixDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets the view matrix for rendering transformations.
    /// The matrix is cached and recalculated only when position or zoom changes.
    /// </summary>
    /// <param name="viewportWidth">Width of the viewport in pixels.</param>
    /// <param name="viewportHeight">Height of the viewport in pixels.</param>
    /// <returns>The view transformation matrix.</returns>
    public Matrix GetViewMatrix(int viewportWidth, int viewportHeight)
    {
        if (_viewMatrixDirty || _viewMatrix == null)
        {
            // Calculate the center of the viewport
            var viewportCenter = new Vector2(viewportWidth / 2.0f, viewportHeight / 2.0f);

            // Create transformation matrix: translate to center, scale by zoom, translate by position
            _viewMatrix = Matrix.CreateTranslation(new Vector3(-_position, 0.0f)) *
                         Matrix.CreateScale(_zoom) *
                         Matrix.CreateTranslation(new Vector3(viewportCenter, 0.0f));

            _viewMatrixDirty = false;
        }

        return _viewMatrix.Value;
    }

    /// <summary>
    /// Converts a screen coordinate to world coordinate.
    /// </summary>
    /// <param name="screenPos">Screen position in pixels.</param>
    /// <param name="viewportWidth">Width of the viewport in pixels.</param>
    /// <param name="viewportHeight">Height of the viewport in pixels.</param>
    /// <returns>World position.</returns>
    public Vector2 ScreenToWorld(Vector2 screenPos, int viewportWidth, int viewportHeight)
    {
        var viewportCenter = new Vector2(viewportWidth / 2.0f, viewportHeight / 2.0f);
        var worldPos = (screenPos - viewportCenter) / _zoom + _position;
        return worldPos;
    }

    /// <summary>
    /// Converts a world coordinate to screen coordinate.
    /// </summary>
    /// <param name="worldPos">World position.</param>
    /// <param name="viewportWidth">Width of the viewport in pixels.</param>
    /// <param name="viewportHeight">Height of the viewport in pixels.</param>
    /// <returns>Screen position in pixels.</returns>
    public Vector2 WorldToScreen(Vector2 worldPos, int viewportWidth, int viewportHeight)
    {
        var viewportCenter = new Vector2(viewportWidth / 2.0f, viewportHeight / 2.0f);
        var screenPos = (worldPos - _position) * _zoom + viewportCenter;
        return screenPos;
    }
}

