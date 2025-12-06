// Animated UV shader for flowing textures (water, animated tiles)
// Scrolls UV coordinates to create flowing/moving texture effects
// Compatible with SpriteBatch (uses SpriteBatch's vertex shader)

texture SpriteTexture;

sampler SpriteSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

float2 ScrollSpeed = float2(0.1, 0.0); // X and Y scroll speed
float Time = 0.0;

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Scroll UV coordinates
    float2 scrolledCoord = texCoord + ScrollSpeed * Time;
    
    // Sample sprite with scrolled coordinates
    float4 sprite = tex2D(SpriteSampler, scrolledCoord);
    
    // Apply vertex color tint
    sprite *= color;
    
    return sprite;
}

technique AnimatedTexture
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

