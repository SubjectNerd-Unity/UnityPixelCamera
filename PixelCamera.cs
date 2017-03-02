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
			public Material cameraMaterial;
			public Vector2 aspectStretch = Vector2.one;
			public float perspectiveZ = 10;
		}

		protected struct CamSettings
		{
			public int[] screenSize;
			public Vector2 aspect;
			public float zoomLevel;
			public float fieldOfView;
			public bool  isOrtho;

			public CamSettings(Vector2 aspect, float zoomLevel, float fieldOfView, bool isOrtho)
			{
				screenSize = new[] {Screen.width, Screen.height};
				this.aspect = aspect;
				this.zoomLevel = zoomLevel;
				this.fieldOfView = fieldOfView;
				this.isOrtho = isOrtho;
			}

			public bool Equals(CamSettings other)
			{
				bool equalScreen = other.screenSize[0] == screenSize[0] &&
				                   other.screenSize[1] == screenSize[1];
				bool equalAspect = other.aspect == aspect;
				bool equalFoV = Math.Abs(other.fieldOfView - fieldOfView) <= float.Epsilon;
				bool equalZoom = Math.Abs(other.zoomLevel - zoomLevel) <= float.Epsilon;
				bool isEqual = equalScreen && equalAspect && equalFoV && equalZoom;
				//if (!isEqual)
				//{
				//	Debug.LogFormat("scr {0}, asp {1}, fov {2}, zoom {3}", equalScreen, equalAspect, equalFoV, equalZoom);
				//	Debug.LogFormat("Aspect: {0}, Other: {1}", aspect, other.aspect);
				//}
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

		public float PerspectiveZ
		{
			get
			{
				if (advancedSettings == null)
					return cam.farClipPlane*0.5f;
				return advancedSettings.perspectiveZ;
			}
			set
			{
				if (advancedSettings == null)
					return;
				advancedSettings.perspectiveZ = value;
			}
		}

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

		public RenderTexture RenderTexture { get { return renderTexture; } }
		public int[] CameraSize { get { return new int[] {renderTexture.width, renderTexture.height}; } }

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
			lastSettings = new CamSettings(AspectStretch, 0, cam.fieldOfView, cam.orthographic);

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

				float zDistance = PerspectiveZ;

				var frustumHeight = 2.0f * zDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
				var frustumWidth = frustumHeight* cam.aspect;

				screenRenderSize.x = frustumWidth;
				screenRenderSize.y = frustumHeight;
				screenRenderSize *= pixelsPerUnit;
			}

			return screenRenderSize;
		}
		
		protected void SetupCamera()
		{
			var aspect = AspectStretch;

			zoomLevel = Mathf.Max(0.05f, Mathf.Abs(zoomLevel))*Math.Sign(zoomLevel);
			// "Physical" pixel render size
			Vector2 screenRenderSize = GetScreenRenderSize();
			// Pixel render size
			int[] pixelRenderSize = GetRenderTextureSize(screenRenderSize, aspect);

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
			Vector2 pixelSize = new Vector2(pixelRenderSize[0], pixelRenderSize[1]) * zoomLevel;
			quadOffset = pixelSize - screenRenderSize;
			quadOffset /= 2;
			quadOffset.x /= Screen.width;
			quadOffset.y /= Screen.height;
			
			// Important to release current render texture
			cam.targetTexture = null;
			fallbackMaterial.SetTexture("_MainTex", null);
			if (advancedSettings != null && advancedSettings.cameraMaterial != null)
				advancedSettings.cameraMaterial.SetTexture("_MainTex", null);
			if (renderTexture != null)
				renderTexture.Release();

			// Create new render texture
			renderTexture = new RenderTexture(pixelRenderSize[0], pixelRenderSize[1], 0)
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
			
			lastSettings = new CamSettings(aspect, zoomLevel, cam.fieldOfView, cam.orthographic);

			cam.Render();
			camDraw.DrawQuad();
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
			width = Mathf.Max(2, width);
			height = Mathf.Max(2, height);
			
			return new[] {width, height};
		}

		public void GetQuadBounds(out Vector2 min, out Vector2 max)
		{
			min = Vector2.zero - quadOffset;
			max = Vector2.one + quadOffset;
			if (advancedSettings == null)
				return;

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
		}

		public void ForceRefresh()
		{
			lastSettings.screenSize = new [] {0, 0};
		}

		public void CheckCamera()
		{
			var currentSettings = new CamSettings(AspectStretch, zoomLevel, cam.fieldOfView, cam.orthographic);
			bool didChange = currentSettings.Equals(lastSettings) == false;
			if (didChange)
				SetupCamera();
		}
	}
}