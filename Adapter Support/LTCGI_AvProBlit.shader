Shader "LTCGI/AvProBlit"
{
    Properties
    {
        _MainTex ("Input Texture", 2D) = "white" {}
        [ToggleUI] _Gamma ("Apply Gamma", Float) = 0
        [ToggleUI] _FlipUV ("Flip Y UV", Float) = 0
        
		_AspectRatio ("Aspect Ratio", Float) = 1.777777

        _OverlayTexture ("Overlay Texture", 2D) = "" {}
        _OverlayOpacity ("Overlay Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityCustomRenderTexture.cginc"

            sampler2D _MainTex;
    		float4 _MainTex_TexelSize;

            float _Gamma, _FlipUV;
            float _AspectRatio;

            sampler2D _OverlayTexture;
		    float4 _OverlayTexture_TexelSize;
            float _OverlayOpacity;

            // Aspect ratio correction by Merlin from USharpVideo
            // taken from AVPro sources
            void correctUV(inout float2 uv, inout float visibility, float2 res)
            {
                float curAspectRatio = res.x / res.y;
                if (abs(curAspectRatio - _AspectRatio) > .01) {
					float2 normRes = float2(res.x / _AspectRatio, res.y);
					float2 correction;

					if (normRes.x > normRes.y)
						correction = float2(1, normRes.y / normRes.x);
					else
						correction = float2(normRes.x / normRes.y, 1);

					uv = ((uv - 0.5) / correction) + 0.5;

					float2 uvPadding = (1 / res) * 0.1;
					float2 uvFwidth = fwidth(uv.xy);
					float2 maxf = smoothstep(uvFwidth + uvPadding + 1, uvPadding + 1, uv.xy);
					float2 minf = smoothstep(-uvFwidth - uvPadding, -uvPadding, uv.xy);
					// calculate the min/max of the true size to apply a 0 to anything beyond the ratio value.
					// This creates the "Black Bars" around the video when the video doesn't match the texel size.
					// If this isn't used, the edge pixels end up getting repeated where the black bars are.
					visibility = maxf.x * maxf.y * minf.x * minf.y;
				}
            }

            half4 frag (v2f_customrendertexture i) : SV_Target
            {
                float2 mainTexUv = i.globalTexcoord.xy;
                float2 overlayTexUv = i.globalTexcoord.xy;
                #ifdef UNITY_UV_STARTS_AT_TOP
                _FlipUV = !_FlipUV;
                #endif
                if (_FlipUV) {
                    mainTexUv.y = 1 - mainTexUv.y;
                    overlayTexUv.y = 1 - overlayTexUv.y;
                }

                float visibility = 1;
                correctUV(mainTexUv, visibility, _MainTex_TexelSize.zw);

                half4 color = tex2Dlod(_MainTex, float4(mainTexUv, 0, 0)) * visibility;
                if (_OverlayOpacity)
                {
                    visibility = 1;
                    correctUV(overlayTexUv, visibility, _OverlayTexture_TexelSize.zw);
                    color = lerp(color, tex2Dlod(_OverlayTexture, float4(overlayTexUv, 0, 0)) * visibility, _OverlayOpacity);
                }

                if (_Gamma) return pow(color, 2.2);
                return color;
            }
            ENDCG
        }
    }
}
