#ifndef LTCGI_CONFIG_INCLUDED
#define LTCGI_CONFIG_INCLUDED

// Feel free to enable or disable (//) the options here.
// They will apply to all LTCGI materials in the project.
// Most of these can be changed in the LTCGI_Controller editor as well.

/// No specular at all.
//#define LTCGI_SPECULAR_OFF
/// No diffuse at all.
//#define LTCGI_DIFFUSE_OFF
/// Disable the ability to toggle specular/diffuse on or off per screen.
//#define LTCGI_TOGGLEABLE_SPEC_DIFF_OFF

/// Only use LTC diffuse mode, never lightmapped diffuse.
/// This disables lightmaps entirely.
//#define LTCGI_ALWAYS_LTC_DIFFUSE

/// Double-sample screen texture for diffuse lighting to smooth resulting lighting
/// a bit more with global screen color data. Slight performance cost.
//#define LTCGI_BLENDED_DIFFUSE_SAMPLING

/// Disable extra specular detail LUT, saves a sampler.
//#define LTCGI_DISABLE_LUT2

/// Use bicubic filtering for LTCGI lightmap. Recommended on.
#define LTCGI_BICUBIC_LIGHTMAP

/// Lightmap values below this will be treated as black for specular/LTC diffuse.
#define LTCGI_LIGHTMAP_CUTOFF 0.1
/// Lightmap values above this (plus cutoff) will be treated as white.
#define LTCGI_SPECULAR_LIGHTMAP_STEP 0.3

/// Distance multiplier for calculating blur amount.
/// Increase to make reflections blurrier faster as distance increases.
#define LTCGI_UV_BLUR_DISTANCE 333

/// Fall back to LTC diffuse (from LM diffuse) on objects that are not marked static.
#define LTCGI_LTC_DIFFUSE_FALLBACK

/// Approximation to ignore diffuse light for far away
/// lights, increase MULT or disable if you notice artifacting
#define LTCGI_DISTANCE_FADE_APPROX
/// Distance at which diffuse from screens will be ignored.
#define LTCGI_DISTANCE_FADE_APPROX_MULT 50


// disabled editor from here on out
///


// automatically kept in sync with LTCGI_Controller.cs
#define MAX_SOURCES 16

// set according to the LUT specified on CONTROLLER
#define LUT_SIZE 256
static float LUT_SCALE = (LUT_SIZE - 1.0)/LUT_SIZE;
const float LUT_BIAS = 0.5/LUT_SIZE;

// will be set automatically if audiolink is available and in use
//#define LTCGI_AUDIOLINK

#ifdef LTCGI_AUDIOLINK
#ifndef AUDIOLINK_WIDTH
#ifndef AUDIOLINK_CGINC_INCLUDED
#include "Packages/at.pimaker.ltcgi/Shaders/LTCGI_AudioLinkNoOp.cginc"
#define AUDIOLINK_CGINC_INCLUDED
#endif
#endif
#endif

// Bake screen data into texture for better performance. Disables moveable screens.
#define LTCGI_STATIC_UNIFORMS

// Allow statically textured lights.
//#define LTCGI_STATIC_TEXTURES

// Enable support for cylindrical screens.
//#define LTCGI_CYLINDER

// Activate avatar mode, which overrides certain configs from above.
//#define LTCGI_AVATAR_MODE

// Slightly simplified and thus faster sampling for reflections at the cost of quality.
//#define LTCGI_FAST_SAMPLING

#endif
