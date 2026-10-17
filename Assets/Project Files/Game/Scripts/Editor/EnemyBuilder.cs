using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter.Examples;

namespace Watermelon.SquadShooter
{
    // Builds world-specific enemies as prefab variants of the template enemies: their behaviour (AI, animations,
    // weapons logic) is reused, while the look is re-dyed into the shared palette with new accessories, weapons
    // and projectiles get the world's theme, and each enemy gets its own entry (type + stats) in the database.
    public static class EnemyBuilder
    {
        private const string GAME = "Assets/Project Files/Game";
        private const string ENEMIES = GAME + "/Prefabs/Characters/Enemy";
        private const string DATABASE = "Assets/Project Files/Data/Enemies/Enemies Database.asset";

        private static readonly Color ICE_PARTICLE = new Color(0.55f, 0.9f, 1f);
        private static readonly Color GAS_PARTICLE = new Color(0.55f, 0.85f, 0.35f);

        private static string meshFolder = ENEMIES + "/World 3/Meshes";
        private static Color effectTint = ICE_PARTICLE;

        public class Recipe
        {
            public string Name;
            public string Source;
            public EnemyType Type;
            public EnemyType SourceType;
            public float Scale = 1f;
            public Func<PaletteTools.Texel, int> Body;
            public Func<PaletteTools.Texel, int> Weapon;
            public Func<string, bool> IsWeapon = name => false;
            public Action<GameObject, SkinnedMeshRenderer, Bounds> Accessories;
            public Dictionary<string, string> Projectiles = new Dictionary<string, string>();
            public Material Laser;
            public float Hp, MoveSpeed;
            public float FrontalArmor;
            public Vector2Int Damage;
        }

        [MenuItem("Tools/Content/Build World 3 Enemies (Glacier)")]
        public static void BuildGlacierEnemies()
        {
            string folder = ENEMIES + "/World 3";
            PaletteTools.EnsureFolder(folder + "/Meshes");
            meshFolder = folder + "/Meshes";
            effectTint = ICE_PARTICLE;

            Dictionary<string, string> projectiles = BuildIceProjectiles(folder);
            Material iceLaser = IceLaserMaterial(folder);

            foreach (Recipe recipe in GlacierRecipes(projectiles, iceLaser))
            {
                GameObject prefab = BuildEnemy(recipe, folder);
                Texture2D icon = RenderIcon(prefab, recipe.Name);
                Register(recipe, prefab, icon);
            }

            // world rule: every hit in the glacier slows the player for a moment (frostbite)
            WorldData world = AssetDatabase.LoadAssetAtPath<WorldData>("Assets/Project Files/Data/Level System/Worlds/World_03.asset");
            SerializedObject worldData = new SerializedObject(world);
            worldData.FindProperty("hitSlowMultiplier").floatValue = 0.7f;
            worldData.FindProperty("hitSlowDuration").floatValue = 0.9f;
            worldData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);

            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyBuilder] glacier enemies built");
        }

        #region Riot city
        [MenuItem("Tools/Content/Build World 4 Enemies (Riot City)")]
        public static void BuildRiotEnemies()
        {
            string folder = ENEMIES + "/World 4";
            PaletteTools.EnsureFolder(folder + "/Meshes");
            meshFolder = folder + "/Meshes";
            effectTint = new Color(1f, 0.85f, 0.45f);

            string bullets = GAME + "/Prefabs/Weapons/Bullets";
            Func<PaletteTools.Texel, int> gasRule = t => t.Value > 0.6f ? Band.Green : Band.Grey;
            var projectiles = new Dictionary<string, string>
            {
                { "Gas Canister", ThemedProjectile($"{bullets}/Enemy Grenade.prefab", $"{folder}/Gas Canister.prefab", folder, GasCanister(), gasRule, GAS_PARTICLE) },
                { "Gas Canister Elite", ThemedProjectile($"{bullets}/Enemy Grenade Elite.prefab", $"{folder}/Gas Canister Elite.prefab", folder, GasCanister(), gasRule, GAS_PARTICLE) },
            };

            foreach (Recipe recipe in RiotRecipes(projectiles))
            {
                GameObject prefab = BuildEnemy(recipe, folder);
                Texture2D icon = RenderIcon(prefab, recipe.Name);
                Register(recipe, prefab, icon);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyBuilder] riot city enemies built");
        }

        private static IEnumerable<Recipe> RiotRecipes(Dictionary<string, string> projectiles)
        {
            Func<PaletteTools.Texel, int> darkGun = t => t.Value > 0.65f ? Band.Grey : Band.Black;

            yield return new Recipe
            {
                Name = "Enemy Riot Cop", Source = "Enemy Rifle", Type = EnemyType.RiotCop, SourceType = EnemyType.Rifle,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.Steel : t.IsWarm ? Band.Yellow : Band.Navy,
                Weapon = darkGun, IsWeapon = n => n == "Rifle" || n == "Weapon",
                Accessories = (root, body, head) => RiotHelmet(root, body, head),
                Hp = 210f, Damage = new Vector2Int(8, 15), MoveSpeed = 2.65f,
            };

            yield return new Recipe
            {
                Name = "Enemy Riot Brute", Source = "Enemy Melee", Type = EnemyType.RiotBrute, SourceType = EnemyType.BatMelee, Scale = 1.15f,
                Body = t => t.IsGreen && (t.OnHead || t.OnArms) ? Band.Yellow : t.IsDark ? Band.Black : t.IsGrey ? Band.Steel : t.IsSkin ? (t.OnTorso ? Band.Steel : -1) : Band.Navy,
                Weapon = t => t.Value > 0.55f ? Band.Yellow : Band.Black, IsWeapon = n => n.Contains("Weapon"),
                Accessories = (root, body, head) => { RiotHelmet(root, body, head); RiotShield(root, body); },
                FrontalArmor = 0.8f,
                Hp = 380f, Damage = new Vector2Int(22, 30), MoveSpeed = 3.8f,
            };

            yield return new Recipe
            {
                Name = "Enemy Gas Grenadier", Source = "Enemy Grenader", Type = EnemyType.GasGrenadier, SourceType = EnemyType.Grenader,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.Grey : t.OnLegs ? Band.Navy : Band.Yellow,
                Weapon = t => t.Value > 0.6f ? Band.Green : Band.Grey, IsWeapon = n => n.Contains("Gran") || n.Contains("Pin") || n.Contains("Striker"),
                Accessories = (root, body, head) => GasMask(root, body, head),
                Projectiles = { { "grenadePrefab", projectiles["Gas Canister"] }, { "eliteGrenadePrefab", projectiles["Gas Canister Elite"] } },
                Hp = 240f, Damage = new Vector2Int(15, 30), MoveSpeed = 3.1f,
            };

            yield return new Recipe
            {
                Name = "Enemy SWAT", Source = "Enemy Shotgun", Type = EnemyType.SwatShotgunner, SourceType = EnemyType.Shotguner,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.Grey : Band.Black,
                Weapon = darkGun, IsWeapon = n => n == "Shotgun" || n == "Forestock",
                Accessories = (root, body, head) => { TacticalHelmet(root, body, head); Goggles(root, body, head, Band.Yellow); },
                Hp = 250f, Damage = new Vector2Int(6, 9), MoveSpeed = 2.9f,
            };

            yield return new Recipe
            {
                Name = "Enemy Rooftop Sniper", Source = "Enemy Sniper", Type = EnemyType.RooftopSniper, SourceType = EnemyType.Sniper,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGreen ? Band.Grey : Band.Grey,
                Weapon = darkGun, IsWeapon = n => n.Contains("Rifle"),
                Accessories = (root, body, head) => Beanie(root, body, head, Band.Black, Band.Red),
                Hp = 130f, Damage = new Vector2Int(80, 110), MoveSpeed = 2.65f,
            };

            yield return new Recipe
            {
                Name = "Boss Juggernaut", Source = "Boss Bomber", Type = EnemyType.BossJuggernaut, SourceType = EnemyType.BossBomber, Scale = 1.08f,
                Body = t => t.IsDark ? Band.Black : t.IsWarm ? Band.Yellow : t.IsGrey ? Band.Steel : Band.Navy,
                Weapon = t => t.IsDark ? Band.Black : t.Value > 0.6f ? Band.Grey : Band.Yellow, IsWeapon = n => n.Contains("Minigun") || n.Contains("Cannon"),
                Accessories = (root, body, head) => { BombSuitHelmet(root, body, head); ChestPlate(root, body); },
                FrontalArmor = 0.5f,
                Hp = 2600f, Damage = new Vector2Int(7, 16), MoveSpeed = 3.1f,
            };

            yield return new Recipe
            {
                Name = "Boss SWAT Chief", Source = "Boss Sniper", Type = EnemyType.BossSwatChief, SourceType = EnemyType.BossSniper, Scale = 1.25f,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.Grey : Band.Black,
                Weapon = darkGun, IsWeapon = n => n.Contains("Weapon") || n.Contains("Rifle"),
                Accessories = (root, body, head) => { Beret(root, body, head); Goggles(root, body, head, Band.Red); },
                Hp = 1800f, Damage = new Vector2Int(85, 115), MoveSpeed = 6.25f,
            };
        }
        #endregion

        #region Glacier recipes
        private static IEnumerable<Recipe> GlacierRecipes(Dictionary<string, string> projectiles, Material laser)
        {
            Func<PaletteTools.Texel, int> iceGun = t => t.IsDark ? Band.Navy : t.Value > 0.6f ? Band.Ice : Band.Steel;

            yield return new Recipe
            {
                Name = "Enemy Snow Trooper", Source = "Enemy Rifle", Type = EnemyType.SnowTrooper, SourceType = EnemyType.Rifle,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Navy : t.IsGrey ? Band.White : t.OnLegs ? Band.Steel : Band.Teal,
                Weapon = iceGun, IsWeapon = n => n == "Rifle" || n == "Weapon",
                Accessories = (root, body, head) => { FurHood(root, body, head, Band.White, Band.Teal); Goggles(root, body, head, Band.Sky); },
                Projectiles = { { "bulletPrefab", projectiles["Ice Bullet"] } },
                Hp = 210f, Damage = new Vector2Int(8, 16), MoveSpeed = 2.65f,
            };

            yield return new Recipe
            {
                Name = "Enemy Yeti", Source = "Enemy Melee", Type = EnemyType.Yeti, SourceType = EnemyType.BatMelee, Scale = 1.3f,
                Body = t => t.IsDark ? Band.Steel : t.IsSkin && t.OnArms ? Band.Ice : Band.White,
                Weapon = t => t.Value > 0.55f ? Band.Ice : Band.Sky, IsWeapon = n => n.Contains("Weapon"),
                Accessories = (root, body, head) => { YetiHead(root, body, head); IceSpikes(root, body); },
                Hp = 330f, Damage = new Vector2Int(18, 26), MoveSpeed = 5.2f,
            };

            yield return new Recipe
            {
                Name = "Enemy Ice Bomber", Source = "Enemy Demo", Type = EnemyType.IceBomber, SourceType = EnemyType.Demo,
                Body = t => t.OnHead && t.Saturation > 0.3f ? Band.White : t.IsSkin ? -1 : t.IsDark ? Band.Navy : t.IsGrey ? Band.White : Band.Teal,
                Weapon = t => t.IsDark ? Band.Navy : t.IsWarm ? Band.Yellow : Band.Sky, IsWeapon = n => n == "Bomb",
                Accessories = (root, body, head) => Beanie(root, body, head, Band.Navy, Band.Yellow),
                Hp = 330f, Damage = new Vector2Int(70, 90), MoveSpeed = 5.2f,
            };

            yield return new Recipe
            {
                Name = "Enemy Snow Grenadier", Source = "Enemy Grenader", Type = EnemyType.SnowGrenadier, SourceType = EnemyType.Grenader,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.White : t.OnLegs ? Band.Navy : Band.Red,
                Weapon = t => Band.White, IsWeapon = n => n.Contains("Gran") || n.Contains("Pin") || n.Contains("Striker"),
                Accessories = (root, body, head) => Beanie(root, body, head, Band.Red, Band.White),
                Projectiles = { { "grenadePrefab", projectiles["Snowball Grenade"] }, { "eliteGrenadePrefab", projectiles["Snowball Grenade Elite"] } },
                Hp = 240f, Damage = new Vector2Int(15, 30), MoveSpeed = 3.1f,
            };

            yield return new Recipe
            {
                Name = "Enemy Frost Sniper", Source = "Enemy Sniper", Type = EnemyType.FrostSniper, SourceType = EnemyType.Sniper,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Steel : t.IsGreen ? Band.Ice : Band.White,
                Weapon = iceGun, IsWeapon = n => n.Contains("Rifle"),
                Accessories = (root, body, head) => { FurHood(root, body, head, Band.White, Band.Ice); Goggles(root, body, head, Band.Sky); },
                Projectiles = { { "bulletPrefab", projectiles["Ice Bullet"] } }, Laser = laser,
                Hp = 130f, Damage = new Vector2Int(80, 110), MoveSpeed = 2.65f,
            };

            yield return new Recipe
            {
                Name = "Boss Yeti King", Source = "Boss Bomber", Type = EnemyType.BossYetiKing, SourceType = EnemyType.BossBomber, Scale = 1.12f,
                Body = t => t.IsDark ? Band.Steel : t.IsWarm ? Band.Sky : Band.White,
                Weapon = t => t.IsDark ? Band.Navy : t.Value > 0.6f ? Band.Ice : Band.Sky, IsWeapon = n => n.Contains("Minigun") || n.Contains("Cannon"),
                Accessories = (root, body, head) => { YetiHead(root, body, head); Crown(root, body, head); IceSpikes(root, body); },
                Projectiles = { { "bulletPrefab", projectiles["Ice Boulder"] } },
                Hp = 2200f, Damage = new Vector2Int(6, 15), MoveSpeed = 3.1f,
            };

            yield return new Recipe
            {
                Name = "Boss Frost Commander", Source = "Boss Sniper", Type = EnemyType.BossFrostCommander, SourceType = EnemyType.BossSniper, Scale = 1.25f,
                Body = t => t.IsSkin ? -1 : t.IsDark ? Band.Black : t.IsGrey ? Band.Ice : t.OnLegs ? Band.Navy : Band.Navy,
                Weapon = iceGun, IsWeapon = n => n.Contains("Weapon") || n.Contains("Rifle"),
                Accessories = (root, body, head) => { OfficerCap(root, body, head); Goggles(root, body, head, Band.Sky); },
                Projectiles = { { "bulletPrefab", projectiles["Frost Boss Bullet"] } }, Laser = laser,
                Hp = 1600f, Damage = new Vector2Int(80, 110), MoveSpeed = 6.25f,
            };
        }
        #endregion

        #region Enemy build
        private static GameObject BuildEnemy(Recipe recipe, string folder)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>($"{ENEMIES}/{recipe.Source}.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = recipe.Name;

            // remove accessories of a previous build of the same variant
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name.StartsWith("Accessory ")) UnityEngine.Object.DestroyImmediate(child.gameObject);

            Material material = PaletteTools.PlayerMaterial;
            SkinnedMeshRenderer mainBody = null;
            int index = 0;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                if (renderer.name.Contains("Laser"))
                {
                    if (recipe.Laser != null) renderer.sharedMaterial = recipe.Laser;
                    continue;
                }

                bool weapon = recipe.IsWeapon(renderer.name) || recipe.IsWeapon(renderer.transform.parent != null ? renderer.transform.parent.name : "");
                Func<PaletteTools.Texel, int> rule = weapon ? recipe.Weapon : recipe.Body;
                Texture texture = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
                if (texture == null || rule == null) continue;

                string meshName = $"{recipe.Type}_{index++}_{renderer.name.Replace(' ', '_')}";
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    Mesh source0 = OriginalMesh(skinned.sharedMesh);
                    skinned.sharedMesh = PaletteTools.SaveMesh(PaletteTools.Repaint(source0, skinned.bones, texture, rule, meshName), folder + "/Meshes", meshName);
                    if (!weapon && (mainBody == null || skinned.sharedMesh.vertexCount > mainBody.sharedMesh.vertexCount)) mainBody = skinned;
                }
                else if (renderer.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh != null)
                {
                    Mesh source0 = OriginalMesh(filter.sharedMesh);
                    filter.sharedMesh = PaletteTools.SaveMesh(PaletteTools.Repaint(source0, null, texture, rule, meshName), folder + "/Meshes", meshName);
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            if (mainBody != null && recipe.Accessories != null)
            {
                Bounds head = PaletteTools.BoneBounds(mainBody, bone => bone.Contains("Head") && !bone.Contains("Top"));
                recipe.Accessories(instance, mainBody, head);
            }

            foreach (ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particle.name.Contains("Fire") && !particle.name.Contains("Muzzle") && !particle.name.Contains("Aura") && !particle.name.Contains("Smoke")) continue;
                ParticleSystem.MainModule main = particle.main;
                main.startColor = new Color(effectTint.r, effectTint.g, effectTint.b, main.startColor.color.a);
            }

            BaseEnemyBehavior behaviour = instance.GetComponent<BaseEnemyBehavior>();
            SerializedObject serialized = new SerializedObject(behaviour);
            serialized.FindProperty("frontalArmor").floatValue = recipe.FrontalArmor;
            foreach (var projectile in recipe.Projectiles)
            {
                SerializedProperty property = serialized.FindProperty(projectile.Key);
                if (property != null) property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(projectile.Value);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            instance.transform.localScale = Vector3.one * recipe.Scale;

            string path = $"{folder}/{recipe.Name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        // re-running a build must start from the template mesh, not from an already re-dyed copy
        private static Mesh OriginalMesh(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.Contains("/World ")) return mesh;

            Debug.LogWarning("[EnemyBuilder] variant already uses a generated mesh, re-dyeing it again: " + mesh.name);
            return mesh;
        }

        private static void Register(Recipe recipe, GameObject prefab, Texture2D icon)
        {
            UnityEngine.Object database = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DATABASE);
            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty enemies = serialized.FindProperty("enemies");

            int sourceIndex = -1, targetIndex = -1;
            for (int i = 0; i < enemies.arraySize; i++)
            {
                int type = enemies.GetArrayElementAtIndex(i).FindPropertyRelative("enemyType").intValue;
                if (type == (int)recipe.SourceType) sourceIndex = i;
                if (type == (int)recipe.Type) targetIndex = i;
            }

            if (targetIndex < 0)
            {
                enemies.InsertArrayElementAtIndex(sourceIndex);
                enemies.MoveArrayElement(sourceIndex + 1, enemies.arraySize - 1);
                targetIndex = enemies.arraySize - 1;
            }

            SerializedProperty entry = enemies.GetArrayElementAtIndex(targetIndex);
            entry.FindPropertyRelative("enemyType").intValue = (int)recipe.Type;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("icon").objectReferenceValue = icon;
            entry.FindPropertyRelative("stats.hp").floatValue = recipe.Hp;
            entry.FindPropertyRelative("stats.damage.firstValue").intValue = recipe.Damage.x;
            entry.FindPropertyRelative("stats.damage.secondValue").intValue = recipe.Damage.y;
            entry.FindPropertyRelative("stats.moveSpeed").floatValue = recipe.MoveSpeed;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static Texture2D RenderIcon(GameObject prefab, string name)
        {
            using (UIArtRenderer renderer = new UIArtRenderer(new Color(0f, 0f, 0f, 0f)))
            {
                GameObject instance = renderer.Spawn(prefab);
                instance.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                Texture2D shot = renderer.Render(instance, new Vector3(0.2f, -0.15f, 1f), 0.75f, false, 30f);
                Texture2D icon = UIArtRenderer.Outline(shot, 3, 0, new Color(0.08f, 0.08f, 0.12f), Color.clear);

                string path = $"{GAME}/Gizmos/{name.Replace("Enemy ", "").ToLowerInvariant()}.png";
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), icon.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        #endregion

        #region Projectiles
        private static Dictionary<string, string> BuildIceProjectiles(string folder)
        {
            string bullets = GAME + "/Prefabs/Weapons/Bullets";
            var result = new Dictionary<string, string>
            {
                { "Ice Bullet", IceProjectile($"{bullets}/Enemy Bullet.prefab", $"{folder}/Ice Bullet.prefab", folder, null) },
                { "Snowball Grenade", IceProjectile($"{bullets}/Enemy Grenade.prefab", $"{folder}/Snowball Grenade.prefab", folder, Snowball()) },
                { "Snowball Grenade Elite", IceProjectile($"{bullets}/Enemy Grenade Elite.prefab", $"{folder}/Snowball Grenade Elite.prefab", folder, Snowball()) },
                { "Ice Boulder", IceProjectile($"{bullets}/Boss Bomb.prefab", $"{folder}/Ice Boulder.prefab", folder, IceBoulder()) },
                { "Frost Boss Bullet", IceProjectile($"{bullets}/Boss Sniper Buller.prefab", $"{folder}/Frost Boss Bullet.prefab", folder, null) },
            };
            return result;
        }

        // copies a projectile, re-dyes its meshes icy (or swaps in `replacement` for the main mesh) and tints effects
        private static string IceProjectile(string source, string target, string folder, Mesh replacement)
        {
            return ThemedProjectile(source, target, folder, replacement, t => t.Value > 0.6f ? Band.White : Band.Sky, ICE_PARTICLE);
        }

        private static string ThemedProjectile(string source, string target, string folder, Mesh replacement, Func<PaletteTools.Texel, int> rule, Color tint)
        {
            if (!File.Exists(target)) AssetDatabase.CopyAsset(source, target);
            GameObject root = PrefabUtility.LoadPrefabContents(target);
            int index = 0;
            bool replaced = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is TrailRenderer trail)
                {
                    trail.startColor = new Color(tint.r, tint.g, tint.b, trail.startColor.a);
                    trail.endColor = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                if (renderer is ParticleSystemRenderer) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                string meshName = Path.GetFileNameWithoutExtension(target).Replace(' ', '_') + "_" + index++;
                if (replacement != null && !replaced)
                {
                    filter.sharedMesh = PaletteTools.SaveMesh(UnityEngine.Object.Instantiate(replacement), folder + "/Meshes", meshName);
                    replaced = true;
                }
                else if (replacement != null)
                {
                    renderer.enabled = false;   // grenade pin / lever: a snowball has none
                    continue;
                }
                else if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
                {
                    filter.sharedMesh = PaletteTools.SaveMesh(PaletteTools.Repaint(filter.sharedMesh, null, renderer.sharedMaterial.mainTexture, rule, meshName), folder + "/Meshes", meshName);
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = PaletteTools.PlayerMaterial;
                renderer.sharedMaterials = materials;
            }

            foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.startColor = new Color(tint.r, tint.g, tint.b, main.startColor.color.a);
            }

            PrefabUtility.SaveAsPrefabAsset(root, target);
            PrefabUtility.UnloadPrefabContents(root);
            return target;
        }

        private static Mesh GasCanister()
        {
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(0.09f, 0.09f, 0.3f, 8, Vector3.zero, new Vector3(90f, 0f, 0f), Band.Grey, 0.1f, 0.5f);
            m.Cylinder(0.095f, 0.095f, 0.07f, 8, Vector3.zero, new Vector3(90f, 0f, 0f), Band.Green, 0.1f, 0.4f);
            m.Cylinder(0.05f, 0.05f, 0.06f, 6, new Vector3(0f, 0f, 0.17f), new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
            return m.Build("GasCanister");
        }

        private static Mesh Snowball()
        {
            PaletteMesh m = new PaletteMesh();
            m.Sphere(0.16f, 4, 8, Vector3.zero, Vector3.one, Band.White, 0f, 0.3f);
            m.Sphere(0.08f, 2, 6, new Vector3(0.09f, 0.08f, -0.05f), Vector3.one, Band.Ice, 0f, 0.3f);
            return m.Build("Snowball");
        }

        private static Mesh IceBoulder()
        {
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(0.55f, 0.5f, 0.5f), 0.14f, Vector3.zero, new Vector3(12f, 30f, 8f), Band.Sky, 0f, 0.45f);
            m.Box(new Vector3(0.35f, 0.3f, 0.3f), 0.1f, new Vector3(0.12f, 0.18f, -0.05f), new Vector3(-10f, 10f, 25f), Band.Ice, 0f, 0.3f);
            return m.Build("IceBoulder");
        }

        private static Material IceLaserMaterial(string folder)
        {
            string path = folder + "/Laser Pointer Ice.mat";
            if (!File.Exists(path)) AssetDatabase.CopyAsset(GAME + "/Materials/Particles/Laser Pointer.mat", path);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            foreach (string property in new[] { "_Color", "_BaseColor", "_TintColor" })
                if (material.HasProperty(property)) material.SetColor(property, new Color(0.35f, 0.85f, 1f, material.GetColor(property).a));
            EditorUtility.SetDirty(material);
            return material;
        }
        #endregion

        #region Accessories
        private static float R(Bounds head) => Mathf.Max(head.extents.x, head.extents.z);

        private static void Attach(GameObject root, SkinnedMeshRenderer body, string bone, string name, PaletteMesh mesh)
        {
            string folder = meshFolder;
            Transform parent = FindBoneContains(body, bone) ?? root.transform;
            string meshName = root.name.Replace(' ', '_') + "_" + name.Replace(' ', '_');
            PaletteTools.Attach(parent, "Accessory " + name, PaletteTools.SaveMesh(mesh.Build(meshName), folder, meshName));
        }

        private static Transform FindBoneContains(SkinnedMeshRenderer body, string part)
        {
            foreach (Transform bone in body.bones)
                if (bone != null && bone.name.Contains(part) && !bone.name.Contains("Top") && !bone.name.Contains("End")) return bone;
            return null;
        }

        private static void FurHood(GameObject root, SkinnedMeshRenderer body, Bounds head, int fur, int lining)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Dome(r * 1.22f, 3, 10, c + new Vector3(0f, -r * 0.1f, -r * 0.12f), new Vector3(12f, 0f, 0f), new Vector3(1f, 1.1f, 1.1f), lining, 0.1f, 0.6f);
            m.Cylinder(r * 1.22f, r * 1.18f, r * 0.42f, 10, c + new Vector3(0f, -r * 0.05f, r * 0.62f), new Vector3(80f, 0f, 0f), fur, 0f, 0.25f);
            Attach(root, body, "Head", "Hood", m);
        }

        private static void Goggles(GameObject root, SkinnedMeshRenderer body, Bounds head, int lens)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(r * 2.1f, r * 0.18f, r * 0.2f), r * 0.05f, c + new Vector3(0f, r * 0.32f, r * 0.82f), Vector3.zero, Band.Black, 0f, 0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = c + new Vector3(side * r * 0.36f, r * 0.32f, r * 0.98f);
                m.Cylinder(r * 0.3f, r * 0.3f, r * 0.12f, 8, p, new Vector3(90f, 0f, 0f), Band.Black, 0f, 0.4f);
                m.Cylinder(r * 0.22f, r * 0.22f, r * 0.12f, 8, p + new Vector3(0f, 0f, r * 0.04f), new Vector3(90f, 0f, 0f), lens, 0f, 0.4f);
            }
            Attach(root, body, "Head", "Goggles", m);
        }

        private static void Beanie(GameObject root, SkinnedMeshRenderer body, Bounds head, int band, int pompom)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(r * 1.06f, r * 0.8f, r * 0.55f, 10, c + new Vector3(0f, r * 0.55f, 0f), Vector3.zero, band, 0.05f, 0.45f);
            m.Dome(r * 0.8f, 3, 10, c + new Vector3(0f, r * 0.82f, 0f), Vector3.zero, new Vector3(1f, 0.6f, 1f), band, 0f, 0.3f);
            m.Cylinder(r * 1.12f, r * 1.12f, r * 0.3f, 10, c + new Vector3(0f, r * 0.3f, 0f), Vector3.zero, Band.White, 0f, 0.35f);
            m.Sphere(r * 0.3f, 3, 8, c + new Vector3(0f, r * 1.42f, 0f), Vector3.one, pompom, 0.35f, 0.85f);
            Attach(root, body, "Head", "Beanie", m);
        }

        private static void YetiHead(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            for (int side = -1; side <= 1; side += 2)
            {
                m.Cylinder(r * 0.22f, 0f, r * 0.9f, 6, c + new Vector3(side * r * 0.85f, r * 0.75f, 0f), new Vector3(0f, 0f, -side * 35f), Band.Cream, 0.1f, 0.6f);
                m.Sphere(r * 0.22f, 2, 6, c + new Vector3(side * r * 0.32f, r * 0.15f, r * 0.92f), Vector3.one, Band.Sky, 0f, 0.2f);
            }
            m.Box(new Vector3(r * 1.3f, r * 0.22f, r * 0.3f), r * 0.08f, c + new Vector3(0f, r * 0.42f, r * 0.88f), new Vector3(10f, 0f, 0f), Band.White, 0f, 0.25f);
            m.Box(new Vector3(r * 0.7f, r * 0.3f, r * 0.2f), r * 0.08f, c + new Vector3(0f, -r * 0.42f, r * 0.9f), Vector3.zero, Band.Steel, 0.3f, 0.7f);
            Attach(root, body, "Head", "Yeti Face", m);

            Bounds chest = PaletteTools.BoneBounds(body, bone => bone.Contains("Spine"));
            PaletteMesh mane = new PaletteMesh();
            mane.Dome(chest.extents.x * 1.25f, 3, 10, new Vector3(chest.center.x, chest.max.y - chest.extents.y * 0.35f, chest.center.z), Vector3.zero, new Vector3(1f, 0.55f, 0.9f), Band.White, 0f, 0.3f);
            Attach(root, body, "Spine", "Mane", mane);
        }

        private static void IceSpikes(GameObject root, SkinnedMeshRenderer body)
        {
            Bounds chest = PaletteTools.BoneBounds(body, bone => bone.Contains("Spine"));
            PaletteMesh m = new PaletteMesh();
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = new Vector3(chest.center.x + i * chest.extents.x * 0.45f, chest.max.y - chest.extents.y * (0.2f + Mathf.Abs(i) * 0.25f), chest.min.z + chest.extents.z * 0.2f);
                m.Cylinder(chest.extents.x * 0.18f, 0f, chest.extents.y * (0.9f - Mathf.Abs(i) * 0.25f), 5, p, new Vector3(-35f, 0f, -i * 20f), Band.Sky, 0f, 0.45f);
            }
            Attach(root, body, "Spine", "Ice Spikes", m);
        }

        private static void Crown(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center + new Vector3(0f, r * 0.95f, 0f);
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(r * 0.75f, r * 0.8f, r * 0.3f, 8, c, Vector3.zero, Band.Gold, 0.05f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                m.Cylinder(r * 0.16f, 0f, r * 0.5f, 5, c + new Vector3(Mathf.Cos(a) * r * 0.7f, r * 0.38f, Mathf.Sin(a) * r * 0.7f), Vector3.zero, Band.Sky, 0f, 0.4f);
            }
            Attach(root, body, "Head", "Crown", m);
        }

        private static void RiotHelmet(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Dome(r * 1.18f, 3, 10, c + new Vector3(0f, -r * 0.05f, 0f), Vector3.zero, new Vector3(1f, 1.05f, 1.05f), Band.Navy, 0.05f, 0.55f);
            m.Cylinder(r * 1.22f, r * 1.22f, r * 0.12f, 10, c + new Vector3(0f, -r * 0.05f, 0f), Vector3.zero, Band.Yellow, 0.35f, 0.75f);
            m.Box(new Vector3(r * 1.7f, r * 0.62f, r * 0.22f), r * 0.08f, c + new Vector3(0f, -r * 0.32f, r * 1.0f), new Vector3(-8f, 0f, 0f), Band.Black, 0f, 0.35f);
            m.Box(new Vector3(r * 1.5f, r * 0.1f, r * 0.08f), r * 0.03f, c + new Vector3(0f, -r * 0.12f, r * 1.13f), new Vector3(-8f, 0f, 0f), Band.Sky, 0f, 0.2f);
            for (int side = -1; side <= 1; side += 2)
                m.Cylinder(r * 0.3f, r * 0.3f, r * 0.16f, 8, c + new Vector3(side * r * 1.15f, -r * 0.2f, 0f), new Vector3(0f, 0f, 90f), Band.Steel, 0.2f, 0.6f);
            Attach(root, body, "Head", "Riot Helmet", m);
        }

        // riot shield on the forearm that does not hold the weapon, placed in the idle pose
        private static void RiotShield(GameObject root, SkinnedMeshRenderer body)
        {
            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name.Contains("Idle")) { clip.SampleAnimation(animator.gameObject, 0f); break; }

            const float W = 0.24f, H = 0.95f, T = 0.055f;
            PaletteMesh m = new PaletteMesh();
            for (int i = -1; i <= 1; i++)
                m.Box(new Vector3(W, H, T), 0.02f, new Vector3(i * W * 0.96f, 0f, -Mathf.Abs(i) * 0.05f), new Vector3(0f, i * 14f, 0f), Band.Steel, 0.05f, 0.45f);
            m.Box(new Vector3(W * 1.5f, 0.11f, 0.03f), 0.01f, new Vector3(0f, H * 0.3f, T * 0.5f), Vector3.zero, Band.Black, 0f, 0.3f);
            m.Box(new Vector3(W * 0.98f, 0.16f, T + 0.012f), 0.01f, new Vector3(0f, -H * 0.38f, 0f), Vector3.zero, Band.Yellow, 0.3f, 0.6f);
            for (int i = -2; i <= 2; i++)
                m.Box(new Vector3(0.04f, 0.14f, T + 0.02f), 0.005f, new Vector3(i * 0.065f, -H * 0.38f, 0f), new Vector3(0f, 0f, 40f), Band.Black, 0f, 0.3f);
            m.Box(new Vector3(0.28f, 0.11f, 0.03f), 0.01f, new Vector3(0f, H * 0.12f, T * 0.5f + 0.005f), Vector3.zero, Band.White, 0f, 0.2f);
            m.Box(new Vector3(0.08f, 0.3f, 0.1f), 0.02f, new Vector3(0f, 0f, -T - 0.04f), Vector3.zero, Band.Black, 0f, 0.4f);
            string meshName = root.name.Replace(' ', '_') + "_Shield";
            Mesh mesh = PaletteTools.SaveMesh(m.Build(meshName), meshFolder, meshName);

            Transform leftArm = PaletteTools.FindBone(body, "LeftForeArm"), rightArm = PaletteTools.FindBone(body, "RightForeArm");
            Renderer weapon = null;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.Contains("Weapon")) weapon = renderer;
            Transform forearm = leftArm;
            if (weapon != null && leftArm != null && rightArm != null)
                forearm = Vector3.Distance(weapon.bounds.center, leftArm.position) < Vector3.Distance(weapon.bounds.center, rightArm.position) ? rightArm : leftArm;

            Bounds all = PaletteTools.BoneBounds(body, bone => true);
            float side = forearm != null ? Mathf.Sign(forearm.position.x - all.center.x) : 1f;
            float armX = forearm != null ? forearm.position.x : all.center.x + all.extents.x * 0.5f;

            GameObject shield = new GameObject("Accessory Riot Shield");
            shield.transform.position = new Vector3(Mathf.Lerp(all.center.x, armX, 0.85f), all.center.y - all.extents.y * 0.12f, all.max.z + 0.08f);
            shield.transform.rotation = Quaternion.Euler(0f, side * 28f, 0f);
            shield.AddComponent<MeshFilter>().sharedMesh = mesh;
            shield.AddComponent<MeshRenderer>().sharedMaterial = PaletteTools.PlayerMaterial;
            shield.transform.SetParent(forearm != null ? forearm : root.transform, true);
        }

        private static void GasMask(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Sphere(r * 0.62f, 3, 9, c + new Vector3(0f, -r * 0.1f, r * 0.62f), new Vector3(1.25f, 1.1f, 0.7f), Band.Black, 0f, 0.4f);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Cylinder(r * 0.22f, r * 0.22f, r * 0.12f, 8, c + new Vector3(side * r * 0.32f, r * 0.2f, r * 1.0f), new Vector3(90f, 0f, 0f), Band.Sky, 0f, 0.3f);
                m.Cylinder(r * 0.22f, r * 0.25f, r * 0.4f, 8, c + new Vector3(side * r * 0.45f, -r * 0.55f, r * 0.85f), new Vector3(60f, side * 25f, 0f), Band.Green, 0.1f, 0.5f);
            }
            m.Box(new Vector3(r * 2.2f, r * 0.12f, r * 0.12f), r * 0.03f, c + new Vector3(0f, r * 0.2f, 0f), Vector3.zero, Band.Black, 0f, 0.3f);
            Attach(root, body, "Head", "Gas Mask", m);
        }

        private static void TacticalHelmet(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Dome(r * 1.14f, 3, 10, c + new Vector3(0f, r * 0.05f, 0f), Vector3.zero, new Vector3(1f, 0.95f, 1.05f), Band.Black, 0.1f, 0.5f);
            m.Box(new Vector3(r * 0.35f, r * 0.3f, r * 0.3f), r * 0.05f, c + new Vector3(0f, r * 0.85f, r * 0.85f), Vector3.zero, Band.Grey, 0.2f, 0.5f);
            Attach(root, body, "Head", "Tactical Helmet", m);
        }

        private static void Beret(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(r * 1.02f, r * 1.02f, r * 0.14f, 10, c + new Vector3(0f, r * 0.62f, 0f), Vector3.zero, Band.Black, 0.1f, 0.4f);
            m.Dome(r * 1.1f, 2, 10, c + new Vector3(r * 0.15f, r * 0.68f, 0f), new Vector3(0f, 0f, -12f), new Vector3(1.05f, 0.35f, 1f), Band.Red, 0.1f, 0.55f);
            m.Sphere(r * 0.14f, 2, 6, c + new Vector3(-r * 0.45f, r * 0.75f, r * 0.9f), Vector3.one, Band.Gold, 0.1f, 0.4f);
            Attach(root, body, "Head", "Beret", m);
        }

        private static void BombSuitHelmet(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Sphere(r * 1.25f, 4, 10, c + new Vector3(0f, r * 0.05f, 0f), new Vector3(1f, 1.05f, 1f), Band.Navy, 0.1f, 0.55f);
            m.Box(new Vector3(r * 1.6f, r * 0.7f, r * 0.2f), r * 0.1f, c + new Vector3(0f, r * 0.1f, r * 1.15f), new Vector3(-6f, 0f, 0f), Band.Sky, 0f, 0.35f);
            m.Cylinder(r * 1.3f, r * 1.4f, r * 0.35f, 10, c + new Vector3(0f, -r * 1.05f, 0f), Vector3.zero, Band.Yellow, 0.3f, 0.7f);
            Attach(root, body, "Head", "Bomb Suit Helmet", m);
        }

        private static void ChestPlate(GameObject root, SkinnedMeshRenderer body)
        {
            Bounds chest = PaletteTools.BoneBounds(body, bone => bone.Contains("Spine"));
            PaletteMesh m = new PaletteMesh();
            m.Box(new Vector3(chest.size.x * 0.75f, chest.size.y * 0.55f, chest.size.z * 0.18f), chest.size.x * 0.05f, new Vector3(chest.center.x, chest.center.y + chest.extents.y * 0.15f, chest.max.z), new Vector3(-6f, 0f, 0f), Band.Steel, 0.05f, 0.5f);
            m.Box(new Vector3(chest.size.x * 0.5f, chest.size.y * 0.08f, chest.size.z * 0.2f), chest.size.x * 0.02f, new Vector3(chest.center.x, chest.center.y + chest.extents.y * 0.42f, chest.max.z + chest.size.z * 0.02f), new Vector3(-6f, 0f, 0f), Band.Yellow, 0.3f, 0.6f);
            Attach(root, body, "Spine", "Chest Plate", m);
        }

        private static void OfficerCap(GameObject root, SkinnedMeshRenderer body, Bounds head)
        {
            float r = R(head);
            Vector3 c = head.center;
            PaletteMesh m = new PaletteMesh();
            m.Cylinder(r * 0.98f, r * 1.12f, r * 0.42f, 10, c + new Vector3(0f, r * 0.72f, -0.02f), new Vector3(6f, 0f, 0f), Band.Navy, 0.05f, 0.5f);
            m.Cylinder(r * 1.0f, r * 1.0f, r * 0.12f, 10, c + new Vector3(0f, r * 0.52f, 0f), Vector3.zero, Band.Ice, 0f, 0.3f);
            m.Box(new Vector3(r * 1.1f, r * 0.06f, r * 0.55f), r * 0.02f, c + new Vector3(0f, r * 0.48f, r * 0.95f), new Vector3(-12f, 0f, 0f), Band.Black, 0f, 0.3f);
            m.Sphere(r * 0.15f, 2, 6, c + new Vector3(0f, r * 0.75f, r * 1.05f), Vector3.one, Band.Gold, 0.1f, 0.4f);
            Attach(root, body, "Head", "Cap", m);
        }
        #endregion
    }
}
