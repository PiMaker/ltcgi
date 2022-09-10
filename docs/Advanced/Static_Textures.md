---
sidebar_position: 3
---

# 🖼️ Static Textures

To use static textures, the controller needs to precompute some blurred LOD stages. This can be done automatically by assigning the static textures you want to use in the Controller, and then clicking "Precompute Static Textures". This needs to be done everytime a static texture changes!

Note that all static textures in a Scene must have the same size!

The textures computed this way can then be selected via the texture index slider on `LTCGI_Screen` components. Note that index 0 will always refer to the realtime "Video Texture" set in the Controller.

<video controls loop width="100%">
  <source src="/vid/static_texture.webm"/>
</video>