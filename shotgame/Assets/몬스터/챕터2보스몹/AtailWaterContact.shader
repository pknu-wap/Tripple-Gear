Shader "Tripple Gear/Atail Water Contact"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _WaterlineY ("Waterline (world Y)", Float) = 0
        [PerRendererData] [HDR] _HitFlashColor ("Hit Flash Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _WaterlineY;
            half4 _HitFlashColor;

            struct SpriteVertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct SpriteFragment
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float2 worldPosition : TEXCOORD1;
            };

            SpriteFragment Vertex(SpriteVertex input)
            {
                SpriteFragment result;
                result.position = UnityObjectToClipPos(input.position);
                result.worldPosition = mul(unity_ObjectToWorld, input.position).xy;
                result.uv = input.uv;
                result.color = input.color;
                return result;
            }

            half4 Fragment(SpriteFragment input) : SV_Target
            {
                half4 skin = tex2D(_MainTex, input.uv) * input.color;
                skin.rgb = lerp(skin.rgb, _HitFlashColor.rgb, 0.65 * _HitFlashColor.a);
                float depth = _WaterlineY - input.worldPosition.y;
                if (depth <= 0.0) return skin;

                float x = input.worldPosition.x;
                float waves = sin(x * 9.0 - _Time.y * 4.2) * sin(x * 3.7 + _Time.y * 2.8);
                float visibility = 1.0 - smoothstep(0.20 + 0.08 * waves, 0.69 + 0.13 * waves, depth);
                clip(visibility - 0.002);
                float edge = (1.0 - smoothstep(0.008, 0.06 + 0.02 * waves, depth)) * (0.55 + 0.45 * waves);
                float ripple = pow(saturate(sin(x * 11.0 - depth * 39.0 + _Time.y * 6.5 + waves)), 6.0);
                half3 submerged = skin.rgb * half3(0.19, 0.39, 0.56) + half3(0.006, 0.022, 0.055);
                submerged += fixed3(0.16, 0.35, 0.47) * (edge * 0.65 + ripple * 0.12);
                return half4(submerged, skin.a * visibility);
            }
            ENDCG
        }
    }
}
