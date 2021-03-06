# LTCGI

Optimized plug-and-play realtime area lighting using the [linearly transformed cosine algorithm](#LTC) for Unity/VRChat. Free to use with [attribution](#Attribution).

**NOTE: While the shader is optimized, it still equates to real-time lighting - so use sparingly, and read the [Wiki](https://github.com/pimaker/ltcgi/wiki) on performance optimization!**

Join the Discord for support or other questions: https://discord.gg/r38vJd2DuJ

![Screenshot of Demo App](./Screenshots/demoapp.jpg)

You can [download](https://github.com/PiMaker/ltcgi/raw/main/DemoApp.zip) the standalone demo app pictured above to try it out for yourself.  
Alternatively, join the [demo world](https://vrchat.com/home/launch?worldId=wrld_aa2627ec-c63a-4db2-aa3e-9078d41c6d9c) in VRChat.

![Demo Video](./Screenshots/demo.gif)  
[Demo Video](https://www.youtube.com/watch?v=DJXgspErWlU)

## Supported shaders

To use LTCGI, all objects that should receive lighting must use a compatible shader. Currently compatible ones are listed below. If you implement LTCGI into your shader, feel free to send a PR to be included.

* [ORL Shader Family](https://shaders.orels.sh/) by [@orels1](https://github.com/orels1)
* [Silent's Filamented](https://gitlab.com/s-ilent/filamented)
* [Mochie's Unity Shaders](https://github.com/MochiesCode/Mochies-Unity-Shaders)
* [Hekky Shaders](https://github.com/hyblocker/hekky-shaders)
* Basic "Unlit" Test Shader (included)
* Surface Shader (included)

## How to use / Download

See the [Wiki](https://github.com/pimaker/ltcgi/wiki) for instructions. Please check it out before using LTCGI!  
Also consider the [attribution requirements](#Attribution).

Check the [Releases](https://github.com/pimaker/ltcgi/releases) tab for downloads.

### Dependencies for VRChat
* [UdonSharp](https://github.com/MerlinVR/UdonSharp)
* [CyanEmu](https://github.com/CyanLaser/CyanEmu) (optional, but highly recommended)

You do *not* need those if you plan to use LTCGI in a standalone Unity project!

In that case, just make sure your color space is set to linear:

![Unity Color Space Setting must be Linear](Screenshots/LinearColorSpace.jpg)

### Debug builds

*NOTE: These are provided as-is with no guarantees. Feel free to report any issues on the [Discord](https://discord.gg/r38vJd2DuJ), but remember that it is explicitly recommended to use the downloads from the [Releases](https://github.com/pimaker/ltcgi/releases) tab instead!*

Check the latest build from the [Actions tab](https://github.com/pimaker/ltcgi/actions/workflows/main.yml?query=is%3Asuccess) and download the corresponding *artifact* (comes as a zip you need to extract first).

## Attribution

According to the [License](#License) you are free to use this in your world, but you need to give credit. You are free to do so in whichever way, but you must provide a link to this GitHub repository, such as to fulfill the imported license of the LTC example code used as a base for this project.

For your convenience, a prefab called `LTCGI Attribution` is provided in the package.

![LTCGI Attribution Prefab](Screenshots/attribution.jpg)

If you don't want to use it, instead display text similar to the following:

```
This project/world uses LTCGI by _pi_, see 'github.com/pimaker/ltcgi'.
```

## LTC

Based on this paper:
```
Real-Time Polygonal-Light Shading with Linearly Transformed Cosines.
Eric Heitz, Jonathan Dupuy, Stephen Hill and David Neubelt.
ACM Transactions on Graphics (Proceedings of ACM SIGGRAPH 2016) 35(4), 2016.
Project page: https://eheitzresearch.wordpress.com/415-2/
```
[Read more](https://eheitzresearch.wordpress.com/415-2/)

## Screenshots

![Screenshot](./Screenshots/collage4.jpg)  
([LTCGI demo hall](https://vrchat.com/home/launch?worldId=wrld_aa2627ec-c63a-4db2-aa3e-9078d41c6d9c))

![Screenshot](./Screenshots/collage2.jpg)  
(venue designed by BananaBread)

![Screenshot](./Screenshots/collage3.jpg)  
(various surfaces)

![Screenshot](./Screenshots/collage1.jpg)  
(static textures, shadows and glass)

## License

This project is made available under the terms of the MIT license, unless explicitly marked otherwise in the source files. See `LICENSE` for more.

The following files are licensed explicitly, and may not be modified or used in commercial projects, but can be redistributed and displayed otherwise, provided this license is kept:

* Propaganda/pi_graffiti.png
* Propaganda/ltcgi_graffiti.png
