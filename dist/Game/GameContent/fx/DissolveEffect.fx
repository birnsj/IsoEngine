// Dissolve effect shader for death/teleportation effects
// Gradually dissolves sprites using a noise pattern
// Compatible with SpriteBatch (uses SpriteBatch's vertex shader)

texture SpriteTexture;
texture NoiseTexture;

sampler SpriteSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler NoiseSampler = sampler_state
{
    Texture = <NoiseTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

float DissolveAmount = 0.0; // 0.0 = fully visible, 1.0 = fully dissolved
float EdgeWidth = 0.1;
float4 EdgeColor = float4(1.0, 0.5, 0.0, 1.0); // Orange edge for dissolve

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Sample the sprite
    float4 sprite = tex2D(SpriteSampler, texCoord);
    
    // Sample noise texture for dissolve pattern
    float noise = tex2D(NoiseSampler, texCoord * 4.0).r; // Scale noise
    
    // Calculate dissolve
    float dissolve = step(DissolveAmount, noise);
    
    // Create edge effect
    float edge = smoothstep(DissolveAmount - EdgeWidth, DissolveAmount, noise);
    edge *= (1.0 - dissolve); // Only show edge where not dissolved
    
    // Combine sprite with dissolve and edge
    float4 result = sprite;
    result.a *= dissolve;
    
    // Add edge color
    result.rgb = lerp(result.rgb, EdgeColor.rgb, edge);
    result.a = max(result.a, edge * EdgeColor.a);
    
    // Apply vertex color tint
    result *= color;
    
    return result;
}

technique Dissolve
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

