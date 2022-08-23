---
sidebar_position: 1
---

# 1️⃣ VRChat Worlds

# Pre-Setup

Before installing LTCGI into your project, make sure you have all prerequisites installed. This includes making sure you can successfully build and upload your world to VRChat _before_ adding LTCGI!

## Creator Companion

I recommend using the [VRChat Creator Companion](https://vcc.docs.vrchat.com/) to automate the steps below. Simply create a new project with the `UdonSharp` template, and you're good to go!

## Manual Installation

* You must use the latest supported version of the **VRChat Worlds SDK** in your project. Also ensure that you are using the correct Unity editor version for that SDK.

* [UdonSharp](https://udonsharp.docs.vrchat.com/)

* [ClientSim](https://clientsim.docs.vrchat.com/)

The only hard dependency is UdonSharp. It is recommended to use U# 1.0 or higher, as installed by the [VRChat Creator Companion](https://vcc.docs.vrchat.com/). For now, LTCGI _should_ be compatible with older versions too.

I do however very highly recommend ClientSim, as it allows you to test your world straight in the editor.

## Installing LTCGI

Download the latest version from the [Releases Tab](https://github.com/pimaker/ltcgi/releases) over on GitHub. You only need the `ltcgi_X.Y.Z.unitypackage` file for now.

Then, simply install it into your project!

<video controls loop width="100%">
  <source src="/vid/installing_unitypackage.webm"/>
</video>