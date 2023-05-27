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

    static void AddDefine()
    {
        var platform = EditorUserBuildSettings.selectedBuildTargetGroup;
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(platform);
        if (!defines.Contains("LTCGI_INCLUDED"))
        {
            if (defines.Length > 0)
            {
                defines += ";";
            }
            defines += "LTCGI_INCLUDED";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(platform, defines);
        }
    }

    static LTCGI_Define()
    {
        AddDefine();
    }

    public void OnActiveBuildTargetChanged(BuildTarget prev, BuildTarget cur)
    {
        AddDefine();
    }
}

#endif
