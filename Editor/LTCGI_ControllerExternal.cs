#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
#if VRC_SDK_VRCSDK2 || UDONSHARP
using VRC.SDKBase.Editor.BuildPipeline;
#endif
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(LTCGI_Controller))]
    public class LTCGI_ControllerEditor : Editor
    {
        const string VERSION = "v1.0.2";

        private static readonly string[] CONFIGURATION_PROPS = new[] {
            "StaticTextures",
            "DynamicRenderers",
            "CustomBlurChain",
            "LightmapIntensity",
            "LightmapMultiplier"
        };

        private static Texture Logo;

        private bool debugFoldout = false;

        private static string configPath;

        private static Dictionary<string, object> configChangedValues = new Dictionary<string, object>();

        private enum ConfigType
        {
            Boolean,
            Float,
        }

        public void OnEnable()
        {
            Logo = Resources.Load("LTCGI-LogoController") as Texture;

            if (configChangedValues != null)
            {
                configChangedValues.Clear();
            }
        }

        private bool _configChanged = false;

        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 150;
            GUI.Box(GUILayoutUtility.GetRect(300, 150, style), Logo, style);

            var rightAlignedLabel = new GUIStyle(EditorStyles.label);
            rightAlignedLabel.alignment = TextAnchor.MiddleRight;
            GUILayout.Label(VERSION, rightAlignedLabel);


            bool applyButtonPressed = false;
            bool revertButtonPressed = false;

            var resetCol = GUI.backgroundColor;

            LTCGIDocsHelper.DrawHelpButton("https://ltcgi.dev/Getting%20Started/Setup/Controller");

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var large = new GUIStyle(EditorStyles.wordWrappedLabel);
                large.fontSize = 32;
                GUILayout.Label("Please put exactly 1 instance of this prefab into your scene!", large);
                return;
            }

            #if !UDONSHARP
            if (LTCGI_Controller.RuntimeMode == LTCGI_Controller.LTCGIRuntimeMode.VRChatWorld)
            {
                const string msg = "LTCGI for VRChat requires at least UdonSharp 1.0 to be installed in your project!";
                Debug.LogError(msg);
                EditorGUILayout.HelpBox(msg, MessageType.Error);
            }
            #endif

            serializedObject.Update();

            if (GUILayout.Button("Force Update"))
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
            }

            if (GUILayout.Button("Precompute Static Textures"))
            {
                LTCGI_Controller.Singleton.CreateLODTextureArrays();
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PrecomputeOnBuild"));

            EditorGUILayout.Separator();

            LTCGIDocsHelper.DrawHelpButton("https://ltcgi.dev/Advanced/Shadowmaps", "Shadowmap Baking");

            if (!LTCGI_Controller.Singleton.bakeInProgress && GUILayout.Button("Bake Shadowmap"))
            {
                LTCGI_Controller.BakeLightmap();
            }
            else if (!LTCGI_Controller.Singleton.bakeInProgress && GUILayout.Button("Bake Shadowmap and Normal Lightmap"))
            {
                LTCGI_Controller.BakeLightmapFollowup();
            }
            if (LTCGI_Controller.Singleton.bakeInProgress && GUILayout.Button("Bake Shadowmap - FORCE FINISH"))
            {
                LTCGI_Controller.Singleton.BakeComplete();
            }

            GUI.backgroundColor = Color.red;
            if (LTCGI_Controller.Singleton.bakeMaterialReset_key != null && GUILayout.Button("DEBUG: Force Settings Reset after Bake"))
            {
                LTCGI_Controller.Singleton.ResetConfiguration();
            }
            
            if (!LTCGI_Controller.Singleton.bakeInProgress && LTCGI_Controller.Singleton.HasLightmapData() &&
                GUILayout.Button("Clear Baked Data"))
            {
                LTCGI_Controller.Singleton.ResetConfiguration();
                LTCGI_Controller.Singleton.ClearLightmapData();
                LTCGI_Controller.Singleton.UpdateMaterials();
            }
            GUI.backgroundColor = resetCol;

            EditorGUILayout.Space(); EditorGUILayout.Space();

            LTCGI_Controller.DrawAutoSetupEditor(LTCGI_Controller.Singleton);

            EditorGUILayout.Space(); EditorGUILayout.Space();

            if (LTCGI_Controller.Singleton.cachedMeshRenderers != null && LTCGI_Controller.Singleton._LTCGI_ScreenTransforms != null)
            {
                    EditorGUILayout.HelpBox(
$@"Affected Renderers Total: {LTCGI_Controller.Singleton.cachedMeshRenderers.Length}
LTCGI_Screen Components: {LTCGI_Controller.Singleton._LTCGI_ScreenTransforms.Count(x => x != null)} / {LTCGI_Controller.MAX_SOURCES}
AudioLink: {(LTCGI_Controller.AudioLinkAvailable == LTCGI_Controller.AudioLinkAvailability.Unavailable ? "Not Detected" : (LTCGI_Controller.AudioLinkAvailable == LTCGI_Controller.AudioLinkAvailability.AvailableAsset ? "Detected (Asset)" : "Detected (Package)"))}",
                    MessageType.Info, true
                );

                if (LTCGI_Controller.AudioLinkAvailable == LTCGI_Controller.AudioLinkAvailability.Unavailable)
                {
                    if (GUILayout.Button("Re-Detect AudioLink"))
                    {
                        LTCGI_Controller.audioLinkAvailable = LTCGI_Controller.AudioLinkAvailability.NeedsCheck;
                        var _ignored = LTCGI_Controller.AudioLinkAvailable;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Hit \"Force Update\" or CTRL-S to calculate info!", MessageType.Info);
            }

            EditorGUILayout.Space(); EditorGUILayout.Space();
            var header = new GUIStyle(EditorStyles.boldLabel);
            header.fontSize += 4;
            GUILayout.Label("LTCGI Configuration", header);
            EditorGUILayout.Space();

            var vidTex = serializedObject.FindProperty("VideoTexture");
            EditorGUILayout.PropertyField(vidTex, true);
            if (vidTex.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Video Texture is not set! This means video player will not reflect their screen. use Auto-Configure options above or refer to documentation on how to set this up if required.", MessageType.Warning);
            }

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

            if (_configChanged)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var bigButton = new GUIStyle(GUI.skin.button);
                    bigButton.fixedHeight = 40.0f;
                    bigButton.fontStyle = FontStyle.Bold;
                    bigButton.fontSize = 18;
                    bigButton.normal.textColor = Color.white;
                    bigButton.hover.textColor = Color.white;
                    resetCol = GUI.backgroundColor;
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Apply", bigButton))
                    {
                        applyButtonPressed = true;
                    }
                    GUI.backgroundColor = Color.blue;
                    if (GUILayout.Button("Revert", bigButton))
                    {
                        revertButtonPressed = true;
                    }
                    GUI.backgroundColor = resetCol;
                }
            }

            RecalculateAutoConfig(target as LTCGI_Controller);

            var config = File.ReadAllLines(configPath);
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
                    var name = line.Substring(line.IndexOf(' ') + 1);
                    var nextSpace = name.IndexOf(' ');
                    if (nextSpace < 0) nextSpace = name.Length;
                    name = name.Substring(0, nextSpace);

                    object ccvValue;
                    var existsInCcv = configChangedValues.TryGetValue(name, out ccvValue);

                    var debug = description.StartsWith("[DEBUG]");
                    description = description.Replace(Environment.NewLine, " ");

                    var type = line.Count(char.IsWhiteSpace) > 1 ? ConfigType.Float : ConfigType.Boolean;

                    if (type == ConfigType.Boolean)
                    {
                        var enabledInConfig = line.StartsWith("#");
                        var enabled = (existsInCcv && (bool)ccvValue) || (!existsInCcv && enabledInConfig);

                        var toggleStyle = new GUIStyle(GUI.skin.toggle);
                        if (debug)
                        {
                            toggleStyle.normal.textColor = Color.gray;
                            toggleStyle.hover.textColor = Color.gray;
                        }
                        var set = GUILayout.Toggle(enabled, name, toggleStyle);

                        EditorGUILayout.HelpBox(description, MessageType.None, true);
                        EditorGUILayout.Space();

                        if (set != enabledInConfig)
                        {
                            configChangedValues[name] = set;
                            if (set)
                            {
                                config[i] = config[i].Substring(2);
                            }
                            else
                            {
                                config[i] = "//" + config[i];
                            }
                        }
                        else
                        {
                            configChangedValues.Remove(name);
                        }
                    }
                    else if (type == ConfigType.Float)
                    {
                        var valueInConfig = float.Parse(line.Substring(line.LastIndexOf(' ')).Replace('f', ' '), System.Globalization.CultureInfo.InvariantCulture);
                        var value = existsInCcv ? (float)ccvValue : valueInConfig;

                        var labelStyle = new GUIStyle(GUI.skin.label);
                        if (debug)
                        {
                            labelStyle.normal.textColor = Color.gray;
                            labelStyle.hover.textColor = Color.gray;
                        }
                        float set;
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label(name, labelStyle);
                            set = EditorGUILayout.FloatField(value);
                        }

                        EditorGUILayout.HelpBox(description, MessageType.None, true);
                        EditorGUILayout.Space();

                        if (set != valueInConfig)
                        {
                            configChangedValues[name] = set;
                            config[i] = $"#define {name} {set}";
                        }
                        else
                        {
                            configChangedValues.Remove(name);
                        }
                    }

                    resetDesc = true;
                }
            }

            _configChanged = configChangedValues.Count > 0;

            if (applyButtonPressed)
            {
                File.WriteAllLines(configPath, config);
                AssetDatabase.Refresh();
                configChangedValues = new Dictionary<string, object>();
            }
            else if (revertButtonPressed)
            {
                configChangedValues = new Dictionary<string, object>();
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
        
        public static void RecalculateAutoConfig(LTCGI_Controller controller)
        {
            if (configPath == null || !File.Exists(configPath))
            {
                configPath = AssetDatabase.GUIDToAssetPath("01c8aa443e7001b45b28cfe65d1c6786");
                if (string.IsNullOrEmpty(configPath))
                {
                    Debug.LogError("LTCGI: Could not find config file! Please don't change the GUID or move the meta file!");
                    return;
                }
            }

            var config = File.ReadAllLines(configPath);
            var changed = false;
            for (int i = 0; i < config.Length; i++)
            {
                string lineRaw = config[i];
                var line = lineRaw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.EndsWith("#define LTCGI_AUDIOLINK"))
                {
                    var enabledInConfig = !line.StartsWith("//");
                    var available = LTCGI_Controller.AudioLinkAvailable != LTCGI_Controller.AudioLinkAvailability.Unavailable;
                    if (enabledInConfig != available)
                    {
                        config[i] = (available ? "" : "//") + "#define LTCGI_AUDIOLINK";
                        changed = true;
                    }
                }
                if (line.StartsWith("#include")) // cursed, but audiolink is the only include for now
                {
                    var newConfig = (LTCGI_Controller.AudioLinkAvailable != LTCGI_Controller.AudioLinkAvailability.Unavailable ? (
                        LTCGI_Controller.AudioLinkAvailable == LTCGI_Controller.AudioLinkAvailability.AvailableAsset ?
                        "#include \"Assets/AudioLink/Shaders/AudioLink.cginc\"" :
                        "#include \"Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc\""
                    ) : "#include \"not-available\"");
                    if (config[i] != newConfig)
                    {
                        config[i] = newConfig;
                        changed = true;
                    }
                }

                if (controller != null)
                {
                    if (line.EndsWith("#define LTCGI_STATIC_UNIFORMS"))
                    {
                        var enabledInConfig = !line.StartsWith("//");
                        var available = !controller.HasDynamicScreens;
                        if (enabledInConfig != available)
                        {
                            config[i] = (available ? "" : "//") + "#define LTCGI_STATIC_UNIFORMS";
                            changed = true;
                        }
                    }

                    if (line.EndsWith("#define LTCGI_CYLINDER"))
                    {
                        var enabledInConfig = !line.StartsWith("//");
                        var available = controller.HasCylinders;
                        if (enabledInConfig != available)
                        {
                            config[i] = (available ? "" : "//") + "#define LTCGI_CYLINDER";
                            changed = true;
                        }
                    }
                }

                if (line.EndsWith("#define LTCGI_AVATAR_MODE"))
                {
                    var enabledInConfig = !line.StartsWith("//");
                    var enabledByProject = LTCGI_Controller.RuntimeMode == LTCGI_Controller.LTCGIRuntimeMode.VRChatAvatar;
                    if (enabledInConfig != enabledByProject)
                    {
                        config[i] = (enabledByProject ? "" : "//") + "#define LTCGI_AVATAR_MODE";
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                File.WriteAllLines(configPath, config);
                AssetDatabase.Refresh();
            }
        }
    }

    public static class LTCGIDocsHelper
    {
        public static void DrawHelpButton(string url, string name = "Help &")
        {
            EditorGUILayout.Space();
            var bigButton = new GUIStyle(GUI.skin.button);
            bigButton.fixedHeight = 30.0f;
            bigButton.fontStyle = FontStyle.Bold;
            bigButton.fontSize = 14;
            bigButton.normal.textColor = Color.white;
            bigButton.hover.textColor = Color.white;
            var resetCol = GUI.backgroundColor;
            GUI.backgroundColor = Color.blue;
            if (GUILayout.Button($"Open {name} Documentation", bigButton))
            {
                System.Diagnostics.Process.Start(url);
            }
            GUI.backgroundColor = resetCol;
            EditorGUILayout.Space();
        }
    }

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

    #if UDONSHARP
    public class VRCSDKHookLTCGI : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 68;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (LTCGI_Controller.Singleton != null)
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
                if (LTCGI_Controller.Singleton.PrecomputeOnBuild)
                {
                    LTCGI_Controller.Singleton.CreateLODTextureArrays();
                }
            }
            return true;
        }
    }
    #else
    public class PostBuildCallbackLTCGI : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => 68;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (LTCGI_Controller.Singleton != null)
            {
                LTCGI_Controller.Singleton.UpdateMaterials();
                if (LTCGI_Controller.Singleton.PrecomputeOnBuild)
                {
                    LTCGI_Controller.Singleton.CreateLODTextureArrays();
                }
            }
        }
    }
    #endif
    #endif
}
