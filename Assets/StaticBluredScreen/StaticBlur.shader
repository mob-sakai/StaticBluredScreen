Shader "Hidden/StaticBlur"
{
	Properties
	{
		[PerRendererData] _MainTex("Main Texture", 2D) = "white" {}
	}

	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off
		Fog{ Mode off }

		Pass
		{
			Name "Default"

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#pragma shader_feature __ UI_BLUR_FAST UI_BLUR_MEDIUM UI_BLUR_DETAIL
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				float2 texcoord  : TEXCOORD0;
				half4 effectFactor : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			half4 _EffectFactor;


			// Calculate blur effect.
			// Sample texture by blured uv, with bias.
			fixed4 Blur(sampler2D tex, half2 uv, half2 addUv, half bias)
			{
				return 
					(
					tex2D(tex, uv + half2(addUv.x, addUv.y))
					+ tex2D(tex, uv + half2(-addUv.x, addUv.y))
					+ tex2D(tex, uv + half2(addUv.x, -addUv.y))
					+ tex2D(tex, uv + half2(-addUv.x, -addUv.y))
			#if UI_BLUR_DETAIL
					+ tex2D(tex, uv + half2(addUv.x, 0))
					+ tex2D(tex, uv + half2(-addUv.x, 0))
					+ tex2D(tex, uv + half2(0, addUv.y))
					+ tex2D(tex, uv + half2(0, -addUv.y))
					)
					* bias / 2;
			#else
					)
					* bias;
			#endif
			}

			// Sample texture with blurring.
			// * Fast: Sample texture with 3x4 blurring.
			// * Medium: Sample texture with 6x4 blurring.
			// * Detail: Sample texture with 6x8 blurring.
			fixed4 Tex2DBlurring(sampler2D tex, half2 uv, half2 blur)
			{
				half4 color = tex2D(tex, uv);

				#if UI_BLUR_FAST
				return color * 0.41511
					+ Blur( tex, uv, blur * 3, 0.12924 )
					+ Blur( tex, uv, blur * 5, 0.01343 )
					+ Blur( tex, uv, blur * 6, 0.00353 );

				#elif UI_BLUR_MEDIUM | UI_BLUR_DETAIL
				return color * 0.14387
					+ Blur( tex, uv, blur * 1, 0.06781 )
					+ Blur( tex, uv, blur * 2, 0.05791 )
					+ Blur( tex, uv, blur * 3, 0.04360 )
					+ Blur( tex, uv, blur * 4, 0.02773 )
					+ Blur( tex, uv, blur * 5, 0.01343 )
					+ Blur( tex, uv, blur * 6, 0.00353 );
				#else
				return color;
				#endif
			}

			v2f vert(appdata_img v)
			{
				v2f OUT;
				OUT.vertex = UnityObjectToClipPos(v.vertex);
				OUT.texcoord = v.texcoord;
				OUT.effectFactor = _EffectFactor;
				
				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 color = Tex2DBlurring(_MainTex, IN.texcoord, _EffectFactor.z * _MainTex_TexelSize.xy * 2);
				color.a = 1;
				return color;
			}
		ENDCG
		}
	}
}
