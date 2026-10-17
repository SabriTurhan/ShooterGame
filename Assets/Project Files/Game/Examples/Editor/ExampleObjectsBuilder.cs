using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Watermelon.SquadShooter.Examples
{
    // Generates the example character (Frost), enemy (Riot Brute) and weapon (Cryo Blaster)
    // in the game's art style and lays them out in the ExampleObjects scene.
    public static class ExampleObjectsBuilder
    {
        private const string GAME = "Assets/Project Files/Game";
        private const string ROOT = GAME + "/Examples";
        private const string MESHES = ROOT + "/Meshes";
        private const string PREFABS = ROOT + "/Prefabs";
        private const string SCENE_PATH = ROOT + "/ExampleObjects.unity";

        private const string PLAYER_MATERIAL = GAME + "/Materials/Characters/Player.mat";
        private const string PLAYER_ATLAS = GAME + "/Textures/Characters/Player/player_atlas.png";

        private const string JIM_PREFAB = GAME + "/Prefabs/Characters/Player/Graphics/Character 1.prefab";
        private const string MELEE_PREFAB = GAME + "/Prefabs/Characters/Enemy/Enemy Melee.prefab";

        [MenuItem("Tools/Examples/Build Example Objects")]
        public static void BuildAll()
        {
            EnsureFolder(ROOT);
            EnsureFolder(MESHES);
            EnsureFolder(PREFABS);

            GameObject gun = BuildCryoBlaster();
            GameObject bullet = BuildCryoShard();
            GameObject frost = BuildFrost();
            GameObject riot = BuildRiotBrute();

            AssetDatabase.SaveAssets();

            BuildScene(frost, riot, gun, bullet);
        }

        #region Weapon
        private static GameObject BuildCryoBlaster()
        {
            PaletteMesh mesh = new PaletteMesh();

            // body
            mesh.Box(new Vector3(0.22f, 0.24f, 0.50f), 0.045f, new Vector3(0f, 0.15f, 0.02f), Vector3.zero, Band.Navy, 0.1f, 0.6f);
            mesh.Box(new Vector3(0.24f, 0.07f, 0.30f), 0.02f, new Vector3(0f, 0.05f, 0.08f), Vector3.zero, Band.Steel, 0.2f, 0.6f);

            // cryo tank with glowing rings
            mesh.Cylinder(0.105f, 0.105f, 0.36f, 10, new Vector3(0f, 0.34f, -0.02f), new Vector3(90f, 0f, 0f), Band.Ice, 0f, 0.35f);
            mesh.Cylinder(0.115f, 0.115f, 0.045f, 10, new Vector3(0f, 0.34f, 0.10f), new Vector3(90f, 0f, 0f), Band.Teal, 0f, 0.3f);
            mesh.Cylinder(0.115f, 0.115f, 0.045f, 10, new Vector3(0f, 0.34f, -0.14f), new Vector3(90f, 0f, 0f), Band.Teal, 0f, 0.3f);
            mesh.Cylinder(0.075f, 0.075f, 0.05f, 10, new Vector3(0f, 0.34f, 0.18f), new Vector3(90f, 0f, 0f), Band.Steel, 0.3f, 0.7f);

            // barrel, cooling fins and muzzle
            mesh.Cylinder(0.07f, 0.07f, 0.32f, 8, new Vector3(0f, 0.15f, 0.40f), new Vector3(90f, 0f, 0f), Band.Steel, 0.15f, 0.55f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f;
                Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 0.085f, 0f);
                mesh.Box(new Vector3(0.025f, 0.09f, 0.20f), 0.008f, new Vector3(0f, 0.15f, 0.40f) + offset, new Vector3(0f, 0f, angle), Band.Ice, 0f, 0.3f);
            }
            mesh.Cylinder(0.075f, 0.115f, 0.09f, 8, new Vector3(0f, 0.15f, 0.60f), new Vector3(90f, 0f, 0f), Band.Teal, 0f, 0.4f);

            // grip, stock and orange accents
            mesh.Box(new Vector3(0.085f, 0.20f, 0.10f), 0.02f, new Vector3(0f, -0.02f, -0.07f), new Vector3(15f, 0f, 0f), Band.Black, 0f, 0.4f);
            mesh.Box(new Vector3(0.18f, 0.19f, 0.10f), 0.03f, new Vector3(0f, 0.15f, -0.27f), Vector3.zero, Band.Black, 0f, 0.5f);
            mesh.Cylinder(0.035f, 0.035f, 0.16f, 8, new Vector3(0.125f, 0.17f, 0.06f), new Vector3(90f, 0f, 0f), Band.Yellow, 0.3f, 0.8f);
            mesh.Cylinder(0.045f, 0.045f, 0.02f, 8, new Vector3(0.115f, 0.21f, -0.15f), new Vector3(0f, 0f, 90f), Band.White, 0f, 0.2f);

            Mesh asset = SaveMesh(mesh.Build("CryoBlaster_mesh"), "CryoBlaster_mesh");

            GameObject root = new GameObject("Gun Cryo");
            CreateMeshChild(root.transform, "Cryoblaster", asset);

            return SavePrefab(root, "Gun Cryo");
        }

        private static GameObject BuildCryoShard()
        {
            PaletteMesh mesh = new PaletteMesh();
            mesh.Sphere(0.12f, 2, 6, Vector3.zero, new Vector3(0.8f, 0.8f, 2.4f), Band.Ice, 0f, 0.3f);
            mesh.Sphere(0.07f, 2, 6, new Vector3(0f, 0f, -0.2f), new Vector3(0.8f, 0.8f, 2f), Band.Sky, 0f, 0.3f);

            Mesh asset = SaveMesh(mesh.Build("CryoShard_mesh"), "CryoShard_mesh");

            GameObject root = new GameObject("Cryo Bullet");
            CreateMeshChild(root.transform, "Shard", asset);

            return SavePrefab(root, "Cryo Bullet");
        }
        #endregion

        #region Character
        private static GameObject BuildFrost()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(JIM_PREFAB));
            instance.name = "Character 4 Frost";

            SkinnedMeshRenderer body = instance.GetComponentInChildren<SkinnedMeshRenderer>();

            // Jim's outfit re-dyed: black vest -> teal parka, red scarf -> ice white, boots stay black
            Mesh mesh = UnityEngine.Object.Instantiate(body.sharedMesh);
            mesh.name = "Main_Char_Frost";
            RemapBands(mesh, body.bones, (band, bone) =>
            {
                bool boots = bone.Contains("Foot") || bone.Contains("Toe") || (bone.EndsWith("Leg") && !bone.EndsWith("UpLeg"));
                if (band == Band.Black && !boots) return Band.Teal;
                if (band == Band.Red) return Band.Ice;
                if (band == Band.Brown) return Band.Navy;
                if (band == Band.Navy) return Band.Steel;

                return band;
            });
            body.sharedMesh = SaveMesh(mesh, "Main_Char_Frost");

            Bounds head = BoneBounds(body, bone => bone.EndsWith("Head") || bone.Contains("HeadTop"));
            Bounds chest = BoneBounds(body, bone => bone.EndsWith("Spine2") || bone.EndsWith("Spine1"));
            float r = head.extents.x;
            Vector3 c = head.center;

            // beanie with fur fold, pompom and snow goggles
            PaletteMesh beanie = new PaletteMesh();
            beanie.Cylinder(r * 1.06f, r * 0.80f, r * 0.55f, 10, c + new Vector3(0f, r * 0.55f, 0f), Vector3.zero, Band.Teal, 0.05f, 0.45f);
            beanie.Dome(r * 0.80f, 3, 10, c + new Vector3(0f, r * 0.82f, 0f), Vector3.zero, new Vector3(1f, 0.6f, 1f), Band.Teal, 0f, 0.3f);
            beanie.Cylinder(r * 1.12f, r * 1.12f, r * 0.30f, 10, c + new Vector3(0f, r * 0.30f, 0f), Vector3.zero, Band.Ice, 0f, 0.35f);
            beanie.Sphere(r * 0.30f, 3, 8, c + new Vector3(0f, r * 1.42f, 0f), Vector3.one, Band.Yellow, 0.35f, 0.85f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 lens = c + new Vector3(side * r * 0.36f, r * 0.42f, r * 1.04f);
                beanie.Cylinder(r * 0.30f, r * 0.30f, r * 0.10f, 8, lens - new Vector3(0f, 0f, r * 0.04f), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
                beanie.Cylinder(r * 0.22f, r * 0.22f, r * 0.10f, 8, lens + new Vector3(0f, 0f, r * 0.03f), new Vector3(90f, 0f, 0f), Band.Yellow, 0.4f, 0.9f);
            }
            AttachToBone(body, "Head", "Frost Beanie", beanie.Build("Frost_Beanie"));

            // cryo backpack
            Vector3 back = new Vector3(chest.center.x, chest.center.y + chest.extents.y * 0.2f, chest.min.z);
            PaletteMesh pack = new PaletteMesh();
            pack.Box(new Vector3(0.36f, 0.42f, 0.18f), 0.04f, back + new Vector3(0f, 0f, -0.08f), Vector3.zero, Band.Navy, 0.1f, 0.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tank = back + new Vector3(side * 0.085f, -0.03f, -0.2f);
                pack.Cylinder(0.072f, 0.072f, 0.30f, 8, tank, Vector3.zero, Band.Ice, 0f, 0.35f);
                pack.Dome(0.072f, 2, 8, tank + new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(1f, 0.6f, 1f), Band.Ice, 0f, 0.2f);
                pack.Cylinder(0.08f, 0.08f, 0.045f, 8, tank + new Vector3(0f, 0.05f, 0f), Vector3.zero, Band.Teal, 0f, 0.3f);
            }
            pack.Box(new Vector3(0.38f, 0.06f, 0.2f), 0.015f, back + new Vector3(0f, -0.12f, -0.08f), Vector3.zero, Band.Yellow, 0.4f, 0.8f);
            AttachToBone(body, "Spine2", "Frost Backpack", pack.Build("Frost_Backpack"));

            return SavePrefab(instance, "Character 4 Frost");
        }
        #endregion

        #region Enemy
        private static GameObject BuildRiotBrute()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MELEE_PREFAB));
            instance.name = "Enemy Riot Brute";

            Material playerMaterial = AssetDatabase.LoadAssetAtPath<Material>(PLAYER_MATERIAL);
            Texture2D enemyAtlas = null;
            Texture2D palette = ReadableCopy(AssetDatabase.LoadAssetAtPath<Texture2D>(PLAYER_ATLAS));

            SkinnedMeshRenderer cloth = null;
            foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name.Contains("Cloth")) cloth = renderer;

            enemyAtlas = ReadableCopy((Texture2D)cloth.sharedMaterial.mainTexture);

            Bounds head = BoneBounds(cloth, bone => bone.EndsWith("Head") || bone.Contains("HeadTop"));

            // riot gear: navy armour, steel pads, hazard-yellow trims
            Mesh clothMesh = UnityEngine.Object.Instantiate(cloth.sharedMesh);
            clothMesh.name = "Riot_Cloth";
            RepaintFromAtlas(clothMesh, cloth.bones, enemyAtlas, palette, (band, bone, colour) =>
            {
                Color.RGBToHSV(colour, out float h, out float s, out float v);

                bool torso = bone.Contains("Spine") || bone.EndsWith("Hips");
                bool trim = bone.Contains("Head") || bone.Contains("Arm") || bone.Contains("Hand");

                if (s > 0.35f && h > 0.22f && h < 0.5f) return trim ? Band.Yellow : Band.Navy;  // mohawk + wrist bands / green trousers
                if (v < 0.3f) return Band.Black;                                                  // vest, boots
                if (s < 0.15f) return Band.Steel;                                                 // grey pads
                if (h < 0.15f && v > 0.75f) return torso ? Band.Steel : band;                    // bare chest gets a chest plate
                return Band.Navy;                                                                  // fabric
            });
            FlattenMohawk(clothMesh, cloth, enemyAtlas);
            cloth.sharedMesh = SaveMesh(clothMesh, "Riot_Cloth");
            cloth.sharedMaterial = playerMaterial;

            // the bat becomes a black baton with a yellow grip
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh weapon = UnityEngine.Object.Instantiate(filter.sharedMesh);
                weapon.name = "Riot_Baton";
                RepaintFromAtlas(weapon, null, enemyAtlas, palette, (band, bone, colour) => colour.grayscale > 0.55f ? Band.Yellow : Band.Black);
                filter.sharedMesh = SaveMesh(weapon, "Riot_Baton");

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = playerMaterial;
                renderer.sharedMaterials = materials;
            }

            float r = head.extents.x;
            Vector3 c = head.center;

            PaletteMesh helmet = new PaletteMesh();
            helmet.Dome(r * 1.18f, 3, 10, c + new Vector3(0f, -r * 0.05f, 0f), Vector3.zero, new Vector3(1f, 1.05f, 1.05f), Band.Navy, 0.05f, 0.55f);
            helmet.Cylinder(r * 1.22f, r * 1.22f, r * 0.12f, 10, c + new Vector3(0f, -r * 0.05f, 0f), Vector3.zero, Band.Yellow, 0.35f, 0.75f);
            helmet.Box(new Vector3(r * 1.7f, r * 0.62f, r * 0.22f), r * 0.08f, c + new Vector3(0f, -r * 0.32f, r * 1.0f), new Vector3(-8f, 0f, 0f), Band.Black, 0f, 0.35f);
            helmet.Box(new Vector3(r * 1.5f, r * 0.10f, r * 0.08f), r * 0.03f, c + new Vector3(0f, -r * 0.12f, r * 1.13f), new Vector3(-8f, 0f, 0f), Band.Sky, 0f, 0.2f);
            for (int side = -1; side <= 1; side += 2)
                helmet.Cylinder(r * 0.30f, r * 0.30f, r * 0.16f, 8, c + new Vector3(side * r * 1.15f, -r * 0.2f, 0f), new Vector3(0f, 0f, 90f), Band.Steel, 0.2f, 0.6f);
            AttachToBone(cloth, "Head", "Riot Helmet", helmet.Build("Riot_Helmet"));

            // riot shield, curved out of three angled panels, hazard stripe at the bottom
            PaletteMesh shield = new PaletteMesh();
            const float W = 0.24f, H = 0.95f, T = 0.055f;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 offset = new Vector3(i * W * 0.96f, 0f, -Mathf.Abs(i) * 0.05f);
                shield.Box(new Vector3(W, H, T), 0.02f, offset, new Vector3(0f, i * 14f, 0f), Band.Steel, 0.05f, 0.45f);
            }
            shield.Box(new Vector3(W * 1.5f, 0.11f, 0.03f), 0.01f, new Vector3(0f, H * 0.3f, T * 0.5f), Vector3.zero, Band.Black, 0f, 0.3f);
            shield.Box(new Vector3(W * 0.98f, 0.16f, T + 0.012f), 0.01f, new Vector3(0f, -H * 0.38f, 0f), Vector3.zero, Band.Yellow, 0.3f, 0.6f);
            for (int i = -2; i <= 2; i++)
                shield.Box(new Vector3(0.04f, 0.14f, T + 0.02f), 0.005f, new Vector3(i * 0.065f, -H * 0.38f, 0f), new Vector3(0f, 0f, 40f), Band.Black, 0f, 0.3f);
            shield.Box(new Vector3(0.28f, 0.11f, 0.03f), 0.01f, new Vector3(0f, H * 0.12f, T * 0.5f + 0.005f), Vector3.zero, Band.White, 0f, 0.2f);
            shield.Box(new Vector3(0.08f, 0.3f, 0.1f), 0.02f, new Vector3(0f, 0f, -T - 0.04f), Vector3.zero, Band.Black, 0f, 0.4f);
            Mesh shieldMesh = SaveMesh(shield.Build("Riot_Shield"), "Riot_Shield");

            // place the shield in front of the body using the idle pose, then let it follow the left forearm
            Animator animator = instance.GetComponentInChildren<Animator>();
            AnimationClip idle = FindClip(animator, "Idle");
            if (idle != null) idle.SampleAnimation(animator.gameObject, 0f);

            Bounds body = BoneBounds(cloth, bone => true);
            // the shield goes on the forearm that is not holding the baton
            Transform leftArm = FindBone(cloth, "LeftForeArm"), rightArm = FindBone(cloth, "RightForeArm");
            MeshRenderer baton = null;
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.Contains("Weapon")) baton = renderer;
            Transform forearm = leftArm;
            if (baton != null && leftArm != null && rightArm != null)
                forearm = Vector3.Distance(baton.bounds.center, leftArm.position) < Vector3.Distance(baton.bounds.center, rightArm.position) ? rightArm : leftArm;
            GameObject shieldObject = new GameObject("Riot Shield");
            // shield sits on the left-arm side, angled outwards so the brute stays readable behind it
            float shieldSide = forearm != null ? Mathf.Sign(forearm.position.x - body.center.x) : 1f;
            float armX = forearm != null ? forearm.position.x : body.center.x + body.extents.x * 0.5f;
            shieldObject.transform.position = new Vector3(Mathf.Lerp(body.center.x, armX, 0.85f), body.center.y - body.extents.y * 0.12f, body.max.z + 0.08f);
            shieldObject.transform.rotation = Quaternion.Euler(0f, shieldSide * 28f, 0f);
            shieldObject.AddComponent<MeshFilter>().sharedMesh = shieldMesh;
            shieldObject.AddComponent<MeshRenderer>().sharedMaterial = playerMaterial;
            shieldObject.transform.SetParent(forearm != null ? forearm : instance.transform, true);

            instance.transform.localScale = Vector3.one * 1.15f;

            return SavePrefab(instance, "Enemy Riot Brute");
        }

        private static void FlattenMohawk(Mesh mesh, SkinnedMeshRenderer renderer, Texture2D atlas)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = renderer.sharedMesh.uv;
            BoneWeight[] weights = mesh.boneWeights;

            Vector3 headCenter = Vector3.zero;
            int count = 0;
            List<int> hair = new List<int>();

            for (int i = 0; i < vertices.Length; i++)
            {
                string bone = renderer.bones[weights[i].boneIndex0].name;
                if (!bone.EndsWith("Head") && !bone.Contains("HeadTop")) continue;

                headCenter += vertices[i];
                count++;

                Color.RGBToHSV(atlas.GetPixelBilinear(uv[i].x, uv[i].y), out float h, out float s, out float v);
                if (s > 0.35f && h > 0.22f && h < 0.5f) hair.Add(i);
            }

            if (count == 0) return;
            headCenter /= count;

            foreach (int i in hair) vertices[i] = Vector3.Lerp(vertices[i], headCenter, 0.55f);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
        #endregion

        #region Scene
        private static void BuildScene(GameObject frost, GameObject riot, GameObject gun, GameObject bullet)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4056604f, 0.4056604f, 0.4056604f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(59f, 25f, 25f);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.427f, 0.694f, 0.969f);
            camera.fieldOfView = 40f;
            cameraObject.transform.position = new Vector3(0f, 6.2f, -11.5f);
            cameraObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            BuildArena();

            Transform examples = new GameObject("EXAMPLES").transform;
            Quaternion facing = Quaternion.Euler(0f, 180f, 0f);

            // reference (existing) vs new, side by side
            GameObject jim = Place(AssetDatabase.LoadAssetAtPath<GameObject>(JIM_PREFAB), examples, new Vector3(-6.6f, 0f, 0f), facing);
            GameObject newFrost = Place(frost, examples, new Vector3(-3.9f, 0f, 0f), facing);
            GameObject melee = Place(AssetDatabase.LoadAssetAtPath<GameObject>(MELEE_PREFAB), examples, new Vector3(3.9f, 0f, 0f), facing);
            GameObject newRiot = Place(riot, examples, new Vector3(6.7f, 0f, 0f), facing);

            PoseCharacter(jim, "Shot Idle");
            PoseCharacter(newFrost, "Shot Idle");
            PoseCharacter(melee, "Idle", 0f);
            PoseCharacter(newRiot, "Idle", 0f);
            DisableGameplayVisuals(examples.gameObject);

            // enemy weapons are snapped to the hand by WeaponRigBehavior, which also runs in the editor once initialised
            foreach (GameObject enemy in new[] { melee, newRiot })
            {
                BaseEnemyBehavior behaviour = enemy.GetComponent<BaseEnemyBehavior>();
                foreach (WeaponRigBehavior rig in enemy.GetComponentsInChildren<WeaponRigBehavior>(true))
                {
                    rig.Init(behaviour);
                    typeof(WeaponRigBehavior).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(rig, null);
                }
            }

            AttachGun(newFrost, gun);
            AttachGun(jim, AssetDatabase.LoadAssetAtPath<GameObject>(GAME + "/Prefabs/Weapons/Gun Tesla.prefab"));

            foreach (GameObject character in new[] { jim, newFrost })
                Place(AssetDatabase.LoadAssetAtPath<GameObject>(GAME + "/Prefabs/Other/Pedestal.prefab"), examples, character.transform.position + Vector3.down * 0.02f, Quaternion.identity);

            // weapon showcase
            GameObject bigGun = Place(gun, examples, new Vector3(0f, 1.15f, -0.6f), Quaternion.Euler(0f, -120f, 0f));
            bigGun.transform.localScale = Vector3.one * 2.4f;
            GameObject shard = Place(bullet, examples, new Vector3(0.05f, 0.45f, -1.9f), Quaternion.Euler(0f, -60f, 0f));
            shard.transform.localScale = Vector3.one * 2f;

            Label(examples, "MEVCUT: JIM", new Vector3(-6.6f, 2.4f, 0f), 0.6f, Color.white);
            Label(examples, "YENI KARAKTER\nFROST", new Vector3(-3.9f, 2.65f, 0f), 0.9f, new Color(0.55f, 1f, 0.95f));
            Label(examples, "YENI SILAH\nCRYO BLASTER", new Vector3(0f, 3.25f, -0.6f), 0.9f, new Color(0.7f, 0.9f, 1f));
            Label(examples, "MEVCUT: BAT MELEE", new Vector3(3.9f, 2.4f, 0f), 0.6f, Color.white);
            Label(examples, "YENI DUSMAN\nRIOT BRUTE", new Vector3(6.7f, 2.95f, 0f), 0.9f, new Color(1f, 0.85f, 0.35f));

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
        }

        private static void BuildArena()
        {
            string presets = GAME + "/Prefabs/Levels/Presets/World_01/";
            Transform arena = new GameObject("ARENA (World 1)").transform;

            // ground tiles are 7.8 x 7.8
            for (int x = -2; x <= 2; x++)
                for (int z = -1; z <= 1; z++)
                    Place(AssetDatabase.LoadAssetAtPath<GameObject>(presets + "Environment/Ground.prefab"), arena, new Vector3(x * 7.8f, 0f, z * 7.8f + 2f), Quaternion.identity);

            (string name, Vector3 position, float angle)[] props =
            {
                ("Obstacles/Block 1x1 Medium", new Vector3(-9.5f, 0f, 4.5f), 0f),
                ("Obstacles/Block 1x2 Big", new Vector3(9.8f, 0f, 4.8f), 90f),
                ("Obstacles/Block 1x1 Small", new Vector3(-1.6f, 0f, 5.2f), 20f),
            };

            foreach (var prop in props)
                Place(AssetDatabase.LoadAssetAtPath<GameObject>(presets + prop.name + ".prefab"), arena, prop.position, Quaternion.Euler(0f, prop.angle, 0f));
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return new GameObject("missing");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);

            return instance;
        }

        private static void PoseCharacter(GameObject character, string clipName, float normalizedTime = 0.25f)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null) return;

            AnimationClip clip = FindClip(animator, clipName);
            if (clip != null) clip.SampleAnimation(animator.gameObject, clip.length * normalizedTime);
        }

        private static void AttachGun(GameObject character, GameObject gunPrefab)
        {
            Transform holder = FindDeep(character.transform, "Tesla Holder");
            if (holder == null || gunPrefab == null) return;

            GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab, holder);
            gun.transform.localPosition = Vector3.zero;
            gun.transform.localRotation = Quaternion.identity;
            gun.transform.localScale = Vector3.one;
        }

        // health bars, aiming lines and particle systems are driven at runtime; hide them in the showcase
        private static void DisableGameplayVisuals(GameObject root)
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
            foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.playOnAwake = false;
            }
        }

        private static void Label(Transform parent, string text, Vector3 position, float size, Color color)
        {
            GameObject labelObject = new GameObject("Label " + text.Replace("\n", " "));
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GAME + "/Fonts/FredokaOne/Fredoka One 120/FredokaOne 120 Outline.asset");
            label.text = text;
            label.fontSize = size * 4f;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
        }
        #endregion

        #region Mesh helpers
        // Moves vertices between palette bands of the player atlas (keeps the shade)
        public static void RemapBands(Mesh mesh, Transform[] bones, Func<int, string, int> remap)
        {
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            int[] source = new int[uv.Length];
            int[] target = new int[uv.Length];

            for (int i = 0; i < uv.Length; i++)
            {
                source[i] = Band.FromUV(uv[i]);
                string bone = bones != null && weights.Length > i ? bones[weights[i].boneIndex0].name : string.Empty;
                target[i] = remap(source[i], bone);
            }

            for (int i = 0; i < uv.Length; i++)
                if (target[i] != source[i]) uv[i] = Band.ShiftBand(uv[i], source[i], target[i]);

            // a triangle whose corners land in different bands would smear across every band in between,
            // so it gets its own vertices, all moved into the band most of its corners went to
            List<Vector3> vertices = new List<Vector3>(mesh.vertices);
            List<Vector3> normals = new List<Vector3>(mesh.normals);
            List<Vector4> tangents = new List<Vector4>(mesh.tangents);
            List<BoneWeight> boneWeights = new List<BoneWeight>(weights);
            List<Vector2> uvList = new List<Vector2>(uv);
            Vector2[] originalUV = mesh.uv;
            int[] triangles = mesh.triangles;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (target[a] == target[b] && target[b] == target[c]) continue;

                int band = target[a] == target[b] || target[a] == target[c] ? target[a] : target[b] == target[c] ? target[b] : target[a];

                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[t + k];

                    vertices.Add(vertices[index]);
                    if (normals.Count > index) normals.Add(normals[index]);
                    if (tangents.Count > index) tangents.Add(tangents[index]);
                    if (boneWeights.Count > index) boneWeights.Add(boneWeights[index]);
                    uvList.Add(Band.ShiftBand(originalUV[index], source[index], band));

                    triangles[t + k] = vertices.Count - 1;
                }
            }

            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
            if (boneWeights.Count == vertices.Count) mesh.boneWeights = boneWeights.ToArray();
            mesh.SetUVs(0, uvList);
            mesh.triangles = triangles;
        }

        // Re-maps a mesh textured with another atlas onto the player palette: nearest palette colour first,
        // then the remap callback picks the final band; the shade of the original colour is preserved
        private static void RepaintFromAtlas(Mesh mesh, Transform[] bones, Texture2D source, Texture2D palette, Func<int, string, Color, int> remap)
        {
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;

            for (int i = 0; i < uv.Length; i++)
            {
                Color colour = source.GetPixelBilinear(uv[i].x, uv[i].y);
                NearestSwatch(palette, colour, out int band, out float shade);

                string bone = bones != null && weights.Length > i ? bones[weights[i].boneIndex0].name : string.Empty;
                int target = remap(band, bone, colour);

                uv[i] = Band.UV(target, target == band ? shade : Mathf.Clamp(1f - colour.grayscale, 0.05f, 0.85f));
            }

            mesh.uv = uv;
        }

        private static void NearestSwatch(Texture2D palette, Color colour, out int bestBand, out float bestShade)
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
                    Color swatch = palette.GetPixelBilinear(uv.x, uv.y);
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

        private static Bounds BoneBounds(SkinnedMeshRenderer renderer, Func<string, bool> filter)
        {
            Mesh baked = new Mesh();
            renderer.BakeMesh(baked, true);

            Vector3[] vertices = baked.vertices;
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            Matrix4x4 toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);

            bool initialised = false;
            Bounds bounds = new Bounds();

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!filter(renderer.bones[weights[i].boneIndex0].name)) continue;

                Vector3 point = toWorld.MultiplyPoint3x4(vertices[i]);
                if (!initialised)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialised = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }

            UnityEngine.Object.DestroyImmediate(baked);

            return bounds;
        }

        private static void AttachToBone(SkinnedMeshRenderer renderer, string boneName, string objectName, Mesh mesh)
        {
            Transform bone = FindBone(renderer, boneName);
            Mesh asset = SaveMesh(mesh, mesh.name);

            GameObject attachment = new GameObject(objectName);
            attachment.AddComponent<MeshFilter>().sharedMesh = asset;
            attachment.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PLAYER_MATERIAL);

            // the mesh is authored in world space at the bind pose, so keep the world placement when parenting
            attachment.transform.SetParent(bone, true);
        }

        private static Transform FindBone(SkinnedMeshRenderer renderer, string boneName)
        {
            foreach (Transform bone in renderer.bones)
                if (bone != null && (bone.name == boneName || bone.name.EndsWith(":" + boneName))) return bone;

            foreach (Transform bone in renderer.bones)
                if (bone != null && bone.name.EndsWith(boneName)) return bone;

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;

            return null;
        }

        private static AnimationClip FindClip(Animator animator, string contains)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return null;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == contains) return clip;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name.Contains(contains)) return clip;

            return null;
        }

        private static Texture2D ReadableCopy(Texture2D texture)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, temporary);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            return copy;
        }
        #endregion

        #region Asset helpers
        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MESHES + "/" + name + ".asset";
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

        private static void CreateMeshChild(Transform parent, string name, Mesh mesh)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PLAYER_MATERIAL);
        }

        private static GameObject SavePrefab(GameObject instance, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PREFABS + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(instance);

            return prefab;
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
