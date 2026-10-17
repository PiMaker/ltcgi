// URP port of LTCGI/Simple. Requires URP 17.7+.
Shader "LTCGI/Simple URP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "17.7.0"
        }

        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "LTCGI" = "ALWAYS"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Roughness;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        ENDHLSL

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC
            #pragma multi_compile_fragment _ _EXPOSURE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DistanceFog.hlsl"
            #include "Packages/at.pimaker.ltcgi/Shaders/LTCGI_URP_Compat.hlsl"
            #include "Packages/at.pimaker.ltcgi/Shaders/LTCGI.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0; // xy = main, zw = raw uv1 for the LTCGI shadowmap
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vp.positionCS;
                output.positionWS = vp.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv.xy      = TRANSFORM_TEX(input.uv0, _MainTex);
                output.uv.zw      = input.uv1;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy) * _Color;

                half3 diff = 0;
                half3 spec = 0;
                LTCGI_Contribution(
                    input.positionWS,
                    normalize(input.normalWS),
                    GetWorldSpaceNormalizeViewDir(input.positionWS),
                    _Roughness,
                    input.uv.zw,
                    diff,
                    spec
                );

                col.rgb *= saturate(diff + 0.1);
                col.rgb += spec;

                col.rgb = ClampExposed(BlendDistanceFogExposed(col.rgb, input.positionCS, GetPreExposureMultiplier()));
                // Opaque surface: the texture's alpha isn't coverage, and hosts that composite on the
                // camera colour's alpha would show anything below 1 as see-through.
                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            struct DAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthOnlyVertex(DAttributes input)
            {
                DVaryings output = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthOnlyFragment(DVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // Drawn into _CameraNormalsTexture (SSAO, decals) and by renderer features that collect
        // scene normals by LightMode. DepthNormalsOnly so the Deferred renderer draws it too.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/PackNormalsTexture.hlsl"

            struct NAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            NVaryings DepthNormalsVertex(NAttributes input)
            {
                NVaryings output = (NVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(NVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 output = half4(PackNormalWSToTexture(NormalizeNormalPerPixel(input.normalWS)), 0.0);
            #if defined(_WRITE_SMOOTHNESS)
                output.a = 1.0 - _Roughness;
            #endif
                return output;
            }
            ENDHLSL
        }

        // Lightmap baking only. Without it a static surface bakes as white bounce.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex SimpleMetaVertex
            #pragma fragment SimpleMetaFragment
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct MAttributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
            };

            struct MVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            #ifdef EDITOR_VISUALIZATION
                float2 VizUV      : TEXCOORD1;
                float4 LightCoord : TEXCOORD2;
            #endif
            };

            MVaryings SimpleMetaVertex(MAttributes input)
            {
                MVaryings output = (MVaryings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = TRANSFORM_TEX(input.uv0, _MainTex);
            #ifdef EDITOR_VISUALIZATION
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
            #endif
                return output;
            }

            half4 SimpleMetaFragment(MVaryings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color).rgb;
            #ifdef EDITOR_VISUALIZATION
                metaInput.VizUV = input.VizUV;
                metaInput.LightCoord = input.LightCoord;
            #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
