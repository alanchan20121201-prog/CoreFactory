// 除錯 HUD（Step 1 版）：FPS / 區塊串流統計 / 游標格資訊 / 模擬與電力摘要。
// 純 ASCII 輸出 —— Godot 內建字型沒有 CJK 字符；正式 UI 在 Step 3 以系統字型（比照 3D 版 ThemeFactory）實作。
using System.Text;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class DebugHud2D : CanvasLayer
    {
        private SimWorld _world;
        private ChunkManager _chunks;
        private WorldGrid2D _grid;
        private CameraController2D _cam;

        private Label _info;
        private Label _hint;
        private readonly StringBuilder _sb = new();
        private float _timer;

        public bool Paused;

        public void Setup(SimWorld world, ChunkManager chunks, WorldGrid2D grid, CameraController2D cam)
        {
            _world = world;
            _chunks = chunks;
            _grid = grid;
            _cam = cam;

            _info = MakeLabel(13);
            _info.Position = new Vector2(8, 6);
            AddChild(_info);

            _hint = MakeLabel(12);
            AddChild(_hint);
            // 不在這裡 Refresh：Setup 可能在節點進入場景樹之前被呼叫，GetViewport() 會是 null。
            // 第一次刷新交給 _Process（進樹之後才會跑）。
        }

        private static Label MakeLabel(int size)
        {
            var l = new Label();
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", new Color(0.92f, 1f, 0.92f));
            l.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
            l.AddThemeConstantOverride("outline_size", 6);
            return l;
        }

        public override void _Process(double delta)
        {
            if (_world == null) return;
            _timer -= (float)delta;
            if (_timer > 0f) return;
            _timer = 0.15f;
            Refresh();
        }

        private void Refresh()
        {
            Vector2 vp = GetViewport().GetVisibleRect().Size;
            _hint.Position = new Vector2(8, vp.Y - 26);

            Vector2 mouseW = _cam.ScreenToWorld(GetViewport().GetMousePosition());
            int tx = GridCoord.WorldToTileX(mouseW.X);
            int tz = GridCoord.WorldToTileZ(mouseW.Y);

            _sb.Clear();
            if (Paused) _sb.Append("== PAUSED ==  ");
            _sb.Append("CoreFactory 2D   ").Append(Engine.GetFramesPerSecond()).Append(" FPS")
               .Append("   zoom ").Append(_cam.Zoom.X.ToString("F2"))
               .Append("   cam (").Append(_cam.Position.X.ToString("F0")).Append(", ").Append(_cam.Position.Y.ToString("F0")).Append(')')
               .Append('\n');
            _sb.Append("chunks ").Append(_chunks.LoadedCount)
               .Append("   terrain tiles ").Append(_chunks.TerrainTilesBuilt)
               .Append("   sim t=").Append(_world.Elapsed.ToString("F1")).Append("s  ticks=").Append(_world.TickCount)
               .Append('\n');
            _sb.Append("cursor (").Append(tx).Append(", ").Append(tz).Append("): ").Append(_grid.DescribeTile(tx, tz))
               .Append('\n');
            _sb.Append("items ").Append(_world.ItemCount)
               .Append("   belts ").Append(_world.BeltCount)
               .Append("   buildings ").Append(_world.BuildingCount)
               .Append("   aliens ").Append(_world.AlienCount)
               .Append('\n');
            _sb.Append("power gen ").Append(_world.PowerGenerated.ToString("F0"))
               .Append("  use ").Append(_world.PowerUsed.ToString("F0"))
               .Append("  eff ").Append((_world.Efficiency * 100f).ToString("F0")).Append('%')
               .Append("  battery ").Append(_world.BatteryStored.ToString("F0"))
               .Append('\n');
            _sb.Append("weather ").Append(_world.Weather.Current)
               .Append("   day ").Append(_world.Weather.DayFactor.ToString("F2"))
               .Append("   wave ").Append(_world.WaveIndex)
               .Append("   research ").Append(_world.Research[0]).Append('/').Append(_world.Research[1])
               .Append('/').Append(_world.Research[2]).Append('/').Append(_world.Research[3]);
            _info.Text = _sb.ToString();

            _hint.Text = "[WASD/Arrows] pan   [Wheel] zoom at cursor   [Middle-drag] pan   [F1] grid   [F2] chunk borders   [Space] pause";
        }
    }
}
