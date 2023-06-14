#if UDONSHARP
using UdonSharp;
using UnityEngine;
using ArchiTech;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LTCGI_ProTvAdapter : UdonSharpBehaviour
{
    const string MATERIAL_PARAM_OVERLAY_TEX = "_OverlayTexture";
    const string MATERIAL_PARAM_OVERLAY_OPACITY = "_OverlayOpacity";
    const string MATERIAL_PARAM_GAMMA = "_Gamma";
    const string MATERIAL_PARAM_FLIPUV = "_FlipUV";

    public TVManagerV2 Tv;

    public Material SharedMaterial;
    public CustomRenderTexture BlitCRT;
    public Texture2D AvProBranding;

    public GameObject[] AdapterScreensKey;
    public GameObject[] AdapterScreensValue;
    public bool[] AdapterScreensIsUnity;

    void Start()
    {
        Tv._RegisterUdonSharpEventReceiver(this);
        _TvStop();
    }

    public void _TvMediaStart() => changeRenderTexture(Tv.activeManager);
    public void _TvPlay() => changeRenderTexture(Tv.activeManager);
    //public void _TvMediaEnd() => _TvStop();
    public void _TvStop()
    {
        _SetOverlayEnabled();
        changeRenderTexture(null);
    }

    public void _SetOverlayEnabled()
    {
        SharedMaterial.SetTexture(MATERIAL_PARAM_OVERLAY_TEX, AvProBranding);
        SharedMaterial.SetFloat(MATERIAL_PARAM_OVERLAY_OPACITY, 1.0f);
        SharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 0.0f);
        SharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 1.0f);
    }

    private void changeRenderTexture(VideoManagerV2 manager)
    {
        for (int i = 0; i < AdapterScreensKey.Length; i++)
        {
            if (manager != null && AdapterScreensKey[i] == manager.gameObject)
            {
                var unity = AdapterScreensIsUnity[i];
                if (unity)
                {
                    var prop = new MaterialPropertyBlock();
                    manager.screens[0].GetComponent<Renderer>().GetPropertyBlock(prop);
                    var tex = prop.GetTexture("_MainTex");
                    SharedMaterial.SetTexture(MATERIAL_PARAM_OVERLAY_TEX, tex);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_OVERLAY_OPACITY, 1.0f);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 0.0f);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 1.0f);
                }
                else
                {
                    AdapterScreensValue[i].SetActive(true);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_OVERLAY_OPACITY, 0.0f);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 1.0f);
                    SharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 0.0f);
                }
                Debug.Log("[LTCGI_ProTvAdapter] switched to " + AdapterScreensKey[i].name + " (unity: " + unity + ")");
            }
            else
            {
                if (!AdapterScreensIsUnity[i])
                {
                    AdapterScreensValue[i].SetActive(false);
                }
            }
        }
    }
}
#endif