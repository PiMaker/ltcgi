// Uncomment for debug messages
//#define DEBUG_LOG

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    [System.Serializable]
    public partial class LTCGI_Controller : MonoBehaviour
    {
        const int MAX_SOURCES = 16;

        [Tooltip("Intensity is set for each screen. Can be a RenderTexture for realtime updates (video players).")]
        public Texture VideoTexture;
        [Tooltip("Static textures are precomputed and *must* all be the same size. Make sure to click 'Precompute Static Textures' after any changes.")]
        public Texture2D[] StaticTextures;
        [Tooltip("Renderers that may change material during runtime. Otherwise only 'sharedMaterial's are updated for performance reasons.")]
        public MeshRenderer[] DynamicRenderers;

        [Header("Expert Settings")]
        [Tooltip("Do not automatically set up the blur chain. Use this if you use AVPro to set _MainTex on the LOD1 material for example.")]
        public bool CustomBlurChain = false;

        [Tooltip("Apply an intensity multiplier *before* baking the lightmap. Offset with Lightmap Multiplier below.")]
        public float LightmapIntensity = 4.0f;

        [Tooltip("Multiply lightmap with this before applying to diffuse. Useful if you have multiple lights next to each other sharing a channel.")]
        public Vector3 LightmapMultiplier = Vector3.one;

        [Header("Internal Settings")]
        public Texture2D DefaultLightmap;
        public CustomRenderTexture LOD1s, LOD1, LOD2s, LOD2, LOD3s, LOD3;
        public Texture2D LUT1, LUT2;
        public Material ProjectorMaterial;

        public static LTCGI_Controller Singleton;

        private static MeshRenderer[] cachedMeshRenderers;
        private static Vector4[] _LTCGI_Vertices_0, _LTCGI_Vertices_1, _LTCGI_Vertices_2, _LTCGI_Vertices_3;
        private static Vector4[] _LTCGI_Vertices_0t, _LTCGI_Vertices_1t, _LTCGI_Vertices_2t, _LTCGI_Vertices_3t;
        private static Transform[] _LTCGI_ScreenTransforms;
        private static Vector4[] _LTCGI_ExtraData;
        private static Vector4 _LTCGI_LightmapMult;

        private Texture2DArray[] _LTCGI_LOD_arrays;

        public void OnEnable()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this.gameObject)) return;
            if (Singleton == null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(this.gameObject))
                    PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log("LTCGI Controller Singleton initialized");
                Singleton = this;
            }
            else if (Singleton != this)
            {
                Debug.LogError("There must only be one LTCGI Controller per project!");
            }
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
                if (s.Cylinder)
                {
                    // Experimental!
                    _LTCGI_Vertices_0[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y,
                        s.CylinderBase.z,
                        s.CylinderHeight
                    );
                    _LTCGI_Vertices_1[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y,
                        s.CylinderBase.z,
                        s.CylinderRadius
                    );
                    _LTCGI_Vertices_2[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y,
                        s.CylinderBase.z,
                        s.CylinderSize
                    );
                    _LTCGI_Vertices_3[i] = new Vector4(
                        s.CylinderBase.x,
                        s.CylinderBase.y,
                        s.CylinderBase.z,
                        s.CylinderAngle
                    );
                }
                else
                {
                    var mf = s.GetComponent<MeshFilter>();
                    if (!fast)
                    {
                        SetMeshImporterFormat(mf.sharedMesh, true);
                    }
                    if (mf.sharedMesh.vertexCount != 4)
                    {
                        if (fast) return;
                        throw new Exception($"Mesh on '{s.gameObject.name}' does not have 4 vertices ({mf.sharedMesh.vertexCount})");
                    }
                    var verts = mf.sharedMesh.vertices;
                    _LTCGI_Vertices_0[i] = new Vector4(verts[0].x, verts[0].y, verts[0].z, mf.sharedMesh.uv[0].x);
                    _LTCGI_Vertices_1[i] = new Vector4(verts[1].x, verts[1].y, verts[1].z, mf.sharedMesh.uv[0].y);
                    _LTCGI_Vertices_2[i] = new Vector4(verts[2].x, verts[2].y, verts[2].z, mf.sharedMesh.uv[3].x);
                    _LTCGI_Vertices_3[i] = new Vector4(verts[3].x, verts[3].y, verts[3].z, mf.sharedMesh.uv[3].y);

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
                        _LTCGI_Vertices_0[i].w = mf.sharedMesh.uv[0].x * flip;
                        _LTCGI_Vertices_1[i].w = mf.sharedMesh.uv[0].y;
                        _LTCGI_Vertices_2[i].w = mf.sharedMesh.uv[2].x * flip;
                        _LTCGI_Vertices_3[i].w = mf.sharedMesh.uv[2].y;
                    }

                    if (s.ColorMode == ColorMode.SingleUV)
                    {
                        _LTCGI_Vertices_0[i].w = s.SingleUV.x;
                        _LTCGI_Vertices_1[i].w = s.SingleUV.y;
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
                var allRenderers = Component.FindObjectsOfType<MeshRenderer>();
                var renderers = new List<MeshRenderer>();
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
                _LTCGI_LOD_arrays = new Texture2DArray[4];
                for (int lod = 0; lod < 4; lod++)
                {
                    try
                    {
                        _LTCGI_LOD_arrays[lod] = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_pi_/_LTCGI/Generated/lod" + lod + ".asset");
                        if (_LTCGI_LOD_arrays[lod] == null) throw new Exception();
                    }
                    catch
                    {
                        _LTCGI_LOD_arrays = null;
                        break;
                    }
                }
            }

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
                LTCGI_UdonAdapter adapter = this.gameObject.GetUdonSharpComponent<LTCGI_UdonAdapter>();
                if (adapter == null)
                {
                    adapter = this.gameObject.AddUdonSharpComponent<LTCGI_UdonAdapter>();
                }
                
                // update LTCGI_UdonAdapter proxy with new data
                adapter.UpdateProxy();
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
                adapter._LTCGI_Mask = cachedMeshRenderers.Select(x => GetMaskForRenderer(screens, x)).ToArray();
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
                adapter._LTCGI_ScreenCount = screens.Length;
                adapter._LTCGI_ScreenCountDynamic = screens.TakeWhile(x => x.Dynamic).Count();
                adapter._LTCGI_ScreenCountMasked = 
                    adapter._LTCGI_Mask.Select(mask =>
                        Math.Max(adapter._LTCGI_ScreenCountDynamic,
                            Array.FindLastIndex(mask, m => m == 0.0f) + 1)).ToArray();
                adapter.BlurCRTInput = LOD1s;
                adapter.ProjectorMaterial = ProjectorMaterial;

                // calculate which renderers can use the shared material update method
                var dynr = DynamicRenderers.ToList();
                var mats = new Dictionary<Material, (int, float[], MeshRenderer)>();
                for (int i = 0; i < cachedMeshRenderers.Length; i++)
                {
                    MeshRenderer r = cachedMeshRenderers[i];
                    if (IsEditorOnly(r.gameObject)) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        var data = (adapter._LTCGI_ScreenCountMasked[i], adapter._LTCGI_Mask[i], r);
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

                adapter.ApplyProxyModifications();

                #if DEBUG_LOG
                    Debug.Log("LTCGI: updated UdonSharp adapter");
                #endif
            }

            #if DEBUG_LOG
                Debug.Log("LTCGI: update done!");
            #endif
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