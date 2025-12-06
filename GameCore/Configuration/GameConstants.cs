using System;
using System.IO;

namespace GameCore;

/// <summary>
/// Centralized game constants to avoid magic numbers throughout the codebase.
/// </summary>
public static class GameConstants
{
    /// <summary>
    /// Tile-related constants.
    /// </summary>
    public static class Tiles
    {
        /// <summary>
        /// Default tile width in pixels (isometric tiles are typically 2:1 ratio).
        /// </summary>
        public const int DefaultWidth = 64;

        /// <summary>
        /// Default tile height in pixels.
        /// </summary>
        public const int DefaultHeight = 32;

        /// <summary>
        /// Fixed tile width for entity positioning (does not scale with actual tile size).
        /// </summary>
        public const int FixedWidth = 64;

        /// <summary>
        /// Fixed tile height for entity positioning (does not scale with actual tile size).
        /// </summary>
        public const int FixedHeight = 32;

        /// <summary>
        /// Default solid tile indices (water, deep water, stone, dark stone).
        /// </summary>
        public static readonly int[] DefaultSolidTiles = { 4, 5, 6, 7 };

        /// <summary>
        /// Default map width in tiles (used when creating fallback maps).
        /// </summary>
        public const int DefaultMapWidth = 20;

        /// <summary>
        /// Default map height in tiles (used when creating fallback maps).
        /// </summary>
        public const int DefaultMapHeight = 20;
    }

    /// <summary>
    /// Player-related constants.
    /// </summary>
    public static class Player
    {
        /// <summary>
        /// Default player movement speed in pixels per second.
        /// </summary>
        public const float DefaultMovementSpeed = 150.0f;

        /// <summary>
        /// Default player size in pixels (width and height).
        /// </summary>
        public const float DefaultSize = 32.0f;

        /// <summary>
        /// Base arrival distance for click-to-move (scales with tile size).
        /// </summary>
        public const float BaseArrivalDistance = 5.0f;

        /// <summary>
        /// Base attack range in pixels (scales with tile size).
        /// </summary>
        public const float BaseAttackRange = 80.0f;

        /// <summary>
        /// Base interaction range in pixels (scales with tile size).
        /// </summary>
        public const float BaseInteractionRange = 80.0f;

        /// <summary>
        /// Base acceleration for smooth movement (scales with tile size).
        /// </summary>
        public const float BaseAcceleration = 800.0f;

        /// <summary>
        /// Base deceleration for smooth movement (scales with tile size).
        /// </summary>
        public const float BaseDeceleration = 1000.0f;

        /// <summary>
        /// Base click radius for interactables (scales with tile size).
        /// </summary>
        public const float BaseInteractableClickRadius = 30.0f;

        /// <summary>
        /// Base click radius for enemies (scales with tile size).
        /// </summary>
        public const float BaseEnemyClickRadius = 20.0f;

        /// <summary>
        /// Base hover radius for entity name tags (scales with tile size).
        /// </summary>
        public const float BaseHoverRadius = 25.0f;
    }

    /// <summary>
    /// Interaction-related constants.
    /// </summary>
    public static class Interaction
    {
        /// <summary>
        /// Default maximum distance for interaction in pixels (used as fallback).
        /// </summary>
        public const float DefaultRange = 50.0f;

        /// <summary>
        /// Cosine of maximum facing angle (about 45 degrees).
        /// Player must face roughly toward interactable to interact.
        /// </summary>
        public const float FacingAngleTolerance = 0.7f;
    }

    /// <summary>
    /// Enemy-related constants.
    /// </summary>
    public static class Enemy
    {
        /// <summary>
        /// Base movement speed for enemies (scales with tile size).
        /// </summary>
        public const float BaseMoveSpeed = 50.0f;

        /// <summary>
        /// Base attack range for enemies (scales with tile size).
        /// </summary>
        public const float BaseAttackRange = 60.0f;

        /// <summary>
        /// Base attack distance for enemies (scales with tile size).
        /// </summary>
        public const float BaseAttackDistance = 40.0f;

        /// <summary>
        /// Base minimum move distance for enemies (scales with tile size).
        /// </summary>
        public const float BaseMinMoveDistance = 5.0f;

        /// <summary>
        /// Cooldown between enemy attacks in seconds.
        /// </summary>
        public const float AttackCooldown = 1.5f;

        /// <summary>
        /// Duration of hit flash effect in seconds.
        /// </summary>
        public const float HitFlashDuration = 0.2f;
    }

    /// <summary>
    /// Camera-related constants.
    /// </summary>
    public static class Camera
    {
        /// <summary>
        /// Default camera zoom level.
        /// </summary>
        public const float DefaultZoom = 1.0f;

        /// <summary>
        /// Camera movement speed in pixels per second (for manual scrolling).
        /// </summary>
        public const float MoveSpeed = 200.0f;

        /// <summary>
        /// Zoom change per scroll unit.
        /// </summary>
        public const float ZoomSpeed = 0.1f;

        /// <summary>
        /// Speed for smooth camera following (higher = faster).
        /// </summary>
        public const float FollowSpeed = 10.0f;
    }

    /// <summary>
    /// UI-related constants.
    /// </summary>
    public static class UI
    {
        /// <summary>
        /// Number of columns in inventory grid.
        /// </summary>
        public const int InventoryColumns = 6;

        /// <summary>
        /// Number of rows in inventory grid.
        /// </summary>
        public const int InventoryRows = 4;

        /// <summary>
        /// Health bar width in pixels.
        /// </summary>
        public const int HealthBarWidth = 40;

        /// <summary>
        /// Health bar height in pixels.
        /// </summary>
        public const int HealthBarHeight = 4;

        /// <summary>
        /// Health bar vertical offset above entity in pixels.
        /// </summary>
        public const int HealthBarOffsetY = 20;
    }

    /// <summary>
    /// File path constants.
    /// </summary>
    public static class Paths
    {
        /// <summary>
        /// Relative path to saves directory.
        /// </summary>
        public const string SavesDirectory = "GameContent/saves";
    }

    /// <summary>
    /// Day/Night cycle constants.
    /// </summary>
    public static class DayNight
    {
        /// <summary>
        /// Default day duration in seconds (full cycle from midnight to midnight).
        /// </summary>
        public const float DefaultDayDuration = 300.0f;

        /// <summary>
        /// Dawn start time (0.0 to 1.0, where 0.0 is midnight).
        /// </summary>
        public const float DawnStart = 0.2f;

        /// <summary>
        /// Dawn end time (0.0 to 1.0, where 0.0 is midnight).
        /// </summary>
        public const float DawnEnd = 0.25f;

        /// <summary>
        /// Dusk start time (0.0 to 1.0, where 0.0 is midnight).
        /// </summary>
        public const float DuskStart = 0.75f;

        /// <summary>
        /// Dusk end time (0.0 to 1.0, where 0.0 is midnight).
        /// </summary>
        public const float DuskEnd = 0.8f;
    }

    /// <summary>
    /// Rendering-related constants.
    /// </summary>
    public static class Rendering
    {
        /// <summary>
        /// Size of the light texture in pixels (gradient circle for lighting effects).
        /// </summary>
        public const int LightTextureSize = 256;
    }

    /// <summary>
    /// Weather system constants.
    /// </summary>
    public static class Weather
    {
        /// <summary>
        /// Minimum weather change interval in seconds.
        /// </summary>
        public const float MinWeatherChangeInterval = 30.0f;

        /// <summary>
        /// Maximum weather change interval in seconds.
        /// </summary>
        public const float MaxWeatherChangeInterval = 120.0f;

        /// <summary>
        /// Weather probability weights (must sum to 100).
        /// </summary>
        public const float ProbabilityClear = 40.0f;
        public const float ProbabilityLightRain = 25.0f;
        public const float ProbabilityHeavyRain = 20.0f;
        public const float ProbabilitySnow = 10.0f;
        public const float ProbabilityFog = 5.0f;
    }

    /// <summary>
    /// Default file name constants to ensure game and editor use the same files.
    /// </summary>
    public static class DefaultFiles
    {
        /// <summary>
        /// Default world map file name (highest priority).
        /// </summary>
        public const string WorldMap = "world.json";

        /// <summary>
        /// Alternative world map file name (second priority).
        /// </summary>
        public const string WorldMapAlt = "world_map.json";

        /// <summary>
        /// Fallback map file name (third priority).
        /// </summary>
        public const string Map = "map.json";

        /// <summary>
        /// Default world entities file name.
        /// </summary>
        public const string WorldEntities = "world_entities.json";

        /// <summary>
        /// Default tiles library file name (shared tile graphics across all maps).
        /// </summary>
        public const string TilesLibrary = "tiles.json";

        /// <summary>
        /// Default items file name.
        /// </summary>
        public const string Items = "items.json";

        /// <summary>
        /// Default world lights file name.
        /// </summary>
        public const string WorldLights = "world_lights.json";

        /// <summary>
        /// Gets the preferred map file from a list of map files.
        /// Returns the file matching the priority order: world.json > world_map.json > map.json > first file.
        /// </summary>
        public static string? GetPreferredMapFile(string[] mapFiles)
        {
            if (mapFiles == null || mapFiles.Length == 0)
                return null;

            // Priority 1: world.json
            var worldMap = Array.Find(mapFiles, f => 
                Path.GetFileName(f).Equals(WorldMap, StringComparison.OrdinalIgnoreCase));
            if (worldMap != null)
                return worldMap;

            // Priority 2: world_map.json
            var worldMapAlt = Array.Find(mapFiles, f => 
                Path.GetFileName(f).Equals(WorldMapAlt, StringComparison.OrdinalIgnoreCase));
            if (worldMapAlt != null)
                return worldMapAlt;

            // Priority 3: map.json
            var defaultMap = Array.Find(mapFiles, f => 
                Path.GetFileName(f).Equals(Map, StringComparison.OrdinalIgnoreCase));
            if (defaultMap != null)
                return defaultMap;

            // Priority 4: First file found
            return mapFiles[0];
        }
    }
}

