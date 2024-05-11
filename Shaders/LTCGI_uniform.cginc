#ifndef LTCGI_UNIFORM_INCLUDED
#define LTCGI_UNIFORM_INCLUDED

// global sampler (trilinear)
#ifndef LTCGI_SAMPLER
SamplerState sampler_LTCGI_trilinear_clamp_sampler;
#define LTCGI_SAMPLER sampler_LTCGI_trilinear_clamp_sampler
#define LTCGI_SAMPLER_RAW _LTCGI_trilinear_clamp_sampler
#endif

// LUTs
#ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
#ifndef LTCGI_DISABLE_LUT2
uniform Texture2D<float4> _Udon_LTCGI_lut2;
#endif
uniform Texture2D<float4> _Udon_LTCGI_lut1;
#endif

#ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
uniform Texture2D<float4> _Udon_LTCGI_static_uniforms;
#endif

#ifdef LTCGI_STATIC_UNIFORMS

float4 _Udon_LTCGI_Vertices_0_get(uint i) {
    return _Udon_LTCGI_static_uniforms[uint2(0, i)];
}
float4 _Udon_LTCGI_Vertices_1_get(uint i) {
    return _Udon_LTCGI_static_uniforms[uint2(1, i)];
}
float4 _Udon_LTCGI_Vertices_2_get(uint i) {
    return _Udon_LTCGI_static_uniforms[uint2(2, i)];
}
float4 _Udon_LTCGI_Vertices_3_get(uint i) {
    return _Udon_LTCGI_static_uniforms[uint2(3, i)];
}

#else

// vertices in object space; w component is UV (legacy)
uniform float4 _Udon_LTCGI_Vertices_0[MAX_SOURCES];
uniform float4 _Udon_LTCGI_Vertices_1[MAX_SOURCES];
uniform float4 _Udon_LTCGI_Vertices_2[MAX_SOURCES];
uniform float4 _Udon_LTCGI_Vertices_3[MAX_SOURCES];

float4 _Udon_LTCGI_Vertices_0_get(uint i) {
    return _Udon_LTCGI_Vertices_0[i];
}
float4 _Udon_LTCGI_Vertices_1_get(uint i) {
    return _Udon_LTCGI_Vertices_1[i];
}
float4 _Udon_LTCGI_Vertices_2_get(uint i) {
    return _Udon_LTCGI_Vertices_2[i];
}
float4 _Udon_LTCGI_Vertices_3_get(uint i) {
    return _Udon_LTCGI_Vertices_3[i];
}

#endif

// light source count, maximum is MAX_SOURCES
uniform uint _Udon_LTCGI_ScreenCount;

// per-renderer mask to select sources,
// for max perf update _Udon_LTCGI_ScreenCount too
uniform bool _Udon_LTCGI_Mask[MAX_SOURCES];

// extra data per light source, layout:
//  color.r   color.g   color.b   flags*
// * b0=double-sided, b1=diffuse-from-lightmap, b2=specular, b3=diffuse,
//   b4-b7=texture index (0=video, (n>0)=n-1)
//   b8-b9=color mode
//   b10-b11=lightmap channel (0=disabled, 1=r, 2=g, 3=b)
//   b12=cylinder
//   b13-14=audio link band
//   b15=lightmap diffuse only
// (color black = fully disabled)
uniform float4 _Udon_LTCGI_ExtraData[MAX_SOURCES];

ltcgi_flags ltcgi_parse_flags(uint val, bool noLmDiff)
{
    ltcgi_flags ret = (ltcgi_flags)0;
    ret.doublesided = (val & 1) == 1;

    #ifdef LTCGI_ALWAYS_LTC_DIFFUSE
    ret.diffFromLm  = false;
    #else
    ret.diffFromLm  = !noLmDiff && (val & 2) == 2;
    #endif

    ret.diffuse     = (val & 8) == 8;

    ret.specular    = (val & 4) == 4;
    ret.texindex    = (val & 0xf0) >> 4;
    ret.colormode   = (val & 0x300) >> 8;

    #ifdef LTCGI_ALWAYS_LTC_DIFFUSE
    ret.lmch        = 0;
    #else
    ret.lmch        = (val & 0xC00) >> 10;
    #endif

    ret.cylinder    = (val & (1 << 12)) == (1 << 12);

    #ifdef LTCGI_AUDIOLINK
    ret.alBand      = (val & 0x6000) >> 13;
    #endif

    ret.lmdOnly     = (val & (1 << 15)) == (1 << 15);

    return ret;
}

// video input
#ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
uniform Texture2D<float4> _Udon_LTCGI_Texture_LOD0;
#ifndef LTCGI_FAST_SAMPLING
uniform Texture2D<float4> _Udon_LTCGI_Texture_LOD1;
uniform Texture2D<float4> _Udon_LTCGI_Texture_LOD2;
uniform Texture2D<float4> _Udon_LTCGI_Texture_LOD3;
#endif
#endif

// static textures
#ifdef LTCGI_STATIC_TEXTURES
UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(_Udon_LTCGI_Texture_LOD0_arr);
#ifndef LTCGI_FAST_SAMPLING
UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(_Udon_LTCGI_Texture_LOD1_arr);
UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(_Udon_LTCGI_Texture_LOD2_arr);
UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(_Udon_LTCGI_Texture_LOD3_arr);
#endif
#endif

// lightmap
#ifndef SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER
#ifndef LTCGI_ALWAYS_LTC_DIFFUSE
uniform Texture2D<float4> _Udon_LTCGI_Lightmap;
#endif
#endif
uniform float3 _Udon_LTCGI_LightmapMult;
uniform float4 _Udon_LTCGI_LightmapST;

// global toggle
uniform float _Udon_LTCGI_GlobalEnable;

#endif