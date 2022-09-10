---
sidebar_position: 11
---

# 🪄 For Shader Authors

The recommended way to include LTCGI support in your shader is by including the `LTCGI.cginc` file in your shader from the import path of this prefab. I.e. do not copy the code and distribute it with your shader, but simply include it if it exists (you can check with the `LTCGI_INCLUDED` define in C#).

To have the controller recognize your shader as LTCGI compatible, you need to include a tag named `LTCGI` which can either have the value `ALWAYS` or the name of a shader property (I believe in upper-case only?) on which LTCGI support depends (e.g. a `[Toggle]` or `[ToggleUI]`).

See z3y's integration for a good example: https://github.com/z3y/shaders/commit/0ff546a874c6ab7dc12f5762f895f3cb76b8164e

Note that this method of distribution does not encumber your shader with any additional licenses.

> 🙋‍♂️ If you implement LTCGI into your shader, feel free to send a PR to be included in the README!

## Amplify

There is an Amplify example available at `_pi_/_LTCGI/Shaders/Amplify/LTCGI_Amplify.shader`.  
You can open that file in Amplify Shader Editor to see how it is configured.

In general, the steps are:

* Configure the `#include` and custom tag

![Amplify Example 1](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/amplify1.jpg)

* Add a `LTCGI_Contribution` node (under `Functions`) and connect it

![Amplify Example 3](https://github.com/PiMaker/ltcgi/raw/main/Screenshots/amplify3.jpg)  
Check the example linked above to see which parameter values are expected.