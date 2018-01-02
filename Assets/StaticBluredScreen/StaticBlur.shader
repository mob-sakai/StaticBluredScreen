Shader "Hidden/StaticBlur"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	
	Subshader
	{
		Pass
		{
			ZTest Always
			Cull Off
			ZWrite Off
			Fog { Mode off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"
			
			sampler2D _MainTex;

			v2f_img vert(appdata_img v)
			{
				v2f_img o;
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);

				#if UNITY_UV_STARTS_AT_TOP
				o.uv = half2(v.texcoord.x, 1 - v.texcoord.y);
				#else
				o.uv = v.texcoord;
				#endif

				return o;
			}

			// 8方向ブラー.
			fixed4 _blur8(sampler2D tex, half2 uv, half addUv)
			{
				return (
						tex2D( tex, uv + half2(addUv, 0))
						+ tex2D( tex, uv + half2(-addUv, 0))
						+ tex2D( tex, uv + half2(0, addUv))
						+ tex2D( tex, uv + half2(0, -addUv))
						+ tex2D( tex, uv + half2(addUv, addUv))
						+ tex2D( tex, uv + half2(addUv, -addUv))
						+ tex2D( tex, uv + half2(-addUv, addUv))
						+ tex2D( tex, uv + half2(-addUv, -addUv))
					)/8;
			}
			
			fixed4 frag (v2f_img i) : COLOR {
				fixed4 color = tex2D(_MainTex, i.uv) * 0.14387
					+ _blur8( _MainTex, i.uv, 0.005 * 1) * 0.27124
					+ _blur8( _MainTex, i.uv, 0.005 * 2) * 0.23164
					+ _blur8( _MainTex, i.uv, 0.005 * 3) * 0.17440
					+ _blur8( _MainTex, i.uv, 0.005 * 4) * 0.11092
					+ _blur8( _MainTex, i.uv, 0.005 * 5) * 0.06784;
				color.a = 1;
				return color;
			}
			ENDCG
		}
	}
	Fallback off
}