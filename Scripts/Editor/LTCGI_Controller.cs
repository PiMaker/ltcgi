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

#if VRC_SDK_VRCSDK3
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
        public Material ProjectorMaterial;

        public static LTCGI_Controller Singleton;

        [NonSerialized] internal Renderer[] cachedMeshRenderers;
        [NonSerialized] private Vector4[] _LTCGI_Vertices_0, _LTCGI_Vertices_1, _LTCGI_Vertices_2, _LTCGI_Vertices_3;
        [NonSerialized] private Vector4[] _LTCGI_Vertices_0t, _LTCGI_Vertices_1t, _LTCGI_Vertices_2t, _LTCGI_Vertices_3t;
        [NonSerialized] internal Transform[] _LTCGI_ScreenTransforms;
        [NonSerialized] private Vector4[] _LTCGI_ExtraData;
        [NonSerialized] private Vector4 _LTCGI_LightmapMult;
        [NonSerialized] private Vector2[][] _LTCGI_UVs;

        private Texture2DArray[] _LTCGI_LOD_arrays;

        public void OnEnable()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this.gameObject)) return;
            if (Singleton == null || Singleton != this)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(this.gameObject))
                    PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Singleton = this;
                Undo.undoRedoPerformed += this.UpdateMaterials;
                Debug.Log("LTCGI Controller Singleton initialized");

                var ctrls = GameObject.FindObjectsOfType<LTCGI_Controller>().Length;
                if (ctrls > 1)
                {
                    Debug.LogError("There must only be one LTCGI Controller per scene!");
                }
            }

            var pathToScript = GetCurrentFileName();
            if (!pathToScript.EndsWith(Path.Combine("Assets", "_pi_", "_LTCGI", "Scripts", "Editor", "LTCGI_Controller.cs")))
            {
                Debug.LogError("Invalid script path: " + pathToScript);
                EditorUtility.DisplayDialog("LTCGI", "ERROR: Wrong path to 'LTCGI_Controller.cs' detected. Please do *not* move the LTCGI folder! Try reimporting the LTCGI package to fix.", "OK");
            }

            EditorApplication.playModeStateChanged += (change) => {
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    UpdateMaterials();
                }
            };

            // workaround a dumb thing
            AssetDatabase.ImportAsset("Assets/_pi_/_LTCGI/Scripts/LTCGI_AssemblyUdon.asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
        }

        private string GetCurrentFileName([System.Runtime.CompilerServices.CallerFilePath] string fileName = null)
        {
            return fileName;
        }

        public static bool MatLTCGIenabled(Material mat)
        {
            if (mat == null) return false;
            var tag = mat.GetTag("LTCGI", true);
            if (tag == null || string.IsNullOrEmpty(tag)) return false;
            if (tag == "ALWAYS") return true;
            return mat.GetFloat(tag) != 0;
        }

        /*private float calcUV(Vector2 uv)
        {
            uv *= 0xffff;
            uv.x = Mathf.Clamp(uv.x, 0, 0xffff);
            uv.y = Mathf.Clamp(uv.y, 0, 0xffff);
            UInt32 map = 0;
            map |= ((UInt32)uv.x) & 0xffff;
            map |= (((UInt32)uv.y) & 0xffff) << 16;
            return BitConverter.ToSingle(BitConverter.GetBytes(map), 0);
        }*/

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
                        _LTCGI_LOD_arrays[lod] = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_pi_/_LTCGI/Generated/lod-" + curscene + "-" + lod + ".asset");
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

            for (int i = 0; i < cachedMeshRenderers.Length; i++)
            {
                var r = cachedMeshRenderers[i];
                if (r == null) {
                    // explicitly do full update in case the renderers have become invalid
                    UpdateMaterials(false);
                    return;
                }
                var found = false;
                // set material props directly too (we need the adapter to have it serialized though)
                foreach (var mat in r.sharedMaterials)
                {
                    if (MatLTCGIenabled(mat))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    var prop = new MaterialPropertyBlock();
                    r.GetPropertyBlock(prop);

                    var lmfound = false;
                    if (_LTCGI_LightmapData_key != null)
                    {
                        var lidx2 = Array.IndexOf(_LTCGI_LightmapData_key, r);
                        if (lidx2 >= 0)
                        {
                            var lidx = _LTCGI_LightmapIndex_val[lidx2];
                            if (_LTCGI_Lightmaps != null && lidx != 0xFFFE && lidx >= 0 && lidx < _LTCGI_Lightmaps.Length && _LTCGI_Lightmaps[lidx] != null)
                            {
                                prop.SetTexture("_LTCGI_Lightmap", _LTCGI_Lightmaps[lidx]);
                                lmfound = true;
                            }
                        }
                    }
                    if (!lmfound)
                    {
                        prop.SetTexture("_LTCGI_Lightmap", DefaultLightmap);
                    }

                    if (_LTCGI_LightmapData_key != null && _LTCGI_LightmapData_key.Length > 0)
                    {
                        var idx = Array.IndexOf(_LTCGI_LightmapData_key, r);
                        if (idx >= 0)
                        {
                            prop.SetVector("_LTCGI_LightmapST", _LTCGI_LightmapOffsets_val[idx]);
                        }
                    }

                    // Video and LUTs
                    if (VideoTexture != null)
                    {
                        prop.SetTexture("_LTCGI_Texture_LOD0", VideoTexture);
                        prop.SetTexture("_LTCGI_Texture_LOD1", LOD1);
                        prop.SetTexture("_LTCGI_Texture_LOD2", LOD2);
                        prop.SetTexture("_LTCGI_Texture_LOD3", LOD3);
                    }
                    prop.SetTexture("_LTCGI_lut1", LUT1);
                    prop.SetTexture("_LTCGI_lut2", LUT2);

                    prop.SetInt("_LTCGI_ScreenCount", screens.Length);
                    if (screens.Length > 0)
                    {
                        prop.SetVectorArray("_LTCGI_Vertices_0", _LTCGI_Vertices_0t);
                        prop.SetVectorArray("_LTCGI_Vertices_1", _LTCGI_Vertices_1t);
                        prop.SetVectorArray("_LTCGI_Vertices_2", _LTCGI_Vertices_2t);
                        prop.SetVectorArray("_LTCGI_Vertices_3", _LTCGI_Vertices_3t);
                        if (staticUniformTex != null)
                            prop.SetTexture("_LTCGI_static_uniforms", staticUniformTex);
                        prop.SetVectorArray("_LTCGI_ExtraData", _LTCGI_ExtraData);
                        prop.SetVector("_LTCGI_LightmapMult", _LTCGI_LightmapMult);
                        prop.SetFloatArray("_LTCGI_Mask", GetMaskForRenderer(screens, r));
                        prop.SetMatrixArray("_LTCGI_ScreenTransforms",
                            _LTCGI_ScreenTransforms.Take(screens.Length).Select(x => x == null ? Matrix4x4.identity : x.localToWorldMatrix).ToArray());
                    }

                    if (_LTCGI_LOD_arrays != null)
                    {
                        prop.SetTexture("_LTCGI_Texture_LOD0_arr", _LTCGI_LOD_arrays[0]);
                        prop.SetTexture("_LTCGI_Texture_LOD1_arr", _LTCGI_LOD_arrays[1]);
                        prop.SetTexture("_LTCGI_Texture_LOD2_arr", _LTCGI_LOD_arrays[2]);
                        prop.SetTexture("_LTCGI_Texture_LOD3_arr", _LTCGI_LOD_arrays[3]);
                    }
                    r.SetPropertyBlock(prop);
                }
            }

            if (ProjectorMaterial != null)
            {
                // Video and LUTs
                if (VideoTexture != null)
                {
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD0", VideoTexture);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD1", LOD1);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD2", LOD2);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD3", LOD3);
                }
                ProjectorMaterial.SetTexture("_LTCGI_lut1", LUT1);
                ProjectorMaterial.SetTexture("_LTCGI_lut2", LUT2);

                ProjectorMaterial.SetInt("_LTCGI_ScreenCount", screens.Length);
                if (screens.Length > 0)
                {
                    ProjectorMaterial.SetVectorArray("_LTCGI_Vertices_0", _LTCGI_Vertices_0t);
                    ProjectorMaterial.SetVectorArray("_LTCGI_Vertices_1", _LTCGI_Vertices_1t);
                    ProjectorMaterial.SetVectorArray("_LTCGI_Vertices_2", _LTCGI_Vertices_2t);
                    ProjectorMaterial.SetVectorArray("_LTCGI_Vertices_3", _LTCGI_Vertices_3t);
                    ProjectorMaterial.SetVectorArray("_LTCGI_ExtraData", _LTCGI_ExtraData);
                    ProjectorMaterial.SetFloatArray("_LTCGI_Mask", new float[screens.Length]);
                    ProjectorMaterial.SetMatrixArray("_LTCGI_ScreenTransforms",
                        _LTCGI_ScreenTransforms.Take(screens.Length).Select(x => x == null ? Matrix4x4.identity : x.localToWorldMatrix).ToArray());
                }

                if (_LTCGI_LOD_arrays != null)
                {
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD0_arr", _LTCGI_LOD_arrays[0]);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD1_arr", _LTCGI_LOD_arrays[1]);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD2_arr", _LTCGI_LOD_arrays[2]);
                    ProjectorMaterial.SetTexture("_LTCGI_Texture_LOD3_arr", _LTCGI_LOD_arrays[3]);
                }
            }

            if (!fast && this != null && this.gameObject != null)
            {
                #if VRC_SDK_VRCSDK3
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
                    #if VRC_SDK_VRCSDK3
                    adapter = this.gameObject.AddUdonSharpComponent<LTCGI_UdonAdapter>();
                    #else
                    adapter = this.gameObject.AddComponent<LTCGI_RuntimeAdapter>();
                    #endif
                }
                else
                {
                    #if VRC_SDK_VRCSDK3
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
                adapter._LTCGI_ScreenCountDynamic = screens.TakeWhile(x => x.Dynamic).Count();
                adapter._LTCGI_ScreenCountMasked = 
                    mask2d.Select(mask =>
                        Math.Max(adapter._LTCGI_ScreenCountDynamic,
                            Array.FindLastIndex(mask, m => m == 0.0f) + 1)).ToArray();
                adapter.BlurCRTInput = LOD1s;
                adapter.ProjectorMaterial = ProjectorMaterial;

                // calculate which renderers can use the shared material update method
                var dynr = DynamicRenderers.ToList();
                var mats = new Dictionary<Material, (int, float[], Renderer)>();
                for (int i = 0; i < cachedMeshRenderers.Length; i++)
                {
                    var r = cachedMeshRenderers[i];
                    if (IsEditorOnly(r.gameObject)) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        var data = (adapter._LTCGI_ScreenCountMasked[i], mask2d[i], r);
                        if (mats.ContainsKey(m))
                        {
                            var prev = mats[m];
                            if (prev.Item1 != data.Item1 || !prev.Item2.SequenceEqual(data.Item2))
                            {
                                // two renderers share material, but have different masks,
                                // so at least one of them needs to use a MPB
                                // Debug.Log($"Cannot use shared material update for {m.name} on {r.name} and {prev.Item3.name} because " +
                                //     $"this({data.Item1} # {data.Item2.Aggregate("", (p, x) => p + (p == "" ? "" : ", ") + x.ToString())}) != " +
                                //     $"prev({prev.Item1} # {prev.Item2.Aggregate("", (p, x) => p + (p == "" ? "" : ", ") + x.ToString())})");
                                dynr.Add(r);
                                if (!dynr.Contains(prev.Item3))
                                {
                                    dynr.Add(prev.Item3);
                                }
                                break;
                            }
                        }
                        else
                        {
                            mats.Add(m, data);
                        }
                    }
                }
                adapter._DynamicRenderers = dynr.ToArray();

                #pragma warning disable 618
                adapter.ApplyProxyModifications();
                #pragma warning restore 618

                #if DEBUG_LOG
                    Debug.Log("LTCGI: updated UdonSharp adapter");
                #endif
            }

            #if DEBUG_LOG
                Debug.Log("LTCGI: update done!");
            #endif
        }

        private Texture2D WriteStaticUniform(LTCGI_Screen[] screens, bool fast)
        {
            var curscene = EditorSceneManager.GetActiveScene().name;
            var path = @"Assets\_pi_\_LTCGI\Generated\StaticUniform-" + curscene + ".exr";

            if (fast)
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            var tex = new Texture2D(6, MAX_SOURCES, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            for (int i = 0; i < MAX_SOURCES; i++)
            {
                if (i >= screens.Length)
                {
                    for (int w = 0; w < tex.width; w++)
                    {
                        tex.SetPixel(w, i, Color.black);
                    }
                }
                else
                {
                    tex.SetPixel(0, i, (Color)_LTCGI_Vertices_0t[i]);
                    tex.SetPixel(1, i, (Color)_LTCGI_Vertices_1t[i]);
                    tex.SetPixel(2, i, (Color)_LTCGI_Vertices_2t[i]);
                    tex.SetPixel(3, i, (Color)_LTCGI_Vertices_3t[i]);
                    if (_LTCGI_UVs[i] != null && _LTCGI_UVs[i].Length == 4)
                    {
                        tex.SetPixel(4, i, (Color)new Vector4(
                            _LTCGI_UVs[i][0].x, _LTCGI_UVs[i][0].y, _LTCGI_UVs[i][1].x, _LTCGI_UVs[i][1].y));
                        tex.SetPixel(5, i, (Color)new Vector4(
                            _LTCGI_UVs[i][2].x, _LTCGI_UVs[i][2].y, _LTCGI_UVs[i][3].x, _LTCGI_UVs[i][3].y));
                    }
                }
            }

            tex.Apply();

            if (!AssetDatabase.IsValidFolder("Assets/_pi_/_LTCGI/Generated"))
                AssetDatabase.CreateFolder("Assets/_pi_/_LTCGI", "Generated");
            var exr = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

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
