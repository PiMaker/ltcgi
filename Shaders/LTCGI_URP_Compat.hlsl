#ifndef LTCGI_URP_COMPAT_INCLUDED
#define LTCGI_URP_COMPAT_INCLUDED

// URP shim for the Built-in RP cgincs that LTCGI ships.
// Include AFTER URP's Core.hlsl, BEFORE LTCGI.cginc.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

#ifndef fixed
    #define fixed  half
    #define fixed2 half2
    #define fixed3 half3
    #define fixed4 half4
#endif

#ifndef UNITY_PI
    #define UNITY_PI 3.14159265359f
#endif
#ifndef UNITY_HALF_PI
    #define UNITY_HALF_PI 1.57079632679f
#endif
#ifndef UNITY_TWO_PI
    #define UNITY_TWO_PI 6.28318530718f
#endif
#ifndef UNITY_FOUR_PI
    #define UNITY_FOUR_PI 12.56637061436f
#endif
#ifndef UNITY_INV_PI
    #define UNITY_INV_PI 0.31830988618f
#endif

// Texture array macros used by LTCGI_STATIC_TEXTURES. The sampler argument is itself a macro
// (LTCGI_SAMPLER_RAW), so it has to expand before the "sampler" prefix is pasted on.
#define LTCGI_URP_CONCAT(a, b) a##b
#define LTCGI_URP_SAMPLER_NAME(s) LTCGI_URP_CONCAT(sampler, s)
#ifndef UNITY_DECLARE_TEX2DARRAY_NOSAMPLER
    #define UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(tex) TEXTURE2D_ARRAY(tex)
#endif
#ifndef UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD
    #define UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(tex, samplertex, coord, lod) tex.SampleLevel(LTCGI_URP_SAMPLER_NAME(samplertex), coord, lod)
#endif

// 1-arg overload bridging to URP's 2-arg DecodeLightmap.
real3 DecodeLightmap(real4 color)
{
#if defined(UNITY_LIGHTMAP_FULL_HDR)
    return color.rgb;
#else
    real4 decodeInstructions = real4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0);
    return DecodeLightmap(color, decodeInstructions);
#endif
}

#endif
