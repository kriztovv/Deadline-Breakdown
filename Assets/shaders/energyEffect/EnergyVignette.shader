Shader "Hidden/EnergyVignette"
{
    // Use with URP's Full Screen Pass Renderer Feature (Unity 2022.2+ / Unity 6).
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "EnergyVignette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Saturation;         // 0 = unchanged, >0 more saturated, <0 less
            float  _Fisheye;            // 0 = none, >0 bulge, <0 pinch
            float  _VignetteIntensity;  // 0..1 (already includes the heartbeat pulse)
            float  _VignetteInner;      // radius (0..1) where the vignette starts fading in
            float4 _VignetteColor;      // rgb = colour, a = max opacity

            float  _HueShift;           // in turns (1 = full rotation around the colour wheel)
            float  _PixelSize;          // size of one "pixel" in screen pixels. 1 = no pixelation

            float4 _GridColor;          // rgb = line colour, a = max opacity
            float  _GridIntensity;      // 0..1 overall grid strength
            float  _GridInner;          // radius where the grid mask starts (smaller than _VignetteInner)
            float  _GridScale;          // number of cells across the screen height
            float  _GridThickness;      // line half-width in cell units (0.02 = thin)
            float2 _GridOffset;         // scroll offset in cells (accumulated by the script)

            // Rotates colour around the grey axis (1,1,1).
            float3 HueShift(float3 c, float turns)
            {
                float a = turns * 6.28318531;
                float s = sin(a);
                float co = cos(a);
                const float3 k = float3(0.57735026, 0.57735026, 0.57735026);
                return c * co + cross(k, c) * s + k * dot(k, c) * (1.0 - co);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Fisheye / barrel distortion
                float2 c = input.texcoord * 2.0 - 1.0;
                float r2 = dot(c, c);
                float2 d = c * (1.0 - _Fisheye * r2);
                float2 uv = saturate(d * 0.5 + 0.5);

                // Pixelate (used by the periodic pop)
                if (_PixelSize > 1.001)
                {
                    float2 res = _ScreenParams.xy;
                    uv = (floor(uv * res / _PixelSize) + 0.5) * _PixelSize / res;
                }

                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // Hue shift (scene only, overlays below keep their own colours)
                col = HueShift(col, _HueShift);

                // Saturation
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(lum.xxx, col, max(0.0, 1.0 + _Saturation));

                // Vignette (radius 1 = screen corners)
                float r = length(c) * 0.70710678;
                float v = smoothstep(_VignetteInner, 1.0, r) * _VignetteIntensity * _VignetteColor.a;
                col = lerp(col, _VignetteColor.rgb, saturate(v));

                // Scrolling square grid, masked by a slightly larger vignette
                if (_GridIntensity > 0.0001)
                {
                    float aspect = _ScreenParams.x / _ScreenParams.y;
                    float2 p = (input.texcoord - 0.5) * float2(aspect, 1.0) * _GridScale + _GridOffset;

                    float2 dist = 0.5 - abs(frac(p) - 0.5);   // distance to the nearest cell edge, per axis
                    float2 aa = max(fwidth(p), 1e-4);          // keeps lines crisp at any resolution
                    float2 l = 1.0 - smoothstep(_GridThickness, _GridThickness + aa, dist);
                    float lines = max(l.x, l.y);

                    float gridMask = smoothstep(_GridInner, 1.0, r) * _GridIntensity;
                    col = lerp(col, _GridColor.rgb, saturate(lines * gridMask * _GridColor.a));
                }

                return float4(max(col, 0), 1);
            }
            ENDHLSL
        }
    }
}
