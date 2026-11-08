// 確定性隨機、雜湊、地形與星球定義（全部是 seed 的純函式 → 這就是「無限地圖」與「種子存檔」的基礎）
using System;

namespace CoreFactory.Core
{
    /// <summary>xorshift64*，固定序列、無平台差異，保證模擬可重現。</summary>
    public struct Rng
    {
        private ulong _s;
        public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public uint NextUInt()
        {
            _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
            return (uint)(_s >> 32);
        }
        public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);
        public int NextInt(int max) => max <= 0 ? 0 : (int)(NextUInt() % (uint)max);
        public float Range(float a, float b) => a + (b - a) * NextFloat();
    }

    public static class Hash2D
    {
        public static uint Hash(int x, int z, uint seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(z * 668265263) + seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }
        public static float Value01(int x, int z, uint seed) => Hash(x, z, seed) * (1.0f / 4294967296.0f);
    }

    /// <summary>平滑值雜訊（value noise）。地形高度、礦脈分佈都由此決定，不儲存。</summary>
    public sealed class Terrain
    {
        public readonly int Seed;
        public readonly PlanetDef Planet;
        private readonly uint _s;
        private readonly uint _s2;
        private readonly uint _s3;

        public Terrain(int seed, PlanetDef planet)
        {
            Seed = seed; Planet = planet; _s = (uint)seed * 2654435761u;
            _s2 = _s ^ 0x9E3779B9u; _s3 = _s ^ 0x85EBCA6Bu;
        }

        private float Lattice(int x, int z, uint salt)
            => Hash2D.Value01(x, z, _s ^ salt) * 2f - 1f;

        public float Noise(float x, float z, float freq, uint salt)
        {
            float fx = x * freq, fz = z * freq;
            int x0 = (int)MathF.Floor(fx), z0 = (int)MathF.Floor(fz);
            float tx = fx - x0, tz = fz - z0;
            tx = tx * tx * (3f - 2f * tx); tz = tz * tz * (3f - 2f * tz);
            float a = Lattice(x0, z0, salt), b = Lattice(x0 + 1, z0, salt);
            float c = Lattice(x0, z0 + 1, salt), d = Lattice(x0 + 1, z0 + 1, salt);
            float top = a + (b - a) * tx;
            float bot = c + (d - c) * tx;
            return top + (bot - top) * tz;
        }

        public float HeightAt(int x, int z)
        {
            float h = Noise(x, z, 0.012f, 1u) * Planet.Amplitude;
            h += Noise(x, z, 0.05f, 2u) * Planet.Amplitude * 0.25f;
            return Planet.BaseHeight + h;
        }

        /// <summary>該格有沒有礦、是什麼礦、豐富度（純函式，可隨時重算 → 不需存檔）。</summary>
        public bool ResourceAt(int x, int z, out ItemId item, out int richness)
        {
            item = ItemId.None; richness = 0;

            // 大尺度「豐度場」：決定哪些區域有礦（低頻 → 有礦區與無礦區分明）
            float field = Noise(x, z, 0.035f, 7u);
            if (field < Planet.ResourceThreshold) return false;

            // 小尺度「礦脈形狀」：把整片豐度場切成有間隙的小礦脈，避免一整片黏在一起。
            // 越富的區域閾值越低（礦脈越連續），邊緣越稀疏 —— 產生自然散佈的礦脈群。
            float vein = Noise(x, z, 0.13f, 11u);
            float gate = 0.62f - 0.25f * (field - Planet.ResourceThreshold) / (1f - Planet.ResourceThreshold);
            if (vein < gate) return false;

            float pick = Hash2D.Value01(x * 31 + 7, z * 17 - 3, _s2);
            item = Planet.PickOre(pick);
            if (item == ItemId.None) return false;

            float amount = (field - Planet.ResourceThreshold) / (1f - Planet.ResourceThreshold);
            richness = 1 + (int)(amount * Planet.ResourceRichness);
            return true;
        }
    }

    public sealed class PlanetDef
    {
        public string NameKey;
        public float BaseHeight;
        public float Amplitude;
        public float ResourceThreshold;
        public int ResourceRichness;
        public float SolarMultiplier;
        public float ThreatScale;
        public int ThreatFirstWaveSeconds;
        public WeatherKind[] WeatherPool;
        public float[] WeatherWeights;
        public (ItemId item, float weight)[] Ores;
        public float R, G, B;              // 地表色（低配用純色 + 網格線）
        public AlienKind[] AlienPool;      // 這顆星球會出現的蟲種（每顆星各有自己的蟲）
        public float ThreatHpMult = 1f;    // 星球蟲潮血量倍率
        public float ThreatSpeedMult = 1f;
        public float ThreatDmgMult = 1f;
        public long PortalCost;            // 解鎖/建造傳送門前往此星球的金幣價（0 = 起始星球）
        public float PriceMultiplier = 1f; // 此星球礦物的賣價倍率（越遠的星球礦越貴）
        public float CostMultiplier = 1f;  // 此星球建築造價倍率（越遠的星球蓋東西越貴）
        public string DescKey;             // 星球簡介的地化鍵

        public ItemId PickOre(float roll)
        {
            float total = 0f;
            for (int i = 0; i < Ores.Length; i++) total += Ores[i].weight;
            float acc = 0f;
            for (int i = 0; i < Ores.Length; i++)
            {
                acc += Ores[i].weight;
                if (roll * total <= acc) return Ores[i].item;
            }
            return Ores.Length > 0 ? Ores[0].item : ItemId.None;
        }
    }

    public static class Planets
    {
        /// <summary>星球 1：苔原邊境——日照充足、天氣溫和，適合開局教學。</summary>
        public static readonly PlanetDef Xenon1 = new PlanetDef
        {
            NameKey = "planet.1",
            BaseHeight = 0f, Amplitude = 3.2f,
            ResourceThreshold = 0.42f, ResourceRichness = 6,
            SolarMultiplier = 1.0f, ThreatScale = 1.0f, ThreatFirstWaveSeconds = 240,
            WeatherPool = new[] { WeatherKind.Clear, WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Storm },
            WeatherWeights = new[] { 0.45f, 0.25f, 0.20f, 0.10f },
            Ores = new[] { (ItemId.IronOre, 3.0f), (ItemId.CopperOre, 2.0f), (ItemId.Coal, 1.5f), (ItemId.Stone, 1.0f) },
            AlienPool = new[] { AlienKind.Grunt, AlienKind.Runner, AlienKind.Spitter },
            ThreatHpMult = 1f, ThreatSpeedMult = 1f, ThreatDmgMult = 1f,
            PortalCost = 0, PriceMultiplier = 1f, CostMultiplier = 1f,
            DescKey = "planet.desc.1",
            R = 0.30f, G = 0.38f, B = 0.30f,
        };

        /// <summary>星球 2：灰燼邊境——太陽能幾乎無用（逼你用火力／電池＋邏輯電路）、風暴頻繁、蟲潮更兇，但有稀有礦。</summary>
        public static readonly PlanetDef Cinder9 = new PlanetDef
        {
            NameKey = "planet.2",
            BaseHeight = 0.8f, Amplitude = 5.5f,
            ResourceThreshold = 0.38f, ResourceRichness = 9,
            SolarMultiplier = 0.35f, ThreatScale = 1.9f, ThreatFirstWaveSeconds = 150,
            WeatherPool = new[] { WeatherKind.Cloudy, WeatherKind.Ash, WeatherKind.Ash, WeatherKind.Ion, WeatherKind.Storm },
            WeatherWeights = new[] { 0.20f, 0.30f, 0.20f, 0.15f, 0.15f },
            Ores = new[] { (ItemId.IronOre, 2.5f), (ItemId.CopperOre, 2.5f), (ItemId.Coal, 3.0f), (ItemId.Stone, 1.5f), (ItemId.TitaniumOre, 1.2f) },
            AlienPool = new[] { AlienKind.Grunt, AlienKind.Runner, AlienKind.Spitter, AlienKind.Brute, AlienKind.Elite },
            ThreatHpMult = 1.4f, ThreatSpeedMult = 1.0f, ThreatDmgMult = 1.25f,
            PortalCost = 8000, PriceMultiplier = 1.35f, CostMultiplier = 1.15f,
            DescKey = "planet.desc.2",
            R = 0.34f, G = 0.24f, B = 0.22f,
        };

        /// <summary>星球 3：翡翠七星 · 矽晶沼澤——矽礦產地，可升級出高級電路；飛行蟲與自爆蟲開始出現。</summary>
        public static readonly PlanetDef Viridian7 = new PlanetDef
        {
            NameKey = "planet.3",
            BaseHeight = -0.4f, Amplitude = 4.0f,
            ResourceThreshold = 0.36f, ResourceRichness = 10,
            SolarMultiplier = 0.8f, ThreatScale = 2.6f, ThreatFirstWaveSeconds = 120,
            WeatherPool = new[] { WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Storm, WeatherKind.Ion },
            WeatherWeights = new[] { 0.30f, 0.28f, 0.27f, 0.15f },
            Ores = new[] { (ItemId.IronOre, 2.0f), (ItemId.CopperOre, 2.0f), (ItemId.Coal, 1.6f),
                           (ItemId.Stone, 1.0f), (ItemId.SiliconOre, 2.2f), (ItemId.TitaniumOre, 0.6f) },
            AlienPool = new[] { AlienKind.Runner, AlienKind.Spitter, AlienKind.Bomber, AlienKind.Flyer, AlienKind.Brute },
            ThreatHpMult = 1.8f, ThreatSpeedMult = 1.1f, ThreatDmgMult = 1.4f,
            PortalCost = 25000, PriceMultiplier = 1.8f, CostMultiplier = 1.3f,
            DescKey = "planet.desc.3",
            R = 0.16f, G = 0.36f, B = 0.30f,
        };

        /// <summary>星球 4：霜蝕四號 · 冰晶荒原——日照極弱（要靠火力/電池），產晶石與金礦，霜蝕蟲冰甲極厚。</summary>
        public static readonly PlanetDef Glacia4 = new PlanetDef
        {
            NameKey = "planet.4",
            BaseHeight = 1.2f, Amplitude = 6.0f,
            ResourceThreshold = 0.34f, ResourceRichness = 12,
            SolarMultiplier = 0.5f, ThreatScale = 3.4f, ThreatFirstWaveSeconds = 100,
            WeatherPool = new[] { WeatherKind.Cloudy, WeatherKind.Storm, WeatherKind.Ion, WeatherKind.Ash },
            WeatherWeights = new[] { 0.30f, 0.30f, 0.22f, 0.18f },
            Ores = new[] { (ItemId.IronOre, 1.6f), (ItemId.Coal, 2.0f), (ItemId.Stone, 1.2f),
                           (ItemId.CrystalOre, 2.0f), (ItemId.GoldOre, 1.6f), (ItemId.SiliconOre, 0.8f) },
            AlienPool = new[] { AlienKind.Grunt, AlienKind.Frost, AlienKind.Brute, AlienKind.Bomber, AlienKind.Elite },
            ThreatHpMult = 2.4f, ThreatSpeedMult = 1.0f, ThreatDmgMult = 1.6f,
            PortalCost = 70000, PriceMultiplier = 2.4f, CostMultiplier = 1.5f,
            DescKey = "planet.desc.4",
            R = 0.28f, G = 0.40f, B = 0.52f,
        };

        /// <summary>星球 5：深淵零號 · 星核遺骸——終局星球，產星核晶（最貴），但只有重火力擋得住星核獸。</summary>
        public static readonly PlanetDef Nyx0 = new PlanetDef
        {
            NameKey = "planet.5",
            BaseHeight = 2.0f, Amplitude = 7.5f,
            ResourceThreshold = 0.30f, ResourceRichness = 14,
            SolarMultiplier = 0.25f, ThreatScale = 4.5f, ThreatFirstWaveSeconds = 80,
            WeatherPool = new[] { WeatherKind.Ash, WeatherKind.Ion, WeatherKind.Storm, WeatherKind.Ash },
            WeatherWeights = new[] { 0.35f, 0.30f, 0.20f, 0.15f },
            Ores = new[] { (ItemId.TitaniumOre, 1.8f), (ItemId.GoldOre, 1.6f), (ItemId.CrystalOre, 1.6f),
                           (ItemId.CoreOre, 1.4f), (ItemId.IronOre, 1.0f), (ItemId.Coal, 1.0f) },
            AlienPool = new[] { AlienKind.Elite, AlienKind.Magma, AlienKind.Titan, AlienKind.Brute, AlienKind.Frost },
            ThreatHpMult = 3.2f, ThreatSpeedMult = 1.15f, ThreatDmgMult = 2.0f,
            PortalCost = 180000, PriceMultiplier = 3.2f, CostMultiplier = 1.8f,
            DescKey = "planet.desc.5",
            R = 0.20f, G = 0.14f, B = 0.28f,
        };

        public static readonly PlanetDef[] All = { Xenon1, Cinder9, Viridian7, Glacia4, Nyx0 };
        public static PlanetDef Get(int index) => All[((index % All.Length) + All.Length) % All.Length];
    }
}
