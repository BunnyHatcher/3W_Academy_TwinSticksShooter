
// pretty much a standard unity shader but it reads colored vertices from the mesh
Shader "Clayxels/ClayxelBuiltInMeshShader" {
    Properties {
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR]_Emission ("Emission", Color) = (0, 0, 0, 0)
        [Toggle(_CLAYXELS_MESHSHADER_SPLATS)] _EnableSplats ("Enable Splats", Range(0.0, 1.0)) = 0.0
        _ClayxelSize ("Clayxel Size", Range(0.1, 20.0)) = 1.0
        _NormalOrient ("Normal Orient", Range(0.0, 1.0)) = 1.0
        _Twist ("Twist", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset]_MainTex ("Texture", 2D) = "defaulttexture" {}
        _Cutoff ("Cutoff", Range(0.0, 1.0)) = 0.95
        _RoughColor ("RoughColor", Range(0.0, 1.0)) = 0.0
        _RoughBump ("RoughBump", Range(0.0, 1.0)) = 0.0
        _RoughOrient ("RoughOrient", Range(0.0, 1.0)) = 0.0
        _RoughTwist ("RoughTwist", Range(0.0, 1.0)) = 0.0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows alphatest:_Cutoff
        #pragma target 4.5

        #include "../clayxelSRPUtils.cginc"

        #pragma shader_feature_local _ _CLAYXELS_MESHSHADER_SPLATS

        float _Smoothness;
        float _Metallic;
        float4 _Emission;
        float _ClayxelSize;
        float _NormalOrient;
        sampler2D _MainTex;
        float _EnableSplats;
        float _Twist;
        float _RoughTwist;
        
        struct VertexData{
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
            float4 color : COLOR;
            float4 texcoord1 : TEXCOORD0;
            float4 texcoord2 : TEXCOORD1;
            float4 texcoord3 : TEXCOORD2;
            float4 texcoord4 : TEXCOORD3;
            uint vid : SV_VertexID;
        };
 
        struct Input {
            float4 vertexColor : COLOR;
            float2 tex : TEXCOORD0;
        };

        void vert(inout VertexData outVertex, out Input outData){
            UNITY_INITIALIZE_OUTPUT(Input, outData);
            
            #if defined(_CLAYXELS_MESHSHADER_SPLATS)
                float3 triangleCenter = float3(outVertex.texcoord3.xy, outVertex.texcoord4.x);
                float distToCenter = outVertex.texcoord4.y;
                bool hdrpCameraRelative = false;

                clayxelsMeshToSplats(
                    outVertex.vid, 
                    hdrpCameraRelative,
                    unity_ObjectToWorld,
                    unity_WorldToObject,
                    outVertex.vertex.xyz,
                    outVertex.normal,
                    outVertex.color.xyz,
                    triangleCenter,
                    distToCenter,
                    _ClayxelSize, 
                    _NormalOrient,
                    _Twist,
                    _RoughColor,
                    _RoughBump,
                    _RoughOrient,
                    _RoughTwist,
                     outVertex.vertex.xyz,
                     outVertex.color.xyz,
                     outData.tex);
            #endif
        }
        
        void surf (Input IN, inout SurfaceOutputStandard o) {
            float alpha = 1.0;
            
            #if defined(_CLAYXELS_MESHSHADER_SPLATS)
                alpha = tex2D(_MainTex, IN.tex).a;
            #endif

            o.Alpha = alpha;

            o.Albedo = IN.vertexColor.xyz;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = _Emission;
        }
        ENDCG
    }
    FallBack "Standard"
}
