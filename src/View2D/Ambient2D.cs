// Ambient2D：晝夜環境光 + 天氣粒子（雨 / 灰燼 / 離子火花 / 風暴閃電）。
// 疊在世界之上（Layer 1）、UI 之下，讓夜晚與天氣有可見的氛圍。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class Ambient2D : CanvasLayer
    {
        private SimWorld _world;
        private ColorRect _tint;
        private ParticleView _particles;
        private float _lightning;
        private float _lightningTimer;

        public void Setup(SimWorld world)
        {
            _world = world;
            Layer = 1;

            _tint = new ColorRect();
            _tint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _tint.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_tint);

            _particles = new ParticleView();
            _particles.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _particles.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_particles);
        }

        /// <summary>換星球/讀檔時更新世界參考（不重建節點）。</summary>
        public void SetWorld(SimWorld world) => _world = world;

        public override void _Process(double delta)
        {
            if (_world == null) return;
            float dt = (float)delta;

            var w = _world.Weather;
            float day = w.DayFactor;              // 0 深夜 .. 1 正午
            float night = Mathf.Clamp(1f - day, 0f, 1f);

            // 夜間深藍底
            var tint = new Color(0.05f, 0.09f, 0.26f);
            float alpha = night * 0.48f;

            // 天氣疊色
            switch (w.Current)
            {
                case WeatherKind.Storm: tint = tint.Lerp(new Color(0.10f, 0.12f, 0.16f), 0.5f); alpha += 0.10f; break;
                case WeatherKind.Ash:   tint = tint.Lerp(new Color(0.22f, 0.12f, 0.05f), 0.6f); alpha += 0.14f; break;
                case WeatherKind.Ion:   tint = tint.Lerp(new Color(0.20f, 0.10f, 0.30f), 0.7f); alpha += 0.08f; break;
                case WeatherKind.Cloudy: alpha += 0.03f; break;
            }

            // 風暴閃電：隨機白閃
            if (w.Current == WeatherKind.Storm)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0f)
                {
                    _lightning = 1f;
                    _lightningTimer = (float)GD.RandRange(2.0, 7.0);
                }
            }
            else _lightning = 0f;

            _lightning = Mathf.Max(0f, _lightning - dt * 3.2f);
            alpha += _lightning * 0.30f;

            _tint.Color = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(alpha, 0f, 0.95f));

            _particles.Weather = w.Current;
            _particles.Time += dt;
            _particles.QueueRedraw();
        }

        // 粒子視圖：以時間驅動的確定性雨/灰燼/火花
        private sealed partial class ParticleView : Control
        {
            public WeatherKind Weather;
            public float Time;

            public override void _Draw()
            {
                if (Weather == WeatherKind.Clear || Weather == WeatherKind.Cloudy) return;
                var size = GetViewportRect().Size;

                switch (Weather)
                {
                    case WeatherKind.Storm:
                        for (int i = 0; i < 140; i++)
                        {
                            uint h = Hash2D.Hash(i, 0x9E37, 0x1234u);
                            float x = (h % 2000) / 2000f * size.X;
                            float speed = 700f + (h >> 8) % 400;
                            float y = ((h >> 16) / 65535f * size.Y + Time * speed) % (size.Y + 40f) - 20f;
                            DrawLine(new Vector2(x, y), new Vector2(x - 2f, y + 14f), new Color(0.5f, 0.7f, 1f, 0.35f), 1.2f);
                        }
                        break;

                    case WeatherKind.Ash:
                        for (int i = 0; i < 90; i++)
                        {
                            uint h = Hash2D.Hash(i, 0x9E37, 0x5678u);
                            float x = ((h % 2000) / 2000f + Mathf.Sin(Time * 0.7f + i) * 0.02f) * size.X;
                            float speed = 40f + (h >> 8) % 40;
                            float y = ((h >> 16) / 65535f * size.Y + Time * speed) % (size.Y + 20f) - 10f;
                            DrawCircle(new Vector2(x, y), 1.6f, new Color(0.5f, 0.35f, 0.22f, 0.5f));
                        }
                        break;

                    case WeatherKind.Ion:
                        for (int i = 0; i < 40; i++)
                        {
                            uint h = Hash2D.Hash(i, 0x9E37, 0x9ABCu);
                            float x = (h % 2000) / 2000f * size.X;
                            float y = (h >> 8) / 65535f * size.Y;
                            if (((int)(Time * 2f + i) & 1) == 0)
                                DrawCircle(new Vector2(x, y), 2f, new Color(0.7f, 0.5f, 1f, 0.5f));
                        }
                        break;
                }
            }
        }
    }
}
