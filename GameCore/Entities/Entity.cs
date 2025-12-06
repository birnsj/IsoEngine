using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace GameCore.Entities;

/// <summary>
/// Base class for all game entities.
/// </summary>
public class Entity
{
    private Vector2 _position;
    private Vector2 _size;
    private float _z;
    private Vector2 _placementBoundsSize;
    private Vector2 _placementBoundsOffset;
    private float _zHeight = 1f;

    /// <summary>
    /// Gets or sets the entity's position in world coordinates.
    /// </summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            if (float.IsNaN(value.X) || float.IsNaN(value.Y))
                throw new ArgumentException("Position cannot contain NaN", nameof(value));
            _position = value;
            _boundingBoxDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the entity's velocity in pixels per second.
    /// </summary>
    public Vector2 Velocity { get; set; }

    /// <summary>
    /// Gets or sets the entity's size in pixels.
    /// </summary>
    public Vector2 Size
    {
        get => _size;
        set
        {
            if (value.X <= 0 || value.Y <= 0)
                throw new ArgumentException("Size must be greater than zero", nameof(value));
            if (float.IsNaN(value.X) || float.IsNaN(value.Y))
                throw new ArgumentException("Size cannot contain NaN", nameof(value));
            _size = value;
            _boundingBoxDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets whether this entity is solid (can collide with other solid entities/tiles).
    /// </summary>
    public bool IsSolid { get; set; }

    /// <summary>
    /// Gets or sets whether this entity is active (should be updated and drawn).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the entity's Z position (height/elevation) in world units.
    /// Higher Z values = higher in the world. Affects render order and visual position.
    /// </summary>
    public float Z
    {
        get => _z;
        set
        {
            if (float.IsNaN(value))
                throw new ArgumentException("Z cannot be NaN", nameof(value));
            _z = value;
            _boundingBoxDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the vertical extent (height) of the entity in Z units.
    /// Used for 3D overlap checking during placement.
    /// </summary>
    public float ZHeight
    {
        get => _zHeight;
        set => _zHeight = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Gets or sets the size of the placement collision footprint.
    /// This defines the ground area the entity occupies for placement collision.
    /// If not set (zero), defaults to the entity's visual Size.
    /// </summary>
    public Vector2 PlacementBoundsSize
    {
        get => _placementBoundsSize == Vector2.Zero ? _size : _placementBoundsSize;
        set => _placementBoundsSize = value;
    }

    /// <summary>
    /// Gets or sets the offset of the placement bounds from the entity's position.
    /// Useful for entities where the visual center differs from the collision footprint center.
    /// </summary>
    public Vector2 PlacementBoundsOffset
    {
        get => _placementBoundsOffset;
        set => _placementBoundsOffset = value;
    }

    /// <summary>
    /// Gets the placement collision footprint rectangle (in screen/world coordinates).
    /// </summary>
    public Rectangle PlacementBounds
    {
        get
        {
            var boundsSize = PlacementBoundsSize;
            var center = _position + _placementBoundsOffset;
            return new Rectangle(
                (int)(center.X - boundsSize.X / 2),
                (int)(center.Y - boundsSize.Y / 2),
                (int)boundsSize.X,
                (int)boundsSize.Y);
        }
    }

    /// <summary>
    /// Gets the depth value for render sorting.
    /// In isometric view: entities with higher depth render later (on top).
    /// Depth = Y position + Z (entities further down or higher up render later).
    /// </summary>
    public float RenderDepth => _position.Y + _z;

    /// <summary>
    /// Checks if this entity's placement bounds overlap with another entity's,
    /// considering Z ranges for 3D collision.
    /// </summary>
    /// <param name="other">The other entity to check.</param>
    /// <returns>True if the entities overlap in 3D space.</returns>
    public bool PlacementOverlaps(Entity other)
    {
        // Check horizontal (XY) overlap using placement bounds
        if (!PlacementBounds.Intersects(other.PlacementBounds))
            return false;

        // Check vertical (Z) overlap
        var thisBottom = _z;
        var thisTop = _z + _zHeight;
        var otherBottom = other.Z;
        var otherTop = other.Z + other.ZHeight;

        return thisBottom < otherTop && thisTop > otherBottom;
    }

    private readonly Dictionary<Type, object> _components = new();
    private Rectangle _boundingBox;
    private bool _boundingBoxDirty = true;

    /// <summary>
    /// Gets the entity's bounding box, computed from position and size.
    /// </summary>
    public Rectangle BoundingBox
    {
        get
        {
            if (_boundingBoxDirty)
            {
                _boundingBox = new Rectangle(
                    (int)(_position.X - _size.X / 2),
                    (int)(_position.Y - _size.Y / 2),
                    (int)_size.X,
                    (int)_size.Y);
                _boundingBoxDirty = false;
            }
            return _boundingBox;
        }
    }

    /// <summary>
    /// Initializes a new instance of the Entity class.
    /// </summary>
    /// <param name="position">Initial position.</param>
    /// <param name="size">Entity size in pixels.</param>
    public Entity(Vector2 position, Vector2 size)
    {
        _position = position;
        _size = size;
        Velocity = Vector2.Zero;
        IsSolid = true;
    }

    /// <summary>
    /// Adds a component to this entity.
    /// </summary>
    /// <typeparam name="T">The type of component.</typeparam>
    /// <param name="component">The component instance.</param>
    public void AddComponent<T>(T component) where T : class
    {
        _components[typeof(T)] = component;
    }

    /// <summary>
    /// Gets a component from this entity.
    /// </summary>
    /// <typeparam name="T">The type of component.</typeparam>
    /// <returns>The component, or null if not found.</returns>
    public T? GetComponent<T>() where T : class
    {
        return _components.TryGetValue(typeof(T), out var component) ? component as T : null;
    }

    /// <summary>
    /// Checks if this entity has a component of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of component.</typeparam>
    /// <returns>True if the component exists, false otherwise.</returns>
    public bool HasComponent<T>() where T : class
    {
        return _components.ContainsKey(typeof(T));
    }
}

