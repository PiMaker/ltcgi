#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    public class LTCGI_Emitter : LTCGI_Screen
    {
        public Renderer[] EmissiveRenderers;

        [HideInInspector] public bool Initialized = false;

        public LTCGI_Emitter()
        {
            // static values for emitters
            this.Cylinder = false;
            this.Diffuse = true;
            this.DiffuseFromLm = true;
            this.DoubleSided = true;
            this.Dynamic = false;
            this.FlipUV = false;
            this.Specular = false;
        }
    }

    [CustomEditor(typeof(LTCGI_Emitter))]
    [CanEditMultipleObjects]
    public class LTCGI_EmitterEditor : LTCGI_ScreenEditor
    {
        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 150;
            GUI.Box(GUILayoutUtility.GetRect(300, 150, style), Logo, style);

            var emitter = (LTCGI_Emitter)target;

            if (!emitter.enabled || !emitter.gameObject.activeInHierarchy)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("This component is disabled, or the GameObject not active! This will cause it to not bake a lightmap, and can cause issues at runtime. If you want this renderer to start disabled, set it's color to Black (0,0,0) or visit https://ltcgi.dev/ to see how you can toggle LTCGI globally.", MessageType.Error, true);
                LTCGIDocsHelper.DrawHelpButton("https://ltcgi.dev/Getting%20Started/Setup/Basic_Toggle", "LTCGI Toggle");
                EditorGUILayout.Space();
            }

            LTCGIDocsHelper.DrawHelpButton("https://ltcgi.dev/Advanced/LTCGI_Emitter");

            serializedObject.Update();

            var emissiveRenderers = serializedObject.FindProperty("EmissiveRenderers");

            if (!emitter.Initialized)
            {
                serializedObject.FindProperty("Initialized").boolValue = true;
                lmProp.intValue = 1;
                emissiveRenderers.InsertArrayElementAtIndex(0);
                emissiveRenderers.GetArrayElementAtIndex(0).objectReferenceValue = emitter.GetComponent<Renderer>();
            }

            EditorGUILayout.HelpBox("This is an emitter component. It can only produce diffuse, untextured light. It can however apply to multiple objects and is a lot cheaper to use.", MessageType.Info);
            EditorGUILayout.Space();

            DrawColorSelector(emitter);
            EditorGUILayout.Space();
            DrawColorModeSelector(false);
            EditorGUILayout.Space();
            DrawRendererModeSelector();
            EditorGUILayout.Space();
            DrawLmChannelSelector();

            if (lmProp.intValue == 0)
            {
                EditorGUILayout.HelpBox("This emitter is not using a lightmap channel. It will not look correct.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("List all emissive renderers below:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(emissiveRenderers, true);

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                LTCGI_Controller.Singleton?.UpdateMaterials();
            }
        }
    }
    #endif
}