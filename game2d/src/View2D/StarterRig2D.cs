// 示範產線（2D 簡化版，比照 3D 版 BuildStarterRig 的意圖）：
//   礦機 → 傳送帶 ×5 → 熔煉爐 → 傳送帶 ×4 → 倉庫
//   外掛：太陽能板 ×3（180 發電 > 130 需求 → 效率 100%）、蓄電池、砲塔
// 擺放位置優先找「鐵礦或銅礦」——只有這兩種礦能進熔煉爐，示範線才轉得起來。
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class StarterRig2D
    {
        public static bool Build(SimWorld w, out int cx, out int cz, out int placed)
        {
            cx = 0;
            cz = 0;
            placed = 0;

            if (!FindOreSpot(w, true, out int x, out int z) && !FindOreSpot(w, false, out x, out z))
                return false;

            // 主線（往東）
            if (w.TryPlace(BuildingKind.Miner, x, z, Dir.East, out _)) placed++;
            for (int i = 1; i <= 5; i++)
                if (w.TryPlace(BuildingKind.Belt, x + i, z, Dir.East, out _)) placed++;

            var miner = w.BuildingAt(x, z);
            byte recipe = miner != null && miner.KindValue == (int)ItemId.CopperOre ? (byte)1 : (byte)0;
            if (w.TryPlace(BuildingKind.Smelter, x + 6, z, Dir.East, recipe, -1, out _)) placed++;
            for (int i = 7; i <= 10; i++)
                if (w.TryPlace(BuildingKind.Belt, x + i, z, Dir.East, out _)) placed++;
            if (w.TryPlace(BuildingKind.Depot, x + 11, z, Dir.East, out _)) placed++;

            // 電力與防禦（南側一排）
            if (w.TryPlace(BuildingKind.SolarPanel, x + 2, z + 2, Dir.South, out _)) placed++;
            if (w.TryPlace(BuildingKind.SolarPanel, x + 4, z + 2, Dir.South, out _)) placed++;
            if (w.TryPlace(BuildingKind.SolarPanel, x + 6, z + 2, Dir.South, out _)) placed++;
            if (w.TryPlace(BuildingKind.Battery, x + 8, z + 2, Dir.South, out _)) placed++;
            if (w.TryPlace(BuildingKind.Turret, x + 10, z + 2, Dir.South, out _)) placed++;

            cx = x + 6;
            cz = z + 1;
            return placed > 0;
        }

        /// <summary>找最近的鐵/銅礦（找不到再退而求其次任何礦）。無示範線開局時給相機對準用。</summary>
        public static bool FindOre(SimWorld w, out int x, out int z)
            => FindOreSpot(w, true, out x, out z) || FindOreSpot(w, false, out x, out z);

        /// <summary>從原點往外做環狀搜尋找礦脈（優先鐵/銅）。要留得下往東 12 格的主線，所以檢查邊界。</summary>
        private static bool FindOreSpot(SimWorld w, bool platesOnly, out int ox, out int oz)
        {
            ox = 0;
            oz = 0;
            for (int r = 4; r <= 260; r++)
            {
                for (int i = -r; i <= r; i++)
                {
                    if (Check(w, platesOnly, i, -r, out ox, out oz)) return true;
                    if (Check(w, platesOnly, i, r, out ox, out oz)) return true;
                    if (Check(w, platesOnly, -r, i, out ox, out oz)) return true;
                    if (Check(w, platesOnly, r, i, out ox, out oz)) return true;
                }
            }
            return false;
        }

        private static bool Check(SimWorld w, bool platesOnly, int x, int z, out int ox, out int oz)
        {
            ox = 0;
            oz = 0;
            if (x < -GridCoord.TileLimit || x + 12 > GridCoord.TileLimit) return false;
            if (z < -GridCoord.TileLimit + 2 || z > GridCoord.TileLimit - 2) return false;

            if (!w.Terrain.ResourceAt(x, z, out var ore, out _) || ore == ItemId.None) return false;
            if (platesOnly && ore != ItemId.IronOre && ore != ItemId.CopperOre) return false;

            ox = x;
            oz = z;
            return true;
        }
    }
}
