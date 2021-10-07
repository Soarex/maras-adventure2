// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Standard Shader" {
	Properties{
		_Tint("Tint", Color) = (1, 1, 1, 1)
		_MainTex("Texture", 2D) = "white" {}
	}

	SubShader{

		Pass {
			Tags {
				"LightMode" = "ForwardBase"
			}

			CGPROGRAM

			#pragma vertex MyVertexProgram
			#pragma fragment MyFragmentProgram

			#include "UnityStandardBRDF.cginc"

			float4 _Tint;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct VertexData {
				float4 position : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct Interpolators {
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
			};

			Interpolators MyVertexProgram(VertexData v) {
				Interpolators i;
				i.uv = TRANSFORM_TEX(v.uv, _MainTex);
				i.position = UnityObjectToClipPos(v.position);
				i.normal = UnityObjectToWorldNormal(v.normal);
				i.normal = normalize(i.normal);
				return i;
			}

			float4 MyFragmentProgram(Interpolators i) : SV_TARGET {
				float3 lightDir = _WorldSpaceLightPos0.xyz;
				i.normal = normalize(i.normal);
				return DotClamped(lightDir, i.normal);
			}

			ENDCG
		}
	}
}