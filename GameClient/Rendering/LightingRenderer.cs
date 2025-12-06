using GameCore;
using GameCore.Rendering;
using GameCore.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient.Rendering;

/// <summary>
/// Renders a 2D lightmap lighting system using render targets and blending.
/// </summary>
public class LightingRenderer : IGameService
{
    private readonly Camera2D _camera;
    private SpriteBatch? _spriteBatch;
    private GraphicsDevice? _graphicsDevice;
    private Texture2D? _lightTexture;
    private Texture2D? _pixelTexture; // 1x1 white texture for overlays
    private RenderTarget2D? _lightMap;
    private RenderTarget2D? _lightsOnlyRenderTarget; // Separate render target for lights only (no ambient)
    private RenderTarget2D? _sceneRenderTarget;
    private BlendState? _multiplyBlendState;
    private readonly List<Light> _lights = new();
    private Color _ambientColor = new Color(100, 100, 100, 255); // Dark ambient (areas without lights will be darker)

    /// <summary>
    /// Gets or sets the ambient light color (base brightness level).
    /// </summary>
    public Color AmbientColor
    {
        get => _ambientColor;
        set => _ambientColor = value;
    }

    /// <summary>
    /// Gets the list of active lights.
    /// </summary>
    public IList<Light> Lights => _lights;

    public LightingRenderer(Camera2D camera)
    {
        _camera = camera;
    }

    public void Initialize()
    {
        // Initialization happens in LoadContent
    }

    public void Update(GameTime gameTime)
    {
        // Lighting renderer doesn't need per-frame updates
        // Lights can be updated externally through the Lights collection
    }

    /// <summary>
    /// Renders the scene to a render target. Call this before Draw().
    /// </summary>
    public void BeginSceneCapture()
    {
        if (_graphicsDevice == null || _sceneRenderTarget == null)
            return;

        var viewport = _graphicsDevice.Viewport;

        // Resize scene render target if viewport size changed
        if (_sceneRenderTarget.Width != viewport.Width || _sceneRenderTarget.Height != viewport.Height)
        {
            _sceneRenderTarget.Dispose();
            _sceneRenderTarget = new RenderTarget2D(_graphicsDevice, viewport.Width, viewport.Height);
        }

        // Set render target to capture the scene
        _graphicsDevice.SetRenderTarget(_sceneRenderTarget);
    }

    /// <summary>
    /// Applies lighting to the captured scene and draws to the back buffer.
    /// </summary>
    public void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null || _graphicsDevice == null || _lightTexture == null || 
            _sceneRenderTarget == null)
            return;

        var viewport = _graphicsDevice.Viewport;

        // Resize lights-only render target if needed
        if (_lightsOnlyRenderTarget == null || 
            _lightsOnlyRenderTarget.Width != viewport.Width || 
            _lightsOnlyRenderTarget.Height != viewport.Height)
        {
            _lightsOnlyRenderTarget?.Dispose();
            _lightsOnlyRenderTarget = new RenderTarget2D(_graphicsDevice, viewport.Width, viewport.Height);
        }

        // Render lights to separate render target (lights only, no ambient)
        // This allows lights to maintain their colors regardless of day/night cycle
        _graphicsDevice.SetRenderTarget(_lightsOnlyRenderTarget);
        _graphicsDevice.Clear(Color.Transparent); // Clear to transparent - lights will be added additively

        var viewMatrix = _camera.GetViewMatrix(viewport.Width, viewport.Height);

        // Draw lights additively (pure light colors, no ambient base)
        _spriteBatch.Begin(
            transformMatrix: viewMatrix,
            samplerState: SamplerState.LinearClamp,
            blendState: BlendState.Additive); // Additive blending for multiple lights

        // Draw each light at full brightness - completely independent of day/night cycle
        // Lights use maximum intensity to ensure they're never affected by ambient
        foreach (var light in _lights)
        {
            var screenPos = light.Position;

            var lightSize = light.Radius * 2.0f;
            var lightRect = new Rectangle(
                (int)(screenPos.X - light.Radius),
                (int)(screenPos.Y - light.Radius),
                (int)(lightSize),
                (int)(lightSize));

            // Use full light color at maximum intensity - completely ignore ambient/day-night
            // Multiply by 2.5 to ensure lights are bright enough to overpower any ambient tint
            var lightColor = new Color(
                (byte)Math.Clamp(light.Color.R * light.Intensity * 2.5f, 0, 255),
                (byte)Math.Clamp(light.Color.G * light.Intensity * 2.5f, 0, 255),
                (byte)Math.Clamp(light.Color.B * light.Intensity * 2.5f, 0, 255),
                (byte)255);

            var sourceRect = new Rectangle(0, 0, _lightTexture.Width, _lightTexture.Height);
            _spriteBatch.Draw(_lightTexture, lightRect, sourceRect, lightColor);
        }

        _spriteBatch.End();

        // Reset render target to back buffer
        _graphicsDevice.SetRenderTarget(null);

        // Draw the scene first
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_sceneRenderTarget, viewport.Bounds, Color.White);
        _spriteBatch.End();

        // Apply day/night cycle ambient color as an overlay tint FIRST
        // This tints the scene but lights will be added on top to maintain their colors
        var ambientBrightness = (_ambientColor.R + _ambientColor.G + _ambientColor.B) / 3.0f;
        
        // Map brightness to tint multiplier: darker nights (0.34x), darker days (0.765x - 15% darker)
        // Night brightness ~43, Day brightness now 61 (15% darker than 72)
        // Map range 43-61 to tint multipliers 0.34-0.765
        var tintMultiplier = 0.34f + (ambientBrightness - 43.0f) / 18.0f * 0.425f;
        tintMultiplier = Math.Clamp(tintMultiplier, 0.34f, 0.765f); // Cap at 0.765x for 15% darker day (0.9 * 0.85)
        
        // Convert ambient RGB to tint RGB, preserving hue
        var r = Math.Clamp((_ambientColor.R / 100.0f) * tintMultiplier, 0.0f, 1.0f);
        var g = Math.Clamp((_ambientColor.G / 100.0f) * tintMultiplier, 0.0f, 1.0f);
        var b = Math.Clamp((_ambientColor.B / 100.0f) * tintMultiplier, 0.0f, 1.0f);
        
        // Boost color saturation slightly to make blue/orange more visible
        var maxChannel = Math.Max(Math.Max(r, g), b);
        if (maxChannel > 0)
        {
            var saturationBoost = 1.1f; // 10% saturation boost
            r = Math.Clamp(r * saturationBoost, 0.0f, 1.0f);
            g = Math.Clamp(g * saturationBoost, 0.0f, 1.0f);
            b = Math.Clamp(b * saturationBoost, 0.0f, 1.0f);
        }
        
        var tintColor = new Color(
            (byte)(255.0f * r),
            (byte)(255.0f * g),
            (byte)(255.0f * b),
            (byte)255);
        
        // Apply ambient tint to scene using multiply blend
        if (_multiplyBlendState == null)
        {
            _multiplyBlendState = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.DestinationColor,
                ColorDestinationBlend = Blend.SourceColor,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One
            };
        }
        
        _spriteBatch.Begin(
            samplerState: SamplerState.LinearClamp,
            blendState: _multiplyBlendState);
        
        if (_pixelTexture != null)
        {
            var tintRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
            _spriteBatch.Draw(_pixelTexture, tintRect, tintColor);
        }
        _spriteBatch.End();

        // Add lights additively on top with maximum brightness
        // This completely preserves their colors and ensures they're never affected by day/night cycle
        if (_lightsOnlyRenderTarget != null)
        {
            _spriteBatch.Begin(
                samplerState: SamplerState.LinearClamp,
                blendState: BlendState.Additive);
            
            // Draw lights at full brightness - white color multiplier to preserve original colors
            _spriteBatch.Draw(_lightsOnlyRenderTarget, viewport.Bounds, Color.White);
            _spriteBatch.End();
        }
    }

    /// <summary>
    /// Adds a light to the lighting system.
    /// </summary>
    public void AddLight(Light light)
    {
        if (!_lights.Contains(light))
        {
            _lights.Add(light);
        }
    }

    /// <summary>
    /// Removes a light from the lighting system.
    /// </summary>
    public void RemoveLight(Light light)
    {
        _lights.Remove(light);
    }

    /// <summary>
    /// Clears all lights.
    /// </summary>
    public void ClearLights()
    {
        _lights.Clear();
    }

    /// <summary>
    /// Sets up the renderer with graphics device and sprite batch.
    /// </summary>
    public void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Microsoft.Xna.Framework.Content.ContentManager? content = null)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Create light map render target (will be resized in Draw if needed)
        var viewport = graphicsDevice.Viewport;
        _lightMap = new RenderTarget2D(graphicsDevice, viewport.Width, viewport.Height);
        _sceneRenderTarget = new RenderTarget2D(graphicsDevice, viewport.Width, viewport.Height);

        // Create light texture (gradient circle)
        _lightTexture = CreateLightTexture(graphicsDevice, GameConstants.Rendering.LightTextureSize);
        
        // Create 1x1 white pixel texture for overlays
        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    /// <summary>
    /// Creates a gradient circle texture for rendering lights.
    /// The texture has a smooth falloff from center to edge.
    /// </summary>
    private Texture2D CreateLightTexture(GraphicsDevice device, int size)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        var center = size / 2.0f;
        var maxRadius = size / 2.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= maxRadius)
                {
                    // Calculate falloff: 1.0 at center, 0.0 at edge
                    var normalizedDistance = distance / maxRadius;
                    var intensity = 1.0f - normalizedDistance;
                    
                    // Apply smooth falloff curve (squared for smoother transition)
                    intensity = intensity * intensity;
                    
                    data[y * size + x] = new Color(1.0f, 1.0f, 1.0f, intensity);
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

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _lightMap?.Dispose();
        _lightsOnlyRenderTarget?.Dispose();
        _lightTexture?.Dispose();
        _pixelTexture?.Dispose();
        _sceneRenderTarget?.Dispose();
        _multiplyBlendState?.Dispose();
    }
}

