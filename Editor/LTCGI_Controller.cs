// Uncomment for debug messages
//#define DEBUG_LOG

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

#if UDONSHARP
using UdonSharp;
using UdonSharpEditor;
#endif
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    [System.Serializable]
    public partial class LTCGI_Controller : MonoBehaviour
    {
        internal const int MAX_SOURCES = 16;

        [Tooltip("Intensity is set for each screen. Can be a RenderTexture for realtime updates (video players).")]
        public Texture VideoTexture;
        [Tooltip("Static textures are precomputed and *must* all be the same size. Make sure to click 'Precompute Static Textures' after any changes.")]
        public Texture2D[] StaticTextures;
        [Tooltip("Renderers that may change material during runtime. Otherwise only 'sharedMaterial's are updated for performance reasons.")]
        public Renderer[] DynamicRenderers;

        [Header("Expert Settings")]
        [Tooltip("Do not automatically set up the blur chain. Use this if you use AVPro to set _MainTex on the LOD1 material for example.")]
        public bool CustomBlurChain = false;

        [Tooltip("Apply an intensity multiplier *before* baking the lightmap. Offset with Lightmap Multiplier below.")]
        public float LightmapIntensity = 4.0f;

        [Tooltip("Multiply lightmap with this before applying to diffuse. Useful if you have multiple lights next to each other sharing a channel.")]
        public Vector3 LightmapMultiplier = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);

        [Header("Internal Settings")]
        public Texture2D DefaultLightmap;
        public CustomRenderTexture LOD1s, LOD1, LOD2s, LOD2, LOD3s, LOD3;
        public Texture2D LUT1, LUT2;

        public static LTCGI_Controller Singleton;

        [NonSerialized] internal Renderer[] cachedMeshRenderers;
        [NonSerialized] private Vector4[] _LTCGI_Vertices_0, _LTCGI_Vertices_1, _LTCGI_Vertices_2, _LTCGI_Vertices_3;
        [NonSerialized] private Vector4[] _LTCGI_Vertices_0t, _LTCGI_Vertices_1t, _LTCGI_Vertices_2t, _LTCGI_Vertices_3t;
        [NonSerialized] internal Transform[] _LTCGI_ScreenTransforms;
        [NonSerialized] private Vector4[] _LTCGI_ExtraData;
        [NonSerialized] private Vector4 _LTCGI_LightmapMult;
        [NonSerialized] private Vector2[][] _LTCGI_UVs;

        private Texture2DArray[] _LTCGI_LOD_arrays;

        public bool HasDynamicScreens = false;
        public bool HasCylinders = false;

        public void OnEnable()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this.gameObject)) return;
            if (Singleton == null || Singleton != this)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(this.gameObject))
                    PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Singleton = this;
                Undo.undoRedoPerformed += this.UpdateMaterials;
                EditorApplication.playModeStateChanged += (change) => {
                    if (change == PlayModeStateChange.ExitingEditMode)
                    {
                        UpdateMaterials();
                    }
                };
                Debug.Log($"LTCGI Controller Singleton initialized (Mode: {RuntimeMode})");

                var ctrls = GameObject.FindObjectsOfType<LTCGI_Controller>().Length;
                if (ctrls > 1)
                {
                    Debug.LogError("There must only be one LTCGI Controller per scene!");
                }
            }

            // god I hate this part, Unity dumb dumb
            if (!AssetDatabase.IsValidFolder("Assets\\Gizmos"))
                AssetDatabase.CreateFolder("Assets", "Gizmos");
            if (!File.Exists("Assets\\Gizmos\\LTCGI_Screen_Gizmo.png"))
                File.Copy("Packages\\at.pimaker.ltcgi\\LTCGI_Screen_Gizmo.png", "Assets\\Gizmos\\LTCGI_Screen_Gizmo.png", true);
            
            CreateShaderRedirectFile();
        }

        private static void CreateShaderRedirectFile()
        {
            if (!AssetDatabase.IsValidFolder("Assets\\_pi_"))
                AssetDatabase.CreateFolder("Assets", "_pi_");
            if (!AssetDatabase.IsValidFolder("Assets\\_pi_\\_LTCGI"))
                AssetDatabase.CreateFolder("Assets\\_pi_", "_LTCGI");
            if (!AssetDatabase.IsValidFolder("Assets\\_pi_\\_LTCGI\\Shaders"))
                AssetDatabase.CreateFolder("Assets\\_pi_\\_LTCGI", "Shaders");
            if (!File.Exists("Assets\\_pi_\\_LTCGI\\Shaders\\LTCGI.cginc"))
                File.WriteAllText("Assets\\_pi_\\_LTCGI\\Shaders\\LTCGI.cginc", "#include \"Packages\\at.pimaker.ltcgi\\Shaders\\LTCGI.cginc\"");
        }

        public static bool MatLTCGIenabled(Material mat)
        {
            if (mat == null) return false;
            var tag = mat.GetTag("LTCGI", true);
            if (tag == null || string.IsNullOrEmpty(tag)) return false;
            if (tag == "ALWAYS") return true;
            return mat.GetFloat(tag) != 0;
        }

        [MenuItem("Tools/LTCGI/Force Material Update")]
        public static void UpdateMaterialsMenu() => Singleton?.UpdateMaterials();
        public void UpdateMaterials() => UpdateMaterials(false);
        public void UpdateMaterials(bool fast, LTCGI_Screen screen = null)
        {
            // don't mess with Udon emulation
            if (EditorApplication.isPlaying)
                return;
            if (Lightmapping.isRunning)
                return;
            if (UnityEditor.SceneManagement.EditorSceneManager.loadedSceneCount == 0)
                return;

            #if DEBUG_LOG
            Debug.Log($"LTCGI: beginning update ({(fast ? "fast" : "full")})");
            #endif

            CreateShaderRedirectFile();
            
            if (_LTCGI_ExtraData == null)
                fast = false;
            if (_LTCGI_LightmapMult == null)
                fast = false;

            if (!fast)
            {
                _LTCGI_Vertices_0 = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_1 = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_2 = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_3 = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_0t = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_1t = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_2t = new Vector4[MAX_SOURCES];
                _LTCGI_Vertices_3t = new Vector4[MAX_SOURCES];
                _LTCGI_ExtraData = new Vector4[MAX_SOURCES];
                _LTCGI_ScreenTransforms = new Transform[MAX_SOURCES];
                _LTCGI_UVs = new Vector2[MAX_SOURCES][];
            }

            var dynamics = false;
            var cylinders = false;

            // construct data
            var screens = GameObject
                .FindObjectsOfType<LTCGI_Screen>()
                .Where(x => x.enabled)
                .OrderByDescending(x => x.Dynamic)
                .ToArray();

            if (screens.Length > MAX_SOURCES)
            {
                if (fast) return;
                throw new Exception("Too many screens in the scene!");
            }

            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                if (fast && screen != null && s != screen) continue;

                _LTCGI_ScreenTransforms[i] = s.transform;
                LTCGI_Emitter emitter;
                if ((emitter = s as LTCGI_Emitter) != null)
                {
                    _LTCGI_Vertices_0[i] = _LTCGI_Vertices_1[i] = _LTCGI_Vertices_2[i] = _LTCGI_Vertices_3[i] = Vector4.zero;
                    _LTCGI_Vertices_0[i].w = s.SingleUV.x;
                    _LTCGI_Vertices_1[i].w = s.SingleUV.y;
                }
                else if (s.Cylinder)
                {
                    // Experimental!
                    _LTCGI_Vertices_0[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y * 2,
                        s.CylinderBase.z,
                        s.CylinderHeight * 2
                    );
                    _LTCGI_Vertices_1[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y * 2,
                        s.CylinderBase.z,
                        s.CylinderRadius
                    );
                    _LTCGI_Vertices_2[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y * 2,
                        s.CylinderBase.z,
                        s.CylinderSize
                    );
                    _LTCGI_Vertices_3[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y * 2,
                        s.CylinderBase.z,
                        s.CylinderAngle
                    );

                    cylinders = true;
                }
                else
                {
                    var mf = s.GetComponent<MeshFilter>();
                    if (mf.sharedMesh == null) continue;
                    if (!fast)
                    {
                        SetMeshImporterFormat(mf.sharedMesh, true);
                    }
                    var mesh = mf.sharedMesh;
                    if (mesh.vertexCount != 4 && mesh.vertexCount != 3)
                    {
                        if (fast) return;
                        throw new Exception($"Mesh on '{s.gameObject.name}' does not have 3 or 4 vertices ({mesh.vertexCount})");
                    }

                    if (mf.sharedMesh.vertexCount == 3)
                    {
                        // extend triangle to virtual quad
                        mesh = Instantiate(mesh);
                        mesh.vertices = new Vector3[] {
                            mesh.vertices[0],
                            mesh.vertices[1],
                            mesh.vertices[2],
                            mesh.vertices[2],
                        };
                        mesh.uv = new Vector2[] {
                            mesh.uv[0],
                            mesh.uv[1],
                            mesh.uv[2],
                            mesh.uv[2],
                        };
                    }

                    var verts = mesh.vertices;
                    _LTCGI_Vertices_0[i] = new Vector4(verts[0].x, verts[0].y, verts[0].z, mesh.uv[0].x);
                    _LTCGI_Vertices_1[i] = new Vector4(verts[1].x, verts[1].y, verts[1].z, mesh.uv[0].y);
                    _LTCGI_Vertices_2[i] = new Vector4(verts[2].x, verts[2].y, verts[2].z, mesh.uv[3].x);
                    _LTCGI_Vertices_3[i] = new Vector4(verts[3].x, verts[3].y, verts[3].z, mesh.uv[3].y);

                    var angle = Vector3.Dot(
                        new Vector3(_LTCGI_Vertices_1[i].x, _LTCGI_Vertices_1[i].y, _LTCGI_Vertices_1[i].z) -
                        new Vector3(_LTCGI_Vertices_0[i].x, _LTCGI_Vertices_0[i].y, _LTCGI_Vertices_0[i].z),
                        new Vector3(_LTCGI_Vertices_1[i].x, _LTCGI_Vertices_1[i].y, _LTCGI_Vertices_1[i].z) -
                        new Vector3(_LTCGI_Vertices_3[i].x, _LTCGI_Vertices_3[i].y, _LTCGI_Vertices_3[i].z)
                    );
                    // workaround for blender imports
                    if (!Mathf.Approximately(angle, 0.0f))
                    {
                        var flip = s.FlipUV ? 1 : -1;
                        var v0 = _LTCGI_Vertices_0[i];
                        var v1 = _LTCGI_Vertices_1[i];
                        var v2 = _LTCGI_Vertices_2[i];
                        var v3 = _LTCGI_Vertices_3[i];
                        _LTCGI_Vertices_0[i] = v0;
                        _LTCGI_Vertices_1[i] = v3;
                        _LTCGI_Vertices_2[i] = v1;
                        _LTCGI_Vertices_3[i] = v2;
                        _LTCGI_Vertices_0[i].w = mesh.uv[0].x * flip;
                        _LTCGI_Vertices_1[i].w = mesh.uv[0].y;
                        _LTCGI_Vertices_2[i].w = mesh.uv[2].x * flip;
                        _LTCGI_Vertices_3[i].w = mesh.uv[2].y;

                        if (s.FlipUV)
                        {
                            _LTCGI_UVs[i] = new Vector2[]
                            {
                                // TODO: is this required? if so, implement it. for now, no-op.
                                mesh.uv[0],
                                mesh.uv[3],
                                mesh.uv[1],
                                mesh.uv[2],
                            };
                        }
                        else
                        {
                            _LTCGI_UVs[i] = new Vector2[]
                            {
                                mesh.uv[0],
                                mesh.uv[3],
                                mesh.uv[1],
                                mesh.uv[2],
                            };
                        }
                    }
                    else
                    {
                        _LTCGI_UVs[i] = new Vector2[]
                        {
                            mesh.uv[0],
                            mesh.uv[1],
                            mesh.uv[2],
                            mesh.uv[3],
                        };
                    }

                    if (s.ColorMode == ColorMode.SingleUV)
                    {
                        _LTCGI_Vertices_0[i].w = s.SingleUV.x;
                        _LTCGI_Vertices_1[i].w = s.SingleUV.y;
                    }

                    if (mf.sharedMesh.vertexCount == 3)
                    {
                        DestroyImmediate(mesh);
                    }

                    if (s.Dynamic)
                    {
                        dynamics = true;
                    }
                }

                _LTCGI_Vertices_0t[i] = s.transform.TransformPoint(_LTCGI_Vertices_0[i]);
                _LTCGI_Vertices_0t[i].w = _LTCGI_Vertices_0[i].w;
                _LTCGI_Vertices_1t[i] = s.transform.TransformPoint(_LTCGI_Vertices_1[i]);
                _LTCGI_Vertices_1t[i].w = _LTCGI_Vertices_1[i].w;
                _LTCGI_Vertices_2t[i] = s.transform.TransformPoint(_LTCGI_Vertices_2[i]);
                _LTCGI_Vertices_2t[i].w = _LTCGI_Vertices_2[i].w;
                _LTCGI_Vertices_3t[i] = s.transform.TransformPoint(_LTCGI_Vertices_3[i]);
                _LTCGI_Vertices_3t[i].w = _LTCGI_Vertices_3[i].w;

                //Debug.Log($"V0: {_LTCGI_Vertices_0[i]}");
                //Debug.Log($"V1: {_LTCGI_Vertices_1[i]}");
                //Debug.Log($"V2: {_LTCGI_Vertices_2[i]}");
                //Debug.Log($"V3: {_LTCGI_Vertices_3[i]}");

                uint flags = 0;
                if (s.DoubleSided) flags |= 1;
                if (s.DiffuseFromLm) flags |= 2;
                if (s.Specular) flags |= 4;
                if (s.Diffuse) flags |= 8;
                flags |= ((uint)s.TextureIndex & 0xf) << 4;
                flags |= ((uint)s.ColorMode & 0x3) << 8;
                flags |= ((uint)s.LightmapChannel & 0x3) << 10;
                if (s.Cylinder) flags |= (1<<12);
                flags |= ((uint)s.AudioLinkBand & 0x3) << 13;
                if (s is LTCGI_Emitter) flags |= (1<<15); // TODO: can this be set based on other flags?

                var col = s.enabled && s.gameObject.activeInHierarchy ? s.Color : Color.black;
                float fflags = BitConverter.ToSingle(BitConverter.GetBytes(flags), 0);
                _LTCGI_ExtraData[i] = new Vector4(col.linear.r, col.linear.g, col.linear.b, fflags);
            }

            if (!fast)
            {
                HasCylinders = cylinders;
                HasDynamicScreens = dynamics;
            }

            /*_LTCGI_LightmapMult = new Vector4(
                1.0f/Mathf.Max(screens.Count(s => s.LightmapChannel == 1), 1.0f),
                1.0f/Mathf.Max(screens.Count(s => s.LightmapChannel == 2), 1.0f),
                1.0f/Mathf.Max(screens.Count(s => s.LightmapChannel == 3), 1.0f),
                0.0f
            );*/

            _LTCGI_LightmapMult = (Vector4)LightmapMultiplier;

            #if DEBUG_LOG
                if (!fast)
                {
                    Debug.Log($"LTCGI: updated screens ({screens.Length}, {_LTCGI_LightmapMult})");
                }
            #endif
            
            if (!fast || cachedMeshRenderers == null)
            {
                // get all affected renderers
                var allRenderers = Component.FindObjectsOfType<Renderer>();
                var renderers = new List<Renderer>();
                foreach (var r in allRenderers)
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (MatLTCGIenabled(mat))
                        {
                            if (!renderers.Contains(r))
                                renderers.Add(r);
                            break;
                        }
                    }
                }
                cachedMeshRenderers = renderers.ToArray();

                #if DEBUG_LOG
                    Debug.Log($"LTCGI: cached renderers ({cachedMeshRenderers.Length})");
                #endif
            }

            // start LOD chain
            if (VideoTexture != null && !CustomBlurChain)
            {
                LOD1s?.material?.SetTexture("_MainTex", VideoTexture);
            }

            // find precomputed static textures
            if (!fast)
            {
                var curscene = EditorSceneManager.GetActiveScene().name;
                _LTCGI_LOD_arrays = new Texture2DArray[4];
                for (int lod = 0; lod < 4; lod++)
                {
                    try
                    {
                        _LTCGI_LOD_arrays[lod] = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/LTCGI-Generated/lod-" + curscene + "-" + lod + ".asset");
                        if (_LTCGI_LOD_arrays[lod] == null) throw new Exception();
                    }
                    catch
                    {
                        _LTCGI_LOD_arrays = null;
                        break;
                    }
                }
            }

            // write out uniforms into data texture
            var staticUniformTex = WriteStaticUniform(screens, fast);
            var screenCountDynamic = screens.TakeWhile(x => x.Dynamic).Count();

            for (int i = 0; i < cachedMeshRenderers.Length; i++)
            {
                var r = cachedMeshRenderers[i];
                if (r == null) {
                    // explicitly do full update in case the renderers have become invalid
                    UpdateMaterials(false);
                    return;
                }
            }

            Shader.SetGlobalFloat("_Udon_LTCGI_GlobalEnable", screens.Length > 0 ? 1.0f : 0.0f);

            if (this != null && this.gameObject != null)
            {
                #if UDONSHARP
                LTCGI_UdonAdapter adapter;
                #pragma warning disable 618
                LTCGI_UdonAdapter[] adapters = this.gameObject.GetUdonSharpComponents<LTCGI_UdonAdapter>();
                #pragma warning restore 618
                #else
                LTCGI_RuntimeAdapter adapter;
                Component[] adapters = this.gameObject.GetComponents<LTCGI_RuntimeAdapter>();
                #endif

                if (adapters == null || adapters.Length == 0)
                {
                    #if UDONSHARP
                    adapter = this.gameObject.AddUdonSharpComponent<LTCGI_UdonAdapter>();
                    #else
                    adapter = this.gameObject.AddComponent<LTCGI_RuntimeAdapter>();
                    #endif
                }
                else
                {
                    #if UDONSHARP
                    adapter = (LTCGI_UdonAdapter)adapters[0];
                    #else
                    adapter = (LTCGI_RuntimeAdapter)adapters[0];
                    #endif
                    if (adapters.Length > 1)
                    {
                        for (int i = 1; i < adapters.Length; i++)
                        {
                            Debug.LogWarning("LTCGI: WARNING: Deleting extra *Adapter component on " + this.gameObject.name);
                            DestroyImmediate(adapters[i]);
                        }
                    }
                }
                
                // update LTCGI_UdonAdapter proxy with new data
                #pragma warning disable 618
                adapter.UpdateProxy();
                #pragma warning restore 618
                adapter._Renderers = cachedMeshRenderers.Where(cm => !IsEditorOnly(cm.gameObject)).ToArray();
                adapter._LTCGI_DefaultLightmap = DefaultLightmap;
                adapter._LTCGI_Lightmaps = cachedMeshRenderers
                    .Select(r => {
                        if (_LTCGI_Lightmaps == null) return DefaultLightmap;
                        if (_LTCGI_LightmapData_key == null) return DefaultLightmap;
                        var lidx2 = Array.IndexOf(_LTCGI_LightmapData_key, r);
                        if (lidx2 < 0) return DefaultLightmap;
                        var lidx = _LTCGI_LightmapIndex_val[lidx2];
                        return lidx != 0xFFFE && lidx >= 0 && lidx < _LTCGI_Lightmaps.Length ?
                            _LTCGI_Lightmaps[lidx] : DefaultLightmap;
                    })
                    .ToArray();
                adapter._LTCGI_LightmapMult = _LTCGI_LightmapMult;
                adapter._LTCGI_LightmapST = cachedMeshRenderers.Select(r => {
                        if (_LTCGI_LightmapData_key == null) return Vector4.zero;
                        var idx = Array.IndexOf(_LTCGI_LightmapData_key, r);
                        return idx < 0 ? Vector4.zero : _LTCGI_LightmapOffsets_val[idx];
                    }).ToArray();
                var mask2d = cachedMeshRenderers.Select(x => GetMaskForRenderer(screens, x)).ToArray();
                // float[][] doesn't serialize in normal Unity, so linearize it
                adapter._LTCGI_Mask =
                    Enumerable.Range(0, adapter._Renderers.Length)
                    .SelectMany(i => mask2d[i])
                    .ToArray();
                // mask is reversed! 1 = not visible, 0 = visible
                var avatarMask = screens.Select(x => x.AffectAvatars ? 0.0f : 1.0f);
                adapter._LTCGI_MaskAvatars = avatarMask.ToArray();
                adapter._Screens = screens.Select(x => x?.gameObject).ToArray();
                adapter._LTCGI_LODs = new Texture[4];
                adapter._LTCGI_LODs[0] = VideoTexture;
                adapter._LTCGI_LODs[1] = LOD1;
                adapter._LTCGI_LODs[2] = LOD2;
                adapter._LTCGI_LODs[3] = LOD3;
                if (_LTCGI_LOD_arrays != null)
                {
                    adapter._LTCGI_Static_LODs_0 = _LTCGI_LOD_arrays[0];
                    adapter._LTCGI_Static_LODs_1 = _LTCGI_LOD_arrays[1];
                    adapter._LTCGI_Static_LODs_2 = _LTCGI_LOD_arrays[2];
                    adapter._LTCGI_Static_LODs_3 = _LTCGI_LOD_arrays[3];
                }
                else
                {
                    adapter._LTCGI_Static_LODs_0 = null;
                    adapter._LTCGI_Static_LODs_1 = null;
                    adapter._LTCGI_Static_LODs_2 = null;
                    adapter._LTCGI_Static_LODs_3 = null;
                }
                adapter._LTCGI_lut1 = LUT1;
                adapter._LTCGI_lut2 = LUT2;
                adapter._LTCGI_ScreenTransforms = _LTCGI_ScreenTransforms;
                adapter._LTCGI_Vertices_0 = _LTCGI_Vertices_0;
                adapter._LTCGI_Vertices_1 = _LTCGI_Vertices_1;
                adapter._LTCGI_Vertices_2 = _LTCGI_Vertices_2;
                adapter._LTCGI_Vertices_3 = _LTCGI_Vertices_3;
                adapter._LTCGI_ExtraData = _LTCGI_ExtraData;
                adapter._LTCGI_static_uniforms = staticUniformTex;
                adapter._LTCGI_ScreenCount = screens.Length;
                adapter._LTCGI_ScreenCountDynamic = screenCountDynamic;
                // masked counts must include all masked screens up to the last non-masked one!
                adapter._LTCGI_ScreenCountMasked = 
                    mask2d.Select(mask =>
                        Math.Max(adapter._LTCGI_ScreenCountDynamic,
                            Array.FindLastIndex(mask, m => m == 0.0f) + 1)).ToArray();
                adapter._LTCGI_ScreenCountMaskedAvatars = Array.FindLastIndex(screens, x => x.AffectAvatars) + 1;
                adapter.BlurCRTInput = LOD1s;

                #pragma warning disable 618
                adapter.ApplyProxyModifications();
                #pragma warning restore 618

                adapter._Initialize();

                #if DEBUG_LOG
                    Debug.Log("LTCGI: updated UdonSharp adapter");
                #endif
            }

            #if DEBUG_LOG
                Debug.Log("LTCGI: writing auto-config file");
            #endif
            LTCGI_ControllerEditor.RecalculateAutoConfig(this);

            #if DEBUG_LOG
                Debug.Log("LTCGI: updating video player adapters");
            #endif
            LTCGI_Controller.DetectAndEnableAdaptersForAvailableVideoplayers();

            #if DEBUG_LOG
                Debug.Log("LTCGI: update done!");
            #endif
        }

        private static Texture2D staticUniformTemp = null;
        private Texture2D WriteStaticUniform(LTCGI_Screen[] screens, bool fast, LTCGI_Screen fastScreen = null)
        {
            var curscene = EditorSceneManager.GetActiveScene().name;
            var path = @"Assets\LTCGI-Generated\StaticUniform-" + curscene + ".exr";

            if (staticUniformTemp == null)
            {
                staticUniformTemp = new Texture2D(6, MAX_SOURCES, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                fast = false;
            }

            for (int i = 0; i < MAX_SOURCES; i++)
            {
                if (fast && fastScreen != null && i < screens.Length && screens[i] != fastScreen)
                    continue;
                if (i >= screens.Length)
                {
                    for (int w = 0; w < staticUniformTemp.width; w++)
                    {
                        staticUniformTemp.SetPixel(w, i, Color.black);
                    }
                }
                else
                {
                    staticUniformTemp.SetPixel(0, i, (Color)_LTCGI_Vertices_0t[i]);
                    staticUniformTemp.SetPixel(1, i, (Color)_LTCGI_Vertices_1t[i]);
                    staticUniformTemp.SetPixel(2, i, (Color)_LTCGI_Vertices_2t[i]);
                    staticUniformTemp.SetPixel(3, i, (Color)_LTCGI_Vertices_3t[i]);
                    if (_LTCGI_UVs[i] != null && _LTCGI_UVs[i].Length == 4)
                    {
                        staticUniformTemp.SetPixel(4, i, (Color)new Vector4(
                            _LTCGI_UVs[i][0].x, _LTCGI_UVs[i][0].y, _LTCGI_UVs[i][1].x, _LTCGI_UVs[i][1].y));
                        staticUniformTemp.SetPixel(5, i, (Color)new Vector4(
                            _LTCGI_UVs[i][2].x, _LTCGI_UVs[i][2].y, _LTCGI_UVs[i][3].x, _LTCGI_UVs[i][3].y));
                    }
                }
            }

            staticUniformTemp.Apply();

            if (fast)
            {
                return staticUniformTemp;
            }

            if (!AssetDatabase.IsValidFolder("Assets/LTCGI-Generated"))
                AssetDatabase.CreateFolder("Assets", "LTCGI-Generated");
            var exr = staticUniformTemp.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

            var existed = File.Exists(path);
            byte[] prev = new byte[0];
            if (existed)
            {
                prev = File.ReadAllBytes(path);
            }

            File.WriteAllBytes(path, exr);
            AssetDatabase.Refresh();
            
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string assetPath = AssetDatabase.GetAssetPath(asset);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && (!prev.SequenceEqual(exr) || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.sRGBTexture = false;
                importer.maxTextureSize = 8192;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            #if DEBUG_LOG
                Debug.Log("LTCGI: updated static uniform declarations");
            #endif

            return asset;
        }



        // Debug stuff, plz ignore...
        private static (float[], float[], int) ReadLookupFile()
        {
            float[] ltc_1, ltc_2;
            int n;

            using (var reader = new StreamReader("Assets\\_pi_\\_LTCGI\\Lookup Tables\\ltc_2.inc"))
            {
                n = int.Parse(reader.ReadLine().Trim());
                ltc_1 = new float[n*n*4];
                ltc_2 = new float[n*n*4];

                var line = reader.ReadLine().Trim();
                var i = 0;
                while (line != "}")
                {
                    var s = line.Split(',');
                    var a = float.Parse(s[0]);
                    var b = float.Parse(s[1]);
                    var c = float.Parse(s[2]);
                    var d = float.Parse(s[3]);
                    ltc_1[i + 0] = a;
                    ltc_1[i + 1] = b;
                    ltc_1[i + 2] = c;
                    ltc_1[i + 3] = d;
                    i += 4;
                    line = reader.ReadLine().Trim();
                }

                i = 0;
                line = reader.ReadLine().Trim();
                while (line != "}")
                {
                    var s = line.Split(',');
                    var a = float.Parse(s[0]);
                    var b = float.Parse(s[1]);
                    var c = float.Parse(s[2]);
                    var d = float.Parse(s[3]);
                    ltc_2[i + 0] = a;
                    ltc_2[i + 1] = b;
                    ltc_2[i + 2] = c;
                    ltc_2[i + 3] = d;
                    i += 4;
                    line = reader.ReadLine().Trim();
                }
            }

            Debug.Log("LTCGI: Read texture with size " + n);
            return (ltc_1, ltc_2, n);
        }

        //[MenuItem("Tools/LTCGI/Encode Lookup Textures into EXR")]
        public static void EncodeLookups()
        {
            var (g_ltc_mat_f, g_ltc_mag_f, n) = ReadLookupFile();

            var tex = new Texture2D(n, n, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 0);
            tex.SetPixelData(g_ltc_mat_f, 0);
            System.IO.File.WriteAllBytes("Assets\\_pi_\\_LTCGI\\Lookup Tables\\ltc_mat_hdr_2.exr", tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            var tex2 = new Texture2D(n, n, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 0);
            tex2.SetPixelData(g_ltc_mag_f, 0);
            System.IO.File.WriteAllBytes("Assets\\_pi_\\_LTCGI\\Lookup Tables\\ltc_mag_hdr_2.exr", tex2.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            GameObject.DestroyImmediate(tex);
            GameObject.DestroyImmediate(tex2);
            AssetDatabase.Refresh();
        }
    }
#endif
}
