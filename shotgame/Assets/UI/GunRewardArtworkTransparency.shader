Shader "UI/Gun Reward Artwork Transparency"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 sampleColor = tex2D(_MainTex, input.uv);
                half brightness = max(sampleColor.r, max(sampleColor.g, sampleColor.b));
                #ifndef UNITY_COLORSPACE_GAMMA
                brightness = LinearToGammaSpaceExact(brightness);
                #endif
                sampleColor.a *= smoothstep(0.045, 0.14, brightness);
                return sampleColor * input.color;
            }
            ENDCG
        }
    }
}
