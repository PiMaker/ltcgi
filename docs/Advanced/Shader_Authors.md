---
sidebar_position: 11
---

# 🪄 For Shader Authors

The recommended way to include LTCGI support in your shader is by including the `LTCGI.cginc` file in your shader from the import path of this prefab. I.e. do not copy the code and distribute it with your shader, but simply include it if it exists (you can check with the `LTCGI_INCLUDED` define in C#).

To have the controller recognize your shader as LTCGI compatible, you need to include a tag named `LTCGI` which can either have the value `ALWAYS` or the name of a shader property (I believe in upper-case only?) on which LTCGI support depends (e.g. a `[Toggle]` or `[ToggleUI]`).

See z3y's integration for an example: https://github.com/z3y/shaders/commit/0ff546a874c6ab7dc12f5762f895f3cb76b8164e

Note that this method of distribution does not encumber your shader with any additional licenses.

> 🙋‍♂️ If you implement LTCGI into your shader, feel free to send a PR to be included in the README!

## The LTCGI v2 API

For more advanced shader use-cases, LTCGI provides an API to get more detailed callbacks for each light source. This allows better integration with your shading model.

A documented example for and APIv2 is given with the `LTCGI_Surface_v2.shader`: https://github.com/PiMaker/ltcgi/blob/main/Shaders/LTCGI_Surface_v2.shader

The callbacks receive certain structs, which can be checked out here: https://github.com/PiMaker/ltcgi/blob/main/Shaders/LTCGI_structs.cginc

The idea is that you define callback functions as well as a custom "data" struct that serves as your custom accumulator/"return" type. These callbacks will then be used instead of simply summing up `intensity * color` for each light and returning specular+diffuse contributions.

I recommend using this API going forward, even though it is somewhat experimental. Please leave feedback on my Discord!

## Sampler slots

LTCGI only uses a single `SamplerState`, but can be configured to use 0 as well. To do so, set the macro `LTCGI_SAMPLER` to your sampler before importing LTCGI. It should be clamp+trilinear, anisotropic is okay but not required.

The default sampler is declared as:
```csharp
SamplerState sampler_LTCGI_trilinear_clamp_sampler;
```

By necessity, LTCGI uses a non-trivial number of texture slots however. By using `LTCGI_ALWAYS_LTC_DIFFUSE` (default in avatar mode) you can at least get rid of one, the lightmap.

## Amplify

There is an Amplify example available at `Packages/at.pimaker.ltcgi/Shaders/Amplify/LTCGI_Amplify.shader`.  
You can open that file in Amplify Shader Editor to see how it is configured.

In general, the steps are:

* Configure the `#include` and custom tag

![Amplify Example 1](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/amplify1.jpg)

* Add a `LTCGI_Contribution` node (under `Functions`) and connect it

![Amplify Example 3](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/amplify3.jpg)  
Check the example linked above to see which parameter values are expected.