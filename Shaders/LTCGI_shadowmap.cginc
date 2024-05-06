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

float4 LTCGI_SampleTexture2DBicubicFilter(Texture2D tex, SamplerState smp, float2 coord, float4 texSize, bool lightmap = false)
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

    if (lightmap) {
        sample0 = float4(DecodeLightmap(sample0), 1.0);
        sample1 = float4(DecodeLightmap(sample1), 1.0);
        sample2 = float4(DecodeLightmap(sample2), 1.0);
        sample3 = float4(DecodeLightmap(sample3), 1.0);
    }

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
    #else
        lmuv = lmuv * _Udon_LTCGI_LightmapST.xy + _Udon_LTCGI_LightmapST.zw;

        #ifdef LTCGI_BICUBIC_LIGHTMAP
            float width, height;
            _Udon_LTCGI_Lightmap.GetDimensions(width, height);

            float4 _Udon_LTCGI_Lightmap_TexelSize = float4(width, height, 1.0/width, 1.0/height);

            return LTCGI_SampleTexture2DBicubicFilter(
                _Udon_LTCGI_Lightmap, LTCGI_SAMPLER,
                lmuv, _Udon_LTCGI_Lightmap_TexelSize,
                true
            );
        #else
            fixed4 sample = _Udon_LTCGI_Lightmap.Sample(LTCGI_SAMPLER, lmuv);
            return float4(DecodeLightmap(sample), 1.0);
        #endif
    #endif
}

#else
// surface shader analysis stub
float4 LTCGI_SampleShadowmap(float2 lmuv) { return 1; }
#endif

#endif