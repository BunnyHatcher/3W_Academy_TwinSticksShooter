
// this shader is only used for PolySplats and SmoothMesh rendering. MicrovoxelSplats don't need any extra shader for picking.

Shader "Clayxels/ClayxelPickingShaderPolySplat" {
	SubShader {
		Tags { "Queue" = "Geometry" "RenderType"="Opaque" }

		Pass {
			Lighting Off

			ZWrite On     
			
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			
			#include "clayxelSRPUtils.cginc"
			
			struct VertexData{
				float4 pos: POSITION;
				nointerpolation float2 pickId: TEXCOORD1;
			};

			struct FragData{
				fixed4 selection: SV_TARGET;
			};

			VertexData vert(uint id : SV_VertexID){
				VertexData outVertex = (VertexData)0;

				pickPolySplat(id, outVertex.pos, outVertex.pickId);
				
				return outVertex;
			}

			FragData frag(VertexData inVertex){
				FragData outData;
				outData.selection = float4(unpackRgb(uint(inVertex.pickId.x)), inVertex.pickId.y);
				
				return outData;
			}

			ENDCG
		}
	}
}