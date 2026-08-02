#if LTCGI_VRC_LIGHT_VOLUMES

using UnityEngine;
using UnityEditor;
using VRCLightVolumes;
using System;
using UnityEngine.UI;

namespace pi.LTCGI.LVAdapter
{
    [AddComponentMenu("LTCGI Light Volume (U# Script)")]
    [DisallowMultipleComponent]
#if UDONSHARP
    [UdonSharp.UdonBehaviourSyncMode(UdonSharp.BehaviourSyncMode.None)]
#endif
    public class LightVolumeLTCGI : LightVolumeInstance
    {
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    [CustomEditor(typeof(LightVolumeLTCGI))]
    public class LightVolumeLTCGIEditor : LightVolumeEditor
    {
        private static Lazy<Texture> Logo = new Lazy<Texture>(() => Resources.Load("VolumeIcon") as Texture);

        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 100;
            style.fontSize = 42;
            style.fontStyle = FontStyle.Bold;
            GUILayout.BeginHorizontal();
            GUI.Box(GUILayoutUtility.GetRect(200, 100, style), LTCGI_ControllerEditor.Logo.Value, style);
            GUI.Box(GUILayoutUtility.GetRect(20, 100, style), "<3", style);
            GUI.Box(GUILayoutUtility.GetRect(200, 100, style), Logo.Value, style);
            GUILayout.EndHorizontal();

            var rightAlignedLabel = new GUIStyle(EditorStyles.label);
            rightAlignedLabel.alignment = TextAnchor.MiddleRight;
            GUILayout.Label(LTCGI_ControllerEditor.VERSION, rightAlignedLabel);

            EditorGUILayout.HelpBox(
                "This is a Light Volume for LTCGI. Certain properties like 'Additive' may not be editable.",
                MessageType.Info
            );

            if (serializedObject.FindProperty("IsDynamic").boolValue == false && (target as LightVolumeLTCGI).isActiveAndEnabled == false)
            {
                EditorGUILayout.HelpBox(
                    "This Light Volume is not Dynamic and is disabled. It will never turn on if you don't set Dynamic.",
                    MessageType.Warning
                );
            }

            base.OnInspectorGUI();

            serializedObject.Update();
            serializedObject.FindProperty("IsAdditive").boolValue = true;
            serializedObject.FindProperty("Bake").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        new protected void OnSceneGUI() {
            base.OnSceneGUI(); // forward call
        }
    }
#endif
}

#endif