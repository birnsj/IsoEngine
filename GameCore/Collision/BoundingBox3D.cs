using Microsoft.Xna.Framework;

namespace GameCore.Collision;

/// <summary>
/// Represents a 3D bounding box with width, height, and depth.
/// </summary>
public struct BoundingBox3D
{
    /// <summary>
    /// Gets the X coordinate of the left edge.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y coordinate of the top edge.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the Z coordinate of the front edge.
    /// </summary>
    public float Z { get; }

    /// <summary>
    /// Gets the width (X dimension).
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Gets the height (Y dimension).
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Gets the depth (Z dimension).
    /// </summary>
    public float Depth { get; }

    /// <summary>
    /// Gets the left edge X coordinate.
    /// </summary>
    public float Left => X;

    /// <summary>
    /// Gets the right edge X coordinate.
    /// </summary>
    public float Right => X + Width;

    /// <summary>
    /// Gets the top edge Y coordinate.
    /// </summary>
    public float Top => Y;

    /// <summary>
    /// Gets the bottom edge Y coordinate.
    /// </summary>
    public float Bottom => Y + Height;

    /// <summary>
    /// Gets the front edge Z coordinate.
    /// </summary>
    public float Front => Z;

    /// <summary>
    /// Gets the back edge Z coordinate.
    /// </summary>
    public float Back => Z + Depth;

    /// <summary>
    /// Initializes a new instance of the BoundingBox3D struct.
    /// </summary>
    /// <param name="x">The X coordinate of the left edge.</param>
    /// <param name="y">The Y coordinate of the top edge.</param>
    /// <param name="z">The Z coordinate of the front edge.</param>
    /// <param name="width">The width (X dimension).</param>
    /// <param name="height">The height (Y dimension).</param>
    /// <param name="depth">The depth (Z dimension).</param>
    public BoundingBox3D(float x, float y, float z, float width, float height, float depth)
    {
        X = x;
        Y = y;
        Z = z;
        Width = width;
        Height = height;
        Depth = depth;
    }

    /// <summary>
    /// Checks if this bounding box intersects with another 3D bounding box.
    /// </summary>
    /// <param name="other">The other bounding box to check.</param>
    /// <returns>True if the boxes intersect, false otherwise.</returns>
    public bool Intersects(BoundingBox3D other)
    {
        return Left < other.Right && Right > other.Left &&
               Top < other.Bottom && Bottom > other.Top &&
               Front < other.Back && Back > other.Front;
    }

    /// <summary>
    /// Checks if this bounding box intersects with another in 2D (ignoring Z/depth).
    /// Useful for ground-level collision detection.
    /// </summary>
    /// <param name="other">The other bounding box to check.</param>
    /// <returns>True if the boxes intersect in 2D, false otherwise.</returns>
    public bool Intersects2D(BoundingBox3D other)
    {
        return Left < other.Right && Right > other.Left &&
               Top < other.Bottom && Bottom > other.Top;
    }

    /// <summary>
    /// Converts this 3D bounding box to a 2D rectangle (ignoring Z/depth).
    /// </summary>
    /// <returns>A RectangleF representing the 2D projection of this bounding box.</returns>
    public RectangleF To2D()
    {
        return new RectangleF(X, Y, Width, Height);
    }
}








