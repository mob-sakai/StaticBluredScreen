using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
#if UNITY_EDITOR
		, ISerializationCallbackReceiver
#endif
	{

	//################################
	// Constant or Static Members.
	//################################
	public const string shaderName = "Hidden/StaticBlur";

	/// <summary>
	/// Blur effect mode.
	/// </summary>
	public enum BlurMode
	{
		None = 0,
		Fast,
		Medium,
		Detail,
	}

	/// <summary>
	/// Desampling rate.
	/// </summary>
	public enum DesamplingRate
	{
		None = 0,
		x1 = 1,
		x2 = 2,
		x4 = 4,
		x8 = 8,
	}


	//################################
	// Serialize Members.
	//################################
	[SerializeField][Range(0, 1)] float m_Blur = 1;
	[SerializeField] BlurMode m_BlurMode = BlurMode.Medium;
	[SerializeField] DesamplingRate m_DesamplingRate = DesamplingRate.x1;
	[SerializeField] DesamplingRate m_ReductionRate = DesamplingRate.x1;
	[SerializeField] FilterMode m_FilterMode = FilterMode.Bilinear;
	[SerializeField] Material m_EffectMaterial;
	[SerializeField][Range(1, 8)] int m_Iterations = 1;
	[SerializeField] bool m_KeepCanvasSize = true;


	//################################
	// Public Members.
	//################################
	/// <summary>
	/// How far is the blurring from the graphic.
	/// </summary>
	public float blur { get { return m_Blur; } set { m_Blur = Mathf.Clamp(value, 0, 4); } }

	/// <summary>
	/// Blur effect mode.
	/// </summary>
	public BlurMode blurMode { get { return m_BlurMode; } set { m_BlurMode = value; } }

	/// <summary>
	/// Effect material.
	/// </summary>
	public virtual Material effectMaterial { get { return m_EffectMaterial; } }

	/// <summary>
	/// Desampling rate of the generated RenderTexture.
	/// </summary>
	public DesamplingRate desamplingRate { get { return m_DesamplingRate; } set { m_DesamplingRate = value; } }

	/// <summary>
	/// Desampling rate of reduction buffer to apply effect.
	/// </summary>
	public DesamplingRate reductionRate { get { return m_ReductionRate; } set { m_ReductionRate = value; } }

	/// <summary>
	/// FilterMode for capture.
	/// </summary>
	public FilterMode filterMode { get { return m_FilterMode; } set { m_FilterMode = value; } }

	/// <summary>
	/// Captured texture.
	/// </summary>
	public RenderTexture capturedTexture { get { return _rt; } }

	/// <summary>
	/// Iterations.
	/// </summary>
	public int iterations { get { return m_Iterations; } set { m_Iterations = value; } }

	/// <summary>
	/// Fits graphic size to the root canvas.
	/// </summary>
	public bool keepCanvasSize { get { return m_KeepCanvasSize; } set { m_KeepCanvasSize = value; } }

	/// <summary>
	/// This function is called when the MonoBehaviour will be destroyed.
	/// </summary>
	protected override void OnDestroy()
	{
		_Release(true);
		base.OnDestroy();
	}

	/// <summary>
	/// Callback function when a UI element needs to generate vertices.
	/// </summary>
	protected override void OnPopulateMesh(VertexHelper vh)
	{
		// When not displaying, clear vertex.
		if (texture == null || color.a < 1 / 255f || canvasRenderer.GetAlpha() < 1 / 255f)
			vh.Clear();
#if UNITY_STANDALONE_WIN
            else if(_capturedIterations % 2 == 0)
            {
                base.OnPopulateMesh(vh);
                int count = vh.currentVertCount;
                UIVertex vt = UIVertex.simpleVert;
                Vector2 one = Vector2.one;
                for (int i = 0; i < count; i++)
                {
                    vh.PopulateUIVertex(ref vt, i);
                    vt.uv0 = new Vector2(vt.uv0.x, 1-vt.uv0.y);
                    vh.SetUIVertex(vt, i);
                }
            }
#endif
			else
			base.OnPopulateMesh(vh);
	}

#if UNITY_EDITOR
	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		var obj = this;
		EditorApplication.delayCall += () =>
		{
			if (Application.isPlaying || !obj)
				return;

			var mat = GetOrGenerateMaterialVariant(Shader.Find(shaderName), blurMode);

			if (m_EffectMaterial == mat)
				return;

			m_EffectMaterial = mat;
			EditorUtility.SetDirty(this);
			EditorApplication.delayCall += AssetDatabase.SaveAssets;
		};
	}

	public static Material GetMaterial(Shader shader, BlurMode blur)
	{
		string variantName = GetVariantName(shader, blur);
		return AssetDatabase.FindAssets("t:Material " + Path.GetFileName(shader.name))
				.Select(x => AssetDatabase.GUIDToAssetPath(x))
				.SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x))
				.OfType<Material>()
				.FirstOrDefault(x => x.name == variantName);
	}


	public static Material GetOrGenerateMaterialVariant(Shader shader, BlurMode blur)
	{
		if (!shader)
			return null;

		Material mat = GetMaterial(shader, blur);

		if (!mat)
		{
			Debug.Log("Generate material : " + GetVariantName(shader, blur));
			mat = new Material(shader);

			if (0 < blur)
				mat.EnableKeyword("UI_BLUR_" + blur.ToString().ToUpper());

			mat.name = GetVariantName(shader, blur);
			mat.hideFlags |= HideFlags.NotEditable;

		#if UIEFFECT_SEPARATE
			bool isMainAsset = true;
			string dir = Path.GetDirectoryName(GetDefaultMaterialPath (shader));
			string materialPath = Path.Combine(Path.Combine(dir, "Separated"), mat.name + ".mat");
		#else
			bool isMainAsset = (0 == blur);
			string materialPath = GetDefaultMaterialPath(shader);
		#endif
			if (isMainAsset)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
				AssetDatabase.CreateAsset(mat, materialPath);
				AssetDatabase.SaveAssets();
			}
			else
			{
				mat.hideFlags |= HideFlags.HideInHierarchy;
				AssetDatabase.AddObjectToAsset(mat, materialPath);
			}
		}
		return mat;
	}

	public static string GetDefaultMaterialPath(Shader shader)
	{
		var name = Path.GetFileName(shader.name);
		return AssetDatabase.FindAssets("t:Material " + name)
				.Select(x => AssetDatabase.GUIDToAssetPath(x))
				.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name)
		?? ("Assets/StaticBluredScreen/" + name + ".mat");
	}

	public static string GetVariantName(Shader shader, BlurMode blur)
	{
		return
		#if UIEFFECT_SEPARATE
		"[Separated] " + Path.GetFileName(shader.name)
		#else
			Path.GetFileName(shader.name)
		#endif
		+ (0 < blur ? "-" + blur : "");
	}
#endif

	/// <summary>
	/// Gets the size of the desampling.
	/// </summary>
	public void GetDesamplingSize(DesamplingRate rate, out int w, out int h)
	{
		var cam = canvas.worldCamera ?? Camera.main;
		h = cam.pixelHeight;
		w = cam.pixelWidth;
		if (rate != DesamplingRate.None)
		{
			h = Mathf.ClosestPowerOfTwo(h / (int)rate);
			w = Mathf.ClosestPowerOfTwo(w / (int)rate);
		}
	}

	/// <summary>
	/// Capture rendering result.
	/// </summary>
	public void Capture()
	{
		// Camera for command buffer.
		_camera = canvas.worldCamera ?? Camera.main;

		// Cache id for RT.
		if (s_CopyId == 0)
		{
			s_CopyId = Shader.PropertyToID("_StaticBluredScreen_ScreenCopyId");
			s_EffectId1 = Shader.PropertyToID("_StaticBluredScreen_EffectId1");
			s_EffectId2 = Shader.PropertyToID("_StaticBluredScreen_EffectId2");
		}

		// If size of generated result RT has changed, relese it.
		int w, h;
		GetDesamplingSize(m_DesamplingRate, out w, out h);
		if (_rt && (_rt.width != w || _rt.height != h))
		{
			_rtToRelease = _rt;
			_rt = null;
		}

		// Generate result RT.
		if (_rt == null)
		{
			_rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
			_rt.filterMode = m_FilterMode;
			_rt.useMipMap = false;
			_rt.wrapMode = TextureWrapMode.Clamp;
			_rt.hideFlags = HideFlags.HideAndDontSave;
		}

		// Create command buffer.
		if (_buffer == null)
		{
			var rtId = new RenderTargetIdentifier(_rt);

			// Material for effect.
			Material mat = effectMaterial;

			_buffer = new CommandBuffer();
			_buffer.name =
					_rt.name =
						mat ? mat.name : "noeffect";

			// Copy to temporary RT.
			_buffer.GetTemporaryRT(s_CopyId, -1, -1, 0, m_FilterMode);
			_buffer.Blit(BuiltinRenderTextureType.CurrentActive, s_CopyId);

			// Set properties.
			_buffer.SetGlobalVector("_EffectFactor", new Vector4(0, 0, blur, 0));

			// Blit without effect.
			if (!mat)
			{
				_buffer.Blit(s_CopyId, rtId);
				_buffer.ReleaseTemporaryRT(s_CopyId);
				_capturedIterations = 1;
			}
				// Blit with effect.
				else
			{
				GetDesamplingSize(m_ReductionRate, out w, out h);
				_buffer.GetTemporaryRT(s_EffectId1, w, h, 0, m_FilterMode);
				_buffer.Blit(s_CopyId, s_EffectId1, mat);    // Apply effect (copied screen -> effect1).
				_buffer.ReleaseTemporaryRT(s_CopyId);
					
				// Iterate the operation.
				if (1 < m_Iterations)
				{
					_buffer.GetTemporaryRT(s_EffectId2, w, h, 0, m_FilterMode);
					for (int i = 1; i < m_Iterations; i++)
					{
						// Apply effect (effect1 -> effect2, or effect2 -> effect1).
						_buffer.Blit(i % 2 == 0 ? s_EffectId2 : s_EffectId1, i % 2 == 0 ? s_EffectId1 : s_EffectId2, mat);
					}
				}

				_buffer.Blit(m_Iterations % 2 == 0 ? s_EffectId2 : s_EffectId1, rtId);
				_buffer.ReleaseTemporaryRT(s_EffectId1);
				if (1 < m_Iterations)
				{
					_buffer.ReleaseTemporaryRT(s_EffectId2);
				}
				_capturedIterations = m_Iterations;
			}
		}

		// Add command buffer to camera.
		_camera.AddCommandBuffer(kCameraEvent, _buffer);

		// StartCoroutine by CanvasScaler.
		var rootCanvas = canvas.rootCanvas;
		var scaler = rootCanvas.GetComponent<CanvasScaler>();
		scaler.StartCoroutine(_CoUpdateTextureOnNextFrame());
		if (m_KeepCanvasSize)
		{
			var size = (rootCanvas.transform as RectTransform).rect.size;
			rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
			rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
		}
	}

	/// <summary>
	/// Release captured image.
	/// </summary>
	public void Release()
	{
		_Release(true);
	}



	//################################
	// Private Members.
	//################################
	const CameraEvent kCameraEvent = CameraEvent.AfterEverything;
	Camera _camera;
	RenderTexture _rt;
	RenderTexture _rtToRelease;
	CommandBuffer _buffer;
	int _capturedIterations = 1;

	static int s_CopyId;
	static int s_EffectId1;
	static int s_EffectId2;

	/// <summary>
	/// Release genarated objects.
	/// </summary>
	/// <param name="releaseRT">If set to <c>true</c> release cached RenderTexture.</param>
	void _Release(bool releaseRT)
	{
		if (releaseRT)
		{
			texture = null;

			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}
		}

		if (_buffer != null)
		{
			if (_camera != null)
				_camera.RemoveCommandBuffer(kCameraEvent, _buffer);
			_buffer.Release();
			_buffer = null;
		}

		if (_rtToRelease)
		{
			_rtToRelease.Release();
			_rtToRelease = null;
		}
	}

	/// <summary>
	/// Set texture on next frame.
	/// </summary>
	IEnumerator _CoUpdateTextureOnNextFrame()
	{
		yield return new WaitForEndOfFrame();

		_Release(false);
		texture = _rt;
	}
}