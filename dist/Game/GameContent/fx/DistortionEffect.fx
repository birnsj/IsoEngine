// Distortion effect shader for environmental effects (heat haze, water ripples)
// Applies UV distortion to create wavy/ripple effects
// Compatible with SpriteBatch (uses SpriteBatch's vertex shader)

texture SpriteTexture;
texture DistortionTexture;

sampler SpriteSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler DistortionSampler = sampler_state
{
    Texture = <DistortionTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

float DistortionStrength = 0.1;
float DistortionSpeed = 1.0;
float Time = 0.0;

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Sample distortion texture (red/green channels represent X/Y offset)
    float2 distortion = tex2D(DistortionSampler, texCoord + Time * DistortionSpeed).rg;
    
    // Convert from 0-1 range to -1 to 1 range
    distortion = (distortion - 0.5) * 2.0;
    
    // Apply distortion to texture coordinates
    float2 distortedCoord = texCoord + distortion * DistortionStrength;
    
    // Sample sprite with distorted coordinates
    float4 sprite = tex2D(SpriteSampler, distortedCoord);
    
    // Apply vertex color tint
    sprite *= color;
    
    return sprite;
}

technique Distortion
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

