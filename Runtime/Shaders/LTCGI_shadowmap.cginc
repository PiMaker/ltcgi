#ifndef LTCGI_SHADOWMAP_INCLUDED
#define LTCGI_SHADOWMAP_INCLUDED

// Adapted from: https://gitlab.com/s-ilent/filamented
// Licensed under the terms of the Apache License 2.0
// Full text: https://gitlab.com/s-ilent/filamented/-/blob/master/LICENSE
//
// Conforming to the terms of the above license, this file is redistributed
// under the terms of the MIT license as part of the LTCGI shader package,
// provided this notice is kept.

#ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER

float4 LTCGI_cubic(float v)
{
    float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
    float4 s = n * n * n;
    float x = s.x;
    float y = s.y - 4.0 * s.x;
    float z = s.z - 4.0 * s.y + 6.0 * s.x;
    float w = 6.0 - x - y - z;
    return float4(x, y, z, w);
}

// Unity's SampleTexture2DBicubic doesn't exist in 2018, which is our target here.
// So this is a similar function with tweaks to have similar semantics. 

float4 LTCGI_SampleTexture2DBicubicFilter(Texture2D tex, SamplerState smp, float2 coord, float4 texSize)
{
    coord = coord * texSize.xy - 0.5;
    float fx = frac(coord.x);
    float fy = frac(coord.y);
    coord.x -= fx;
    coord.y -= fy;

    float4 xcubic = LTCGI_cubic(fx);
    float4 ycubic = LTCGI_cubic(fy);

    float4 c = float4(coord.x - 0.5, coord.x + 1.5, coord.y - 0.5, coord.y + 1.5);
    float4 s = float4(xcubic.x + xcubic.y, xcubic.z + xcubic.w, ycubic.x + ycubic.y, ycubic.z + ycubic.w);
    float4 offset = c + float4(xcubic.y, xcubic.w, ycubic.y, ycubic.w) / s;

    float4 sample0 = tex.Sample(smp, float2(offset.x, offset.z) * texSize.zw);
    float4 sample1 = tex.Sample(smp, float2(offset.y, offset.z) * texSize.zw);
    float4 sample2 = tex.Sample(smp, float2(offset.x, offset.w) * texSize.zw);
    float4 sample3 = tex.Sample(smp, float2(offset.y, offset.w) * texSize.zw);

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    return lerp(
        lerp(sample3, sample2, sx),
        lerp(sample1, sample0, sx), sy);
}

float4 LTCGI_SampleShadowmap(float2 lmuv)
{
    #ifdef LTCGI_ALWAYS_LTC_DIFFUSE
        return 1;
    #endif

    lmuv = lmuv * _LTCGI_LightmapST.xy + _LTCGI_LightmapST.zw;

    #ifdef LTCGI_BICUBIC_LIGHTMAP
        float width, height;
        _LTCGI_Lightmap.GetDimensions(width, height);

        float4 _LTCGI_Lightmap_TexelSize = float4(width, height, 1.0/width, 1.0/height);

        return LTCGI_SampleTexture2DBicubicFilter(
            _LTCGI_Lightmap, sampler_LTCGI_trilinear_clamp_sampler,
            lmuv, _LTCGI_Lightmap_TexelSize
        );
    #else
        return _LTCGI_Lightmap.Sample(sampler_LTCGI_trilinear_clamp_sampler, lmuv);
    #endif
}

#else
// surface shader analysis stub
float4 LTCGI_SampleShadowmap(float2 lmuv) { return 1; }
#endif

#endif