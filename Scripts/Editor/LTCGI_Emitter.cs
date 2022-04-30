#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    public class LTCGI_Emitter : LTCGI_Screen
    {
        public Renderer[] EmissiveRenderers;

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

            EditorGUILayout.HelpBox("This is an emitter component. It can only produce diffuse, untextured light. It can however apply to multiple objects and is a lot cheaper to use.", MessageType.Info);
            EditorGUILayout.Space();

            DrawColorSelector(emitter);
            EditorGUILayout.Space();
            DrawColorModeSelector(false);
            EditorGUILayout.Space();
            DrawRendererModeSelector();
            EditorGUILayout.Space();
            DrawLmChannelSelector();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("List all emissive renderers below:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("EmissiveRenderers"), true);

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                LTCGI_Controller.Singleton?.UpdateMaterials();
            }
        }
    }
    #endif
}