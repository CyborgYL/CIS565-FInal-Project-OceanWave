Shader "Ocean/Ocean_BRDF" 
{
	Properties{
		sky_map("SkyMap", 2D) = "sky_map"{}
		sky_color("SkyColor", Color) = (.34, .85, .92, 1)
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
			#define Rg 6360000.0
			#define Rt 6420000.0
			#define RL 6421000.0
			
			uniform sampler2D trans;
			uniform float3 earth_pos;
			uniform float3 sun_dir;
			uniform float intensity;
			uniform sampler2D map, slop_map0, slop_map1;
			uniform sampler2D _Map3, _Map4;	
			uniform sampler2D _Foam0, _Foam1;		
			uniform float4 size;
			uniform float3 sea_color;
			uniform float LOD_num;
			uniform float far;
			uniform sampler2D sky_map;
			uniform sampler3D variance_map; 
			uniform float2 max_var;
			uniform float fresnel_on;
			uniform float refl_on = 1.0f;
			uniform float refr_on = 1.0f;
			uniform float white_on = 1.0f;
			uniform float sun_on = 1.0f;
			uniform float _WhiteCapStr;
			uniform float4 _Choppyness;
			uniform float4 sky_color : Color;
		
			struct VS_IN 
			{
    			float4  pos : SV_POSITION;
    			float3 pos_w : TEXCOORD;
			};

			VS_IN vert(appdata_base v)
			{
				float3 world_pos = mul(_Object2World, v.vertex).xyz;
			
				float dist = clamp(distance(_WorldSpaceCameraPos.xyz, world_pos) / far, 0.0, 1.0);			
				v.vertex.y += tex2Dlod(map, float4(world_pos.xz/size.x, 0, LOD_num * dist)).x;
				v.vertex.y += tex2Dlod(map, float4(world_pos.xz/size.y, 0, LOD_num * dist)).y;
			
				v.vertex.xz += tex2Dlod(_Map3, float4(world_pos.xz/size.x, 0, LOD_num)).xy * _Choppyness.x;
				v.vertex.xz += tex2Dlod(_Map3, float4(world_pos.xz/size.y, 0, LOD_num)).zw * _Choppyness.y;
				v.vertex.xz += tex2Dlod(_Map4, float4(world_pos.xz/size.z, 0, LOD_num)).xy * _Choppyness.z;
				v.vertex.xz += tex2Dlod(_Map4, float4(world_pos.xz/size.w, 0, LOD_num)).zw * _Choppyness.w;
				
    			VS_IN OUT;
    			OUT.pos_w = mul(_Object2World, v.vertex).xyz;
    			OUT.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    			//OUT.dist = clamp(dist * 5.0, 0.0, 1.0);
    			return OUT;
			}
			
			
			float Fresnel(float3 V, float3 N, float2 sigma) 
			{
			    float sigmaV2 = dot(V.xz * V.xz / (1.0 - V.y * V.y), sigma);
			    float sigmaV = sqrt(sigmaV2);
			    return pow(1.0 - dot(V, N), 5.0 * exp(-2.69 * sigmaV)) / (1.0 + 22.7 * pow(sigmaV, 1.5));
			}
			
			float3 hdr(float3 L)
			{
			    L = L * 0.4;
			    L.r = L.r < 1.413 ? pow(L.r * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.r);
			    L.g = L.g < 1.413 ? pow(L.g * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.g);
			    L.b = L.b < 1.413 ? pow(L.b * 0.38317, 1.0 / 2.2) : 1.0 - exp(-L.b);
			    return L;
			}
			
			float2 U(float2 zeta, float3 V, float3 N, float3 Tx, float3 Ty) 
			{
			    float3 f = normalize(float3(-zeta, 1.0)); 
			    float3 F = f.x * Tx + f.y * Ty + f.z * N; 
			    float3 R = 2.0 * dot(F, V) * F - V;
			    return R.xz / (1.0 + R.y);
			}
			
			
			float Lambda(float cosTheta, float sigmaSq) 
			{
				float v = cosTheta / sqrt((1.0 - cosTheta * cosTheta) * (2.0 * sigmaSq));
				float erfc = 2.0 * exp(-v * v) / (2.319 * v + sqrt(4.0 + 1.52 * v * v));
			    return max((exp(-v * v) - v * sqrt(3.14159) * erfc) / (2.0 * v * sqrt(3.14159)),0);
			}
			
			float erf(float x) 
			{
				float a  = 0.140012;
				float x2 = x*x;
				float ax2 = a*x2;
				return sign(x) * sqrt( 1.0 - exp(-x2*(4.0/3.14159 + ax2)/(1.0 + ax2)) );
			}
			
			float whitecapCoverage(float epsilon, float mu, float sigma2) {
				return 0.5*erf((0.5*sqrt(2.0)*(epsilon-mu)*(1.0/sqrt(sigma2)))) + 0.5;
			}
			
			float4 frag(VS_IN IN) : COLOR
			{
				float2 uv = IN.pos_w.xz;
				
				//get slope
				float2 slope = float2(0,0);
				slope += tex2D(slop_map0,uv/size.x).xy;
				slope += tex2D(slop_map0,uv/size.y).zw;
				slope += tex2D(slop_map1,uv/size.z).xy;
				slope += tex2D(slop_map1,uv/size.w).zw;
				slope = -slope;
			
				//compute sigma
			    float xx = ddx(uv.x);
			    float xy = ddy(uv.x);
			    float yx = ddx(uv.y);
			    float yy = ddy(uv.y);
			    float ua = pow((xx * xx + yx * yx) / 12.0, 0.25);
			    float ub = 0.5 + 0.5 * xx * xy + yx * yy / sqrt((xx * xx + yx * yx) * (xy * xy + yy * yy));
			    float uc = pow((xy * xy + yy * yy) / 12.0, 0.25);
			    float2 sigmaSq = tex3D(variance_map, float3(ua, ub, uc)).xy * max_var;
			    
				//view dir and surface normal
			    float3 V = normalize(_WorldSpaceCameraPos-IN.pos_w);
			    float3 N = normalize(float3(slope.x, 1.0, slope.y));
			    			    			    
			    //compute fresnel
			    float fresnel = Fresnel(V, N, sigmaSq);	    

			    //tangent space
			    float3 Ty = normalize(float3(0.0, N.z, -N.y));
			    float3 Tx = cross(Ty, N);
			    float3 col = float3(0,0,0);
			    
			    float3 worldV = normalize(_WorldSpaceCameraPos + earth_pos);
				float r = length(_WorldSpaceCameraPos + earth_pos);
			    float muS = dot(worldV, sun_dir);
			    float3 t;
				if(muS < -sqrt(1.0 - (Rg / r) * (Rg / r)))
					t = float3(0,0,0);
				else
				{
				    float uR, uMu;
					uR = sqrt((r - Rg) / (Rt - Rg));
					uMu = atan((muS + 0.15) / (1.0 + 0.15) * tan(1.5)) / 1.5;
					    
					t= tex2D(trans, float2(uMu, uR)).rgb;
				}
				    
				    float3 Lsun = t * intensity;
			    //sun light
			    if(sun_on==1.0){
			    
				    
				    float3 tmp = normalize(sun_dir + V);
				    float zx = dot(tmp, Tx) / dot(tmp, N);
				    zx = zx * zx;
				    float zy = dot(tmp, Ty) / dot(tmp, N);
				    zy = zy * zy;
				
				    float zL = max(dot(sun_dir, N),0.01);
				    float zV = max(dot(V, N),0.01);  
				    float tanV = atan2(dot(V, Ty), dot(V, Tx));
				    float cosV2 = 1.0 / (1.0 + tanV * tanV);
				    float sigmaV2 = sigmaSq.x * cosV2 + sigmaSq.y * (1.0 - cosV2);		
				    float tanL = atan2(dot(sun_dir, Ty), dot(sun_dir, Tx));
				    float cosL2 = 1.0 / (1.0 + tanL * tanL);
				    float sigmaL2 = sigmaSq.x * cosL2 + sigmaSq.y * (1.0 - cosL2);
				
				    float f = pow(1.0 - dot(V, tmp), 5.0);   
				    float p = exp(-0.5 * ( zx/ sigmaSq.x +  zy/ sigmaSq.y)) / (2.0 * 3.14159 * sqrt(sigmaSq.x * sigmaSq.y));
				    float sun_radiance = f * p / ((0.5 + Lambda(zL, sigmaL2) + Lambda(zV, sigmaV2)) * zV * pow(dot(tmp, N),4) );
				    col += max(sun_radiance,0) * Lsun;
			    }
			    

			    //reflection of sky lighting
			    if(refl_on==1.0){

				    float2 u0 = U(float2(0,0), V, N, Tx, Ty);
				    float2 dux = 2.0 * (U(float2(0.001, 0.0), V, N, Tx, Ty) - u0) / 0.001 * sqrt(sigmaSq.x);
				    float2 duy = 2.0 * (U(float2(0.0, 0.001), V, N, Tx, Ty) - u0) / 0.001 * sqrt(sigmaSq.y);
				
				    col += tex2D(sky_map, u0 * 0.45+0.5, dux *0.45, duy *0.45).rgb * sky_color.rgb * fresnel * 2.0;
				    //col = hdr(col);
					//return float4(col,1.0);
			    }
			    
			    
			    //refraction of seawater color
			    if(refr_on==1.0)
    				col +=  sea_color * 10.0 / 3.14159 * (1.0 - fresnel);
    			
    			if(fresnel_on==1.0)
    				col =  float3(fresnel,fresnel,fresnel);
    			
    			
    			if(white_on==1.0){
	    			// extract mean and variance of the jacobian matrix determinant
					float2 jm1 = tex2D(_Foam0, uv/size.x).xy;
					float2 jm2 = tex2D(_Foam0, uv/size.y).zw;
					float2 jm3 = tex2D(_Foam1, uv/size.z).xy;
					float2 jm4 = tex2D(_Foam1, uv/size.w).zw;
					float2 jm  = (jm1+jm2+jm3+jm4);
					float jSigma2 = max((jm.y - (jm1.x*jm1.x + jm2.x*jm2.x + jm3.x*jm3.x + jm4.x*jm4.x)), 0.0);
					
	    			// get coverage
					float W = max(0,whitecapCoverage(_WhiteCapStr,jm.x,jSigma2));
					//W *= 1 - IN.dist;
					// compute and add whitecap radiance
					float3 l = (100.0 * (max(dot(N, sun_dir), 0.0))) / 3.14159;
					float3 R_ftot = float3(W * l * 0.4);
					col += R_ftot;
				}
    			col = hdr(col);
    			
    			//return float4(IN.dist,IN.dist,IN.dist,1.0);
				return float4(col,1.0);
			}
			
			ENDCG

    	}
	}
}