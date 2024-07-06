#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UDONSHARP
using UdonSharp;
using UdonSharpEditor;
#endif
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    public partial class LTCGI_Controller
    {
        public GameObject ConfiguredAdapter;

        private const string PROTV_ADAPTER_VERSION = "1.0.0";
        private const string UDONSHARP_ADAPTER_VERSION = "1.0.0";

        [SerializeField] private bool _hasShownProTvOutdatedWarning = false;
        private struct PackageJson
        {
            public string version;
        }

        internal static List<ILTCGI_AutoSetup> wizards;
        internal static List<ILTCGI_AutoSetup> Wizards
        {
            get
            {
                if (wizards != null) return wizards;
                wizards = new List<ILTCGI_AutoSetup>();

                var asms = System.AppDomain.CurrentDomain.GetAssemblies();
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (var a in asms)
                {
                    if (a.FullName.Contains("Assembly-CSharp-Editor"))
                    {
                        asm = a;
                        break;
                    }
                }

                foreach (var wizard in asm.GetTypes()
                    .Where(x => x.IsClass && !x.IsAbstract && typeof(ILTCGI_AutoSetup).IsAssignableFrom(x)))
                {
                    var instance = Activator.CreateInstance(wizard) as ILTCGI_AutoSetup;
                    if (instance != null)
                    {
                        //Debug.Log("LTCGI: Found AutoSetup wizard: " + wizard.Name);
                        wizards.Add(instance);
                    }
                }

                return wizards;
            }
        }

        internal static void DrawAutoSetupEditor(LTCGI_Controller my)
        {
            // we have one configured, only allow un-setup
            if (my.ConfiguredAdapter != null)
            {
                EditorGUILayout.LabelField("Configured Adapter: " + my.ConfiguredAdapter.name);
                if (GUILayout.Button("Un-Configure"))
                {
                    DestroyImmediate(my.ConfiguredAdapter);
                    my.ConfiguredAdapter = null;
                    LTCGI_Controller.Singleton.UpdateMaterials();
                }
                return;
            }

            // try to detect auto-setupable video players
            foreach (var wizard in Wizards)
            {
                var set = wizard.AutoSetupEditor(my);
                if (set != null)
                {
                    my.ConfiguredAdapter = set;
                    LTCGI_Controller.Singleton.UpdateMaterials();
                    return;
                }
            }
        }

        internal void DetectAndEnableAdaptersForAvailableVideoplayers()
        {
#if UDONSHARP
            if (!AssetDatabase.IsValidFolder("Assets/_pi_"))
                AssetDatabase.CreateFolder("Assets", "_pi_");
            if (!AssetDatabase.IsValidFolder("Assets/_pi_/_LTCGI-Adapters"))
                AssetDatabase.CreateFolder("Assets/_pi_", "_LTCGI-Adapters");
            if (!AssetDatabase.IsValidFolder("Assets/_pi_/_LTCGI-Adapters/Editor"))
                AssetDatabase.CreateFolder("Assets/_pi_/_LTCGI-Adapters", "Editor");

            var changed = false;

            // ProTv (outdated!)
            if (System.IO.File.Exists("Packages/dev.architech.protv/package.json") && System.IO.File.Exists("Assets/_pi_/_LTCGI-Adapters/protv_adapter_version.txt"))
            {
                try
                {
                    var parsedJson = JsonUtility.FromJson<PackageJson>(System.IO.File.ReadAllText("Packages/dev.architech.protv/package.json"));
                    if (int.Parse(parsedJson.version[0].ToString()) >= 3)
                    {
                        EditorUtility.DisplayDialog("LTCGI/ProTv", "It looks like you upgraded to ProTv 3 or higher but still have the old LTCGI adapter. I will delete it for you, please use ProTv's native LTCGI integration going forward!", "OK");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/protv_adapter_version.txt");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/protv_adapter_version.txt.meta");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/LTCGI_ProTvAdapter.cs");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/LTCGI_ProTvAdapter.cs.meta");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/LTCGI_ProTvAdapter.asset");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/LTCGI_ProTvAdapter.asset.meta");
                        System.IO.File.Delete("Assets/_pi_/_LTCGI-Adapters/Editor/LTCGI_ProTvAdapterAutoSetup.cs");
                        AssetDatabase.Refresh();
                        changed = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("LTCGI: Error handling ProTv package: " + e.Message);
                }
            }

            if (!_hasShownProTvOutdatedWarning && System.IO.File.Exists("Assets/_pi_/_LTCGI-Adapters/protv_adapter_version.txt"))
            {
                EditorUtility.DisplayDialog("LTCGI", "An old version of the LTCGI ProTv adapter was detected. It is recommended to upgrade ProTv to version 3 or later, which integrates with LTCGI natively.", "OK");
                _hasShownProTvOutdatedWarning = true;
            }

            // USharpVideo
            if (AssetDatabase.IsValidFolder("Assets/USharpVideo") && (!System.IO.File.Exists("Assets/_pi_/_LTCGI-Adapters/usharpvideo_adapter_version.txt") || System.IO.File.ReadAllText("Assets/_pi_/_LTCGI-Adapters/usharpvideo_adapter_version.txt") != UDONSHARP_ADAPTER_VERSION))
            {
                EditorUtility.DisplayDialog("LTCGI", "USharpVideo detected, enabling USharpVideo adapter.", "OK");

                System.IO.File.WriteAllText("Assets/_pi_/_LTCGI-Adapters/usharpvideo_adapter_version.txt", UDONSHARP_ADAPTER_VERSION);

                System.IO.File.Copy("Packages/at.pimaker.ltcgi/Adapters/LTCGI_USharpVideoAdapter.cs_disabled", "Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.cs", true);
                System.IO.File.Copy("Packages/at.pimaker.ltcgi/Adapters/LTCGI_USharpVideoAdapter.cs_disabled.meta", "Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.cs.meta", true);
                System.IO.File.Copy("Packages/at.pimaker.ltcgi/Adapters/LTCGI_USharpVideoAdapter.asset_disabled", "Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.asset", true);
                System.IO.File.Copy("Packages/at.pimaker.ltcgi/Adapters/LTCGI_USharpVideoAdapter.asset_disabled.meta", "Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.asset.meta", true);
                System.IO.File.Copy("Packages/at.pimaker.ltcgi/Adapters/Editor/LTCGI_USharpVideoAdapterAutoSetup.cs_disabled", "Assets/_pi_/_LTCGI-Adapters/Editor/LTCGI_USharpVideoAdapterAutoSetup.cs", true);

                AssetDatabase.ImportAsset("Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.asset", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();

                UdonSharpProgramAsset adapter = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.asset");
                adapter.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_pi_/_LTCGI-Adapters/LTCGI_USharpVideoAdapter.cs");
                adapter.ApplyProgram();
                EditorUtility.SetDirty(adapter);

                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            }
#endif
        }
    }

    public interface ILTCGI_AutoSetup
    {
        // returns value if set up
        GameObject AutoSetupEditor(LTCGI_Controller controller);
    }
    #endif
}