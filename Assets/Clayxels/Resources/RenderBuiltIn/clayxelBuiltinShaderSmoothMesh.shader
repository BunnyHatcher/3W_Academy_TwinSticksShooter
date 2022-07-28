// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Clayxels/ClayxelBuiltInShaderSmoothMesh"
{
	Properties {
		_Smoothness ("Smoothness", Range(0.0,1.0)) = 0.5
		_Metallic ("Metallic", Range(0.0,1.0)) = 0.0
		[HDR]_Emission ("Emission", Color) = (0, 0, 0, 0)
	}
	SubShader
	{
		Tags { "Queue" = "Geometry" "RenderType"="Opaque" }

		ZWrite On
		Cull Back
		
		CGPROGRAM

		#pragma surface surf Standard vertex:vert addshadow fullforwardshadows
		#pragma target 4.5
		
		#include "../clayxelSRPUtils.cginc"

		float _Smoothness;
		float _Metallic;
		float4 _Emission;
		
		struct VertexData{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 color : COLOR;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			uint vid : SV_VertexID;
		};

		struct Input
		{
			float4 color : COLOR;
		};

		void vert(inout VertexData outVertex, out Input outData){
			UNITY_INITIALIZE_OUTPUT(Input, outData);

			clayxelVertSmoothMesh(outVertex.vid, outVertex.vertex.xyz, outVertex.normal, outVertex.color.xyz);
			
			outVertex.color.w = 1.0;
			outVertex.vertex.w = 1.0;
		}

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = IN.color;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Emission = _Emission;
		}

		ENDCG
	}
}