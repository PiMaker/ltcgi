#if UNITY_EDITOR && UDONSHARP
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using UdonSharp.Video;

namespace pi.LTCGI
{
    public class LTCGI_USharpVideoAdapterAutoSetup : ILTCGI_AutoSetup
    {
        private static readonly Vector2 DefaultScale = new Vector2(7.63f, 4.2925f);

        public LTCGI_USharpVideoAdapterAutoSetup()
        {
        }

        public GameObject AutoSetupEditor(LTCGI_Controller controller)
        {
            #pragma warning disable 618
            var usharpPlayers = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(sceneRoot => sceneRoot.GetUdonSharpComponentsInChildren<USharpVideoPlayer>());
            #pragma warning restore 618
            var first = true;
            foreach (var player in usharpPlayers)
            {
                if (first)
                {
                    EditorGUILayout.LabelField("Detected U# Video Players in scene:");
                    first = false;
                }
                if (GUILayout.Button($"Auto-Configure '{VRC.Core.ExtensionMethods.GetHierarchyPath(player.gameObject.transform)}'"))
                {
                    var adapter = new GameObject("LTCGI_USharpVideoAdapter");
                    adapter.transform.parent = controller.transform;
                    adapter.transform.position = player.transform.position;
                    adapter.transform.rotation = player.transform.rotation;

                    var script = adapter.AddUdonSharpComponent<LTCGI_USharpVideoAdapter>();
                    #pragma warning disable 618
                    script.UpdateProxy();
                    #pragma warning restore 618
                    script.VideoPlayer = player;
                    script.CRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(AssetDatabase.GUIDToAssetPath("802e4542fd374664aa4d0858e525b454") /* LTCGI_BlitCRT.asset */);

                    controller.VideoTexture = script.CRT;

                    // attempt to read standby texture from player
                    #pragma warning disable 618
                    var handler = player.GetUdonSharpComponentInChildren<VideoScreenHandler>();
                    #pragma warning restore 618
                    if (handler != null)
                    {
                        script.StandbyTexture = handler.standbyTexture;
                    }
                    else
                    {
                        script.StandbyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("68718da77206620438ca14e29cefa6fb") /* black1px.png */);
                    }
                    #pragma warning disable 618
                    script.ApplyProxyModifications();
                    #pragma warning restore 618

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.parent = adapter.transform;
                    quad.transform.localScale = DefaultScale * player.transform.lossyScale;
                    quad.transform.localEulerAngles = Vector3.zero;
                    quad.transform.localPosition = Vector3.zero;
                    quad.transform.GetComponent<MeshRenderer>().enabled = false;
                    Component.DestroyImmediate(quad.transform.GetComponent<Collider>());
                    quad.name = "LTCGI Video Screen";

                    var ltcgi = quad.AddComponent<LTCGI_Screen>();
                    ltcgi.ColorMode = ColorMode.Texture;
                    ltcgi.Specular = true;
                    ltcgi.Diffuse = true; // LTC Diffuse by default
                    ltcgi.TextureIndex = 0;

                    EditorUtility.DisplayDialog("Auto-Configure", "Auto-Configured LTCGI_USharpVideoAdapter. Please make sure the 'LTCGI Video Screen' object has the same position, rotation and scale in your scene as your actual video screen.", "OK");
                    EditorGUIUtility.PingObject(quad);

                    return adapter;
                }
            }

            return null;
        }
    }
}

#endif