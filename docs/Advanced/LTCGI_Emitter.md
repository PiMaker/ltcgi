---
sidebar_position: 9
---

# 🔦 The `LTCGI_Emitter` component

This is very similar to the [LTCGI_Screen](</Getting Started/Setup/LTCGI_Screen>) component, but with different tradeoffs:

* It _only_ supports lightmap-diffuse lighting mode, no specular, no LTC (i.e. it requires a [Shadowmap bake](/Advanced/Shadowmaps))
* It does _not_ support textured mode (and thus no mesh UVs will be taken into consideration for the emissive color)
* It _does_ however support arbitrary geometry!
* It is _very_ cheap compared to the performance of a Screen component

The intended use case is for static geometry that you want to make emissive, but still have some control over color and intensity of. It also combines very well with AudioLink color mode, where you can have static geometry pulse to AudioLink channels (e.g. for trim lighting in clubs, etc.).

## Usage

You only need one Emitter component per "light source". That is, you can put multiple meshes into one Emitter component (and it is recommended to so, as opposed to creating multiple Emitters).

The options you can select are similar to those on the [LTCGI_Screen](</Getting Started/Setup/LTCGI_Screen>) component, refer to that page for documentation on how to use them.

The exception is the **"Emissive Renderers"** list at the very bottom. You must put all mesh renderers that you want affected by the emissive properties into this list. Note that the GameObject the Emitter component is on _can_ be included in this list, but doesn't _have_ to be. In fact, you can put the component on a completely separate GameObject somewhere in your hierarchy, and then just put the Renderers you want affected into this list. Note however, that the "Distance" based affected target selector will be based on the transform position of the Emitter component.

## U# API

For the API you can treat an Emitter component the same as a Screen component. It will have a normal screen index.