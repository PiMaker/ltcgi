Shader "LTCGI/Surface (APIv2)"
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
        _LTCGI_DiffuseColor ("LTCGI Diffuse Color", Color) = (1,1,1,1)
        _LTCGI_SpecularColor ("LTCGI Specular Color", Color) = (1,1,1,1)
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

        // This shader demonstrates how to use the APIv2 LTCGI functionality, which has access to per-light callbacks

        // preamble: include this first to get access to required types
        #include "Packages/at.pimaker.ltcgi/Shaders/LTCGI_structs.cginc"

        // then define the accumulator type and callback functions (can forward-declare functions to keep things tidy)
        // note the function signatures, especially that the accumulator is "inout" so it will keep modifications between calls
        struct accumulator_struct {
            // let your imagination run wild on what to accumulate here...
            float3 diffuse;
            float3 specular;
        };
        void callback_diffuse(inout accumulator_struct acc, in ltcgi_output output);
        void callback_specular(inout accumulator_struct acc, in ltcgi_output output);

        // tell LTCGI that we want the V2 API, and which constructs to use
        #define LTCGI_V2_CUSTOM_INPUT accumulator_struct
        #define LTCGI_V2_DIFFUSE_CALLBACK callback_diffuse
        #define LTCGI_V2_SPECULAR_CALLBACK callback_specular

        // then include this to finish the deal
        #include "Packages/at.pimaker.ltcgi/Shaders/LTCGI.cginc"

        // standard shader stuff follows...
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
        half4 _Color;
        float _LTCGI;
        half4 _LTCGI_DiffuseColor;
        half4 _LTCGI_SpecularColor;

        float3 get_camera_pos() {
            float3 worldCam;
            worldCam.x = unity_CameraToWorld[0][3];
            worldCam.y = unity_CameraToWorld[1][3];
            worldCam.z = unity_CameraToWorld[2][3];
            return worldCam;
        }

        // now we declare LTCGI APIv2 functions for real
        void callback_diffuse(inout accumulator_struct acc, in ltcgi_output output) {
            // you can do whatever here! check out the ltcgi_output struct in
            // "LTCGI_structs.cginc" to see what data you have available
            acc.diffuse += output.intensity * output.color * _LTCGI_DiffuseColor;
        }
        void callback_specular(inout accumulator_struct acc, in ltcgi_output output) {
            // same here, this example one is pretty boring though.
            // you could accumulate intensity separately for example,
            // to emulate total{Specular,Diffuse}Intensity from APIv1
            acc.specular += output.intensity * output.color * _LTCGI_SpecularColor;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // standard surface shader stuff again...
            fixed4 mainColor = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = mainColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = tex2D (_GlossinessMap, IN.uv_MainTex);
            if (_MapIsRoughness)
                o.Smoothness = 1.0f - o.Smoothness;
            o.Smoothness *= _Glossiness;
            o.Alpha = mainColor.a;

            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            float3 worldSpaceNormal = normalize(WorldNormalVector(IN, o.Normal));

            if (_LTCGI) {
                // and lastly, here's how we call the APIv2 version of LTCGI:

                // first, we create the struct that'll be passed through
                accumulator_struct acc = (accumulator_struct)0;

                // then we make the LTCGI_Contribution call as usual, but with slightly different params
                LTCGI_Contribution(
                    acc, // our accumulator
                    IN.worldPos, // world position of the shaded point
                    worldSpaceNormal, // world space normal
                    normalize(get_camera_pos() - IN.worldPos), // view vector to shaded point, normalized
                    1.0f - o.Smoothness, // roughness
                    IN.uv2_LightMap // shadowmap coordinates (the normal Unity ones, they should be in sync with LTCGI maps)
                );

                // after the call, our accumulator struct will have been modified by our callbacks
                // we can now use it to set the output color
                o.Emission += acc.specular;
                o.Emission += acc.diffuse * mainColor.rgb;
            }
        }
        ENDCG
    }

    FallBack "Diffuse"
}