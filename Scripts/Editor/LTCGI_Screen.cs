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
    public class LTCGI_Screen : MonoBehaviour
    {
        public Color Color = Color.white;

        public bool DoubleSided = false;

        [Tooltip("If enabled, allows moving the screen GameObject during runtime. Has CPU performance overhead (Udon).")]
        public bool Dynamic;

        public int TextureIndex;

        [Tooltip("Specular and Diffuse are the two types of lighting that LTCGI calculates. For performance, try disabling Diffuse and see if the result is visually similar, in which case you should leave it off.")]
        public bool Diffuse = true, Specular = true;
        public bool DiffuseFromLm;
        [SerializeField] private int diffMode;

        public int LightmapChannel;
        public float LightmapIntensity = 1.0f;

        public ColorMode ColorMode;

        public Vector2 SingleUV;

        [Tooltip("Workaround for some Blender imports. Try to enable it if you notice your reflection is sideways.")]
        public bool FlipUV;

        public RendererMode RendererMode = RendererMode.Distance;
        public MeshRenderer[] RendererList;
        [Min(0.0f)] public float RendererDistance = 15.0f;

        public bool Cylinder;
        public Vector3 CylinderBase;
        public float CylinderHeight;
        public float CylinderRadius;
        public float CylinderSize;
        public float CylinderAngle;

        private Vector3 prevPos, prevScale, prevRot;

        private bool update = false;

        public void Update()
        {
            // don't mess with Udon emulation
            if (EditorApplication.isPlaying)
                return;
            
            if (this.prevPos != this.transform.position ||
                this.prevRot != this.transform.rotation.eulerAngles ||
                this.prevScale != this.transform.lossyScale)
            {
                this.prevPos = this.transform.position;
                this.prevRot = this.transform.rotation.eulerAngles;
                this.prevScale = this.transform.lossyScale;
                update = true;
            }
            if (update && LTCGI_Controller.Singleton != null)
            {
                LTCGI_Controller.Singleton.UpdateMaterials(true, this);
                update = false;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawIcon(transform.position, "LTCGI_Screen_Gizmo.png", true);
        }

        void OnDrawGizmosSelected()
        {
            if (RendererMode == RendererMode.Distance)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, RendererDistance);
            }
        }
    }

    public enum ColorMode
    {
        Static = 0,
        Texture = 1,
        SingleUV = 2,
    }

    public enum RendererMode
    {
        All = 0,
        ExcludeListed = 1,
        OnlyListed = 2,
        Distance = 3,
    }

    [CustomEditor(typeof(LTCGI_Screen))]
    [CanEditMultipleObjects]
    public class LTCGI_ScreenEditor : Editor
    {
        SerializedProperty colorProp, sidedProp, dynamicProp, indexProp, colormodeProp, specProp, diffProp, lmProp, singleUVProp, rendererModeProp, rendererListProp, rendererDistProp, diffModeProp, diffuseFromLmProp, flipProp, lmIntensProp;

        LTCGI_Screen screen;

        private static Texture Logo;

        private enum LMChannel
        {
            Off = 0,
            Red = 1,
            Green = 2,
            Blue = 3,
        }

        private enum DiffMode
        {
            NoDiffuse = 0,
            LTCDiffuse = 1,
            LightmapDiffuse = 2,
        }

        void OnEnable()
        {
            colorProp = serializedObject.FindProperty("Color");
            sidedProp = serializedObject.FindProperty("DoubleSided");
            dynamicProp = serializedObject.FindProperty("Dynamic");
            indexProp = serializedObject.FindProperty("TextureIndex");
            colormodeProp = serializedObject.FindProperty("ColorMode");
            specProp = serializedObject.FindProperty("Specular");
            diffProp = serializedObject.FindProperty("Diffuse");
            lmProp = serializedObject.FindProperty("LightmapChannel");
            singleUVProp = serializedObject.FindProperty("SingleUV");
            rendererModeProp = serializedObject.FindProperty("RendererMode");
            rendererListProp = serializedObject.FindProperty("RendererList");
            rendererDistProp = serializedObject.FindProperty("RendererDistance");
            diffModeProp = serializedObject.FindProperty("diffMode");
            diffuseFromLmProp = serializedObject.FindProperty("DiffuseFromLm");
            lmIntensProp = serializedObject.FindProperty("LightmapIntensity");
            flipProp = serializedObject.FindProperty("FlipUV");

            screen = (LTCGI_Screen)target;
            Logo = Resources.Load("LTCGI-Logo") as Texture;
        }

        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 150;
            GUI.Box(GUILayoutUtility.GetRect(300, 150, style), Logo, style);

            serializedObject.Update();

            colorProp.colorValue = EditorGUILayout.ColorField(new GUIContent("Color"), colorProp.colorValue, true, false, true);

            if (colorProp.colorValue.maxColorComponent == 0.0f)
            {
                GUILayout.Label("WARNING: Screen is disabled with color black!");
            }
            
            if (GUILayout.Button("Try get Color from Material"))
            {
                var ren = screen.gameObject.GetComponent<Renderer>();
                if (ren != null)
                {
                    var mat = ren.sharedMaterial;
                    if (mat != null)
                    {
                        var col = mat.GetColor("_Color");
                        if (col != null)
                        {
                            colorProp.colorValue = col;
                        }
                    }
                }
            }

            EditorGUILayout.Separator();

            var dm = (DiffMode)EditorGUILayout.EnumPopup("Diffuse Mode", (DiffMode)diffModeProp.intValue);
            diffModeProp.intValue = (int)dm;
            diffProp.boolValue = dm != DiffMode.NoDiffuse;
            diffuseFromLmProp.boolValue = dm == DiffMode.LightmapDiffuse;

            EditorGUILayout.PropertyField(specProp);
            EditorGUILayout.PropertyField(dynamicProp);
            EditorGUILayout.PropertyField(sidedProp);
            EditorGUILayout.PropertyField(flipProp);

            EditorGUILayout.Separator();

            var texmode = (ColorMode)EditorGUILayout.EnumPopup("Color Mode", (ColorMode)colormodeProp.intValue);
            colormodeProp.intValue = (int)texmode;
            Action texSelect = () =>
            {
                EditorGUILayout.IntSlider(indexProp, 0,
                    LTCGI_Controller.Singleton == null && LTCGI_Controller.Singleton.StaticTextures != null ? 2 :
                        LTCGI_Controller.Singleton.StaticTextures.Length);

                if (LTCGI_Controller.Singleton != null)
                {
                    if (indexProp.intValue == 0)
                    {
                        GUILayout.Label("Texture: [Live Video]");
                    }
                    else
                    {
                        GUILayout.Label("Texture: " + LTCGI_Controller.Singleton.StaticTextures[indexProp.intValue - 1].name);
                    }
                }
            };
            switch (texmode)
            {
                case ColorMode.Static:
                    indexProp.intValue = 0;
                    break;
                case ColorMode.Texture:
                    texSelect();
                    break;
                case ColorMode.SingleUV:
                    texSelect();
                    singleUVProp.vector2Value = EditorGUILayout.Vector2Field("Texture UV", singleUVProp.vector2Value);
                    break;
            }

            EditorGUILayout.Separator();

            var rendererMode = (RendererMode)EditorGUILayout.EnumPopup("Affected Renderers", (RendererMode)rendererModeProp.intValue);
            rendererModeProp.intValue = (int)rendererMode;
            if (rendererMode != RendererMode.All && rendererMode != RendererMode.Distance)
            {
                EditorGUILayout.PropertyField(rendererListProp, new GUIContent("Renderer List"), true);
            }
            else if (rendererMode == RendererMode.Distance)
            {
                EditorGUILayout.PropertyField(rendererDistProp);
            }

            EditorGUILayout.Separator();

            var lmch = (LMChannel)EditorGUILayout.EnumPopup("Lightmap Channel", (LMChannel)lmProp.intValue);
            lmProp.intValue = (int)lmch;

            if (lmch != LMChannel.Off)
            {
                EditorGUILayout.PropertyField(lmIntensProp);
            }

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                LTCGI_Controller.Singleton?.UpdateMaterials();
            }
        }
    }
#endif
}