// Gix – Star Billboard Shader
// Renders stars as billboarded quads on the celestial sphere.
// Supports GPU instancing (_Color per instance).
// Suitable for both bright (LOD 0) and dim (LOD 1) star rendering.

Shader "Gix/StarBillboard"
{
    Properties
    {
        _MainTex   ("Star Sprite", 2D)    = "white" {}
        _Color     ("Tint Color", Color)  = (1,1,1,1)
        _Brightness("Brightness",  Float) = 1.0
        _Softness  ("Edge Softness", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend  SrcAlpha One            // Additive blending for star glow
        ZWrite Off
        Cull   Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO

                // Per-instance color passed from vertex to fragment
                fixed4 color : COLOR0;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _Brightness;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * _Brightness;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Soft circular alpha mask: fades at edges for natural star appearance
                float2 uv = i.uv - 0.5;
                float  d  = dot(uv, uv) * 4.0; // 0 at centre, 1 at edge
                float  alpha = tex.a * smoothstep(1.0, 1.0 - _Softness, d);

                fixed4 col = i.color;
                col.a      = alpha;
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
