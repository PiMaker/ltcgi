#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    public partial class LTCGI_Controller
    {
        public GameObject ConfiguredAdapter;

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
    }

    public interface ILTCGI_AutoSetup
    {
        // returns value if set up
        GameObject AutoSetupEditor(LTCGI_Controller controller);
    }
    #endif
}