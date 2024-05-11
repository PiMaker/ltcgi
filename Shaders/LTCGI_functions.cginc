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

half3 premul_alpha(half4 i)
{
    return i.rgb * i.a;
}

half max2(half2 v)
{
    return max(v.x, v.y);
}

void LTCGI_sample(float2 uv, uint lod, uint idx, float blend, out float3 result)
{
#ifndef LTCGI_STATIC_TEXTURES
    idx = 0; // optimize away the branches below
#endif

#ifdef LTCGI_FAST_SAMPLING
    #ifndef SHADER_TARGET_SURFACE_ANALYSIS
        float outside = max2(abs(uv - 0.5f) - 0.5f);
        float outmod = smoothstep(-0.1f, 0.1f, outside) * 2.5f;
        blend = blend * 2.5f + outmod;
        [branch]
        if (idx == 0)
        {
            result = premul_alpha(_Udon_LTCGI_Texture_LOD0.SampleLevel(LTCGI_SAMPLER, uv, blend));
        }
        #ifdef LTCGI_STATIC_TEXTURES
        else
        {
            result = UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _Udon_LTCGI_Texture_LOD0_arr,
                    LTCGI_SAMPLER_RAW,
                    float3(uv, idx - 1),
                    blend
                ).rgb;
        }
        #endif
    #else
        result = 0;
    #endif
#else
    result = 0;

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
                result = premul_alpha(_Udon_LTCGI_Texture_LOD0.SampleLevel(LTCGI_SAMPLER, uv, lod));
                return;
                #else
                result = 0;
                return;
                #endif
            }
            #ifdef LTCGI_STATIC_TEXTURES
            else
            {
                result = premul_alpha(UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _Udon_LTCGI_Texture_LOD0_arr,
                    LTCGI_SAMPLER_RAW,
                    float3(uv, idx - 1),
                    lod
                ));
                return;
            }
            #endif
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
                result = _Udon_LTCGI_Texture_LOD1.SampleLevel(LTCGI_SAMPLER, ruv, 0).rgb;
                return;
            case 2:
                result = _Udon_LTCGI_Texture_LOD2.SampleLevel(LTCGI_SAMPLER, ruv, 0).rgb;
                return;
            default:
                result = _Udon_LTCGI_Texture_LOD3.SampleLevel(LTCGI_SAMPLER, ruv, blend*0.72).rgb;
                return;
        }
        #else
        result = 0;
        return;
        #endif
    }
    #ifdef LTCGI_STATIC_TEXTURES
    else
    {
        [forcecase]
        switch (lod)
        {
            case 1:
                result = UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _Udon_LTCGI_Texture_LOD1_arr,
                    LTCGI_SAMPLER_RAW,
                    float3(ruv, idx - 1),
                    0
                ).rgb;
                return;
            case 2:
                result = UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _Udon_LTCGI_Texture_LOD2_arr,
                    LTCGI_SAMPLER_RAW,
                    float3(ruv, idx - 1),
                    0
                ).rgb;
                return;
            default:
                result = UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(
                    _Udon_LTCGI_Texture_LOD3_arr,
                    LTCGI_SAMPLER_RAW,
                    float3(ruv, idx - 1),
                    blend
                ).rgb;
                return;
        }
    }
    #endif
#endif
}

void LTCGI_trilinear(float2 uv, float d, uint idx, out float3 result)
{
#ifdef LTCGI_FAST_SAMPLING
    LTCGI_sample(uv, 0, idx, d, result);
#else
    uint low = (uint)d;
    uint high = low + 1;

    // DEBUG: colorize d/lod
    //return float3(low == 0, low == 1, low == 2);

    if (low >= 3)
    {
        LTCGI_sample(uv, 3, idx, d - 3, result);
    }
    else
    {
        float amount = saturate(high - d);
        float3 low_sample;
        LTCGI_sample(uv, low, idx, amount, low_sample);
        float3 high_sample;
        LTCGI_sample(uv, high, idx, 0, high_sample);

        result = lerp(high_sample, low_sample, amount);
    }
#endif
}

/*
    GENERIC HELPERS
*/

// from: https://seblagarde.wordpress.com/2014/12/01/inverse-trigonometric-functions-gpu-optimization-for-amd-gcn-architecture/
// max absolute error 9.0x10^-3
// Eberly's polynomial degree 1 - respect bounds
// 4 VGPR, 12 FR (8 FR, 1 QR), 1 scalar
// input [-1, 1] and output [0, PI]
float LTCGI_acos_fast(float inX) 
{ 
    float x = abs(inX); 
    float res = -0.156583f * x + UNITY_HALF_PI; 
    res *= sqrt(1.0f - x); 
    return (inX >= 0) ? res : UNITY_PI - res; 
}

bool LTCGI_tri_ray(float3 orig, float3 dir, float3 v0, float3 v1, float3 v2, out float2 bary) {
    float3 v0v1 = v1 - v0;
    float3 v0v2 = v2 - v0;
    float3 pvec = cross(dir, v0v2);
    float det = dot(v0v1, pvec);
    float invDet = 1 / det;

    float3 tvec = orig - v0;
    bary.x = dot(tvec, pvec) * invDet;

    float3 qvec = cross(tvec, v0v1);
    bary.y = dot(dir, qvec) * invDet;

    // return false when other triangle of quad should be sampled,
    // i.e. we went over the diagonal line
    return bary.x >= 0;
}

float2 LTCGI_rotateVector(float2 x, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mul(float2x2(c,s,-s,c), x);
}

/*float LTCGI_remap(float3 from, float3 to, float2 targetFrom, float2 targetTo, float3 value)
{
    float rel = (value - from) / (to - from);
    return lerp(targetFrom, targetTo, rel);
}*/

float2 LTCGI_calculateUV(uint i, ltcgi_flags flags, float3 L[5], bool isTri, float4 uvStart, float4 uvEnd, out float3 ray)
{
    // calculate perpendicular vector to plane defined by area light
    float3 E1 = L[1] - L[0];
    float3 E2 = L[3] - L[0];
    ray = cross(E1, E2);

    // raycast it against the two triangles formed by the quad
    float2 bary;
    bool hit0 = LTCGI_tri_ray(0, ray, L[0], L[2], L[3], bary) || isTri;
    if (!hit0) {
        LTCGI_tri_ray(0, ray, L[0], L[1], L[2], bary);
    }

    float3 bary3 = float3(bary, 1 - bary.x - bary.y);
    float2 uv;
    if (hit0)
        uv = uvEnd.zw * bary3.x + uvEnd.xy * bary3.y;
    else
        uv = uvStart.zw * bary3.x + uvEnd.zw * bary3.y;
    return uv + uvStart.xy * bary3.z;
}

/*
    EXPERIMENTAL: CYLINDER HELPER
*/

void LTCGI_GetLw(uint i, ltcgi_flags flags, float3 worldPos, out float3 Lw[4], out float4 uvStart, out float4 uvEnd, out bool isTri) {
    bool cylinder = false;
    #ifdef LTCGI_CYLINDER
        // statically optimize out branch below in case disabled
        cylinder = flags.cylinder;
    #endif

    float4 v0 = _Udon_LTCGI_Vertices_0_get(i);
    float4 v1 = _Udon_LTCGI_Vertices_1_get(i);
    float4 v2 = _Udon_LTCGI_Vertices_2_get(i);
    float4 v3 = _Udon_LTCGI_Vertices_3_get(i);

    [branch]
    if (cylinder) {
        // construct data according to worldPos to create aligned
        // rectangle tangent to the cylinder
        
        float3 in_base = v0.xyz;
        float in_height = v0.w;
        float in_radius = v1.w;
        float in_size = v2.w;
        float in_angle = v3.w;

        // get angle between 2D unit plane and vector pointing from cylinder to shade point
        float2 towardsCylinder = LTCGI_rotateVector((in_base - worldPos).xz, -in_angle);
        float angle = atan2(towardsCylinder.x, towardsCylinder.y);
        // clamp angle to size parameter, i.e. "width" of lit surface area
        float angleClamped = clamp(angle, -in_size, in_size) + in_angle;
        // construct vector that *most* faces shade point
        float2 facing = float2(sin(angleClamped), cos(angleClamped));
        // tangent of rectangular screen on cylinder surface used for calculating lighting for shade point
        float2 tangent = float2(facing.y, -facing.x);
        float2 onCylinderFacing = facing * in_radius;

        // clip ends, approximately
        float rclip = saturate(lerp(1, 0, (angleClamped - in_angle) - (in_size - UNITY_HALF_PI*0.5f)));
        float lclip = saturate(lerp(1, 0, -(angleClamped - in_angle) - (in_size - UNITY_HALF_PI*0.5f)));

        float2 p1 = in_base.xz - onCylinderFacing + tangent * in_radius * lclip;
        float2 p2 = in_base.xz - onCylinderFacing - tangent * in_radius * rclip;

        Lw[0] = float3(p1.x, in_base.y,             p1.y) - worldPos;
        Lw[1] = float3(p1.x, in_base.y + in_height, p1.y) - worldPos;
        Lw[2] = float3(p2.x, in_base.y,             p2.y) - worldPos;
        Lw[3] = float3(p2.x, in_base.y + in_height, p2.y) - worldPos;

        isTri = false;

        // UV depends on "viewing" angle of the shade point towards the cylinder
        float2 viewDir = normalize((in_base - worldPos).xz);
        // forwardAngle == atan2(cos(in_angle), sin(in_angle)); but only negative
        float forwardAngle = -in_angle + UNITY_HALF_PI;
        // offset from center of screen forward to the side ends, positive goes left/ccw fpv top,
        // sine to account for the fact we're rotating around a cylinder which has depth
        float viewAngle = forwardAngle - atan2(viewDir.y, viewDir.x);
        // prevent rollover, since we need to clamp we must stay withing [-pi, pi]
        if (viewAngle < -UNITY_PI)
            viewAngle += UNITY_TWO_PI;
        if (viewAngle > UNITY_PI)
            viewAngle -= UNITY_TWO_PI;
        viewAngle = clamp(viewAngle * 0.5f, -in_size, in_size);
        viewAngle = sin(viewAngle);
        // full view UVs, but shifted left/right depending on view angle
        float2 uvStart2 = float2(1 - saturate(viewAngle), 0);
        float2 uvEnd2 = float2(1 - saturate(viewAngle + 1), 1);
        uvStart = float4(uvStart2.x, uvStart2.y, uvStart2.x, uvEnd2.y);
        uvEnd = float4(uvEnd2.x, uvStart2.y, uvEnd2.x, uvEnd2.y);

    } else {
        // use passed in data, offset around worldPos
        Lw[0] = v0.xyz - worldPos;
        Lw[1] = v1.xyz - worldPos;
        Lw[2] = v2.xyz - worldPos;
        Lw[3] = v3.xyz - worldPos;
        #ifndef SHADER_TARGET_SURFACE_ANALYSIS
            uvStart = _Udon_LTCGI_static_uniforms[uint2(4, i)];
            uvEnd = _Udon_LTCGI_static_uniforms[uint2(5, i)];
        #else
            uvStart = float4(0, 0, 1, 0);
            uvEnd = float4(1, 1, 0, 1);
        #endif

        // we only detect triangles for "blender" import configuration, as those are the only
        // ones that can actually be triangles (I think?)
        isTri = /*distance(Lw[2], Lw[3]) < 0.001 || */distance(Lw[1], Lw[3]) < 0.001;
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