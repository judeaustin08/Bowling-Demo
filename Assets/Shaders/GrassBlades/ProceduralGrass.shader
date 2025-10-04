Shader "Grass/ProceduralGrass"
{
	Properties
	{
		_BaseColor("Base Color", Color) = (0, 0, 0, 1)
		_TipColor("Tip Color", Color) = (1, 1, 1, 1)
		_ColorNoise("Color Noise", float) = 0.1
		_ViewDistance("View Distance", float) = 10
		_FadeDistance("Fade Distance", float) = 3
		_TrampleDistance("Trample Distance", float) = 2
		_BaseTex("Base Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
		}
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		HLSLINCLUDE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT

			struct appdata
			{
				uint vertexID : SV_VertexID;
				uint instanceID : SV_InstanceID;
			};

			struct v2f
			{
				float4 positionCS : SV_Position;
				float4 positionWS : TEXCOORD0;
				float2 uv : TEXCOORD1;
				float3 objNoise : float;
			};

			StructuredBuffer<float3> _Positions;
			StructuredBuffer<float3> _Normals;
			StructuredBuffer<float2> _UVs;
			StructuredBuffer<float4x4> _TransformMatrices;
			StructuredBuffer<float3> _GrassNoise;
			float3 _PlayerPosition;

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseColor;
				float4 _TipColor;
				sampler2D _BaseTex;
				float _ColorNoise;
				float4 _BaseTex_ST;
				float _ViewDistance;
				float _FadeDistance;
				float _TrampleDistance;

				float _Cutoff;
			CBUFFER_END
		ENDHLSL

		Pass
		{
			Name "GrassPass"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert alpha
			#pragma fragment frag alpha

			v2f vert(appdata v)
			{
				v2f o;

				float4 positionOS = float4(_Positions[v.vertexID], 1.0f);
				float4x4 objectToWorld = _TransformMatrices[v.instanceID];

				o.positionWS = mul(objectToWorld, positionOS);

				// Calculate shear offset of vertex based on height and distance to player
				_PlayerPosition.y = o.positionWS.y;
				float3 dir = _PlayerPosition - o.positionWS.xyz;
				float dir_mag = length(dir);
				if (dir_mag < _TrampleDistance)
				{
					float shearAmount = 1 - dir_mag / _TrampleDistance;
					positionOS += float4((dir / dir_mag) * positionOS.y * shearAmount, 0);
				}

				o.positionWS = mul(objectToWorld, positionOS);

				o.positionCS = mul(UNITY_MATRIX_VP, o.positionWS);
				o.uv = _UVs[v.vertexID];
				o.objNoise = _GrassNoise[v.instanceID];

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float4 color = tex2D(_BaseTex, i.uv);

//#ifdef _MAIN_LIGHT_SHADOWS
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = i.positionWS;

				float4 shadowCoord = GetShadowCoord(vertexInput);
				float shadowAttenuation = saturate(MainLightRealtimeShadow(shadowCoord) + 0.25f);
				float4 shadowColor = lerp(0.0f, 1.0f, shadowAttenuation);
				color *= shadowColor;
//#endif

				float distanceFromViewer = length(i.positionWS.xyz - _WorldSpaceCameraPos);
				float alpha = (_ViewDistance - distanceFromViewer) / _FadeDistance;
				if (alpha > 1)
					alpha = 1;
				if (alpha < 0)
					alpha = 0;

				float t = i.uv.y - i.objNoise * _ColorNoise;
				color *= lerp(_BaseColor, _TipColor, t);

				return float4(color.rgb, alpha);
			}

            ENDHLSL
        }

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma vertex shadowVert
			#pragma fragment shadowFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			float3 _LightDirection;
			float3 _LightPosition;

			v2f shadowVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				v2f o;

				float4 positionOS = float4(_Positions[vertexID], 1.0f);
				float3 normalOS = _Normals[vertexID];
				float4x4 objectToWorld = _TransformMatrices[instanceID];

				float4 positionWS = mul(objectToWorld, positionOS);
				o.positionCS = mul(UNITY_MATRIX_VP, positionWS);
				o.uv = _UVs[vertexID];

				float3 normalWS = TransformObjectToWorldNormal(normalOS);

				// Code required to account for shadow bias.
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
				float3 lightDirectionWS = _LightDirection;
#endif
				o.positionWS = float4(ApplyShadowBias(positionWS, normalWS, lightDirectionWS), 1.0f);

				return o;
			}

			float4 shadowFrag(v2f i) : SV_Target
			{
				//Alpha(SampleAlbedoAlpha(i.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
				return 0;
			}

			ENDHLSL
		}
    }
	Fallback Off
}
