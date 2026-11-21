Shader "Tripple Gear/Boat Sea Scroll"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sea Sprite", 2D) = "white" {}
        _Scroll ("Visual Travel and Camera Travel", Vector) = (0, 0, 0, 0)
        _SeaArea ("Sea Origin and Inverse Tile Size", Vector) = (0, 0, 1, 1)
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
                COMMON_2D_OUTPUTS_SHARED
                float2 seaUV : TEXCOORD1;
                half4 color : COLOR;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Scroll;
                float4 _SeaArea;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.seaUV = (positionWS.xy - _SeaArea.xy) * _SeaArea.zw;
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 seaUV = input.seaUV;
                float nearWater = 1.0 - smoothstep(0.28, 0.402, seaUV.y);
                float parallax = lerp(0.32, 1.0, nearWater);
                // Keep the apparent speed continuous when the camera starts or stops.
                float offset = (_Scroll.x * parallax - _Scroll.y) * _SeaArea.z;
                float scrolledX = 1.0 - abs(2.0 * frac((seaUV.x + offset) * 0.5) - 1.0);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(scrolledX, saturate(seaUV.y))) * input.color;
            }
            ENDHLSL
        }
    }
}
