// 跨區塊的動態覆蓋層：游標格高亮 + 放置幽靈 + 多種蟲潮 + 夜間燈光。
// 掛在 Main2D 底下，直接用世界座標繪製。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class Overlay2D : Node2D
    {
        private const float TS = GridCoord.TileSizeF;

        private SimWorld _world;
        private CameraController2D _cam;

        public bool ShowCursor = true;

        // 放置幽靈（由 Main2D 每幀設定）
        private BuildingKind _ghostKind = BuildingKind.None;
        private Dir _ghostFacing = Dir.East;
        private bool _ghostValid;
        private int _ghostX, _ghostZ;
        private bool _hasGhost;

        public void Setup(SimWorld world, CameraController2D cam)
        {
            _world = world;
            _cam = cam;
        }

        public void SetGhost(BuildingKind kind, Dir facing, int x, int z, bool valid)
        {
            _ghostKind = kind;
            _ghostFacing = facing;
            _ghostX = x;
            _ghostZ = z;
            _ghostValid = valid;
            _hasGhost = true;
        }

        public void ClearGhost() => _hasGhost = false;

        public override void _Process(double delta)
        {
            // 走路動畫相位（模擬層不管動畫，這裡累加）
            if (_world == null) return;
            var aliens = _world.Aliens;
            for (int i = 0; i < aliens.Count; i++)
                if (aliens[i].Moving) aliens[i].WalkPhase += (float)delta * 10f;
        }

        public override void _Draw()
        {
            if (_world == null || _cam == null) return;

            if (ShowCursor)
            {
                Vector2 mouse = GetViewport().GetMousePosition();
                Vector2 w = _cam.ScreenToWorld(mouse);
                int tx = GridCoord.WorldToTileX(w.X);
                int tz = GridCoord.WorldToTileZ(w.Y);
                bool ok = GridCoord.InBounds(tx, tz) && !_world.IsOccupied(tx, tz);
                var rect = new Rect2(tx * TS + 1f, tz * TS + 1f, TS - 2f, TS - 2f);
                DrawRect(rect, ok ? Palette2D.CursorOk : Palette2D.CursorBad, false, 2f);
            }

            DrawGhost();

            // 夜間燈光光暈
            float day = _world.Weather.DayFactor;
            if (day < 0.5f)
            {
                float glow = 1f - day * 2f;   // 0..1，越夜越亮
                foreach (var kv in _world.Buildings)
                {
                    var b = kv.Value;
                    if (b.Kind != BuildingKind.Lamp) continue;
                    var p = new Vector2((b.X + 0.5f) * TS, (b.Z + 0.5f) * TS);
                    for (int r = 3; r >= 1; r--)
                        DrawCircle(p, r * 14f, new Color(1f, 0.92f, 0.55f, 0.05f * glow));
                }
            }

            // 蟲潮
            var aliens = _world.Aliens;
            for (int i = 0; i < aliens.Count; i++)
            {
                var a = aliens[i];
                var pos = new Vector2(a.X * TS, a.Z * TS);
                Art2D.DrawAlien(this, pos, a);

                // 血條
                float maxHp = Mathf.Max(1f, AlienDefs.Base(a.Kind).hp);
                float hp01 = Mathf.Clamp(a.Hp / maxHp, 0f, 1f);
                float wdt = a.Radius * TS * 2f;
                DrawRect(new Rect2(pos.X - wdt * 0.5f, pos.Y - a.Radius * TS - 8f, wdt, 3f), Palette2D.AlienHpBack);
                DrawRect(new Rect2(pos.X - wdt * 0.5f, pos.Y - a.Radius * TS - 8f, wdt * hp01, 3f), Palette2D.AlienHpFill);
            }
        }

        private void DrawGhost()
        {
            if (!_hasGhost || _ghostKind == BuildingKind.None) return;
            var rect = new Rect2(_ghostX * TS + 1f, _ghostZ * TS + 1f, TS - 2f, TS - 2f);
            var fill = _ghostValid ? new Color(0.40f, 1.00f, 0.50f, 0.28f) : new Color(1.00f, 0.35f, 0.30f, 0.28f);
            var edge = _ghostValid ? Palette2D.CursorOk : Palette2D.CursorBad;
            DrawRect(rect, fill);
            DrawRect(rect, edge, false, 2f);

            // 方向箭頭（放置前就讓玩家看到朝向與進/出料方向）
            Vector2 dir = GridCoord.DirVec(_ghostFacing);
            if (_ghostKind == BuildingKind.Belt)
                DrawGhostArrow(_ghostX, _ghostZ, dir, Palette2D.ArrowOut);
            else
            {
                bool any = false;
                if (Palette2D.HasOutput(_ghostKind))
                { DrawGhostArrow(_ghostX, _ghostZ, dir, Palette2D.ArrowOut); any = true; }
                if (Palette2D.HasInput(_ghostKind))
                { DrawGhostArrow(_ghostX, _ghostZ, -dir, Palette2D.ArrowIn); any = true; }
                if (!any) DrawGhostArrow(_ghostX, _ghostZ, dir, new Color(1f, 1f, 1f, 0.75f));   // 無進出料者也顯示朝向
            }
        }

        private void DrawGhostArrow(int x, int z, Vector2 dir, Color color)
        {
            Vector2 mid = new Vector2((x + 0.5f) * TS, (z + 0.5f) * TS) + dir * (TS * 0.5f - 3f);
            Vector2 side = new(-dir.Y, dir.X);
            Vector2 tip = mid + dir * 6f;
            DrawColoredPolygon(new[] { tip, mid + side * 5f - dir * 2f, mid - side * 5f - dir * 2f },
                new Color(color.R, color.G, color.B, 0.8f));
        }
    }
}
