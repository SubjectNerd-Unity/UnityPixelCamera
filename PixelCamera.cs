using System;
using UnityEngine;

namespace SubjectNerd.Utilities
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	public class PixelCamera : MonoBehaviour
	{
		[Serializable]
		protected class AdvancedSettings
		{
			[Tooltip("Material to draw output render with")]
			public Material cameraMaterial;
			[Tooltip("Stretches output display, for non square pixels")]
			public Vector2 aspectStretch = Vector2.one;
			[Tooltip("Scales down camera render size")]
			public float downSample = 1;
			[Tooltip("Z distance to draw as pixel perfect for perspective camera.")]
			public float perspectiveZ = 10;
		}

		protected struct CamSettings
		{
			public int[] screenSize;
			public Vector2 aspect;
			public float zoomLevel;
			public float pixelsPerUnit;
			public float zDistance;
			public float downsample;
			public float fieldOfView;
			public float farPlane;
			public bool  isOrtho;

			public CamSettings(PixelCamera pixelCam, Camera cam)
			{
				screenSize = new[] {Screen.width, Screen.height};
				this.aspect = pixelCam.AspectStretch;
				this.zoomLevel = pixelCam.ZoomLevel;
				this.pixelsPerUnit = pixelCam.pixelsPerUnit;
				this.zDistance = pixelCam.PerspectiveZ;
				this.downsample = pixelCam.DownSample;
				this.fieldOfView = cam.fieldOfView;
				this.isOrtho = cam.orthographic;
				this.farPlane = cam.farClipPlane;
			}

			public bool Equals(CamSettings other)
			{
				bool equalScreen = other.screenSize[0] == screenSize[0] &&
				                   other.screenSize[1] == screenSize[1];
				bool equalAspect = other.aspect == aspect;
				
				bool isEqual = other.isOrtho == isOrtho &&
				               equalScreen &&
							   equalAspect &&
				               Mathf.Approximately(other.zoomLevel, zoomLevel) &&
				               Mathf.Approximately(other.pixelsPerUnit, pixelsPerUnit) &&
							   Mathf.Approximately(other.downsample, downsample);

				if (isEqual && isOrtho == false)
				{
					isEqual &= Mathf.Approximately(other.zDistance, zDistance) &&
					           Mathf.Approximately(other.fieldOfView, fieldOfView) &&
					           Mathf.Approximately(other.farPlane, farPlane);
				}

				return isEqual;
			}
		}

		[SerializeField] protected Camera cam;
		[SerializeField] protected float pixelsPerUnit = 100;
		[SerializeField] protected float zoomLevel = 1f;
		[Space]
		[SerializeField] protected AdvancedSettings advancedSettings;

		protected RenderTexture renderTexture;

		protected GameObject falseCamGO;
		protected Camera falseCam;
		protected PixelCamDrawer camDraw;

		protected Shader fallbackShader;
		protected Material fallbackMaterial;
		
		protected CamSettings lastSettings;

		protected Vector2 quadOffset;
		public Vector2 QuadMin { get; protected set; }
		public Vector2 QuadMax { get; protected set; }

		/// <summary>
		/// Material to draw output render with
		/// </summary>
		public Material CameraMaterial
		{
			get
			{
				Material useMaterial = fallbackMaterial;
				if (advancedSettings != null && advancedSettings.cameraMaterial != null)
					useMaterial = advancedSettings.cameraMaterial;
				return useMaterial;
			}
			set
			{
				if (advancedSettings.cameraMaterial != null)
					advancedSettings.cameraMaterial.SetTexture("_MainTex", null);

				advancedSettings.cameraMaterial = value;
				if (advancedSettings.cameraMaterial == null) return;

				if (renderTexture != null)
					advancedSettings.cameraMaterial.SetTexture("_MainTex", renderTexture);
			}
		}

		public float ZoomLevel
		{
			get { return zoomLevel; }
			set { zoomLevel = value; }
		}

		public float PixelsPerUnit
		{
			get { return pixelsPerUnit; }
			set { pixelsPerUnit = value; }
		}

		/// <summary>
		/// For perspective cameras. Z distance between near and far plane to scale as pixel perfect.
		/// </summary>
		public float PerspectiveZ
		{
			get
			{
				if (advancedSettings == null)
					return cam.farClipPlane*0.5f;
				advancedSettings.perspectiveZ = Mathf.Clamp(advancedSettings.perspectiveZ, cam.nearClipPlane, cam.farClipPlane);
                return advancedSettings.perspectiveZ;
			}
			set
			{
				if (advancedSettings == null)
					return;
				advancedSettings.perspectiveZ = Mathf.Clamp(value, cam.nearClipPlane, cam.farClipPlane); ;
			}
		}

		/// <summary>
		/// Stretches output display, for non square pixels
		/// </summary>
		public Vector2 AspectStretch
		{
			get
			{
				if (advancedSettings == null)
					return Vector2.one;
				return advancedSettings.aspectStretch;
			}
			set
			{
				if (advancedSettings == null)
					return;
				advancedSettings.aspectStretch = value;
			}
		}

		/// <summary>
		/// Scales down camera render size. Clamped at minimum value of 1.
		/// </summary>
		public float DownSample
		{
			get
			{
				if (advancedSettings == null)
					return 1f;
				advancedSettings.downSample = Mathf.Max(1f, advancedSettings.downSample);
				return advancedSettings.downSample;
			}
			set
			{
				if (advancedSettings == null)
					return;
				advancedSettings.downSample = Mathf.Max(1f, value);
			}
		}

		/// <summary>
		/// The render texture camera is being drawn into
		/// </summary>
		public RenderTexture RenderTexture { get { return renderTexture; } }

		/// <summary>
		/// Pixel size of the camera
		/// </summary>
		public int[] CameraSize
		{
			get
			{
				if (renderTexture == null)
					return new[] {0, 0};

				return new[] {renderTexture.width, renderTexture.height};
			}
		}

		private void Reset()
		{
			cam = GetComponent<Camera>();
			float cameraPixelHeight = Mathf.FloorToInt(cam.aspect*2*pixelsPerUnit);
			zoomLevel = Screen.height/cameraPixelHeight;
		}

		private void Start()
		{
			// Disable if we don't support image effects
			if (!SystemInfo.supportsImageEffects)
				enabled = false;
			
			if (cam == null)
				cam = GetComponent<Camera>();
			if (cam == null)
				enabled = false;

			OnDisable(); // Force cleanup
			if (enabled)
				OnEnable();
		}

		protected virtual void OnEnable()
		{
			lastSettings = new CamSettings(this, cam)
			{
				screenSize = new []{0, 0}
			};
			ForceRefresh();

			falseCamGO = new GameObject("False Camera") {hideFlags = HideFlags.HideAndDontSave};
			falseCam = falseCamGO.AddComponent<Camera>();
			falseCam.cullingMask = LayerMask.GetMask();

			camDraw = falseCamGO.AddComponent<PixelCamDrawer>();
			camDraw.SourceCamera = this;
			
			fallbackShader = Shader.Find("Hidden/SubjectNerd/PixelCamFallback");
			if (fallbackShader != null)
			{
				fallbackMaterial = new Material(fallbackShader)
				{
					hideFlags = HideFlags.DontSave
				};
			}
			else
			{
				Debug.Log("Couldn't find fall back shader, material not created");
				enabled = false;
			}
		}

		protected virtual void OnDisable()
		{
			if (fallbackMaterial != null)
				DestroyImmediate(fallbackMaterial);
			fallbackShader = null;
			cam.targetTexture = null;
			cam.ResetAspect();
			if (renderTexture != null)
				DestroyImmediate(renderTexture);
			if (falseCamGO != null)
				DestroyImmediate(falseCamGO);
			falseCam = null;
		}

		private void SetupCamera(CamSettings settings)
		{
			var aspect = settings.aspect;

			zoomLevel = Mathf.Max(0.05f, Mathf.Abs(zoomLevel))*Math.Sign(zoomLevel);
			// "Physical" pixel render size
			Vector2 screenRenderSize = GetScreenRenderSize();
			// Pixel render size
			int[] pixelRenderSize = GetRenderTextureSize(screenRenderSize, settings.aspect);

			float targetAspect = (float)pixelRenderSize[0] / (float)pixelRenderSize[1];
			cam.aspect = targetAspect;

			if (cam.orthographic)
			{
				// Orthographic camera needs to use screen size when calculating quad offset
				screenRenderSize = new Vector2(Screen.width, Screen.height);

				// Set the camera's size, according to pixel size
				float targetHeight = pixelRenderSize[1];
				// Use pixel density to convert to world units
				targetHeight /= pixelsPerUnit;
				targetHeight /= 2f;
				// Change orthographic size so camera is sized to world unit 
				cam.orthographicSize = targetHeight;
			}

			// Find the settings to be used for drawing the GL quad
			CalculateQuad(screenRenderSize, pixelRenderSize);
			
			// Important to release current render texture
			cam.targetTexture = null;
			fallbackMaterial.SetTexture("_MainTex", null);
			if (advancedSettings != null && advancedSettings.cameraMaterial != null)
				advancedSettings.cameraMaterial.SetTexture("_MainTex", null);
			if (renderTexture != null)
				renderTexture.Release();

			// Create new render texture
			Vector2 renderSize = new Vector2(pixelRenderSize[0], pixelRenderSize[1]) / settings.downsample;
			int[] actualRenderSize = GetRenderTextureSize(renderSize, Vector2.one);
			renderTexture = new RenderTexture(actualRenderSize[0], actualRenderSize[1], 0)
			{
				useMipMap = true,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
            };
			// Make main camera render into it
			cam.targetTexture = renderTexture;

			// Set render texture as _MainTex on materials
			fallbackMaterial.SetTexture("_MainTex", renderTexture);
			if (advancedSettings != null)
				CameraMaterial = advancedSettings.cameraMaterial;

			lastSettings = settings;

			cam.Render();
			camDraw.DrawQuad();
		}

		private float GetPerspectiveHeight(float z)
		{
			var frustumHeight = 2.0f * z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
			return frustumHeight;
		}

		protected Vector2 GetScreenRenderSize()
		{
			// For orthographic camera, physical render size is based on screen pixels
			Vector2 screenRenderSize = new Vector2(Screen.width, Screen.height);
			screenRenderSize /= zoomLevel;

			// For perspective camera, physical render is based on world unit height
			// in terms of fustrum distance, converted to pixels
			if (cam.orthographic == false)
			{
				cam.aspect = (float) Screen.width / Screen.height;

				float scale = Mathf.InverseLerp(cam.nearClipPlane, cam.farClipPlane, PerspectiveZ);
				float maxHeight = GetPerspectiveHeight(cam.farClipPlane);
				float minHeight = GetPerspectiveHeight(cam.nearClipPlane);

				float height = Mathf.Lerp(minHeight, maxHeight, scale);
				float width = height*cam.aspect;

				screenRenderSize.x = width;
				screenRenderSize.y = height;
				screenRenderSize *= pixelsPerUnit;
			}

			return screenRenderSize;
		}

		/// <summary>
		/// The integer width and height of the texture to render to
		/// </summary>
		/// <returns></returns>
		protected int[] GetRenderTextureSize(Vector2 size, Vector2 aspect)
		{
			int width = Mathf.FloorToInt(Mathf.Abs(size.x / aspect.x));
			int height = Mathf.FloorToInt(Mathf.Abs(size.y / aspect.y));
			
			// Size is not integer, add padding
			if (Math.Abs(size.x - width) > float.Epsilon)
				width += 2;
			if (Mathf.Abs(size.y - height) > float.Epsilon)
				height += 2;
			// Make sure this isn't an odd number
			if (width%2 > 0)
				width += 1;
			if (height%2 > 0)
				height += 1;

			// Just in case
			width = Mathf.Clamp(width, 2, 4096);
			height = Mathf.Clamp(height, 2, 4096);
			
			return new[] {width, height};
		}

		private void CalculateQuad(Vector2 screenRenderSize, int[] pixelRenderSize)
		{
			Vector2 pixelSize = new Vector2(pixelRenderSize[0], pixelRenderSize[1]) * zoomLevel;
			quadOffset = pixelSize - screenRenderSize;
			quadOffset /= 2;
			quadOffset.x /= Screen.width;
			quadOffset.y /= Screen.height;

			Vector2 min = Vector2.zero - quadOffset;
			Vector2 max = Vector2.one + quadOffset;
			if (advancedSettings == null)
			{
				QuadMin = min;
				QuadMax = max;
				return;
			}

			Vector2 aspectStretch = advancedSettings.aspectStretch;
			if (aspectStretch.x < float.Epsilon || aspectStretch.y < float.Epsilon)
				return;

			Vector2 center = (min + max) / 2;
			min -= center;
			max -= center;

			min.x *= aspectStretch.x;
			max.x *= aspectStretch.x;

			min.y *= aspectStretch.y;
			max.y *= aspectStretch.y;

			min += center;
			max += center;

			QuadMin = min;
			QuadMax = max;
		}

		public void ForceRefresh()
		{
			lastSettings.screenSize = new [] {0, 0};
		}

		public bool CheckCamera()
		{
			var currentSettings = new CamSettings(this, cam);
			bool didChange = currentSettings.Equals(lastSettings) == false;
			if (didChange)
				SetupCamera(currentSettings);
			return didChange;
		}
	}
}