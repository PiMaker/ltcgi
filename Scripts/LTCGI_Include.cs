#if UNITY_EDITOR

// based on similar script from Bakery

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

[InitializeOnLoad]
public class LTCGI_Define : IActiveBuildTargetChanged
{
    public int callbackOrder => 0;

    static void AddDefine(string define)
    {
        var platform = EditorUserBuildSettings.selectedBuildTargetGroup;
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(platform);
        if (!defines.Contains(define))
        {
            if (defines.Length > 0)
            {
                defines += ";";
            }
            defines += define;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(platform, defines);
        }
    }

    static void ScanForAdapters()
    {
        var needsAdapterMove = false;
        if (AssetDatabase.GUIDToAssetPath("ecf7b0fb0de08cf47953bc100f818854") != "") // TVManagerV2.cs
        {
            //AddDefine("LTCGI_PROTV_DETECTED");
            needsAdapterMove = true;
        }
        if (AssetDatabase.GUIDToAssetPath("61a08afb94ef7364d8358a64333fb431") != "") // VideoPlayerManager.cs
        {
            //AddDefine("LTCGI_USHARP_VIDEO_DETECTED");
            needsAdapterMove = true;
        }

        if (needsAdapterMove && AssetDatabase.IsValidFolder("Packages/at.pimaker.ltcgi/Adapters"))
        {
            //AssetDatabase.MoveAsset("Packages/at.pimaker.ltcgi/Adapters", "Assets/_pi_/_LTCGI/Adapters");
        }
    }

    static LTCGI_Define()
    {
        AddDefine("LTCGI_INCLUDED");
        ScanForAdapters();
    }

    public void OnActiveBuildTargetChanged(BuildTarget prev, BuildTarget cur)
    {
        AddDefine("LTCGI_INCLUDED");
        ScanForAdapters();
    }
}

#endif
