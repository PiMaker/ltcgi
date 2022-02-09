Shader "LTCGI/Avatar Projector"
{
    Properties
    {
        _Intensity ("Itensity", Range(0.0, 100.0)) = 1.0
        _X ("X", Float) = 0.0006
        _Y ("Y", Float) = 50
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="Transparent" }

        Pass
        {
            AlphaToMask On
            Blend Zero One // blend away into nothingness

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 5.0

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
                float4 worldDirection : TEXCOORD2;
            };

            uniform float _X, _Y;

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            #define PM UNITY_MATRIX_P
            inline float4 CalculateFrustumCorrection()
            {
                float x1 = -PM._31/(PM._11*PM._34);
                float x2 = -PM._32/(PM._22*PM._34);
                return float4(x1, x2, 0, PM._33/PM._34 + x1*PM._13 + x2*PM._23);
            }
            inline float CorrectedLinearEyeDepth(float z, float B)
            {
                return 1.0 / (z/PM._34 + B);
            }
            #undef PM

            v2f vert (appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;

                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.worldDirection.xyz = o.worldPos.xyz - _WorldSpaceCameraPos;
                // pack correction factor into direction w component to save space
                o.worldDirection.w = dot(o.vertex, CalculateFrustumCorrection());

                return o;
            }

            float3 get_camera_pos() {
                float3 worldCam;
                worldCam.x = unity_CameraToWorld[0][3];
                worldCam.y = unity_CameraToWorld[1][3];
                worldCam.z = unity_CameraToWorld[2][3];
                return worldCam;
            }
            static float3 camera_pos = get_camera_pos();

            half4 frag (v2f i) : SV_Target
            {
                // don't render on transparent surfaces (that don't write to depth)
                // by alpha-testing them away using A2C
                float perspectiveDivide = 1.0f / i.vertex.w;
                float4 direction = i.worldDirection * perspectiveDivide;
                float2 screenpos = i.grabPos.xy * perspectiveDivide;
                screenpos.y = _ProjectionParams.x * .5 + .5 - screenpos.y * _ProjectionParams.x;
                float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenpos);
                float depth = CorrectedLinearEyeDepth(z, direction.w);
                float3 worldpos = direction * depth + _WorldSpaceCameraPos.xyz;
                return half4(0, 0, 0, distance(worldpos, i.worldPos) > _X ? 0 : saturate(distance(worldpos, i.worldPos)*_Y));
            }
            ENDCG
        }

        Pass
        {
            AlphaToMask Off
            Blend DstColor One, Zero One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 5.0

            //#define LTCGI_SPECULAR_OFF
            #define LTCGI_ALWAYS_LTC_DIFFUSE

            #include "UnityCG.cginc"
            #include "../../Shaders/LTCGI.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNorm : TEXCOORD1;
                float4 grabPos : TEXCOORD2;
                float4 worldDirection : TEXCOORD3;
            };

            uniform float _Intensity;
            uniform float _X, _Y;

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            #define PM UNITY_MATRIX_P
            inline float4 CalculateFrustumCorrection()
            {
                float x1 = -PM._31/(PM._11*PM._34);
                float x2 = -PM._32/(PM._22*PM._34);
                return float4(x1, x2, 0, PM._33/PM._34 + x1*PM._13 + x2*PM._23);
            }
            inline float CorrectedLinearEyeDepth(float z, float B)
            {
                return 1.0 / (z/PM._34 + B);
            }
            #undef PM

            float getRawDepth(float2 uv) { return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0.0, 0.0)); }

            // inspired by keijiro's depth inverse projection
            // https://github.com/keijiro/DepthInverseProjection
            // constructs view space ray at the far clip plane from the screen uv
            // then multiplies that ray by the linear 01 depth
            float3 viewSpacePosAtScreenUV(float2 uv)
            {
                float rawDepth = getRawDepth(uv);
                #ifdef UNITY_SINGLE_PASS_STEREO
                    uv -= unity_StereoScaleOffset[unity_StereoEyeIndex].zw;
                    uv /= unity_StereoScaleOffset[unity_StereoEyeIndex].xy;
                #endif
                float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z);
                return viewSpaceRay * Linear01Depth(rawDepth);
            }
            float3 viewSpacePosAtPixelPosition(float2 vpos)
            {
                float2 uv = vpos * _CameraDepthTexture_TexelSize.xy;
                return viewSpacePosAtScreenUV(uv);
            }

            half3 viewNormalAtPixelPosition(float2 uv)
            {
                // current pixel's depth
                float c = getRawDepth(uv);

                // get current pixel's view space position
                half3 viewSpacePos_c = viewSpacePosAtScreenUV(uv);

                // get view space position at 1 pixel offsets in each major direction
                half3 viewSpacePos_l = viewSpacePosAtScreenUV(uv + float2(-1.0, 0.0) * _CameraDepthTexture_TexelSize.xy);
                half3 viewSpacePos_r = viewSpacePosAtScreenUV(uv + float2( 1.0, 0.0) * _CameraDepthTexture_TexelSize.xy);
                half3 viewSpacePos_d = viewSpacePosAtScreenUV(uv + float2( 0.0,-1.0) * _CameraDepthTexture_TexelSize.xy);
                half3 viewSpacePos_u = viewSpacePosAtScreenUV(uv + float2( 0.0, 1.0) * _CameraDepthTexture_TexelSize.xy);

                // get the difference between the current and each offset position
                half3 l = viewSpacePos_c - viewSpacePos_l;
                half3 r = viewSpacePos_r - viewSpacePos_c;
                half3 d = viewSpacePos_c - viewSpacePos_d;
                half3 u = viewSpacePos_u - viewSpacePos_c;

                // get depth values at 1 & 2 pixels offsets from current along the horizontal axis
                half4 H = half4(
                    getRawDepth(uv + float2(-1.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2( 1.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2(-2.0, 0.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2( 2.0, 0.0) * _CameraDepthTexture_TexelSize.xy)
                );

                // get depth values at 1 & 2 pixels offsets from current along the vertical axis
                half4 V = half4(
                    getRawDepth(uv + float2(0.0,-1.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2(0.0, 1.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2(0.0,-2.0) * _CameraDepthTexture_TexelSize.xy),
                    getRawDepth(uv + float2(0.0, 2.0) * _CameraDepthTexture_TexelSize.xy)
                );

                // current pixel's depth difference from slope of offset depth samples
                // differs from original article because we're using non-linear depth values
                // see article's comments
                half2 he = abs((2 * H.xy - H.zw) - c);
                half2 ve = abs((2 * V.xy - V.zw) - c);

                // pick horizontal and vertical diff with the smallest depth difference from slopes
                half3 hDeriv = he.x < he.y ? l : r;
                half3 vDeriv = ve.x < ve.y ? d : u;

                // get view space normal from the cross product of the best derivatives
                half3 viewNormal = normalize(cross(hDeriv, vDeriv));

                return viewNormal;
            }

            v2f vert (appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.worldNorm = mul(unity_ObjectToWorld, float4(v.normal.xyz, 0.0)).xyz;

                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.worldDirection.xyz = o.worldPos.xyz - _WorldSpaceCameraPos;
                // pack correction factor into direction w component to save space
                o.worldDirection.w = dot(o.vertex, CalculateFrustumCorrection());

                return o;
            }

            float3 get_camera_pos() {
                float3 worldCam;
                worldCam.x = unity_CameraToWorld[0][3];
                worldCam.y = unity_CameraToWorld[1][3];
                worldCam.z = unity_CameraToWorld[2][3];
                return worldCam;
            }
            static float3 camera_pos = get_camera_pos();

            half4 frag (v2f i) : SV_Target
            {
                float perspectiveDivide = 1.0f / i.vertex.w;
                float4 direction = i.worldDirection * perspectiveDivide;
                float2 screenpos = i.grabPos.xy * perspectiveDivide;
                screenpos.y = _ProjectionParams.x * .5 + .5 - screenpos.y * _ProjectionParams.x;
                float z = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(screenpos, 0, 0));
                float depth = CorrectedLinearEyeDepth(z, direction.w);
                float3 worldpos = direction * depth + _WorldSpaceCameraPos.xyz;

                // don't render on transparent surfaces (that don't write to depth)
                if (distance(worldpos, i.worldPos) > _X)
                    discard;

                // get view space normal at the current pixel position
                half3 viewNormal = viewNormalAtPixelPosition(screenpos);
                // transform normal from view space to world space
                half3 worldNormal = mul((float3x3)unity_MatrixInvV, viewNormal);

                half4 col = half4(0, 0, 0, 1);
                half3 diff = 0;
                half3 spec = 0;

                LTCGI_Contribution(
                    worldpos,
                    normalize(i.worldNorm),
                    normalize(camera_pos - worldpos),
                    1.0, // roughness
                    (float2)0,
                    diff,
                    spec
                );

                col.rgb += spec * _Intensity * 1.5;
                col.rgb += diff * _Intensity * 0.8;
                return col;
            }
            ENDCG
        }
    }
}
