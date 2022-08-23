---
sidebar_position: 3
---

# 2️⃣ Setting up the Controller

The main component of LTCGI is the `LTCGI_Controller`. It controls everything related to LTCGI and bakes the required data into new builds. Note that the controller itself does _not_ get uploaded to VRChat or included into your final build! It only exists in the editor.

## Putting the Controller into your Scene

To begin with, drag the `LTCGI_Controller.prefab` somewhere into your scene. It is recommended to put it into the root of your hierarchy. **You must only ever have 1 of these in your scene!**

![dragging the controller into your scene](../../vid/drag_in_controller.webm)