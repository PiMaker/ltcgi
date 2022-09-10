#ifndef LTCGI_INCLUDED
#define LTCGI_INCLUDED

#include "LTCGI_config.cginc"
#include "LTCGI_uniform.cginc"
#include "LTCGI_functions.cginc"
#include "LTCGI_shadowmap.cginc"

#ifdef LTCGI_AUDIOLINK
#ifndef AUDIOLINK_WIDTH
#include "Assets/AudioLink/Shaders/AudioLink.cginc"
#define AUDIOLINK_CGINC_INCLUDED
#endif
#endif

#ifdef SHADER_TARGET_SURFACE_ANALYSIS
#define const  
#endif

// Main function - this calculates the approximated model for one pixel and one light
/* private */ float LTCGI_Evaluate(
    float3 Lw[4], float3 worldNorm, float3 viewDir, float3x3 Minv, uint i, float roughness, bool isTri,
    float2 uvStart, float2 uvEnd, const bool diffuse, ltcgi_flags flags, inout float3 color
) {
    // diffuse distance fade
    #ifdef LTCGI_DISTANCE_FADE_APPROX
        #ifdef LTCGI_DISTANCE_FADE_APPROX_ERROR_VISUALIZE
            bool distFadeError = false;
        #endif
        if (diffuse) // static branch, specular does not directly fade with distance
        {
            if (!flags.lmdOnly) {
                // very approximate lol
                float3 ctr = (Lw[0] + Lw[1])/2;
                float dist = length(ctr);
                if (dist > LTCGI_DISTANCE_FADE_APPROX_MULT)
                {
                    #ifdef LTCGI_DISTANCE_FADE_APPROX_ERROR_VISUALIZE
                        distFadeError = true;
                    #else
                        return 0;
                    #endif
                }
            }
        }
    #endif

    #define RET1_IF_LMDIFF [branch] if (/*const*/ diffuse && flags.diffFromLm) return 1;

    if (flags.colormode == LTCGI_COLORMODE_SINGLEUV) {
        float2 uv = uvStart;
        if (uv.x < 0) uv.xy = uv.yx;
        // TODO: make more configurable?
        #ifdef LTCGI_VISUALIZE_SAMPLE_UV
            color = float3(uv.xy, 0);
        #else
            color *= LTCGI_sample(LTCGI_inset_uv(uv), 1, flags.texindex, 0);
        #endif

        RET1_IF_LMDIFF
    }

    #ifdef LTCGI_AUDIOLINK
        if (flags.colormode == LTCGI_COLORMODE_AUDIOLINK) {
            float al = AudioLinkData(ALPASS_AUDIOLINK + uint2(0, flags.alBand)).r;
            color *= al;

            RET1_IF_LMDIFF
        }
    #endif

    // create LTC polygon array
    // note the order of source verts (keyword: winding order)
    float3 L[5];
    L[0] = mul(Minv, Lw[0]);
    L[1] = mul(Minv, Lw[1]);
    L[2] = isTri ? L[1] : mul(Minv, Lw[3]);
    L[3] = mul(Minv, Lw[2]);
    L[4] = 0;

    // get texture coords (before clipping!)
    [branch]
    if (flags.colormode == LTCGI_COLORMODE_TEXTURE) {
        float3 RN;
        float2 uv = LTCGI_calculateUV(i, flags, L, isTri, uvStart, uvEnd, RN);
        float planeAreaSquared = dot(RN, RN);
        float planeDistxPlaneArea = dot(RN, L[0]);

        float3 sampled;
        #ifdef LTCGI_VISUALIZE_SAMPLE_UV
            sampled = float3(uv.xy, 0);
        #else
            [branch]
            if (diffuse) { // static branch
                sampled =
                    LTCGI_sample(uv, 3, flags.texindex, 10) * 0.75 +
                    LTCGI_sample(uv, 3, flags.texindex, 100) * 0.25;
            } else {
                float d = abs(planeDistxPlaneArea) / planeAreaSquared;
                d *= LTCGI_UV_BLUR_DISTANCE;
                d = log(d) / log(3.0);

                // a rough material must never show a perfect reflection,
                // since our LOD0 texture is not prefiltered (and thus cannot
                // depict any blur correctly) - without this there is artifacting
                // on the border of LOD0 and LOD1
                d = clamp(d, saturate(roughness * 5.75), 1000);

                sampled = LTCGI_trilinear(uv, d, flags.texindex);
            }
        #endif

        // colorize output
        color *= sampled;
    }

    RET1_IF_LMDIFF
    #undef RET1_IF_LMDIFF

    int n;
    LTCGI_ClipQuadToHorizon(L, n);

    // early out if everything was clipped below horizon
    if (n == 0)
        return float3(0, 0, 0);

    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    L[3] = normalize(L[3]);
    L[4] = normalize(L[4]);

    // integrate (and pray that constant folding works well)
    float sum = 0;
    [unroll(5)]
    for (uint v = 0; v < max(3, (uint)n); v++) {
        float3 a = L[v];
        float3 b = L[(v + 1) % 5];
        sum += LTCGI_IntegrateEdge(a, b).z;
    }

    #ifdef LTCGI_DISTANCE_FADE_APPROX
    #ifdef LTCGI_DISTANCE_FADE_APPROX_ERROR_VISUALIZE
        if (diffuse && abs(sum) > 0.005 && distFadeError)
        {
            // debug distance fade failure cases
            color = float3(1, 0, 0);
            return 1;
        }
    #endif
    #endif

    // doublesided is accounted for with optimization at the start
    // return flags.doublesided ? abs(sum) : max(0, sum);
    return abs(sum);
}

// Calculate light contribution for all lights,
// call this from your shader and use the "diffuse" and "specular" outputs
// lmuv is the raw lightmap UV coordinate (e.g. UV1)
/* public */ void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse
#ifndef LTCGI_SPECULAR_OFF
    , inout half3 specular, out float totalSpecularIntensity
#endif
) {
    if (_LTCGI_GlobalDisable) {
        totalSpecularIntensity = 0;
        return;
    }

    // sample lookup tables
    float theta = acos(dot(worldNorm, viewDir));
    float2 uv = float2(roughness, theta/(0.5*UNITY_PI));
    uv = uv*LUT_SCALE + LUT_BIAS;

    #ifndef UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif

    // calculate LTCGI custom lightmap UV and sample
    float3 lms = LTCGI_SampleShadowmap(lmuv);

    #ifdef LTCGI_SHOW_SHADOWMAP
        diffuse += lms;
        totalSpecularIntensity = 0;
        return;
    #endif

    #ifdef LTCGI_SHOW_SHADOWMAP_UV
        diffuse = float3(lmuv.xy, 0);
        totalSpecularIntensity = 0;
        return;
    #endif

    // sample BDRF approximation from lookup texture
    float4 t = tex2Dlod(_LTCGI_lut1, float4(uv, 0, 0));
    float3x3 Minv = float3x3(
        float3(  1,   0, t.w),
        float3(  0, t.z,   0),
        float3(t.y,   0, t.x)
    );

    // construct orthonormal basis around N
    float3 T1, T2;
    T1 = normalize(viewDir - worldNorm*dot(viewDir, worldNorm));
    T2 = cross(worldNorm, T1);

    // for diffuse lighting we assume the identity matrix as BDRF, so the
    // LTC approximation is directly equivalent to the orthonormal rotation matrix
    float3x3 identityBrdf = float3x3(float3(T1), float3(T2), float3(worldNorm));
    // rotate area light in (T1, T2, N) basis for actual BRDF matrix as well
    Minv = mul(Minv, identityBrdf);

    // specular brightness
    #ifndef LTCGI_SPECULAR_OFF
        float spec_amp = tex2Dlod(_LTCGI_lut2, float4(uv, 0, 0)).x;
    #endif

    #ifdef LTCGI_VISUALIZE_SCREEN_COUNT
        uint ccc = 0;
    #endif

    #ifndef LTCGI_SPECULAR_OFF
        totalSpecularIntensity = 0;
    #endif

    bool noLm = false;
    #ifdef LTCGI_LTC_DIFFUSE_FALLBACK
    #ifndef SHADER_TARGET_SURFACE_ANALYSIS
        float2 lmSize;
        _LTCGI_Lightmap.GetDimensions(lmSize.x, lmSize.y);
        noLm = lmSize.x == 1;
    #endif
    #endif

    // loop through all lights and add them to the output
    uint count = min(_LTCGI_ScreenCount, MAX_SOURCES);
    [loop]
    for (uint i = 0; i < count; i++) {
        // skip masked and black lights
        if (_LTCGI_Mask[i]) continue;
        float4 extra = _LTCGI_ExtraData[i];
        float3 color = extra.rgb;
        if (!any(color)) continue;

        ltcgi_flags flags = ltcgi_parse_flags(asuint(extra.w), noLm);

        #ifdef LTCGI_TOGGLEABLE_SPEC_DIFF_OFF
            // compile branches below away statically
            flags.diffuse = flags.specular = true;
        #endif

        // calculate (shifted) world space positions
        float3 Lw[4];
        float2 uvStart = (float2)0, uvEnd = (float2)0;
        bool isTri = false;
        if (flags.lmdOnly) {
            Lw[0] = Lw[1] = Lw[2] = Lw[3] = (float3)0;
        } else {
            LTCGI_GetLw(i, flags, worldPos, Lw, uvStart, uvEnd, isTri);
        }

        // skip single-sided lights that face the other way
        if (!flags.doublesided) {
            float3 screenNorm = cross(Lw[1] - Lw[0], Lw[2] - Lw[0]);
            if (dot(screenNorm, Lw[0]) < 0)
                continue;
        }

        float lm = 1;
        if (flags.lmch) {
            lm = lms[flags.lmch - 1];
            if (lm < 0.001) continue;
        }

        #ifdef LTCGI_VISUALIZE_SCREEN_COUNT
            ccc++;
        #endif

        // diffuse lighting
        #ifndef LTCGI_DIFFUSE_OFF
            [branch]
            if (flags.diffuse)
            {
                float lmd = lm;
                if (flags.lmch) {
                    if (flags.diffFromLm)
                        lmd *= _LTCGI_LightmapMult[flags.lmch - 1];
                    else
                        lmd = smoothstep(0.0, LTCGI_SPECULAR_LIGHTMAP_STEP, saturate(lm - LTCGI_LIGHTMAP_CUTOFF));
                }
                float diff = LTCGI_Evaluate(Lw, worldNorm, viewDir, identityBrdf, i, roughness, isTri, uvStart, uvEnd, true, flags, color);
                diffuse += (diff * color * lmd);
            }
        #endif

        // specular lighting
        #ifndef LTCGI_SPECULAR_OFF
            [branch]
            if (flags.specular)
            {
                // reset color
                #ifdef LTCGI_ALLOW_HDR_SPECULAR
                    color = extra.rgb;
                #else
                    color = saturate(extra.rgb);
                #endif

                float spec = LTCGI_Evaluate(Lw, worldNorm, viewDir, Minv, i, roughness, isTri, uvStart, uvEnd, false, flags, color);
                spec *= spec_amp * smoothstep(0.0, LTCGI_SPECULAR_LIGHTMAP_STEP, saturate(lm - LTCGI_LIGHTMAP_CUTOFF));
                #ifndef LTCGI_SPECULAR_OFF
                    totalSpecularIntensity += spec;
                #endif
                specular += spec * color;
            }
        #endif
    }

    #ifdef LTCGI_VISUALIZE_SCREEN_COUNT
        diffuse = float3(ccc == 1, ccc == 2, ccc > 2);
    #endif
}

// COMPATIBILITY FALLBACKS

#ifdef LTCGI_SPECULAR_OFF

/* public */ void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse, inout half3 specular_UNUSED
) {
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse);
}

/* public */ void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse, inout half3 specular_UNUSED, out float totalSpecularIntensity
) {
    totalSpecularIntensity = 0;
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse);
}

#else

/* public */ void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse, inout half3 specular
) {
    float tsi;
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse, specular, tsi);
}

#endif

#endif

/*

Parts of the code in this file are adapted from the example code found here:
  
  https://github.com/selfshadow/ltc_code

Modifications by _pi_ (@pimaker on GitHub), licensed under the terms of the
MIT license as far as applicable.

Original copyright notice:

Copyright (c) 2017, Eric Heitz, Jonathan Dupuy, Stephen Hill and David Neubelt.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* If you use (or adapt) the source code in your own work, please include a 
  reference to the paper:

  Real-Time Polygonal-Light Shading with Linearly Transformed Cosines.
  Eric Heitz, Jonathan Dupuy, Stephen Hill and David Neubelt.
  ACM Transactions on Graphics (Proceedings of ACM SIGGRAPH 2016) 35(4), 2016.
  Project page: https://eheitzresearch.wordpress.com/415-2/

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/
