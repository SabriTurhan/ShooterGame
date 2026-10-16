using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    // Generates levels straight into the WorldData assets (the same format the Level Editor writes).
    // Rooms are laid out on a coarse grid inside the arena, built from cover patterns, validated for
    // reachability and checked against every other room for duplicates. Rewards and difficulty come
    // from Tools/Balance/balance.json, so levels can be regenerated any time the balance changes.
    public static class LevelGenerator
    {
        private const string DATABASE = "Assets/Project Files/Data/Level System/Levels Database.asset";

        // arena grid: 8 columns x 13 rows of 2 world units; x -7.8..7.8, z -10.7..14.6
        private const float UNIT = 0.9765625f;
        private const float CELL = UNIT * 2f;
        private const int COLS = 8;
        private const int ROWS = 13;
        private static float CellX(float column) => (column - 3.5f) * CELL;
        private static float CellZ(float row) => -10f * UNIT + row * CELL;

        private static readonly Quaternion FACING_PLAYER = Quaternion.Euler(0f, 180f, 0f);

        #region Menu
        [MenuItem("Tools/Levels/Generate Worlds 1-2 (40 levels)")]
        public static void GenerateWorlds12()
        {
            // keep the hand-made template levels (1-10 desert, 21-30 crystal), fill the rest, replace copied rooms
            Generate(0, 10, 19);
            Generate(1, 10, 19);
            ReplaceDuplicateRooms(1);
        }

        [MenuItem("Tools/Levels/Render Room Previews")]
        public static void RenderPreviewsMenu() => RenderPreviews(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/RoomPreviews"));
        #endregion

        #region Generation
        public static void Generate(int worldIndex, int fromLevel, int toLevel)
        {
            BalanceData balance = BalanceData.Load();
            if (balance == null) return;

            LevelsDatabase database = AssetDatabase.LoadAssetAtPath<LevelsDatabase>(DATABASE);
            WorldData world = database.Worlds[worldIndex];
            Dictionary<string, WeaponData> weapons = BalanceImporter.WeaponsByName(balance);
            Palette palette = new Palette(world);

            HashSet<string> signatures = CollectSignatures(world, fromLevel, toLevel);

            SerializedObject serialized = new SerializedObject(world);
            SerializedProperty levels = serialized.FindProperty("levels");
            while (levels.arraySize <= toLevel) levels.InsertArrayElementAtIndex(levels.arraySize - 1);

            for (int l = fromLevel; l <= toLevel; l++)
            {
                int number = BalanceData.LevelNumber(worldIndex, l);
                BalanceData.LevelJson data = balance.GetLevel(number);

                List<RoomPlan> rooms = new List<RoomPlan>();
                for (int r = 0; r < data.rooms; r++)
                    rooms.Add(BuildRoom(palette, world, number, r, data.rooms, data.boss, signatures));

                PlaceChests(rooms, number, data.boss);
                WriteLevel(levels.GetArrayElementAtIndex(l), rooms, data, weapons, palette);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LevelGenerator] world {worldIndex + 1}: generated levels {fromLevel + 1}-{toLevel + 1}");
        }

        // rooms that repeat another room of the world (the template ships a few copies) get regenerated
        public static void ReplaceDuplicateRooms(int worldIndex)
        {
            LevelsDatabase database = AssetDatabase.LoadAssetAtPath<LevelsDatabase>(DATABASE);
            WorldData world = database.Worlds[worldIndex];
            Palette palette = new Palette(world);

            HashSet<string> seen = new HashSet<string>();
            List<HashSet<string>> kept = new List<HashSet<string>>();
            SerializedObject serialized = new SerializedObject(world);
            SerializedProperty levels = serialized.FindProperty("levels");
            int replaced = 0;

            for (int l = 0; l < levels.arraySize; l++)
            {
                SerializedProperty rooms = levels.GetArrayElementAtIndex(l).FindPropertyRelative("rooms");
                bool boss = levels.GetArrayElementAtIndex(l).FindPropertyRelative("type").intValue == (int)LevelType.Boss;

                for (int r = 0; r < rooms.arraySize; r++)
                {
                    string signature = Signature(rooms.GetArrayElementAtIndex(r), palette);
                    HashSet<string> parts = new HashSet<string>(signature.Split(';'));

                    // near-copies count too: more than 80% of the obstacles in the same place
                    bool copy = kept.Any(other => Similarity(parts, other) > 0.8f);
                    if (!copy)
                    {
                        seen.Add(signature);
                        kept.Add(parts);
                        continue;
                    }

                    int number = BalanceData.LevelNumber(worldIndex, l);
                    RoomPlan plan = BuildRoom(palette, world, number, r + 100, rooms.arraySize, boss, seen);
                    WriteRoom(rooms.GetArrayElementAtIndex(r), plan, palette);
                    kept.Add(new HashSet<string>(Signature(rooms.GetArrayElementAtIndex(r), palette).Split(';')));
                    replaced++;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LevelGenerator] world {worldIndex + 1}: replaced {replaced} duplicate rooms");
        }

        private static RoomPlan BuildRoom(Palette palette, WorldData world, int levelNumber, int roomIndex, int roomCount, bool boss, HashSet<string> signatures)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                System.Random rng = new System.Random(levelNumber * 7919 + roomIndex * 104729 + attempt * 31);
                RoomPlan plan = new RoomPlan(rng.Next(world.RoomEnvPresets.Length));

                if (boss)
                    BossLayout(plan, rng);
                else
                    CoverLayout(plan, rng, levelNumber);

                if (!plan.Grid.IsConnected()) continue;

                if (boss)
                    plan.Enemies.Add(new EnemyPlan(BossFor(levelNumber), new Vector3(0f, 0f, CellZ(8)), levelNumber >= 40));
                else
                    PlaceEnemies(plan, rng, levelNumber, roomIndex, roomCount);

                string signature = plan.Signature();
                if (signatures.Contains(signature)) continue;

                signatures.Add(signature);
                return plan;
            }

            throw new Exception($"[LevelGenerator] could not build room {roomIndex + 1} of level {levelNumber}");
        }

        private static void BossLayout(RoomPlan plan, System.Random rng)
        {
            // open arena with four pillars to break line of sight
            string pillar = rng.Next(2) == 0 ? "Block 1x1 Big" : "Block 1x1 Medium";
            int inner = rng.Next(2);
            foreach ((int c, int r) in new[] { (1 + inner, 4), (6 - inner, 4), (1 + inner, 10), (6 - inner, 10) })
                plan.Place(pillar, c, r, 0);
        }

        // two or three cover patterns stacked in bands between the spawn and the exit
        private static void CoverLayout(RoomPlan plan, System.Random rng, int levelNumber)
        {
            bool mirror = rng.NextDouble() < 0.65;
            int density = Mathf.Clamp(1 + levelNumber / 25, 1, 4);

            Pattern[] near = { Pillars, CoverLine, Bunkers, Scatter, Zigzag };
            Pattern[] far = { CoverLine, Fort, Lanes, Pillars, Bunkers, Zigzag, Scatter };

            near[rng.Next(near.Length)](plan, rng, 2, 5, mirror, density);
            far[rng.Next(far.Length)](plan, rng, 6, 10, mirror, density);

            if (rng.NextDouble() < 0.5 + levelNumber * 0.004)
                Scatter(plan, rng, 2, 11, mirror, 1);
        }

        private delegate void Pattern(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density);

        private static readonly string[] SMALL = { "Block 1x1 Small", "Block 1x1 Small Half", "Block 1x1 Medium Half" };
        private static readonly string[] MEDIUM = { "Block 1x1 Medium", "Block 1x1 Big Half", "Block 1x1 Medium Half" };
        private static readonly string[] BIG = { "Block 1x1 Big", "Block 1x1 Medium" };
        private static readonly string[] LONG = { "Block 1x2 Big", "Block 1x2 Medium", "Block 1x2 Big Half", "Block 1x2 Medium Half", "Block 1x2 Small" };

        private static string Pick(System.Random rng, string[] options) => options[rng.Next(options.Length)];

        private static void PlaceMirrored(RoomPlan plan, string item, int column, int row, int rotation, bool mirror, int width = 1)
        {
            plan.Place(item, column, row, rotation);
            if (mirror) plan.Place(item, COLS - column - width, row, rotation);
        }

        private static void Pillars(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            string item = Pick(rng, rng.Next(2) == 0 ? BIG : MEDIUM);
            int step = density >= 3 ? 2 : 3;
            int offset = rng.Next(2);
            for (int r = rowFrom + rng.Next(2); r <= rowTo; r += 2)
                for (int c = offset + (r / 2) % 2; c < (mirror ? COLS / 2 : COLS); c += step)
                    PlaceMirrored(plan, item, c, r, 0, mirror);
        }

        private static void CoverLine(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            int lines = 1 + (density >= 3 && rowTo - rowFrom >= 3 ? 1 : 0);
            for (int i = 0; i < lines; i++)
            {
                int row = rowFrom + rng.Next(Mathf.Max(1, rowTo - rowFrom - 1)) + i * 2;
                string item = Pick(rng, LONG);
                // horizontal 1x2 pieces with a gap so the player can always walk through
                int gap = rng.Next(1, 3);
                for (int c = 0; c < (mirror ? COLS / 2 : COLS) - 1; c += 2)
                {
                    if (c / 2 == gap && !mirror) continue;
                    if (mirror && c == 2) continue;
                    PlaceMirrored(plan, item, c, row, 90, mirror, 2);
                }
            }
        }

        private static void Bunkers(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            int count = 1 + density / 2;
            for (int i = 0; i < count; i++)
            {
                int row = rowFrom + rng.Next(Mathf.Max(1, rowTo - rowFrom));
                int column = rng.Next(0, mirror ? 2 : COLS - 1);
                PlaceMirrored(plan, "Block 2x2 Big Half", column, row, 0, mirror, 2);
            }
        }

        private static void Scatter(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            int count = 2 + density + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                int row = rng.Next(rowFrom, rowTo + 1);
                int column = rng.Next(0, mirror ? COLS / 2 : COLS);
                PlaceMirrored(plan, Pick(rng, rng.Next(3) == 0 ? MEDIUM : SMALL), column, row, rng.Next(4) * 90, mirror);
            }
        }

        private static void Zigzag(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            string item = Pick(rng, MEDIUM);
            bool left = rng.Next(2) == 0;
            for (int r = rowFrom; r <= rowTo; r += 2)
            {
                int column = left ? 1 : 4;
                PlaceMirrored(plan, item, column, r, 0, mirror);
                PlaceMirrored(plan, item, column + 1, r, 0, mirror);
                left = !left;
            }
        }

        private static void Lanes(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            // two vertical walls split the far half into three lanes
            int column = rng.Next(1, 3);
            for (int r = rowFrom; r < rowTo; r += 2)
                PlaceMirrored(plan, Pick(rng, LONG), column, r, 0, true);
        }

        private static void Fort(RoomPlan plan, System.Random rng, int rowFrom, int rowTo, bool mirror, int density)
        {
            // U-shaped cover open towards the exit; enemies like to sit inside
            int row = rng.Next(rowFrom, Mathf.Max(rowFrom + 1, rowTo - 2));
            plan.Place("Block 1x2 Medium", 2, row, 90);
            plan.Place("Block 1x2 Medium", 4, row, 90);
            plan.Place("Block 1x1 Medium", 2, row + 1, 0);
            plan.Place("Block 1x1 Medium", 5, row + 1, 0);
        }
        #endregion

        #region Enemies & chests
        // enemy type, first level it appears in, spawn weight
        private static readonly (EnemyType type, int from, float weight)[] ENEMY_POOL =
        {
            (EnemyType.Pistol, 1, 3f),
            (EnemyType.BatMelee, 2, 3f),
            (EnemyType.Demo, 5, 1.4f),
            (EnemyType.Rifle, 8, 2f),
            (EnemyType.Sniper, 12, 1f),
            (EnemyType.Cowboy, 16, 1.6f),
            (EnemyType.Grenader, 22, 1.8f),
            (EnemyType.Shotguner, 26, 1.8f),
        };

        private static EnemyType BossFor(int levelNumber)
        {
            return (levelNumber / 10) % 2 == 1 || levelNumber == 40 ? EnemyType.BossSniper : EnemyType.BossBomber;
        }

        private static void PlaceEnemies(RoomPlan plan, System.Random rng, int levelNumber, int roomIndex, int roomCount)
        {
            int blockPosition = (levelNumber - 1) % 10;
            int count = 2 + levelNumber / 14 + (roomIndex == roomCount - 1 ? 1 : 0) + (blockPosition >= 7 ? 1 : 0) + rng.Next(2);
            count = Mathf.Clamp(count, 2, 9);

            List<(EnemyType type, float weight)> pool = ENEMY_POOL.Where(e => e.from <= levelNumber).Select(e => (e.type, e.weight)).ToList();
            float eliteChance = levelNumber < 6 ? 0f : Mathf.Min(0.35f, 0.04f + levelNumber * 0.004f);

            List<EnemyType> types = new List<EnemyType>();

            // a type that is new on this level shows up in the first room
            if (roomIndex == 0)
                foreach (var e in ENEMY_POOL.Where(e => e.from == levelNumber)) types.Add(e.type);

            while (types.Count < count)
            {
                float roll = (float)rng.NextDouble() * pool.Sum(p => p.weight);
                foreach (var p in pool)
                {
                    roll -= p.weight;
                    if (roll <= 0f) { types.Add(p.type); break; }
                }
            }

            List<Vector2Int> taken = new List<Vector2Int>();
            foreach (EnemyType type in types)
            {
                int minRow = type == EnemyType.Sniper ? 8 : (type == EnemyType.BatMelee || type == EnemyType.Demo) ? 4 : 5;
                Vector2Int? cell = BestEnemyCell(plan, rng, minRow, taken, type != EnemyType.BatMelee && type != EnemyType.Demo);
                if (cell == null) continue;

                taken.Add(cell.Value);
                Vector3 position = new Vector3(CellX(cell.Value.x), 0f, CellZ(cell.Value.y));
                plan.Enemies.Add(new EnemyPlan(type, position, rng.NextDouble() < eliteChance));
            }
        }

        private static Vector2Int? BestEnemyCell(RoomPlan plan, System.Random rng, int minRow, List<Vector2Int> taken, bool likesCover)
        {
            Vector2Int? best = null;
            float bestScore = float.MinValue;

            for (int r = minRow; r <= 11; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    if (!plan.Grid.IsFree(c, r) || !plan.Grid.IsReachable(c, r)) continue;
                    if (taken.Any(t => Mathf.Abs(t.x - c) + Mathf.Abs(t.y - r) < 3)) continue;

                    float score = (float)rng.NextDouble() * 2f;
                    if (likesCover && plan.Grid.IsBlocked(c, r - 1)) score += 1.5f;   // cover between enemy and player
                    if (c == 0 || c == COLS - 1) score -= 0.5f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new Vector2Int(c, r);
                    }
                }
            }

            return best;
        }

        private static void PlaceChests(List<RoomPlan> rooms, int levelNumber, bool boss)
        {
            if (boss || levelNumber < 3) return;

            System.Random rng = new System.Random(levelNumber * 977);

            // a regular chest most levels, a rewarded (video) chest every fifth level
            RoomPlan room = rooms[rng.Next(Mathf.Max(1, rooms.Count - 1))];
            AddChest(room, rng, LevelChestType.Standart);

            if (levelNumber % 5 == 3)
                AddChest(rooms[rooms.Count - 1], rng, LevelChestType.Rewarded);
        }

        private static void AddChest(RoomPlan room, System.Random rng, LevelChestType type)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int c = rng.Next(2) == 0 ? 0 : COLS - 1;
                int r = rng.Next(3, 11);
                if (!room.Grid.IsFree(c, r) || !room.Grid.IsReachable(c, r)) continue;
                if (room.Enemies.Any(e => Vector3.Distance(e.Position, new Vector3(CellX(c), 0f, CellZ(r))) < CELL)) continue;

                room.Grid.Block(c, r);
                room.Chests.Add((type, new Vector3(CellX(c), 0f, CellZ(r)), Quaternion.Euler(0f, c == 0 ? 90f : -90f, 0f)));
                return;
            }
        }
        #endregion

        #region Writing
        private static void WriteLevel(SerializedProperty level, List<RoomPlan> rooms, BalanceData.LevelJson data, Dictionary<string, WeaponData> weapons, Palette palette)
        {
            level.FindPropertyRelative("type").intValue = data.boss ? (int)LevelType.Boss : (int)LevelType.Default;
            level.FindPropertyRelative("requiredUpg").intValue = 0;
            level.FindPropertyRelative("hasCharacterSuggestion").boolValue = data.n % 6 == 0;
            level.FindPropertyRelative("healSpawnPercent").floatValue = 0.4f;
            level.FindPropertyRelative("specialBehaviours").arraySize = 0;

            BalanceImporter.WriteLevelBalance(level, data, weapons);

            SerializedProperty roomsProperty = level.FindPropertyRelative("rooms");
            roomsProperty.arraySize = rooms.Count;
            for (int r = 0; r < rooms.Count; r++)
                WriteRoom(roomsProperty.GetArrayElementAtIndex(r), rooms[r], palette);
        }

        private static void WriteRoom(SerializedProperty room, RoomPlan plan, Palette palette)
        {
            RoomEnvironmentPreset preset = palette.Presets[plan.Preset];
            room.FindPropertyRelative("spawnPoint").vector3Value = palette.SpawnOf(preset);

            List<ItemEntityData> items = new List<ItemEntityData>(palette.ItemsOf(preset));
            foreach (ItemPlan item in plan.Items)
                items.Add(new ItemEntityData(palette.Hash(item.Name), palette.WorldPosition(item), Quaternion.Euler(0f, item.Rotation, 0f), Vector3.one));

            SerializedProperty itemsProperty = room.FindPropertyRelative("itemEntities");
            itemsProperty.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                SerializedProperty e = itemsProperty.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Hash").intValue = items[i].Hash;
                e.FindPropertyRelative("Position").vector3Value = items[i].Position;
                e.FindPropertyRelative("Rotation").quaternionValue = items[i].Rotation;
                e.FindPropertyRelative("Scale").vector3Value = items[i].Scale;
            }

            SerializedProperty enemies = room.FindPropertyRelative("enemyEntities");
            enemies.arraySize = plan.Enemies.Count;
            for (int i = 0; i < plan.Enemies.Count; i++)
            {
                SerializedProperty e = enemies.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("EnemyType").intValue = (int)plan.Enemies[i].Type;
                e.FindPropertyRelative("Position").vector3Value = plan.Enemies[i].Position;
                e.FindPropertyRelative("Rotation").quaternionValue = FACING_PLAYER;
                e.FindPropertyRelative("Scale").vector3Value = Vector3.one;
                e.FindPropertyRelative("IsElite").boolValue = plan.Enemies[i].Elite;
                e.FindPropertyRelative("PathPoints").arraySize = 0;
            }

            SerializedProperty chests = room.FindPropertyRelative("chestEntities");
            chests.arraySize = plan.Chests.Count;
            for (int i = 0; i < plan.Chests.Count; i++)
            {
                SerializedProperty e = chests.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("IsInited").boolValue = false;
                e.FindPropertyRelative("ChestType").intValue = (int)plan.Chests[i].type;
                e.FindPropertyRelative("RewardCurrency").intValue = (int)CurrencyType.Coins;
                e.FindPropertyRelative("RewardValue").intValue = 5;
                e.FindPropertyRelative("DroppedCurrencyItemsAmount").intValue = 6;
                e.FindPropertyRelative("Position").vector3Value = plan.Chests[i].position;
                e.FindPropertyRelative("Rotation").quaternionValue = plan.Chests[i].rotation;
                e.FindPropertyRelative("Scale").vector3Value = Vector3.one;
            }

            room.FindPropertyRelative("roomCustomObjects").arraySize = 0;
        }
        #endregion

        #region Signatures
        private static HashSet<string> CollectSignatures(WorldData world, int skipFrom, int skipTo)
        {
            HashSet<string> result = new HashSet<string>();
            Palette palette = new Palette(world);
            SerializedProperty levels = new SerializedObject(world).FindProperty("levels");

            for (int l = 0; l < levels.arraySize; l++)
            {
                if (l >= skipFrom && l <= skipTo) continue;

                SerializedProperty rooms = levels.GetArrayElementAtIndex(l).FindPropertyRelative("rooms");
                for (int r = 0; r < rooms.arraySize; r++)
                    result.Add(Signature(rooms.GetArrayElementAtIndex(r), palette));
            }

            return result;
        }

        private static float Similarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0f;
            return a.Count(b.Contains) / (float)Mathf.Max(a.Count, b.Count);
        }

        // obstacles only, rounded to the grid, so copied rooms match even with different enemies
        private static string Signature(SerializedProperty room, Palette palette)
        {
            SerializedProperty items = room.FindPropertyRelative("itemEntities");
            List<string> parts = new List<string>();
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                int hash = item.FindPropertyRelative("Hash").intValue;
                if (!palette.IsObstacle(hash)) continue;

                Vector3 p = item.FindPropertyRelative("Position").vector3Value;
                parts.Add(palette.NameOf(hash) + "@" + Mathf.RoundToInt(p.x / UNIT) + "," + Mathf.RoundToInt(p.z / UNIT));
            }

            parts.Sort();
            return string.Join(";", parts);
        }
        #endregion

        #region Previews
        // renders every room of every world top-down into PNG files (for reviewing generated levels)
        public static void RenderPreviews(string folder)
        {
            Directory.CreateDirectory(folder);
            LevelsDatabase database = AssetDatabase.LoadAssetAtPath<LevelsDatabase>(DATABASE);
            GameSettings settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Project Files/Data/Game Settings.asset");

            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            camera.fieldOfView = 50f;
            camera.farClipPlane = 500f;
            camera.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            camera.transform.position = new Vector3(0f, 0f, 2f) - camera.transform.forward * 30f;

            RenderTexture target = new RenderTexture(600, 860, 24);
            Texture2D texture = new Texture2D(600, 860, TextureFormat.RGB24, false);

            for (int w = 0; w < database.Worlds.Length; w++)
            {
                WorldData world = database.Worlds[w];
                for (int l = 0; l < world.Levels.Length; l++)
                {
                    for (int r = 0; r < world.Levels[l].Rooms.Length; r++)
                    {
                        GameObject root = BuildPreviewRoom(world, world.Levels[l].Rooms[r], settings);

                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        texture.Apply();
                        camera.targetTexture = null;
                        RenderTexture.active = null;

                        File.WriteAllBytes(Path.Combine(folder, $"w{w + 1}_l{l + 1:00}_r{r + 1}.png"), texture.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("[LevelGenerator] previews written to " + folder);
        }

        private static GameObject BuildPreviewRoom(WorldData world, RoomData room, GameSettings settings)
        {
            GameObject root = new GameObject("Preview Room");

            foreach (ItemEntityData item in room.ItemEntities)
            {
                LevelItem levelItem = world.Items.FirstOrDefault(i => i.Hash == item.Hash);
                if (levelItem == null) continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(levelItem.Prefab, root.transform);
                instance.transform.SetPositionAndRotation(item.Position, item.Rotation);
                instance.transform.localScale = item.Scale;
            }

            foreach (EnemyEntityData enemy in room.EnemyEntities)
            {
                EnemyData data = settings.EnemiesDatabase.GetEnemyData(enemy.EnemyType);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(data.Prefab, root.transform);
                instance.transform.SetPositionAndRotation(enemy.Position, enemy.Rotation);
                instance.transform.localScale = Vector3.one * (enemy.IsElite ? 1.3f : 1f);
            }

            foreach (ChestEntityData chest in room.ChestEntities)
            {
                ChestData data = settings.ChestData.FirstOrDefault(c => c.Type == chest.ChestType);
                if (data == null) continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(data.Prefab, root.transform);
                instance.transform.SetPositionAndRotation(chest.Position, chest.Rotation);
            }

            GameObject spawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = room.SpawnPoint + Vector3.up * 0.05f;
            spawn.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            spawn.GetComponent<Renderer>().sharedMaterial = new Material(UnityEngine.Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.2f, 1f, 0.3f) };

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer || renderer is SpriteRenderer) renderer.enabled = false;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(false);

            return root;
        }
        #endregion

        #region Types
        private class Palette
        {
            public readonly RoomEnvironmentPreset[] Presets;
            private readonly Dictionary<string, LevelItem> byName = new Dictionary<string, LevelItem>();
            private readonly Dictionary<int, LevelItem> byHash = new Dictionary<int, LevelItem>();
            private readonly Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

            public Palette(WorldData world)
            {
                Presets = world.RoomEnvPresets;
                foreach (LevelItem item in world.Items)
                {
                    byName[item.Prefab.name] = item;
                    byHash[item.Hash] = item;
                }
            }

            public int Hash(string name)
            {
                if (!byName.TryGetValue(name, out LevelItem item)) throw new Exception("[LevelGenerator] world has no item " + name);
                return item.Hash;
            }

            public bool IsObstacle(int hash) => byHash.TryGetValue(hash, out LevelItem item) && item.Type == LevelItemType.Obstacle;
            public string NameOf(int hash) => byHash.TryGetValue(hash, out LevelItem item) ? item.Prefab.name : hash.ToString();

            // offset of the visual center from the pivot (from the prefab bounds), so pieces sit centered on their cells
            public Vector3 Offset(string name)
            {
                if (offsets.TryGetValue(name, out Vector3 cached)) return cached;

                GameObject prefab = byName[name].Prefab;
                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;

                    Bounds local = TransformBounds(filter.sharedMesh.bounds, renderer.transform.localToWorldMatrix);
                    if (first) { bounds = local; first = false; }
                    else bounds.Encapsulate(local);
                }

                Vector3 offset = new Vector3(bounds.center.x, 0f, bounds.center.z);
                offsets[name] = offset;

                return offset;
            }

            public Vector3 WorldPosition(ItemPlan item)
            {
                Vector3 offset = Offset(item.Name);
                Vector2Int cells = Cells(item.Name, item.Rotation);
                Vector3 center = new Vector3(CellX(item.Column + (cells.x - 1) * 0.5f), 0f, CellZ(item.Row + (cells.y - 1) * 0.5f));

                return center - Quaternion.Euler(0f, item.Rotation, 0f) * offset;
            }

            public Vector3 SpawnOf(RoomEnvironmentPreset preset) => (Vector3)typeof(RoomEnvironmentPreset).GetField("spawnPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(preset);

            public ItemEntityData[] ItemsOf(RoomEnvironmentPreset preset) => (ItemEntityData[])typeof(RoomEnvironmentPreset).GetField("itemEntities", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(preset);

            private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
            {
                Bounds result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    result.Encapsulate(matrix.MultiplyPoint3x4(corner));
                }

                return result;
            }
        }

        private class RoomPlan
        {
            public readonly int Preset;
            public readonly GridMap Grid = new GridMap();
            public readonly List<ItemPlan> Items = new List<ItemPlan>();
            public readonly List<EnemyPlan> Enemies = new List<EnemyPlan>();
            public readonly List<(LevelChestType type, Vector3 position, Quaternion rotation)> Chests = new List<(LevelChestType, Vector3, Quaternion)>();

            public RoomPlan(int preset) { Preset = preset; }

            // places an item if its whole footprint is free; 1x2 pieces run along z at 0 degrees
            public bool Place(string name, int column, int row, int rotation)
            {
                Vector2Int size = Cells(name, rotation);

                for (int c = column; c < column + size.x; c++)
                    for (int r = row; r < row + size.y; r++)
                        if (!Grid.IsFree(c, r) || GridMap.IsReserved(c, r)) return false;

                for (int c = column; c < column + size.x; c++)
                    for (int r = row; r < row + size.y; r++)
                        Grid.Block(c, r);

                Items.Add(new ItemPlan(name, column, row, rotation));
                return true;
            }

            public string Signature()
            {
                return string.Join(";", Items.Select(i => i.Name + "@" + i.Column + "," + i.Row + "," + i.Rotation).OrderBy(s => s));
            }
        }

        // footprint in grid cells: "1x2" pieces run along z at 0 degrees
        private static Vector2Int Cells(string name, int rotation)
        {
            Vector2Int size = name.Contains("2x2") ? new Vector2Int(2, 2) : name.Contains("1x2") ? new Vector2Int(1, 2) : Vector2Int.one;
            return rotation % 180 == 0 ? size : new Vector2Int(size.y, size.x);
        }

        private class ItemPlan
        {
            public readonly string Name;
            public readonly int Column, Row, Rotation;
            public ItemPlan(string name, int column, int row, int rotation) { Name = name; Column = column; Row = row; Rotation = rotation; }
        }

        private class EnemyPlan
        {
            public readonly EnemyType Type;
            public readonly Vector3 Position;
            public readonly bool Elite;
            public EnemyPlan(EnemyType type, Vector3 position, bool elite) { Type = type; Position = position; Elite = elite; }
        }

        private class GridMap
        {
            private readonly bool[,] blocked = new bool[COLS, ROWS];
            private bool[,] reachable;

            // keep the area in front of the spawn and the walk to the exit door open
            public static bool IsReserved(int c, int r) => (r <= 1 && c >= 2 && c <= 5) || (r >= 11 && c >= 3 && c <= 4);

            public bool Inside(int c, int r) => c >= 0 && c < COLS && r >= 0 && r < ROWS;
            public bool IsFree(int c, int r) => Inside(c, r) && !blocked[c, r];
            public bool IsBlocked(int c, int r) => Inside(c, r) && blocked[c, r];
            public void Block(int c, int r) { if (Inside(c, r)) blocked[c, r] = true; reachable = null; }

            public bool IsReachable(int c, int r)
            {
                if (reachable == null) Flood();
                return Inside(c, r) && reachable[c, r];
            }

            // every free cell must be reachable from the spawn, and the exit cells too
            public bool IsConnected()
            {
                Flood();
                for (int c = 0; c < COLS; c++)
                    for (int r = 0; r < ROWS; r++)
                        if (!blocked[c, r] && !reachable[c, r]) return false;

                return reachable[3, ROWS - 1] || reachable[4, ROWS - 1];
            }

            private void Flood()
            {
                reachable = new bool[COLS, ROWS];
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(3, 0));
                reachable[3, 0] = true;

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        Vector2Int next = cell + step;
                        if (!Inside(next.x, next.y) || blocked[next.x, next.y] || reachable[next.x, next.y]) continue;

                        reachable[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
        }
        #endregion
    }
}
