Shader "Tripple Gear/Atail Platform Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;

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
            };

            SpriteFragment Vertex(SpriteVertex input)
            {
                SpriteFragment result;
                result.position = UnityObjectToClipPos(input.position);
                result.uv = input.uv;
                result.color = input.color;
                return result;
            }

            fixed4 Fragment(SpriteFragment input) : SV_Target
            {
                input.color.a *= tex2D(_MainTex, input.uv).a;
                return input.color;
            }
            ENDCG
        }
    }
}
