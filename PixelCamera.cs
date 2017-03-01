using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubjectNerd.Utilities
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	public class PixelCamera : MonoBehaviour
	{
		[SerializeField] protected Camera cam;
		[SerializeField] protected float pixelsPerUnit = 100;
		[SerializeField] protected float zoomLevel = 1f;
		[SerializeField] protected Material cameraMaterial;

		protected RenderTexture renderTexture;

		protected GameObject falseCamGO;
		protected Camera falseCam;
		protected PixelCamDrawer camDraw;

		protected Shader fallbackShader;
		protected Material fallbackMaterial;

		protected int[] lastScreenSize;
		protected float lastZoomLevel;
		protected Vector2 quadOffset;

		public Material CameraMaterial
		{
			get
			{
				Material useMaterial = fallbackMaterial;
				if (cameraMaterial != null)
					useMaterial = cameraMaterial;
				return useMaterial;
			}
		}

		public RenderTexture RenderTexture { get { return renderTexture; } }
		public int[] CameraSize { get { return new int[] {renderTexture.width, renderTexture.height}; } }

		public void GetQuadBounds(out Vector2 min, out Vector2 max)
		{
			min = Vector2.zero - quadOffset;
			max = Vector2.one + quadOffset;
		}

		private void Reset()
		{
			cam = GetComponent<Camera>();
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
			OnDisable();
			OnEnable();
		}

		protected virtual void OnEnable()
		{
			lastScreenSize = new[] {0, 0};
			lastZoomLevel = 0;

			falseCamGO = new GameObject("False Camera") {hideFlags = HideFlags.HideAndDontSave};
			falseCam = falseCamGO.AddComponent<Camera>();
			falseCam.CopyFrom(cam);
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
			if (renderTexture != null)
				DestroyImmediate(renderTexture);
			if (falseCamGO != null)
				DestroyImmediate(falseCamGO);
			falseCam = null;
			ResetCamera();
		}

		protected void SetupCamera()
		{
			zoomLevel = Mathf.Max(0.05f, Mathf.Abs(zoomLevel))*Math.Sign(zoomLevel);
			// "Physical" render size
			Vector2 screenRenderSize = new Vector2(Screen.width, Screen.height);
			screenRenderSize /= zoomLevel;
			// Pixel render size
			int[] pixelRenderSize = GetRenderTextureSize(screenRenderSize);

			// Find the settings to be used for drawing the GL quad
			Vector2 pixelSize = new Vector2(pixelRenderSize[0], pixelRenderSize[1]) * zoomLevel;
			screenRenderSize = new Vector2(Screen.width, Screen.height);
			quadOffset = pixelSize - screenRenderSize;
			quadOffset /= 2;
			quadOffset.x /= Screen.width;
			quadOffset.y /= Screen.height;

			// Set the camera's size, according to pixel size
			float targetHeight = pixelRenderSize[1];
			// Use pixel density to convert to world units
			targetHeight /= pixelsPerUnit;
			targetHeight /= 2f;
			float targetAspect = (float)pixelRenderSize[0] / (float)pixelRenderSize[1];
			// Change orthographic size so camera is sized to world unit 
			cam.orthographicSize = targetHeight;
			cam.aspect = targetAspect;

			cam.targetTexture = null;
			// Create the render texture
			renderTexture = new RenderTexture(pixelRenderSize[0], pixelRenderSize[1], 0)
			{
				useMipMap = true,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
            };
			// Make main camera render into it
			cam.targetTexture = renderTexture;

			// Set render texture as _MainTex on the materials
			SetCameraMaterial(cameraMaterial);
			fallbackMaterial.SetTexture("_MainTex", renderTexture);

			lastScreenSize[0] = Screen.width;
			lastScreenSize[1] = Screen.height;
			lastZoomLevel = zoomLevel;

			cam.Render();
			camDraw.DrawQuad();
		}

		public void SetCameraMaterial(Material cameraMat)
		{
			if (cameraMat == null) return;

			cameraMaterial = cameraMat;
			if (renderTexture != null)
				cameraMaterial.SetTexture("_MainTex", renderTexture);
		}

		public void ForceRefresh()
		{
			lastScreenSize = new[] {0, 0};
		}

		public void CheckCamera()
		{
			bool didChange = Screen.width != lastScreenSize[0] ||
							Screen.height != lastScreenSize[1] ||
							Math.Abs(zoomLevel - lastZoomLevel) > float.Epsilon;
			if (didChange)
				SetupCamera();
		}

		/// <summary>
		/// The integer width and height of the texture to render to
		/// </summary>
		/// <returns></returns>
		protected int[] GetRenderTextureSize(Vector2 size)
		{
			int width = Mathf.FloorToInt(Mathf.Abs(size.x));
			int height = Mathf.FloorToInt(Mathf.Abs(size.y));
			// Size is not integer, add padding
			if (Math.Abs(size.x - width) > float.Epsilon)
				width += 2;
			if (Mathf.Abs(size.y - height) > float.Epsilon)
				height += 2;
			// Make sure this isn't an odd number
			if (width%2 != 0)
				width += 1;
			if (height%2 != 0)
				height += 1;

			width = Mathf.Max(2, width);
			height = Mathf.Max(2, height);
			
			return new[] {width, height};
		}
		
		private void ResetCamera()
		{
			cam.ResetAspect();
			//cam.rect = new Rect(Vector2.zero, Vector2.one);
			//// Set the camera's size, according to pixel size
			//float targetHeight = Screen.height / zoomLevel / zoomLevel;
			//// Use pixel density to convert to world units
			//targetHeight /= pixelsPerUnit;
			//targetHeight /= 2f;
			//cam.orthographicSize = targetHeight;
		}
	}
}