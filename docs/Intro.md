---
id: Intro
slug: /
sidebar_position: 1
---

# ✨ About LTCGI

LTCGI is an optimized plug-and-play realtime area light solution using the [linearly transformed cosine algorithm](#LTC) for standalone Unity and VRChat. Free to use with [attribution](#Attribution).

![screenshot of standalone demo app](https://raw.githubusercontent.com/PiMaker/ltcgi/main/Screenshots/demoapp.jpg)

You can [download](https://github.com/PiMaker/ltcgi/raw/main/DemoApp.zip) the standalone demo app pictured above to try it out for yourself.  
Alternatively, join the [demo world](https://vrchat.com/home/launch?worldId=wrld_aa2627ec-c63a-4db2-aa3e-9078d41c6d9c) in VRChat.

*NOTE: While the shader is optimized, it still equates to real-time lighting - so use sparingly, and read these docs carefully for tips on performance! Often times a reflection probe of a simple realtime light are the better solutions!*

## Getting Started

The 'Getting Started' row on the left is a good place to start reading.

I also recommend reading _at least_ the [Performance Optimization](/Advanced/Performance_Optimization) section from the Advanced row.

### About this Page

This site is the official documentation for using LTCGI-based lighting in your projects and worlds. It covers everything you should know before using LTCGI. I highly recommend skimming all the pages at least once to gain a basic understanding of what journey you're about to embark on.

I've tried to keep things simple, but do note that LTCGI is an _advanced_ lighting solution. If this is your first time touching Unity, this may simply not be for you - stuff _will_ break, any you _will_ need to read error messages and troubleshoot things.

Read the [FAQ](/FAQ) before asking for help anywhere!

**Once you've done that**, feel free to join my Discord and ask for help: https://discord.gg/r38vJd2DuJ

## Attribution

According to the [License](https://raw.githubusercontent.com/PiMaker/ltcgi/main/LICENSE) you are free to use this in your world, but you need to give credit. You are free to do so in whichever way, but you must provide a link to the [GitHub repository](https://github.com/pimaker/ltcgi), such as to fulfill the imported license of the LTC example code used as a base for this project.

For your convenience, a prefab called `LTCGI Attribution` is provided in the package. **You do not need to use this as-is, as long as a link to the [GitHub repository](https://github.com/pimaker/ltcgi) is provided!**

![LTCGI Attribution Prefab](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/attribution.jpg)

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

### More glamour shots

![](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/demo.gif)

![](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/collage4.jpg)
![](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/collage2.jpg)