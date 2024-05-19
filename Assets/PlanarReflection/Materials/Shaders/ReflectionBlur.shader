// This shader fills the mesh shape with a color predefined in the code.
Shader "Hidden/ReflectionBlur"
{
    // The properties block of the Unity shader. In this example this block is empty
    // because the output color is predefined in the fragment shader code.
    Properties
    {
       [MainTex] _ReflectionTexture ("ReflectionTexture", 2D) = "white" {}
       [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    
    }

    HLSLINCLUDE       
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // input structure (Attributes) and output strucutre (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_ReflectionTexture);
        SAMPLER(sampler_linear_Clamp);
        float4 _BlitTexture_TexelSize;
        half _Offset;

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END

        
        half4 frag(Varyings IN) : SV_Target
        {
            // Defining the color variable and returning it.
            half4 customColor = half4(0.5, 0, 0, 1);
            return customColor;
        }

        half4 fragInvert(Varyings IN) : SV_Target
        {
            half4 color = 1 - SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, IN.texcoord);
            return color;
        }

        half4 fragDownsample(Varyings IN) : SV_Target
        {
            half halfTexel = _BlitTexture_TexelSize * 0.5;             
            float2 offset = float2(1.0 + _Offset, 1.0 + _Offset) * halfTexel;

            half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, IN.texcoord) * 4.0;
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + offset));
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord - offset));
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, float2(IN.texcoord.x + offset.x, IN.texcoord.y - offset.y) );
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, float2(IN.texcoord.x - offset.x, IN.texcoord.y + offset.y) );
            
            return color / 8.0;
        }

        half4 fragUpsample(Varyings IN) : SV_Target
        {
            half halfTexel = _BlitTexture_TexelSize * 0.5; 
            half offset = float2(1.0 + _Offset, 1.0 + _Offset) * halfTexel;

            half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(-offset * 2.0, 0.0)));
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(0.0, offset * 2.0)));
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(offset * 2.0, 0.0)));

            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(-offset, offset))) * 2.0;
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(offset, offset))) * 2.0;
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(-offset, -offset))) * 2.0;
            color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_linear_Clamp, (IN.texcoord + float2(offset, -offset))) * 2.0;
            
            return color / 12.0;
        }
    ENDHLSL
    
    // The SubShader block containing the Shader code.
    SubShader
    {
        // SubShader Tags define when and under which conditions a SubShader block or
        // a pass is executed.
        Tags {"RenderPipeline" = "UniversalPipeline" }

        Pass
        { // 0
            Name "Reflection Blur Donwsample"
            ZWrite Off  
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragDownsample
            ENDHLSL
        }

        Pass
        { // 1
            Name "Reflection Blur Upsample"
            ZWrite Off  
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragUpsample
            ENDHLSL
        }
    }
}