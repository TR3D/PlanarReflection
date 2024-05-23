// Unity_Shader no upgrade
#ifndef REFLECTIONBLUR_HLSL
#define REFLECTIONBLUR_HLSL


SAMPLER (sampler_linear_clamp);
float4 _ReflectionTex_TexelSize;
float _Offset;

// taken from https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf

//float4 downsample(float2 uv, float2 halfpixel)
//{
//    float4 sum = texture(_ReflectionTex, uv) * 4.0;
//    sum += texture(_ReflectionTex, uv - halfpixel.xy);
//    sum += texture(_ReflectionTex, uv + halfpixel.xy);
//    sum += texture(_ReflectionTex, uv + float2(halfpixel.x, -halfpixel.y));
//    sum += texture(_ReflectionTex, uv - float2(halfpixel.x, -halfpixel.y));
//    return sum / 8.0;
//}

//float4 upsample(float2 uv, float2 halfpixel)
//{
//    float4 sum = texture(_ReflectionTex, uv + float2(-halfpixel.x * 2.0, 0.0));
//    sum += texture(_ReflectionTex, uv + float2(-halfpixel.x, halfpixel.y)) * 2.0;
//    sum += texture(_ReflectionTex, uv + float2(0.0, halfpixel.y * 2.0));
//    sum += texture(_ReflectionTex, uv + float2(halfpixel.x, halfpixel.y)) * 2.0;
//    sum += texture(_ReflectionTex, uv + float2(halfpixel.x * 2.0, 0.0));
//    sum += texture(_ReflectionTex, uv + float2(halfpixel.x, -halfpixel.y)) * 2.0;
//    sum += texture(_ReflectionTex, uv + float2(0.0, -halfpixel.y * 2.0));
//    sum += texture(_ReflectionTex, uv + float2(-halfpixel.x, -halfpixel.y)) * 2.0;
//    return sum / 12.0;
//}

void FragInvert_float (float4 col, out float4 Out)
{
    Out.rgb = 1 - col.rgb;    
    Out.a = col.a;
}

// Shader Graph Preview will throw an error if the first output is a Texture
// texSize = (width, height, 1/width, 1/height)
void BlurDownsampleUV_float(float2 uv, float2 texel, out float2 OutUV1, out float2 OutUV2)
{
    
    float2 halfPixel = texel * 0.5;
    float blurOffset = 1.0 + _Offset;
        
    //half4 result = SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv) * 4.0;
    OutUV1 = uv - (halfPixel.xy );
    OutUV2 = uv - (float2(halfPixel.x, -halfPixel.y) );
}

void BlurDownsample_float(float2 uv, Texture2D _tex, out half4 Out)
{
    
    float2 halfPixel = _ReflectionTex_TexelSize.zw * 0.5;
    float2 blurOffset = float2(1.0 + _Offset, 1.0 + _Offset);
        
    half4 result = SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv) * 4.0;
    result += SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv - halfPixel.xy);
    result += SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv + halfPixel.xy);
    result += SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv - float2(halfPixel.x, -halfPixel.y));
    result += SAMPLE_TEXTURE2D(_tex, sampler_linear_clamp, uv + float2(halfPixel.x, -halfPixel.y));
    
    Out = result / 8.0;
    
    //Out = SAMPLE_TEXTURE2D_X_LOD(_ReflectionTex, sampler_linear_clamp, downsample(uv, halfPixel * blurOffset));
}



#endif // REFLECTIONBLUR_HLSL