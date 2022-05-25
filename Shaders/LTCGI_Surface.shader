Shader "LTCGI/Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [HideInInspector] _LightMap ("(for surface ST only)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _GlossinessMap ("Smoothness Map", 2D) = "white" {}
        [ToggleUI] _MapIsRoughness ("Invert Smoothness Map", Float) = 0.0
        [ToggleUI] _LTCGI ("LTCGI enabled", Float) = 1.0
    }
    SubShader
    {
        // The LTCGI tag can either be "ALWAYS" or specify a "Toggle"/"ToggleUI" property.
        // It is required so that renderers using this material will be updated by the controller.
        Tags { "RenderType"="Opaque" "LTCGI"="_LTCGI" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "LTCGI.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _GlossinessMap;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv2_LightMap;
            float3 worldPos;
            float3 worldNormal; INTERNAL_DATA
        };

        half _Glossiness;
        float _MapIsRoughness;
        half _Metallic;
        fixed4 _Color;
        float _LTCGI;

        float3 get_camera_pos() {
            float3 worldCam;
            worldCam.x = unity_CameraToWorld[0][3];
            worldCam.y = unity_CameraToWorld[1][3];
            worldCam.z = unity_CameraToWorld[2][3];
            return worldCam;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = tex2D (_GlossinessMap, IN.uv_MainTex);
            if (_MapIsRoughness)
                o.Smoothness = 1 - o.Smoothness;
            o.Smoothness *= _Glossiness;
            o.Alpha = c.a;

            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

            if (_LTCGI) {
                float3 normal = WorldNormalVector(IN, o.Normal);
                float3 spec = 0, diff = 0;
                LTCGI_Contribution(
                    IN.worldPos,
                    normalize(normal),
                    normalize(get_camera_pos() - IN.worldPos),
                    1 - o.Smoothness,
                    IN.uv2_LightMap,
                    diff,
                    spec
                );
                o.Emission += spec;
                o.Emission += diff * c;
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
