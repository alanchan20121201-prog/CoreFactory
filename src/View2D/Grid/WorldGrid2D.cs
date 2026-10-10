// 網格查詢門面：把「模擬層 + 地形純函式 + 區塊」三邊資料併成一個查詢視角。
// 除錯 HUD、未來的建造工具與右側資訊欄都從這裡問「這一格有什麼」。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public struct TileInfo
    {
        public bool Valid;
        public ItemId Ore;
        public int Richness;
        public BuildingKind Building;
        public Dir Facing;
        public bool BuildingEnabled;
        public bool HasBelt;
        public int BeltItems;
        public Dir BeltFacing;
        public bool Occupied;
    }

    public sealed class WorldGrid2D
    {
        public readonly SimWorld World;
        public readonly ChunkManager Chunks;
        public readonly Terrain Terrain;

        public WorldGrid2D(SimWorld world, ChunkManager chunks)
        {
            World = world;
            Chunks = chunks;
            Terrain = world.Terrain;
        }

        public TileInfo GetTileInfo(int x, int z)
        {
            var info = new TileInfo { Valid = GridCoord.InBounds(x, z) };
            if (!info.Valid) return info;

            if (Terrain.ResourceAt(x, z, out var ore, out int rich))
            {
                info.Ore = ore;
                info.Richness = rich;
            }
            var b = World.BuildingAt(x, z);
            if (b != null)
            {
                info.Building = b.Kind;
                info.Facing = b.Facing;
                info.BuildingEnabled = b.Enabled;
            }
            var t = World.BeltAt(x, z);
            if (t != null)
            {
                info.HasBelt = true;
                info.BeltItems = t.Count;
                info.BeltFacing = t.Facing;
            }
            info.Occupied = b != null || t != null;
            return info;
        }

        /// <summary>除錯 HUD / 資訊列用的單行描述（純 ASCII，避開 CJK 字型問題；正式 UI 在 Step 3 接 Loc）。</summary>
        public string DescribeTile(int x, int z)
        {
            if (!GridCoord.InBounds(x, z)) return "out of bounds";

            var b = World.BuildingAt(x, z);
            if (b != null)
            {
                string s = $"{b.Kind}({b.Facing.ToString()[0]})";
                if (!b.Enabled) s += " OFF";
                var r = GameData.GetRecipe(b);
                if (b.IsCrafter && b.Producing && r.Seconds > 0f)
                    s += $" {b.Progress / r.Seconds * 100f:F0}%";
                else if (b.Kind == BuildingKind.Miner)
                    s += $" {b.Progress / Mathf.Max(0.01f, GameData.MinerSeconds) * 100f:F0}%";
                if (b.IsLogic) s += $" ch{b.ChannelIn}->{b.ChannelOut} v={b.SignalOut} t={b.Threshold}";
                if (b.Blocked) s += " BLOCKED";
                return s;
            }

            var t = World.BeltAt(x, z);
            if (t != null) return $"Belt({t.Facing.ToString()[0]}) items={t.Count}";

            if (Terrain.ResourceAt(x, z, out var ore, out int rich)) return $"Ore {ore} r={rich}";

            float h = Terrain.HeightAt(x, z);
            return $"Empty h={h:F1}";
        }
    }
}
