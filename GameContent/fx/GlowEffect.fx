// Glow effect shader for entity highlighting
// Creates a glowing outline around sprites
// Compatible with SpriteBatch (uses SpriteBatch's vertex shader)

texture SpriteTexture;

sampler SpriteSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float GlowIntensity = 1.0;
float GlowSize = 2.0;
float4 GlowColor = float4(1.0, 1.0, 1.0, 1.0);

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Sample the sprite
    float4 sprite = tex2D(SpriteSampler, texCoord);
    
    // Calculate glow by sampling surrounding pixels
    float glow = 0.0;
    float glowStep = 1.0 / 256.0; // Adjust based on texture size
    
    for (float x = -GlowSize; x <= GlowSize; x += 1.0)
    {
        for (float y = -GlowSize; y <= GlowSize; y += 1.0)
        {
            float2 offset = float2(x, y) * glowStep;
            float4 sample = tex2D(SpriteSampler, texCoord + offset);
            glow += sample.a;
        }
    }
    
    glow /= ((GlowSize * 2.0 + 1.0) * (GlowSize * 2.0 + 1.0));
    
    // Create glow only outside the sprite
    float glowAmount = glow * (1.0 - sprite.a) * GlowIntensity;
    
    // Combine sprite with glow
    float4 result = sprite;
    result.rgb += GlowColor.rgb * glowAmount;
    result.a = max(sprite.a, glowAmount * GlowColor.a);
    
    // Apply vertex color tint
    result *= color;
    
    return result;
}

technique Glow
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

