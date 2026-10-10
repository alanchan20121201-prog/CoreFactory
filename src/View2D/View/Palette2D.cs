// 2D 配色表：每個物品 / 建築 / 蟲種都有「主色 + 暗部 + 亮部」，供 Art2D 畫出有立體感的向量精靈。
// 方向語言沿用 3D 版鎖定：出貨 = 綠、進料 = 藍。
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class Palette2D
    {
        // ── 全域 ────────────────────────────────────────────────
        public static readonly Color Background = new("10141c");
        public static readonly Color GridLine = new(1f, 1f, 1f, 0.045f);
        public static readonly Color ChunkLine = new(1f, 0.85f, 0.3f, 0.30f);
        public static readonly Color CursorOk = new(0.40f, 1.00f, 0.50f, 0.95f);
        public static readonly Color CursorBad = new(1.00f, 0.35f, 0.30f, 0.95f);
        public static readonly Color ArrowOut = new(0.25f, 0.95f, 0.35f);   // 出貨方向 = 綠
        public static readonly Color ArrowIn = new(0.30f, 0.60f, 1.00f);     // 進料方向 = 藍

        public static readonly Color BeltBase = new(0.18f, 0.19f, 0.23f);
        public static readonly Color BeltBaseDark = new(0.13f, 0.14f, 0.17f);
        public static readonly Color BeltChevron = new(0.44f, 0.47f, 0.55f);
        public static readonly Color BeltEdge = new(0.05f, 0.05f, 0.07f, 0.7f);
        public static readonly Color BuildingEdge = new(0.02f, 0.02f, 0.04f, 0.65f);
        public static readonly Color DisabledVeil = new(0f, 0f, 0f, 0.45f);
        public static readonly Color Coin = new("ffd75a");

        // 地形裝飾
        public static readonly Color Rock = new(0.42f, 0.44f, 0.50f);
        public static readonly Color RockDark = new(0.28f, 0.30f, 0.35f);
        public static readonly Color Grass = new(0.30f, 0.55f, 0.32f);
        public static readonly Color Crystal = new(0.35f, 0.85f, 0.85f);

        // ── 物品（主色 / 暗部 / 亮部）───────────────────────────
        public static Color Item(ItemId i) => i switch
        {
            ItemId.IronOre => new Color("8a6a4a"),
            ItemId.CopperOre => new Color("c8762e"),
            ItemId.Coal => new Color("33333c"),
            ItemId.Stone => new Color("7a7a84"),
            ItemId.IronPlate => new Color("b8c0c8"),
            ItemId.CopperPlate => new Color("d8893a"),
            ItemId.Gear => new Color("8a929a"),
            ItemId.Circuit => new Color("3aa06a"),
            ItemId.Science => new Color("8a5ac8"),
            ItemId.Ammo => new Color("c8483a"),
            ItemId.TitaniumOre => new Color("7a8a9a"),
            ItemId.TitaniumPlate => new Color("c0d0e0"),
            ItemId.SiliconOre => new Color("6f8a7a"),
            ItemId.SiliconWafer => new Color("9ac8e0"),
            ItemId.CrystalOre => new Color("6aa8c8"),
            ItemId.Crystal => new Color("7ce8f8"),
            ItemId.GoldOre => new Color("c8a020"),
            ItemId.GoldPlate => new Color("f0c840"),
            ItemId.CoreOre => new Color("8858c8"),
            ItemId.CoreCrystal => new Color("c070f8"),
            ItemId.AdvancedCircuit => new Color("2ec8a0"),
            ItemId.HeavyAmmo => new Color("e06820"),
            _ => new Color("ffffff"),
        };

        public static Color ItemDark(ItemId i) => i switch
        {
            ItemId.IronOre => new Color("5e452e"),
            ItemId.CopperOre => new Color("8a4f1c"),
            ItemId.Coal => new Color("1c1c22"),
            ItemId.Stone => new Color("55555e"),
            ItemId.IronPlate => new Color("7e868e"),
            ItemId.CopperPlate => new Color("a85e1e"),
            ItemId.Gear => new Color("5e646c"),
            ItemId.Circuit => new Color("286e46"),
            ItemId.Science => new Color("5e3a8a"),
            ItemId.Ammo => new Color("8a2e24"),
            ItemId.TitaniumOre => new Color("52606e"),
            ItemId.TitaniumPlate => new Color("8a9aa8"),
            ItemId.SiliconOre => new Color("48604f"),
            ItemId.SiliconWafer => new Color("5a8aa0"),
            ItemId.CrystalOre => new Color("3a6a88"),
            ItemId.Crystal => new Color("3aa0b8"),
            ItemId.GoldOre => new Color("8a6c10"),
            ItemId.GoldPlate => new Color("a88c20"),
            ItemId.CoreOre => new Color("54308a"),
            ItemId.CoreCrystal => new Color("7a3aa8"),
            ItemId.AdvancedCircuit => new Color("1a7a62"),
            ItemId.HeavyAmmo => new Color("98280a"),
            _ => new Color("888888"),
        };

        public static Color ItemAccent(ItemId i) => i switch
        {
            ItemId.IronOre => new Color("b8926a"),
            ItemId.CopperOre => new Color("e8a050"),
            ItemId.Coal => new Color("4a4a56"),
            ItemId.Stone => new Color("9a9aa6"),
            ItemId.IronPlate => new Color("e8f0f4"),
            ItemId.CopperPlate => new Color("f0b068"),
            ItemId.Gear => new Color("b8c0c8"),
            ItemId.Circuit => new Color("6ae09a"),
            ItemId.Science => new Color("b88ae8"),
            ItemId.Ammo => new Color("e8786a"),
            ItemId.TitaniumOre => new Color("a0b4c8"),
            ItemId.TitaniumPlate => new Color("f0f6fc"),
            ItemId.SiliconOre => new Color("9ac0a8"),
            ItemId.SiliconWafer => new Color("d8f4ff"),
            ItemId.CrystalOre => new Color("a0d8f0"),
            ItemId.Crystal => new Color("d0fbff"),
            ItemId.GoldOre => new Color("f0d860"),
            ItemId.GoldPlate => new Color("ffe888"),
            ItemId.CoreOre => new Color("b090e8"),
            ItemId.CoreCrystal => new Color("e8b0ff"),
            ItemId.AdvancedCircuit => new Color("7cf0d0"),
            ItemId.HeavyAmmo => new Color("ffa858"),
            _ => new Color("ffffff"),
        };

        // ── 建築（主色 / 暗部 / 亮部 / 圖標色）──────────────────
        public static Color Building(BuildingKind k) => k switch
        {
            BuildingKind.Miner => new Color("c8a050"),
            BuildingKind.Smelter => new Color("b0553a"),
            BuildingKind.Assembler => new Color("5a7fb0"),
            BuildingKind.Depot => new Color("b8b8c2"),
            BuildingKind.SolarPanel => new Color("2a5a9a"),
            BuildingKind.ThermalGenerator => new Color("8a4a3a"),
            BuildingKind.Battery => new Color("4a9a4a"),
            BuildingKind.Turret => new Color("8a3a3a"),
            BuildingKind.Lamp => new Color("d8c060"),
            BuildingKind.SensorNode => new Color("3a8a86"),
            BuildingKind.ComparatorNode => new Color("8a4aa8"),
            BuildingKind.ActuatorNode => new Color("a86e3a"),
            _ => new Color("666666"),
        };

        public static Color BuildingDark(BuildingKind k) => k switch
        {
            BuildingKind.Miner => new Color("8a6a30"),
            BuildingKind.Smelter => new Color("7a3624"),
            BuildingKind.Assembler => new Color("3a5678"),
            BuildingKind.Depot => new Color("80808a"),
            BuildingKind.SolarPanel => new Color("1a3a64"),
            BuildingKind.ThermalGenerator => new Color("5e2e22"),
            BuildingKind.Battery => new Color("2e682e"),
            BuildingKind.Turret => new Color("5e2424"),
            BuildingKind.Lamp => new Color("987c38"),
            BuildingKind.SensorNode => new Color("265e5c"),
            BuildingKind.ComparatorNode => new Color("5e2e74"),
            BuildingKind.ActuatorNode => new Color("744822"),
            _ => new Color("444444"),
        };

        public static Color BuildingAccent(BuildingKind k) => k switch
        {
            BuildingKind.Miner => new Color("e8c878"),
            BuildingKind.Smelter => new Color("d87858"),
            BuildingKind.Assembler => new Color("84a8d8"),
            BuildingKind.Depot => new Color("eaeaf0"),
            BuildingKind.SolarPanel => new Color("4a8ad0"),
            BuildingKind.ThermalGenerator => new Color("b06a52"),
            BuildingKind.Battery => new Color("70c070"),
            BuildingKind.Turret => new Color("b05a5a"),
            BuildingKind.Lamp => new Color("f8e888"),
            BuildingKind.SensorNode => new Color("5ac0ba"),
            BuildingKind.ComparatorNode => new Color("b068d0"),
            BuildingKind.ActuatorNode => new Color("d09058"),
            _ => new Color("888888"),
        };

        public static Color BuildingGlyph(BuildingKind k) => k switch
        {
            BuildingKind.Miner => new Color("3e2c12"),
            BuildingKind.Smelter => new Color("ffd060"),
            BuildingKind.Assembler => new Color("e8f2fc"),
            BuildingKind.Depot => new Color("56565e"),
            BuildingKind.SolarPanel => new Color("9ad8ff"),
            BuildingKind.ThermalGenerator => new Color("ff9840"),
            BuildingKind.Battery => new Color("c0f0c0"),
            BuildingKind.Turret => new Color("e8e8e8"),
            BuildingKind.Lamp => new Color("fff8c0"),
            BuildingKind.SensorNode => new Color("d0fffc"),
            BuildingKind.ComparatorNode => new Color("f0d0ff"),
            BuildingKind.ActuatorNode => new Color("f8d0a0"),
            _ => new Color("ffffff"),
        };

        // ── 蟲潮（主色 / 暗部 / 亮部）───────────────────────────
        public static Color AlienBody(AlienKind k) => k switch
        {
            AlienKind.Runner => new Color("e08828"),
            AlienKind.Spitter => new Color("6fae3a"),
            AlienKind.Brute => new Color("88405a"),
            AlienKind.Elite => new Color("b83058"),
            AlienKind.Bomber => new Color("d8a020"),
            AlienKind.Flyer => new Color("48b0d8"),
            AlienKind.Frost => new Color("7cc8e8"),
            AlienKind.Magma => new Color("e05820"),
            AlienKind.Titan => new Color("a020f0"),
            _ => new Color("c8503a"),
        };

        public static Color AlienDark(AlienKind k) => k switch
        {
            AlienKind.Runner => new Color("a05c10"),
            AlienKind.Spitter => new Color("467420"),
            AlienKind.Brute => new Color("56283c"),
            AlienKind.Elite => new Color("7a1e38"),
            AlienKind.Bomber => new Color("946410"),
            AlienKind.Flyer => new Color("24708e"),
            AlienKind.Frost => new Color("3a7a9a"),
            AlienKind.Magma => new Color("8a2c08"),
            AlienKind.Titan => new Color("5c0c94"),
            _ => new Color("8a3224"),
        };

        public static Color AlienAccent(AlienKind k) => k switch
        {
            AlienKind.Runner => new Color("f8b050"),
            AlienKind.Spitter => new Color("a8d868"),
            AlienKind.Brute => new Color("b06080"),
            AlienKind.Elite => new Color("e05880"),
            AlienKind.Bomber => new Color("ffe070"),
            AlienKind.Flyer => new Color("9ae8ff"),
            AlienKind.Frost => new Color("d0f4ff"),
            AlienKind.Magma => new Color("ffb060"),
            AlienKind.Titan => new Color("e0a0ff"),
            _ => new Color("e87858"),
        };

        public static readonly Color AlienHpBack = new(0f, 0f, 0f, 0.6f);
        public static readonly Color AlienHpFill = new(0.95f, 0.35f, 0.35f);

        /// <summary>星球地表底色 + 高度明暗（shade01：0 低地、1 高地）。</summary>
        public static Color Terrain(PlanetDef p, float shade01)
        {
            var baseC = new Color(p.R, p.G, p.B);
            return baseC.Lightened(0.05f).Lerp(baseC.Lightened(0.30f), shade01);
        }

        /// <summary>這個建築「有沒有」進/出料口（沿用 3D 版的方向語言）。</summary>
        public static bool HasOutput(BuildingKind k)
            => k == BuildingKind.Miner || k == BuildingKind.Smelter || k == BuildingKind.Assembler;

        public static bool HasInput(BuildingKind k)
            => k == BuildingKind.Smelter || k == BuildingKind.Assembler || k == BuildingKind.Depot;
    }
}
