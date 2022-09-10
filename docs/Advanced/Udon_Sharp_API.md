---
sidebar_position: 6
---

# 🍜 UdonSharp API

There are several functions for runtime control available, all of them callable on the LTCGI_UdonAdapter instance. To use them, put a public property of type `LTCGI_UdonAdapter` in your script, and assign it the LTCGI_Controller game object (which will automatically contain the correct Udon script).

## Global Settings

You can use the following function on the adapter to globally enable or disable LTCGI. Note that material-swapping to a material with LTCGI disabled in addition to using this method is still recommended for best performance.
```csharp
public void _SetGlobalState(bool enabled)
```

You can also change the global realtime video texture input at runtime with the following function:
```csharp
public void _SetVideoTexture(Texture texture);
```
Keep in mind that this operation is fairly expensive and should only be called when necessary.

## Per-Screen settings

The first thing to when handling individual screens (preferably in `Start`, not `Update`, as this is a fairly expensive call) is to get the index of the screen you want to access/modify. The index will act as a unique identifier. To do so, call:

```csharp
public int _GetIndex(GameObject screen);
```

...with the GameObject that contains the `LTCGI_Screen` component. Note that for this to work, only one `LTCGI_Screen` component is allowed per GameObject. The index itself is internal and should be used for anything other than passing it on to other functions - treat it like an unknown value.

With the index at hand, you can call the following functions:

```csharp
public Color _GetColor(int screen);
public void _SetColor(int screen, Color color);
public void _SetTexture(int screen, uint index);
```

`screen` parameters take the index retrieved before.

**NOTE:** The `color` parameter and returned color value may be in an unexpected color space. Use `Color.linear` and `Color.gamma` to convert them. Generally speaking, `_SetColor` takes `color.linear` if you want the reflected color to match the one put as `_Color` on an Unlit object such as a screen.
