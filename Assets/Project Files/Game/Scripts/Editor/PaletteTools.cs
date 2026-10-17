using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Watermelon.SquadShooter.Examples;

namespace Watermelon.SquadShooter
{
    // Re-dyes template meshes into the shared character palette (player_atlas.png) and attaches palette-style
    // accessories to bones. Used by the enemy/character builders so new content matches the art style.
    public static class PaletteTools
    {
        public const string PLAYER_MATERIAL = "Assets/Project Files/Game/Materials/Characters/Player.mat";
        public const string PLAYER_ATLAS = "Assets/Project Files/Game/Textures/Characters/Player/player_atlas.png";

        private static Texture2D palette;
        private static readonly Dictionary<Texture, Texture2D> readable = new Dictionary<Texture, Texture2D>();

        public static Material PlayerMaterial => AssetDatabase.LoadAssetAtPath<Material>(PLAYER_MATERIAL);

        // colour classes of a source texel, used by repaint rules
        public struct Texel
        {
            public Color Colour;
            public float Hue, Saturation, Value;
            public string Bone;
            public int NearestBand;

            public bool IsDark => Value < 0.3f;
            public bool IsGrey => Saturation < 0.15f && !IsDark;
            public bool IsSkin => Hue < 0.14f && Saturation > 0.12f && Value > 0.72f;
            public bool IsGreen => Saturation > 0.3f && Hue > 0.2f && Hue < 0.5f;
            public bool IsWarm => Saturation > 0.3f && (Hue < 0.12f || Hue > 0.9f);
            public bool OnHead => Bone.Contains("Head");
            public bool OnTorso => Bone.Contains("Spine") || Bone.EndsWith("Hips");
            public bool OnLegs => Bone.Contains("Leg") || Bone.Contains("Foot") || Bone.Contains("Toe");
            public bool OnArms => Bone.Contains("Arm") || Bone.Contains("Hand") || Bone.Contains("Shoulder");
        }

        // re-maps a mesh textured with any atlas onto the player palette; `rule` returns the target band
        // (or -1 to keep the nearest palette colour); the brightness of the source texel picks the shade
        public static Mesh Repaint(Mesh source, Transform[] bones, Texture texture, Func<Texel, int> rule, string name)
        {
            Mesh mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = name;

            Texture2D atlas = Readable(texture);
            Texture2D swatches = Palette();
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;

            for (int i = 0; i < uv.Length; i++)
            {
                Color colour = atlas.GetPixelBilinear(uv[i].x, uv[i].y);
                Color.RGBToHSV(colour, out float h, out float s, out float v);
                NearestSwatch(swatches, colour, out int band, out float shade);

                Texel texel = new Texel
                {
                    Colour = colour, Hue = h, Saturation = s, Value = v, NearestBand = band,
                    Bone = bones != null && weights.Length > i && bones[weights[i].boneIndex0] != null ? bones[weights[i].boneIndex0].name : string.Empty,
                };

                int target = rule(texel);
                if (target < 0) uv[i] = Band.UV(band, shade);
                else uv[i] = Band.UV(target, target == band ? shade : Mathf.Clamp(1f - colour.grayscale, 0.05f, 0.85f));
            }

            mesh.uv = uv;
            return mesh;
        }

        public static Texture2D Palette()
        {
            if (palette == null) palette = Readable(AssetDatabase.LoadAssetAtPath<Texture2D>(PLAYER_ATLAS));
            return palette;
        }

        public static Texture2D Readable(Texture texture)
        {
            if (readable.TryGetValue(texture, out Texture2D cached) && cached != null) return cached;

            RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            readable[texture] = copy;
            return copy;
        }

        private static void NearestSwatch(Texture2D swatches, Color colour, out int bestBand, out float bestShade)
        {
            bestBand = 0;
            bestShade = 0f;
            float bestDistance = float.MaxValue;

            for (int band = 0; band <= Band.DeepIndigo; band++)
            {
                for (int step = 0; step <= 10; step++)
                {
                    float shade = step / 10f;
                    Vector2 uv = Band.UV(band, shade);
                    Color swatch = swatches.GetPixelBilinear(uv.x, uv.y);
                    float distance = (swatch.r - colour.r) * (swatch.r - colour.r) + (swatch.g - colour.g) * (swatch.g - colour.g) + (swatch.b - colour.b) * (swatch.b - colour.b);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestBand = band;
                        bestShade = shade;
                    }
                }
            }
        }

        // world-space bounds of the vertices weighted to the bones that pass `filter`, in the current pose
        public static Bounds BoneBounds(SkinnedMeshRenderer renderer, Func<string, bool> filter)
        {
            Mesh baked = new Mesh();
            renderer.BakeMesh(baked, true);

            Vector3[] vertices = baked.vertices;
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            Matrix4x4 toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);

            // some rigs carry scaled bones, so the baked mesh can be off by a factor: calibrate it against the
            // renderer's world bounds, which Unity computes correctly
            Bounds all = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) all.Encapsulate(toWorld.MultiplyPoint3x4(vertices[i]));
            renderer.ResetBounds();
            Bounds world = renderer.bounds;
            float scale = all.size.y > 1e-5f ? world.size.y / all.size.y : 1f;
            if (Mathf.Abs(scale - 1f) < 0.05f) scale = 1f;
            Vector3 shift = scale == 1f ? Vector3.zero : world.center - all.center * scale;

            bool initialised = false;
            Bounds bounds = new Bounds(renderer.transform.position, Vector3.zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                Transform bone = renderer.bones[weights[i].boneIndex0];
                if (bone == null || !filter(bone.name)) continue;

                Vector3 point = toWorld.MultiplyPoint3x4(vertices[i]) * scale + shift;
                if (!initialised) { bounds = new Bounds(point, Vector3.zero); initialised = true; }
                else bounds.Encapsulate(point);
            }

            UnityEngine.Object.DestroyImmediate(baked);
            return bounds;
        }

        public static Transform FindBone(SkinnedMeshRenderer renderer, string boneName)
        {
            foreach (Transform bone in renderer.bones)
                if (bone != null && (bone.name == boneName || bone.name.EndsWith(":" + boneName))) return bone;
            foreach (Transform bone in renderer.bones)
                if (bone != null && bone.name.EndsWith(boneName)) return bone;
            return null;
        }

        // the mesh is authored in world space for the current pose; parenting keeps that placement
        public static GameObject Attach(Transform parent, string objectName, Mesh mesh)
        {
            GameObject attachment = new GameObject(objectName);
            attachment.AddComponent<MeshFilter>().sharedMesh = mesh;
            attachment.AddComponent<MeshRenderer>().sharedMaterial = PlayerMaterial;
            attachment.transform.SetParent(parent, true);
            return attachment;
        }

        public static Mesh SaveMesh(Mesh mesh, string folder, string name)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                return existing;
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
