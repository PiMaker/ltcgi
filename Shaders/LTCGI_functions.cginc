#ifndef LTCGI_FUNCTIONS_INCLUDED
#define LTCGI_FUNCTIONS_INCLUDED

/*
    LTC HELPERS
*/

float3 LTCGI_IntegrateEdge(float3 v1, float3 v2)
{
    float x = dot(v1, v2);
    float y = abs(x);

    float a = 0.8543985 + (0.4965155 + 0.0145206*y)*y;
    float b = 3.4175940 + (4.1616724 + y)*y;
    float v = a / b;
    float theta_sintheta = (x > 0.0) ? v : 0.5*rsqrt(max(1.0 - x*x, 1e-7)) - v;

    return cross(v1, v2) * theta_sintheta;
}

void LTCGI_ClipQuadToHorizon(inout float3 L[5], out int n)
{
    // detect clipping config
    uint config = 0;
    if (L[0].z > 0.0) config += 1;
    if (L[1].z > 0.0) config += 2;
    if (L[2].z > 0.0) config += 4;
    if (L[3].z > 0.0) config += 8;

    n = 0;

    // This [forcecase] only works when the cases are ordered in a specific manner.
    // It gives like 10%-20% performance boost, so *make sure to leave it on*!
    // If it breaks however, see if [branch] fixes it, and if it does, start
    // reordering cases at random until it works again.
    // It seems the compiler somehow optimizes away anything but setting 'n' in
    // some orderings, including the ascending and descending ones.
    // I wish I was joking.
    [forcecase]
    switch (config) {
        case 13: // V1 V3 V4 clip V2 <- tl;dr: this fecker has to be first or shader go boom
            n = 5;
            L[4] = L[3];
            L[3] = L[2];
            L[2] = -L[1].z * L[2] + L[2].z * L[1];
            L[1] = -L[1].z * L[0] + L[0].z * L[1];
            break;
        case 15: // V1 V2 V3 V4 - most common
            n = 4;
            break;
        case 9: // V1 V4 clip V2 V3
            n = 4;
            L[1] = -L[1].z * L[0] + L[0].z * L[1];
            L[2] = -L[2].z * L[3] + L[3].z * L[2];
            break;
        case 0: // clip all
            break;
        case 1: // V1 clip V2 V3 V4
            n = 3;
            L[1] = -L[1].z * L[0] + L[0].z * L[1];
            L[2] = -L[3].z * L[0] + L[0].z * L[3];
            L[3] =  L[0];
            break;
        case 2: // V2 clip V1 V3 V4
            n = 3;
            L[0] = -L[0].z * L[1] + L[1].z * L[0];
            L[2] = -L[2].z * L[1] + L[1].z * L[2];
            L[3] =  L[0];
            break;
        case 3: // V1 V2 clip V3 V4
            n = 4;
            L[2] = -L[2].z * L[1] + L[1].z * L[2];
            L[3] = -L[3].z * L[0] + L[0].z * L[3];
            break;
        case 4: // V3 clip V1 V2 V4
            n = 3;
            L[0] = -L[3].z * L[2] + L[2].z * L[3];
            L[1] = -L[1].z * L[2] + L[2].z * L[1];
            L[3] =  L[0];
            break;
        case 5: // V1 V3 clip V2 V4) impossible
            break;
        case 6: // V2 V3 clip V1 V4
            n = 4;
            L[0] = -L[0].z * L[1] + L[1].z * L[0];
            L[3] = -L[3].z * L[2] + L[2].z * L[3];
            break;
        case 7: // V1 V2 V3 clip V4
            n = 5;
            L[4] = -L[3].z * L[0] + L[0].z * L[3];
            L[3] = -L[3].z * L[2] + L[2].z * L[3];
            break;
        case 8: // V4 clip V1 V2 V3
            n = 3;
            L[0] = -L[0].z * L[3] + L[3].z * L[0];
            L[1] = -L[2].z * L[3] + L[3].z * L[2];
            L[2] =  L[3];
            break;
        case 10: // V2 V4 clip V1 V3) impossible
            break;
        case 11: // V1 V2 V4 clip V3
            n = 5;
            L[4] = L[3];
            L[3] = -L[2].z * L[3] + L[3].z * L[2];
            L[2] = -L[2].z * L[1] + L[1].z * L[2];
            break;
        case 12: // V3 V4 clip V1 V2
            n = 4;
            L[1] = -L[1].z * L[2] + L[2].z * L[1];
            L[0] = -L[0].z * L[3] + L[3].z * L[0];
            break;
        case 14: // V2 V3 V4 clip V1
            n = 5;
            L[4] = -L[0].z * L[3] + L[3].z * L[0];
            L[0] = -L[0].z * L[1] + L[1].z * L[0];
            break;
    }
    
    // inlining these branches *unconditionally* breaks the [forcecase] above
    // ...yeah I know
    if (n == 3)
        L[3] = L[0];
    if (n == 4)
        L[4] = L[0];
}

/*
    TEXTURE SAMPLING
*/

float2 LTCGI_inset_uv(float2 uv)
{
    return uv * 0.75 + float2(0.125, 0.125);
}

float3 LTCGI_sample(float2 uv, uint lod, uint idx, float blend)
{
    #ifndef LTCGI_STATIC_TEXTURES
    idx = 0; // optimize away the branches below
    #endif

    [branch]
    if (lod == 0)
    {
        // if we're outside of the 0-1 UV space we must sample a prefiltered texture
        [branch]
        if(any(saturate(abs(uv - 0.5) - 0.5)))
        {
            lod = 1;
        }
        else
        {
            // LOD0 is the original texture itself, so not prefiltered, but we can
            // approximate it a bit with trilinear lod
            float lod = (1 - blend) * 1.5;
            [branch]
            if (idx == 0)
            {
                #ifndef SHADER_TARGET_SURFACE_ANALYSIS
                return _LTCGI_Texture_LOD0.SampleLevel(sampler_LTCGI_trilinear_clamp_sampler, uv, lod).rgb;
                #else
                return 0;
                #endif
            }
            else
            {
                return UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _LTCGI_Texture_LOD0_arr,
                    _LTCGI_trilinear_clamp_sampler,
                    float3(uv, idx - 1),
                    lod
                ).rgb;
            }
        }
    }

    float2 ruv = LTCGI_inset_uv(uv);

    [branch]
    if (idx == 0)
    {
        #ifndef SHADER_TARGET_SURFACE_ANALYSIS
        switch (lod)
        {
            case 1:
                return _LTCGI_Texture_LOD1.SampleLevel(sampler_LTCGI_bilinear_clamp_sampler, ruv, 0).rgb;
            case 2:
                return _LTCGI_Texture_LOD2.SampleLevel(sampler_LTCGI_bilinear_clamp_sampler, ruv, 0).rgb;
            default:
                return _LTCGI_Texture_LOD3.SampleLevel(sampler_LTCGI_trilinear_clamp_sampler, ruv, blend*0.72).rgb;
        }
        #else
        return 0;
        #endif
    }
    else
    {
        [forcecase]
        switch (lod)
        {
            case 1:
                return UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _LTCGI_Texture_LOD1_arr,
                    _LTCGI_bilinear_clamp_sampler,
                    float3(ruv, idx - 1),
                    0
                ).rgb;
            case 2:
                return UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _LTCGI_Texture_LOD2_arr,
                    _LTCGI_bilinear_clamp_sampler,
                    float3(ruv, idx - 1),
                    0
                ).rgb;
            default:
                return UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _LTCGI_Texture_LOD3_arr,
                    _LTCGI_trilinear_clamp_sampler,
                    float3(ruv, idx - 1),
                    blend
                ).rgb;
        }
    }
}

float3 LTCGI_trilinear(float2 uv, float d, uint idx)
{
    uint low = (uint)d;
    uint high = low + 1;

    // DEBUG: colorize d/lod
    //return float3(low == 0, low == 1, low == 2);

    if (low >= 3)
    {
        return LTCGI_sample(uv, 3, idx, d - 3);
    }

    float amount = saturate(high - d);
    float3 low_sample = LTCGI_sample(uv, low, idx, amount);
    float3 high_sample = LTCGI_sample(uv, high, idx, 0);

    return lerp(high_sample, low_sample, amount);
}

/*
    GENERIC HELPERS
*/

float LTCGI_invlerp(float from, float to, float value){
    return (value - from) / (to - from);
}

float LTCGI_remap(float origFrom, float origTo, float targetFrom, float targetTo, float value){
    float rel = LTCGI_invlerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}

bool LTCGI_tri_ray(float3 orig, float3 dir, float3 v0, float3 v1, float3 v2, out float2 bary) {
    float3 v0v1 = v1 - v0;
    float3 v0v2 = v2 - v0;
    float3 pvec = cross(dir, v0v2);
    float det = dot(v0v1, pvec);
    float invDet = 1 / det;

    float3 tvec = orig - v0;
    bary.x = dot(tvec, pvec) * invDet;
    if (bary.x < 0 || bary.x > 1) return false;

    float3 qvec = cross(tvec, v0v1);
    bary.y = dot(dir, qvec) * invDet;

    return true;
}

float2 LTCGI_rotateVector(float2 x, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mul(float2x2(c,s,-s,c), x);
}

/*
    EXPERIMENTAL: CYLINDER HELPER
*/

void LTCGI_GetLw(uint i, ltcgi_flags flags, float3 worldPos, float3 viewDir, out float3 Lw[4], out float2 uvStart, out float2 uvEnd) {
    bool cylinder = false;
    #ifdef LTCGI_CYLINDER
        // statically optimize out branch below in case disabled
        cylinder = flags.cylinder;
    #endif

    if (cylinder) {
        // construct data according to worldPos to create aligned
        // rectangle tangent to the cylinder
        float3 in_base = _LTCGI_Vertices_0[i].xyz;
        float in_height = _LTCGI_Vertices_0[i].w;
        float in_radius = _LTCGI_Vertices_1[i].w;
        float in_size = _LTCGI_Vertices_2[i].w;
        float in_angle = _LTCGI_Vertices_3[i].w;

        float2 centerForward = float2(sin(in_angle), cos(in_angle));
        float2 towardsCylinder = (in_base - worldPos).xz;
        float angle = atan2(centerForward.y, centerForward.x) - atan2(towardsCylinder.y, towardsCylinder.x);
        float angleClamped = clamp(angle, -UNITY_PI/2, UNITY_PI/2);
        float2 facing = LTCGI_rotateVector(centerForward, angleClamped);
        float2 tangent = float2(facing.y, -facing.x);
        float2 onCylinderFacing = facing * in_radius;

        // clip ends, approximately
        float rclip = saturate(lerp(1, 0, angleClamped - (UNITY_PI/2) + in_size*2));
        float lclip = saturate(lerp(1, 0, -angleClamped - (UNITY_PI/2) + in_size*2));

        float2 p1 = in_base.xz - onCylinderFacing + tangent * in_size * lclip;
        float2 p2 = in_base.xz - onCylinderFacing - tangent * in_size * rclip;

        Lw[0] = float3(p1.x, in_base.y,             p1.y) - worldPos;
        Lw[1] = float3(p1.x, in_base.y + in_height, p1.y) - worldPos;
        Lw[2] = float3(p2.x, in_base.y,             p2.y) - worldPos;
        Lw[3] = float3(p2.x, in_base.y + in_height, p2.y) - worldPos;

        // UV depends on viewing angle (for specular)
        float2 centerTangent = float2(centerForward.y, -centerForward.x);
        viewDir = normalize(in_base - worldPos);
        float viewAngle = atan2(centerForward.y, centerForward.x) - atan2(viewDir.z, viewDir.x);
        uvStart = float2(-(1 - sin(saturate(viewAngle*0.5))), 0);
        uvEnd = float2(-sin(saturate(-viewAngle*0.5)), 1);

    } else {
        // use passed in data, offset around worldPos
        Lw[0] = _LTCGI_Vertices_0[i].xyz - worldPos;
        Lw[1] = _LTCGI_Vertices_1[i].xyz - worldPos;
        Lw[2] = _LTCGI_Vertices_2[i].xyz - worldPos;
        Lw[3] = _LTCGI_Vertices_3[i].xyz - worldPos;
        uvStart = float2(_LTCGI_Vertices_0[i].w, _LTCGI_Vertices_1[i].w);
        uvEnd = float2(_LTCGI_Vertices_2[i].w, _LTCGI_Vertices_3[i].w);
    }
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