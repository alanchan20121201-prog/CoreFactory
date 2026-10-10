// 區塊串流管理器：依相機視野建立/卸載區塊、維護實體索引、對帳模擬層的變動。
// headless 模式傳 container = null：只維護資料（自我測試用），不建任何節點。
//
// 索引一致性策略（三層保險）：
//   1) 走管理器 API 的放置/拆除 → MarkTileChanged 立即更新該區塊
//   2) 蟲潮會在模擬層「直接」刪建築 → 每個 tick 用 Count 對帳，發現不一致就全量重建（成本 O(N)，極少發生）
//   3) 讀檔/換星球整批換內容 → ResetAll 全清，讓區塊用新 seed 重新生成
using System;
using System.Collections.Generic;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed class ChunkManager
    {
        public const int DefaultLoadBudget = 3;   // 每幀最多完整載入幾個區塊（地形計算不爆幀）
        public const int KeepMargin = 1;          // 視野外再多留一圈（載入範圍與卸載都參考它）

        private readonly SimWorld _world;
        private readonly Node2D _container;       // null = headless（不建 View）
        private readonly Dictionary<int, Chunk> _chunks = new();
        private readonly List<Chunk> _loadedCache = new();
        private readonly List<Chunk> _scratchRemove = new();
        private readonly List<(int cx, int cz)> _needCreate = new();

        private int _lastBuildingCount = -1, _lastBeltCount = -1;

        public long TerrainTilesBuilt;            // 累計生成的地形格數（效能觀察用）

        public int LoadedCount => _chunks.Count;
        public List<Chunk> Loaded => _loadedCache;

        public ChunkManager(SimWorld world, Node2D container = null)
        {
            _world = world;
            _container = container;
            RebuildAllIndices();
        }

        public Chunk Get(int cx, int cz) => _chunks.TryGetValue(GridCoord.ChunkKey(cx, cz), out var c) ? c : null;
        public Chunk GetAtTile(int x, int z) => Get(GridCoord.ChunkOf(x), GridCoord.ChunkOf(z));

        // ── 串流 ───────────────────────────────────────────────
        /// <summary>依相機世界視野更新：建立缺少的（每幀預算內、由中心向外）、卸載遠離的。</summary>
        public void Update(Rect2 worldView, int budget = DefaultLoadBudget)
        {
            int cx0 = GridCoord.ChunkOf(GridCoord.WorldToTileX(worldView.Position.X)) - KeepMargin;
            int cz0 = GridCoord.ChunkOf(GridCoord.WorldToTileZ(worldView.Position.Y)) - KeepMargin;
            int cx1 = GridCoord.ChunkOf(GridCoord.WorldToTileX(worldView.End.X)) + KeepMargin;
            int cz1 = GridCoord.ChunkOf(GridCoord.WorldToTileZ(worldView.End.Y)) + KeepMargin;

            // 1) 缺哪些區塊（由視野中心向外排優先序，讓玩家附近先出現）
            _needCreate.Clear();
            float ccx = (cx0 + cx1) * 0.5f, ccz = (cz0 + cz1) * 0.5f;
            for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                    if (!_chunks.ContainsKey(GridCoord.ChunkKey(cx, cz)))
                        _needCreate.Add((cx, cz));
            if (_needCreate.Count > 0)
            {
                _needCreate.Sort((a, b) =>
                {
                    float da = (a.cx - ccx) * (a.cx - ccx) + (a.cz - ccz) * (a.cz - ccz);
                    float db = (b.cx - ccx) * (b.cx - ccx) + (b.cz - ccz) * (b.cz - ccz);
                    return da.CompareTo(db);
                });
                int n = Mathf.Min(budget, _needCreate.Count);
                for (int i = 0; i < n; i++) Create(_needCreate[i].cx, _needCreate[i].cz);
            }

            // 2) 卸載（比載入範圍再多留一圈 → 遲滯，避免在邊界來回抖動）
            _scratchRemove.Clear();
            foreach (var kv in _chunks)
            {
                var c = kv.Value;
                if (c.Cx < cx0 - KeepMargin || c.Cx > cx1 + KeepMargin ||
                    c.Cz < cz0 - KeepMargin || c.Cz > cz1 + KeepMargin)
                    _scratchRemove.Add(c);
            }
            for (int i = 0; i < _scratchRemove.Count; i++) Destroy(_scratchRemove[i]);
        }

        private Chunk Create(int cx, int cz)
        {
            var chunk = new Chunk(cx, cz);
            chunk.FillTerrain(_world.Terrain);
            TerrainTilesBuilt += GridCoord.ChunkSize * GridCoord.ChunkSize;
            FillIndex(chunk);
            _chunks[chunk.Key] = chunk;
            _loadedCache.Add(chunk);

            if (_container != null)
            {
                var view = new ChunkView2D(chunk, _world)
                {
                    Name = $"Chunk_{cx}_{cz}",
                    Position = GridCoord.TileOriginWorld(chunk.OriginX, chunk.OriginZ),
                };
                _container.AddChild(view);
                chunk.View = view;
            }
            return chunk;
        }

        private void Destroy(Chunk c)
        {
            if (c.View != null)
            {
                c.View.QueueFree();
                c.View = null;
            }
            _chunks.Remove(c.Key);
            _loadedCache.Remove(c);
        }

        // ── 索引維護 ───────────────────────────────────────────
        /// <summary>重掃單一區塊（1024 格 ×2 次字典查詢）——放置/拆除後呼叫，永遠重掃所以加減都正確。</summary>
        private void FillIndex(Chunk chunk)
        {
            chunk.BuildingKeys.Clear();
            chunk.BeltKeys.Clear();
            const int N = GridCoord.ChunkSize;
            for (int lz = 0; lz < N; lz++)
            {
                int wz = chunk.OriginZ + lz;
                for (int lx = 0; lx < N; lx++)
                {
                    int key = SimWorld.Key(chunk.OriginX + lx, wz);
                    if (_world.Buildings.ContainsKey(key)) chunk.BuildingKeys.Add(key);
                    else if (_world.Belts.ContainsKey(key)) chunk.BeltKeys.Add(key);
                }
            }
            chunk.IndexValid = true;
        }

        /// <summary>放置/拆除後通知（x, z 是格座標）。未載入的區塊不用管：載入時會自然重掃。</summary>
        public void MarkTileChanged(int x, int z)
        {
            var chunk = GetAtTile(x, z);
            if (chunk == null) return;
            FillIndex(chunk);
            if (chunk.View is ChunkView2D v)
            {
                v.QueueEntityRedraw();
                v.QueueTerrainRedraw();   // 礦脈格被建築蓋掉時，地形層也要更新
            }
        }

        /// <summary>每個 tick 呼叫：用數量對帳偵測「模擬層直接刪東西」（蟲潮破建築）。</summary>
        public void EnsureFresh()
        {
            if (_world.Buildings.Count != _lastBuildingCount || _world.Belts.Count != _lastBeltCount)
                RebuildAllIndices();
        }

        /// <summary>全量重建所有已載入區塊的索引（一輪掃過 Buildings + Belts，O(N)）。</summary>
        public void RebuildAllIndices()
        {
            for (int i = 0; i < _loadedCache.Count; i++)
            {
                _loadedCache[i].BuildingKeys.Clear();
                _loadedCache[i].BeltKeys.Clear();
            }
            foreach (var kv in _world.Buildings)
            {
                var b = kv.Value;
                GetAtTile(b.X, b.Z)?.BuildingKeys.Add(kv.Key);
            }
            foreach (var kv in _world.Belts)
            {
                var t = kv.Value;
                GetAtTile(t.X, t.Z)?.BeltKeys.Add(kv.Key);
            }
            _lastBuildingCount = _world.Buildings.Count;
            _lastBeltCount = _world.Belts.Count;
        }

        /// <summary>讀檔/換星球後：整批清掉，讓區塊用新的 seed 重新生成。</summary>
        public void ResetAll()
        {
            for (int i = _loadedCache.Count - 1; i >= 0; i--) Destroy(_loadedCache[i]);
            _chunks.Clear();
            _loadedCache.Clear();
            RebuildAllIndices();
        }

        /// <summary>讓所有已載入區塊的實體層重畫（有東西的區塊才需要）。</summary>
        public void QueueEntityRedraws()
        {
            for (int i = 0; i < _loadedCache.Count; i++)
                if (_loadedCache[i].HasEntities && _loadedCache[i].View is ChunkView2D v)
                    v.QueueEntityRedraw();
        }

        /// <summary>格線/區塊邊界等顯示選項切換後，地形層全部重畫。</summary>
        public void QueueTerrainRedraws()
        {
            for (int i = 0; i < _loadedCache.Count; i++)
                if (_loadedCache[i].View is ChunkView2D v) v.QueueTerrainRedraw();
        }
    }
}
