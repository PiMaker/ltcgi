#ifndef LTCGI_STRUCTS_INCLUDED
#define LTCGI_STRUCTS_INCLUDED

#define LTCGI_COLORMODE_STATIC 0
#define LTCGI_COLORMODE_TEXTURE 1
#define LTCGI_COLORMODE_SINGLEUV 2
#define LTCGI_COLORMODE_AUDIOLINK 3

struct ltcgi_flags
{
    bool doublesided; // if the light is doublesided or only illuminates the front face
    bool diffFromLm; // diffuse lighting intensity will not be calculated via LTC but taken directly from the lightmap
    bool specular; // if the light has a specular component
    bool diffuse; // if the light has a diffuse component
    uint colormode; // colormode, see above
    uint texindex; // index of the texture to shade with, if colormode == LTCGI_COLORMODE_TEXTURE
    uint lmch, lmidx; // lightmap channel and index
    bool cylinder; // is this light a cylinder
    uint alBand; // audiolink band if colormode == LTCGI_COLORMODE_AUDIOLINK
    bool lmdOnly; // if this light is lightmap-diffuse _only_, that is, no LTC will be run (Lw will be all 0 in that case) - this will never be true on avatars (with LTCGI_ALWAYS_LTC_DIFFUSE)
};

struct ltcgi_input
{
    uint i; // light number
    float3 Lw[4]; // world space area light vertices, Lw[1] == Lw[3] for triangle lights, shifted by input worldPos (i.e. world space position as seen from (0, 0, 0))
    bool isTri; // if this is a triangle light, quad if false
    float4 uvStart; // defines the UV layout of the area (xy = top-left, zw=top-right)
    float4 uvEnd; // defines the UV layout of the area (xy = bottom-left, zw=bottom-right), different use for cylinders
    float3 rawColor; // the raw light color, unaffected by any colormode calculations (i.e. exactly what's given as "color" in editor)
    float3 screenNormal; // world space normal direction of area light
    ltcgi_flags flags; // flags, see above
};

struct ltcgi_output
{
    ltcgi_input input; // input data that resulted in this output

    float intensity; // intensity output by LTC calculation
    float3 color; // color output by LTCGI colormode calculation
};

#endif