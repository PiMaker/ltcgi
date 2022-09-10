// See also: https://www.ronja-tutorials.com/post/023-postprocessing-blur/
Shader "LTCGI/Blur Prefilter"
{
	Properties
    {
		_MainTex ("Texture", 2D) = "white" {}
		_BlurSize ("Blur Size", Range(0, 1.0)) = 0.5
		[PowerSlider(3)] _StandardDeviation ("Standard Deviation", Range(0.0, 0.5)) = 0.05
        _Samples ("Samples", Range(1.0, 100.0)) = 100.0
        _LOD ("Mip Map LOD", Int) = 2
        _OutsideMult ("Outside Blur Multiplier", Float) = 2.0
        _InsetSample ("Inset UV (sample)", Float) = 0.0
        _InsetCalculate ("Inset UV (calculate)", Float) = 0.0
        [ToggleUI] _OutsideBlurOnly ("Outside Blur Only", Float) = 0.0
	}

	SubShader
    {
		Cull Off
		ZWrite Off 
		ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        // CRT blurb (from UnityCustomRenderTexture.cginc)
        #define kCustomTextureBatchSize 16

        struct appdata_customrendertexture
        {
            uint    vertexID    : SV_VertexID;
        };

        // User facing vertex to fragment shader structure
        struct v2f_customrendertexture
        {
            float4 vertex           : SV_POSITION;
            float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
            float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
            uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
        };

        // Internal
        float4      CustomRenderTextureCenters[kCustomTextureBatchSize];
        float4      CustomRenderTextureSizesAndRotations[kCustomTextureBatchSize];
        float       CustomRenderTexturePrimitiveIDs[kCustomTextureBatchSize];

        float4      CustomRenderTextureParameters;
        #define     CustomRenderTextureUpdateSpace  CustomRenderTextureParameters.x // Normalized(0)/PixelSpace(1)
        #define     CustomRenderTexture3DTexcoordW  CustomRenderTextureParameters.y
        #define     CustomRenderTextureIs3D         CustomRenderTextureParameters.z

        // User facing uniform variables
        float4      _CustomRenderTextureInfo; // x = width, y = height, z = depth, w = face/3DSlice

        // Helpers
        #define _CustomRenderTextureWidth   _CustomRenderTextureInfo.x
        #define _CustomRenderTextureHeight  _CustomRenderTextureInfo.y
        #define _CustomRenderTextureDepth   _CustomRenderTextureInfo.z

        Texture2D<float4>   _SelfTexture2D; // NOTE: THIS IS THE IMPORTANT MODIFICATION!

        // standard custom texture vertex shader that should always be used
        v2f_customrendertexture CustomRenderTextureVertexShader(appdata_customrendertexture IN)
        {
            v2f_customrendertexture OUT;

        #if UNITY_UV_STARTS_AT_TOP
            const float2 vertexPositions[6] =
            {
                { -1.0f,  1.0f },
                { -1.0f, -1.0f },
                {  1.0f, -1.0f },
                {  1.0f,  1.0f },
                { -1.0f,  1.0f },
                {  1.0f, -1.0f }
            };

            const float2 texCoords[6] =
            {
                { 0.0f, 0.0f },
                { 0.0f, 1.0f },
                { 1.0f, 1.0f },
                { 1.0f, 0.0f },
                { 0.0f, 0.0f },
                { 1.0f, 1.0f }
            };
        #else
            const float2 vertexPositions[6] =
            {
                {  1.0f,  1.0f },
                { -1.0f, -1.0f },
                { -1.0f,  1.0f },
                { -1.0f, -1.0f },
                {  1.0f,  1.0f },
                {  1.0f, -1.0f }
            };

            const float2 texCoords[6] =
            {
                { 1.0f, 1.0f },
                { 0.0f, 0.0f },
                { 0.0f, 1.0f },
                { 0.0f, 0.0f },
                { 1.0f, 1.0f },
                { 1.0f, 0.0f }
            };
        #endif

            uint primitiveID = IN.vertexID / 6;
            uint vertexID = IN.vertexID % 6;
            float3 updateZoneCenter = CustomRenderTextureCenters[primitiveID].xyz;
            float3 updateZoneSize = CustomRenderTextureSizesAndRotations[primitiveID].xyz;

            // Normalize rect if needed
            if (CustomRenderTextureUpdateSpace > 0.0) // Pixel space
            {
                // Normalize xy because we need it in clip space.
                updateZoneCenter.xy /= _CustomRenderTextureInfo.xy;
                updateZoneSize.xy /= _CustomRenderTextureInfo.xy;
            }
            else // normalized space
            {
                // Un-normalize depth because we need actual slice index for culling
                updateZoneCenter.z *= _CustomRenderTextureInfo.z;
                updateZoneSize.z *= _CustomRenderTextureInfo.z;
            }

            // Compute quad vertex position
            float2 clipSpaceCenter = updateZoneCenter.xy * 2.0 - 1.0;
            float2 pos = vertexPositions[vertexID] * updateZoneSize.xy;
            pos.x += clipSpaceCenter.x;
        #if UNITY_UV_STARTS_AT_TOP
            pos.y += clipSpaceCenter.y;
        #else
            pos.y -= clipSpaceCenter.y;
        #endif

            OUT.vertex = float4(pos, 0.0, 1.0);
            OUT.primitiveID = asuint(CustomRenderTexturePrimitiveIDs[primitiveID]);
            OUT.localTexcoord = float3(texCoords[vertexID], CustomRenderTexture3DTexcoordW);
            OUT.globalTexcoord = float3(pos.xy * 0.5 + 0.5, CustomRenderTexture3DTexcoordW);
        #if UNITY_UV_STARTS_AT_TOP
            OUT.globalTexcoord.y = 1.0 - OUT.globalTexcoord.y;
        #endif

            return OUT;
        }
        // END CRT blurb

        #pragma vertex CustomRenderTextureVertexShader
        #pragma fragment frag

        float _BlurSize;
        float _StandardDeviation;
        int _Samples;
        int _LOD;
        float _OutsideMult, _OutsideBlurOnly;
        float _InsetSample, _InsetCalculate;

        #define E 2.718281828f
        
        uniform SamplerState _blur_trilinear_mirror_sampler;
        uniform SamplerState _blur_trilinear_clamp_sampler;

        half3 alpha(half4 i)
        {
            return i.rgb * i.a;
        }

        half3 frag_blur(Texture2D<float4> tex, float2 iuv_sample, float2 iuv_calc, float2 dir)
        {
            half3 col = 0;
            float sum = 0;

            float2 outside = saturate(abs(iuv_calc - 0.5) - 0.5);
            outside = smoothstep(0.0, _InsetCalculate*4, outside);
            float2 dirmod = 1 + smoothstep(0.5, 1.0, abs(iuv_calc - 0.5).yx);

            if (!any(outside))
            {
                if (_OutsideBlurOnly)
                {
                    return alpha(tex.SampleLevel(_blur_trilinear_clamp_sampler, iuv_sample, _LOD));
                }

                dirmod = 1;
            }

            //return half4(dirmod - 1, 0, 1);

            float olen = length(outside);
            float mult = (1 - _OutsideBlurOnly) + olen * _OutsideMult;
            float stddev = _StandardDeviation * mult;
            dirmod *= mult;
            _Samples = clamp(_Samples * mult, 0, 90);

            [loop]
            for (float index = 0; index < _Samples; index++)
            {
                float2 offset = ((index/(_Samples-1) - 0.5) * _BlurSize).xx * dirmod;
                float2 uv = iuv_sample + dir*offset;
                float sq = stddev * stddev;
                float gauss = (1 / sqrt(2*UNITY_PI*sq)) * pow(E, -((offset*offset)/(2*sq)));
                sum += gauss;
                col += lerp(
                        alpha(tex.SampleLevel(_blur_trilinear_clamp_sampler, uv, _LOD)),
                        alpha(tex.SampleLevel(_blur_trilinear_mirror_sampler, uv, _LOD)),
                        saturate(olen * 3.6 - 0.25)
                    ) * gauss;
            }

            col = col / sum;
            return col;
        }
        ENDCG
		
        Pass
        {
            Name "Vertical"

            CGPROGRAM
            Texture2D<float4> _MainTex;
            half4 frag(v2f_customrendertexture i) : SV_TARGET
            {
                const float2 dir = float2(0, 1);
                float2 insSample = float2(_InsetSample, _InsetSample) * dir.yx;
                float2 insCalc = float2(_InsetCalculate, _InsetCalculate) * dir.yx;
                float2 uv_sample = (1 + insSample*2) * i.globalTexcoord.xy - insSample;
                float2 uv_calc = (1 + insCalc*2) * i.globalTexcoord.xy - insCalc;
                return half4(frag_blur(_MainTex, uv_sample, uv_calc, dir), 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Horizontal"

            CGPROGRAM
            Texture2D<float4> _MainTex;
            half4 frag(v2f_customrendertexture i) : SV_TARGET
            {
                const float2 dir = float2(1, 0);
                float2 insSample = float2(_InsetSample, _InsetSample) * dir.yx;
                float2 insCalc = float2(_InsetCalculate, _InsetCalculate) * dir.yx;
                float2 uv_sample = (1 + insSample*2) * i.globalTexcoord.xy - insSample;
                float2 uv_calc = (1 + insCalc*2) * i.globalTexcoord.xy - insCalc;
                return half4(frag_blur(_MainTex, uv_sample, uv_calc, dir), 1);
            }
            ENDCG
        }
	}
}