Shader "Hidden/PixelSort"
{
    // Vertical pixel sort, one odd-even transposition step per draw.
    // Used by PixelSortFeature (custom Render Graph pass). Four passes:
    //   0 Copy       camera colour -> snapshot texture
    //   1 SortEven   one sort step, pairs (y, y+1) for even y
    //   2 SortOdd    one sort step, pairs (y, y+1) for odd y
    //   3 Composite  sorted texture drawn over the camera colour with opacity
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _SortSize;        // x = width, y = height of the sort textures (pixels)
        float4 _SortRange;       // x = lowest key that may be sorted, y = highest
        float  _KeyMode;         // 0 luminance, 1 brightness, 2 saturation, 3 hue, 4 red, 5 green, 6 blue
        float  _Dir;             // +1: dark pixels sink (towards y = 0), -1: bright pixels sink
        float  _ColumnVariation; // 0 = every column moves every frame, 1 = very uneven speeds
        float  _Seed;            // changes every frame
        float  _Opacity;         // composite opacity

        TEXTURE2D(_GradientTex); // 256x1 gradient, left = darkest, right = brightest
        float  _GradientStrength; // 0 = original colours, 1 = fully gradient-mapped

        float Hash11(float p)
        {
            p = frac(p * 0.1031);
            p *= p + 33.33;
            p *= p + p;
            return frac(p);
        }

        float Hash21(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        // Value the pixels are ordered by (and the range is applied to). Everything is 0..1.
        float SortKey(float3 c)
        {
            c = max(c, 0.0);
            float mx = max(c.r, max(c.g, c.b));
            float mn = min(c.r, min(c.g, c.b));

            if (_KeyMode < 0.5)  return sqrt(saturate(dot(c, float3(0.2126, 0.7152, 0.0722))));
            if (_KeyMode < 1.5)  return sqrt(saturate(mx));
            if (_KeyMode < 2.5)  return (mx - mn) / max(mx, 1e-4);
            if (_KeyMode < 3.5)
            {
                float d = mx - mn + 1e-5;
                float h = (mx == c.r) ? (c.g - c.b) / d
                        : (mx == c.g) ? (c.b - c.r) / d + 2.0
                        :               (c.r - c.g) / d + 4.0;
                return frac(h / 6.0);
            }
            if (_KeyMode < 4.5)  return sqrt(saturate(c.r));
            if (_KeyMode < 5.5)  return sqrt(saturate(c.g));
            return sqrt(saturate(c.b));
        }

        float4 SortStep(Varyings input, int parity)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            int2 px = int2(input.positionCS.xy);
            int2 size = int2(_SortSize.xy);
            float4 own = LOAD_TEXTURE2D_X(_BlitTexture, px);

            // Per-column speed variation. Both pixels of a pair share a column, so they
            // always agree on whether this column moves this frame.
            float colSpeed = lerp(1.0, Hash11((float)px.x), _ColumnVariation);
            if (Hash21(float2((float)px.x, _Seed)) > colSpeed)
                return own;

            // Pair (y, y+1) when (y + parity) is even, otherwise (y-1, y).
            bool isLower = (((px.y + parity) & 1) == 0);
            int partnerY = isLower ? px.y + 1 : px.y - 1;
            if (partnerY < 0 || partnerY >= size.y)
                return own;

            float4 other = LOAD_TEXTURE2D_X(_BlitTexture, int2(px.x, partnerY));

            float kOwn = SortKey(own.rgb);
            float kOther = SortKey(other.rgb);
            float kLow = isLower ? kOwn : kOther;   // key of the pixel at the lower row
            float kUp  = isLower ? kOther : kOwn;   // key of the pixel at the upper row

            // Only swap when BOTH pixels are inside the range (so the decision is symmetric),
            // and only when the pair is out of order. Strict comparison: ties never swap.
            bool inRange = kLow >= _SortRange.x && kLow <= _SortRange.y &&
                           kUp  >= _SortRange.x && kUp  <= _SortRange.y;
            bool outOfOrder = (_Dir > 0.0) ? (kLow > kUp) : (kLow < kUp);

            return (inRange && outOfOrder) ? other : own;
        }
        ENDHLSL

        Pass
        {
            Name "Copy"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy

            float4 FragCopy(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return float4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SortEven"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragEven

            float4 FragEven(Varyings input) : SV_Target { return SortStep(input, 0); }
            ENDHLSL
        }

        Pass
        {
            Name "SortOdd"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOdd

            float4 FragOdd(Varyings input) : SV_Target { return SortStep(input, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).rgb;

                // Gradient map: look the pixel's brightness up in the gradient.
                // Same perceptual luminance as the sort key, so mid-grey lands mid-gradient.
                if (_GradientStrength > 0.001)
                {
                    float g = sqrt(saturate(dot(max(c, 0.0), float3(0.2126, 0.7152, 0.0722))));
                    float u = (g * 255.0 + 0.5) / 256.0;   // centre of the matching texel
                    float3 mapped = SAMPLE_TEXTURE2D(_GradientTex, sampler_LinearClamp, float2(u, 0.5)).rgb;
                    c = lerp(c, mapped, saturate(_GradientStrength));
                }

                return float4(c, saturate(_Opacity));
            }
            ENDHLSL
        }
    }
}
