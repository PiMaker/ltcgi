---
sidebar_position: 10
---

# 💡 VRC Light Volumes

LTCGI can render baked LTCGI shadowmap data into [VRCLightVolumes](https://github.com/REDSIM/VRCLightVolumes). This is useful for avatar lighting in VRChat worlds, because it lets Light Volume compatible shaders pick up LTCGI lighting without requiring dedicated LTCGI shaders on every avatar.

This integration only exists when VRCLightVolumes is installed and the `VRC_LIGHT_VOLUMES` scripting define is available. If the Light Volumes package is not installed, these components will not be active.

To install Light Volumes, follow the regular installation instructions on [GitHub](https://github.com/REDSIM/VRCLightVolumes). ⚠️ **Familiarity with non-LTCGI Light Volumes is required!**

## Setup

First, set up LTCGI and VRCLightVolumes normally in the same scene. You need a `LightVolumeManager` in the scene. You can add as many static or non-LTCGI additive LVs as you wish.

To create a volume that receives LTCGI data, add a `Light Volume LTCGI` component instead of a regular Light Volume component. The inspector forces the volume into the settings LTCGI needs, including additive mode, baking enabled and point light shadows disabled.

You can otherwise configure the LTCGI Light Volume the same way as any other volume. Specify the density and sample layout you want, and set blending options.

![Screenshot of an LTCGI Light Volume component](../img/ltcgi_lv.jpg)

On each `LTCGI_Screen` or `LTCGI_Emitter` that should contribute to Light Volumes:

* Set `Diffuse Mode` to `Lightmap Diffuse`
* Select a `Lightmap Channel`
* Leave `Affect Light Volumes` enabled

Then run `Bake LTCGI Shadowmap and Standard Lightmap` from the LTCGI Controller. During and after the bake, LTCGI automatically creates the Light Volume data it needs under `Assets/LTCGI-Generated`.

## Runtime

Just enter playmode or upload your VRChat world and it should work out of the box!

Behind the scenes, LTCGI automatically adds an `LV_LTCGI_Adapter` Udon behaviour to the `LTCGI_Controller` when VRC Light Volumes is available. The adapter connects the scene's `LightVolumeManager` to LTCGI's generated post-processor CRT and keeps the enabled LTCGI light volume list in sync.

If your `LightVolumeManager` has `Auto Update Volumes` disabled, the adapter only updates once at runtime. Enable `Auto Update Volumes` if the enabled volume set changes during play.

⚠️ **There is a performance overhead for running an LTCGI-enabled Light Volume in your scenes, and it scales by the number and density of _all_ LVs in your scene (not just LTCGI ones).** However, this overhead is fairly small.

## Notes

`Affect Light Volumes` is separate from `Affect Avatars`. Prefer Light Volumes for avatar lighting over LTCGI on avatar shaders.

Only screens and emitters using `Lightmap Diffuse` with a selected lightmap channel can affect Light Volumes. Pure specular, LTC diffuse and unbaked screens are not written into Light Volumes data.