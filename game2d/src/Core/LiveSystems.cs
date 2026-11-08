// 中期節奏系統：動態市場、可選委託與有預警的世界事件。
// 全部使用固定 tick 與種子雜湊，不依賴 Godot，也不消耗戰鬥 RNG 序列。
using System;
using System.IO;

namespace CoreFactory.Core
{
    public enum CrisisKind : byte
    {
        None = 0,
        SolarEclipse = 1,  // 太陽能大減：提前準備火力與電池
        Overclock = 2,     // 機器加速但耗電上升：有電就能把握增產窗口
        MarketBoom = 3,    // 全場需求旺盛：適合交付庫存與完成委託
    }

    public sealed class ContractOrder
    {
        public ItemId Item;
        public int Required;
        public int Delivered;
        public long Reward;
        public float Remaining;
        public bool Accepted;
        public int Needed => Math.Max(0, Required - Delivered);
    }

    public sealed partial class SimWorld
    {
        private readonly float[] _marketSaturation = new float[GameData.ItemSlots];
        public ItemId HotItem { get; private set; } = ItemId.IronPlate;
        public const float HotPeriod = 120f;
        public float HotRemaining { get; private set; } = HotPeriod;

        public ContractOrder Contract { get; private set; }
        public float ContractCooldown { get; private set; } = 12f;
        public int ContractSerial { get; private set; }
        public int ContractsCompleted { get; private set; }
        public int ContractsFailed { get; private set; }

        public CrisisKind CurrentCrisis { get; private set; }
        public CrisisKind PendingCrisis { get; private set; }
        public float CrisisRemaining { get; private set; }
        public float PendingCrisisIn { get; private set; }
        public float NextCrisisIn { get; private set; } = 300f;
        public int CrisisSerial { get; private set; }
        public bool CrisisActive => CurrentCrisis != CrisisKind.None && CrisisRemaining > 0f;

        public float SaturationPercent(ItemId item)
            => Math.Clamp(_marketSaturation[(int)item], 0f, 100f);

        public bool IsHot(ItemId item) => item == HotItem;

        /// <summary>賣價倍率：基礎 1，賣爆後最低 0.45，熱門貨／市場狂熱再加乘。</summary>
        public float MarketPriceFactor(ItemId item)
        {
            float saturation = 1f - SaturationPercent(item) * 0.004f; // 100% → 0.60
            float hot = item == HotItem ? 1.80f : 1f;
            float boom = CrisisActive && CurrentCrisis == CrisisKind.MarketBoom ? 1.35f : 1f;
            return saturation * hot * boom;
        }

        public float EventSolarFactor
            => CrisisActive && CurrentCrisis == CrisisKind.SolarEclipse ? 0.20f : 1f;

        public float EventMachineSpeed
            => CrisisActive && CurrentCrisis == CrisisKind.Overclock ? 1.40f : 1f;

        public float EventPowerDemand
            => CrisisActive && CurrentCrisis == CrisisKind.Overclock ? 1.30f : 1f;

        private void TickLiveSystems(float dt)
        {
            TickMarket(dt);
            TickContract(dt);
            TickCrisis(dt);
        }

        private void TickMarket(float dt)
        {
            // 大約五分鐘從完全飽和恢復；轉做別的產品會讓舊市場自然回溫。
            float recovery = dt * 0.34f;
            for (int i = 1; i < _marketSaturation.Length; i++)
                _marketSaturation[i] = MathF.Max(0f, _marketSaturation[i] - recovery);

            // 交易市場第 1 分鐘後才開市；每 HotPeriod 秒輪換一次「熱門貨」。
            if (Elapsed < 60f) return;
            HotRemaining -= dt;
            if (HotRemaining > 0f) return;
            HotRemaining += HotPeriod;
            // 只在賣得少（飽和低）的東西裡挑，避免選中已經崩價的貨
            int span = GameData.ItemSlots - 5;
            var pickCandidates = new ItemId[3];
            int found = 0;
            for (int s = 0; s < 3; s++)
            {
                uint serial = (uint)(Elapsed / HotPeriod) + (uint)s;
                int pick = 5 + (int)(Hash2D.Hash(Seed, (int)serial, 0xC0FFEEu) % (uint)span);
                var candidate = (ItemId)Math.Clamp(pick, 5, GameData.ItemSlots - 1);
                if (SaturationPercent(candidate) < 60f) pickCandidates[found++] = candidate;
            }
            if (found > 0) HotItem = pickCandidates[0];
            Alert("market:" + (int)HotItem);
        }

        private void RecordSale(ItemId item, int count)
        {
            _marketSaturation[(int)item] = MathF.Min(100f,
                _marketSaturation[(int)item] + count * (item == ItemId.Science ? 2.0f : 0.8f));
        }

        private void TickContract(float dt)
        {
            if (Contract == null)
            {
                ContractCooldown -= dt;
                if (ContractCooldown <= 0f) GenerateContract();
                return;
            }
            if (!Contract.Accepted) return;
            Contract.Remaining -= dt;
            if (Contract.Remaining > 0f) return;
            ContractsFailed++;
            Contract = null;
            ContractCooldown = 20f;
            Alert("contract:failed");
        }

        private void GenerateContract()
        {
            int tier = Math.Clamp(Math.Max(PlanetIndex, UnlockedCount - 1), 0, 4);
            ItemId[] pool = tier switch
            {
                0 => new[] { ItemId.IronPlate, ItemId.CopperPlate, ItemId.Gear, ItemId.Circuit, ItemId.Ammo },
                1 => new[] { ItemId.Gear, ItemId.Circuit, ItemId.Ammo, ItemId.TitaniumPlate, ItemId.Science },
                2 => new[] { ItemId.Circuit, ItemId.SiliconWafer, ItemId.AdvancedCircuit, ItemId.Science },
                3 => new[] { ItemId.Crystal, ItemId.GoldPlate, ItemId.HeavyAmmo, ItemId.AdvancedCircuit },
                _ => new[] { ItemId.CoreCrystal, ItemId.AdvancedCircuit, ItemId.HeavyAmmo, ItemId.Science },
            };
            uint h = Hash2D.Hash(Seed ^ ContractSerial, PlanetIndex * 97 + 13, 0xB0A5u);
            var item = pool[h % (uint)pool.Length];
            int baseCount = item switch
            {
                ItemId.IronPlate or ItemId.CopperPlate or ItemId.Ammo => 30,
                ItemId.Gear or ItemId.Circuit or ItemId.TitaniumPlate or ItemId.SiliconWafer => 16,
                ItemId.Science or ItemId.AdvancedCircuit or ItemId.HeavyAmmo => 8,
                _ => 5,
            };
            int required = Math.Max(3, (int)(baseCount * (1f + Math.Min(ContractsCompleted, 8) * 0.12f)));
            ContractSerial++;
            Contract = new ContractOrder
            {
                Item = item,
                Required = required,
                Reward = (long)(required * GameData.CoinValue(item) * 2.2f + 200),
                Remaining = 180f,
            };
            Alert("contract:new");
        }

        public bool AcceptContract()
        {
            if (Contract == null || Contract.Accepted) return false;
            Contract.Accepted = true;
            Alert("contract:accepted");
            return true;
        }

        public int DeliverContract()
        {
            if (Contract == null || !Contract.Accepted) return 0;
            int moved = Math.Min(Contract.Needed, Hub[(int)Contract.Item]);
            if (moved <= 0) return 0;
            Hub[(int)Contract.Item] -= moved;
            Contract.Delivered += moved;
            if (Contract.Needed > 0) return moved;

            Coins += Contract.Reward;
            ContractsCompleted++;
            Alert("contract:done");
            Contract = null;
            ContractCooldown = 25f;
            return moved;
        }

        private void TickCrisis(float dt)
        {
            if (CrisisActive)
            {
                CrisisRemaining -= dt;
                if (CrisisRemaining <= 0f)
                {
                    Alert("event:end:" + (int)CurrentCrisis);
                    CurrentCrisis = CrisisKind.None;
                    CrisisRemaining = 0f;
                    NextCrisisIn = 240f + Hash2D.Hash(Seed, CrisisSerial, 0xE7E47u) % 121;
                }
                return;
            }

            if (PendingCrisis != CrisisKind.None)
            {
                PendingCrisisIn -= dt;
                if (PendingCrisisIn <= 0f)
                {
                    // 預警時間走完 → 事件正式開始
                    CurrentCrisis = PendingCrisis;
                    PendingCrisis = CrisisKind.None;
                    CrisisRemaining = CurrentCrisis == CrisisKind.MarketBoom ? 100f : 90f;
                    Alert("event:start:" + (int)CurrentCrisis);
                }
                return;
            }

            NextCrisisIn -= dt;
            if (NextCrisisIn > 30f) return;

            // 進入 30 秒預警窗 → 抽一種事件，先通知玩家再開始
            CrisisSerial++;
            PendingCrisis = (CrisisKind)(1 + Hash2D.Hash(Seed, CrisisSerial, 0xCA515u) % 3);
            PendingCrisisIn = MathF.Max(0f, NextCrisisIn);
            Alert("event:warning:" + (int)PendingCrisis);
            if (PendingCrisisIn <= 0f)
            {
                CurrentCrisis = PendingCrisis;
                PendingCrisis = CrisisKind.None;
                CrisisRemaining = CurrentCrisis == CrisisKind.MarketBoom ? 100f : 90f;
                Alert("event:start:" + (int)CurrentCrisis);
            }
        }

        private void SaveLiveSystems(BinaryWriter w)
        {
            for (int i = 0; i < _marketSaturation.Length; i++) w.Write(_marketSaturation[i]);
            w.Write((byte)HotItem); w.Write(HotRemaining);
            w.Write(ContractSerial); w.Write(ContractCooldown);
            w.Write(ContractsCompleted); w.Write(ContractsFailed);
            w.Write(Contract != null);
            if (Contract != null)
            {
                w.Write((byte)Contract.Item); w.Write(Contract.Required); w.Write(Contract.Delivered);
                w.Write(Contract.Reward); w.Write(Contract.Remaining); w.Write(Contract.Accepted);
            }
            w.Write((byte)CurrentCrisis); w.Write((byte)PendingCrisis);
            w.Write(CrisisRemaining); w.Write(PendingCrisisIn); w.Write(NextCrisisIn); w.Write(CrisisSerial);
        }

        private void LoadLiveSystems(BinaryReader r, int version)
        {
            if (version < 10) { ContractCooldown = 10f; return; }
            for (int i = 0; i < _marketSaturation.Length; i++) _marketSaturation[i] = r.ReadSingle();
            HotItem = (ItemId)r.ReadByte(); HotRemaining = r.ReadSingle();
            ContractSerial = r.ReadInt32(); ContractCooldown = r.ReadSingle();
            ContractsCompleted = r.ReadInt32(); ContractsFailed = r.ReadInt32();
            Contract = null;
            if (r.ReadBoolean())
            {
                Contract = new ContractOrder
                {
                    Item = (ItemId)r.ReadByte(), Required = r.ReadInt32(), Delivered = r.ReadInt32(),
                    Reward = r.ReadInt64(), Remaining = r.ReadSingle(), Accepted = r.ReadBoolean(),
                };
            }
            CurrentCrisis = (CrisisKind)r.ReadByte(); PendingCrisis = (CrisisKind)r.ReadByte();
            CrisisRemaining = r.ReadSingle(); PendingCrisisIn = r.ReadSingle();
            NextCrisisIn = r.ReadSingle(); CrisisSerial = r.ReadInt32();
        }
    }
}
