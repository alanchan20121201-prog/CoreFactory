// 天氣與晝夜：不是裝飾，而是會改變設計約束的機械系統（太陽能、傳送帶速度、砲塔可用性）
using System;

namespace CoreFactory.Core
{
    public sealed class WeatherSystem
    {
        public const float DayLength = 480f;     // 8 分鐘一晝夜

        public float Time;
        public WeatherKind Current = WeatherKind.Clear;
        public float ChangeIn = 90f;

        private Rng _rng;
        private readonly PlanetDef _planet;

        public WeatherSystem(int seed, PlanetDef planet)
        {
            _rng = new Rng((ulong)seed * 6364136223846793005UL + 1442695040888963407UL);
            _planet = planet;
            Current = planet.WeatherPool[0];
            Time = DayLength * 0.40f;      // 開局是明亮的上午（不讓玩家一開始就摸黑）
        }

        /// <summary>0 = 深夜，1 = 正午。</summary>
        public float DayFactor => 0.5f + 0.5f * MathF.Sin(Time / DayLength * MathF.Tau - MathF.PI * 0.5f);
        public bool IsNight => DayFactor < 0.12f;

        public float WeatherSolarMultiplier => Current switch
        {
            WeatherKind.Clear => 1.00f,
            WeatherKind.Cloudy => 0.55f,
            WeatherKind.Storm => 0.22f,
            WeatherKind.Ash => 0.18f,
            WeatherKind.Ion => 0.00f,
            _ => 1f,
        };

        /// <summary>沙暴會讓傳送帶變慢 —— 這是最「看得見」的天氣機械效果。</summary>
        public float BeltSpeedMultiplier => Current switch
        {
            WeatherKind.Storm => 0.85f,
            WeatherKind.Ash => 0.70f,
            WeatherKind.Ion => 0.95f,
            _ => 1.00f,
        };

        /// <summary>離子風暴會週期性癱瘓砲塔（逼你準備後備系統）。</summary>
        public bool TurretsOnline
        {
            get
            {
                if (Current != WeatherKind.Ion) return true;
                return ((int)(Time * 0.5f) & 1) == 0;
            }
        }

        public float SolarFactor(PlanetDef planet) => planet.SolarMultiplier * DayFactor * WeatherSolarMultiplier;

        public void Tick(float dt)
        {
            Time += dt;
            ChangeIn -= dt;
            if (ChangeIn > 0f) return;

            float total = 0f;
            for (int i = 0; i < _planet.WeatherWeights.Length; i++) total += _planet.WeatherWeights[i];
            float roll = _rng.NextFloat() * total;
            float acc = 0f;
            for (int i = 0; i < _planet.WeatherPool.Length; i++)
            {
                acc += _planet.WeatherWeights[i];
                if (roll <= acc) { Current = _planet.WeatherPool[i]; break; }
            }
            ChangeIn = _rng.Range(70f, 190f);
        }
    }
}
