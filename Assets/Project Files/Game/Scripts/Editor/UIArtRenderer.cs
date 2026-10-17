using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Watermelon.SquadShooter
{
    // Renders menu art (weapon card icons, character portraits, world previews) in the style of the template:
    // icons get a dark rim plus a thick white outline, portraits fade out at the bottom, locked portraits are
    // black silhouettes. Everything is rendered in an isolated preview scene with the game's lighting.
    public class UIArtRenderer : IDisposable
    {
        public const int SIZE = 512;

        private readonly Scene scene;
        private readonly Camera camera;
        private readonly RenderTexture target;

        public UIArtRenderer(Color background)
        {
            scene = EditorSceneManager.NewPreviewScene();

            GameObject lightObject = new GameObject("Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            SceneManager.MoveGameObjectToScene(lightObject, scene);

            GameObject cameraObject = new GameObject("Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            // rendered at twice the size and filtered down instead of MSAA (MSAA breaks preview-scene renders in URP)
            target = new RenderTexture(SIZE * 2, SIZE * 2, 24, RenderTextureFormat.ARGB32);
        }

        public GameObject Spawn(GameObject prefab)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) renderer.enabled = false;

            return instance;
        }

        public void Add(GameObject gameObject) => SceneManager.MoveGameObjectToScene(gameObject, scene);

        // frames the renderers of `subject` from `direction`, orthographic or perspective
        public Texture2D Render(GameObject subject, Vector3 direction, float padding, bool orthographic, float fieldOfView = 30f, Vector3? lookOffset = null)
        {
            Bounds bounds = BoundsOf(subject);
            Vector3 center = bounds.center + (lookOffset ?? Vector3.zero);
            float radius = bounds.extents.magnitude * padding;

            camera.orthographic = orthographic;
            camera.orthographicSize = radius;
            camera.fieldOfView = fieldOfView;
            float distance = orthographic ? radius * 4f : radius / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = center - direction.normalized * distance;
            camera.transform.LookAt(center);

            return Capture();
        }

        public Texture2D RenderFrom(Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);

            return Capture();
        }

        private Texture2D Capture()
        {
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;

            RenderTexture small = RenderTexture.GetTemporary(SIZE, SIZE, 0, RenderTextureFormat.ARGB32);
            target.filterMode = FilterMode.Bilinear;
            Graphics.Blit(target, small);

            RenderTexture.active = small;
            Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(small);

            return texture;
        }

        public void Dispose()
        {
            target.Release();
            EditorSceneManager.ClosePreviewScene(scene);
        }

        public static Bounds BoundsOf(GameObject subject)
        {
            Bounds bounds = new Bounds(subject.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        #region Post effects
        // dark rim right at the silhouette and a thick white outline around it (weapon card icons)
        public static Texture2D Outline(Texture2D source, int rim, int outline, Color rimColor, Color outlineColor)
        {
            int size = source.width;
            Color[] pixels = source.GetPixels();
            float[] distance = DistanceToShape(pixels, size, rim + outline + 1);

            Color[] result = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color colour = pixels[i];
                float d = distance[i];

                Color under = d <= rim ? rimColor : d <= rim + outline ? outlineColor : Color.clear;
                if (d > rim && d <= rim + outline) under.a = Mathf.Clamp01(rim + outline + 0.5f - d);

                result[i] = Color.Lerp(under, new Color(colour.r, colour.g, colour.b, 1f), colour.a);
                result[i].a = Mathf.Max(colour.a, under.a);
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(result);
            texture.Apply();
            return texture;
        }

        public static Texture2D FadeBottom(Texture2D source, float fadeFraction)
        {
            Color[] pixels = source.GetPixels();
            int size = source.width;
            for (int y = 0; y < size; y++)
            {
                float alpha = Mathf.Clamp01(y / (size * fadeFraction));
                for (int x = 0; x < size; x++) pixels[y * size + x].a *= alpha;
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D Silhouette(Texture2D source)
        {
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, pixels[i].a);

            Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // brute-force distance (in pixels) to the nearest opaque pixel, capped at maxDistance
        private static float[] DistanceToShape(Color[] pixels, int size, int maxDistance)
        {
            float[] distance = new float[pixels.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = pixels[i].a > 0.5f ? 0f : float.MaxValue;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a <= 0.5f) continue;

                    // only edge pixels can be the nearest opaque pixel of a transparent one
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++)
                    {
                        int ex = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ey = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        edge = ex < 0 || ey < 0 || ex >= size || ey >= size || pixels[ey * size + ex].a <= 0.5f;
                    }
                    if (!edge) continue;

                    for (int dy = -maxDistance; dy <= maxDistance; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= size) continue;
                        for (int dx = -maxDistance; dx <= maxDistance; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= size) continue;
                            int index = ny * size + nx;
                            if (distance[index] == 0f) continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < distance[index]) distance[index] = d;
                        }
                    }
                }
            }

            return distance;
        }
        #endregion

        // writes a PNG next to an existing sprite and copies that sprite's import settings
        public static Sprite SaveSprite(Texture2D texture, string path, string importTemplate)
        {
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            TextureImporter source = (TextureImporter)AssetImporter.GetAtPath(importTemplate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterSettings settings = new TextureImporterSettings();
            source.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            foreach (string platform in new[] { "Android", "iPhone", "Standalone" })
                importer.SetPlatformTextureSettings(source.GetPlatformTextureSettings(platform));
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
