#if UDONSHARP
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LTCGI_USharpVideoAdapter : UdonSharpBehaviour
{
    const string MATERIAL_PARAM_TEX = "_MainTex";
    const string MATERIAL_PARAM_OVERLAY_OPACITY = "_OverlayOpacity";
    const string MATERIAL_PARAM_GAMMA = "_Gamma";
    const string MATERIAL_PARAM_FLIPUV = "_FlipUV";

    public USharpVideoPlayer VideoPlayer;
    public CustomRenderTexture CRT;

    [Tooltip("Place the same Standby Texture as in your VideoScreenHandler here if you want it to reflect too. Should be black if unset.")]
    public Texture StandbyTexture;

    private Material sharedMaterial;

    void Start()
    {
        sharedMaterial = CRT.material;
        VideoPlayer.RegisterCallbackReceiver(this);

        sharedMaterial.SetFloat(MATERIAL_PARAM_OVERLAY_OPACITY, 0.0f);
    }

    public void OnUSharpVideoModeChange() => OnUSharpVideoRenderTextureChange();

    public void OnUSharpVideoRenderTextureChange()
    {
        var manager = VideoPlayer.GetVideoManager();
        var tex = manager.GetVideoTexture();
        var unity = VideoPlayer.IsUsingUnityPlayer();

        if (tex == null)
        {
            Debug.Log("[LTCGI_USharpVideoAdapter] set to standby texture");
            sharedMaterial.SetTexture(MATERIAL_PARAM_TEX, StandbyTexture);
            sharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 1.0f);
            sharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 0.0f);
        }
        else
        {
            sharedMaterial.SetTexture(MATERIAL_PARAM_TEX, tex);
            if (unity)
            {
                Debug.Log("[LTCGI_USharpVideoAdapter] set to unity player");
                sharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 1.0f);
                sharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 0.0f);
            }
            else
            {
                Debug.Log("[LTCGI_USharpVideoAdapter] set to avpro player");
                sharedMaterial.SetFloat(MATERIAL_PARAM_FLIPUV, 0.0f);
                sharedMaterial.SetFloat(MATERIAL_PARAM_GAMMA, 1.0f);
            }
        }
    }
}
#endif