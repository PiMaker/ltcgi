#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    // Serialization helper for lightmap baking primitives.
    [ExecuteInEditMode]
    public class LTCGI_BakeReset : MonoBehaviour
    {
        public bool Reenable;

        public bool ResetData;
        public Material[] Materials;
        public StaticEditorFlags Flags;
        public ShadowCastingMode ShadowCastingMode;

        public bool RemoveBakeryLightMesh;

        internal void ApplyReset()
        {
            if (Reenable)
            {
                this.gameObject.SetActive(true);
            }
            if (ResetData)
            {
                var rend = this.GetComponent<Renderer>();
                rend.sharedMaterials = Materials;
                rend.shadowCastingMode = ShadowCastingMode;
                GameObjectUtility.SetStaticEditorFlags(this.gameObject, Flags);
            }

            #if BAKERY_INCLUDED
            if (RemoveBakeryLightMesh)
            {
                var lm = this.GetComponent<BakeryLightMesh>();
                if (lm != null)
                {
                    DestroyImmediate(lm);
                }
            }
            #endif
        }
    }
    #endif
}