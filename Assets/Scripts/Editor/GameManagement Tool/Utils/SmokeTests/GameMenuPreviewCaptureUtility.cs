using System.IO;
using UnityEngine;

/// <summary>
/// Captures authored menu canvases in batch smoke tests where no Game view is available for screenshots.
/// </summary>
internal static class GameMenuPreviewCaptureUtility
{
    #region Methods

    #region Capture
    /// <summary>
    /// Renders the existing canvas through a temporary camera and restores its original presentation settings.
    /// </summary>
    /// <param name="canvas">Authored menu canvas being verified in Play Mode.</param>
    /// <param name="filePath">Output PNG path for visual inspection.</param>
    public static void Capture(Canvas canvas, string filePath)
    {
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        RenderTexture previousTarget = RenderTexture.active;
        GameObject cameraObject = new GameObject("MenuSmokeCaptureCamera", typeof(Camera));
        RenderTexture target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        Texture2D frame = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);

        try
        {
            // Route only the existing canvas into an isolated target; no UI objects are generated.
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            frame.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            frame.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)));
            File.WriteAllBytes(filePath, frame.EncodeToPNG());
        }
        finally
        {
            // Restore the live canvas before the test continues with its input assertions.
            RenderTexture.active = previousTarget;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            UnityEngine.Object.DestroyImmediate(frame);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            Canvas.ForceUpdateCanvases();
        }
    }
    #endregion

    #endregion
}
