Shader "wireframe" 
{
	Properties
	{
		frame_color("Color", Color) = (1,1,1,1)
		map("WaveMap", 2D) ="WaveMap"
	}
	
	SubShader 
	{
    	Pass 
    	{		
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma glsl

			uniform sampler2D map;
			uniform float4 size;
			uniform float far;
			uniform float LOD_num; 		
			
			float4 frame_color;
		
			struct VS_IN
			{
    			float4  pos : SV_POSITION;
			};


			VS_IN vert(appdata_base v)
			{
				float3 worldPos = mul(_Object2World, v.vertex).xyz;
			
				float dist = clamp(distance(_WorldSpaceCameraPos.xyz, worldPos) / far, 0.0, 1.0);
				float lod = LOD_num * dist;
	
				v.vertex.y += tex2Dlod(map, float4(worldPos.xz/size.x, 0, lod)).x;
				v.vertex.y += tex2Dlod(map, float4(worldPos.xz/size.y, 0, lod)).y;
			
    			VS_IN vs_out;
    			vs_out.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    			return vs_out;
			}
			
			float4 frag(VS_IN IN) : COLOR
			{
				return frame_color;
			}
			
			ENDCG

    	}
	}
}