// Animated ocean. Uses exactly the same two-sine wave as WaterSurface.HeightAt on the CPU,
// driven by the global _BrineWaveTime, so floating objects ride the waves you see.
Shader "BrineTime/Water"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.03, 0.22, 0.38, 1)
        _ShallowColor ("Shallow", Color) = (0.12, 0.62, 0.68, 1)
        _FoamColor ("Foam", Color) = (0.9, 0.97, 1, 1)
        _Dir1 ("Dir1", Vector) = (0.857, 0.514, 0, 0)
        _Dir2 ("Dir2", Vector) = (-0.371, 0.928, 0, 0)
        _Freq1 ("Freq1", Float) = 0.35
        _Freq2 ("Freq2", Float) = 0.8
        _Speed1 ("Speed1", Float) = 1.1
        _Speed2 ("Speed2", Float) = 1.7
        _Amp1 ("Amp1", Float) = 0.22
        _Amp2 ("Amp2", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert
        #pragma target 3.0

        fixed4 _DeepColor, _ShallowColor, _FoamColor;
        float4 _Dir1, _Dir2;
        float _Freq1, _Freq2, _Speed1, _Speed2, _Amp1, _Amp2;
        float _BrineWaveTime;

        struct Input
        {
            float waveT;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float a1 = dot(_Dir1.xy, wp.xz) * _Freq1 + _BrineWaveTime * _Speed1;
            float a2 = dot(_Dir2.xy, wp.xz) * _Freq2 + _BrineWaveTime * _Speed2;
            float h = _Amp1 * sin(a1) + _Amp2 * sin(a2);
            v.vertex.y += h;
            float2 grad = _Dir1.xy * (_Amp1 * _Freq1 * cos(a1)) + _Dir2.xy * (_Amp2 * _Freq2 * cos(a2));
            v.normal = normalize(float3(-grad.x, 1.0, -grad.y));
            o.waveT = saturate((h + _Amp1 + _Amp2) / (2.0 * (_Amp1 + _Amp2)));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, IN.waveT);
            float crest = smoothstep(0.82, 0.98, IN.waveT);
            o.Albedo = lerp(col, _FoamColor.rgb, crest * 0.55);
            o.Smoothness = 0.9;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
