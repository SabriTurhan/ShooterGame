using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Watermelon.SquadShooter
{
    /// <summary>
    /// WebGL-only performance profile. Browsers drive the frame loop with requestAnimationFrame, so the frame
    /// rate is left uncapped (a fixed targetFrameRate switches Unity to setTimeout and stutters on Safari).
    /// Mobile browsers also get a lighter shadow setup and an adaptive render scale that trades resolution for
    /// a steady frame rate. The pipeline asset is only changed at runtime in the player, never in the editor.
    /// </summary>
    public class WebPerformance : MonoBehaviour
    {
        private const int MOBILE_SHADOWMAP_RESOLUTION = 2048;
        private const float MOBILE_SHADOW_DISTANCE = 50f;

        private const float MIN_RENDER_SCALE = 0.6f;
        private const float MAX_RENDER_SCALE = 0.9f;
        private const float SCALE_DOWN_STEP = 0.1f;
        private const float SCALE_UP_STEP = 0.05f;

        private const float SAMPLE_WINDOW = 2f;
        private const float LOW_FPS = 48f;
        private const float HIGH_FPS = 57f;
        private const int GOOD_WINDOWS_BEFORE_SCALE_UP = 3;
        private const float HITCH_TIME = 0.25f;

        private UniversalRenderPipelineAsset pipeline;

        private float windowTime;
        private int windowFrames;
        private bool windowHitched;
        private int goodWindows;

#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Application.targetFrameRate = -1;

            if (!Application.isMobilePlatform) return;

            GameObject host = new GameObject("WebPerformance");
            DontDestroyOnLoad(host);
            host.AddComponent<WebPerformance>();
        }
#endif

        private void Awake()
        {
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                pipeline.mainLightShadowmapResolution = Mathf.Min(pipeline.mainLightShadowmapResolution, MOBILE_SHADOWMAP_RESOLUTION);
                pipeline.shadowDistance = Mathf.Min(pipeline.shadowDistance, MOBILE_SHADOW_DISTANCE);
                pipeline.renderScale = Mathf.Min(pipeline.renderScale, MAX_RENDER_SCALE);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Soft shadows filter every shadowed pixel with several taps; hard shadows are much cheaper on mobile GPUs
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.shadows == LightShadows.Soft) light.shadows = LightShadows.Hard;
            }
        }

        private void Update()
        {
            if (pipeline == null) return;

            float delta = Time.unscaledDeltaTime;
            if (delta > HITCH_TIME) windowHitched = true;

            windowTime += delta;
            windowFrames++;

            if (windowTime < SAMPLE_WINDOW) return;

            float fps = windowFrames / windowTime;
            bool hitched = windowHitched;

            windowTime = 0f;
            windowFrames = 0;
            windowHitched = false;

            // Loading spikes, ads and hidden tabs are not a rendering cost
            if (hitched) return;

            if (fps < LOW_FPS)
            {
                goodWindows = 0;
                if (pipeline.renderScale > MIN_RENDER_SCALE + 0.001f)
                    pipeline.renderScale = Mathf.Max(MIN_RENDER_SCALE, pipeline.renderScale - SCALE_DOWN_STEP);
            }
            else if (fps > HIGH_FPS)
            {
                goodWindows++;
                if (goodWindows >= GOOD_WINDOWS_BEFORE_SCALE_UP && pipeline.renderScale < MAX_RENDER_SCALE - 0.001f)
                {
                    goodWindows = 0;
                    pipeline.renderScale = Mathf.Min(MAX_RENDER_SCALE, pipeline.renderScale + SCALE_UP_STEP);
                }
            }
            else
            {
                goodWindows = 0;
            }
        }
    }
}
