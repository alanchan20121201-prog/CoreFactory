// 2D 相機：WASD/方向鍵平移（螢幕速度固定，不受縮放影響）、滾輪縮放（以游標為錨點）、
// 中鍵拖曳、位置夾限（±32760 格 —— 尊重 Core 的 16-bit 座標打包）。
// 手感教訓來自 3D 版 0.7.0：移動速度不隨縮放劇變。
using Godot;

namespace CoreFactory.View2D
{
    public sealed partial class CameraController2D : Camera2D
    {
        public float PanScreenSpeed = 950f;     // 螢幕像素/秒（與縮放無關）
        public float ZoomStep = 1.15f;
        public float ZoomMin = 0.35f;
        public float ZoomMax = 3.0f;
        public float Smoothing = 16f;
        public bool InputEnabled = true;

        private static readonly float WorldLimit = (GridCoord.TileLimit + 0.5f) * GridCoord.TileSizeF;

        private Vector2 _targetPos;
        private float _targetZoom = 1f;
        private bool _dragging;
        private Vector2 _dragStartMouse, _dragStartPos;

        public override void _Ready()
        {
            Zoom = new Vector2(_targetZoom, _targetZoom);
            MakeCurrent();
        }

        public void SnapTo(Vector2 world)
        {
            _targetPos = ClampPos(world);
            Position = _targetPos;
        }

        public void SetZoom(float z)
        {
            _targetZoom = Mathf.Clamp(z, ZoomMin, ZoomMax);
            Zoom = new Vector2(_targetZoom, _targetZoom);
        }

        public Vector2 ScreenToWorld(Vector2 screen)
            => Position + (screen - GetViewportRect().Size * 0.5f) / Zoom;

        public Vector2 WorldToScreen(Vector2 world)
            => (world - Position) * Zoom + GetViewportRect().Size * 0.5f;

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (dt <= 0f) return;

            if (InputEnabled)
            {
                Vector2 dir = Vector2.Zero;
                if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1f;
                if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1f;
                if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1f;
                if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1f;
                if (dir != Vector2.Zero)
                {
                    // 螢幕速度固定：縮放越小，世界位移越大 —— 玩家感知的移動速度恆定
                    _targetPos += dir.Normalized() * PanScreenSpeed * dt / _targetZoom;
                    _targetPos = ClampPos(_targetPos);
                }
            }

            float k = 1f - Mathf.Exp(-Smoothing * dt);
            Position = Position.Lerp(_targetPos, k);
            float z = Mathf.Lerp(Zoom.X, _targetZoom, k);
            Zoom = new Vector2(z, z);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (!InputEnabled) return;

            if (e is InputEventMouseButton mb)
            {
                if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
                {
                    if (Input.IsKeyPressed(Key.Ctrl)) return;   // Ctrl+滾輪 = 換配方（交給 Main2D）
                    ZoomAt(mb.Position, ZoomStep);
                }
                else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
                {
                    if (Input.IsKeyPressed(Key.Ctrl)) return;
                    ZoomAt(mb.Position, 1f / ZoomStep);
                }
                else if (mb.ButtonIndex == MouseButton.Middle)
                {
                    _dragging = mb.Pressed;
                    if (_dragging) { _dragStartMouse = mb.Position; _dragStartPos = _targetPos; }
                }
            }
            else if (e is InputEventMouseMotion mm && _dragging)
            {
                _targetPos = ClampPos(_dragStartPos - (mm.Position - _dragStartMouse) / _targetZoom);
                Position = _targetPos;   // 拖曳要跟手 → 不經平滑
            }
        }

        private void ZoomAt(Vector2 screen, float factor)
        {
            float before = _targetZoom;
            float after = Mathf.Clamp(before * factor, ZoomMin, ZoomMax);
            if (Mathf.IsEqualApprox(before, after)) return;

            // 以游標為錨點：該世界點在縮放前後都停在游標底下
            Vector2 rel = screen - GetViewportRect().Size * 0.5f;
            Vector2 anchor = _targetPos + rel / before;
            _targetZoom = after;
            _targetPos = ClampPos(anchor - rel / after);
        }

        private static Vector2 ClampPos(Vector2 p)
            => new(Mathf.Clamp(p.X, -WorldLimit, WorldLimit), Mathf.Clamp(p.Y, -WorldLimit, WorldLimit));
    }
}
