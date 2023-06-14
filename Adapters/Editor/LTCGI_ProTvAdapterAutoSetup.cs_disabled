#if UNITY_EDITOR && UDONSHARP
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using ArchiTech;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;

namespace pi.LTCGI
{
    public class LTCGI_ProTvAdapterAutoSetup : ILTCGI_AutoSetup
    {
        // for modern model
        private static readonly Vector2 DefaultScale = new Vector3(2.4875f, 1.38f);
        private static readonly Vector3 DefaultPosition = new Vector3(0.0f, 1.151f, -0.121f);

        public LTCGI_ProTvAdapterAutoSetup()
        {
        }

        public GameObject AutoSetupEditor(LTCGI_Controller controller)
        {
            #pragma warning disable 618
            var protvPlayers = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(sceneRoot => sceneRoot.GetUdonSharpComponentsInChildren<TVManagerV2>());
            #pragma warning restore 618
            var first = true;
            foreach (var player in protvPlayers)
            {
                if (first)
                {
                    EditorGUILayout.LabelField("Detected Pro TVs in scene:");
                    first = false;
                }
                if (GUILayout.Button($"Auto-Configure '{VRC.Core.ExtensionMethods.GetHierarchyPath(player.gameObject.transform)}'"))
                {
                    var adapter = new GameObject("LTCGI_ProTvAdapter");
                    adapter.transform.parent = controller.transform;
                    adapter.transform.position = player.transform.position;
                    adapter.transform.rotation = player.transform.rotation;

                    var script = adapter.AddUdonSharpComponent<LTCGI_ProTvAdapter>();
                    #pragma warning disable 618
                    script.UpdateProxy();
                    #pragma warning restore 618
                    script.Tv = player;
                    script.SharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("77ef72900fca1b14b867f03b4d1f4ed5") /* LTCGI_AvProBlit_Material.mat */);
                    script.BlitCRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(AssetDatabase.GUIDToAssetPath("802e4542fd374664aa4d0858e525b454") /* LTCGI_BlitCRT.asset */);
                    script.AvProBranding = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ArchiTechAnon/ProTV/Images/ProTVLogo_16x9.png");

                    controller.VideoTexture = script.BlitCRT;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.parent = adapter.transform;
                    quad.transform.localScale = DefaultScale * player.transform.lossyScale;
                    quad.transform.localEulerAngles = Vector3.zero;
                    quad.transform.localPosition = DefaultPosition;
                    quad.transform.GetComponent<MeshRenderer>().enabled = false;
                    Component.DestroyImmediate(quad.transform.GetComponent<Collider>());
                    quad.name = "LTCGI Video Screen";

                    // generate adapter screens
                    var adapterScreensKey = new List<GameObject>();
                    var adapterScreensValue = new List<GameObject>();
                    var adapterScreensIsUnity = new List<bool>();
                    foreach (var screen in player.videoManagers)
                    {
                        var unity = screen.gameObject.GetComponent<VRCUnityVideoPlayer>() != null;
                        adapterScreensIsUnity.Add(unity);
                        adapterScreensKey.Add(screen.gameObject);

                        if (unity)
                        {
                            adapterScreensValue.Add(null);
                        }
                        else
                        {
                            var capture = new GameObject("LTCGI_ProTvAdapter_" + screen.name);
                            capture.transform.parent = adapter.transform;
                            capture.transform.localPosition = Vector3.zero;

                            var renderer = capture.AddComponent<MeshRenderer>();
                            renderer.sharedMaterials = new [] { script.SharedMaterial };
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                            renderer.allowOcclusionWhenDynamic = false;

                            var avpro = capture.AddComponent<VRCAVProVideoScreen>();
                            var serializedObject = new SerializedObject(avpro);
                            serializedObject.FindProperty("videoPlayer").objectReferenceValue = screen.GetComponent<VRCAVProVideoPlayer>();
                            serializedObject.FindProperty("useSharedMaterial").boolValue = true;
                            serializedObject.ApplyModifiedProperties();

                            adapterScreensValue.Add(capture);
                        }
                    }

                    script.AdapterScreensKey = adapterScreensKey.ToArray();
                    script.AdapterScreensValue = adapterScreensValue.ToArray();
                    script.AdapterScreensIsUnity = adapterScreensIsUnity.ToArray();
                    #pragma warning disable 618
                    script.ApplyProxyModifications();
                    #pragma warning restore 618

                    script._SetOverlayEnabled();

                    var ltcgi = quad.AddComponent<LTCGI_Screen>();
                    ltcgi.ColorMode = ColorMode.Texture;
                    ltcgi.Specular = true;
                    ltcgi.Diffuse = true; // LTC Diffuse by default
                    ltcgi.TextureIndex = 0;

                    EditorUtility.DisplayDialog("Auto-Configure", "Auto-Configured LTCGI_ProTvAdapter. Please make sure the 'LTCGI Video Screen' object has the same position, rotation and scale in your scene as your actual video screen. (The default transform is configured for the 'Modern Model' prefab!)", "OK");
                    EditorGUIUtility.PingObject(quad);

                    return adapter;
                }
            }

            return null;
        }
    }
}

#endif