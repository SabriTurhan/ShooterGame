using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter.Examples;

namespace Watermelon.SquadShooter
{
    // Builds the environment kit of a new world from the World_02 kit: borders/ground are re-textured with a
    // recoloured atlas (Tools/Art/recolor_atlas.py), every obstacle gets a new low-poly model in the palette
    // style while colliders and nav mesh settings stay the same, and a WorldData asset is created.
    public static class WorldKitBuilder
    {
        private const string GAME = "Assets/Project Files/Game";
        private const string PRESETS = GAME + "/Prefabs/Levels/Presets";
        private const string WORLDS = "Assets/Project Files/Data/Level System/Worlds";
        private const string LEVELS_DATABASE = "Assets/Project Files/Data/Level System/Levels Database.asset";
        private const string ENV_MATERIALS = GAME + "/Materials/Environment";

        [MenuItem("Tools/Worlds/Build World 3 (Glacier Base)")]
        public static void BuildGlacier()
        {
            BuildWorld(3, "Glacier", GlacierObstacles());
        }

        [MenuItem("Tools/Worlds/Build World 4 (Riot City)")]
        public static void BuildRiotCity()
        {
            BuildWorld(4, "Riot City", CityObstacles());
        }

        [MenuItem("Tools/Worlds/Build World 5 (Volcano Factory)")]
        public static void BuildVolcano()
        {
            BuildWorld(5, "Volcano Factory", VolcanoObstacles());
            AddLavaPool(5);
            AssetDatabase.SaveAssets();
        }

        public static void BuildWorld(int number, string theme, Dictionary<string, Func<Mesh>> obstacles)
        {
            string folder = $"{PRESETS}/World_{number:00}";
            string sourceFolder = $"{PRESETS}/World_02";

            Material environment = CreateEnvironmentMaterial(number);
            Material props = CreatePropsMaterial(number);

            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            foreach (string sub in new[] { "Environment", "Obstacles" })
            {
                EnsureFolder($"{folder}/{sub}");
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"{sourceFolder}/{sub}" }))
                {
                    string source = AssetDatabase.GUIDToAssetPath(guid);
                    string target = $"{folder}/{sub}/{Path.GetFileName(source)}";
                    if (!File.Exists(target)) AssetDatabase.CopyAsset(source, target);

                    GameObject root = PrefabUtility.LoadPrefabContents(target);
                    string name = Path.GetFileNameWithoutExtension(target);

                    if (sub == "Obstacles" && obstacles.TryGetValue(name, out Func<Mesh> build))
                    {
                        Mesh mesh = SaveMesh(build(), $"{folder}/Meshes", $"{number}_{name.Replace(' ', '_')}");
                        MeshFilter filter = root.GetComponent<MeshFilter>();
                        filter.sharedMesh = mesh;
                        root.GetComponent<MeshRenderer>().sharedMaterial = props;
                    }
                    else
                    {
                        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            Material[] materials = renderer.sharedMaterials;
                            for (int i = 0; i < materials.Length; i++)
                                if (materials[i] != null && materials[i].name == "Environment 2") materials[i] = environment;
                            renderer.sharedMaterials = materials;
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, target);
                    PrefabUtility.UnloadPrefabContents(root);
                    prefabs[name] = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                }
            }

            CreateWorldData(number, prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldKit] world {number} ({theme}) kit built: {prefabs.Count} prefabs");
        }

        private static void CreateWorldData(int number, Dictionary<string, GameObject> prefabs)
        {
            string path = $"{WORLDS}/World_{number:00}.asset";
            if (!File.Exists(path)) AssetDatabase.CopyAsset($"{WORLDS}/World_02.asset", path);

            WorldData world = AssetDatabase.LoadAssetAtPath<WorldData>(path);
            SerializedObject serialized = new SerializedObject(world);
            serialized.FindProperty("worldType").intValue = number;

            // same item hashes as the source world, so the environment presets keep working; only the prefabs change
            SerializedProperty items = serialized.FindProperty("items");
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty prefab = items.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");
                if (prefab.objectReferenceValue != null && prefabs.TryGetValue(prefab.objectReferenceValue.name, out GameObject replacement))
                    prefab.objectReferenceValue = replacement;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);

            LevelsDatabase database = AssetDatabase.LoadAssetAtPath<LevelsDatabase>(LEVELS_DATABASE);
            SerializedObject databaseObject = new SerializedObject(database);
            SerializedProperty worlds = databaseObject.FindProperty("worlds");
            bool present = false;
            for (int i = 0; i < worlds.arraySize; i++)
                if (worlds.GetArrayElementAtIndex(i).objectReferenceValue == world) present = true;

            if (!present)
            {
                worlds.arraySize = Mathf.Max(worlds.arraySize, number);
                worlds.GetArrayElementAtIndex(number - 1).objectReferenceValue = world;
                databaseObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(database);
            }
        }

        #region Materials
        private static Material CreateEnvironmentMaterial(int number)
        {
            string path = $"{ENV_MATERIALS}/Environment {number}.mat";
            string atlasPath = $"{GAME}/Textures/World/{number}_environment_atlas.png";

            CopyTextureImport($"{GAME}/Textures/World/2_environment_atlas.png", atlasPath);

            if (!File.Exists(path)) AssetDatabase.CopyAsset($"{ENV_MATERIALS}/Environment 2.mat", path);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            EditorUtility.SetDirty(material);

            return material;
        }

        // props use the shared character palette (same atlas as Frost / Cryo Blaster) with the environment shading
        private static Material CreatePropsMaterial(int number)
        {
            string path = $"{ENV_MATERIALS}/Environment {number} Props.mat";
            if (!File.Exists(path)) AssetDatabase.CopyAsset($"{ENV_MATERIALS}/Environment 2.mat", path);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GAME}/Textures/Characters/Player/player_atlas.png");
            EditorUtility.SetDirty(material);

            return material;
        }

        private static void CopyTextureImport(string sourcePath, string targetPath)
        {
            AssetDatabase.ImportAsset(targetPath);
            TextureImporter source = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            TextureImporter target = (TextureImporter)AssetImporter.GetAtPath(targetPath);
            if (source == null || target == null) return;

            TextureImporterSettings settings = new TextureImporterSettings();
            source.ReadTextureSettings(settings);
            target.SetTextureSettings(settings);
            target.maxTextureSize = source.maxTextureSize;
            target.textureCompression = source.textureCompression;
            foreach (string platform in new[] { "Android", "iPhone", "Standalone" })
                target.SetPlatformTextureSettings(source.GetPlatformTextureSettings(platform));
            target.SaveAndReimport();
        }
        #endregion

        #region Glacier obstacles
        // every model fits the collider of the World_02 piece it replaces (pivot at the bottom center, front = -z)
        private static Dictionary<string, Func<Mesh>> GlacierObstacles()
        {
            return new Dictionary<string, Func<Mesh>>
            {
                { "Block 1x1 Big", IceBlock },
                { "Block 1x1 Big Half", SnowCrate },
                { "Block 1x1 Medium", FuelDrums },
                { "Block 1x1 Medium Half", IceChunk },
                { "Block 1x1 Small", IceCrystals },
                { "Block 1x1 Small Half", () => Scaled(Snowman(), 0.8f) },
                { "Block 1x2 Big", Container },
                { "Block 1x2 Big Half", PipePallet },
                { "Block 1x2 Medium", CrateStack },
                { "Block 1x2 Medium Half", SnowBarricade },
                { "Block 1x2 Small", Fence },
                { "Block 2x2 Big Half", RadarPlatform },
                { "Wall 1x1", () => ConcreteWall(1.9f) },
                { "Wall 1x2", () => ConcreteWall(3.86f) },
                { "Wall Corner", WallPost },
            };
        }

        private static Mesh IceBlock()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.72f, 1.5f, 1.7f), 0.2f, new Vector3(0f, 0.75f, 0f), new Vector3(0f, 7f, 0f), Band.Sky, 0.05f, 0.45f);
            m.Box(new Vector3(1.15f, 0.6f, 1.05f), 0.14f, new Vector3(0.18f, 1.72f, -0.08f), new Vector3(4f, 24f, 5f), Band.Sky, 0f, 0.35f);
            m.Box(new Vector3(1.45f, 0.16f, 1.38f), 0.07f, new Vector3(-0.05f, 1.55f, 0.05f), new Vector3(0f, 7f, 0f), Band.White, 0f, 0.1f);
            m.Cylinder(0.16f, 0f, 0.6f, 5, new Vector3(-0.78f, 0.28f, -0.72f), new Vector3(-15f, 0f, 20f), Band.Sky, 0f, 0.4f);
            m.Cylinder(0.12f, 0f, 0.45f, 5, new Vector3(0.8f, 0.2f, -0.75f), new Vector3(-10f, 0f, -25f), Band.Sky, 0f, 0.4f);
            return m.Build("IceBlock");
        }

        private static Mesh SnowCrate()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.7f, 0.68f, 1.7f), 0.05f, new Vector3(0f, 0.34f, 0f), Vector3.zero, Band.Gold, 0.15f, 0.55f);
            for (int i = -1; i <= 1; i += 2)
            {
                m.Box(new Vector3(1.74f, 0.1f, 0.12f), 0.02f, new Vector3(0f, 0.34f + i * 0.2f, -0.85f), Vector3.zero, Band.Brown, 0.2f, 0.5f);
                m.Box(new Vector3(0.12f, 0.1f, 1.74f), 0.02f, new Vector3(-0.85f, 0.34f + i * 0.2f, 0f), Vector3.zero, Band.Brown, 0.2f, 0.5f);
            }
            m.Box(new Vector3(1.6f, 0.14f, 1.6f), 0.06f, new Vector3(0f, 0.74f, 0f), Vector3.zero, Band.White, 0f, 0.12f);
            m.Dome(0.42f, 2, 7, new Vector3(0.35f, 0.78f, 0.25f), Vector3.zero, new Vector3(1f, 0.55f, 1f), Band.White, 0f, 0.1f);
            return m.Build("SnowCrate");
        }

        private static Mesh FuelDrums()
        {
            PaletteMesh m = new PaletteMesh();
            Vector3[] spots = { new Vector3(-0.36f, 0f, -0.25f), new Vector3(0.36f, 0f, -0.2f), new Vector3(0f, 0f, 0.38f) };
            foreach (Vector3 spot in spots)
            {
                m.Cylinder(0.36f, 0.36f, 1.1f, 9, spot + new Vector3(0f, 0.55f, 0f), Vector3.zero, Band.Red, 0.1f, 0.6f);
                m.Cylinder(0.375f, 0.375f, 0.07f, 9, spot + new Vector3(0f, 0.3f, 0f), Vector3.zero, Band.Black, 0.2f, 0.6f);
                m.Cylinder(0.375f, 0.375f, 0.07f, 9, spot + new Vector3(0f, 0.82f, 0f), Vector3.zero, Band.Black, 0.2f, 0.6f);
                m.Cylinder(0.3f, 0.26f, 0.08f, 9, spot + new Vector3(0f, 1.14f, 0f), Vector3.zero, Band.White, 0f, 0.1f);
            }
            return m.Build("FuelDrums");
        }

        private static Mesh Snowman()
        {
            PaletteMesh m = new PaletteMesh();
            m.Sphere(0.55f, 4, 10, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.9f, 1f), Band.White, 0f, 0.25f);
            m.Sphere(0.4f, 4, 10, new Vector3(0f, 1.18f, 0f), Vector3.one, Band.White, 0f, 0.25f);
            m.Sphere(0.3f, 4, 9, new Vector3(0f, 1.72f, 0f), Vector3.one, Band.White, 0f, 0.2f);
            m.Cylinder(0.42f, 0.42f, 0.13f, 10, new Vector3(0f, 1.45f, 0f), Vector3.zero, Band.Red, 0.2f, 0.6f);
            m.Box(new Vector3(0.14f, 0.36f, 0.06f), 0.02f, new Vector3(0.22f, 1.3f, -0.33f), new Vector3(0f, 0f, 10f), Band.Red, 0.3f, 0.6f);
            m.Cylinder(0.06f, 0f, 0.32f, 6, new Vector3(0f, 1.72f, -0.42f), new Vector3(-90f, 0f, 0f), Band.Orange, 0.4f, 0.8f);
            for (int side = -1; side <= 1; side += 2)
                m.Sphere(0.045f, 2, 6, new Vector3(side * 0.11f, 1.82f, -0.26f), Vector3.one, Band.Black, 0f, 0.3f);
            m.Cylinder(0.32f, 0.32f, 0.05f, 10, new Vector3(0f, 1.97f, 0f), Vector3.zero, Band.Black, 0f, 0.4f);
            m.Cylinder(0.2f, 0.2f, 0.32f, 10, new Vector3(0f, 2.15f, 0f), Vector3.zero, Band.Black, 0f, 0.4f);
            m.Box(new Vector3(0.05f, 0.5f, 0.05f), 0.01f, new Vector3(0.48f, 1.3f, 0f), new Vector3(0f, 0f, -55f), Band.Brown, 0.2f, 0.6f);
            m.Box(new Vector3(0.05f, 0.5f, 0.05f), 0.01f, new Vector3(-0.48f, 1.3f, 0f), new Vector3(0f, 0f, 55f), Band.Brown, 0.2f, 0.6f);
            return m.Build("Snowman");
        }

        private static Mesh IceCrystals()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.24f, 0f, 1.8f, 6, new Vector3(0f, 0.9f, 0f), new Vector3(4f, 0f, -6f), Band.Sky, 0f, 0.45f);
            m.Cylinder(0.18f, 0f, 1.2f, 6, new Vector3(0.24f, 0.55f, -0.12f), new Vector3(-12f, 0f, -25f), Band.Ice, 0f, 0.4f);
            m.Cylinder(0.16f, 0f, 0.95f, 6, new Vector3(-0.25f, 0.42f, 0.1f), new Vector3(10f, 0f, 28f), Band.Sky, 0.1f, 0.5f);
            m.Dome(0.42f, 2, 7, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(1f, 0.35f, 1f), Band.White, 0f, 0.1f);
            return m.Build("IceCrystals");
        }

        // a frozen block half buried in a snow drift
        private static Mesh IceChunk()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.9f, 0.7f, 0.8f), 0.14f, new Vector3(0.2f, 0.35f, 0.1f), new Vector3(8f, 30f, 6f), Band.Sky, 0.05f, 0.45f);
            m.Cylinder(0.12f, 0f, 0.5f, 5, new Vector3(-0.45f, 0.3f, 0.3f), new Vector3(10f, 0f, 22f), Band.Ice, 0f, 0.4f);
            m.Dome(0.75f, 3, 9, new Vector3(-0.15f, 0f, -0.1f), Vector3.zero, new Vector3(1f, 0.55f, 0.95f), Band.White, 0f, 0.15f);
            return m.Build("IceChunk");
        }

        private static Mesh Scaled(Mesh mesh, float scale)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] *= scale;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SnowPile()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.5f, 0.42f, 0.45f), 0.1f, new Vector3(0.12f, 0.2f, 0.08f), new Vector3(0f, 25f, 8f), Band.Grey, 0.2f, 0.6f);
            m.Dome(0.48f, 3, 8, new Vector3(-0.05f, 0f, -0.05f), Vector3.zero, new Vector3(1f, 0.75f, 1f), Band.White, 0f, 0.15f);
            return m.Build("SnowPile");
        }

        private static Mesh Container()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.7f, 1.75f, 1.72f), 0.05f, new Vector3(0f, 0.88f, 0f), Vector3.zero, Band.Teal, 0.1f, 0.55f);
            for (float x = -1.5f; x <= 1.51f; x += 0.5f)
            {
                m.Box(new Vector3(0.1f, 1.6f, 0.06f), 0.01f, new Vector3(x, 0.88f, -0.88f), Vector3.zero, Band.Teal, 0.45f, 0.75f);
                m.Box(new Vector3(0.1f, 1.6f, 0.06f), 0.01f, new Vector3(x, 0.88f, 0.88f), Vector3.zero, Band.Teal, 0.45f, 0.75f);
            }
            m.Box(new Vector3(0.06f, 1.7f, 1.7f), 0.01f, new Vector3(-1.86f, 0.88f, 0f), Vector3.zero, Band.Teal, 0.5f, 0.8f);
            m.Box(new Vector3(0.08f, 1.2f, 0.06f), 0.01f, new Vector3(-1.9f, 0.88f, -0.3f), Vector3.zero, Band.Steel, 0.3f, 0.6f);
            m.Box(new Vector3(0.08f, 1.2f, 0.06f), 0.01f, new Vector3(-1.9f, 0.88f, 0.3f), Vector3.zero, Band.Steel, 0.3f, 0.6f);
            m.Box(new Vector3(1.2f, 0.28f, 0.03f), 0.01f, new Vector3(0.6f, 1.15f, -0.92f), Vector3.zero, Band.Yellow, 0.3f, 0.5f);
            m.Box(new Vector3(3.6f, 0.15f, 1.62f), 0.06f, new Vector3(0f, 1.83f, 0f), Vector3.zero, Band.White, 0f, 0.12f);
            m.Dome(0.5f, 2, 8, new Vector3(-0.9f, 1.88f, 0.2f), Vector3.zero, new Vector3(1.4f, 0.4f, 1f), Band.White, 0f, 0.1f);
            return m.Build("Container");
        }

        private static Mesh PipePallet()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.14f, 1.55f), 0.03f, new Vector3(0f, 0.07f, 0f), Vector3.zero, Band.Gold, 0.3f, 0.7f);
            m.Cylinder(0.24f, 0.24f, 3.4f, 8, new Vector3(0f, 0.38f, -0.3f), new Vector3(0f, 0f, 90f), Band.Steel, 0.1f, 0.55f);
            m.Cylinder(0.24f, 0.24f, 3.4f, 8, new Vector3(0f, 0.38f, 0.3f), new Vector3(0f, 0f, 90f), Band.Steel, 0.1f, 0.55f);
            m.Cylinder(0.24f, 0.24f, 3.2f, 8, new Vector3(0.1f, 0.78f, 0f), new Vector3(0f, 0f, 90f), Band.Red, 0.15f, 0.6f);
            m.Box(new Vector3(2.4f, 0.1f, 0.5f), 0.04f, new Vector3(0.2f, 1.04f, 0f), Vector3.zero, Band.White, 0f, 0.1f);
            return m.Build("PipePallet");
        }

        private static Mesh CrateStack()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.55f, 1.3f, 1.45f), 0.05f, new Vector3(-0.85f, 0.65f, 0f), new Vector3(0f, 3f, 0f), Band.Gold, 0.15f, 0.55f);
            m.Box(new Vector3(1.55f, 1.05f, 1.45f), 0.05f, new Vector3(0.85f, 0.53f, 0.02f), new Vector3(0f, -4f, 0f), Band.Gold, 0.15f, 0.55f);
            m.Box(new Vector3(0.95f, 0.65f, 0.95f), 0.04f, new Vector3(-0.75f, 1.62f, -0.05f), new Vector3(0f, 18f, 0f), Band.Gold, 0.1f, 0.5f);
            foreach (float x in new[] { -0.85f, 0.85f })
                m.Box(new Vector3(1.58f, 0.1f, 0.06f), 0.02f, new Vector3(x, 0.55f, -0.74f), Vector3.zero, Band.Brown, 0.2f, 0.5f);
            m.Box(new Vector3(1.4f, 0.12f, 1.3f), 0.05f, new Vector3(0.85f, 1.1f, 0.02f), new Vector3(0f, -4f, 0f), Band.White, 0f, 0.1f);
            m.Box(new Vector3(0.85f, 0.1f, 0.85f), 0.04f, new Vector3(-0.75f, 1.98f, -0.05f), new Vector3(0f, 18f, 0f), Band.White, 0f, 0.1f);
            return m.Build("CrateStack");
        }

        private static Mesh SnowBarricade()
        {
            PaletteMesh m = new PaletteMesh();
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(0.82f, 0.34f, 0.62f), 0.14f, new Vector3(-1.29f + i * 0.86f, 0.17f, 0f), new Vector3(0f, (i % 2) * 6f - 3f, 0f), Band.Cream, 0.15f, 0.6f);
            for (int i = 0; i < 3; i++)
                m.Box(new Vector3(0.82f, 0.32f, 0.6f), 0.14f, new Vector3(-0.86f + i * 0.86f, 0.48f, 0.02f), new Vector3(0f, (i % 2) * -6f + 3f, 0f), Band.Cream, 0.1f, 0.55f);
            m.Box(new Vector3(2.4f, 0.12f, 0.5f), 0.05f, new Vector3(0f, 0.68f, 0.02f), Vector3.zero, Band.White, 0f, 0.1f);
            return m.Build("SnowBarricade");
        }

        private static Mesh Fence()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (float x in new[] { -0.82f, 0.82f })
            {
                m.Cylinder(0.08f, 0.08f, 1.7f, 6, new Vector3(x, 0.85f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
                m.Cylinder(0.11f, 0.09f, 0.08f, 6, new Vector3(x, 1.72f, 0f), Vector3.zero, Band.White, 0f, 0.1f);
            }
            foreach (float y in new[] { 0.45f, 0.95f, 1.45f })
            {
                m.Box(new Vector3(1.62f, 0.09f, 0.07f), 0.02f, new Vector3(0f, y, 0f), Vector3.zero, Band.Steel, 0.2f, 0.6f);
                m.Box(new Vector3(1.5f, 0.05f, 0.1f), 0.02f, new Vector3(0f, y + 0.07f, 0f), Vector3.zero, Band.White, 0f, 0.1f);
            }
            m.Box(new Vector3(0.5f, 0.36f, 0.04f), 0.02f, new Vector3(0.1f, 1.18f, -0.06f), new Vector3(0f, 0f, 3f), Band.Yellow, 0.3f, 0.5f);
            m.Box(new Vector3(0.3f, 0.05f, 0.03f), 0.01f, new Vector3(0.1f, 1.18f, -0.09f), new Vector3(0f, 0f, 3f), Band.Black, 0f, 0.2f);
            return m.Build("Fence");
        }

        private static Mesh RadarPlatform()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.55f, 3.6f), 0.1f, new Vector3(0f, 0.28f, 0f), Vector3.zero, Band.Steel, 0.15f, 0.6f);
            m.Box(new Vector3(3.62f, 0.1f, 0.3f), 0.02f, new Vector3(0f, 0.5f, -1.66f), Vector3.zero, Band.Yellow, 0.3f, 0.5f);
            m.Cylinder(0.12f, 0.15f, 1.5f, 8, new Vector3(0.4f, 1.3f, 0.4f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Dome(1.05f, 3, 12, new Vector3(0.4f, 2.0f, 0.4f), new Vector3(-55f, 25f, 0f), new Vector3(1f, 0.32f, 1f), Band.White, 0f, 0.3f);
            m.Cylinder(0.05f, 0.05f, 0.75f, 6, new Vector3(0.4f, 2.35f, 0.05f), new Vector3(-55f, 25f, 0f), Band.Grey, 0.2f, 0.5f);
            m.Sphere(0.1f, 2, 6, new Vector3(0.25f, 2.6f, -0.2f), Vector3.one, Band.Red, 0.2f, 0.5f);
            m.Box(new Vector3(0.9f, 0.6f, 0.7f), 0.06f, new Vector3(-1.0f, 0.85f, -0.9f), Vector3.zero, Band.Navy, 0.1f, 0.5f);
            m.Box(new Vector3(0.5f, 0.2f, 0.04f), 0.02f, new Vector3(-1.0f, 0.95f, -1.26f), Vector3.zero, Band.Sky, 0f, 0.2f);
            m.Dome(0.6f, 2, 8, new Vector3(1.1f, 0.55f, -1.1f), Vector3.zero, new Vector3(1.2f, 0.4f, 1f), Band.White, 0f, 0.1f);
            return m.Build("RadarPlatform");
        }

        private static Mesh ConcreteWall(float width)
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(width, 1.62f, 0.45f), 0.05f, new Vector3(0f, 0.81f, 0.24f), Vector3.zero, Band.Grey, 0.05f, 0.45f);
            m.Box(new Vector3(width + 0.02f, 0.2f, 0.47f), 0.02f, new Vector3(0f, 1.38f, 0.24f), Vector3.zero, Band.Yellow, 0.3f, 0.5f);
            for (float x = -width / 2f + 0.2f; x < width / 2f - 0.1f; x += 0.38f)
                m.Box(new Vector3(0.1f, 0.24f, 0.49f), 0.01f, new Vector3(x, 1.38f, 0.24f), new Vector3(0f, 0f, 35f), Band.Black, 0f, 0.3f);
            m.Box(new Vector3(width - 0.06f, 0.14f, 0.5f), 0.06f, new Vector3(0f, 1.66f, 0.24f), Vector3.zero, Band.White, 0f, 0.1f);
            return m.Build("ConcreteWall");
        }

        private static Mesh WallPost()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.46f, 2.05f, 0.46f), 0.05f, new Vector3(-0.25f, 1.03f, 0.25f), Vector3.zero, Band.Grey, 0.05f, 0.45f);
            m.Box(new Vector3(0.5f, 0.14f, 0.5f), 0.06f, new Vector3(-0.25f, 2.1f, 0.25f), Vector3.zero, Band.White, 0f, 0.1f);
            return m.Build("WallPost");
        }
        #endregion

        #region City obstacles
        private static Dictionary<string, Func<Mesh>> CityObstacles()
        {
            return new Dictionary<string, Func<Mesh>>
            {
                { "Block 1x1 Big", VendingMachine },
                { "Block 1x1 Big Half", CardboardBoxes },
                { "Block 1x1 Medium", TrashCans },
                { "Block 1x1 Medium Half", BurningBarrel },
                { "Block 1x1 Small", StreetLamp },
                { "Block 1x1 Small Half", TrafficCones },
                { "Block 1x2 Big", PoliceCar },
                { "Block 1x2 Big Half", ConcretePlanter },
                { "Block 1x2 Medium", Dumpster },
                { "Block 1x2 Medium Half", PoliceBarricade },
                { "Block 1x2 Small", BusStopBench },
                { "Block 2x2 Big Half", TreePlanter },
                { "Wall 1x1", () => JerseyBarrier(1.9f) },
                { "Wall 1x2", () => JerseyBarrier(3.86f) },
                { "Wall Corner", Bollard },
            };
        }

        private static Mesh VendingMachine()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.2f, 1.8f, 0.85f), 0.06f, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 8f, 0f), Band.Red, 0.1f, 0.6f);
            m.Box(new Vector3(0.8f, 1.05f, 0.05f), 0.02f, new Vector3(-0.12f, 1.05f, -0.44f), new Vector3(0f, 8f, 0f), Band.Sky, 0f, 0.3f);
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(0.62f, 0.05f, 0.06f), 0.01f, new Vector3(-0.12f, 0.68f + i * 0.25f, -0.47f), new Vector3(0f, 8f, 0f), Band.White, 0f, 0.2f);
            m.Box(new Vector3(0.2f, 0.5f, 0.06f), 0.02f, new Vector3(0.42f, 1.15f, -0.43f), new Vector3(0f, 8f, 0f), Band.Black, 0f, 0.3f);
            m.Box(new Vector3(0.7f, 0.18f, 0.06f), 0.02f, new Vector3(0f, 0.28f, -0.43f), new Vector3(0f, 8f, 0f), Band.Black, 0f, 0.4f);
            m.Box(new Vector3(1.22f, 0.2f, 0.88f), 0.04f, new Vector3(0f, 1.72f, 0f), new Vector3(0f, 8f, 0f), Band.White, 0f, 0.2f);
            return m.Build("VendingMachine");
        }

        private static Mesh CardboardBoxes()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.9f, 0.6f, 0.8f), 0.03f, new Vector3(-0.38f, 0.3f, -0.2f), new Vector3(0f, 10f, 0f), Band.Gold, 0.1f, 0.5f);
            m.Box(new Vector3(0.75f, 0.55f, 0.75f), 0.03f, new Vector3(0.45f, 0.28f, 0.25f), new Vector3(0f, -14f, 0f), Band.Gold, 0.15f, 0.55f);
            m.Box(new Vector3(0.6f, 0.45f, 0.6f), 0.03f, new Vector3(-0.3f, 0.82f, -0.15f), new Vector3(0f, 32f, 0f), Band.Gold, 0.05f, 0.45f);
            m.Box(new Vector3(0.92f, 0.08f, 0.15f), 0.01f, new Vector3(-0.38f, 0.61f, -0.2f), new Vector3(0f, 10f, 0f), Band.Cream, 0f, 0.3f);
            return m.Build("CardboardBoxes");
        }

        private static Mesh TrashCans()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (float x in new[] { -0.36f, 0.38f })
            {
                m.Cylinder(0.32f, 0.36f, 0.95f, 9, new Vector3(x, 0.48f, 0.05f), Vector3.zero, Band.Steel, 0.15f, 0.6f);
                m.Cylinder(0.4f, 0.4f, 0.08f, 9, new Vector3(x, 0.99f, 0.05f), new Vector3(0f, 0f, x < 0 ? 0f : 12f), Band.Grey, 0.1f, 0.5f);
                m.Box(new Vector3(0.18f, 0.05f, 0.05f), 0.01f, new Vector3(x, 1.06f, 0.05f), Vector3.zero, Band.Grey, 0.3f, 0.6f);
            }
            m.Dome(0.38f, 3, 8, new Vector3(0.05f, 0f, -0.45f), Vector3.zero, new Vector3(1f, 0.85f, 1f), Band.Black, 0f, 0.4f);
            return m.Build("TrashCans");
        }

        private static Mesh BurningBarrel()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.42f, 0.42f, 0.95f, 10, new Vector3(0f, 0.48f, 0f), Vector3.zero, Band.Brown, 0.2f, 0.75f);
            m.Cylinder(0.44f, 0.44f, 0.07f, 10, new Vector3(0f, 0.3f, 0f), Vector3.zero, Band.Black, 0.2f, 0.5f);
            m.Cylinder(0.44f, 0.44f, 0.07f, 10, new Vector3(0f, 0.72f, 0f), Vector3.zero, Band.Black, 0.2f, 0.5f);
            m.Cylinder(0.3f, 0f, 0.6f, 6, new Vector3(0f, 1.2f, 0f), new Vector3(0f, 15f, 0f), Band.Orange, 0.3f, 0.75f);
            m.Cylinder(0.2f, 0f, 0.45f, 5, new Vector3(0.14f, 1.14f, 0.08f), new Vector3(0f, 0f, -18f), Band.Yellow, 0f, 0.3f);
            m.Cylinder(0.16f, 0f, 0.4f, 5, new Vector3(-0.15f, 1.1f, -0.05f), new Vector3(0f, 0f, 20f), Band.Yellow, 0f, 0.3f);
            return m.Build("BurningBarrel");
        }

        private static Mesh StreetLamp()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.22f, 0.26f, 0.2f, 8, new Vector3(0f, 0.1f, 0f), Vector3.zero, Band.Grey, 0.3f, 0.7f);
            m.Cylinder(0.08f, 0.08f, 2.6f, 8, new Vector3(0f, 1.4f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Box(new Vector3(0.06f, 0.06f, 0.6f), 0.01f, new Vector3(0f, 2.66f, -0.28f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Cylinder(0.22f, 0.12f, 0.2f, 8, new Vector3(0f, 2.6f, -0.55f), Vector3.zero, Band.Black, 0f, 0.4f);
            m.Cylinder(0.14f, 0.14f, 0.05f, 8, new Vector3(0f, 2.48f, -0.55f), Vector3.zero, Band.Yellow, 0f, 0.15f);
            return m.Build("StreetLamp");
        }

        private static Mesh TrafficCones()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (Vector3 p in new[] { new Vector3(-0.18f, 0f, -0.12f), new Vector3(0.22f, 0f, 0.15f) })
            {
                m.Box(new Vector3(0.42f, 0.06f, 0.42f), 0.02f, p + new Vector3(0f, 0.03f, 0f), Vector3.zero, Band.Orange, 0.4f, 0.8f);
                m.Cylinder(0.17f, 0.03f, 0.6f, 8, p + new Vector3(0f, 0.36f, 0f), Vector3.zero, Band.Orange, 0.3f, 0.75f);
                m.Cylinder(0.12f, 0.1f, 0.1f, 8, p + new Vector3(0f, 0.4f, 0f), Vector3.zero, Band.White, 0f, 0.15f);
            }
            return m.Build("TrafficCones");
        }

        private static Mesh PoliceCar()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.62f, 1.6f), 0.14f, new Vector3(0f, 0.55f, 0f), Vector3.zero, Band.White, 0f, 0.3f);
            m.Box(new Vector3(1.6f, 0.62f, 1.48f), 0.12f, new Vector3(-0.2f, 1.12f, 0f), Vector3.zero, Band.Navy, 0.1f, 0.5f);
            m.Box(new Vector3(1.66f, 0.38f, 1.5f), 0.06f, new Vector3(-0.2f, 1.14f, 0f), Vector3.zero, Band.Sky, 0f, 0.35f);
            m.Box(new Vector3(1.2f, 0.42f, 1.62f), 0.06f, new Vector3(-0.1f, 0.6f, 0f), Vector3.zero, Band.Navy, 0.15f, 0.5f);
            m.Box(new Vector3(0.36f, 0.14f, 0.7f), 0.03f, new Vector3(-0.2f, 1.5f, -0.18f), Vector3.zero, Band.Red, 0.1f, 0.4f);
            m.Box(new Vector3(0.36f, 0.14f, 0.7f), 0.03f, new Vector3(-0.2f, 1.5f, 0.18f), Vector3.zero, Band.Blue, 0.1f, 0.4f);
            foreach (float x in new[] { -1.15f, 1.15f })
                foreach (float z in new[] { -0.78f, 0.78f })
                    m.Cylinder(0.34f, 0.34f, 0.22f, 10, new Vector3(x, 0.34f, z), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
            m.Box(new Vector3(0.08f, 0.2f, 1.2f), 0.02f, new Vector3(1.82f, 0.55f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.5f);
            return m.Build("PoliceCar");
        }

        private static Mesh ConcretePlanter()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.6f, 1.55f), 0.06f, new Vector3(0f, 0.3f, 0f), Vector3.zero, Band.Grey, 0.05f, 0.45f);
            m.Box(new Vector3(3.4f, 0.1f, 1.35f), 0.02f, new Vector3(0f, 0.58f, 0f), Vector3.zero, Band.Brown, 0.4f, 0.8f);
            foreach (float x in new[] { -1.1f, -0.2f, 0.75f, 1.35f })
                m.Sphere(0.42f, 3, 8, new Vector3(x, 0.8f, (x * 7f) % 0.3f), new Vector3(1.1f, 0.85f, 1f), Band.Green, 0.15f, 0.6f);
            return m.Build("ConcretePlanter");
        }

        private static Mesh Dumpster()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.3f, 1.25f, 1.45f), 0.06f, new Vector3(0f, 0.72f, 0f), Vector3.zero, Band.Teal, 0.2f, 0.7f);
            m.Box(new Vector3(1.66f, 0.1f, 1.52f), 0.02f, new Vector3(-0.82f, 1.4f, 0.06f), new Vector3(-6f, 0f, 0f), Band.Black, 0.1f, 0.5f);
            m.Box(new Vector3(1.66f, 0.1f, 1.52f), 0.02f, new Vector3(0.82f, 1.48f, 0.15f), new Vector3(-20f, 0f, 0f), Band.Black, 0.1f, 0.5f);
            foreach (float x in new[] { -1.4f, 1.4f })
                m.Cylinder(0.12f, 0.12f, 0.14f, 8, new Vector3(x, 0.08f, -0.55f), new Vector3(0f, 0f, 90f), Band.Black, 0f, 0.4f);
            m.Box(new Vector3(1.3f, 0.3f, 0.04f), 0.02f, new Vector3(0f, 0.85f, -0.74f), Vector3.zero, Band.White, 0f, 0.2f);
            m.Dome(0.32f, 2, 7, new Vector3(-1.9f, 0f, 0.3f), Vector3.zero, new Vector3(1f, 0.9f, 1f), Band.Black, 0f, 0.4f);
            return m.Build("Dumpster");
        }

        private static Mesh PoliceBarricade()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (float y in new[] { 0.42f, 0.66f })
            {
                m.Box(new Vector3(3.3f, 0.16f, 0.1f), 0.02f, new Vector3(0f, y, 0f), Vector3.zero, Band.White, 0f, 0.15f);
                for (float x = -1.5f; x <= 1.51f; x += 0.42f)
                    m.Box(new Vector3(0.14f, 0.17f, 0.11f), 0.01f, new Vector3(x, y, 0f), new Vector3(0f, 0f, 30f), Band.Orange, 0.3f, 0.6f);
            }
            foreach (float x in new[] { -1.45f, 1.45f })
            {
                m.Box(new Vector3(0.08f, 0.8f, 0.08f), 0.01f, new Vector3(x, 0.38f, -0.18f), new Vector3(-18f, 0f, 0f), Band.Grey, 0.2f, 0.6f);
                m.Box(new Vector3(0.08f, 0.8f, 0.08f), 0.01f, new Vector3(x, 0.38f, 0.18f), new Vector3(18f, 0f, 0f), Band.Grey, 0.2f, 0.6f);
            }
            m.Cylinder(0.08f, 0.08f, 0.1f, 8, new Vector3(0f, 0.82f, 0f), Vector3.zero, Band.Red, 0f, 0.3f);
            return m.Build("PoliceBarricade");
        }

        private static Mesh BusStopBench()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.8f, 0.1f, 0.5f), 0.02f, new Vector3(0f, 0.48f, 0f), Vector3.zero, Band.Gold, 0.2f, 0.5f);
            m.Box(new Vector3(1.8f, 0.42f, 0.08f), 0.02f, new Vector3(0f, 0.82f, 0.24f), new Vector3(-8f, 0f, 0f), Band.Gold, 0.2f, 0.5f);
            foreach (float x in new[] { -0.75f, 0.75f })
                m.Box(new Vector3(0.08f, 0.48f, 0.45f), 0.01f, new Vector3(x, 0.24f, 0f), Vector3.zero, Band.Black, 0.1f, 0.4f);
            m.Cylinder(0.05f, 0.05f, 1.9f, 6, new Vector3(0.95f, 0.95f, 0.3f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Box(new Vector3(0.5f, 0.36f, 0.05f), 0.02f, new Vector3(0.95f, 1.75f, 0.3f), Vector3.zero, Band.Blue, 0.1f, 0.4f);
            m.Box(new Vector3(0.3f, 0.12f, 0.06f), 0.01f, new Vector3(0.95f, 1.75f, 0.27f), Vector3.zero, Band.White, 0f, 0.2f);
            return m.Build("BusStopBench");
        }

        private static Mesh TreePlanter()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(1.75f, 1.75f, 0.55f, 14, new Vector3(0f, 0.28f, 0f), Vector3.zero, Band.Grey, 0.05f, 0.45f);
            m.Cylinder(1.6f, 1.6f, 0.08f, 14, new Vector3(0f, 0.55f, 0f), Vector3.zero, Band.Green, 0.4f, 0.75f);
            m.Cylinder(0.16f, 0.2f, 1.8f, 7, new Vector3(0.2f, 1.4f, 0.1f), Vector3.zero, Band.Brown, 0.2f, 0.6f);
            m.Sphere(0.9f, 3, 9, new Vector3(0.2f, 2.55f, 0.1f), new Vector3(1f, 0.85f, 1f), Band.Green, 0.05f, 0.5f);
            m.Sphere(0.6f, 3, 8, new Vector3(-0.35f, 2.2f, -0.3f), Vector3.one, Band.Green, 0.15f, 0.55f);
            m.Sphere(0.55f, 3, 8, new Vector3(0.75f, 2.2f, 0.35f), Vector3.one, Band.Green, 0.15f, 0.55f);
            return m.Build("TreePlanter");
        }

        private static Mesh JerseyBarrier(float width)
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(width, 0.5f, 0.48f), 0.04f, new Vector3(0f, 0.25f, 0.24f), Vector3.zero, Band.Grey, 0.1f, 0.5f);
            m.Box(new Vector3(width, 0.75f, 0.3f), 0.04f, new Vector3(0f, 0.85f, 0.24f), Vector3.zero, Band.Grey, 0.05f, 0.4f);
            for (float x = -width / 2f + 0.25f; x < width / 2f - 0.15f; x += 0.5f)
                m.Box(new Vector3(0.25f, 0.2f, 0.32f), 0.01f, new Vector3(x, 1.12f, 0.24f), Vector3.zero, Band.Red, 0.2f, 0.5f);
            m.Box(new Vector3(width - 0.04f, 0.2f, 0.31f), 0.01f, new Vector3(0f, 1.12f, 0.24f), Vector3.zero, Band.White, 0f, 0.15f);
            return m.Build("JerseyBarrier");
        }

        private static Mesh Bollard()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.2f, 0.2f, 1.1f, 8, new Vector3(-0.25f, 0.55f, 0.25f), Vector3.zero, Band.Yellow, 0.3f, 0.6f);
            m.Cylinder(0.21f, 0.21f, 0.14f, 8, new Vector3(-0.25f, 0.8f, 0.25f), Vector3.zero, Band.Black, 0f, 0.3f);
            m.Cylinder(0.21f, 0.21f, 0.14f, 8, new Vector3(-0.25f, 0.45f, 0.25f), Vector3.zero, Band.Black, 0f, 0.3f);
            m.Dome(0.2f, 2, 8, new Vector3(-0.25f, 1.1f, 0.25f), Vector3.zero, Vector3.one, Band.Yellow, 0.2f, 0.5f);
            return m.Build("Bollard");
        }
        #endregion

        #region Volcano obstacles
        private static Dictionary<string, Func<Mesh>> VolcanoObstacles()
        {
            return new Dictionary<string, Func<Mesh>>
            {
                { "Block 1x1 Big", Furnace },
                { "Block 1x1 Big Half", OreCart },
                { "Block 1x1 Medium", BasaltPillar },
                { "Block 1x1 Medium Half", MagmaRock },
                { "Block 1x1 Small", SteamValve },
                { "Block 1x1 Small Half", Brazier },
                { "Block 1x2 Big", Crucible },
                { "Block 1x2 Big Half", IngotPallet },
                { "Block 1x2 Medium", Conveyor },
                { "Block 1x2 Medium Half", HeatShield },
                { "Block 1x2 Small", SafetyRail },
                { "Block 2x2 Big Half", LavaVent },
                { "Wall 1x1", () => FactoryWall(1.9f) },
                { "Wall 1x2", () => FactoryWall(3.86f) },
                { "Wall Corner", GirderPost },
            };
        }

        // a smelting furnace with a glowing mouth and a chimney
        private static Mesh Furnace()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.7f, 1.35f, 1.6f), 0.08f, new Vector3(0f, 0.68f, 0.02f), Vector3.zero, Band.Grey, 0.1f, 0.6f);
            m.Box(new Vector3(1.76f, 0.16f, 1.66f), 0.03f, new Vector3(0f, 0.12f, 0.02f), Vector3.zero, Band.Black, 0f, 0.35f);
            m.Box(new Vector3(0.8f, 0.6f, 0.08f), 0.06f, new Vector3(0f, 0.6f, -0.78f), Vector3.zero, Band.Black, 0f, 0.3f);
            m.Box(new Vector3(0.62f, 0.44f, 0.06f), 0.05f, new Vector3(0f, 0.58f, -0.81f), Vector3.zero, Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.44f, 0.16f, 0.05f), 0.03f, new Vector3(0f, 0.48f, -0.84f), Vector3.zero, Band.Yellow, 0.35f, 0.6f);
            m.Box(new Vector3(1.8f, 0.14f, 1.7f), 0.04f, new Vector3(0f, 1.4f, 0.02f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            m.Cylinder(0.26f, 0.22f, 0.9f, 8, new Vector3(0.45f, 1.9f, 0.35f), Vector3.zero, Band.Black, 0.1f, 0.45f);
            m.Cylinder(0.28f, 0.28f, 0.1f, 8, new Vector3(0.45f, 2.35f, 0.35f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            for (int side = -1; side <= 1; side += 2)
                m.Box(new Vector3(0.12f, 0.12f, 0.06f), 0.02f, new Vector3(side * 0.62f, 1.1f, -0.79f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            return m.Build("Furnace");
        }

        // a mine cart heaped with glowing ore
        private static Mesh OreCart()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(1.6f, 0.5f, 1.3f), 0.05f, new Vector3(0f, 0.42f, 0f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            m.Box(new Vector3(1.66f, 0.08f, 1.36f), 0.02f, new Vector3(0f, 0.66f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.55f);
            foreach (float x in new[] { -0.55f, 0.55f })
                foreach (float z in new[] { -0.68f, 0.68f })
                    m.Cylinder(0.18f, 0.18f, 0.08f, 8, new Vector3(x, 0.18f, z), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
            m.Dome(0.62f, 2, 8, new Vector3(-0.15f, 0.66f, 0f), Vector3.zero, new Vector3(1.15f, 0.5f, 0.95f), Band.Black, 0.1f, 0.5f);
            m.Box(new Vector3(0.24f, 0.22f, 0.22f), 0.06f, new Vector3(0.25f, 0.86f, -0.15f), new Vector3(20f, 30f, 10f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.18f, 0.16f, 0.16f), 0.05f, new Vector3(-0.35f, 0.88f, 0.2f), new Vector3(-15f, 10f, 25f), Band.Yellow, 0.35f, 0.6f);
            m.Box(new Vector3(0.2f, 0.18f, 0.18f), 0.05f, new Vector3(0.45f, 0.8f, 0.3f), new Vector3(5f, 45f, -10f), Band.Orange, 0.72f, 1f);
            return m.Build("OreCart");
        }

        // a dark basalt column with glowing cracks
        private static Mesh BasaltPillar()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.85f, 0.7f, 1.55f, 6, new Vector3(0f, 0.78f, 0f), new Vector3(0f, 10f, 0f), Band.Black, 0.15f, 0.55f);
            m.Cylinder(0.6f, 0.45f, 0.45f, 6, new Vector3(0.12f, 1.75f, 0.05f), new Vector3(4f, 40f, -5f), Band.Black, 0.1f, 0.5f);
            m.Box(new Vector3(0.08f, 0.9f, 0.06f), 0.02f, new Vector3(-0.32f, 0.75f, -0.72f), new Vector3(0f, 10f, 12f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.07f, 0.55f, 0.06f), 0.02f, new Vector3(0.38f, 0.95f, -0.68f), new Vector3(0f, -20f, -18f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.06f, 0.6f, 0.06f), 0.02f, new Vector3(0.78f, 0.6f, 0.1f), new Vector3(0f, 0f, 8f), Band.Yellow, 0.35f, 0.6f);
            return m.Build("BasaltPillar");
        }

        // a cooling magma boulder: black crust, orange core showing through
        private static Mesh MagmaRock()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.95f, 0.75f, 0.85f), 0.22f, new Vector3(0.1f, 0.36f, 0.05f), new Vector3(6f, 25f, 4f), Band.Black, 0.1f, 0.55f);
            m.Box(new Vector3(0.55f, 0.45f, 0.5f), 0.14f, new Vector3(-0.45f, 0.22f, -0.3f), new Vector3(-8f, -15f, 10f), Band.Black, 0.15f, 0.55f);
            m.Box(new Vector3(0.5f, 0.12f, 0.5f), 0.04f, new Vector3(0.12f, 0.76f, 0f), new Vector3(6f, 25f, 4f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.3f, 0.06f, 0.22f), 0.02f, new Vector3(-0.45f, 0.46f, -0.32f), new Vector3(-8f, -15f, 10f), Band.Yellow, 0.35f, 0.6f);
            return m.Build("MagmaRock");
        }

        // a pipe stub with a red valve wheel and a steam cap
        private static Mesh SteamValve()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.7f, 0.12f, 0.7f), 0.03f, new Vector3(0f, 0.06f, 0f), Vector3.zero, Band.Black, 0.1f, 0.45f);
            m.Cylinder(0.2f, 0.2f, 1.4f, 8, new Vector3(0f, 0.75f, 0f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            m.Cylinder(0.26f, 0.26f, 0.1f, 8, new Vector3(0f, 0.45f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Cylinder(0.26f, 0.26f, 0.1f, 8, new Vector3(0f, 1.3f, 0f), Vector3.zero, Band.Grey, 0.2f, 0.6f);
            m.Cylinder(0.34f, 0.34f, 0.06f, 10, new Vector3(0f, 1.0f, -0.3f), new Vector3(90f, 0f, 0f), Band.Red, 0.15f, 0.5f);
            m.Cylinder(0.08f, 0.08f, 0.12f, 6, new Vector3(0f, 1.0f, -0.25f), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.3f);
            m.Cylinder(0.16f, 0.24f, 0.18f, 8, new Vector3(0f, 1.55f, 0f), Vector3.zero, Band.Yellow, 0.25f, 0.6f);
            return m.Build("SteamValve");
        }

        // the world's accent prop: an iron brazier full of embers
        private static Mesh Brazier()
        {
            PaletteMesh m = new PaletteMesh();
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * 360f;
                Vector3 leg = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.28f);
                m.Box(new Vector3(0.07f, 0.7f, 0.07f), 0.01f, leg + new Vector3(0f, 0.35f, 0f), new Vector3(0f, a, 0f), Band.Black, 0.1f, 0.4f);
            }
            m.Cylinder(0.3f, 0.48f, 0.35f, 8, new Vector3(0f, 0.82f, 0f), Vector3.zero, Band.Black, 0.15f, 0.5f);
            m.Cylinder(0.42f, 0.42f, 0.06f, 8, new Vector3(0f, 0.98f, 0f), Vector3.zero, Band.Orange, 0.72f, 1f);
            m.Cylinder(0.26f, 0f, 0.55f, 6, new Vector3(0f, 1.28f, 0f), new Vector3(0f, 20f, 0f), Band.Orange, 0.72f, 1f);
            m.Cylinder(0.16f, 0f, 0.42f, 5, new Vector3(0.13f, 1.2f, 0.06f), new Vector3(0f, 0f, -18f), Band.Yellow, 0.35f, 0.6f);
            m.Cylinder(0.14f, 0f, 0.36f, 5, new Vector3(-0.14f, 1.17f, -0.05f), new Vector3(0f, 0f, 20f), Band.Yellow, 0.35f, 0.6f);
            return m.Build("Brazier");
        }

        // a big ladle of molten metal on a steel cradle
        private static Mesh Crucible()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.3f, 1.6f), 0.05f, new Vector3(0f, 0.15f, 0f), Vector3.zero, Band.Black, 0.1f, 0.45f);
            foreach (float x in new[] { -1.55f, 1.55f })
                m.Box(new Vector3(0.22f, 1.5f, 0.3f), 0.03f, new Vector3(x, 0.9f, 0f), Vector3.zero, Band.Yellow, 0.25f, 0.6f);
            m.Cylinder(0.08f, 0.08f, 3.3f, 6, new Vector3(0f, 1.25f, 0f), new Vector3(0f, 0f, 90f), Band.Grey, 0.4f, 0.85f);
            m.Cylinder(0.62f, 0.8f, 1.05f, 10, new Vector3(0f, 1.0f, 0f), Vector3.zero, Band.Grey, 0.1f, 0.6f);
            m.Cylinder(0.84f, 0.84f, 0.1f, 10, new Vector3(0f, 1.52f, 0f), Vector3.zero, Band.Black, 0.1f, 0.4f);
            m.Cylinder(0.74f, 0.74f, 0.06f, 10, new Vector3(0f, 1.55f, 0f), Vector3.zero, Band.Orange, 0.72f, 1f);
            m.Sphere(0.18f, 2, 6, new Vector3(0.25f, 1.6f, -0.2f), new Vector3(1f, 0.4f, 1f), Band.Yellow, 0.35f, 0.6f);
            m.Box(new Vector3(0.3f, 0.12f, 0.5f), 0.03f, new Vector3(0f, 1.48f, -0.85f), new Vector3(-20f, 0f, 0f), Band.Grey, 0.15f, 0.5f);
            m.Box(new Vector3(0.9f, 0.18f, 0.04f), 0.02f, new Vector3(-0.9f, 0.4f, -0.82f), Vector3.zero, Band.Yellow, 0.3f, 0.5f);
            return m.Build("Crucible");
        }

        // stacked steel ingots on a pallet, the top row still hot
        private static Mesh IngotPallet()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(3.6f, 0.14f, 1.55f), 0.03f, new Vector3(0f, 0.07f, 0f), Vector3.zero, Band.Brown, 0.3f, 0.7f);
            for (int i = 0; i < 5; i++)
            {
                float x = -1.4f + i * 0.7f;
                m.Box(new Vector3(0.6f, 0.26f, 1.3f), 0.06f, new Vector3(x, 0.28f, 0f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -1.05f + i * 0.7f;
                m.Box(new Vector3(0.6f, 0.26f, 1.2f), 0.06f, new Vector3(x, 0.54f, 0f), Vector3.zero, i == 1 ? Band.Orange : Band.Grey, i == 1 ? 0.72f : 0.1f, i == 1 ? 1f : 0.55f);
            }
            m.Box(new Vector3(0.6f, 0.26f, 1.1f), 0.06f, new Vector3(0.35f, 0.8f, 0f), Vector3.zero, Band.Orange, 0.72f, 1f);
            return m.Build("IngotPallet");
        }

        // a conveyor belt carrying ore chunks
        private static Mesh Conveyor()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (float x in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
                foreach (float z in new[] { -0.6f, 0.6f })
                    m.Box(new Vector3(0.12f, 0.75f, 0.12f), 0.02f, new Vector3(x, 0.38f, z), Vector3.zero, Band.Yellow, 0.25f, 0.6f);
            m.Box(new Vector3(3.7f, 0.3f, 1.5f), 0.08f, new Vector3(0f, 0.9f, 0f), Vector3.zero, Band.Grey, 0.1f, 0.55f);
            m.Box(new Vector3(3.6f, 0.06f, 1.2f), 0.02f, new Vector3(0f, 1.07f, 0f), Vector3.zero, Band.Black, 0f, 0.3f);
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(0.36f, 0.3f, 0.34f), 0.1f, new Vector3(-1.3f + i * 0.85f, 1.25f, (i % 2) * 0.3f - 0.15f), new Vector3(10f, i * 35f, 15f), i % 2 == 0 ? Band.Black : Band.Orange, 0.72f, 1f);
            return m.Build("Conveyor");
        }

        // low steel heat shields with hazard stripes
        private static Mesh HeatShield()
        {
            PaletteMesh m = new PaletteMesh();
            for (int i = 0; i < 3; i++)
            {
                float x = -1.2f + i * 1.2f;
                m.Box(new Vector3(1.1f, 0.7f, 0.22f), 0.04f, new Vector3(x, 0.35f, 0f), new Vector3(0f, (i - 1) * 6f, 0f), Band.Grey, 0.4f, 0.85f);
                m.Box(new Vector3(1.12f, 0.16f, 0.24f), 0.02f, new Vector3(x, 0.6f, 0f), new Vector3(0f, (i - 1) * 6f, 0f), Band.Yellow, 0.3f, 0.5f);
                for (int k = -1; k <= 1; k++)
                    m.Box(new Vector3(0.1f, 0.18f, 0.26f), 0.01f, new Vector3(x + k * 0.32f, 0.6f, 0f), new Vector3(0f, (i - 1) * 6f, 35f), Band.Black, 0f, 0.3f);
                m.Box(new Vector3(0.5f, 0.06f, 0.5f), 0.02f, new Vector3(x, 0.03f, 0.18f), Vector3.zero, Band.Black, 0f, 0.4f);
            }
            return m.Build("HeatShield");
        }

        private static Mesh SafetyRail()
        {
            PaletteMesh m = new PaletteMesh();
            foreach (float x in new[] { -0.82f, 0f, 0.82f })
                m.Cylinder(0.06f, 0.06f, 1.15f, 6, new Vector3(x, 0.58f, 0f), Vector3.zero, Band.Yellow, 0.25f, 0.6f);
            foreach (float y in new[] { 0.6f, 1.12f })
                m.Cylinder(0.05f, 0.05f, 1.75f, 6, new Vector3(0f, y, 0f), new Vector3(0f, 0f, 90f), Band.Yellow, 0.2f, 0.55f);
            m.Box(new Vector3(0.5f, 0.32f, 0.04f), 0.02f, new Vector3(0.4f, 0.86f, -0.05f), Vector3.zero, Band.Red, 0.15f, 0.5f);
            m.Box(new Vector3(0.12f, 0.12f, 0.03f), 0.01f, new Vector3(0.4f, 0.86f, -0.08f), new Vector3(0f, 0f, 45f), Band.White, 0f, 0.2f);
            return m.Build("SafetyRail");
        }

        // a rocky mound with a lava vent: the big centre piece of volcano rooms
        private static Mesh LavaVent()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(1.8f, 1.35f, 0.6f, 8, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 12f, 0f), Band.Black, 0.1f, 0.55f);
            m.Cylinder(1.2f, 0.75f, 0.6f, 8, new Vector3(0f, 0.85f, 0f), new Vector3(0f, 30f, 0f), Band.Black, 0.15f, 0.55f);
            m.Cylinder(0.62f, 0.62f, 0.08f, 8, new Vector3(0f, 1.16f, 0f), Vector3.zero, Band.Orange, 0.72f, 1f);
            m.Cylinder(0.32f, 0f, 0.5f, 6, new Vector3(0f, 1.4f, 0f), new Vector3(0f, 15f, 0f), Band.Yellow, 0.35f, 0.6f);
            m.Box(new Vector3(0.12f, 0.08f, 1.1f), 0.02f, new Vector3(0.9f, 0.45f, -0.5f), new Vector3(0f, -35f, 25f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.12f, 0.08f, 0.9f), 0.02f, new Vector3(-0.85f, 0.4f, 0.6f), new Vector3(0f, 50f, -25f), Band.Orange, 0.72f, 1f);
            m.Box(new Vector3(0.6f, 0.5f, 0.55f), 0.15f, new Vector3(-1.2f, 0.25f, -1.1f), new Vector3(0f, 20f, 8f), Band.Grey, 0.15f, 0.55f);
            return m.Build("LavaVent");
        }

        private static Mesh FactoryWall(float width)
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(width, 1.62f, 0.45f), 0.05f, new Vector3(0f, 0.81f, 0.24f), Vector3.zero, Band.Grey, 0.4f, 0.85f);
            m.Box(new Vector3(width + 0.02f, 0.22f, 0.47f), 0.02f, new Vector3(0f, 0.25f, 0.24f), Vector3.zero, Band.Yellow, 0.3f, 0.5f);
            for (float x = -width / 2f + 0.2f; x < width / 2f - 0.1f; x += 0.38f)
                m.Box(new Vector3(0.1f, 0.26f, 0.49f), 0.01f, new Vector3(x, 0.25f, 0.24f), new Vector3(0f, 0f, 35f), Band.Black, 0f, 0.3f);
            for (float x = -width / 2f + 0.3f; x < width / 2f - 0.2f; x += 0.6f)
                m.Sphere(0.04f, 2, 6, new Vector3(x, 1.4f, 0f), Vector3.one, Band.Grey, 0.2f, 0.5f);
            m.Box(new Vector3(width - 0.06f, 0.12f, 0.5f), 0.04f, new Vector3(0f, 1.66f, 0.24f), Vector3.zero, Band.Black, 0.05f, 0.4f);
            return m.Build("FactoryWall");
        }

        private static Mesh GirderPost()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.46f, 2.05f, 0.12f), 0.02f, new Vector3(-0.25f, 1.03f, 0.08f), Vector3.zero, Band.Red, 0.15f, 0.55f);
            m.Box(new Vector3(0.46f, 2.05f, 0.12f), 0.02f, new Vector3(-0.25f, 1.03f, 0.42f), Vector3.zero, Band.Red, 0.15f, 0.55f);
            m.Box(new Vector3(0.1f, 2.05f, 0.3f), 0.01f, new Vector3(-0.25f, 1.03f, 0.25f), Vector3.zero, Band.Red, 0.3f, 0.7f);
            m.Box(new Vector3(0.52f, 0.12f, 0.52f), 0.03f, new Vector3(-0.25f, 2.1f, 0.25f), Vector3.zero, Band.Black, 0.1f, 0.4f);
            return m.Build("GirderPost");
        }

        // walkable hazard: a flat pool of lava in a rock rim (the trigger burns the player, see LavaPoolBehaviour)
        private static Mesh LavaPoolMesh()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.92f, 0.86f, 0.08f, 9, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 12f, 0f), Band.Black, 0.1f, 0.5f);
            m.Cylinder(0.76f, 0.76f, 0.04f, 9, new Vector3(0f, 0.07f, 0f), new Vector3(0f, 12f, 0f), Band.Orange, 0.72f, 1f);
            m.Cylinder(0.42f, 0.42f, 0.03f, 7, new Vector3(0.12f, 0.09f, -0.08f), new Vector3(0f, 40f, 0f), Band.Yellow, 0.35f, 0.6f);
            m.Sphere(0.09f, 2, 6, new Vector3(-0.35f, 0.1f, 0.25f), new Vector3(1f, 0.5f, 1f), Band.Yellow, 0.35f, 0.6f);
            m.Sphere(0.07f, 2, 6, new Vector3(0.38f, 0.1f, 0.3f), new Vector3(1f, 0.5f, 1f), Band.Yellow, 0.35f, 0.6f);
            foreach (Vector3 p in new[] { new Vector3(0.85f, 0f, 0.2f), new Vector3(-0.7f, 0f, -0.55f), new Vector3(-0.2f, 0f, 0.85f) })
                m.Box(new Vector3(0.28f, 0.2f, 0.25f), 0.07f, p + new Vector3(0f, 0.08f, 0f), new Vector3(5f, p.x * 60f, 8f), Band.Black, 0.15f, 0.55f);
            return m.Build("LavaPool");
        }

        public const string LAVA_POOL = "Lava Pool";

        private static void AddLavaPool(int number)
        {
            string folder = $"{PRESETS}/World_{number:00}";
            EnsureFolder(folder + "/Hazards");
            string path = $"{folder}/Hazards/{LAVA_POOL}.prefab";

            GameObject root = new GameObject(LAVA_POOL);
            // "Default" ignores the player in the collision matrix; the enemy-bullet layer only meets the player
            root.layer = LayerMask.NameToLayer("Enemy Bullet");
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = SaveMesh(LavaPoolMesh(), $"{folder}/Meshes", $"{number}_Lava_Pool");
            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ENV_MATERIALS}/Environment {number} Props.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.5f, 0.8f, 1.5f);
            trigger.center = new Vector3(0f, 0.4f, 0f);
            root.AddComponent<LavaPoolBehaviour>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            // register as an environment item (not an obstacle: it does not block movement or bullets)
            WorldData world = AssetDatabase.LoadAssetAtPath<WorldData>($"{WORLDS}/World_{number:00}.asset");
            SerializedObject serialized = new SerializedObject(world);
            SerializedProperty items = serialized.FindProperty("items");
            for (int i = 0; i < items.arraySize; i++)
                if (items.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab) return;

            items.InsertArrayElementAtIndex(items.arraySize);
            SerializedProperty item = items.GetArrayElementAtIndex(items.arraySize - 1);
            item.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            item.FindPropertyRelative("type").intValue = (int)LevelItemType.Environment;
            item.FindPropertyRelative("hash").intValue = 0x1A7A0000 + number;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
        }
        #endregion

        #region Helpers
        private static Mesh SaveMesh(Mesh mesh, string folder, string name)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                // CopySerialized leaves the GPU copy stale: previews in this session would still show the old mesh
                existing.vertices = existing.vertices;
                existing.uv = existing.uv;
                existing.MarkModified();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        #endregion
    }
}
