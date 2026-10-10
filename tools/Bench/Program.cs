// Core 層壓測與正確性測試（純 .NET，不需要 Godot）
//   1) 傳送帶位移是否正確
//   2) 生產鏈（採礦→傳送帶→熔煉→傳送帶→倉庫）是否真的產出
//   3) 存檔／讀檔往返是否一致
//   4) 大規模效能：5k / 10k / 20k 段傳送帶的每 tick 耗時
using System;
using System.Diagnostics;
using System.IO;
using CoreFactory.Core;

namespace CoreFactory.Bench
{
    internal static class Program
    {
        private static int Main()
        {
            Console.WriteLine("=== Core Factory · Core 模擬層壓測 ===");
            Console.WriteLine("(純 C#、無 Godot 依賴；壓測的就是遊戲跑的那份模擬代碼)\n");

            int fails = 0;
            if (!TestBeltAdvance()) fails++;
            if (!TestProductionChain()) fails++;
            if (!TestLogicCutoff()) fails++;
            if (!TestSaveLoad()) fails++;
            if (!TestBlueprint()) fails++;

            Console.WriteLine();
            Benchmark("小   5,000 段 / 40,000 物品", 50, 100);
            Benchmark("中  10,000 段 / 80,000 物品", 100, 100);
            Benchmark("大  20,000 段 / 160,000 物品", 200, 100);

            Console.WriteLine();
            Console.WriteLine(fails == 0 ? "全部測試通過 ✔" : $"有 {fails} 項測試失敗 ✘");
            return fails;
        }

        private static SimWorld NewWorld()
        {
            var w = new SimWorld(20261010, 0);
            w.Hub[(int)ItemId.IronPlate] += 1_000_000;
            w.Hub[(int)ItemId.CopperPlate] += 1_000_000;
            w.Hub[(int)ItemId.Gear] += 1_000_000;
            w.Hub[(int)ItemId.Circuit] += 1_000_000;
            w.Hub[(int)ItemId.Coal] += 1_000_000;
            return w;
        }

        private static void Run(SimWorld w, float seconds, int ups = 60)
        {
            int ticks = (int)(seconds * ups);
            float dt = 1f / ups;
            for (int i = 0; i < ticks; i++) w.Tick(dt);
        }

        // ── 1) 傳送帶位移 ───────────────────────────────────────
        private static bool TestBeltAdvance()
        {
            var w = NewWorld();
            w.TryPlace(BuildingKind.Belt, 0, 0, Dir.East, out _);
            w.TryPlace(BuildingKind.Belt, 1, 0, Dir.East, out _);
            var first = w.Belts[SimWorld.Key(0, 0)];
            w.BeltInsert(first, ItemId.IronOre, 0f);

            Run(w, 1f);   // 1 秒 → 基準速度 1.2 格

            bool ok = w.ItemCount == 1;
            float pos = -1f;
            int tile = 0;
            if (ok)
            {
                var second = w.Belts[SimWorld.Key(1, 0)];
                int head = second.Count > 0 ? w.BeltItemHead(second) : -1;
                if (head >= 0) { pos = w.ItemPosAt(head); tile = 2; }
                else
                {
                    head = w.BeltItemHead(first);
                    if (head >= 0) { pos = w.ItemPosAt(head); tile = 1; }
                }
            }
            bool pass = ok && tile == 2 && pos > 0.10f && pos < 0.35f;
            Console.WriteLine($"[1] 傳送帶位移        : 物品落在第 {tile} 段 pos={pos:0.000} (預期 1.2 → 第 2 段約 0.2) → {(pass ? "通過" : "失敗")}");
            return pass;
        }

        // ── 2) 生產鏈 ──────────────────────────────────────────
        private static bool TestProductionChain()
        {
            var w = NewWorld();
            int ox = 0, oz = 0; bool found = false;
            for (int r = 1; r < 60 && !found; r++)
                for (int dz = -r; dz <= r && !found; dz++)
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        if (!w.Terrain.ResourceAt(dx, dz, out var ore, out _)) continue;
                        if (ore != ItemId.IronOre) continue;
                        ox = dx; oz = dz; found = true;
                    }
            if (!found) { Console.WriteLine("[2] 生產鏈            : 找不到鐵礦 → 失敗"); return false; }

            w.TryPlace(BuildingKind.Miner, ox, oz, Dir.East, out _);
            for (int i = 1; i <= 4; i++) w.TryPlace(BuildingKind.Belt, ox + i, oz, Dir.East, out _);
            w.TryPlace(BuildingKind.Smelter, ox + 5, oz, Dir.East, out _);
            for (int i = 6; i <= 8; i++) w.TryPlace(BuildingKind.Belt, ox + i, oz, Dir.East, out _);
            w.TryPlace(BuildingKind.Depot, ox + 9, oz, Dir.East, out _);
            w.TryPlace(BuildingKind.ThermalGenerator, ox + 1, oz + 2, Dir.North, out _);
            w.TryPlace(BuildingKind.Battery, ox + 3, oz + 2, Dir.North, out _);

            int before = w.Hub[(int)ItemId.IronPlate];
            Run(w, 90f);
            int made = w.Hub[(int)ItemId.IronPlate] - before;

            bool pass = made > 10;
            Console.WriteLine($"[2] 生產鏈            : 90 秒產出鐵板 {made} 片 (電力 {w.PowerGenerated:0}/{w.PowerUsed:0}W, 效率 {w.Efficiency:0.00}) → {(pass ? "通過" : "失敗")}");
            return pass;
        }

        // ── 3) 邏輯電路：低電量自動停機 ─────────────────────────
        private static bool TestLogicCutoff()
        {
            var w = NewWorld();
            w.TryPlace(BuildingKind.Smelter, 0, 0, Dir.East, out _);
            w.TryPlace(BuildingKind.SensorNode, 2, 0, Dir.North, out _);
            w.TryPlace(BuildingKind.ComparatorNode, 3, 0, Dir.North, out _);
            w.TryPlace(BuildingKind.ActuatorNode, 4, 0, Dir.North, out _);

            // 電池沒電（0%）→ 比較器 (0 > 30) = false → 執行器應把熔煉爐停機
            w.Tick(1f / 60f);
            bool cut = !w.Buildings[SimWorld.Key(0, 0)].Enabled;

            // 手動灌電 → 30% 以上 → 應該恢復運轉
            w.BatteryStored = GameData.BatteryCapacity * 0.8f;
            w.Tick(1f / 60f);
            bool restored = w.Buildings[SimWorld.Key(0, 0)].Enabled;

            bool pass = cut && restored;
            Console.WriteLine($"[3] 邏輯電路          : 低電量停機={cut}, 復電後恢復={restored} → {(pass ? "通過" : "失敗")}");
            return pass;
        }

        // ── 4) 存檔往返 ────────────────────────────────────────
        private static bool TestSaveLoad()
        {
            var w = NewWorld();
            w.TryPlace(BuildingKind.Belt, 0, 0, Dir.East, out _);
            w.TryPlace(BuildingKind.Belt, 1, 0, Dir.East, out _);
            w.TryPlace(BuildingKind.Smelter, 2, 0, Dir.East, out _);
            var b0 = w.Belts[SimWorld.Key(0, 0)];
            for (int j = 3; j >= 0; j--) w.BeltInsert(b0, ItemId.IronOre, j * GameData.ItemSize);
            Run(w, 2f);

            string path = Path.Combine(Path.GetTempPath(), "corefactory_bench_save.cf");
            w.SaveTo(path);
            long size = new FileInfo(path).Length;

            var w2 = new SimWorld(1, 1);
            bool loaded = w2.LoadFrom(path, out string err);

            bool same = loaded
                     && w2.BuildingCount == w.BuildingCount
                     && w2.BeltCount == w.BeltCount
                     && w2.ItemCount == w.ItemCount
                     && w2.Hub[(int)ItemId.IronPlate] == w.Hub[(int)ItemId.IronPlate];

            Console.WriteLine($"[4] 存檔往返          : {(loaded ? "讀取成功" : "讀取失敗 " + err)}, 大小 {size} bytes, 建築 {w2.BuildingCount}/{w.BuildingCount}, 帶上物品 {w2.ItemCount}/{w.ItemCount} → {(same ? "通過" : "失敗")}");
            return same;
        }

        // ── 5) 藍圖 ────────────────────────────────────────────
        private static bool TestBlueprint()
        {
            var list = new System.Collections.Generic.List<Blueprint.Entry>
            {
                new Blueprint.Entry { Kind = BuildingKind.Belt, X = 0, Z = 0, Facing = Dir.East },
                new Blueprint.Entry { Kind = BuildingKind.Smelter, X = 3, Z = 0, Facing = Dir.East, RecipeIndex = 1 },
                new Blueprint.Entry { Kind = BuildingKind.ActuatorNode, X = 3, Z = 2, Facing = Dir.North, KindValue = 3 },
            };
            string s = Blueprint.Encode(list);
            bool ok = Blueprint.TryDecode(s, out var back, out string err) && back.Count == 3
                      && back[1].Kind == BuildingKind.Smelter && back[1].RecipeIndex == 1
                      && back[2].KindValue == 3;
            Console.WriteLine($"[5] 藍圖字串          : {s.Length} 字元, 解回 {back.Count} 筆 → {(ok ? "通過" : "失敗 " + err)}");
            return ok;
        }

        // ── 效能 ───────────────────────────────────────────────
        private static void Benchmark(string label, int lanes, int laneLength)
        {
            var w = NewWorld();
            int placed = 0, depots = 0;
            for (int l = 0; l < lanes; l++)
            {
                int z = l * 2;
                for (int x = 0; x < laneLength; x++)
                {
                    if (!w.TryPlace(BuildingKind.Belt, x, z, Dir.East, out _)) break;
                    placed++;
                }
                if (w.TryPlace(BuildingKind.Depot, laneLength, z, Dir.East, out _)) depots++;
            }

            int items = 0;
            foreach (var kv in w.Belts)
            {
                var t = kv.Value;
                for (int j = 7; j >= 0; j--) if (w.BeltInsert(t, ItemId.IronOre, j * GameData.ItemSize)) items++;
            }

            for (int i = 0; i < 60; i++) w.Tick(1f / 60f);          // 暖機
            long movedBefore = w.TotalItemsMoved;

            const int ticks = 600;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < ticks; i++) w.Tick(1f / 60f);
            sw.Stop();

            double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
            long moved = w.TotalItemsMoved - movedBefore;
            double mem = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            double budget = msPerTick / 16.67 * 100.0;

            Console.WriteLine($"{label} | 段 {placed} / 倉 {depots} / 物品 {items}");
            Console.WriteLine($"    每 tick {msPerTick:0.000} ms  = 60 UPS 的 {budget:0.0}% 幀預算（上限 5 ms 模擬預算）");
            Console.WriteLine($"    tick 內位移 {moved / (double)ticks:0} 個物品 / tick，託管記憶體 {mem:0.0} MB\n");
        }
    }
}
