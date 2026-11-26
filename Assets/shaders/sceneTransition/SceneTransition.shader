Shader "Hidden/SceneTransition"
{
    // Zoom-whip transition: push-in zoom, radial (zoom) blur, sideways whip blur,
    // chromatic aberration and an optional flash. Driven by SceneTransition.cs.
    // Use with URP's Full Screen Pass Renderer Feature (Unity 6).
    Properties
    {
        _Zoom ("Zoom (1 = none)", Float) = 1
        _ZoomBlur ("Radial blur", Float) = 0
        _Whip ("Whip blur", Float) = 0
        _WhipDir ("Whip direction", Vector) = (-1, 0, 0, 0)
        _Chroma ("Chromatic aberration", Float) = 0
        _Center ("Zoom centre", Vector) = (0.5, 0.5, 0, 0)
        _Flash ("Flash", Range(0, 1)) = 0
        [HDR] _FlashColor ("Flash colour", Color) = (1, 1, 1, 1)
        _Samples ("Blur samples", Float) = 24
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "SceneTransition"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Zoom;
            float  _ZoomBlur;    // 0..1: fraction of the distance to the centre that gets smeared
            float  _Whip;        // sideways smear length in UV units
            float4 _WhipDir;     // xy = normalized direction of the whip
            float  _Chroma;      // red/blue radial scale offset (0.03 = clearly visible fringes)
            float4 _Center;      // xy = zoom centre in UV
            float  _Flash;
            float4 _FlashColor;
            float  _Samples;

            // Mirrors UVs that leave the screen, so zoom/whip never smear the border pixels.
            float2 MirrorUV(float2 uv)
            {
                return 1.0 - abs(1.0 - fmod(abs(uv), 2.0));
            }

            float3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, MirrorUV(uv)).rgb;
            }

            // Interleaved gradient noise: a different 0..1 value per pixel. Offsetting the blur
            // samples by it turns visible banding into fine grain.
            float Jitter(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 center = _Center.xy;

                bool blurred = (_ZoomBlur > 0.0005) || (_Whip > 0.0005);
                bool split   = abs(_Chroma) > 0.0005;
                bool zoomed  = abs(_Zoom - 1.0) > 0.0005;

                // Idle: plain copy of the frame.
                if (!blurred && !split && !zoomed && _Flash < 0.0005)
                    return float4(SampleScene(uv), 1.0);

                int samples = blurred ? clamp((int)_Samples, 2, 64) : 1;
                float jitter = (samples > 1) ? Jitter(input.positionCS.xy) : 0.5;

                // Push-in zoom: look at a smaller area around the centre.
                float2 dir = (uv - center) / max(_Zoom, 0.01);

                float3 sum = 0;
                [loop]
                for (int i = 0; i < samples; i++)
                {
                    float k = (samples > 1) ? (i + jitter) / samples : 0.0;   // 0..1 along the blur

                    // Radial blur: pull samples towards the centre. Whip blur: slide them sideways.
                    float2 p = center + dir * (1.0 - _ZoomBlur * k)
                                      + _WhipDir.xy * (_Whip * (k - 0.5));

                    if (split)
                    {
                        // Chromatic aberration: red and blue are sampled at slightly different zooms.
                        float2 off = p - center;
                        sum += float3(SampleScene(center + off * (1.0 + _Chroma)).r,
                                      SampleScene(p).g,
                                      SampleScene(center + off * (1.0 - _Chroma)).b);
                    }
                    else
                    {
                        sum += SampleScene(p);
                    }
                }
                float3 col = sum / samples;

                col = lerp(col, _FlashColor.rgb, saturate(_Flash));
                return float4(max(col, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
}
