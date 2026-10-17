using System;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter.Examples;

namespace Watermelon.SquadShooter
{
    // Turns the approved example content into real game content: weapons/characters get their data assets,
    // gameplay prefabs and menu art, and are registered in the databases. Balance values come from
    // Tools/Balance (run Import Balance afterwards, which this does automatically).
    public static class ContentBuilder
    {
        private const string GAME = "Assets/Project Files/Game";
        private const string DATA = "Assets/Project Files/Data";
        private const string EXAMPLES = GAME + "/Examples";

        [MenuItem("Tools/Content/Build World 3 Content (Cryo Gun, Frost, art)")]
        public static void BuildWorld3Content()
        {
            WeaponData cryo = BuildCryoBlaster();
            CharacterData frost = BuildFrost();
            RenderWorldPreview(3, "World_03");

            AssetDatabase.SaveAssets();
            BalanceImporter.Import();

            Debug.Log($"[Content] world 3 content ready: {cryo.WeaponName}, {frost.name}");
        }

        [MenuItem("Tools/Content/Build World 4 Content (Rook, Flash Gun, art)")]
        public static void BuildWorld4Content()
        {
            BuildFlashGun();
            BuildRook();
            RenderWorldPreview(4, "World_04");
            AssignWorldHeroes();

            AssetDatabase.SaveAssets();
            BalanceImporter.Import();
        }

        // each world's hero and weapon (WorldHeroes unlocks and equips them when the world is reached)
        [MenuItem("Tools/Content/Assign World Heroes")]
        public static void AssignWorldHeroes()
        {
            (string world, string character, string weapon)[] pairs =
            {
                ("World_01", "Character 1", "Weapon Minigun"),
                ("World_02", "Character 3", "Weapon Lava"),
                ("World_03", "Character 4", "Weapon Cryo"),
                ("World_04", "Character 5", "Weapon Flash"),
                ("World_05", "Character 6", "Weapon Rocket"),
            };

            foreach (var pair in pairs)
            {
                WorldData world = AssetDatabase.LoadAssetAtPath<WorldData>($"{DATA}/Level System/Worlds/{pair.world}.asset");
                if (world == null) continue;

                SerializedObject serialized = new SerializedObject(world);
                serialized.FindProperty("featuredCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterData>($"{DATA}/Characters System/{pair.character}.asset");
                serialized.FindProperty("featuredWeapon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DATA}/Weapon System/{pair.weapon}.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(world);
            }
        }

        #region Flash Gun
        // arcing flashbang launcher on the lava gun rig: area damage from above (ignores riot shields) + stun
        private static WeaponData BuildFlashGun()
        {
            string gunPath = GAME + "/Prefabs/Weapons/Gun Flash.prefab";
            Copy(GAME + "/Prefabs/Weapons/Gun Lava.prefab", gunPath);
            GameObject gun = PrefabUtility.LoadPrefabContents(gunPath);

            Mesh model = SaveMesh(FlashGunMesh(), "FlashGun_mesh");
            MeshFilter main = null;
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
                if (filter.GetComponent<MeshRenderer>() != null && (main == null || filter.name == "Flashgun")) main = filter;
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
                if (filter != main && filter.GetComponent<MeshRenderer>() is MeshRenderer extra) extra.enabled = false;

            main.name = "Flashgun";
            main.sharedMesh = model;
            main.transform.localRotation = Quaternion.identity;
            main.transform.localScale = Vector3.one;
            main.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GAME + "/Materials/Characters/Player.mat");

            foreach (ParticleSystem particle in gun.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule settings = particle.main;
                settings.startColor = new Color(1f, 0.95f, 0.7f, settings.startColor.color.a);
            }

            PrefabUtility.SaveAsPrefabAsset(gun, gunPath);
            PrefabUtility.UnloadPrefabContents(gun);

            string bulletPath = GAME + "/Prefabs/Weapons/Bullets/Flash Bullet.prefab";
            Copy(GAME + "/Prefabs/Weapons/Bullets/Lava Bullet.prefab", bulletPath);
            GameObject bullet = PrefabUtility.LoadPrefabContents(bulletPath);

            SerializedObject behaviour = new SerializedObject(bullet.GetComponent<LavaBulletBehavior>());
            behaviour.FindProperty("hitParticle").stringValue = "Tesla Hit";
            behaviour.FindProperty("wallHitParticle").stringValue = "Tesla Wall Hit";
            behaviour.FindProperty("ignoreFrontalArmor").boolValue = true;
            behaviour.FindProperty("stunMultiplier").floatValue = 0.3f;
            behaviour.FindProperty("stunDuration").floatValue = 1f;
            behaviour.ApplyModifiedPropertiesWithoutUndo();

            Mesh canister = SaveMesh(FlashCanisterMesh(), "FlashCanister_mesh");
            bool replaced = false;
            foreach (MeshFilter filter in bullet.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                if (!replaced)
                {
                    filter.sharedMesh = canister;
                    filter.transform.localScale = Vector3.one;
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GAME + "/Materials/Characters/Player.mat");
                    replaced = true;
                }
                else renderer.enabled = false;
            }
            foreach (ParticleSystem particle in bullet.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule settings = particle.main;
                settings.startColor = new Color(1f, 0.97f, 0.8f, settings.startColor.color.a);
            }

            PrefabUtility.SaveAsPrefabAsset(bullet, bulletPath);
            PrefabUtility.UnloadPrefabContents(bullet);

            string dataPath = DATA + "/Weapon System/Weapon Flash.asset";
            bool created = Copy(DATA + "/Weapon System/Weapon Lava.asset", dataPath);
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);

            SerializedObject data = new SerializedObject(weapon);
            if (created) data.FindProperty("id").stringValue = Guid.NewGuid().ToString();
            data.FindProperty("weaponName").stringValue = "FLASH GUN";
            data.FindProperty("rarity").intValue = (int)Rarity.Epic;
            data.FindProperty("icon").objectReferenceValue = RenderWeaponIcon(AssetDatabase.LoadAssetAtPath<GameObject>(gunPath), "weapon_flash_gun");

            SerializedProperty upgrades = data.FindProperty("upgrades");
            for (int i = 0; i < upgrades.arraySize; i++)
            {
                SerializedProperty upgrade = upgrades.GetArrayElementAtIndex(i);
                upgrade.FindPropertyRelative("weaponPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
                upgrade.FindPropertyRelative("bulletPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);

            Register(DATA + "/Weapon System/Weapon Database.asset", "weapons", weapon, "TESLA");
            return weapon;
        }

        private static Mesh FlashGunMesh()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.22f, 0.22f, 0.5f), 0.04f, new Vector3(0f, 0.12f, 0.02f), Vector3.zero, Band.Yellow, 0.3f, 0.75f);
            for (int i = -2; i <= 2; i++)
                m.Box(new Vector3(0.226f, 0.05f, 0.04f), 0.005f, new Vector3(0f, 0.12f, 0.02f + i * 0.09f), new Vector3(35f, 0f, 0f), Band.Black, 0f, 0.3f);
            m.Cylinder(0.14f, 0.14f, 0.2f, 10, new Vector3(0f, 0.02f, -0.04f), new Vector3(0f, 0f, 90f), Band.Grey, 0.15f, 0.55f);
            m.Cylinder(0.145f, 0.145f, 0.04f, 10, new Vector3(0.08f, 0.02f, -0.04f), new Vector3(0f, 0f, 90f), Band.Black, 0f, 0.3f);
            m.Cylinder(0.1f, 0.1f, 0.32f, 8, new Vector3(0f, 0.16f, 0.42f), new Vector3(90f, 0f, 0f), Band.Black, 0.1f, 0.45f);
            m.Cylinder(0.115f, 0.115f, 0.06f, 8, new Vector3(0f, 0.16f, 0.57f), new Vector3(90f, 0f, 0f), Band.Orange, 0.3f, 0.7f);
            m.Box(new Vector3(0.08f, 0.2f, 0.1f), 0.02f, new Vector3(0f, -0.06f, -0.14f), new Vector3(15f, 0f, 0f), Band.Black, 0f, 0.4f);
            m.Box(new Vector3(0.16f, 0.16f, 0.12f), 0.03f, new Vector3(0f, 0.12f, -0.28f), Vector3.zero, Band.Black, 0f, 0.5f);
            m.Box(new Vector3(0.05f, 0.07f, 0.12f), 0.01f, new Vector3(0f, 0.27f, 0.1f), Vector3.zero, Band.Red, 0.2f, 0.5f);
            return m.Build("FlashGun_mesh");
        }

        private static Mesh FlashCanisterMesh()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.09f, 0.09f, 0.22f, 8, Vector3.zero, new Vector3(90f, 0f, 0f), Band.Grey, 0.1f, 0.5f);
            m.Cylinder(0.095f, 0.095f, 0.06f, 8, Vector3.zero, new Vector3(90f, 0f, 0f), Band.Yellow, 0.3f, 0.6f);
            m.Cylinder(0.05f, 0.05f, 0.05f, 6, new Vector3(0f, 0f, 0.13f), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
            return m.Build("FlashCanister_mesh");
        }
        #endregion

        #region Rook
        // street rebel built on Jim's rig: orange hoodie, jeans, sneakers, bandana, backwards cap
        private static CharacterData BuildRook()
        {
            string graphicsPath = GAME + "/Prefabs/Characters/Player/Graphics/Character 5.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GAME + "/Prefabs/Characters/Player/Graphics/Character 1.prefab"));
            instance.name = "Character 5";

            SkinnedMeshRenderer body = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            Mesh mesh = UnityEngine.Object.Instantiate(body.sharedMesh);
            Watermelon.SquadShooter.Examples.ExampleObjectsBuilder.RemapBands(mesh, body.bones, (band, bone) =>
            {
                bool feet = bone.Contains("Foot") || bone.Contains("Toe");
                bool shins = bone.EndsWith("Leg") && !bone.EndsWith("UpLeg");
                if (band == Band.Black && feet) return Band.White;
                if (band == Band.Black && shins) return Band.Blue;
                if (band == Band.Black) return Band.Orange;
                if (band == Band.Red) return Band.Black;
                if (band == Band.Navy) return Band.Blue;
                return band;
            });
            body.sharedMesh = SaveMesh(mesh, "Main_Char_Rook");

            Bounds head = PaletteTools.BoneBounds(body, bone => bone.EndsWith("Head") || bone.Contains("HeadTop"));
            float r = Mathf.Max(head.extents.x, head.extents.z);
            Vector3 c = head.center;

            PaletteMesh hat = new PaletteMesh();
            hat.Dome(r * 1.04f, 3, 10, c + new Vector3(0f, r * 0.32f, 0f), Vector3.zero, new Vector3(1f, 0.85f, 1f), Band.Red, 0.1f, 0.55f);
            hat.Box(new Vector3(r * 1.1f, r * 0.08f, r * 0.75f), r * 0.03f, c + new Vector3(0f, r * 0.42f, -r * 1.15f), new Vector3(-8f, 0f, 0f), Band.Red, 0.3f, 0.7f);
            Attach(body, "Head", "Rook Cap", SaveMesh(hat.Build("Rook_Cap"), "Rook_Cap"));

            Bounds chest = PaletteTools.BoneBounds(body, bone => bone.EndsWith("Spine2") || bone.EndsWith("Spine1"));
            PaletteMesh hood = new PaletteMesh();
            hood.Dome(chest.extents.x * 0.85f, 3, 9, new Vector3(chest.center.x, chest.max.y - 0.05f, chest.min.z + 0.04f), new Vector3(-70f, 0f, 0f), new Vector3(1f, 0.7f, 0.8f), Band.Orange, 0.35f, 0.75f);
            Attach(body, "Spine2", "Rook Hood", SaveMesh(hood.Build("Rook_Hood"), "Rook_Hood"));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, graphicsPath);
            UnityEngine.Object.DestroyImmediate(instance);

            (Sprite portrait, Sprite locked) = RenderPortrait(prefab, "character_5");

            string dataPath = DATA + "/Characters System/Character 5.asset";
            bool created = Copy(DATA + "/Characters System/Character 3.asset", dataPath);
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);

            SerializedObject data = new SerializedObject(character);
            if (created) data.FindProperty("id").stringValue = Guid.NewGuid().ToString();
            data.FindProperty("characterName").stringValue = "Rook";
            data.FindProperty("previewSprite").objectReferenceValue = portrait;
            data.FindProperty("lockedSprite").objectReferenceValue = locked;
            SerializedProperty stages = data.FindProperty("stages");
            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                stage.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                stage.FindPropertyRelative("previewSprite").objectReferenceValue = portrait;
                stage.FindPropertyRelative("lockedSprite").objectReferenceValue = locked;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);

            Register(DATA + "/Characters System/Characters Database.asset", "characters", character, null);
            return character;
        }

        private static void Attach(SkinnedMeshRenderer body, string bone, string name, Mesh mesh)
        {
            Transform parent = PaletteTools.FindBone(body, bone);
            PaletteTools.Attach(parent, name, mesh);
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            return PaletteTools.SaveMesh(mesh, GAME + "/Prefabs/Characters/Player/Graphics/Meshes", name);
        }
        #endregion

        #region Cryo Blaster
        private static WeaponData BuildCryoBlaster()
        {
            // gun: the minigun rig (hand holders, shoot point, muzzle flash) with the Cryo Blaster model
            string gunPath = GAME + "/Prefabs/Weapons/Gun Cryo.prefab";
            Copy(GAME + "/Prefabs/Weapons/Gun Minigun.prefab", gunPath);
            GameObject gun = PrefabUtility.LoadPrefabContents(gunPath);

            Transform body = Find(gun.transform, "Minigun_mesh_v3") ?? Find(gun.transform, "Cryoblaster");
            body.name = "Cryoblaster";
            body.localPosition = new Vector3(0f, 0f, -0.05f);
            body.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(EXAMPLES + "/Meshes/CryoBlaster_mesh.asset");
            Find(gun.transform, "Minigun_barrel_v3").GetComponent<MeshRenderer>().enabled = false;

            SerializedObject behaviour = new SerializedObject(gun.GetComponent<MiniGunBehavior>());
            behaviour.FindProperty("fireRotationSpeed").floatValue = 0f;
            behaviour.ApplyModifiedPropertiesWithoutUndo();

            foreach (ParticleSystem particle in gun.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.startColor = new Color(0.6f, 0.95f, 1f, main.startColor.color.a);
            }

            PrefabUtility.SaveAsPrefabAsset(gun, gunPath);
            PrefabUtility.UnloadPrefabContents(gun);

            // bullet: minigun bullet physics + slow on hit, ice shard model, cyan trails
            string bulletPath = GAME + "/Prefabs/Weapons/Bullets/Cryo Bullet.prefab";
            Copy(GAME + "/Prefabs/Weapons/Bullets/Minigun Bullet.prefab", bulletPath);
            GameObject bullet = PrefabUtility.LoadPrefabContents(bulletPath);

            TrailRenderer[] trails = bullet.GetComponentsInChildren<TrailRenderer>(true);
            if (bullet.GetComponent<MinigunBulletBehavior>() is MinigunBulletBehavior old && !(old is CryoBulletBehavior))
            {
                UnityEngine.Object.DestroyImmediate(old, true);
                CryoBulletBehavior cryoBullet = bullet.AddComponent<CryoBulletBehavior>();
                SerializedObject serialized = new SerializedObject(cryoBullet);
                serialized.FindProperty("trailRenderer").objectReferenceValue = trails.Length > 0 ? trails[trails.Length - 1] : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Color[] trailColours = { new Color(0.9f, 1f, 1f), new Color(0.35f, 0.85f, 1f) };
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].startColor = new Color(trailColours[i % 2].r, trailColours[i % 2].g, trailColours[i % 2].b, trails[i].startColor.a);
                trails[i].endColor = new Color(trailColours[i % 2].r, trailColours[i % 2].g, trailColours[i % 2].b, 0f);
            }

            Transform shard = bullet.transform.Find("Shard");
            if (shard == null)
            {
                shard = new GameObject("Shard").transform;
                shard.SetParent(bullet.transform, false);
                shard.gameObject.AddComponent<MeshFilter>();
                shard.gameObject.AddComponent<MeshRenderer>();
            }
            shard.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(EXAMPLES + "/Meshes/CryoShard_mesh.asset");
            shard.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GAME + "/Materials/Characters/Player.mat");
            shard.localScale = Vector3.one * 0.9f;

            PrefabUtility.SaveAsPrefabAsset(bullet, bulletPath);
            PrefabUtility.UnloadPrefabContents(bullet);

            // data: a copy of the minigun data pointing at the new prefabs
            string dataPath = DATA + "/Weapon System/Weapon Cryo.asset";
            bool created = Copy(DATA + "/Weapon System/Weapon Minigun.asset", dataPath);
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);

            SerializedObject data = new SerializedObject(weapon);
            if (created) data.FindProperty("id").stringValue = Guid.NewGuid().ToString();
            data.FindProperty("weaponName").stringValue = "CRYO GUN";
            data.FindProperty("rarity").intValue = (int)Rarity.Rare;
            data.FindProperty("icon").objectReferenceValue = RenderWeaponIcon(AssetDatabase.LoadAssetAtPath<GameObject>(gunPath), "weapon_cryo_gun");

            SerializedProperty upgrades = data.FindProperty("upgrades");
            for (int i = 0; i < upgrades.arraySize; i++)
            {
                SerializedProperty upgrade = upgrades.GetArrayElementAtIndex(i);
                upgrade.FindPropertyRelative("weaponPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
                upgrade.FindPropertyRelative("bulletPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);
                upgrade.FindPropertyRelative("spread").floatValue = 2.5f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);

            // list it before the Tesla gun, which unlocks later
            Register(DATA + "/Weapon System/Weapon Database.asset", "weapons", weapon, "TESLA");

            return weapon;
        }

        private static Sprite RenderWeaponIcon(GameObject gunPrefab, string fileName)
        {
            using (UIArtRenderer renderer = new UIArtRenderer(new Color(0f, 0f, 0f, 0f)))
            {
                GameObject gun = renderer.Spawn(gunPrefab);
                gun.transform.rotation = Quaternion.Euler(0f, -110f, -18f);
                Texture2D shot = renderer.Render(gun, new Vector3(0.15f, -0.35f, 1f), 1.02f, true);
                Texture2D icon = UIArtRenderer.Outline(shot, 4, 12, new Color(0.05f, 0.05f, 0.08f), Color.white);

                return UIArtRenderer.SaveSprite(icon, $"{GAME}/Images/Weapons/{fileName}.png", $"{GAME}/Images/Weapons/weapon_lava_gun.png");
            }
        }
        #endregion

        #region Frost
        private static CharacterData BuildFrost()
        {
            string graphicsPath = GAME + "/Prefabs/Characters/Player/Graphics/Character 4.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(graphicsPath) == null)
                AssetDatabase.MoveAsset(EXAMPLES + "/Prefabs/Character 4 Frost.prefab", graphicsPath);
            GameObject graphics = AssetDatabase.LoadAssetAtPath<GameObject>(graphicsPath);

            (Sprite portrait, Sprite locked) = RenderPortrait(graphics, "character_4");

            string dataPath = DATA + "/Characters System/Character 4.asset";
            bool created = Copy(DATA + "/Characters System/Character 3.asset", dataPath);
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPath);

            SerializedObject data = new SerializedObject(character);
            if (created) data.FindProperty("id").stringValue = Guid.NewGuid().ToString();
            data.FindProperty("characterName").stringValue = "Frost";
            data.FindProperty("previewSprite").objectReferenceValue = portrait;
            data.FindProperty("lockedSprite").objectReferenceValue = locked;

            SerializedProperty stages = data.FindProperty("stages");
            for (int i = 0; i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                stage.FindPropertyRelative("prefab").objectReferenceValue = graphics;
                stage.FindPropertyRelative("previewSprite").objectReferenceValue = portrait;
                stage.FindPropertyRelative("lockedSprite").objectReferenceValue = locked;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);

            Register(DATA + "/Characters System/Characters Database.asset", "characters", character, null);

            return character;
        }

        private static (Sprite portrait, Sprite locked) RenderPortrait(GameObject graphicsPrefab, string fileName)
        {
            using (UIArtRenderer renderer = new UIArtRenderer(new Color(0f, 0f, 0f, 0f)))
            {
                GameObject character = renderer.Spawn(graphicsPrefab);
                character.transform.rotation = Quaternion.Euler(0f, 160f, 0f);

                Animator animator = character.GetComponentInChildren<Animator>();
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Shot Idle") { clip.SampleAnimation(animator.gameObject, clip.length * 0.5f); break; }

                // knees-up close-up like the template portraits
                Bounds bounds = UIArtRenderer.BoundsOf(character);
                Vector3 chest = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.62f, bounds.center.z);
                Texture2D shot = renderer.RenderFrom(chest + new Vector3(0.6f, 0.3f, -2.85f), chest + new Vector3(0f, 0.08f, 0f), 32f);
                Texture2D portrait = UIArtRenderer.FadeBottom(UIArtRenderer.Outline(shot, 3, 0, new Color(0.08f, 0.08f, 0.12f), Color.clear), 0.22f);

                Sprite colour = UIArtRenderer.SaveSprite(portrait, $"{GAME}/Images/Characters/{fileName}.png", $"{GAME}/Images/Characters/character_3.png");
                Sprite black = UIArtRenderer.SaveSprite(UIArtRenderer.Silhouette(portrait), $"{GAME}/Images/Characters/{fileName}_locked.png", $"{GAME}/Images/Characters/character_3_locked.png");

                return (colour, black);
            }
        }
        #endregion

        #region World preview
        // a low camera inside a room of the world, like the template's world 1/2 pictures
        private static void RenderWorldPreview(int number, string worldName)
        {
            WorldData world = AssetDatabase.LoadAssetAtPath<WorldData>($"{DATA}/Level System/Worlds/{worldName}.asset");
            using (UIArtRenderer renderer = new UIArtRenderer(new Color(0.42f, 0.72f, 0.98f, 1f)))
            {
                GameObject root = new GameObject("Preview");
                renderer.Add(root);

                RoomData room = world.Levels[3].Rooms[0];
                foreach (ItemEntityData item in room.ItemEntities)
                {
                    LevelItem levelItem = Array.Find(world.Items, i => i.Hash == item.Hash);
                    if (levelItem == null) continue;
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(levelItem.Prefab, root.transform);
                    instance.transform.SetPositionAndRotation(item.Position, item.Rotation);
                }

                // hero props in front of the camera
                Place(world, root, "Block 1x1 Small Half", new Vector3(-1.2f, 0f, -6.2f), 200f);
                Place(world, root, "Block 1x2 Big", new Vector3(1.6f, 0f, -3.6f), 15f);
                Place(world, root, "Block 1x1 Small", new Vector3(-2.6f, 0f, -4.4f), 0f);

                Texture2D shot = renderer.RenderFrom(new Vector3(-1.2f, 2.2f, -10.5f), new Vector3(0.4f, 1.3f, 0f), 52f);
                Sprite sprite = UIArtRenderer.SaveSprite(shot, $"{GAME}/Images/Worlds/world {number}.png", $"{GAME}/Images/Worlds/world 2.png");

                SerializedObject data = new SerializedObject(world);
                data.FindProperty("previewSprite").objectReferenceValue = sprite;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(world);
            }
        }

        private static void Place(WorldData world, GameObject root, string name, Vector3 position, float angle)
        {
            LevelItem item = Array.Find(world.Items, i => i.Prefab != null && i.Prefab.name == name);
            if (item == null) return;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.Prefab, root.transform);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, angle, 0f));
        }
        #endregion

        #region Helpers
        private static bool Copy(string source, string target)
        {
            if (AssetDatabase.LoadMainAssetAtPath(target) != null) return false;
            AssetDatabase.CopyAsset(source, target);
            return true;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        // adds `item` to a database array once; inserted before the first entry whose name contains `before`
        private static void Register(string databasePath, string arrayName, UnityEngine.Object item, string before)
        {
            UnityEngine.Object database = AssetDatabase.LoadMainAssetAtPath(databasePath);
            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty array = serialized.FindProperty(arrayName);

            int insertAt = array.arraySize;
            for (int i = 0; i < array.arraySize; i++)
            {
                UnityEngine.Object existing = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing == item) return;
                if (before != null && existing != null && insertAt == array.arraySize && existing.name.ToUpperInvariant().Contains(before)) insertAt = i;
            }

            array.InsertArrayElementAtIndex(insertAt);
            array.GetArrayElementAtIndex(insertAt).objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }
        #endregion
    }
}
