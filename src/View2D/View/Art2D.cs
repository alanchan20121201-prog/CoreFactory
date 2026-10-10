// Art2D：程序化向量精靈 —— 不用任何貼圖檔，靠多層幾何（底板 + 倒角 + 圖標 + 高光）畫出高辨識度的工業風外觀。
// 所有方法都只呼叫 CanvasItem 的 Draw*，因此必須在 _Draw 內被呼叫（ChunkView2D / Overlay2D 都在 _Draw 內轉呼叫）。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class Art2D
    {
        public const float TS = GridCoord.TileSizeF;

        /// <summary>帶倒角的工業底板：填充 + 頂/左高光 + 底/右陰影 + 描邊。</summary>
        public static void Panel(CanvasItem ci, Rect2 r, Color fill, Color edge, Color hi, Color lo, float w = 1.5f)
        {
            ci.DrawRect(r, fill);
            ci.DrawLine(r.Position, r.Position + new Vector2(r.Size.X, 0f), hi, w);
            ci.DrawLine(r.Position, r.Position + new Vector2(0f, r.Size.Y), hi, w);
            ci.DrawLine(new Vector2(r.End.X, r.Position.Y), r.End, lo, w);
            ci.DrawLine(new Vector2(r.Position.X, r.End.Y), r.End, lo, w);
            ci.DrawRect(r, edge, false, w);
        }

        // ── 物品（傳送帶上的小物件 / 圖鑑圖示）──────────────────
        public static void DrawItem(CanvasItem ci, Vector2 c, ItemId item, float r)
        {
            var main = Palette2D.Item(item);
            var dark = Palette2D.ItemDark(item);
            var acc = Palette2D.ItemAccent(item);

            switch (item)
            {
                case ItemId.IronOre:
                case ItemId.CopperOre:
                case ItemId.Coal:
                case ItemId.Stone:
                case ItemId.SiliconOre:
                case ItemId.CrystalOre:
                case ItemId.GoldOre:
                case ItemId.CoreOre:
                case ItemId.TitaniumOre:
                    // 礦石 = 不規則石塊（菱形）＋ 亮點
                    ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.85f, 0), c + new Vector2(0, r), c + new Vector2(-r * 0.85f, 0) }, dark);
                    ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r * 0.62f), c + new Vector2(r * 0.5f, 0), c + new Vector2(0, r * 0.62f), c + new Vector2(-r * 0.5f, 0) }, main);
                    ci.DrawCircle(c + new Vector2(-r * 0.18f, -r * 0.2f), r * 0.2f, acc);
                    break;

                case ItemId.IronPlate:
                case ItemId.CopperPlate:
                case ItemId.GoldPlate:
                case ItemId.TitaniumPlate:
                    // 板材 = 圓角板 + 摺線
                    ci.DrawRect(new Rect2(c.X - r * 0.8f, c.Y - r * 0.5f, r * 1.6f, r), dark);
                    ci.DrawRect(new Rect2(c.X - r * 0.7f, c.Y - r * 0.4f, r * 1.4f, r * 0.8f), main);
                    ci.DrawLine(c + new Vector2(-r * 0.4f, -r * 0.4f), c + new Vector2(r * 0.4f, r * 0.4f), acc, 1f);
                    break;

                case ItemId.Gear:
                    // 齒輪 = 圓 + 8 齒
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.Tau / 8f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        ci.DrawRect(new Rect2(c + d * (r * 0.78f) - new Vector2(r * 0.18f, r * 0.18f), new Vector2(r * 0.36f, r * 0.36f)), dark);
                    }
                    ci.DrawCircle(c, r * 0.72f, main);
                    ci.DrawCircle(c, r * 0.32f, dark);
                    break;

                case ItemId.Circuit:
                    // 電路 = 綠晶片 + 金腳
                    ci.DrawRect(new Rect2(c.X - r * 0.7f, c.Y - r * 0.6f, r * 1.4f, r * 1.2f), dark);
                    ci.DrawRect(new Rect2(c.X - r * 0.55f, c.Y - r * 0.45f, r * 1.1f, r * 0.9f), main);
                    for (int i = -1; i <= 1; i++)
                    {
                        ci.DrawRect(new Rect2(c.X + i * r * 0.4f - r * 0.08f, c.Y - r * 0.78f, r * 0.16f, r * 0.4f), acc);
                        ci.DrawRect(new Rect2(c.X + i * r * 0.4f - r * 0.08f, c.Y + r * 0.38f, r * 0.16f, r * 0.4f), acc);
                    }
                    break;

                case ItemId.Science:
                    // 科研包 = 燒瓶（圓 + 頸）
                    ci.DrawCircle(c + new Vector2(0, r * 0.1f), r * 0.62f, main);
                    ci.DrawRect(new Rect2(c.X - r * 0.18f, c.Y - r * 0.85f, r * 0.36f, r * 0.5f), dark);
                    ci.DrawCircle(c + new Vector2(-r * 0.2f, r * 0.05f), r * 0.16f, acc);
                    break;

                case ItemId.SiliconWafer:
                    // 矽晶圓 = 薄圓片 + 格子紋
                    ci.DrawCircle(c, r * 0.8f, dark);
                    ci.DrawCircle(c, r * 0.68f, main);
                    ci.DrawCircle(c, r * 0.68f, acc, false, 1f);
                    ci.DrawLine(c + new Vector2(-r * 0.5f, 0), c + new Vector2(r * 0.5f, 0), acc, 1f);
                    ci.DrawLine(c + new Vector2(0, -r * 0.5f), c + new Vector2(0, r * 0.5f), acc, 1f);
                    break;

                case ItemId.Crystal:
                case ItemId.CoreCrystal:
                    // 晶體 = 六角柱（上尖 + 透光高光）
                    ci.DrawColoredPolygon(new[]
                    {
                        c + new Vector2(0, -r * 0.95f), c + new Vector2(r * 0.55f, -r * 0.3f),
                        c + new Vector2(r * 0.4f, r * 0.8f), c + new Vector2(-r * 0.4f, r * 0.8f),
                        c + new Vector2(-r * 0.55f, -r * 0.3f),
                    }, dark);
                    ci.DrawColoredPolygon(new[]
                    {
                        c + new Vector2(0, -r * 0.8f), c + new Vector2(r * 0.32f, -r * 0.2f),
                        c + new Vector2(0, r * 0.6f), c + new Vector2(-r * 0.32f, -r * 0.2f),
                    }, main);
                    ci.DrawLine(c + new Vector2(-r * 0.15f, -r * 0.4f), c + new Vector2(r * 0.1f, r * 0.3f), acc, 1.4f);
                    break;

                case ItemId.AdvancedCircuit:
                    // 高級電路 = 雙層晶片 + 發光核心
                    ci.DrawRect(new Rect2(c.X - r * 0.78f, c.Y - r * 0.68f, r * 1.56f, r * 1.36f), dark);
                    ci.DrawRect(new Rect2(c.X - r * 0.62f, c.Y - r * 0.52f, r * 1.24f, r * 1.04f), main);
                    ci.DrawRect(new Rect2(c.X - r * 0.26f, c.Y - r * 0.22f, r * 0.52f, r * 0.44f), acc);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        ci.DrawRect(new Rect2(c.X + i * r * 0.5f - r * 0.07f, c.Y - r * 0.88f, r * 0.14f, r * 0.34f), acc);
                        ci.DrawRect(new Rect2(c.X + i * r * 0.5f - r * 0.07f, c.Y + r * 0.54f, r * 0.14f, r * 0.34f), acc);
                    }
                    break;

                case ItemId.HeavyAmmo:
                    // 穿甲彈 = 加長彈殼 + 尖錐彈頭（橘色高熱）
                    ci.DrawRect(new Rect2(c.X - r * 0.34f, c.Y - r * 0.45f, r * 0.68f, r * 0.95f), dark);
                    ci.DrawRect(new Rect2(c.X - r * 0.28f, c.Y - r * 0.38f, r * 0.56f, r * 0.8f), main);
                    ci.DrawColoredPolygon(new[]
                    {
                        c + new Vector2(-r * 0.34f, -r * 0.45f), c + new Vector2(r * 0.34f, -r * 0.45f),
                        c + new Vector2(0, -r * 1.0f),
                    }, acc);
                    break;

                case ItemId.Ammo:
                    // 砲彈 = 彈殼（黃銅底 + 紅頭）
                    ci.DrawRect(new Rect2(c.X - r * 0.4f, c.Y - r * 0.7f, r * 0.8f, r * 0.7f), dark);
                    ci.DrawRect(new Rect2(c.X - r * 0.34f, c.Y - r * 0.62f, r * 0.68f, r * 0.56f), acc);
                    ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.34f, -r * 0.6f), c + new Vector2(r * 0.34f, -r * 0.6f), c + new Vector2(0, -r) }, main);
                    break;

                default:
                    ci.DrawCircle(c, r, main);
                    ci.DrawCircle(c, r, dark, false, 1f);
                    break;
            }
        }

        // ── 建築（origin = 格子左上角，chunk 局部座標）────────────
        public static void DrawBuilding(CanvasItem ci, Vector2 origin, Building b)
        {
            var kind = b.Kind;
            var main = Palette2D.Building(kind);
            var dark = Palette2D.BuildingDark(kind);
            var acc = Palette2D.BuildingAccent(kind);
            var glyph = Palette2D.BuildingGlyph(kind);
            var body = new Rect2(origin.X + 2.5f, origin.Y + 2.5f, TS - 5f, TS - 5f);
            Vector2 c = origin + new Vector2(TS * 0.5f, TS * 0.5f);
            Vector2 dir = GridCoord.DirVec(b.Facing);

            // 底座（略微外擴的暗色襯墊，讓建築「浮」在地面上）
            ci.DrawRect(new Rect2(origin.X + 1f, origin.Y + 1f, TS - 2f, TS - 2f), new Color(0f, 0f, 0f, 0.35f));

            switch (kind)
            {
                case BuildingKind.Miner:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 鑽頭（朝向 facing）
                    Vector2 tip = c + dir * TS * 0.30f;
                    Vector2 side = new(-dir.Y, dir.X);
                    ci.DrawColoredPolygon(new[] { tip, c + side * 5f - dir * 3f, c - side * 5f - dir * 3f }, glyph);
                    ci.DrawCircle(c, 3.5f, Palette2D.Item((ItemId)b.KindValue));
                    break;

                case BuildingKind.Smelter:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 爐口（暗窗）+ 火焰
                    ci.DrawRect(new Rect2(c.X - 6f, c.Y - 1f, 12f, 9f), dark);
                    ci.DrawColoredPolygon(new[] { c + new Vector2(-4f, 6f), c + new Vector2(4f, 6f), c + new Vector2(0, -3f) }, new Color("ff7a30"));
                    ci.DrawColoredPolygon(new[] { c + new Vector2(-2f, 6f), c + new Vector2(2f, 6f), c + new Vector2(0, 0.5f) }, glyph);
                    break;

                case BuildingKind.Assembler:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 齒輪圖標
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.Tau / 6f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        ci.DrawRect(new Rect2(c + d * 6f - new Vector2(1.6f, 1.6f), new Vector2(3.2f, 3.2f)), glyph);
                    }
                    ci.DrawCircle(c, 5f, glyph);
                    ci.DrawCircle(c, 2f, dark);
                    break;

                case BuildingKind.Depot:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 儲物格
                    for (int i = 0; i < 3; i++)
                        ci.DrawRect(new Rect2(c.X - 8f, origin.Y + 8f + i * 6f, 16f, 3f), dark);
                    ci.DrawLine(origin + new Vector2(6f, 6f), origin + new Vector2(TS - 6f, TS - 6f), acc, 1.2f);
                    break;

                case BuildingKind.SolarPanel:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 2×2 電池陣列 + 高光斜線
                    for (int ix = 0; ix < 2; ix++)
                        for (int iy = 0; iy < 2; iy++)
                            ci.DrawRect(new Rect2(origin.X + 6f + ix * 11f, origin.Y + 6f + iy * 11f, 9f, 9f), new Color("3f7cc8"));
                    ci.DrawLine(origin + new Vector2(7f, 7f), origin + new Vector2(TS - 7f, TS - 7f), new Color(0.6f, 0.9f, 1f, 0.8f), 1.6f);
                    break;

                case BuildingKind.ThermalGenerator:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 煙囪 + 燃燒核心
                    ci.DrawRect(new Rect2(c.X - 2f, origin.Y + 4f, 4f, 8f), dark);
                    ci.DrawCircle(c + new Vector2(0, 3f), 5f, dark);
                    ci.DrawCircle(c + new Vector2(0, 3f), 3f, glyph);
                    break;

                case BuildingKind.Battery:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 電池端子 + 閃電
                    ci.DrawRect(new Rect2(c.X - 3f, origin.Y + 3f, 6f, 3f), glyph);
                    ci.DrawColoredPolygon(new[] { c + new Vector2(1f, -5f), c + new Vector2(-2f, 1f), c + new Vector2(0.5f, 1f), c + new Vector2(-1f, 5f), c + new Vector2(2f, -1f), c + new Vector2(-0.5f, -1f) }, glyph);
                    break;

                case BuildingKind.Turret:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 砲座 + 砲管（朝 facing）
                    ci.DrawCircle(c, 7f, dark);
                    Vector2 btip = c + dir * 11f;
                    ci.DrawRect(new Rect2(c.X - 2.5f, c.Y - 2.5f, 5f, 5f), glyph);
                    ci.DrawLine(c, btip, glyph, 4f);
                    break;

                case BuildingKind.Lamp:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 燈泡 + 光暈
                    ci.DrawCircle(c + new Vector2(0, -2f), 6f, glyph);
                    ci.DrawCircle(c + new Vector2(0, -2f), 3f, new Color(1f, 1f, 0.95f));
                    break;

                case BuildingKind.SensorNode:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 天線 + 眼睛
                    ci.DrawLine(c, c + new Vector2(0, -9f), glyph, 2f);
                    ci.DrawCircle(c + new Vector2(0, -9f), 2.4f, glyph);
                    ci.DrawCircle(c + new Vector2(0, 1f), 5f, dark);
                    ci.DrawCircle(c + new Vector2(0, 1f), 2.6f, glyph);
                    break;

                case BuildingKind.ComparatorNode:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 「>」比較符號
                    ci.DrawLine(c + new Vector2(-4f, -4f), c + new Vector2(4f, 0f), glyph, 2.6f);
                    ci.DrawLine(c + new Vector2(-4f, 4f), c + new Vector2(4f, 0f), glyph, 2.6f);
                    break;

                case BuildingKind.ActuatorNode:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    // 開關（滑軌 + 旋鈕）
                    ci.DrawLine(c + new Vector2(-5f, 0f), c + new Vector2(5f, 0f), glyph, 2f);
                    ci.DrawCircle(b.Enabled ? c + new Vector2(4f, 0f) : c + new Vector2(-4f, 0f), 3f, glyph);
                    break;

                default:
                    Panel(ci, body, main, Palette2D.BuildingEdge, acc, dark);
                    break;
            }
        }

        // ── 礦脈（origin = 格子左上角；以雜湊做確定性抖動，畫出石塊簇）──
        public static void DrawOre(CanvasItem ci, Vector2 origin, ItemId ore, int rich, int wx, int wz)
        {
            var main = Palette2D.Item(ore);
            var dark = Palette2D.ItemDark(ore);
            var acc = Palette2D.ItemAccent(ore);
            Vector2 c = origin + new Vector2(TS * 0.5f, TS * 0.5f);

            // 陰影底
            ci.DrawCircle(c + new Vector2(1f, 1.5f), 6f, new Color(0f, 0f, 0f, 0.25f));

            // 依豐富度決定石塊數（1..4）
            int rocks = 1 + (rich / 3);
            if (rocks > 4) rocks = 4;
            for (int i = 0; i < rocks; i++)
            {
                uint h = Hash2D.Hash(wx * 7 + i * 131, wz * 5 - i * 29, 0x5A5A5A5Au);
                float jx = ((h & 0xFF) / 255f - 0.5f) * 12f;
                float jy = (((h >> 8) & 0xFF) / 255f - 0.5f) * 12f;
                float s = 4.5f + ((h >> 16) & 0xFF) / 255f * 3.5f;
                var pc = c + new Vector2(jx, jy);
                // 不規則石塊（五邊形）
                var pts = new Vector2[5];
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.Tau / 5f + (h & 7) * 0.15f;
                    float rr = s * (0.7f + ((h >> (k * 3 + 2)) & 7) / 7f * 0.6f);
                    pts[k] = pc + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
                }
                ci.DrawColoredPolygon(pts, i % 2 == 0 ? main : dark);
                ci.DrawCircle(pc + new Vector2(-s * 0.2f, -s * 0.25f), s * 0.22f, acc);
            }
        }

        // ── 蟲潮（pos = 世界像素，身體半徑 = a.Radius * TS）────────
        public static void DrawAlien(CanvasItem ci, Vector2 pos, Alien a)
        {
            float R = a.Radius * TS;
            var main = Palette2D.AlienBody(a.Kind);
            var dark = Palette2D.AlienDark(a.Kind);
            var acc = Palette2D.AlienAccent(a.Kind);
            float wob = a.Moving ? Mathf.Sin(a.WalkPhase) : 0f;

            // 地面陰影
            ci.DrawCircle(pos + new Vector2(0, R * 0.4f), R * 0.95f, new Color(0f, 0f, 0f, 0.3f));

            // 腳（依種類不同數量，走路時前後擺動）
            int legs = a.Kind switch
            {
                AlienKind.Brute or AlienKind.Elite or AlienKind.Frost or AlienKind.Titan => 4,
                AlienKind.Magma => 6,
                AlienKind.Flyer => 0,      // 飛行蟲用翅膀，不畫腳
                _ => 2,
            };
            for (int i = 0; i < legs; i++)
            {
                float phase = wob + i * Mathf.Pi;
                var ldir = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
                var side = new Vector2(-ldir.Y, ldir.X);
                float off = (i % 2 == 0 ? 1f : -1f) * R * 0.4f;
                var footBase = pos + side * off;
                var foot = footBase + ldir * Mathf.Sin(phase) * R * 0.35f;
                ci.DrawLine(footBase, foot, dark, Mathf.Max(1.5f, R * 0.22f));
            }

            // 身體
            switch (a.Kind)
            {
                case AlienKind.Brute:
                    // 六邊形重甲
                    var hex = new Vector2[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float aa = i * Mathf.Tau / 6f + a.Yaw;
                        hex[i] = pos + new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * R;
                    }
                    ci.DrawColoredPolygon(hex, main);
                    ci.DrawPolyline(hex, dark, 1.6f);
                    // 裝甲板
                    ci.DrawLine(pos, pos + new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw)) * R * 0.9f, acc, 2f);
                    break;

                case AlienKind.Elite:
                    // 大圓 + 棘冠
                    ci.DrawCircle(pos, R, main);
                    ci.DrawCircle(pos, R, dark, false, 1.6f);
                    for (int i = 0; i < 5; i++)
                    {
                        float aa = i * Mathf.Tau / 5f + a.Yaw;
                        var s = new Vector2(Mathf.Cos(aa), Mathf.Sin(aa));
                        ci.DrawLine(pos + s * R * 0.7f, pos + s * R * 1.15f, acc, 2.2f);
                    }
                    break;

                case AlienKind.Bomber:
                    // 圓身 + 引信（閃爍）+ 警示條紋
                    ci.DrawCircle(pos, R, main);
                    ci.DrawCircle(pos, R, dark, false, 1.5f);
                    for (int i = -1; i <= 1; i++)
                        ci.DrawLine(pos + new Vector2(i * R * 0.42f, -R * 0.55f),
                                    pos + new Vector2(i * R * 0.42f + R * 0.22f, R * 0.55f), acc, 2.2f);
                    ci.DrawLine(pos, pos + new Vector2(0, -R * 1.05f), dark, 2f);   // 引信
                    ci.DrawCircle(pos + new Vector2(0, -R * 1.15f), R * 0.2f * (1f + 0.3f * Mathf.Sin(a.WalkPhase * 3f)),
                        new Color("ffe070"));                                        // 火花
                    break;

                case AlienKind.Flyer:
                {
                    // 細身 + 兩對拍動的翅膀
                    var fw2 = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
                    var sd2 = new Vector2(-fw2.Y, fw2.X);
                    ci.DrawCircle(pos, R * 0.72f, main);
                    float flap = Mathf.Sin(a.WalkPhase * 2.2f) * R * 0.5f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        ci.DrawColoredPolygon(new[]
                        {
                            pos + sd2 * s * R * 0.5f,
                            pos + sd2 * s * R * 1.5f + fw2 * flap,
                            pos + sd2 * s * R * 1.1f - fw2 * R * 0.4f,
                        }, new Color(1f, 1f, 1f, 0.55f));
                    }
                    ci.DrawCircle(pos, R * 0.72f, dark, false, 1.3f);
                    ci.DrawCircle(pos + fw2 * R * 0.4f, R * 0.18f, acc);
                    break;
                }

                case AlienKind.Frost:
                    // 冰甲：多邊形 + 冰刺
                    var ice = new Vector2[7];
                    for (int i = 0; i < 7; i++)
                    {
                        float aa = i * Mathf.Tau / 7f + a.Yaw;
                        ice[i] = pos + new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * R;
                    }
                    ci.DrawColoredPolygon(ice, main);
                    ci.DrawPolyline(ice, dark, 1.5f);
                    for (int i = 0; i < 6; i++)
                    {
                        float aa = i * Mathf.Tau / 6f + a.Yaw * 0.5f;
                        var s = new Vector2(Mathf.Cos(aa), Mathf.Sin(aa));
                        ci.DrawLine(pos + s * R * 0.8f, pos + s * R * 1.35f, acc, 1.8f);
                    }
                    break;

                case AlienKind.Magma:
                    // 厚甲 + 熔岩裂紋（發光）
                    ci.DrawCircle(pos, R, dark);
                    ci.DrawCircle(pos, R * 0.86f, main);
                    for (int i = 0; i < 4; i++)
                    {
                        float aa = i * Mathf.Tau / 4f + a.Yaw;
                        var s = new Vector2(Mathf.Cos(aa), Mathf.Sin(aa));
                        var e = new Vector2(Mathf.Cos(aa + 0.7f), Mathf.Sin(aa + 0.7f));
                        ci.DrawLine(pos + s * R * 0.9f, pos + e * R * 0.35f, new Color("ffd060"), 2.2f);
                    }
                    ci.DrawCircle(pos, R * 0.3f, new Color("ff9040"));
                    break;

                case AlienKind.Titan:
                    // BOSS：巨大核心 + 環繞護盾 + 角
                    ci.DrawCircle(pos, R * 1.05f, new Color(0.2f, 0.05f, 0.3f, 0.5f));
                    var tit = new Vector2[8];
                    for (int i = 0; i < 8; i++)
                    {
                        float aa = i * Mathf.Tau / 8f + a.Yaw;
                        tit[i] = pos + new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * R;
                    }
                    ci.DrawColoredPolygon(tit, main);
                    ci.DrawPolyline(tit, dark, 2f);
                    ci.DrawCircle(pos, R * 0.42f, acc);                              // 核心
                    ci.DrawCircle(pos, R * 0.42f + 2f * Mathf.Sin(a.WalkPhase * 2f), new Color(1f, 1f, 1f, 0.35f), false, 2f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var fw3 = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
                        var sd3 = new Vector2(-fw3.Y, fw3.X);
                        ci.DrawLine(pos + fw3 * R * 0.6f + sd3 * s * R * 0.4f,
                                    pos + fw3 * R * 1.5f + sd3 * s * R * 0.9f, acc, 2.6f);   // 角
                    }
                    break;

                case AlienKind.Runner:
                    // 流線三角形（朝 yaw）
                    var fwd = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
                    var sd = new Vector2(-fwd.Y, fwd.X);
                    ci.DrawColoredPolygon(new[] { pos + fwd * R, pos - fwd * R * 0.8f + sd * R * 0.6f, pos - fwd * R * 0.8f - sd * R * 0.6f }, main);
                    ci.DrawPolyline(new[] { pos + fwd * R, pos - fwd * R * 0.8f + sd * R * 0.6f, pos - fwd * R * 0.8f - sd * R * 0.6f, pos + fwd * R }, dark, 1.4f);
                    break;

                default: // Grunt / Spitter = 圓形身體
                    ci.DrawCircle(pos, R, main);
                    ci.DrawCircle(pos, R, dark, false, 1.5f);
                    if (a.Kind == AlienKind.Spitter)
                    {
                        // 酸液嘴
                        var fw = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
                        ci.DrawCircle(pos + fw * R * 0.55f, R * 0.32f, new Color("c8e85a"));
                    }
                    break;
            }

            // 眼睛（面向 yaw 前側）
            var eyeFwd = new Vector2(-Mathf.Sin(a.Yaw), -Mathf.Cos(a.Yaw));
            var eyeSide = new Vector2(-eyeFwd.Y, eyeFwd.X);
            var eyeBase = pos + eyeFwd * R * 0.35f;
            ci.DrawCircle(eyeBase + eyeSide * R * 0.28f, R * 0.16f, new Color(1f, 1f, 1f, 0.95f));
            ci.DrawCircle(eyeBase - eyeSide * R * 0.28f, R * 0.16f, new Color(1f, 1f, 1f, 0.95f));
            ci.DrawCircle(eyeBase + eyeSide * R * 0.28f, R * 0.08f, new Color(0.1f, 0.1f, 0.1f));
            ci.DrawCircle(eyeBase - eyeSide * R * 0.28f, R * 0.08f, new Color(0.1f, 0.1f, 0.1f));
        }
    }
}
