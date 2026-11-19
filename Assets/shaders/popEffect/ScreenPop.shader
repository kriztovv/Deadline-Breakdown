Shader "Hidden/ScreenPop"
{
    // Use with URP's Full Screen Pass Renderer Feature (Unity 2022.2+ / Unity 6).
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ScreenPop"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;    // size of one "pixel" in screen pixels. 1 = no pixelation
            float _Saturation;   // 0 = unchanged, 1 = double saturation, -1 = grayscale
            float _Fisheye;      // 0 = none, ~0.5 = strong bulge

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 1) Fisheye / barrel bulge (centre magnified)
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                centered *= 1.0 - _Fisheye * r2;
                uv = centered * 0.5 + 0.5;

                // 2) Pixelate (in screen-pixel units so it's resolution independent in feel)
                if (_PixelSize > 1.001)
                {
                    float2 res = _ScreenParams.xy;
                    uv = (floor(uv * res / _PixelSize) + 0.5) * _PixelSize / res;
                }

                // Avoid smearing the border when the fisheye pulls UVs outside
                uv = saturate(uv);

                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                // 3) Saturation
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(lum.xxx, col, 1.0 + _Saturation);

                return float4(max(col, 0), 1);
            }
            ENDHLSL
        }
    }
}
