Shader "LTCGI/Simple"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0)

        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.5

        //[ToggleUI] _Debug ("Debug Param, does whatever it wants", float) = 0.0
    }
    SubShader
    {
        // "LTCGI" tag defines that any renderer with this material will
        // receive data from the LTCGI editor and Udon scripts.
        // Can be either "ALWAYS" or the name of a float ("ToggleUI")
        // property on which input will depends.
        Tags { "RenderType"="Opaque" "LTCGI"="ALWAYS" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            //#define LTCGI_SPECULAR_OFF

            #include "UnityCG.cginc"
            #include "LTCGI.cginc"

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNorm : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;
            float _Roughness;

            v2f vert (appdata_full v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.worldNorm = mul(unity_ObjectToWorld, float4(v.normal.xyz, 0.0)).xyz;
                o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uv.zw = v.texcoord1;
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
                //return half4(tex2Dlod(_LTCGI_mat, float4(i.uv, 0, 0)).rgb, 1);

                half4 col = tex2D(_MainTex, i.uv) * _Color;
                half3 diff = 0;
                half3 spec = 0;
                
                LTCGI_Contribution(
                    i.worldPos,
                    normalize(i.worldNorm),
                    normalize(camera_pos - i.worldPos),
                    _Roughness,
                    i.uv.zw,
                    diff,
                    spec
                );
                col.rgb *= saturate(diff + 0.1);
                col.rgb += spec;
                return col;
            }
            ENDCG
        }
    }
}
