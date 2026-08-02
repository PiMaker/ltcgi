#if LTCGI_VRC_LIGHT_VOLUMES && UDON

using System;
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using UnityEditor;
using VRCLightVolumes;
using VRC.Udon;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UdonSharpEditor;
#endif

#if UDONSHARP
using VRC.SDKBase;
#else
using VRCShader = UnityEngine.Shader;
#endif

namespace pi.LTCGI.LVAdapter
{
    [DefaultExecutionOrder(100)] // after LightVolumeManager
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LV_LTCGI_Adapter : UdonSharpBehaviour
    {
        private const int MAX_LVS = 32;

        public LightVolumeManager LightVolumeManager;
        public RenderTexture PostProcessorRT;
        public Material PostProcessorMat;
        public Texture2DArray DummyTextureArray;
        public int[] LTCGIEnabledLightVolumeIDs = new int[0];

        private int lightVolumesWithLTCGIID;
        private int lightVolumeFwdWorldMatrixID;
        private int lightVolumeLayerOffsetID;
        private readonly Matrix4x4[] lightVolumeFwdWorldMatrices = new Matrix4x4[MAX_LVS];
        private long updateCount;

        private const int LVsPerSlice = 24; // keep in sync with shader

        private void Start()
        {
            Init();
            Debug.Log($"LV_LTCGI_Adapter initialized with {LTCGIEnabledLightVolumeIDs.Length} LTCGI enabled light volumes.", this);
            foreach (var id in LTCGIEnabledLightVolumeIDs)
            {
                Debug.Log($" - {id}: {LightVolumeManager.LightVolumeInstances[id].name}", LightVolumeManager.LightVolumeInstances[id]);
            }
        }

        private void Init()
        {
            lightVolumeLayerOffsetID = VRCShader.PropertyToID("_Udon_LTCGI_LV_LayerOffset");
            PostProcessorMat.SetFloat("_Udon_LTCGI_LV_LayerDepth", PostProcessorRT.volumeDepth);
            lightVolumeFwdWorldMatrixID = VRCShader.PropertyToID("_UdonLightVolumeFwdWorldMatrix");
            VRCShader.SetGlobalMatrixArray(lightVolumeFwdWorldMatrixID, lightVolumeFwdWorldMatrices);
            lightVolumesWithLTCGIID = VRCShader.PropertyToID("_UdonLightVolumesWithLTCGI");
            VRCShader.SetGlobalFloat(lightVolumesWithLTCGIID, BitConverter.Int32BitsToSingle(0));
        }

        internal void EditorUpdate()
        {
            Init();
            DoUpdate();
            DoLTCGIBlit();
        }

        private void LateUpdate() // after Update() in LightVolumeManager for the frame
        {
            if (LightVolumeManager.AutoUpdateVolumes || updateCount < 2)
                DoUpdate();

            DoLTCGIBlit();
        }

        private void DoLTCGIBlit()
        {
            var total = PostProcessorRT.volumeDepth + LVsPerSlice - 1;
            for (int i = 0; i < total; i += LVsPerSlice)
            {
                PostProcessorMat.SetFloat(lightVolumeLayerOffsetID, i);

                // okay listen up, it's about to get stupid:
                // we want to take the 3D volume that VRCLV outputs and run it through a shader for every voxel once per frame
                // originally this used a Custom Render Texture, but Unity in its endless wisdom decided to dispatch a new drawcall for every depth slice
                // so to avoid adding hundreds of batches to every frame, we do a Graphics.Blit
                // but hold on, how is our blit shader going to target a slice?
                // easy: SV_RenderTargetArrayIndex and a geometry shader
                // that's right, Unity blits a single Quad that would usually just go to the first depth slice, but we can have our shader override it!
                // so we generate `LVsPerSlice` quads in geom targeting slice `(0 to LVsPerSlice) + i` (i for offset if we need more dispatches)
                // there is one last crucial detail: the DummyTextureArray
                // for SV_RenderTargetArrayIndex to work, the destDepthSlice parameter of Blit needs to be -1
                // however, Udon does not expose a variant that takes both destDepthSlice _and_ a custom Material
                // luckily, passing a `Texture2DArray` as the _source_ of a Blit operation with no explicit `destDepthSlice` does the trick!
                // why only a 2D array and not a 3D texture? No idea, but this is how Unity does things apparently
                // we don't use the input texture at all in our shader, so we bind a random tiny 1x1x1 dummy texture just to trigger the all-slices binding logic
                // computers should be banned

                #if UDONSHARP
                VRCGraphics
                #else
                Graphics
                #endif
                    .Blit(DummyTextureArray, PostProcessorRT, PostProcessorMat);
            }
        }

        public void DoUpdate()
        {
            updateCount++;

            var enabledCount = LightVolumeManager.EnabledCount;
            var enabledIDs = LightVolumeManager.EnabledIDs;
            
            int enabledVolumes = 0;
            for (int i = 0; i < enabledCount; i++)
            {
                var id = enabledIDs[i];
                if (Array.IndexOf(LTCGIEnabledLightVolumeIDs, id) >= 0)
                {
                    var instance = LightVolumeManager.LightVolumeInstances[id];
                    var invWorldMatrix = instance.InvWorldMatrix;
                    lightVolumeFwdWorldMatrices[i] = invWorldMatrix.inverse;
                    enabledVolumes |= 1 << i;
                }
            }

            VRCShader.SetGlobalMatrixArray(lightVolumeFwdWorldMatrixID, lightVolumeFwdWorldMatrices);
            VRCShader.SetGlobalFloat(lightVolumesWithLTCGIID, BitConverter.Int32BitsToSingle(enabledVolumes));
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        private static readonly List<int> tmpLTCGIEnabledLightVolumeIDs = new();
        public static void SetupDependencies(
            ref LightVolumeManager refLightVolumeManager,
            ref RenderTexture refPostProcessorRT,
            ref Material refPostProcessorMat,
            ref Texture2DArray refDummyTextureArray,
            ref int[] refLTCGIEnabledLightVolumeIDs,
            bool isUI)
        {
            if (refLightVolumeManager == null)
            {
                var foundManager = FindObjectOfType<LightVolumeManager>();
                if (foundManager != null)
                {
                    refLightVolumeManager = foundManager;
                }
                else if (isUI)
                {
                    EditorGUILayout.HelpBox("No LightVolumeManager found in the scene. Please create one.", MessageType.Warning);
                }
            }

            if (refPostProcessorRT == null || refPostProcessorMat == null || refDummyTextureArray == null)
            {
                var thisPath = AssetDatabase.GUIDToAssetPath("b9ee507ae056a484ba791c34abfe3982"); // this script's GUID
                var thisDir = System.IO.Path.GetDirectoryName(thisPath);
                var crtPath = System.IO.Path.Combine(thisDir, "LV_RT_LTCGI.renderTexture");
                refPostProcessorRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(crtPath);

                if (refPostProcessorRT == null)
                {
                    Debug.LogError($"LV_LTCGI_Adapter: Could not load post processor render texture at {crtPath}.");
                }

                var matPath = System.IO.Path.Combine(thisDir, "LV_Mat_LTCGI.mat");
                refPostProcessorMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                if (refPostProcessorMat == null)
                {
                    Debug.LogError($"LV_LTCGI_Adapter: Could not load post processor material at {matPath}.");
                }

                var dummyPath = System.IO.Path.Combine(thisDir, "LV_DummyTextureArray.asset");
                refDummyTextureArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(dummyPath);

                if (refDummyTextureArray == null)
                {
                    Debug.LogError($"LV_LTCGI_Adapter: Could not load dummy texture array at {dummyPath}.");
                }
            }

            if (refLightVolumeManager != null && refPostProcessorRT != null && refPostProcessorMat != null && refDummyTextureArray != null)
            {
                // find LTCGI enabled light volumes
                tmpLTCGIEnabledLightVolumeIDs.Clear();
                for (int i = 0; i < refLightVolumeManager.LightVolumeInstances.Length; i++)
                {
                    var lv = refLightVolumeManager.LightVolumeInstances[i];
                    if (lv != null && lv.TryGetComponent<LightVolumeLTCGI>(out _))
                    {
                        tmpLTCGIEnabledLightVolumeIDs.Add(i);
                    }
                }
                var difference = tmpLTCGIEnabledLightVolumeIDs.Count != refLTCGIEnabledLightVolumeIDs.Length;
                if (!difference)
                {
                    for (int i = 0; i < tmpLTCGIEnabledLightVolumeIDs.Count; i++)
                    {
                        if (tmpLTCGIEnabledLightVolumeIDs[i] != refLTCGIEnabledLightVolumeIDs[i])
                        {
                            difference = true;
                            break;
                        }
                    }
                }
                if (!difference)
                {
                    var found = false;
                    foreach (var pp in refLightVolumeManager.AtlasPostProcessors)
                    {
                        if (pp.RT == refPostProcessorRT && pp.Mat == refPostProcessorMat)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found != (tmpLTCGIEnabledLightVolumeIDs.Count > 0))
                    {
                        difference = true;
                    }
                }
                if (difference)
                {
                    refLTCGIEnabledLightVolumeIDs = tmpLTCGIEnabledLightVolumeIDs.ToArray();
                    Debug.Log($"LV_LTCGI_Adapter: Found and set {refLTCGIEnabledLightVolumeIDs.Length} LTCGI enabled light volumes.");

                    // register the CRT as a post processor
                    if (refLTCGIEnabledLightVolumeIDs.Length == 0)
                    {
                        refLightVolumeManager.UnregisterPostProcessor(refPostProcessorRT);
                    }
                    else
                    {
                        RenderTexture rtCopy = refPostProcessorRT;
                        Material matCopy = refPostProcessorMat;
                        refLightVolumeManager.RegisterPostProcessor(new LightVolumeManager.PostProcessor()
                        {
                            RT = rtCopy,
                            Mat = matCopy,
                            TextureName = "_LV_Volume",
                            Update = null, // EditorUpdator will handle it
                        });
                    }
                }
            }
        }
#endif
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [ExecuteAlways]
    [DefaultExecutionOrder(101)]
    public class LV_LTCGI_EditorUpdator : MonoBehaviour
    {
        public LV_LTCGI_Adapter Adapter;

        public void LateUpdate()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Adapter.EditorUpdate();
        }
    }

    [CustomEditor(typeof(LV_LTCGI_Adapter))]
    public class LV_LTCGI_AdapterEditor : Editor
    {
        [InitializeOnLoadMethod]
        private static void InitEditorUpdateLoop()
        {
            // register callbacks
            EditorApplication.update += EditorUpdate;
            LTCGI_Controller.OnLTCGIShadowmapBakeComplete += OnLTCGIShadowmapBakeComplete;
            LTCGI_Controller.OnLTCGIShadowmapClearData += OnLTCGIShadowmapClearData;
            Texture3DAtlasGenerator.OnPreAtlasCreate += OnPreAtlasCreate;
        }

        private static LightVolumeManager lightVolumeManager;
        private static Matrix4x4[] lightVolumeFwdWorldMatrices = new Matrix4x4[32];
        private static List<UdonBehaviour> tmpUdonBehaviours = new();
        private static int[] LTCGIEnabledLightVolumeIDs = new int[0];
        private static void EditorUpdate()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (LTCGI_Controller.Singleton == null)
                return;

            if (!FindAndUpdateLVAdapter())
            {
                var newUdon = LTCGI_Controller.Singleton.gameObject.AddUdonSharpComponent<LV_LTCGI_Adapter>();
                LV_LTCGI_Adapter.SetupDependencies(ref lightVolumeManager, ref newUdon.PostProcessorRT, ref newUdon.PostProcessorMat, ref newUdon.DummyTextureArray, ref newUdon.LTCGIEnabledLightVolumeIDs, isUI: false);
                EditorUtility.SetDirty(newUdon);
                UdonSharpEditorUtility.CopyProxyToUdon(newUdon);
                RefreshUpdator();
                Debug.Log("LV_LTCGI_Adapter: Added to LTCGI_Controller.");
            }
            else if (lightVolumeManager != null)
            {
                var enabledCount = lightVolumeManager.EnabledCount;
                var enabledIDs = lightVolumeManager.EnabledIDs;

                Array.Clear(lightVolumeFwdWorldMatrices, 0, lightVolumeFwdWorldMatrices.Length); // fine in editor

                int enabledVolumes = 0;
                for (int i = 0; i < enabledCount; i++)
                {
                    var id = enabledIDs[i];
                    if (Array.IndexOf(LTCGIEnabledLightVolumeIDs, id) >= 0)
                    {
                        var instance = lightVolumeManager.LightVolumeInstances[id];
                        var invWorldMatrix = instance.InvWorldMatrix;
                        lightVolumeFwdWorldMatrices[i] = invWorldMatrix.inverse;
                        enabledVolumes |= 1 << i;
                    }
                }

                VRCShader.SetGlobalMatrixArray(VRCShader.PropertyToID("_UdonLightVolumeFwdWorldMatrix"), lightVolumeFwdWorldMatrices);
                VRCShader.SetGlobalFloat(VRCShader.PropertyToID("_UdonLightVolumesWithLTCGI"), BitConverter.Int32BitsToSingle(enabledVolumes));
            }
        }

        private static bool FindAndUpdateLVAdapter()
        {
            LTCGI_Controller.Singleton.GetComponents(tmpUdonBehaviours);
            if (tmpUdonBehaviours.Count == 0)
                return false;

            var foundLVAdapter = false;
            foreach (var ub in tmpUdonBehaviours)
            {
                var proxy = UdonSharpEditorUtility.GetProxyBehaviour(ub);
                if (proxy is LV_LTCGI_Adapter adapter)
                {
                    if (foundLVAdapter)
                    {
                        Debug.LogWarning("Multiple LV_LTCGI_Adapter components found on LTCGI_Controller.", adapter);
                        DestroyImmediate(adapter);
                    }
                    else
                    {
                        foundLVAdapter = true;
                        LV_LTCGI_Adapter.SetupDependencies(ref adapter.LightVolumeManager, ref adapter.PostProcessorRT, ref adapter.PostProcessorMat, ref adapter.DummyTextureArray, ref adapter.LTCGIEnabledLightVolumeIDs, isUI: false);
                        UdonSharpEditorUtility.CopyProxyToUdon(adapter);
                        RefreshUpdator();
                    }
                }
            }
            return foundLVAdapter;
        }

        private static void RefreshUpdator()
        {
            var controller = LTCGI_Controller.Singleton;
            if (controller == null)
                return;

            var adapter = controller.GetComponent<LV_LTCGI_Adapter>();
            if (adapter == null)
                return;

            var updator = controller.GetComponent<LV_LTCGI_EditorUpdator>();
            if (updator == null)
            {
                updator = controller.gameObject.AddComponent<LV_LTCGI_EditorUpdator>();
            }

            updator.Adapter = adapter;
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
                return;
            
            var adapter = (LV_LTCGI_Adapter)target;
            LV_LTCGI_Adapter.SetupDependencies(ref adapter.LightVolumeManager, ref adapter.PostProcessorRT, ref adapter.PostProcessorMat, ref adapter.DummyTextureArray, ref adapter.LTCGIEnabledLightVolumeIDs, isUI: true);
            UdonSharpEditorUtility.CopyProxyToUdon(adapter);
            RefreshUpdator();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Light Volume Manager", adapter.LightVolumeManager, typeof(LightVolumeManager), true);
            EditorGUILayout.ObjectField("Post Processor RT", adapter.PostProcessorRT, typeof(RenderTexture), false);
            EditorGUILayout.ObjectField("Post Processor Material", adapter.PostProcessorMat, typeof(Material), false);
            EditorGUILayout.ObjectField("Dummy Texture Array", adapter.DummyTextureArray, typeof(Texture2DArray), false);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "LTCGI Light Volume Adapter is active.",
                MessageType.Info
            );
        }

        // Bake handlers

        private static void OnPreAtlasCreate(LightVolumeInstance[] obj)
        {
            if (LTCGI_Controller.Singleton == null || LTCGI_Controller.Singleton.bakeInProgress)
                return;

            if (!FindAndUpdateLVAdapter())
            {
                Debug.LogWarning("LV_LTCGI_Adapter: Could not find LV_LTCGI_Adapter during pre-atlas creation. Skipping LTCGI LV bake.");
                return;
            }

            var curscene = EditorSceneManager.GetActiveScene().name;
            foreach (var volume in obj)
            {
                if (volume is LightVolumeLTCGI)
                {
                    // re-apply our LTCGI shadowmap textures
                    var path = $"Assets/LTCGI-Generated/{curscene}-lvdata-{VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform).Replace('/', '_')}-";
                    var tex0 = AssetDatabase.LoadAssetAtPath<Texture3D>($"{path}tex0.asset");
                    var tex1 = AssetDatabase.LoadAssetAtPath<Texture3D>($"{path}tex1.asset");
                    var tex2 = AssetDatabase.LoadAssetAtPath<Texture3D>($"{path}tex2.asset");
                    volume.Texture0 = tex0;
                    volume.Texture1 = tex1;
                    volume.Texture2 = tex2;
                    Debug.Log($"LV_LTCGI_Adapter: Re-applied LTCGI textures for {VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform)}.", tex0);
                }
            }
        }

        private static void OnLTCGIShadowmapBakeComplete()
        {
            var curscene = EditorSceneManager.GetActiveScene().name;
            var allLTCGIVolumes = FindObjectsOfType<LightVolumeLTCGI>();
            foreach (var volume in allLTCGIVolumes)
            {
                var path = $"Assets/LTCGI-Generated/{curscene}-lvdata-{VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform).Replace('/', '_')}-";
                var copy0 = Instantiate(volume.Texture0);
                AssetDatabase.CreateAsset(copy0, $"{path}tex0.asset");
                var copy1 = Instantiate(volume.Texture1);
                AssetDatabase.CreateAsset(copy1, $"{path}tex1.asset");
                var copy2 = Instantiate(volume.Texture2);
                AssetDatabase.CreateAsset(copy2, $"{path}tex2.asset");
                Debug.Log($"LV_LTCGI_Adapter: Created LTCGI textures for {VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform)} at {path}*", copy0);
            }
        }

        private static void OnLTCGIShadowmapClearData()
        {
            var curscene = EditorSceneManager.GetActiveScene().name;
            var allLTCGIVolumes = FindObjectsOfType<LightVolumeLTCGI>();
            foreach (var volume in allLTCGIVolumes)
            {
                var path = $"Assets/LTCGI-Generated/{curscene}-lvdata-{VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform).Replace('/', '_')}-";
                AssetDatabase.DeleteAsset($"{path}tex0.asset");
                AssetDatabase.DeleteAsset($"{path}tex1.asset");
                AssetDatabase.DeleteAsset($"{path}tex2.asset");
                Debug.Log($"LV_LTCGI_Adapter: Cleared LTCGI textures for {VRC.Core.ExtensionMethods.GetHierarchyPath(volume.transform)} at {path}*");
            }
        }
    }
#endif
}

#elif UDON

public class LV_LTCGI_Adapter : UdonSharp.UdonSharpBehaviour {} // placeholder so UdonSharp doesn't complain

#endif