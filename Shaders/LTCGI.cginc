#ifndef LTCGI_INCLUDED
#define LTCGI_INCLUDED

#include "LTCGI_config.cginc"

#ifdef LTCGI_AVATAR_MODE
    #undef LTCGI_STATIC_UNIFORMS
    #undef LTCGI_BICUBIC_LIGHTMAP
    #define LTCGI_ALWAYS_LTC_DIFFUSE
    // for perf and locality don't allow cylinders on avatars for now (it probably would be misdetected anyway)
    #undef LTCGI_CYLINDER
#endif

#ifdef LTCGI_TOGGLEABLE_SPEC_DIFF_OFF
    #undef LTCGI_DIFFUSE_OFF
    #undef LTCGI_SPECULAR_OFF
#endif

#if defined(LTCGI_V2_CUSTOM_INPUT) || defined(LTCGI_V2_DIFFUSE_CALLBACK) || defined(LTCGI_V2_SPECULAR_CALLBACK)
    #define LTCGI_API_V2
#endif

#include "LTCGI_structs.cginc"
#include "LTCGI_uniform.cginc"
#include "LTCGI_functions.cginc"
#include "LTCGI_shadowmap.cginc"

#ifdef SHADER_TARGET_SURFACE_ANALYSIS
#define const  
#endif

// Main function - this calculates the approximated model for one pixel and one light
void LTCGI_Evaluate(ltcgi_input input, float3 worldNorm, float3 viewDir, float3x3 Minv, float roughness, const bool diffuse, out ltcgi_output output) {
    output.input = input;
    output.color = input.rawColor; // copy for colormode static
    output.intensity = 0;

    // diffuse distance fade
    #ifdef LTCGI_DISTANCE_FADE_APPROX
        if (diffuse) // static branch, specular does not directly fade with distance
        {
            if (!input.flags.lmdOnly) {
                // very approximate lol
                float3 ctr = (input.Lw[0] + input.Lw[1]) * 0.5f;
                if (dot(ctr, ctr) > LTCGI_DISTANCE_FADE_APPROX_MULT * LTCGI_DISTANCE_FADE_APPROX_MULT)
                {
                    return;
                }
            }
        }
    #endif

    #define RET1_IF_LMDIFF [branch] if (/*const*/ diffuse && input.flags.diffFromLm) { output.intensity = 1.0f; return; }

    [branch]
    if (input.flags.colormode == LTCGI_COLORMODE_SINGLEUV) {
        float2 uv = input.uvStart;
        if (uv.x < 0) uv.xy = uv.yx;
        // TODO: make more configurable?
        #ifdef LTCGI_VISUALIZE_SAMPLE_UV
            output.color = float3(uv.xy, 0);
        #elif !defined(SHADER_TARGET_SURFACE_ANALYSIS)
            // sample video texture directly for accuracy
            float3 sampled = _Udon_LTCGI_Texture_LOD0.SampleLevel(LTCGI_SAMPLER, uv.xy, 0).rgb;
            output.color *= sampled;
        #endif

        RET1_IF_LMDIFF
    }

    #ifdef LTCGI_AUDIOLINK
        [branch]
        if (input.flags.colormode == LTCGI_COLORMODE_AUDIOLINK) {
            float al = AudioLinkData(ALPASS_AUDIOLINK + uint2(0, input.flags.alBand)).r;
            output.color *= al;

            RET1_IF_LMDIFF
        }
    #endif

    // create LTC polygon array
    // note the order of source verts (keyword: winding order)
    float3 L[5];
    L[0] = mul(Minv, input.Lw[0]);
    L[1] = mul(Minv, input.Lw[1]);
    L[2] = input.isTri ? L[1] : mul(Minv, input.Lw[3]);
    L[3] = mul(Minv, input.Lw[2]);
    L[4] = 0;

    // get texture coords (before clipping!)
    [branch]
    if (input.flags.colormode == LTCGI_COLORMODE_TEXTURE) {
        float3 RN;
        float2 uv = LTCGI_calculateUV(input.i, input.flags, L, input.isTri, input.uvStart, input.uvEnd, RN);
        float planeAreaSquared = dot(RN, RN);
        float planeDistxPlaneArea = dot(RN, L[0]);

        float3 sampled;
        [branch]
        if (diffuse) { // static branch
            #ifdef LTCGI_BLENDED_DIFFUSE_SAMPLING
                float3 sampled1;
                LTCGI_sample(uv, 3, input.flags.texindex, 10, sampled1);
                float3 sampled2;
                LTCGI_sample(uv, 3, input.flags.texindex, 100, sampled2);
                sampled =
                    sampled1 * 0.75 +
                    sampled2 * 0.25;
            #else
                LTCGI_sample(uv, 3, input.flags.texindex, 10, sampled);
            #endif
        } else {
            float d = abs(planeDistxPlaneArea) / planeAreaSquared;
            d *= LTCGI_UV_BLUR_DISTANCE;
            d = log(d) / log(3.0);

            // a rough material must never show a perfect reflection,
            // since our LOD0 texture is not prefiltered (and thus cannot
            // depict any blur correctly) - without this there is artifacting
            // on the border of LOD0 and LOD1
            d = clamp(d, saturate(roughness * 5.75), 1000);

            LTCGI_trilinear(uv, d, input.flags.texindex, sampled);
        }

        // colorize output
        output.color *= sampled;
    }

    RET1_IF_LMDIFF
    #undef RET1_IF_LMDIFF

    int n;
    LTCGI_ClipQuadToHorizon(L, n);

    // early out if everything was clipped below horizon
    [branch]
    if (n == 0)
        return;

    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    L[3] = normalize(L[3]);

    // integrate
    float sum = 0;
    sum += LTCGI_IntegrateEdge(L[0], L[1]).z;
    sum += LTCGI_IntegrateEdge(L[1], L[2]).z;
    sum += LTCGI_IntegrateEdge(L[2], L[3]).z;
    [branch]
    if (n >= 4)
    {
        L[4] = normalize(L[4]);
        sum += LTCGI_IntegrateEdge(L[3], L[4]).z;
        [branch]
        if (n == 5)
            sum += LTCGI_IntegrateEdge(L[4], L[0]).z;
    }

    // doublesided is accounted for with optimization at the start, so return abs
    output.intensity = abs(sum);
    return;
}

// Calculate light contribution for all lights,
// call this from your shader and use the "diffuse" and "specular" outputs
// lmuv is the raw lightmap UV coordinate (e.g. UV1)
void LTCGI_Contribution(
#ifdef LTCGI_API_V2
    inout LTCGI_V2_CUSTOM_INPUT data,
#endif
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv
#ifndef LTCGI_API_V2
    , inout half3 diffuse, inout half3 specular, out float totalSpecularIntensity, out float totalDiffuseIntensity
#endif
) {
    #ifndef LTCGI_API_V2
        totalSpecularIntensity = 0;
        totalDiffuseIntensity = 0;
    #endif

    #ifdef LTCGI_SPECULAR_OFF
        specular = 0;
    #endif
    #ifdef LTCGI_DIFFUSE_OFF
        diffuse = 0;
    #endif

    [branch]
    if (_Udon_LTCGI_GlobalEnable == 0.0f) {
        return;
    }

    // sample lookup tables
    float theta = LTCGI_acos_fast(dot(worldNorm, viewDir));
    float2 uv = float2(roughness, theta/(0.5*UNITY_PI));
    uv = uv*LUT_SCALE + LUT_BIAS;

    // calculate LTCGI custom lightmap UV and sample
    float3 lms = LTCGI_SampleShadowmap(lmuv);

    #ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
    // sample BDRF approximation from lookup texture
    float4 t = _Udon_LTCGI_lut1.SampleLevel(LTCGI_SAMPLER, uv, 0);
    #endif
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
    float spec_amp = 1.0f;
    #ifndef LTCGI_SPECULAR_OFF
    #ifndef LTCGI_DISABLE_LUT2
    #ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
        spec_amp = _Udon_LTCGI_lut2.SampleLevel(LTCGI_SAMPLER, uv, 0).x;
    #endif
    #endif
    #endif

    bool noLm = false;
    #ifdef LTCGI_LTC_DIFFUSE_FALLBACK
    #ifndef LTCGI_ALWAYS_LTC_DIFFUSE
    #ifndef SHADER_TARGET_SURFACE_ANALYSIS
        float2 lmSize;
        _Udon_LTCGI_Lightmap.GetDimensions(lmSize.x, lmSize.y);
        noLm = lmSize.x == 1;
    #endif
    #endif
    #endif
    #ifdef LTCGI_ALWAYS_LTC_DIFFUSE
        noLm = true;
    #endif

    // loop through all lights and add them to the output
#if MAX_SOURCES != 1
    uint count = min(_Udon_LTCGI_ScreenCount, MAX_SOURCES);
    [loop]
#else
    // mobile config
    const uint count = 1;
    [unroll(1)]
#endif
    for (uint i = 0; i < count; i++) {
        // skip masked and black lights
        if (_Udon_LTCGI_Mask[i]) continue;
        float4 extra = _Udon_LTCGI_ExtraData[i];
        float3 color = extra.rgb;
        if (!any(color)) continue;

        ltcgi_flags flags = ltcgi_parse_flags(asuint(extra.w), noLm);
        
        #ifdef LTCGI_ALWAYS_LTC_DIFFUSE
            // can't honor a lightmap-only light in this mode
            if (flags.lmdOnly) continue;
        #endif

        #ifdef LTCGI_TOGGLEABLE_SPEC_DIFF_OFF
            // compile branches below away statically
            flags.diffuse = flags.specular = true;
        #endif

        // calculate (shifted) world space positions
        float3 Lw[4];
        float4 uvStart = (float4)0, uvEnd = (float4)0;
        bool isTri = false;
        if (flags.lmdOnly) {
            Lw[0] = Lw[1] = Lw[2] = Lw[3] = (float3)0;
        } else {
            LTCGI_GetLw(i, flags, worldPos, Lw, uvStart, uvEnd, isTri);
        }

        // skip single-sided lights that face the other way
        float3 screenNorm = cross(Lw[1] - Lw[0], Lw[2] - Lw[0]);
        if (!flags.doublesided) {
            if (dot(screenNorm, Lw[0]) < 0)
                continue;
        }

        float lm = 1;
        if (flags.lmch) {
            lm = lms[flags.lmch - 1];
            if (lm < 0.001) continue;
        }

        ltcgi_input input;
        input.i = i;
        input.Lw = Lw;
        input.isTri = isTri;
        input.uvStart = uvStart;
        input.uvEnd = uvEnd;
        input.rawColor = color;
        input.flags = flags;
        input.screenNormal = screenNorm;

        // diffuse lighting
        #ifndef LTCGI_DIFFUSE_OFF
            [branch]
            if (flags.diffuse)
            {
                float lmd = lm;
                if (flags.lmch) {
                    if (flags.diffFromLm)
                        lmd *= _Udon_LTCGI_LightmapMult[flags.lmch - 1];
                    else
                        lmd = smoothstep(0.0, LTCGI_SPECULAR_LIGHTMAP_STEP, saturate(lm - LTCGI_LIGHTMAP_CUTOFF));
                }
                ltcgi_output diff;
                diff.color = 0;
                LTCGI_Evaluate(input, worldNorm, viewDir, identityBrdf, roughness, true, diff);
                diff.intensity *= lmd;

                #ifdef LTCGI_API_V2
                    LTCGI_V2_DIFFUSE_CALLBACK(data, diff);
                #else
                    // simply accumulate all lights
                    diffuse += (diff.intensity * diff.color);
                    totalDiffuseIntensity += diff.intensity;
                #endif
            }
        #endif

        // specular lighting
        #ifndef LTCGI_SPECULAR_OFF
            [branch]
            if (flags.specular)
            {
                ltcgi_output spec;
                spec.color = 0;
                LTCGI_Evaluate(input, worldNorm, viewDir, Minv, roughness, false, spec);
                spec.intensity *= spec_amp * smoothstep(0.0, LTCGI_SPECULAR_LIGHTMAP_STEP, saturate(lm - LTCGI_LIGHTMAP_CUTOFF));

                #ifdef LTCGI_API_V2
                    LTCGI_V2_SPECULAR_CALLBACK(data, spec);
                #else
                    // simply accumulate all lights
                    specular += spec.intensity * spec.color;
                    totalSpecularIntensity += spec.intensity;
                #endif
            }
        #endif
    }
}

// COMPATIBILITY FALLBACKS

#ifndef LTCGI_API_V2

// missing totalSpecularIntensity, totalDiffuseIntensity, specular
void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse
) {
    half3 _u1 = (half3)0;
    float _u2, _u3;
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse, _u1, _u2, _u3);
}

// missing totalSpecularIntensity, totalDiffuseIntensity
void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse, inout half3 specular
) {
    float _u1, _u2;
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse, specular, _u1, _u2);
}

// missing totalDiffuseIntensity
void LTCGI_Contribution(
    float3 worldPos, float3 worldNorm, float3 viewDir, float roughness, float2 lmuv, inout half3 diffuse, inout half3 specular, out float totalSpecularIntensity
) {
    float _u1;
    LTCGI_Contribution(worldPos, worldNorm, viewDir, roughness, lmuv, diffuse, specular, totalSpecularIntensity, _u1);
}

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

#endif
