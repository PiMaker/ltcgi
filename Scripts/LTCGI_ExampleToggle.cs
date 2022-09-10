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
