#if UNITY_EDITOR
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
            var usharpPlayers = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(sceneRoot => sceneRoot.GetUdonSharpComponentsInChildren<USharpVideoPlayer>());
            var first = true;
            foreach (var player in usharpPlayers)
            {
                if (first)
                {
                    EditorGUILayout.LabelField("Detected U# Video Players in scene:");
                    first = false;
                }
                if (GUILayout.Button($"Auto-Configure '{player.gameObject.transform.GetHierarchyPath()}'"))
                {
                    var adapter = new GameObject("LTCGI_USharpVideoAdapter");
                    adapter.transform.parent = controller.transform;
                    adapter.transform.position = player.transform.position;
                    adapter.transform.rotation = player.transform.rotation;

                    var script = adapter.AddUdonSharpComponent<LTCGI_USharpVideoAdapter>();
                    script.UpdateProxy();
                    script.VideoPlayer = player;
                    script.CRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>("Assets/_pi_/_LTCGI/Adapters/LTCGI_BlitCRT.asset");

                    controller.VideoTexture = script.CRT;

                    // attempt to read standby texture from player
                    var handler = player.GetUdonSharpComponentInChildren<VideoScreenHandler>();
                    if (handler != null)
                    {
                        script.StandbyTexture = handler.standbyTexture;
                    }
                    else
                    {
                        script.StandbyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_pi_/_LTCGI/Adapters/black1px.png");
                    }
                    script.ApplyProxyModifications();

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