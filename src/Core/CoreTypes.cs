// ============================================================================
//  Core Factory: Infinite Horizon —— Core 層（純 C#，不引用任何 Godot 型別）
//  這一層可以獨立單元測試、可以獨立壓測、可以整塊搬去 GDExtension。
//  數值與配方全部是可變的靜態欄位 → 可由 data/core.json 覆蓋（改平衡不用改程式）。
// ============================================================================
using System;
using System.Collections.Generic;

namespace CoreFactory.Core
{
    public enum Lang : byte { ZH = 0, TW = 1, EN = 2, JA = 3 }

    public enum ItemId : byte
    {
        None = 0, IronOre = 1, CopperOre = 2, Coal = 3, Stone = 4,
        IronPlate = 5, CopperPlate = 6, Gear = 7, Circuit = 8, Science = 9, Ammo = 10,
        TitaniumOre = 11, TitaniumPlate = 12,
        // ── 第二顆星球之後才有的物質 ──
        SiliconOre = 13, SiliconWafer = 14,     // 矽礦 → 矽晶圓（翡翠七星）
        CrystalOre = 15, Crystal = 16,          // 晶石礦 → 晶體（霜蝕四號）
        GoldOre = 17, GoldPlate = 18,           // 金礦 → 金板
        CoreOre = 19, CoreCrystal = 20,         // 星核礦 → 星核晶（深淵零號，最貴）
        AdvancedCircuit = 21,                   // 高級電路：電路 + 矽晶圓
        HeavyAmmo = 22,                         // 穿甲彈：砲彈 + 晶體
    }

    public enum BuildingKind : byte
    {
        None = 0, Miner = 1, Belt = 2, Smelter = 3, Assembler = 4, Depot = 5,
        SolarPanel = 6, ThermalGenerator = 7, Battery = 8,
        Turret = 9, Lamp = 10,
        SensorNode = 11, ComparatorNode = 12, ActuatorNode = 13,
    }

    public enum Dir : byte { North = 0, East = 1, South = 2, West = 3 }

    public enum WeatherKind : byte { Clear = 0, Cloudy = 1, Storm = 2, Ash = 3, Ion = 4 }

    /// <summary>蟲潮種類：不同數值與外觀，讓防禦不再單調。</summary>
    public enum AlienKind : byte
    {
        Grunt = 0,    // 潮蟲：基礎炮灰
        Runner = 1,   // 迅蟲：高速低血，直衝產線
        Spitter = 2,  // 毒液蟲：中速中血，酸液啃建築
        Brute = 3,    // 重甲蟲：低速高血高攻，扛塔主力
        Elite = 4,    // 巢主：後期菁英，接近即重創
        Bomber = 5,   // 自爆蟲：衝到建築旁自爆，單次傷害極高
        Flyer = 6,    // 飛行蟲：速度最快、血薄，繞過防線直搗產線
        Frost = 7,    // 霜蝕蟲：高血中速，冰甲能扛砲彈
        Magma = 8,    // 熔岩蟲：超高血高攻，需要穿甲彈才咬得動
        Titan = 9,    // 星核獸：終局 BOSS 級，只有重火力能擋
    }

    public static class Dirs
    {
        public static int Dx(Dir d) => d == Dir.East ? 1 : (d == Dir.West ? -1 : 0);
        public static int Dz(Dir d) => d == Dir.South ? 1 : (d == Dir.North ? -1 : 0);
        public static Dir Opposite(Dir d) => (Dir)(((int)d + 2) & 3);
        public static Dir Rotate(Dir d, int steps) => (Dir)(((int)d + steps) & 3);
    }

    public struct Recipe
    {
        public ItemId In0; public byte N0;
        public ItemId In1; public byte N1;
        public ItemId Out; public byte NOut;
        public float Seconds;

        public Recipe(ItemId i0, byte n0, ItemId i1, byte n1, ItemId o, byte no, float s)
        { In0 = i0; N0 = n0; In1 = i1; N1 = n1; Out = o; NOut = no; Seconds = s; }
    }

    /// <summary>建築實例。用 class 而非 struct：字典儲存 + 欄位可變，讀寫最直接。</summary>
    public sealed class Building
    {
        public BuildingKind Kind;
        public int X, Z;
        public Dir Facing;

        // 生產
        public float Progress;
        public bool Producing;
        public byte In0Kind, In0Count, In1Kind, In1Count;
        public byte OutKind, OutCount;
        public byte RecipeIndex;
        public bool Blocked;          // 出貨被堵住（工廠體檢用）

        // 能源
        public float Energy;

        // 戰鬥
        public byte Ammo;
        public float Hp = 100f;
        public float Cooldown;

        // 邏輯
        public bool Enabled = true;
        public int SignalOut;
        public int Threshold = 50;
        public byte ChannelIn, ChannelOut;
        public int LinkKey = -1;
        public int KindValue;

        public bool IsCrafter => Kind == BuildingKind.Smelter || Kind == BuildingKind.Assembler;
        public bool IsLogic => Kind == BuildingKind.SensorNode || Kind == BuildingKind.ComparatorNode || Kind == BuildingKind.ActuatorNode;
    }

    /// <summary>傳送帶格：Head 是最靠出口的物品（pos 最大），Tail 最靠入口（pos 最小）。</summary>
    public sealed class BeltTile
    {
        public int X, Z;
        public Dir Facing;
        public int Head = -1, Tail = -1;
        public int Count;
    }

    public sealed class Alien
    {
        public float X, Z;
        public float Hp;
        public float Speed;
        public float Damage;
        public float Cooldown;
        public bool Alive = true;
        /// <summary>面向（模型正面 = -Z，yaw = atan2(-dx, -dz)）；給走路動畫用。</summary>
        public float Yaw;
        /// <summary>走路動畫相位（由渲染層累加，模擬層不管）。</summary>
        public float WalkPhase;
        /// <summary>這一 tick 是否在移動（動畫用）。</summary>
        public bool Moving;
        /// <summary>種類（決定數值與外觀）。</summary>
        public AlienKind Kind;
        /// <summary>視覺半徑（格）：渲染層畫身體大小用。</summary>
        public float Radius = 0.5f;
    }

    /// <summary>蟲潮數值表（基礎值；實際會再乘上星球威脅倍率與波次成長）。</summary>
    public static class AlienDefs
    {
        public static (float hp, float speed, float damage, float radius) Base(AlienKind k) => k switch
        {
            AlienKind.Runner  => (16f, 3.9f,  6f, 0.38f),
            AlienKind.Spitter => (55f, 1.7f, 14f, 0.55f),
            AlienKind.Brute   => (130f, 1.15f, 24f, 0.80f),
            AlienKind.Elite   => (280f, 1.45f, 32f, 0.92f),
            AlienKind.Bomber  => (48f, 2.6f, 46f, 0.60f),   // 自爆：血不多但炸一下很痛
            AlienKind.Flyer   => (30f, 4.8f, 11f, 0.42f),   // 飛行：最快，專鑽漏洞
            AlienKind.Frost   => (95f, 1.35f, 18f, 0.70f),  // 霜蝕：冰甲厚
            AlienKind.Magma   => (210f, 1.5f, 34f, 0.88f),  // 熔岩：厚血高攻
            AlienKind.Titan   => (620f, 1.25f, 60f, 1.25f), // 星核獸：BOSS
            _                 => (30f, 2.1f,  9f, 0.50f),
        };
    }

    /// <summary>所有可調數值集中在此；ContentJson 會覆蓋它們。</summary>
    public static class GameData
    {
        /// <summary>物品槽數量（物品 id 上限）。Hub / Signals 陣列大小。</summary>
        public const int ItemSlots = 24;
        // ── 傳送帶 ────────────────────────────────────────────────
        public const float ItemSize = 0.125f;      // 每格最多 8 個物品（結構常數，不開放調整）
        public static float BeltSpeed = 1.2f;      // 格／秒（基準值）

        // ── 生產 ──────────────────────────────────────────────────
        public static float MinerSeconds = 1.0f;

        // ── 能源 ──────────────────────────────────────────────────
        public static float SolarPower = 60f;
        public static float ThermalPower = 220f;
        public static float BatteryCapacity = 6000f;

        // ── 戰鬥 ──────────────────────────────────────────────────
        public static float TurretRange = 10f;
        public static float TurretInterval = 0.55f;
        public static float TurretDamage = 12f;
        public static byte TurretMagazine = 30;    // 砲彈匣容量（一次從帶上補滿的量）
        public static byte TurretStartAmmo = 20;   // 新建砲塔免費附贈的彈藥（新手不會一開始就沒子彈）

        // ── 玩家起始庫存 ──────────────────────────────────────────
        public static (ItemId item, int count)[] StartStock =
        {
            (ItemId.IronPlate, 200), (ItemId.CopperPlate, 150),
            (ItemId.Gear, 60), (ItemId.Circuit, 40), (ItemId.Ammo, 200),
            (ItemId.Coal, 200), (ItemId.Science, 20),
        };

        // ── 金幣經濟：礦物→賣錢→蓋建築 ──────────────────────────
        public static long StartCoins = 2000;

        public static readonly Dictionary<BuildingKind, long> CoinCosts = DefaultCoinCosts();

        private static Dictionary<BuildingKind, long> DefaultCoinCosts() => new()
        {
            [BuildingKind.Belt] = 10,
            [BuildingKind.Miner] = 120,
            [BuildingKind.Smelter] = 180,
            [BuildingKind.Assembler] = 260,
            [BuildingKind.Depot] = 300,
            [BuildingKind.SolarPanel] = 180,
            [BuildingKind.ThermalGenerator] = 240,
            [BuildingKind.Battery] = 340,
            [BuildingKind.Turret] = 360,
            [BuildingKind.Lamp] = 25,
            [BuildingKind.SensorNode] = 80,
            [BuildingKind.ComparatorNode] = 120,
            [BuildingKind.ActuatorNode] = 120,
        };

        public static long CoinCost(BuildingKind k) => CoinCosts.TryGetValue(k, out long v) ? v : 0;

        /// <summary>每件物品賣出的金幣（在交易面板變現）。</summary>
        public static int CoinValue(ItemId i) => i switch
        {
            ItemId.IronOre => 6, ItemId.CopperOre => 8, ItemId.Coal => 4, ItemId.Stone => 2,
            ItemId.IronPlate => 20, ItemId.CopperPlate => 26, ItemId.Gear => 45, ItemId.Circuit => 70,
            ItemId.Science => 90, ItemId.Ammo => 18,
            ItemId.TitaniumOre => 30, ItemId.TitaniumPlate => 95,
            ItemId.SiliconOre => 14, ItemId.SiliconWafer => 46,
            ItemId.CrystalOre => 48, ItemId.Crystal => 135,
            ItemId.GoldOre => 62, ItemId.GoldPlate => 170,
            ItemId.CoreOre => 140, ItemId.CoreCrystal => 430,
            ItemId.AdvancedCircuit => 240, ItemId.HeavyAmmo => 66,
            _ => 1,
        };

        public static Recipe[] SmelterRecipes = DefaultSmelterRecipes();
        public static Recipe[] AssemblerRecipes = DefaultAssemblerRecipes();

        public static readonly Dictionary<BuildingKind, (ItemId item, int count)[]> Costs = DefaultCosts();
        public static readonly Dictionary<BuildingKind, float> PowerUse = DefaultPower();

        public static Recipe[] DefaultSmelterRecipes() => new[]
        {
            new Recipe(ItemId.IronOre,   1, ItemId.None, 0, ItemId.IronPlate,   1, 1.8f),
            new Recipe(ItemId.CopperOre, 1, ItemId.None, 0, ItemId.CopperPlate, 1, 1.8f),
            new Recipe(ItemId.TitaniumOre, 1, ItemId.None, 0, ItemId.TitaniumPlate, 1, 2.5f),
            new Recipe(ItemId.SiliconOre, 1, ItemId.None, 0, ItemId.SiliconWafer, 1, 2.2f),
            new Recipe(ItemId.CrystalOre, 1, ItemId.None, 0, ItemId.Crystal,      1, 2.6f),
            new Recipe(ItemId.GoldOre,    1, ItemId.None, 0, ItemId.GoldPlate,    1, 3.0f),
            new Recipe(ItemId.CoreOre,    1, ItemId.None, 0, ItemId.CoreCrystal,  1, 4.0f),
        };

        public static Recipe[] DefaultAssemblerRecipes() => new[]
        {
            new Recipe(ItemId.IronPlate,   3, ItemId.None,        0, ItemId.Gear,    1, 1.6f),
            new Recipe(ItemId.CopperPlate, 2, ItemId.None,        0, ItemId.Circuit, 1, 2.0f),
            new Recipe(ItemId.Gear,        1, ItemId.CopperPlate, 1, ItemId.Ammo,    2, 2.0f),
            new Recipe(ItemId.Gear,        1, ItemId.Circuit,     1, ItemId.Science, 1, 4.0f),
            new Recipe(ItemId.Circuit,     1, ItemId.SiliconWafer, 1, ItemId.AdvancedCircuit, 1, 3.2f),
            new Recipe(ItemId.Ammo,        1, ItemId.Crystal,     1, ItemId.HeavyAmmo, 2, 2.8f),
        };

        private static Dictionary<BuildingKind, (ItemId, int)[]> DefaultCosts() => new()
        {
            [BuildingKind.Belt] = new[] { (ItemId.IronPlate, 1) },
            [BuildingKind.Miner] = new[] { (ItemId.IronPlate, 10), (ItemId.Gear, 5) },
            [BuildingKind.Smelter] = new[] { (ItemId.IronPlate, 12), (ItemId.Gear, 4) },
            [BuildingKind.Assembler] = new[] { (ItemId.IronPlate, 10), (ItemId.Gear, 8), (ItemId.Circuit, 4) },
            [BuildingKind.Depot] = new[] { (ItemId.IronPlate, 20) },
            [BuildingKind.SolarPanel] = new[] { (ItemId.CopperPlate, 12), (ItemId.Circuit, 6) },
            [BuildingKind.ThermalGenerator] = new[] { (ItemId.IronPlate, 20), (ItemId.Gear, 10) },
            [BuildingKind.Battery] = new[] { (ItemId.CopperPlate, 20), (ItemId.Circuit, 10) },
            [BuildingKind.Turret] = new[] { (ItemId.IronPlate, 25), (ItemId.Gear, 10) },
            [BuildingKind.Lamp] = new[] { (ItemId.CopperPlate, 2) },
            [BuildingKind.SensorNode] = new[] { (ItemId.CopperPlate, 5), (ItemId.Circuit, 2) },
            [BuildingKind.ComparatorNode] = new[] { (ItemId.CopperPlate, 5), (ItemId.Circuit, 4) },
            [BuildingKind.ActuatorNode] = new[] { (ItemId.CopperPlate, 5), (ItemId.Circuit, 4) },
        };

        private static Dictionary<BuildingKind, float> DefaultPower() => new()
        {
            [BuildingKind.Miner] = 30f,
            [BuildingKind.Smelter] = 70f,
            [BuildingKind.Assembler] = 50f,
            [BuildingKind.Turret] = 25f,
            [BuildingKind.Lamp] = 5f,
            [BuildingKind.Depot] = 5f,
            [BuildingKind.SensorNode] = 2f,
            [BuildingKind.ComparatorNode] = 2f,
            [BuildingKind.ActuatorNode] = 2f,
        };

        /// <summary>回到出廠預設（讀取新資料包前先呼叫）。</summary>
        public static void ResetDefaults()
        {
            BeltSpeed = 1.2f;
            MinerSeconds = 1.0f;
            SolarPower = 60f;
            ThermalPower = 220f;
            BatteryCapacity = 6000f;
            TurretRange = 10f;
            TurretInterval = 0.55f;
            TurretDamage = 12f;
            SmelterRecipes = DefaultSmelterRecipes();
            AssemblerRecipes = DefaultAssemblerRecipes();

            Costs.Clear();
            foreach (var kv in DefaultCosts()) Costs[kv.Key] = kv.Value;
            PowerUse.Clear();
            foreach (var kv in DefaultPower()) PowerUse[kv.Key] = kv.Value;
        }

        public static int RecipeCount(BuildingKind k)
            => k == BuildingKind.Smelter ? SmelterRecipes.Length
             : k == BuildingKind.Assembler ? AssemblerRecipes.Length : 0;

        public static Recipe GetRecipe(Building b)
        {
            if (b.Kind == BuildingKind.Smelter && SmelterRecipes.Length > 0)
                return SmelterRecipes[b.RecipeIndex % SmelterRecipes.Length];
            if (b.Kind == BuildingKind.Assembler && AssemblerRecipes.Length > 0)
                return AssemblerRecipes[b.RecipeIndex % AssemblerRecipes.Length];
            return default;
        }

        public static float PowerDemand(BuildingKind k)
            => PowerUse.TryGetValue(k, out float v) ? v : 0f;

        public static (ItemId item, int count)[] Cost(BuildingKind k)
            => Costs.TryGetValue(k, out var c) ? c : Array.Empty<(ItemId, int)>();
    }
}
