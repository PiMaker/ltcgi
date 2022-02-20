#ifndef LTCGI_CONFIG_INCLUDED
#define LTCGI_CONFIG_INCLUDED

// Feel free to enable or disable (//) the options here.
// They will apply to all LTCGI materials in the project.
// Most of these can be changed in the LTCGI_Controller editor as well.

/// Allow statically textured lights.
#define LTCGI_STATIC_TEXTURES

/// No specular at all.
//#define LTCGI_SPECULAR_OFF
/// No diffuse at all.
//#define LTCGI_DIFFUSE_OFF
/// Disable the ability to toggle specular/diffuse on or off per screen.
//#define LTCGI_TOGGLEABLE_SPEC_DIFF_OFF

/// Always use LTC diffuse mode, never lightmapped diffuse.
/// This disables lightmaps entirely.
//#define LTCGI_ALWAYS_LTC_DIFFUSE

/// Use bicubic filtering for LTCGI lightmap. Recommended on.
#define LTCGI_BICUBIC_LIGHTMAP

/// Lightmap values below this will be treated as black for specular/LTC diffuse.
#define LTCGI_LIGHTMAP_CUTOFF 0.1
/// Lightmap values above this (plus cutoff) will be treated as white.
#define LTCGI_SPECULAR_LIGHTMAP_STEP 0.25
/// Increase distance of LTC diffuse. Calculates as `diff = pos(diff, <this>)`.
#define LTCGI_LTC_DIFFUSE_POWER 1

/// Distance multiplier for calculating blur amount.
/// Increase to make reflections blurrier faster as distance increases.
#define LTCGI_UV_BLUR_DISTANCE 333

/// Approximation to ignore diffuse light for far away
/// lights, increase MULT or disable if you notice artifacting
#define LTCGI_DISTANCE_FADE_APPROX
/// Distance at which diffuse from screens will be ignored.
#define LTCGI_DISTANCE_FADE_APPROX_MULT 50
/// [DEBUG] Visualize distance fade error with red pixels.
//#define LTCGI_DISTANCE_FADE_APPROX_ERROR_VISUALIZE

/// [DEBUG] Visualize screen count per pixel for debugging.
/// (black = 0, red = 1, green = 2, blue >= 3)
//#define LTCGI_VISUALIZE_SCREEN_COUNT

/// [DEBUG] Visualize texture sample UVs in .rg space.
//#define LTCGI_VISUALIZE_SAMPLE_UV

/// [DEBUG] Show raw LTCGI shadowmap.
//#define LTCGI_SHOW_SHADOWMAP

/// [DEBUG] Show LTCGI shadowmap UV.
//#define LTCGI_SHOW_SHADOWMAP_UV


// disabled editor from here on out
///


// keep in sync with LTCGI_Controller.cs
#define MAX_SOURCES 16

// set according to the LUT specified on CONTROLLER
#define LUT_SIZE 256
static float LUT_SCALE = (LUT_SIZE - 1.0)/LUT_SIZE;
const float LUT_BIAS = 0.5/LUT_SIZE;


// EXPERIMENTAL! NOT OPTIMIZED!
//
// Slightly less artifacting around UV edges, but more
// in general. Also worse performance (for now).
//#define LTCGI_EXPERIMENTAL_UV_MAP

#endif
