// 一個區塊的 2D 檢視：地形層（載入時畫一次）+ 實體層（有東西才重畫）。
// 地形：星球底色 + 高度明暗 + 雜湊抖色 + 裝飾（岩石/草/水晶）+ 礦脈石簇（Art2D）。
// 實體：傳送帶（流動條紋 + 物品）+ 建築（程序化精靈 + 方向箭頭 + 進度條 + 警告）。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class ChunkView2D : Node2D
    {
        private const int N = GridCoord.ChunkSize;
        private const float TS = GridCoord.TileSizeF;

        private readonly Chunk _chunk;
        private readonly SimWorld _world;
        private readonly DrawLayer2D _terrainLayer;
        private readonly DrawLayer2D _entityLayer;

        public static bool ShowGrid = true;
        public static bool ShowChunkBorder = false;

        public ChunkView2D(Chunk chunk, SimWorld world)
        {
            _chunk = chunk;
            _world = world;
            _terrainLayer = new DrawLayer2D { Name = "Terrain", Drawer = DrawTerrain };
            _entityLayer = new DrawLayer2D { Name = "Entities", Drawer = DrawEntities };
            AddChild(_terrainLayer);
            AddChild(_entityLayer);
            _terrainLayer.QueueRedraw();
            _entityLayer.QueueRedraw();
        }

        public void QueueEntityRedraw() => _entityLayer.QueueRedraw();
        public void QueueTerrainRedraw() => _terrainLayer.QueueRedraw();

        // ── 地形層 ─────────────────────────────────────────────
        private void DrawTerrain(DrawLayer2D layer)
        {
            var planet = _world.Planet;
            uint salt = (uint)_world.Seed;
            for (int lz = 0; lz < N; lz++)
            {
                for (int lx = 0; lx < N; lx++)
                {
                    int i = lz * N + lx;
                    int wx = _chunk.OriginX + lx, wz = _chunk.OriginZ + lz;
                    float x = lx * TS, y = lz * TS;

                    var c = Palette2D.Terrain(planet, _chunk.Shade[i] / 255f);
                    // 雜湊抖色：每格 ±2% 亮度，取代生硬的棋盤格，呈現自然的地表紋理
                    float dither = Hash2D.Value01(wx, wz, salt) - 0.5f;
                    c = dither >= 0f ? c.Lightened(dither * 0.05f) : c.Darkened(-dither * 0.05f);
                    layer.DrawRect(new Rect2(x, y, TS, TS), c);

                    if (_chunk.TerrainKind[i] == 1)
                    {
                        Art2D.DrawOre(layer, new Vector2(x, y), (ItemId)_chunk.OreKind[i], _chunk.OreRich[i], wx, wz);
                    }
                    else
                    {
                        DrawDecor(layer, x, y, wx, wz, salt, c);
                    }
                }
            }

            if (ShowGrid)
            {
                for (int k = 0; k <= N; k++)
                {
                    float o = k * TS;
                    layer.DrawLine(new Vector2(o, 0), new Vector2(o, N * TS), Palette2D.GridLine, 1f);
                    layer.DrawLine(new Vector2(0, o), new Vector2(N * TS, o), Palette2D.GridLine, 1f);
                }
            }

            if (ShowChunkBorder)
            {
                layer.DrawRect(new Rect2(0, 0, N * TS, N * TS), Palette2D.ChunkLine, false, 2f);
            }
        }

        /// <summary>地面裝飾：以雜湊決定「哪些格子」有岩石／草／水晶，確定性且不阻擋建造。</summary>
        private static void DrawDecor(DrawLayer2D layer, float x, float y, int wx, int wz, uint salt, Color ground)
        {
            float h = Hash2D.Value01(wx * 3 + 1, wz * 7 - 2, salt ^ 0xDEADBEEFu);
            Vector2 c = new(x + TS * 0.5f, y + TS * 0.5f);

            if (h < 0.020f)
            {
                // 岩石
                layer.DrawColoredPolygon(new[] { c + new Vector2(-3f, 2f), c + new Vector2(2f, 1f), c + new Vector2(3f, -1f), c + new Vector2(0, -3f), c + new Vector2(-3f, -1f) }, Palette2D.Rock);
                layer.DrawCircle(c + new Vector2(-1f, -1f), 1f, Palette2D.RockDark);
            }
            else if (h < 0.045f)
            {
                // 草叢（3 根）
                for (int k = -1; k <= 1; k++)
                {
                    var baseP = c + new Vector2(k * 2.5f, 1.5f);
                    layer.DrawLine(baseP, baseP + new Vector2(k * 0.8f, -3f), Palette2D.Grass, 1.4f);
                }
            }
            else if (h < 0.055f)
            {
                // 水晶
                layer.DrawColoredPolygon(new[] { c + new Vector2(0, -4f), c + new Vector2(2f, 1f), c + new Vector2(-2f, 1f) }, Palette2D.Crystal);
                layer.DrawLine(c + new Vector2(-1f, 1f), c + new Vector2(-1f, -3f), new Color(1f, 1f, 1f, 0.5f), 1f);
            }
        }

        // ── 實體層（傳送帶 / 物品 / 建築）──────────────────────
        private void DrawEntities(DrawLayer2D layer)
        {
            float phase = FlowPhase();
            DrawBelts(layer, phase);
            DrawBuildings(layer);
        }

        /// <summary>帶面流動動畫相位（像素，0..TS）——跟著模擬的真實帶速（含研究與天氣加成）。</summary>
        private float FlowPhase()
        {
            double px = _world.Elapsed * GameData.BeltSpeed * _world.BeltSpeedMultiplier
                        * _world.Weather.BeltSpeedMultiplier * TS;
            return (float)(px % TS);
        }

        private void DrawBelts(DrawLayer2D layer, float phase)
        {
            for (int i = 0; i < _chunk.BeltKeys.Count; i++)
            {
                if (!_world.Belts.TryGetValue(_chunk.BeltKeys[i], out var t)) continue;
                float bx = (t.X - _chunk.OriginX) * TS;
                float bz = (t.Z - _chunk.OriginZ) * TS;
                var p = new Vector2(bx, bz);
                var center = p + new Vector2(TS * 0.5f, TS * 0.5f);
                Vector2 dir = GridCoord.DirVec(t.Facing);
                Vector2 side = new(-dir.Y, dir.X);

                // 帶體（深底 + 邊緣）
                layer.DrawRect(new Rect2(bx + 0.5f, bz + 0.5f, TS - 1f, TS - 1f), Palette2D.BeltBase);
                layer.DrawLine(p, p + dir * TS, Palette2D.BeltEdge, 2f);
                layer.DrawLine(p + side * TS, p + side * TS + dir * TS, Palette2D.BeltEdge, 2f);

                // 流動 V 形條紋：隨帶速向 facing 方向流動
                for (int k = 0; k < 2; k++)
                {
                    float u = ((phase + k * TS * 0.5f) % TS) / TS;
                    Vector2 a = center + dir * ((u - 0.5f) * TS);
                    Vector2 tip = a + dir * 5f;
                    Vector2 b1 = a - dir * 2f + side * 5f;
                    Vector2 b2 = a - dir * 2f - side * 5f;
                    layer.DrawColoredPolygon(new[] { tip, b1, b2 }, Palette2D.BeltChevron);
                }

                // 物品（Head 最靠出口往 Tail 走）
                int cur = _world.BeltItemHead(t);
                while (cur >= 0)
                {
                    byte kind = _world.ItemKindAt(cur);
                    float pos = _world.ItemPosAt(cur);
                    Vector2 ip = center + dir * ((pos - 0.5f) * TS);
                    Art2D.DrawItem(layer, ip, (ItemId)kind, 5f);
                    cur = _world.ItemNext(cur);
                }
            }
        }

        private void DrawBuildings(DrawLayer2D layer)
        {
            for (int i = 0; i < _chunk.BuildingKeys.Count; i++)
            {
                if (!_world.Buildings.TryGetValue(_chunk.BuildingKeys[i], out var b)) continue;
                float bx = (b.X - _chunk.OriginX) * TS;
                float bz = (b.Z - _chunk.OriginZ) * TS;
                var p = new Vector2(bx, bz);
                var inner = new Rect2(bx + 2.5f, bz + 2.5f, TS - 5f, TS - 5f);

                Art2D.DrawBuilding(layer, p, b);
                if (!b.Enabled) layer.DrawRect(inner, Palette2D.DisabledVeil);

                Vector2 dir = GridCoord.DirVec(b.Facing);
                if (Palette2D.HasOutput(b.Kind)) DrawEdgeArrow(layer, p, dir, Palette2D.ArrowOut);
                if (Palette2D.HasInput(b.Kind)) DrawEdgeArrow(layer, p, -dir, Palette2D.ArrowIn);

                // 生產進度條（礦機 / 熔煉爐 / 組裝機）
                float frac = -1f;
                if (b.Kind == BuildingKind.Miner)
                    frac = b.Progress / Mathf.Max(0.01f, GameData.MinerSeconds);
                else if (b.IsCrafter && b.Producing)
                {
                    var r = GameData.GetRecipe(b);
                    if (r.Seconds > 0f) frac = b.Progress / r.Seconds;
                }
                if (frac >= 0f)
                {
                    frac = Mathf.Clamp(frac, 0f, 1f);
                    layer.DrawRect(new Rect2(bx + 4f, bz + TS - 7f, TS - 8f, 3f), new Color(0f, 0f, 0f, 0.55f));
                    layer.DrawRect(new Rect2(bx + 4f, bz + TS - 7f, (TS - 8f) * frac, 3f), new Color(1f, 0.9f, 0.4f, 0.95f));
                }

                // 物流建築的「出貨未接線」警告標記
                if (Palette2D.HasOutput(b.Kind) && !_world.OutputConnected(b.X, b.Z, b.Facing))
                {
                    layer.DrawCircle(new Vector2(bx + TS - 6f, bz + 6f), 4.5f, Palette2D.CursorBad);
                }
            }
        }

        /// <summary>在格子的指定邊畫一顆方向箭頭（尖端朝 dir）。</summary>
        private static void DrawEdgeArrow(DrawLayer2D layer, Vector2 tilePos, Vector2 dir, Color color)
        {
            Vector2 mid = tilePos + new Vector2(TS * 0.5f, TS * 0.5f) + dir * (TS * 0.5f - 3f);
            Vector2 side = new(-dir.Y, dir.X);
            Vector2 tip = mid + dir * 6f;
            layer.DrawColoredPolygon(new[]
            {
                tip,
                mid + side * 5f - dir * 2f,
                mid - side * 5f - dir * 2f,
            }, color);
        }
    }
}
