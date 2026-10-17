using System;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;

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
