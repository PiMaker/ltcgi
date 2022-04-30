#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
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

        [Range(0, 3)]
        public int AudioLinkBand;

        [Tooltip("Workaround for some Blender imports. Try to enable it if you notice your reflection is sideways.")]
        public bool FlipUV;

        public RendererMode RendererMode = RendererMode.Distance;
        public MeshRenderer[] RendererList;
        [Min(0.0f)] public float RendererDistance = 15.0f;

        public bool Cylinder;
        public Vector3 CylinderBase;
        public float CylinderHeight = 1.0f;
        public float CylinderRadius = 1.0f;
        [Range(0.0f, Mathf.PI*0.5f)]
        public float CylinderSize = Mathf.PI*0.5f;
        [Range(0.0f, Mathf.PI*2.0f)]
        public float CylinderAngle;

        private Vector3 prevPos, prevScale, prevRot;

        private bool update = false;

        private static readonly Color[] GIZMO_COLORS = new Color[]
        {
            Color.white,
            Color.red,
            Color.green,
            Color.blue,
        };

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
            Gizmos.color = GIZMO_COLORS[this.LightmapChannel];
            Gizmos.DrawIcon(transform.position, "LTCGI_Screen_Gizmo.png", true, Gizmos.color);
        }

        private static Mesh cylMesh = null;
        void OnDrawGizmosSelected()
        {
            if (RendererMode == RendererMode.Distance)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, RendererDistance);
            }

            if (Cylinder)
            {
                if (cylMesh == null)
                {
                    cylMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                }

                Gizmos.color = new Color(0, 1, 1, 0.2f);
                Gizmos.DrawMesh(cylMesh, 0,
                    transform.position + CylinderBase - Vector3.up * 0.5f + Vector3.up * CylinderHeight,
                    Quaternion.AngleAxis(Mathf.Rad2Deg * CylinderAngle, Vector3.up),
                    new Vector3(CylinderRadius, CylinderHeight, CylinderRadius) * 1.01f);
            }
        }
    }

    public enum ColorMode
    {
        Static = 0,
        Texture = 1,
        SingleUV = 2,
        AudioLink = 3,
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
        protected SerializedProperty colorProp, sidedProp, dynamicProp, indexProp, colormodeProp, specProp, diffProp, lmProp, singleUVProp, rendererModeProp, rendererListProp, rendererDistProp, diffModeProp, diffuseFromLmProp, flipProp, lmIntensProp, alBandProp;
        protected SerializedProperty cylProp, cylBaseProp, cylHeightProp, cylAngleProp, cylRadiusProp, cylSizeProp;

        protected static Texture Logo;

        protected enum LMChannel
        {
            Off = 0,
            Red = 1,
            Green = 2,
            Blue = 3,
        }

        protected enum DiffMode
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
            alBandProp = serializedObject.FindProperty("AudioLinkBand");

            cylProp = serializedObject.FindProperty("Cylinder");
            cylAngleProp = serializedObject.FindProperty("CylinderAngle");
            cylBaseProp = serializedObject.FindProperty("CylinderBase");
            cylHeightProp = serializedObject.FindProperty("CylinderHeight");
            cylRadiusProp = serializedObject.FindProperty("CylinderRadius");
            cylSizeProp = serializedObject.FindProperty("CylinderSize");

            Logo = Resources.Load("LTCGI-Logo") as Texture;
        }

        public override void OnInspectorGUI()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fixedHeight = 150;
            GUI.Box(GUILayoutUtility.GetRect(300, 150, style), Logo, style);

            var screen = (LTCGI_Screen)target;

            serializedObject.Update();

            GUILayout.Label("Area light shape:");
            var isCylinder = cylProp.boolValue;
            using (var hor = new EditorGUILayout.HorizontalScope())
            {
                var leftStyle = EditorStyles.miniButtonLeft;
                var midStyle = EditorStyles.miniButtonMid;
                var rightStyle = EditorStyles.miniButtonRight;
                var toggleMesh = GUILayout.Toggle(!isCylinder, "Quad/Triangle", leftStyle);
                var toggleCyl = GUILayout.Toggle(isCylinder, "Cylinder", rightStyle);

                if ((toggleMesh && isCylinder) || (!toggleMesh && !isCylinder))
                {
                    isCylinder = cylProp.boolValue = !toggleMesh;
                }
                else if ((toggleCyl && !isCylinder) || (!toggleCyl && isCylinder))
                {
                    isCylinder = cylProp.boolValue = toggleCyl;
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (!isCylinder)
            {
                var mesh = screen.gameObject.GetComponent<MeshFilter>()?.sharedMesh;
                string error = "";
                if (mesh == null)
                {
                    error = "No mesh or mesh filter assigned!";
                }
                else if (!mesh.isReadable)
                {
                    error = "Mesh is not readable!";
                }
                else if (mesh.vertexCount != 4 && mesh.vertexCount != 3)
                {
                    error = "Mesh does not have exactly 3 or 4 vertices!";
                }
                if (error != "")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(error, MessageType.Error, true);
                    EditorGUILayout.Space();
                }
            }

            if (isCylinder)
            {
                EditorGUILayout.PropertyField(cylBaseProp);
                EditorGUILayout.PropertyField(cylHeightProp);
                EditorGUILayout.PropertyField(cylRadiusProp);
                EditorGUILayout.PropertyField(cylAngleProp);
                EditorGUILayout.PropertyField(cylSizeProp);
                if (GUILayout.Button("Try cylinder auto-detect"))
                {
                    var coll = screen.gameObject.GetComponent<Collider>();
                    if (coll != null)
                    {
                        cylBaseProp.vector3Value = new Vector3(0, -coll.bounds.extents.y / 2.0f, 0);
                        cylHeightProp.floatValue = coll.bounds.extents.y;
                        cylRadiusProp.floatValue = Mathf.Max(coll.bounds.extents.x, coll.bounds.extents.z);
                    }
                    else
                    {
                        cylHeightProp.floatValue = screen.transform.localScale.y;
                        cylRadiusProp.floatValue = screen.transform.localScale.x / 2.0f;
                    }
                }

                EditorGUILayout.Separator();
            }

            DrawColorSelector(screen);

            EditorGUILayout.Space();
            EditorGUILayout.Separator();

            var dm = (DiffMode)EditorGUILayout.EnumPopup("Diffuse Mode", (DiffMode)diffModeProp.intValue);
            if (diffModeProp.intValue != (int)dm)
            {
                diffModeProp.intValue = (int)dm;
                diffProp.boolValue = dm != DiffMode.NoDiffuse;
                diffuseFromLmProp.boolValue = dm == DiffMode.LightmapDiffuse;
            }

            if (diffuseFromLmProp.boolValue && lmProp.intValue == 0)
            {
                EditorGUILayout.HelpBox("You have \"Lightmap Diffuse\" enabled but no lightmap channel selected - this is probably not what you want.", MessageType.Warning, true);
            }

            EditorGUILayout.PropertyField(specProp);
            EditorGUILayout.PropertyField(dynamicProp);
            if (isCylinder)
            {
                sidedProp.boolValue = false;
            }
            else
            {
                EditorGUILayout.PropertyField(sidedProp);
            }
            EditorGUILayout.PropertyField(flipProp);

            EditorGUILayout.Separator();
            DrawColorModeSelector(true);
            EditorGUILayout.Separator();
            DrawRendererModeSelector();
            EditorGUILayout.Separator();
            DrawLmChannelSelector();

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                LTCGI_Controller.Singleton?.UpdateMaterials();
            }
        }

        protected void DrawColorSelector(LTCGI_Screen screen)
        {
            var newCol = EditorGUILayout.ColorField(new GUIContent("Color"), colorProp.colorValue, true, false, true);
            if (colorProp.colorValue != newCol)
            {
                colorProp.colorValue = newCol;
            }

            if (colorProp.colorValue.maxColorComponent == 0.0f)
            {
                EditorGUILayout.HelpBox("Screen is disabled with color black!", MessageType.Warning, true);
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
        }

        protected void DrawColorModeSelector(bool allowTextured)
        {
            var texmode = (ColorMode)EditorGUILayout.EnumPopup("Color Mode", (ColorMode)colormodeProp.intValue);
            if (colormodeProp.intValue != (int)texmode)
            {
                colormodeProp.intValue = (int)texmode;
            }
            if (targets.Length == 1)
            {
                Action texSelect = () =>
                {
                    EditorGUILayout.IntSlider(indexProp, 0,
                        LTCGI_Controller.Singleton == null && LTCGI_Controller.Singleton.StaticTextures != null ? 2 :
                            LTCGI_Controller.Singleton.StaticTextures.Length);

                    if (LTCGI_Controller.Singleton != null)
                    {
                        if (indexProp.intValue == 0)
                        {
                            EditorGUILayout.HelpBox("Texture: [Live Video]", MessageType.None, false);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Texture: " + LTCGI_Controller.Singleton.StaticTextures[indexProp.intValue - 1].name, MessageType.None, false);
                        }
                    }
                };
                switch (texmode)
                {
                    case ColorMode.Static:
                        indexProp.intValue = 0;
                        break;
                    case ColorMode.Texture:
                        if (allowTextured)
                        {
                            texSelect();
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Texture mode is not allowed for emitters! Falling back to static color.", MessageType.Error, true);
                        }
                        break;
                    case ColorMode.SingleUV:
                        texSelect();
                        singleUVProp.vector2Value = EditorGUILayout.Vector2Field("Texture UV", singleUVProp.vector2Value);
                        break;
                    case ColorMode.AudioLink:
                        EditorGUILayout.PropertyField(alBandProp);
                        string[] bandNames = new[] {"Bass", "Low Mids", "High Mids", "Treble"};
                        EditorGUILayout.HelpBox($"Selected Band: {bandNames[alBandProp.intValue]}", MessageType.None, false);
                        break;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("(cannot multi-edit 'Color Mode' settings)", MessageType.None, false);
            }
        }

        protected void DrawRendererModeSelector()
        {
            var rendererMode = (RendererMode)EditorGUILayout.EnumPopup("Affected Renderers", (RendererMode)rendererModeProp.intValue);
            if (rendererModeProp.intValue != (int)rendererMode)
            {
                rendererModeProp.intValue = (int)rendererMode;
            }
            if (rendererMode != RendererMode.All && rendererMode != RendererMode.Distance)
            {
                EditorGUILayout.PropertyField(rendererListProp, new GUIContent("Renderer List"), true);
            }
            else if (rendererMode == RendererMode.Distance)
            {
                EditorGUILayout.PropertyField(rendererDistProp);
            }
        }

        protected void DrawLmChannelSelector()
        {
            var lmch = (LMChannel)EditorGUILayout.EnumPopup("Lightmap Channel", (LMChannel)lmProp.intValue);
            if (lmProp.intValue != (int)lmch)
            {
                lmProp.intValue = (int)lmch;
            }

            if (lmch != LMChannel.Off)
            {
                EditorGUILayout.PropertyField(lmIntensProp);
            }
        }
    }
#endif
}