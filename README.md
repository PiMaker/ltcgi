# [LTCGI](https://ltcgi.dev)

LTCGI is an optimized plug-and-play realtime area light solution using the [linearly transformed cosine algorithm](#LTC) for standalone Unity and VRChat. Free to use with [attribution](#Attribution). It can utilize the Unity build-in lightmapper or [Bakery](https://assetstore.unity.com/packages/tools/level-design/bakery-gpu-lightmapper-122218) for realistic shadows on static objects.

![screenshot of standalone demo app](https://raw.githubusercontent.com/PiMaker/ltcgi/main/Screenshots/demoapp.jpg)

# Check out the [**official website**](https://ltcgi.dev) for documentation and a "Getting Started" guide! (https://ltcgi.dev)

Consider the [attribution requirements](#Attribution). Check the [Releases](https://github.com/pimaker/ltcgi/releases) tab for downloads.

You can also [download](https://github.com/PiMaker/ltcgi/raw/main/DemoApp.zip) the standalone demo app pictured above to try it out for yourself.  
Alternatively, join the [demo world](https://vrchat.com/home/launch?worldId=wrld_aa2627ec-c63a-4db2-aa3e-9078d41c6d9c) in VRChat.

[Read the FAQ](https://ltcgi.dev/FAQ) before asking for help anywhere! Once you've done that, feel free to join my Discord and ask for help: https://discord.gg/r38vJd2DuJ

## Supported shaders

To use LTCGI, all objects that should receive lighting must use a compatible shader. Currently compatible ones are listed below. If you implement LTCGI into your shader, feel free to send a PR to be included.

* [ORL Shader Family](https://shaders.orels.sh/) by [@orels1](https://github.com/orels1)
* [Silent's Filamented](https://gitlab.com/s-ilent/filamented)
* [Mochie's Unity Shaders](https://github.com/MochiesCode/Mochies-Unity-Shaders)
* [Hekky Shaders](https://github.com/hyblocker/hekky-shaders)
* Basic "Unlit" Test Shader (included)
* Surface Shader (included)

## Attribution

According to the [License](#License) you are free to use this in your world, but you need to give credit. You are free to do so in whichever way, but you must provide a link to this GitHub repository, such as to fulfill the imported license of the LTC example code used as a base for this project.

For your convenience, a prefab called `LTCGI Attribution` is provided in the package.

![LTCGI Attribution Prefab](Screenshots/attribution.jpg)

If you don't want to use it, instead display text similar to the following:

```
This project/world uses LTCGI by _pi_, see 'github.com/pimaker/ltcgi'.
```

# Licensing

## The LTC algorithm

Based on this paper:
```
Real-Time Polygonal-Light Shading with Linearly Transformed Cosines.
Eric Heitz, Jonathan Dupuy, Stephen Hill and David Neubelt.
ACM Transactions on Graphics (Proceedings of ACM SIGGRAPH 2016) 35(4), 2016.
Project page: https://eheitzresearch.wordpress.com/415-2/
```
[Read more](https://eheitzresearch.wordpress.com/415-2/)

## LTCGI

This project is made available under the terms of the MIT license, unless explicitly marked otherwise in the source files. See `LICENSE` for more.

The following files are licensed explicitly, and may not be modified or used in commercial projects, but can be redistributed and displayed otherwise, provided this license is kept:

* Propaganda/pi_graffiti.png
* Propaganda/ltcgi_graffiti.png
