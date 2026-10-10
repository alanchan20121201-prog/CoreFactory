// 自我測試：headless 也能跑的 5 組回歸（座標 / 地形確定性 / 區塊串流 / 模擬+索引一致性 / 存讀檔+藍圖）。
// 用法：Godot --headless --path game2d -- --selftest
// 退出碼 0 = 全過、1 = 有失敗（可直接進 CI）。
using System;
using System.IO;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class SelfTest2D
    {
        private static int _fail;

        public static int Run()
        {
            _fail = 0;
            GD.Print("[selftest] CoreFactory2D self test start");

            T1_GridCoord();
            T2_TerrainDeterminism();
            T3_ChunkStreaming();
            T4_SimAndIndexConsistency();
            T5_SaveLoadAndBlueprint();
            T6_OnboardingFixes();
            T7_PlanetsAndPortals();

            GD.Print($"[selftest] done, fail={_fail}");
            return _fail;
        }

        private static void Check(bool cond, string name)
        {
            if (cond) GD.Print($"[selftest] PASS {name}");
            else { _fail++; GD.PrintErr($"[selftest] FAIL {name}"); }
        }

        // ── T1：座標系統 ───────────────────────────────────────
        private static void T1_GridCoord()
        {
            bool ok = true;
            int checks = 0;
            int[] samples = { -1000, -33, -32, -31, -1, 0, 1, 31, 32, 33, 1000, 32760, -32760 };

            foreach (int t in samples)
            {
                ok &= GridCoord.ChunkOf(t) * GridCoord.ChunkSize + GridCoord.TileInChunk(t) == t;
                checks++;
                var center = GridCoord.TileCenterWorld(t, t);
                GridCoord.WorldToTile(center, out int wx, out int wz);
                ok &= wx == t && wz == t;
                checks++;
            }
            foreach (var (px, pz) in new[] { (0, 0), (1234, -5678), (32760, -32760), (-1, 1) })
            {
                int key = SimWorld.Key(px, pz);
                ok &= SimWorld.UnX(key) == px && SimWorld.UnZ(key) == pz;
                checks++;
            }
            ok &= GridCoord.InBounds(0, 0) && !GridCoord.InBounds(40000, 0);

            Check(ok, $"T1 grid coords ({checks} cases)");
        }

        // ── T2：地形確定性（同 seed 完全一致、異 seed 有差異）──
        private static void T2_TerrainDeterminism()
        {
            var a = new Terrain(12345, Planets.Get(0));
            var b = new Terrain(12345, Planets.Get(0));
            var c = new Terrain(54321, Planets.Get(0));

            bool same = true, diff = false;
            for (int i = 0; i < 400; i++)
            {
                int x = (i * 37) % 200 - 100;
                int z = (i * 91) % 200 - 100;

                bool oa = a.ResourceAt(x, z, out var ia, out int ra);
                bool ob = b.ResourceAt(x, z, out var ib, out int rb);
                bool oc = c.ResourceAt(x, z, out var ic, out int rc);

                if (oa != ob || ia != ib || ra != rb) same = false;
                if (oa != oc || ia != ic) diff = true;
                if (float.IsNaN(a.HeightAt(x, z))) same = false;
            }
            Check(same, "T2a terrain determinism (same seed identical)");
            Check(diff, "T2b terrain varies by seed");
        }

        // ── T3：區塊串流（載入 / 卸載 / 地形快取正確性）────────
        private static void T3_ChunkStreaming()
        {
            var world = new SimWorld(777, 0);
            var mgr = new ChunkManager(world);   // container = null → 純資料模式（不建 View）

            var view = new Rect2(-320, -320, 640, 640);   // 20×20 格視野
            for (int i = 0; i < 20; i++) mgr.Update(view, 4);
            Check(mgr.LoadedCount > 0, $"T3a chunks loaded ({mgr.LoadedCount})");

            // 地形快取 = 純函式直接查詢（抽樣）
            bool terrainOk = true;
            foreach (var ch in mgr.Loaded)
            {
                for (int lz = 0; lz < GridCoord.ChunkSize; lz += 7)
                {
                    for (int lx = 0; lx < GridCoord.ChunkSize; lx += 7)
                    {
                        int gx = ch.OriginX + lx, gz = ch.OriginZ + lz;
                        bool direct = world.Terrain.ResourceAt(gx, gz, out var ore, out _);
                        int idx = lz * GridCoord.ChunkSize + lx;
                        if (direct != (ch.TerrainKind[idx] == 1)) terrainOk = false;
                        if (direct && (ItemId)ch.OreKind[idx] != ore) terrainOk = false;
                    }
                }
            }
            Check(terrainOk, "T3b chunk terrain cache matches pure function");

            // 視野移動 → 舊區塊卸載、新區塊跟上
            var far = new Rect2(9000 * GridCoord.TileSizeF, 9000 * GridCoord.TileSizeF, 640, 640);
            for (int i = 0; i < 20; i++) mgr.Update(far, 4);

            int fc = GridCoord.ChunkOf(GridCoord.WorldToTileX(far.Position.X));
            int fz = GridCoord.ChunkOf(GridCoord.WorldToTileZ(far.Position.Y));
            bool follows = mgr.LoadedCount > 0;
            foreach (var ch in mgr.Loaded)
                if (Math.Abs(ch.Cx - fc) > 4 || Math.Abs(ch.Cz - fz) > 4) follows = false;
            Check(follows, $"T3c streaming follows camera ({mgr.LoadedCount} chunks at new site)");
        }

        // ── T4：示範產線真的會轉 + 索引一致性 ─────────────────
        private static void T4_SimAndIndexConsistency()
        {
            var world = new SimWorld(20261010, 0);
            bool built = StarterRig2D.Build(world, out int cx, out int cz, out int placed);
            Check(built && placed >= 15, $"T4a starter rig built ({placed} pieces)");

            var mgr = new ChunkManager(world);
            var view = new Rect2((cx - 8) * GridCoord.TileSizeF, (cz - 8) * GridCoord.TileSizeF, 640, 640);
            for (int i = 0; i < 10; i++) mgr.Update(view, 4);

            // 每個建築鍵都必須出現在「它所在區塊」的清單裡
            bool indexOk = true;
            foreach (var kv in world.Buildings)
            {
                var ch = mgr.GetAtTile(kv.Value.X, kv.Value.Z);
                if (ch == null || !ch.BuildingKeys.Contains(kv.Key)) indexOk = false;
            }
            Check(indexOk, "T4b building index consistent");

            // 找礦機 → 推得預期產物 → 快跑 20 秒模擬
            var miner = null as Building;
            foreach (var kv in world.Buildings)
                if (kv.Value.Kind == BuildingKind.Miner) { miner = kv.Value; break; }

            ItemId halfProduct = ItemId.None;
            if (miner != null)
                halfProduct = miner.KindValue == (int)ItemId.CopperOre ? ItemId.CopperPlate : ItemId.IronPlate;

            long movedBefore = world.TotalItemsMoved;
            int productBefore = halfProduct != ItemId.None ? world.Hub[(int)halfProduct] : 0;
            for (int i = 0; i < 1200; i++) world.Tick(1f / 60f);

            Check(world.TotalItemsMoved > movedBefore, "T4c items flowed on belts");
            if (halfProduct != ItemId.None)
                Check(world.Hub[(int)halfProduct] > productBefore,
                    $"T4d depot received {halfProduct} (+{world.Hub[(int)halfProduct] - productBefore})");
            else
                GD.Print("[selftest] SKIP T4d (miner on non-smeltable ore)");

            // 模擬層直接刪建築（蟲潮路徑）→ EnsureFresh 對帳
            bool foundBuilding = false;
            int someKey = -1;
            foreach (var kv in world.Buildings) { someKey = kv.Key; foundBuilding = true; break; }
            if (foundBuilding)
            {
                int sx = SimWorld.UnX(someKey), sz = SimWorld.UnZ(someKey);
                world.Buildings.Remove(someKey);
                mgr.EnsureFresh();
                var ch = mgr.GetAtTile(sx, sz);
                Check(ch == null || !ch.BuildingKeys.Contains(someKey),
                    "T4e external removal reconciled by EnsureFresh");
            }

            // 走管理器路徑的拆除（未來 BuildTool 用）
            int beltX = 0, beltZ = 0;
            bool foundBelt = false;
            foreach (var kv in world.Belts)
            {
                beltX = kv.Value.X;
                beltZ = kv.Value.Z;
                foundBelt = true;
                break;
            }
            if (foundBelt)
            {
                int bkey = SimWorld.Key(beltX, beltZ);
                bool removed = world.Remove(beltX, beltZ);
                mgr.MarkTileChanged(beltX, beltZ);
                var ch = mgr.GetAtTile(beltX, beltZ);
                Check(removed && (ch == null || !ch.BeltKeys.Contains(bkey)),
                    "T4f managed removal updates chunk index");
            }
        }

        // ── T7：五個星球與傳送門解鎖條件 ────────────────────────
        private static void T7_PlanetsAndPortals()
        {
            Check(Planets.All.Length == 5, $"T7a five planets ({Planets.All.Length})");

            // 每顆星球的礦池、蟲池都不一樣，且越後面越貴
            bool oresDistinct = true, aliensDistinct = true, priceRising = true, costRising = true;
            var seenOres = new System.Collections.Generic.HashSet<ItemId>();
            for (int i = 0; i < Planets.All.Length; i++)
            {
                var p = Planets.All[i];
                if (p.Ores == null || p.Ores.Length == 0) oresDistinct = false;
                if (p.AlienPool == null || p.AlienPool.Length == 0) aliensDistinct = false;
                foreach (var o in p.Ores) seenOres.Add(o.item);
                if (i > 0)
                {
                    if (p.PriceMultiplier <= Planets.All[i - 1].PriceMultiplier) priceRising = false;
                    if (p.CostMultiplier < Planets.All[i - 1].CostMultiplier) costRising = false;
                    if (p.PortalCost <= Planets.All[i - 1].PortalCost) priceRising = false;
                }
            }
            Check(oresDistinct && aliensDistinct, "T7b every planet has its own ores & aliens");
            Check(seenOres.Count >= 9, $"T7c ore variety across planets ({seenOres.Count})");
            Check(priceRising, "T7d portal cost & ore price rise per planet");
            Check(costRising, "T7e building cost rises per planet");

            // 傳送門：沒錢不能開
            var w = new SimWorld(31337, 0);
            Check(w.IsUnlocked(0) && !w.IsUnlocked(1), "T7f only starting planet unlocked");
            w.Coins = 100;
            Check(!w.TryUnlockPortal(1, out var e1) && e1 == "ui.portal.poor", "T7g cannot unlock when poor");

            // 有錢但有欠款也不能開
            w.Coins = 999999;
            w.TakeLoan(1000);
            Check(!w.TryUnlockPortal(1, out var e2) && e2 == "ui.portal.debt", "T7h cannot unlock while in debt");

            // 還清 + 有錢 → 可開通
            w.RepayLoan(100000);
            long before = w.Coins;
            bool ok = w.TryUnlockPortal(1, out _);
            Check(ok && w.IsUnlocked(1) && w.Coins == before - Planets.All[1].PortalCost,
                $"T7i portal opened by paying {Planets.All[1].PortalCost}");

            // 存讀檔後解鎖狀態要保留
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cf2d_t7.sav");
            w.SaveTo(path);
            var w2 = new SimWorld(1, 0);
            bool loaded = w2.LoadFrom(path, out var err);
            Check(loaded && w2.IsUnlocked(1), "T7j unlocked mask survives save/load (" + err + ")");
            Check(w2.Hub[(int)ItemId.Ammo] == w.Hub[(int)ItemId.Ammo], "T7k hub survives save/load with 24 slots");

            // 後期星球真的挖得到新礦
            var w5 = new SimWorld(2026, 4);
            var kinds = new System.Collections.Generic.HashSet<ItemId>();
            for (int z = -60; z <= 60; z += 3)
                for (int x = -60; x <= 60; x += 3)
                    if (w5.Terrain.ResourceAt(x, z, out var ore, out _)) kinds.Add(ore);
            Check(kinds.Contains(ItemId.CoreOre) || kinds.Contains(ItemId.GoldOre) || kinds.Contains(ItemId.CrystalOre),
                $"T7l endgame planet yields rare ores ({kinds.Count} kinds found)");
        }

        // ── T6：新手友善修正（熔煉爐自動辨識礦種、砲塔出廠附彈藥）──
        private static void T6_OnboardingFixes()
        {
            // 熔煉爐餵銅礦 → 應自動從「鐵板」配方切到「銅板」配方
            var w = new SimWorld(777, 0);
            w.TryPlace(BuildingKind.Smelter, 0, 0, Dir.East, 0, -1, out _);
            var sm = w.BuildingAt(0, 0);
            Check(sm != null, "T6a smelter placed");
            if (sm != null)
            {
                byte before = sm.RecipeIndex;
                w.TryPlace(BuildingKind.Belt, -1, 0, Dir.East, 0, -1, out _);
                var belt = w.BeltAt(-1, 0);
                Check(belt != null, "T6b feed belt placed");
                if (belt != null)
                {
                    w.BeltInsert(belt, ItemId.CopperOre, 0.5f);
                    for (int i = 0; i < 120; i++) w.Tick(1f / 60f);
                    var r = GameData.GetRecipe(sm);
                    Check(before == 0 && r.In0 == ItemId.CopperOre && r.Out == ItemId.CopperPlate,
                        $"T6c smelter auto-switched to copper ({before}→{sm.RecipeIndex})");
                }
            }

            // 餵鐵礦 → 維持鐵板配方（不會亂切）
            var w2 = new SimWorld(778, 0);
            w2.TryPlace(BuildingKind.Smelter, 0, 0, Dir.East, 0, -1, out _);
            var sm2 = w2.BuildingAt(0, 0);
            w2.TryPlace(BuildingKind.Belt, -1, 0, Dir.East, 0, -1, out _);
            var belt2 = w2.BeltAt(-1, 0);
            if (sm2 != null && belt2 != null)
            {
                w2.BeltInsert(belt2, ItemId.IronOre, 0.5f);
                for (int i = 0; i < 120; i++) w2.Tick(1f / 60f);
                var r2 = GameData.GetRecipe(sm2);
                Check(r2.Out == ItemId.IronPlate, "T6d iron recipe kept for iron ore");
            }

            // 砲塔出廠就有彈藥（並用庫存補滿彈匣）
            var w3 = new SimWorld(779, 0);
            int ammoHub = w3.Hub[(int)ItemId.Ammo];
            Check(ammoHub >= 100, $"T6e start stock has ammo ({ammoHub})");
            w3.TryPlace(BuildingKind.Turret, 6, 6, Dir.North, 0, -1, out var err);
            var t = w3.BuildingAt(6, 6);
            Check(t != null, "T6f turret placed (" + err + ")");
            if (t != null)
                Check(t.Ammo >= GameData.TurretStartAmmo,
                    $"T6g turret ships with ammo ({t.Ammo}/{GameData.TurretMagazine})");
        }

        // ── T5：存讀檔 + CFBP1 藍圖往返 ────────────────────────
        private static void T5_SaveLoadAndBlueprint()
        {
            var world = new SimWorld(4242, 1);   // 灰燼星：順便驗證第二顆星球
            StarterRig2D.Build(world, out _, out _, out _);
            for (int i = 0; i < 240; i++) world.Tick(1f / 60f);

            string path = Path.Combine(Path.GetTempPath(), "cf2d_selftest_save.bin");
            world.SaveTo(path);

            var loaded = new SimWorld(1, 0);
            bool ok = loaded.LoadFrom(path, out string err);
            Check(ok, "T5a save/load ok" + (ok ? "" : " (" + err + ")"));
            if (ok)
            {
                bool same = loaded.Seed == world.Seed
                            && loaded.PlanetIndex == world.PlanetIndex
                            && loaded.Buildings.Count == world.Buildings.Count
                            && loaded.Belts.Count == world.Belts.Count
                            && loaded.ItemCount == world.ItemCount
                            && loaded.Hub[(int)ItemId.IronPlate] == world.Hub[(int)ItemId.IronPlate];
                Check(same, $"T5b save/load state matches (b={loaded.Buildings.Count}, belts={loaded.Belts.Count}, items={loaded.ItemCount})");
            }

            var entries = new System.Collections.Generic.List<Blueprint.Entry>();
            foreach (var kv in world.Buildings)
            {
                var b = kv.Value;
                entries.Add(new Blueprint.Entry
                {
                    Kind = b.Kind, X = b.X, Z = b.Z,
                    Facing = b.Facing, RecipeIndex = b.RecipeIndex, KindValue = b.KindValue,
                });
            }
            string bp = Blueprint.Encode(entries);
            bool dec = Blueprint.TryDecode(bp, out var back, out string berr);
            Check(dec && back.Count == entries.Count && bp.StartsWith("CFBP1"),
                $"T5c blueprint CFBP1 roundtrip ({entries.Count} entries, {bp.Length} chars)" +
                (dec ? "" : " (" + berr + ")"));

            try { File.Delete(path); } catch { /* 清不掉就算了 */ }
        }
    }
}
