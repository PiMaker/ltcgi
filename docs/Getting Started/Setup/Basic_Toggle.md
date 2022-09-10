---
sidebar_position: 8
---

# 7️⃣ Basic Toggle

For venues or other performance-heavy scenes, it might make sense to allow your users to disable LTCGI if they want.

The _recommended_ way of doing this is to simply material-swap all objects that have an LTCGI-enabled shader on them to a version that has LTCGI disabled. This will remove GPU overhead entirely.

Alternatively, the [UdonSharp API](/Advanced/Udon_Sharp_API) has a function `_SetGlobalState(bool)` that you can use to enable or disable LTCGI at will. This function is somewhat heavy and probably shouldn't be called per-frame, but cheap enough that you can use it directly on user input.

The LTCGI unitypackage contains an example script (also listed below) that shows how to use this. Note that because the function has a parameter, it can not be called from Udon-Graph at this time.

## Example Script

(this is written with VRChat Udon in mind, but can easily be translated to standalone)

```csharp
using UdonSharp;

// NOTE: This script has to be in the "_LTCGI/Scripts" folder, *or* reference
// the "LTCGI_AssemblyUdon" to allow it to use the "LTCGI_UdonAdapter" type!

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LTCGI_ExampleToggle : UdonSharpBehaviour
{
    // set this to your controller object (specifically the adapter object):
    public LTCGI_UdonAdapter Adapter;

    // set this however you want:
    public bool StartingState = true;
    private bool state;

    void Start()
    {
        state = StartingState;
        Adapter._SetGlobalState(state);
    }

    // you can make this a UI event as well!
    public override void Interact()
    {
        state = !state;
        Adapter._SetGlobalState(state);
    }
}
```

Put this script on an object with a collider (e.g. default sphere or cube) and click it in VRChat to toggle LTCGI.