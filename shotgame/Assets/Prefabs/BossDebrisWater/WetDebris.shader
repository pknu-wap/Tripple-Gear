Shader "Tripple Gear/Wet Debris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Waterline ("Waterline", Range(0, 1)) = 0.23
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        _PaintColor ("Robot Paint", Color) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Waterline;
                float4 _SpriteUVRect;
                half4 _PaintColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 metal = sprite.rgb * input.color.rgb;
                const half3 luminanceWeights = half3(0.2126, 0.7152, 0.0722);
                half luminance = dot(metal, luminanceWeights);
                // Blue plates take new paint while warm rust and neutral edges retain their color.
                half blueBias = min(metal.b - metal.r, (metal.b - metal.g) * 2.0) / max(0.025, metal.b);
                half paint = smoothstep(0.06, 0.29, blueBias) * smoothstep(0.008, 0.036, luminance);
                half3 paintedMetal = luminance * _PaintColor.rgb / max(0.08, dot(_PaintColor.rgb, luminanceWeights));
                metal = lerp(metal, saturate(paintedMetal), paint * _PaintColor.a);

                float2 spriteUV = (input.uv - _SpriteUVRect.xy) / _SpriteUVRect.zw;
                float waterline = _Waterline + 0.012 * sin(spriteUV.x * 16.0);
                float heightAboveWater = spriteUV.y - waterline;
                half submerged = 1.0 - smoothstep(-0.035, 0.0, heightAboveWater);
                half3 wetMetal = metal * half3(0.42, 0.59, 0.74);

                // Pick up the brighter edges already painted into the metal.
                half polish = smoothstep(0.055, 0.36, luminance);
                half glint = (1.0 - smoothstep(0.008, 0.045, abs(heightAboveWater + 0.024))) * polish;
                wetMetal += half3(0.035, 0.12, 0.20) * glint;
                return half4(lerp(metal, wetMetal, submerged), sprite.a);
            }
            ENDHLSL
        }
    }
}
