// Scrolling energy graph drawn entirely in the fragment shader.
// Driven by EnergyGraph.cs (keep SAMPLES and GRID_SPAN in sync with it).
//
// Look by energy level (0-100, thresholds come from the component):
//   below baseline : grey, dim, thin line, no glow
//   baseline       : calm cyan
//   baseline..80   : line becomes more saturated
//   80..100        : danger fades in -> red tint, pulse, pixelation, red vignette, red grid
Shader "UI/EnergyGraph"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _LineWidth ("Line width (% of panel height)", Range(0.5, 5)) = 1.8

        // Driven by EnergyGraph.cs
        [HideInInspector] _Scroll ("Scroll", Float) = 0
        [HideInInspector] _GridPhase ("Grid phase", Float) = 0
        [HideInInspector] _Dull ("Dull", Float) = 0
        [HideInInspector] _Danger ("Danger", Float) = 0
        [HideInInspector] _Baseline ("Baseline (0-1)", Float) = 0.2
        [HideInInspector] _DangerLine ("Danger start (0-1)", Float) = 0.8
        [HideInInspector] _RectPx ("Rect size (px)", Vector) = (360, 140, 0, 0)

        // UI mask support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define SAMPLES 64      // must match EnergyGraph.SampleCount
            #define GRID_SPAN 6.0   // must match EnergyGraph GridSpanSamples

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Samples[SAMPLES];   // normalised 0-1, oldest first, newest last
            float _Scroll;
            float _GridPhase;
            float _Dull;
            float _Danger;
            float _Baseline;
            float _DangerLine;
            float _LineWidth;
            float4 _RectPx;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            // Straight-alpha "over" compositing
            void Over(inout float3 col, inout float a, float3 src, float srcA)
            {
                srcA = saturate(srcA);
                float outA = srcA + a * (1.0 - srcA);
                col = (src * srcA + col * a * (1.0 - srcA)) / max(outA, 1e-5);
                a = outA;
            }

            float Lum(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float3 Desat(float3 c, float amount)
            {
                return lerp(Lum(c).xxx, c, amount);
            }

            // Colour of the graph at a given (normalised) energy value
            float3 EnergyColor(float v)
            {
                const float3 grey  = float3(0.50, 0.55, 0.62);
                const float3 calm  = float3(0.20, 0.85, 0.95);
                const float3 vivid = float3(0.05, 1.00, 0.70);
                const float3 warn  = float3(1.00, 0.72, 0.12);
                const float3 hot   = float3(1.00, 0.10, 0.08);

                float3 c = lerp(grey, calm, smoothstep(0.0, _Baseline, v));
                c = lerp(c, vivid, smoothstep(_Baseline, _DangerLine, v));
                float t = smoothstep(_DangerLine, 1.0, v);
                c = lerp(c, warn, smoothstep(0.0, 0.5, t));
                c = lerp(c, hot, smoothstep(0.5, 1.0, t));
                return c;
            }

            float RoundBoxSDF(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 sz = _RectPx.xy;
                float2 P = i.uv * sz;                 // pixel position inside the panel, origin bottom-left
                float danger = saturate(_Danger);
                float dull = saturate(_Dull);

                // Danger pulse (gets faster as danger rises)
                float pulseRate = lerp(1.2, 3.5, danger);
                float pulse = 0.5 + 0.5 * sin(_Time.y * pulseRate * 6.2831853);
                pulse *= pulse;

                // Pixelation: block size grows with danger
                float block = max(1.0, floor(lerp(1.0, sz.y / 20.0, danger)));
                float2 Pq = (floor(P / block) + 0.5) * block;

                // Layout
                float padY = sz.y * 0.08;
                float usable = sz.y - 2.0 * padY;
                float stepCount = SAMPLES - 2.0;
                float stepPx = sz.x / stepCount;
                float headX = sz.x - sz.y * 0.09;
                float lineW = max(_LineWidth * 0.01 * sz.y * lerp(1.0, 0.7, dull) * (1.0 + 0.5 * pulse * danger),
                                  block * 1.1);

                // Walk the polyline: nearest distance, local value, curve height at this column, head value.
                // Sample k sits at x = W - (stepCount - k + scroll) * stepPx, so the newest point enters
                // from the right edge and the whole graph scrolls smoothly between pushes.
                float minD = 1e9;
                float bestV = 0.0;
                float curveV = -1.0;
                float headV = 0.0;

                [loop]
                for (int k = 0; k < SAMPLES - 1; k++)
                {
                    float sa = _Samples[k];
                    float sb = _Samples[k + 1];
                    float xa = sz.x - (stepCount - k + _Scroll) * stepPx;
                    float xb = xa + stepPx;
                    float2 A = float2(xa, padY + sa * usable);
                    float2 B = float2(xb, padY + sb * usable);

                    float2 pa = Pq - A;
                    float2 ba = B - A;
                    float h = saturate(dot(pa, ba) / dot(ba, ba));
                    float d = length(pa - ba * h);
                    if (d < minD)
                    {
                        minD = d;
                        bestV = lerp(sa, sb, h);
                    }

                    float f = (Pq.x - xa) / stepPx;
                    if (f >= 0.0 && f <= 1.0) curveV = lerp(sa, sb, f);

                    float fh = (headX - xa) / stepPx;
                    if (fh >= 0.0 && fh <= 1.0) headV = lerp(sa, sb, fh);
                }

                float3 col = float3(0, 0, 0);
                float a = 0.0;

                // 1. Panel background
                float3 bg = lerp(float3(0.025, 0.04, 0.07), float3(0.045, 0.045, 0.05), dull);
                bg = lerp(bg, float3(0.16, 0.02, 0.03), danger * (0.35 + 0.35 * pulse));
                Over(col, a, bg, 0.78);

                // 2. Grid (vertical lines scroll with the data: one line every GRID_SPAN samples;
                //    horizontal lines every 20% of the range)
                float cellX = stepPx * GRID_SPAN;
                float gx = Pq.x + _GridPhase * stepPx;
                float dx = abs(frac(gx / cellX + 0.5) - 0.5) * cellX;
                float cellY = usable * 0.2;
                float dy = abs(frac((Pq.y - padY) / cellY + 0.5) - 0.5) * cellY;
                float gw = max(1.0, block);
                float gridCov = max(saturate(0.5 + 0.5 * gw - dx), saturate(0.5 + 0.5 * gw - dy));
                float3 gridCol = lerp(float3(0.55, 0.75, 0.95), float3(0.6, 0.62, 0.65), dull);
                Over(col, a, gridCol, gridCov * 0.07);

                // 3. Danger band above the danger threshold
                float yD = padY + _DangerLine * usable;
                float yB = padY + _Baseline * usable;
                float bandGrad = saturate((Pq.y - yD) / max(sz.y - yD, 1.0));
                float inBand = step(yD, Pq.y);
                Over(col, a, float3(1.0, 0.1, 0.1), inBand * danger * (0.10 + 0.10 * pulse) * (0.4 + 0.6 * bandGrad));

                // Threshold markers: dashed danger line, dotted baseline
                float dashLen = max(6.0, block * 2.0);
                float dashD = step(0.5, frac(Pq.x / dashLen));
                float covD = saturate(0.5 + 0.5 * gw - abs(Pq.y - yD));
                Over(col, a, lerp(float3(0.7, 0.75, 0.8), float3(1.0, 0.2, 0.15), danger), covD * dashD * (0.18 + 0.6 * danger));
                float dotLen = max(3.0, block * 1.0);
                float dotB = step(0.5, frac(Pq.x / dotLen));
                float covB = saturate(0.5 + 0.5 * gw - abs(Pq.y - yB));
                Over(col, a, float3(0.7, 0.8, 0.9), covB * dotB * 0.22);

                // 4. Area under the curve
                if (curveV >= 0.0)
                {
                    float yc = padY + curveV * usable;
                    if (Pq.y < yc)
                    {
                        float fall = saturate(Pq.y / max(yc, 1.0));
                        float3 ac = Desat(EnergyColor(curveV), 1.0 - 0.75 * dull);
                        Over(col, a, ac, 0.32 * fall * fall * (1.0 - 0.5 * dull));
                    }
                }

                // 5. Red vignette + red grid masked by it (danger only)
                float2 cc = (Pq / sz - 0.5) * 2.0;
                float vig = smoothstep(0.30, 1.15, length(cc));
                Over(col, a, float3(0.95, 0.04, 0.05), vig * danger * (0.45 + 0.35 * pulse));
                Over(col, a, float3(1.0, 0.25, 0.25), gridCov * vig * danger * (0.55 + 0.45 * pulse));

                // 6. Line: glow, body, hot core
                float3 lc = Desat(EnergyColor(bestV), 1.0 - 0.75 * dull);
                float glow = exp(-minD / (sz.y * 0.035)) * 0.45 * (1.0 - dull) * (0.7 + 0.3 * pulse * danger);
                Over(col, a, lc, glow);
                float cov = saturate(0.5 + 0.5 * lineW - minD);
                Over(col, a, saturate(lc * (1.0 + 0.35 * pulse * danger)), cov * lerp(1.0, 0.65, dull));
                float core = saturate(0.5 + 0.5 * lineW * 0.35 - minD);
                Over(col, a, lerp(lc, float3(1, 1, 1), 0.65), core * (1.0 - dull) * 0.9);

                // 7. Head marker with a ring that expands once per sample
                float yH = padY + headV * usable;
                float dh = length(Pq - float2(headX, yH));
                float3 hc = Desat(EnergyColor(headV), 1.0 - 0.75 * dull);
                float r = lineW * 1.7;
                float ringR = r + _Scroll * sz.y * 0.07;
                Over(col, a, hc, saturate(1.0 - abs(dh - ringR)) * (1.0 - _Scroll) * 0.6 * (1.0 - 0.5 * dull));
                Over(col, a, lerp(hc, float3(1, 1, 1), 0.5), saturate(0.5 + r - dh));

                // 8. Rounded panel shape + border
                float2 halfSz = sz * 0.5;
                float rad = sz.y * 0.07;
                float sdf = RoundBoxSDF(P - halfSz, halfSz, rad);
                float shape = saturate(0.5 - sdf);
                float bw = max(1.0, sz.y * 0.012);
                float borderCov = saturate(0.5 + 0.5 * bw - abs(sdf + 0.5 * bw));
                float3 bc = lerp(float3(0.45, 0.8, 0.95), float3(1.0, 0.15, 0.15), danger);
                bc = Desat(bc, 1.0 - 0.7 * dull);
                Over(col, a, bc, borderCov * (0.35 + 0.65 * danger * (0.5 + 0.5 * pulse)));

                float4 outc;
                outc.rgb = col * i.color.rgb;
                outc.a = a * shape * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outc.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outc.a - 0.001);
                #endif

                return outc;
            }
            ENDCG
        }
    }
}
