Shader "Tripple Gear/Final Sky Cloud Scroll"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cloud Sprite", 2D) = "white" {}
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        _CloudTravel ("Cloud Travel", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="Universal2D" }
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
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _SpriteUVRect;
                float _CloudTravel;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = (input.uv - _SpriteUVRect.xy) / _SpriteUVRect.zw;
                float foreground = 1.0 - smoothstep(0.13, 0.48, uv.y);
                float offset = _CloudTravel + foreground * 0.05 * sin(_CloudTravel * 3.14159265);
                // Mirrored tiles meet at the same edge, and both offsets repeat every two widths.
                uv.x = 1.0 - abs(2.0 * frac((uv.x + offset) * 0.5) - 1.0);
                uv.y = saturate(uv.y);
                float2 sampleUV = _SpriteUVRect.xy + uv * _SpriteUVRect.zw;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV) * input.color;
            }
            ENDHLSL
        }
    }
}
