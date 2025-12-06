// Outline effect shader for entity highlighting
// Creates a colored outline around sprites
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

float OutlineWidth = 2.0;
float4 OutlineColor = float4(1.0, 1.0, 0.0, 1.0);

float4 PixelShaderFunction(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Sample the sprite
    float4 sprite = tex2D(SpriteSampler, texCoord);
    
    // Check if this pixel is on the edge
    float edge = 0.0;
    float step = 1.0 / 256.0; // Adjust based on texture size
    
    // Sample surrounding pixels
    float alphaSum = 0.0;
    alphaSum += tex2D(SpriteSampler, texCoord + float2(-step, 0)).a;
    alphaSum += tex2D(SpriteSampler, texCoord + float2(step, 0)).a;
    alphaSum += tex2D(SpriteSampler, texCoord + float2(0, -step)).a;
    alphaSum += tex2D(SpriteSampler, texCoord + float2(0, step)).a;
    
    // If center is transparent but neighbors have alpha, we're on an edge
    if (sprite.a < 0.5 && alphaSum > 0.5)
    {
        edge = 1.0;
    }
    
    // Combine sprite with outline
    float4 result = sprite;
    if (edge > 0.0)
    {
        result = OutlineColor;
        result.a *= edge;
    }
    
    // Apply vertex color tint
    result *= color;
    
    return result;
}

technique Outline
{
    pass Pass0
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

