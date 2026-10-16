// General lit shader for everything Brine Time builds in code: flat colour, optional vertex
// colours (procedural fish), emission and double-sided rendering. Built-in render pipeline.
Shader "BrineTime/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Color;
        half _UseVertexColor;
        half _Glossiness;
        half _Metallic;
        fixed4 _EmissionColor;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 vc = lerp(fixed4(1,1,1,1), IN.color, _UseVertexColor);
            o.Albedo = _Color.rgb * vc.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
