Shader "Tripple Gear/GongOil Missile Smoke"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BandAspect ("Band Aspect Ratio", Float) = 8
        _SmokeTime ("Smoke Time", Float) = 0
        _SmokeOpacity ("Smoke Opacity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            float _BandAspect;
            float _SmokeTime;
            float _SmokeOpacity;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(float4 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(position);
                output.uv = uv;
                return output;
            }

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            float CloudNoise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 blend = smoothstep(0.0, 1.0, frac(coordinate));
                float lower = lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x);
                float upper = lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), blend.x);
                return lerp(lower, upper, blend.y);
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 coordinate = float2(uv.x * _BandAspect, uv.y);
                float2 drift = float2(_SmokeTime * 0.72, -_SmokeTime * 0.94);
                float swirl = CloudNoise(coordinate * 3.1 + drift * 0.65);
                float2 flow = coordinate + float2(swirl * 0.32, (swirl - 0.5) * 0.27);
                float large = CloudNoise(flow * 4.4 + drift);
                float detail = CloudNoise(flow * 11.3 + float2(-drift.x * 0.82, drift.y * 1.9));
                float fine = CloudNoise(flow * 24.0 + drift * 2.5);
                float shape = large * 0.62 + detail * 0.27 + fine * 0.11;

                float side = smoothstep(0.0, 0.014, min(uv.x, 1.0 - uv.x));
                float bottom = smoothstep(0.0, 0.09, uv.y);
                float top = smoothstep(0.0, 0.12 + detail * 0.05, 1.0 - uv.y);
                float density = lerp(0.86, 0.99, smoothstep(0.16, 0.78, shape));
                float3 tint = lerp(float3(0.009, 0.011, 0.017), float3(0.105, 0.11, 0.128),
                    smoothstep(0.27, 0.8, shape));
                return fixed4(tint, density * side * bottom * top * _SmokeOpacity);
            }
            ENDCG
        }
    }
}
