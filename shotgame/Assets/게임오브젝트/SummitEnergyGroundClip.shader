Shader "Tripple Gear/Summit Energy Ground Clip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GroundY ("Ground Height", Float) = 0
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

            sampler2D _MainTex;
            float _GroundY;

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float worldY : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.worldY = mul(unity_ObjectToWorld, input.position).y;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                clip(input.worldY - _GroundY);
                return tex2D(_MainTex, input.uv) * input.color;
            }
            ENDCG
        }
    }
}
