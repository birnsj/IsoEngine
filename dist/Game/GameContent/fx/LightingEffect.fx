// Lighting effect shader for 2D lightmap
// Multiplies the scene with the light map to create lighting
// Compatible with SpriteBatch (uses SpriteBatch's vertex shader)

texture SceneTexture;
texture LightMapTexture;

sampler SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler LightMapSampler = sampler_state
{
    Texture = <LightMapTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Sample the scene
    float4 scene = tex2D(SceneSampler, texCoord);
    
    // Sample the light map
    float4 lightMap = tex2D(LightMapSampler, texCoord);
    
    // Multiply: result = scene * lightMap
    // This darkens areas where light map is dark, brightens where light map is bright
    // Normalize light map from 0-255 to 0-1 range for proper multiplication
    // Clamp to prevent values over 255 (from additive blending) from causing white screen
    // Use a lower maximum (0.9) to prevent saturation and white screen
    float4 normalizedLightMap = clamp(lightMap / 255.0, 0.0, 0.9);
    float4 result = scene * normalizedLightMap;
    
    // Apply vertex color tint if needed
    result *= color;
    
    // Ensure alpha is preserved
    result.a = scene.a * color.a;
    
    // Clamp final result to prevent overflow to white
    result = clamp(result, 0.0, 1.0);
    
    return result;
}

technique Lighting
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

