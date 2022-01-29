#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRC.SDKBase.Editor.BuildPipeline;
#endif
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(LTCGI_Controller))]
    public class LTCGI_ControllerEditor : Editor
    {
        private static readonly string[] CONFIGURATION_PROPS = new[] {
            "VideoTexture",
            "StaticTextures",
            "DynamicRenderers",
            "CustomBlurChain",
            "LightmapIntensity",
            "LightmapMultiplier"
        };

        private static Texture Logo;

        private bool debugFoldout = false;

        private static string configPath;

        public void OnEnable()
        {
            Logo = Resources.Load("LTCGI-LogoController") as Texture;
        }

        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 150;
            GUI.Box(GUILayoutUtility.GetRect(300, 150, style), Logo, style);

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var large = new GUIStyle(EditorStyles.wordWrappedLabel);
                large.fontSize = 32;
                GUILayout.Label("Please put exactly 1 instance of this prefab into your scene!", large);
                return;
            }

            serializedObject.Update();

            if (GUILayout.Button("Force Update"))
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
            }

            if (GUILayout.Button("Precompute Static Textures"))
            {
                LTCGI_Controller.Singleton.CreateLODTextureArrays();
            }

            if (!LTCGI_Controller.Singleton.bakeInProgress && GUILayout.Button("Bake Shadowmap"))
            {
                LTCGI_Controller.BakeLightmap();
            }
            if (LTCGI_Controller.Singleton.bakeInProgress && GUILayout.Button("Bake Shadowmap - FORCE FINISH"))
            {
                LTCGI_Controller.Singleton.BakeComplete();
            }

            EditorGUILayout.Space(); EditorGUILayout.Space(); EditorGUILayout.Space();
            var header = new GUIStyle(EditorStyles.boldLabel);
            header.fontSize += 4;
            GUILayout.Label("LTCGI Configuration", header);
            EditorGUILayout.Space();

            foreach (var prop in CONFIGURATION_PROPS)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(prop), true);
            }

            // multiplier clamp, negative light go brrr
            var lmm = serializedObject.FindProperty("LightmapMultiplier");
            lmm.vector3Value = new Vector3(Mathf.Max(0.0f, lmm.vector3Value.x), Mathf.Max(0.0f, lmm.vector3Value.y), Mathf.Max(0.0f, lmm.vector3Value.z));

            EditorGUILayout.Space(); EditorGUILayout.Space(); EditorGUILayout.Space();
            GUILayout.Label("Global Shader Options", header);
            EditorGUILayout.Space();

            if (configPath == null || !File.Exists(configPath))
            {
                // FIXME: make more robust
                var guids = AssetDatabase.FindAssets("LTCGI_config");
                if (guids.Length != 1)
                {
                    Debug.LogError($"Could not find LTCGI_config.cginc ({guids.Length})! Please reimport package.");
                    return;
                }
                configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            var config = File.ReadAllLines(configPath);
            var changed = false;
            var description = "";
            var resetDesc = false;
            for (int i = 0; i < config.Length; i++)
            {
                string lineRaw = config[i];
                var line = lineRaw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("///"))
                {
                    if (resetDesc)
                    {
                        description = "";
                        resetDesc = false;
                    }
                    else if (!string.IsNullOrEmpty(description))
                    {
                        description += Environment.NewLine;
                    }
                    description += line.Substring(3).Trim();
                }
                else if (description != "" && (line.StartsWith("//#define") || line.StartsWith("#define")))
                {
                    var enabled = line.StartsWith("#");

                    var set = GUILayout.Toggle(enabled, line.Substring(line.IndexOf(" ")));
                    EditorGUILayout.HelpBox(description.Replace(Environment.NewLine, " "), MessageType.None, true);
                    EditorGUILayout.Space();

                    if (set != enabled)
                    {
                        // config option changed
                        if (set)
                        {
                            config[i] = config[i].Substring(2);
                        }
                        else
                        {
                            config[i] = "//" + config[i];
                        }
                        changed = true;
                    }

                    resetDesc = true;
                }
            }
            if (changed)
            {
                File.WriteAllLines(configPath, config);
                AssetDatabase.Refresh();
            }

            EditorGUILayout.Space(); EditorGUILayout.Space(); EditorGUILayout.Space();
            if ((debugFoldout = EditorGUILayout.Foldout(debugFoldout, "[ Debug Menu (Default Inspector) ]")))
            {
                DrawDefaultInspector();
            }

            var update = serializedObject.hasModifiedProperties;
            serializedObject.ApplyModifiedProperties();

            if (update)
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
            }
        }
    }
    
    // [InitializeOnLoad]
    // static class LTCGI_Loader
    // {
    //     static LTCGI_Loader()
    //     {
    //         // force load on project startup
    //         var controllers = AssetDatabase.FindAssets("t:" + nameof(LTCGI_Controller));
    //         foreach (var guid in controllers)
    //         {
    //             var cpath = AssetDatabase.GUIDToAssetPath(guid);
    //             AssetDatabase.LoadAssetAtPath<LTCGI_Controller>(cpath);
    //         }
    //     }
    // }

    // automatic callbacks
    public class ShaderPostprocessLTCGI : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (LTCGI_Controller.Singleton != null)
            {
                EditorApplication.delayCall += LTCGI_Controller.Singleton.UpdateMaterials;
            }
        }
    }

    #if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
    public class VRCSDKHookLTCGI : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 68;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (LTCGI_Controller.Singleton != null)
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
                //LTCGI_Controller.Singleton.CreateLODTextureArrays();
            }
            return true;
        }
    }
    #endif
    #endif
}