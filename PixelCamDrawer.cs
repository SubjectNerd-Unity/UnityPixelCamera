using UnityEngine;

namespace SubjectNerd.Utilities
{
	[ExecuteInEditMode]
	[AddComponentMenu("")]
	public class PixelCamDrawer : MonoBehaviour
	{
		/*
		This class sits on a hidden camera that draws nothing, the pixel camera output
		is being redirected to a render texture, so we need a camera context to draw into
		*/
		public PixelCamera SourceCamera { get; set; }
		
		private void OnPostRender()
		{
			if (SourceCamera == null) return;
			DrawQuad();
			SourceCamera.CheckCamera();
		}
		
		public void DrawQuad()
		{
			if (SourceCamera == null || SourceCamera.CameraMaterial == null)
				return;

			Vector2 min = SourceCamera.QuadMin;
			Vector2 max = SourceCamera.QuadMax;

			float zOffset = -0.1f;

			Material renderMat = SourceCamera.CameraMaterial;
			GL.PushMatrix();
			GL.LoadOrtho();
			for (int i = 0; i < renderMat.passCount; i++)
			{
				renderMat.SetPass(i);

				GL.Begin(GL.QUADS);

				GL.TexCoord2(0.0f, 0.0f);
				GL.Vertex3(min.x, min.y, zOffset);
				GL.TexCoord2(0.0f, 1.0f);
				GL.Vertex3(min.x, max.y, zOffset);
				GL.TexCoord2(1.0f, 1.0f);
				GL.Vertex3(max.x, max.y, zOffset);
				GL.TexCoord2(1.0f, 0.0f);
				GL.Vertex3(max.x, min.y, zOffset);

				GL.End();
			}
			GL.PopMatrix();
		}
	}
}