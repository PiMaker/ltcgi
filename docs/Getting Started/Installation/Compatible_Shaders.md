---
sidebar_position: 5
---

# 3️⃣ Compatible Shaders

For any object to receive lighting and reflections from LTCGI-enabled screens or emitters, it **must** use a supported shader. The list of currently supported shaders can be found [on GitHub](https://github.com/PiMaker/ltcgi#supported-shaders).

Objects that use a different material **will not** receive any lighting. Note that you _can_ mix and match, if you only want some objects to receive lighting for performance or aesthetic reasons.

> ⚠️ Some of the supported shaders need to use a specific variant to support LTCGI, or have a toggle (sometimes in "advanced" sections) that you need to enable for it to work! Check the documentation for the shader you are using on how to enable LTCGI.

LTCGI comes bundled with a basic surface shader that can be used for test purposes. This video shows how to set it up on a new material:

<video controls loop width="100%">
  <source src="/vid/create_material.webm"/>
</video>

The material used in the video comes from [ambientCG](https://ambientcg.com).