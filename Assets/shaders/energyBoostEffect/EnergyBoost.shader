Shader "Hidden/EnergyBoost"
{
    // Energy-boost pickup effect: zoom punch, flash, yellow tint, delayed zoomed "ghost" copy
    // with radial blur, and Sobel edge outlines.
    // Use with URP's Full Screen Pass Renderer Feature (Unity 2022.2+ / Unity 6).
    // All values are driven by EnergyBoostEffect.cs.
    Properties
    {
        _Zoom ("Zoom (1 = none)", Float) = 1
        _ZoomCenter ("Zoom Center", Vector) = (0.5, 0.5, 0, 0)

        _GhostZoom ("Ghost Zoom", Float) = 1
        _GhostOpacity ("Ghost Opacity", Range(0, 1)) = 0
        _GhostBlur ("Ghost Radial Blur", Float) = 0

        _Flash ("Flash", Range(0, 1)) = 0
        [HDR] _FlashColor ("Flash Color", Color) = (1, 0.93, 0.65, 1)

        _Tint ("Tint", Range(0, 1)) = 0
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.8, 0.25, 1)
        _TintDesaturate ("Tint Desaturate", Range(0, 1)) = 0.5

        _Darken ("Darken", Range(0, 1)) = 0

        _EdgeStrength ("Edge Strength", Float) = 0
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.85, 0.1, 1)
        _EdgeThreshold ("Edge Threshold", Float) = 0.15
        _EdgeSoftness ("Edge Softness", Float) = 0.1
        _EdgeWidth ("Edge Width (px)", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "EnergyBoost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define GHOST_SAMPLES 12   // radial blur quality (cost only paid while the ghost is visible)

            float  _Zoom;
            float4 _ZoomCenter;

            float  _GhostZoom;
            float  _GhostOpacity;
            float  _GhostBlur;       // 0..1, fraction of the distance to the centre that is smeared

            float  _Flash;
            float4 _FlashColor;

            float  _Tint;
            float4 _TintColor;
            float  _TintDesaturate;

            float  _Darken;

            float  _EdgeStrength;
            float4 _EdgeColor;
            float  _EdgeThreshold;
            float  _EdgeSoftness;
            float  _EdgeWidth;

            // Zoom in towards the centre (zoom > 1 magnifies).
            float2 ZoomUV(float2 uv, float zoom)
            {
                return (uv - _ZoomCenter.xy) / max(zoom, 0.01) + _ZoomCenter.xy;
            }

            float3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float Luma(float2 uv)
            {
                return dot(SampleScene(uv), float3(0.2126, 0.7152, 0.0722));
            }

            // Radial blur: averages samples lying between the pixel and the zoom centre.
            float3 RadialBlur(float2 zuv, float amount)
            {
                float2 dir = _ZoomCenter.xy - zuv;
                float3 sum = 0;
                [unroll]
                for (int i = 0; i < GHOST_SAMPLES; i++)
                {
                    float k = i / (float)(GHOST_SAMPLES - 1);
                    sum += SampleScene(zuv + dir * (amount * k));
                }
                return sum / GHOST_SAMPLES;
            }

            // Sobel operator on luminance. Returns gradient magnitude.
            float Sobel(float2 uv, float2 t)
            {
                float tl = Luma(uv + float2(-t.x,  t.y));
                float tc = Luma(uv + float2( 0.0,  t.y));
                float tr = Luma(uv + float2( t.x,  t.y));
                float ml = Luma(uv + float2(-t.x,  0.0));
                float mr = Luma(uv + float2( t.x,  0.0));
                float bl = Luma(uv + float2(-t.x, -t.y));
                float bc = Luma(uv + float2( 0.0, -t.y));
                float br = Luma(uv + float2( t.x, -t.y));

                float gx = -tl - 2.0 * ml - bl + tr + 2.0 * mr + br;
                float gy = -tl - 2.0 * tc - tr + bl + 2.0 * bc + br;
                return sqrt(gx * gx + gy * gy);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 1) Main image, zoomed
                float2 mainUV = ZoomUV(uv, _Zoom);
                float3 col = SampleScene(mainUV);

                // 2) Ghost copy: same frame, zoomed differently, radially blurred, laid on top
                if (_GhostOpacity > 0.001)
                {
                    float3 ghost = RadialBlur(ZoomUV(uv, _GhostZoom), _GhostBlur);
                    col = lerp(col, ghost, saturate(_GhostOpacity));
                }

                // 3) Yellow tint (desaturate a bit, then multiply by the tint colour)
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                float3 tinted = lerp(col, lum.xxx, _TintDesaturate) * _TintColor.rgb;
                col = lerp(col, tinted, saturate(_Tint));

                // 4) Exposure dip
                col *= (1.0 - saturate(_Darken));

                // 5) Flash
                col = lerp(col, _FlashColor.rgb, saturate(_Flash));

                // 6) Sobel outlines (added last so they stay bright after the other effects fade)
                if (_EdgeStrength > 0.001)
                {
                    float2 texel = _EdgeWidth / _ScreenParams.xy;
                    float e = Sobel(mainUV, texel);
                    e = smoothstep(_EdgeThreshold, _EdgeThreshold + max(_EdgeSoftness, 1e-4), e);
                    col += _EdgeColor.rgb * (e * _EdgeStrength);
                }

                return float4(max(col, 0), 1);
            }
            ENDHLSL
        }
    }
}
