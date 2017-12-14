using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 静的なスクリーンブラーを表示します.
/// ポストエフェクト等によるリアルタイムスクリーンブラーとは異なり、ある時点でのスクリーンショットに対するブラーのみを提供します.
/// 1. ブラー処理用のCameraが不要です.
/// 2. ブラーは常時実行されません. テクスチャ更新を実行したあと1度だけ実行されます.
/// 3. 縮小バッファを利用することで、メモリサイズを小さく抑えます.
/// 4. スクリーン | ブラー | ダイアログ1 | ブラー | ダイアログ2 ... のように、重ねて表示できます.
/// 5. 激しい動きのあるオブジェクトがスクリーン上にある場合、ブラーテクスチャにズレが発生し得ます.
/// </summary>
public class StaticBluredScreen : RawImage
{

#region Serialize

	[SerializeField] Shader m_Shader;

#endregion Serialize

#region Public

	/// <summary>
	/// ブラーテクスチャを更新します
	/// </summary>
	public void UpdateTexture()
	{
		// レンダリングカメラはCanvasのカメラを利用します.
		_camera = canvas.worldCamera ?? Camera.main;

		int h = Mathf.ClosestPowerOfTwo(_camera.pixelHeight / kDesamplingRate);
		int w = Mathf.ClosestPowerOfTwo(_camera.pixelWidth / kDesamplingRate);

		// staticなオブジェクトをキャッシュ.
		if (!s_MaterialBlur)
		{
			s_MaterialBlur = new Material(m_Shader);
			s_MaterialBlur.hideFlags = HideFlags.HideAndDontSave;
			s_CopyId = Shader.PropertyToID("_ScreenCopy");
			s_BlurId = Shader.PropertyToID("_StaticBlur");
		}

		// 出力先RT生成.
		if (_rt == null)
		{
			// 出力先RT生成
			_rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
			_rt.name = kShaderName;
			_rt.filterMode = FilterMode.Bilinear;
			_rt.useMipMap = false;
			_rt.wrapMode = TextureWrapMode.Clamp;
			_rt.hideFlags = HideFlags.HideAndDontSave;
		}

		// コマンドバッファ生成.
		if (_buffer == null)
		{
			// 出力先RTのID生成
			var rtId = new RenderTargetIdentifier(_rt);

			// コマンドバッファ生成
			_buffer = new CommandBuffer();
			_buffer.name = kShaderName;

			// テンポラリRTにスクリーンコピー
			_buffer.GetTemporaryRT(s_CopyId, -1, -1, 0, FilterMode.Bilinear);
			_buffer.Blit(BuiltinRenderTextureType.CurrentActive, s_CopyId);

			// ブラー
			_buffer.GetTemporaryRT(s_BlurId, w / kReductionBufferRate, h / kReductionBufferRate, 0, FilterMode.Bilinear);
			_buffer.Blit(s_CopyId, s_BlurId, s_MaterialBlur);
			_buffer.Blit(s_BlurId, rtId);

			_buffer.ReleaseTemporaryRT(s_BlurId);
			_buffer.ReleaseTemporaryRT(s_CopyId);
		}

		// コマンドバッファをカメラに追加します.
		_camera.AddCommandBuffer(kCameraEvent, _buffer);

		// 1フレーム後の処理を追加します.
		// コルーチン呼び出しの移譲先として、CanvasScalerを取得します.
		// コルーチン呼び出しを以上することで、このオブジェクトが非アクティブな状態でもブラーテクスチャが更新できます.
		canvas.rootCanvas.GetComponent<CanvasScaler>().StartCoroutine(_CoNextFrame(() =>
				{
					_camera.RemoveCommandBuffer(kCameraEvent, _buffer);
					texture = _rt;
					SetMaterialDirty();
				}));
	}

#endregion Public

#region Override

	/// <summary>
	/// This function is called when the MonoBehaviour will be destroyed.
	/// </summary>
	protected override void OnDestroy()
	{
		// 生成したオブジェクトを解放します.
		if (_rt != null)
			_rt.Release();

		if (_buffer != null)
			_buffer.Release();
		
		base.OnDestroy();
	}

#if UNITY_EDITOR
	/// <summary>
	/// Reset to default values.
	/// </summary>
	protected override void Reset()
	{
		// ブラーシェーダを設定します.
		m_Shader = Shader.Find(kShaderName);
		base.Reset();
	}
#endif

	/// <summary>
	/// Callback function when a UI element needs to generate vertices.
	/// </summary>
	protected override void OnPopulateMesh(VertexHelper vh)
	{
		// 非表示状態ならば、vhをクリアし、オーバードローを抑えます.
		if (texture == null || color.a < 1 / 255f || canvasRenderer.GetAlpha() < 1 / 255f)
			vh.Clear();
		else
			base.OnPopulateMesh(vh);
	}

#endregion Override

	/// <summary>
	/// 最終的なブラーテクスチャのデサンプリングレート.
	/// この値が高いほど、処理負荷が小さくなりますが、より角張った仕上がりになります.
	/// 2のべき乗を指定してください.
	/// </summary>
	const int kDesamplingRate = 2;

	/// <summary>
	/// ブラー処理のデサンプリングレート.
	/// この値が高いほど、処理負荷が小さくなりますが、より角張った仕上がりになります.
	/// 2のべき乗を指定してください.
	/// </summary>
	const int kReductionBufferRate = 2;

	const CameraEvent kCameraEvent = CameraEvent.AfterImageEffects;
	const string kShaderName = "Hidden/StaticBlur";

	Camera _camera;
	RenderTexture _rt;
	private CommandBuffer _buffer;

	static Material s_MaterialBlur;
	static int s_CopyId;
	static int s_BlurId;

	/// <summary>
	/// 次フレームでアクションを実行します.
	/// </summary>
	IEnumerator _CoNextFrame(System.Action action)
	{
		yield return new WaitForEndOfFrame();
		action();
	}
}
