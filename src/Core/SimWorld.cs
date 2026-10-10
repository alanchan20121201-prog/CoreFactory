// ============================================================================
//  SimWorld：整個模擬的心臟。純 C#，零 Godot 依賴。
//  設計要點（對應 docs/00 工程約定）：
//   · 固定步長 tick，所有隨機走種子化 RNG → 可重現
//   · 傳送帶 = 「帶段 + 槽位 + 位移進度」環狀鏈結串列，物品不做物理
//   · 物品走 SoA 陣列 + 自由索引堆 → 十萬物品也不產生 GC 垃圾
//   · 存檔 = 世界種子 + 增量（建築/帶上物品），地形永遠用種子重算
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;

namespace CoreFactory.Core
{
    public sealed class SimWorld
    {
        public const int SaveVersion = 8;

        public int Seed { get; private set; }
        public int PlanetIndex { get; private set; }
        public PlanetDef Planet { get; private set; }
        public Terrain Terrain { get; private set; }
        public WeatherSystem Weather { get; private set; }

        public readonly Dictionary<int, Building> Buildings = new();
        public readonly Dictionary<int, BeltTile> Belts = new();

        private readonly List<int> _activeBelts = new();
        private readonly List<BeltTile> _scratchBelts = new();
        private readonly List<int> _scratchKeys = new();
        private readonly List<Building> _scratchBuildings = new();
        private readonly List<int> _scratchItemIdx = new();

        // ── 物品：SoA ────────────────────────────────────────────
        private byte[] _itKind = new byte[1 << 14];
        private float[] _itPos = new float[1 << 14];
        private int[] _itTile = new int[1 << 14];
        private int[] _itNext = new int[1 << 14];
        private int[] _freeStack = new int[1 << 12];
        private int _freeCount;
        private int _itemHigh;

        public int ItemCount { get; private set; }
        public long TotalItemsMoved { get; private set; }

        public readonly int[] Signals = new int[GameData.ItemSlots];
        public readonly int[] Hub = new int[GameData.ItemSlots];

        /// <summary>金幣：賣礦物賺錢、蓋建築花錢（見 GameData.CoinCost / CoinValue）。</summary>
        public long Coins;
        /// <summary>貸款債務（未還清前會利滾利）。</summary>
        public long LoanDebt;
        private float _interestTimer;

        // ── 無限研究（消耗科研包，永久強化） ────────────────────
        public readonly int[] Research = new int[4];   // 0 傳送帶速度 1 機器速度 2 採礦速度 3 發電量
        public float BeltSpeedMultiplier => 1f + Research[0] * 0.06f;
        public float MachineSpeedMultiplier => 1f + Research[1] * 0.06f;
        public float MinerSpeedMultiplier => 1f + Research[2] * 0.06f;
        public float PowerMultiplier => 1f + Research[3] * 0.06f;
        public int ResearchCost(int i) => (int)(20f * MathF.Pow(1.6f, Research[i]));

        // ── 工廠體檢（每 15 tick 統計一次） ─────────────────────
        public readonly int[] MachineTotal = new int[16];
        public readonly int[] MachineRunning = new int[16];
        public readonly int[] MachineBlocked = new int[16];

        public bool TryResearch(int index)
        {
            if (index < 0 || index >= Research.Length) return false;
            int cost = ResearchCost(index);
            if (Hub[(int)ItemId.Science] < cost) return false;
            Hub[(int)ItemId.Science] -= cost;
            Research[index]++;
            Alert("research:" + index);
            return true;
        }

        public readonly List<Alien> Aliens = new();
        public int AliensKilled;
        public int SoldCount;      // 累計賣出物品數（新手教學用）
        public float Efficiency = 1f;
        public float PowerGenerated, PowerUsed, BatteryStored;
        public float Elapsed;
        public long TickCount;
        public int WaveIndex;

        /// <summary>目前動態難度（依玩家實力自動縮放蟲潮，1.0 = 起始）。</summary>
        public float ThreatLevel = 1f;
        /// <summary>距離下一波蟲潮的秒數（給 UI 倒數用）。</summary>
        public float WaveTimer => _waveTimer;

        public readonly List<string> Alerts = new();

        /// <summary>false = 完全不生成蟲潮（教學期間用）。</summary>
        public bool MonstersEnabled = true;

        /// <summary>由設定介面調整的威脅倍率（1.0 = 星球原始強度）。</summary>
        public float ThreatMultiplier = 1f;

        /// <summary>地圖圖釘（玩家自己標記的地點），會存進存檔。</summary>
        public readonly List<(int x, int z)> Pins = new();

        public void AddPin(int x, int z)
        {
            for (int i = 0; i < Pins.Count; i++)
                if (Pins[i].x == x && Pins[i].z == z) return;
            Pins.Add((x, z));
            Alert("ui.pin_added");
        }

        public void RemovePinAt(int x, int z, int radius)
        {
            for (int i = Pins.Count - 1; i >= 0; i--)
            {
                int dx = Pins[i].x - x, dz = Pins[i].z - z;
                if (dx * dx + dz * dz <= radius * radius) { Pins.RemoveAt(i); return; }
            }
        }

        private Rng _rng;
        private float _waveTimer;

        public int BuildingCount => Buildings.Count;
        public int BeltCount => Belts.Count;
        public int AlienCount => Aliens.Count;

        public SimWorld(int seed, int planetIndex)
        {
            Seed = seed;
            PlanetIndex = planetIndex;
            Planet = Planets.Get(planetIndex);
            Terrain = new Terrain(seed, Planet);
            Weather = new WeatherSystem(seed, Planet);
            _rng = new Rng((ulong)seed * 0x9E3779B97F4A7C15UL + (ulong)planetIndex * 7919UL + 1UL);
            foreach (var s in GameData.StartStock) Hub[(int)s.item] = s.count;
            Coins = GameData.StartCoins;
            _waveTimer = Planet.ThreatFirstWaveSeconds;
            Alert(Loc.T("planet." + (planetIndex + 1)));
        }

        private void Alert(string msg)
        {
            Alerts.Add(msg);
            if (Alerts.Count > 40) Alerts.RemoveAt(0);
        }

        // ── 座標打包 ────────────────────────────────────────────
        public static int Key(int x, int z) => (x & 0xFFFF) | ((z & 0xFFFF) << 16);
        public static int UnX(int k) { int v = k & 0xFFFF; return v >= 0x8000 ? v - 0x10000 : v; }
        public static int UnZ(int k) { int v = (k >> 16) & 0xFFFF; return v >= 0x8000 ? v - 0x10000 : v; }

        /// <summary>拆除建築的回收價（不同建築回收價不同；隨市場浮動）。</summary>
        public long SellValue(BuildingKind k) => (long)(BuildingPrice(k) * 0.6f);

        /// <summary>市場因子：隨時間平滑起伏，範圍約 ±10%（升降價不會太多）。</summary>
        public float MarketFactor(int salt)
        {
            float t = Elapsed;
            float s = MathF.Sin(t * 0.05f + salt * 1.3f);
            float s2 = MathF.Sin(t * 0.013f + salt * 2.7f);
            return 1f + s * 0.06f + s2 * 0.04f;
        }

        /// <summary>礦物當前賣價（基礎價 × 市場因子 × 星球價格倍率）。</summary>
        public int ItemPrice(ItemId i) => (int)(GameData.CoinValue(i) * MarketFactor((int)i + 1) * Planet.PriceMultiplier);

        /// <summary>建築當前造價（基礎價 × 市場因子 × 星球造價倍率：越遠的星球越貴）。</summary>
        public long BuildingPrice(BuildingKind k)
            => (long)(GameData.CoinCost(k) * MarketFactor(100 + (int)k) * Planet.CostMultiplier);

        // ── 星球傳送門 ──────────────────────────────────────────
        /// <summary>已解鎖的星球位元遮罩（bit i = 第 i 顆星球已開通傳送門）。起始星球永遠解鎖。</summary>
        public long UnlockedMask = 1;

        public bool IsUnlocked(int planetIndex) => (UnlockedMask & (1L << planetIndex)) != 0;

        /// <summary>嘗試花錢開通某顆星球的傳送門：需要金幣足夠「且」沒有欠款。</summary>
        public bool TryUnlockPortal(int planetIndex, out string errorKey)
        {
            errorKey = "";
            if (planetIndex < 0 || planetIndex >= Planets.All.Length) { errorKey = "ui.portal.bad"; return false; }
            if (IsUnlocked(planetIndex)) { errorKey = "ui.portal.already"; return false; }
            if (LoanDebt > 0) { errorKey = "ui.portal.debt"; return false; }

            long cost = Planets.All[planetIndex].PortalCost;
            if (Coins < cost) { errorKey = "ui.portal.poor"; return false; }

            Coins -= cost;
            UnlockedMask |= 1L << planetIndex;
            Alert("portal:" + (planetIndex + 1));
            return true;
        }

        /// <summary>已解鎖的星球數量（UI 顯示用）。</summary>
        public int UnlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Planets.All.Length; i++) if (IsUnlocked(i)) n++;
                return n;
            }
        }

        /// <summary>借貸：立刻拿到金幣，債務利滾利（上限 20000）。</summary>
        public bool TakeLoan(long amount)
        {
            if (amount <= 0) return false;
            if (LoanDebt + amount > 20000) return false;
            Coins += amount;
            LoanDebt += amount;
            return true;
        }

        /// <summary>還款：從手頭金幣扣，償還債務。</summary>
        public bool RepayLoan(long amount)
        {
            if (amount <= 0 || LoanDebt <= 0) return false;
            amount = Math.Min(amount, Math.Min(Coins, LoanDebt));
            if (amount <= 0) return false;
            Coins -= amount;
            LoanDebt -= amount;
            return true;
        }

        public bool IsOccupied(int x, int z) => Buildings.ContainsKey(Key(x, z)) || Belts.ContainsKey(Key(x, z));

        public BeltTile BeltAt(int x, int z) => Belts.TryGetValue(Key(x, z), out var t) ? t : null;
        public Building BuildingAt(int x, int z) => Buildings.TryGetValue(Key(x, z), out var b) ? b : null;

        /// <summary>這個機器的出貨端（面向方向）是否接得到東西（傳送帶或倉庫）。</summary>
        public bool OutputConnected(int x, int z, Dir facing)
        {
            int fx = x + Dirs.Dx(facing), fz = z + Dirs.Dz(facing);
            if (Belts.ContainsKey(Key(fx, fz))) return true;
            var b = BuildingAt(fx, fz);
            return b != null && b.Kind == BuildingKind.Depot;
        }

        // ── 給渲染層讀物品（唯讀存取，避免外部動到內部鏈結） ────
        public int BeltItemHead(BeltTile t) => t.Head;
        public int ItemNext(int i) => _itNext[i];
        public byte ItemKindAt(int i) => _itKind[i];
        public float ItemPosAt(int i) => _itPos[i];

        // ── 建造 ────────────────────────────────────────────────
        public bool CanPlace(BuildingKind kind, int x, int z, out string errorKey)
        {
            errorKey = null;
            if (IsOccupied(x, z)) { errorKey = "ui.occupied"; return false; }
            if (kind == BuildingKind.Miner)
            {
                if (!Terrain.ResourceAt(x, z, out var ore, out _) || ore == ItemId.None)
                { errorKey = "ui.no_ore"; return false; }
            }
            long coinCost = BuildingPrice(kind);
            if (Coins < coinCost) { errorKey = "ui.cost_lack"; return false; }
            return true;
        }

        public bool TryPlace(BuildingKind kind, int x, int z, Dir facing, out string errorKey)
            => TryPlace(kind, x, z, facing, 0, -1, out errorKey);

        public bool TryPlace(BuildingKind kind, int x, int z, Dir facing, byte recipeIndex, int kindValue, out string errorKey)
        {
            if (!CanPlace(kind, x, z, out errorKey)) return false;
            Coins -= BuildingPrice(kind);

            int key = Key(x, z);
            if (kind == BuildingKind.Belt)
            {
                Belts[key] = new BeltTile { X = x, Z = z, Facing = facing };
                return true;
            }

            var b = new Building { Kind = kind, X = x, Z = z, Facing = facing, RecipeIndex = recipeIndex };
            if (kind == BuildingKind.Miner)
            {
                Terrain.ResourceAt(x, z, out var ore, out _);
                b.KindValue = (int)ore;
            }
            else if (kind == BuildingKind.ActuatorNode)
            {
                b.ChannelIn = 1;
                b.KindValue = kindValue >= 0 ? kindValue : (int)BuildingKind.Smelter;
            }
            else if (kind == BuildingKind.SensorNode)
            {
                b.ChannelOut = 0;
            }
            else if (kind == BuildingKind.ComparatorNode)
            {
                b.ChannelIn = 0; b.ChannelOut = 1; b.Threshold = 30;
                b.KindValue = 0;   // 0 = '>' 1 = '<' 2 = '=='
            }
            else if (kind == BuildingKind.Turret)
            {
                // 新手友善：砲塔出廠就附贈彈藥（並優先用庫存補到滿），馬上能防守
                b.Ammo = GameData.TurretStartAmmo;
                int want = GameData.TurretMagazine - b.Ammo;
                if (want > 0 && Hub[(int)ItemId.Ammo] > 0)
                {
                    int take = Math.Min(want, Hub[(int)ItemId.Ammo]);
                    Hub[(int)ItemId.Ammo] -= take;
                    b.Ammo += (byte)take;
                }
            }
            Buildings[key] = b;
            return true;
        }

        public bool Remove(int x, int z)
        {
            int key = Key(x, z);
            if (Belts.TryGetValue(key, out var belt))
            {
                int cur = belt.Head;
                while (cur >= 0) { int next = _itNext[cur]; FreeItem(cur); cur = next; }
                Belts.Remove(key);
                return true;
            }
            if (Buildings.TryGetValue(key, out var b))
            {
                Buildings.Remove(key);
                Coins += SellValue(b.Kind);   // 拆除回收（不同建築回收價不同）
                return true;
            }
            return false;
        }

        /// <summary>把庫存物品變賣成金幣（交易面板用）。</summary>
        public bool Sell(ItemId item, int count)
        {
            if (count <= 0 || Hub[(int)item] < count) return false;
            Hub[(int)item] -= count;
            Coins += (long)ItemPrice(item) * count;
            SoldCount += count;
            return true;
        }

        // ── 物品池 ──────────────────────────────────────────────
        private void EnsureItemCapacity(int need)
        {
            if (need < _itKind.Length) return;
            int cap = _itKind.Length;
            while (cap <= need) cap <<= 1;
            Array.Resize(ref _itKind, cap);
            Array.Resize(ref _itPos, cap);
            Array.Resize(ref _itTile, cap);
            Array.Resize(ref _itNext, cap);
        }

        private int AllocItem(ItemId kind, float pos, int tileKey)
        {
            int idx;
            if (_freeCount > 0) idx = _freeStack[--_freeCount];
            else { idx = _itemHigh++; EnsureItemCapacity(idx); }
            _itKind[idx] = (byte)kind;
            _itPos[idx] = pos;
            _itTile[idx] = tileKey;
            _itNext[idx] = -1;
            ItemCount++;
            return idx;
        }

        private void FreeItem(int idx)
        {
            if (_freeCount < _freeStack.Length) _freeStack[_freeCount++] = idx;
            _itNext[idx] = -1;
            ItemCount--;
        }

        // ── 傳送帶操作 ──────────────────────────────────────────
        public bool BeltInsert(BeltTile b, ItemId item, float pos)
        {
            if (b.Tail >= 0 && _itPos[b.Tail] < GameData.ItemSize + pos) return false;
            int idx = AllocItem(item, pos, Key(b.X, b.Z));
            if (b.Tail < 0) { b.Head = b.Tail = idx; b.Count = 1; }
            else { _itNext[b.Tail] = idx; b.Tail = idx; b.Count++; }
            return true;
        }

        private bool BeltUnlink(BeltTile b, int idx)
        {
            int prev = -1, cur = b.Head;
            while (cur >= 0 && cur != idx) { prev = cur; cur = _itNext[cur]; }
            if (cur < 0) return false;
            if (prev < 0) b.Head = _itNext[idx]; else _itNext[prev] = _itNext[idx];
            if (b.Tail == idx) b.Tail = prev;
            b.Count--;
            FreeItem(idx);
            return true;
        }

        private int BeltFind(BeltTile b, ItemId item)
        {
            int cur = b.Head;
            while (cur >= 0)
            {
                if (_itKind[cur] == (byte)item) return cur;
                cur = _itNext[cur];
            }
            return -1;
        }

        private BeltTile BackBelt(Building b)
        {
            int k = Key(b.X - Dirs.Dx(b.Facing), b.Z - Dirs.Dz(b.Facing));
            return Belts.TryGetValue(k, out var t) ? t : null;
        }

        private bool PushFront(Building b, ItemId item, int count)
        {
            if (count <= 0) return true;
            int k = Key(b.X + Dirs.Dx(b.Facing), b.Z + Dirs.Dz(b.Facing));
            if (Belts.TryGetValue(k, out var belt)) return BeltInsert(belt, item, 0f);
            if (Buildings.TryGetValue(k, out var tgt) && tgt.Kind == BuildingKind.Depot)
            { Hub[(int)item] += count; return true; }
            return false;
        }

        // ── 主循環 ──────────────────────────────────────────────
        public void Tick(float dt)
        {
            TickCount++;
            Elapsed += dt;

            // 貸款利滾利（每 60 秒 +5%，不還就會越滾越大）
            if (LoanDebt > 0)
            {
                _interestTimer += dt;
                if (_interestTimer >= 60f)
                {
                    _interestTimer = 0f;
                    LoanDebt = (long)(LoanDebt * 1.05f);
                    if (LoanDebt > 100000) LoanDebt = 100000;
                }
            }

            Weather.Tick(dt);
            PowerTick(dt);
            MinerTick(dt);
            CrafterTick(dt);
            DepotTick(dt);
            BeltsTick(dt);
            LogicTick();
            TurretTick(dt);
            AlienTick(dt);
            WaveTick(dt);
            if ((TickCount % 15) == 0) StatsTick();
        }

        private void StatsTick()
        {
            for (int i = 0; i < 16; i++) { MachineTotal[i] = 0; MachineRunning[i] = 0; MachineBlocked[i] = 0; }
            foreach (var kv in Buildings)
            {
                var b = kv.Value;
                if (b.Kind == BuildingKind.None || b.IsLogic) continue;
                int k = (int)b.Kind;
                MachineTotal[k]++;
                if (!b.Enabled) continue;

                if (b.Blocked && (b.Kind == BuildingKind.Miner || b.IsCrafter)) { MachineBlocked[k]++; continue; }
                bool working = b.Kind switch
                {
                    BuildingKind.Miner => !b.Blocked,
                    BuildingKind.Smelter => b.Producing,
                    BuildingKind.Assembler => b.Producing,
                    _ => true,
                };
                if (working) MachineRunning[k]++;
            }
        }

        private float SpeedFactor()
        {
            if (Efficiency >= 0.98f) return 1f;
            if (Efficiency < 0.15f) return 0f;
            return Efficiency;
        }

        private void PowerTick(float dt)
        {
            float gen = 0f, demand = 0f;
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) _scratchBuildings.Add(kv.Value);

            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                switch (b.Kind)
                {
                    case BuildingKind.SolarPanel:
                        gen += GameData.SolarPower * Weather.SolarFactor(Planet) * PowerMultiplier;
                        break;
                    case BuildingKind.ThermalGenerator:
                        if (b.Enabled)
                        {
                            if (b.In0Count == 0)
                            {
                                var fb = BackBelt(b);
                                if (fb != null)
                                {
                                    int ci = BeltFind(fb, ItemId.Coal);
                                    if (ci >= 0 && BeltUnlink(fb, ci)) b.In0Count++;
                                }
                                else if (Hub[(int)ItemId.Coal] > 0)
                                {
                                    // 切片簡化：沒有燃料帶時，火力發電機直接由倉庫補給煤
                                    Hub[(int)ItemId.Coal]--;
                                    b.In0Count++;
                                }
                            }
                            if (b.In0Count > 0)
                            {
                                gen += GameData.ThermalPower * PowerMultiplier;
                                b.Progress += dt * 0.5f;      // 2 秒燒一塊煤
                                if (b.Progress >= 1f) { b.Progress = 0f; b.In0Count--; }
                            }
                        }
                        break;
                }
                if (b.Enabled) demand += GameData.PowerDemand(b.Kind);
            }

            if (gen >= demand)
            {
                BatteryStored = MathF.Min(BatteryStored + (gen - demand) * dt, GameData.BatteryCapacity);
                Efficiency = 1f;
            }
            else
            {
                float deficit = demand - gen;
                float fromBattery = MathF.Min(BatteryStored, deficit * dt);
                BatteryStored -= fromBattery;
                float available = gen + (dt > 0f ? fromBattery / dt : 0f);
                Efficiency = demand <= 1e-3f ? 1f : MathF.Min(1f, available / demand);
            }
            PowerGenerated = gen;
            PowerUsed = demand * Efficiency;
        }

        private void MinerTick(float dt)
        {
            float sf = SpeedFactor();
            if (sf <= 0f) return;
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) if (kv.Value.Kind == BuildingKind.Miner) _scratchBuildings.Add(kv.Value);

            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                if (!b.Enabled) continue;
                b.Progress += dt * sf * MinerSpeedMultiplier;
                if (b.Progress < GameData.MinerSeconds) continue;
                if (PushFront(b, (ItemId)b.KindValue, 1)) { b.Progress = 0f; b.Blocked = false; }
                else { b.Progress = GameData.MinerSeconds; b.Blocked = true; }
            }
        }

        private void CrafterTick(float dt)
        {
            float sf = SpeedFactor();
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) if (kv.Value.IsCrafter) _scratchBuildings.Add(kv.Value);

            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                if (!b.Enabled) continue;
                var r = GameData.GetRecipe(b);

                if (b.Producing)
                {
                    b.Progress += dt * sf * MachineSpeedMultiplier;
                    if (b.Progress >= r.Seconds)
                    {
                        if (PushFront(b, r.Out, r.NOut)) { b.Producing = false; b.Progress = 0f; b.Blocked = false; }
                        else { b.Progress = r.Seconds; b.Blocked = true; }
                    }
                    continue;
                }

                var belt = BackBelt(b);
                if (belt == null) continue;

                // 熔煉爐自動辨識進料：帶上送什麼礦就自動切到對應配方
                // （新手不用記得按 Ctrl+滾輪，銅礦也能直接熔成銅板）
                if (b.Kind == BuildingKind.Smelter) { AutoSmelterRecipe(b, belt); r = GameData.GetRecipe(b); }

                int need0 = r.N0, need1 = r.N1;
                int got0 = 0, got1 = 0;
                _scratchItemIdx.Clear();
                int cur = belt.Head;
                while (cur >= 0)
                {
                    byte k = _itKind[cur];
                    if (k == (byte)r.In0 && need1 > 0 && k == (byte)r.In1) { /* 同種配方不會發生 */ }
                    if (k == (byte)r.In0 && got0 < need0) { got0++; _scratchItemIdx.Add(cur); }
                    else if (need1 > 0 && k == (byte)r.In1 && got1 < need1) { got1++; _scratchItemIdx.Add(cur); }
                    cur = _itNext[cur];
                }
                if (got0 >= need0 && got1 >= need1)
                {
                    for (int j = 0; j < _scratchItemIdx.Count; j++) BeltUnlink(belt, _scratchItemIdx[j]);
                    b.In0Kind = (byte)r.In0; b.In0Count = (byte)need0;
                    b.In1Kind = (byte)r.In1; b.In1Count = (byte)need1;
                    b.Producing = true;
                    b.Progress = 0f;
                    b.Blocked = false;
                }
            }
        }

        /// <summary>
        /// 熔煉爐自動辨識：後方傳送帶上出現的礦種，若與目前配方不符，
        /// 就自動切到對應的熔煉配方（鐵礦→鐵板、銅礦→銅板、鈦礦→鈦板）。
        /// 玩家仍可用 Ctrl+滾輪 手動指定，但手動指定的配方不會被覆蓋（除非帶上完全是別種礦）。
        /// </summary>
        private void AutoSmelterRecipe(Building b, BeltTile belt)
        {
            var recipes = GameData.SmelterRecipes;
            if (recipes == null || recipes.Length == 0) return;

            var cur = recipes[b.RecipeIndex % recipes.Length];

            // 帶上是否已存在「目前配方」要的礦？是就維持不動
            if (BeltFind(belt, cur.In0) >= 0) return;

            // 否則找第一個帶上真的有的礦種
            for (byte i = 0; i < recipes.Length; i++)
            {
                if (BeltFind(belt, recipes[i].In0) >= 0)
                {
                    b.RecipeIndex = i;
                    return;
                }
            }
        }

        private void DepotTick(float dt)
        {
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) if (kv.Value.Kind == BuildingKind.Depot) _scratchBuildings.Add(kv.Value);
            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var d = _scratchBuildings[i];
                var belt = BackBelt(d);
                if (belt == null) continue;
                int moved = 0;
                while (belt.Head >= 0 && moved < 8)
                {
                    byte kind = _itKind[belt.Head];
                    if (!BeltUnlink(belt, belt.Head)) break;
                    Hub[kind]++;
                    moved++;
                }
            }
        }

        private void BeltsTick(float dt)
        {
            float v = GameData.BeltSpeed * BeltSpeedMultiplier * Weather.BeltSpeedMultiplier * dt;
            if (v <= 0f) return;
            const float IS = GameData.ItemSize;

            _activeBelts.Clear();
            foreach (var kv in Belts) if (kv.Value.Count > 0) _activeBelts.Add(kv.Key);

            for (int i = 0; i < _activeBelts.Count; i++)
            {
                var b = Belts[_activeBelts[i]];
                int nkey = Key(b.X + Dirs.Dx(b.Facing), b.Z + Dirs.Dz(b.Facing));
                Belts.TryGetValue(nkey, out var nb);

                float frontLimit = 1f;
                if (nb != null && nb.Tail >= 0 && _itPos[nb.Tail] < IS) frontLimit = 1f - IS;

                float limit = frontLimit;
                int cur = b.Head;
                while (cur >= 0)
                {
                    float p = _itPos[cur] + v;
                    if (p > limit) p = limit;
                    if (p > _itPos[cur]) { _itPos[cur] = p; TotalItemsMoved++; }
                    limit = p - IS;
                    cur = _itNext[cur];
                }

                if (nb != null && b.Head >= 0 && _itPos[b.Head] >= 1f - 1e-4f)
                {
                    bool room = nb.Tail < 0 || _itPos[nb.Tail] >= IS;
                    if (room)
                    {
                        int idx = b.Head;
                        b.Head = _itNext[idx];
                        if (b.Head < 0) b.Tail = -1;
                        b.Count--;
                        _itNext[idx] = -1;
                        _itPos[idx] = 0f;
                        _itTile[idx] = nkey;
                        if (nb.Tail < 0) { nb.Head = nb.Tail = idx; nb.Count = 1; }
                        else { _itNext[nb.Tail] = idx; nb.Tail = idx; nb.Count++; }
                    }
                }
            }
        }

        private void LogicTick()
        {
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) if (kv.Value.IsLogic) _scratchBuildings.Add(kv.Value);

            // 1) 感測器：讀「電池百分比」或連結目標的產出
            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                if (b.Kind != BuildingKind.SensorNode) continue;
                int v = 0;
                if (b.LinkKey >= 0 && Buildings.TryGetValue(b.LinkKey, out var t)) v = t.OutCount;
                else v = (int)(BatteryStored / GameData.BatteryCapacity * 100f);
                b.SignalOut = v;
                Signals[b.ChannelOut & 15] = v;
            }

            // 2) 比較器
            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                if (b.Kind != BuildingKind.ComparatorNode) continue;
                int inVal = Signals[b.ChannelIn & 15];
                bool res = b.KindValue switch
                {
                    1 => inVal < b.Threshold,
                    2 => inVal == b.Threshold,
                    _ => inVal > b.Threshold,
                };
                b.SignalOut = res ? 1 : 0;
                Signals[b.ChannelOut & 15] = b.SignalOut;
            }

            // 3) 執行器：依訊號開關「指定種類的所有建築」
            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var b = _scratchBuildings[i];
                if (b.Kind != BuildingKind.ActuatorNode) continue;
                bool on = Signals[b.ChannelIn & 15] != 0;
                var target = (BuildingKind)b.KindValue;
                _scratchKeys.Clear();
                foreach (var kv in Buildings)
                    if (kv.Value.Kind == target) _scratchKeys.Add(kv.Key);
                for (int j = 0; j < _scratchKeys.Count; j++) Buildings[_scratchKeys[j]].Enabled = on;
            }
        }

        private void TurretTick(float dt)
        {
            bool online = Weather.TurretsOnline;
            _scratchBuildings.Clear();
            foreach (var kv in Buildings) if (kv.Value.Kind == BuildingKind.Turret) _scratchBuildings.Add(kv.Value);

            for (int i = 0; i < _scratchBuildings.Count; i++)
            {
                var t = _scratchBuildings[i];
                if (!t.Enabled || !online) continue;
                if (t.Ammo == 0)
                {
                    var belt = BackBelt(t);
                    if (belt != null)
                    {
                        // 優先吃普通砲彈，沒有的話吃穿甲彈（傷害加倍）
                        int idx = BeltFind(belt, ItemId.Ammo);
                        bool heavy = false;
                        if (idx < 0) { idx = BeltFind(belt, ItemId.HeavyAmmo); heavy = true; }
                        if (idx >= 0 && BeltUnlink(belt, idx))
                        {
                            t.Ammo = GameData.TurretMagazine;
                            t.KindValue = heavy ? 1 : 0;   // 1 = 裝填穿甲彈
                        }
                    }
                }
                t.Cooldown -= dt;
                if (t.Ammo <= 0 || t.Cooldown > 0f) continue;

                Alien best = null; float bestD = GameData.TurretRange * GameData.TurretRange;
                for (int j = 0; j < Aliens.Count; j++)
                {
                    var a = Aliens[j];
                    if (!a.Alive) continue;
                    float dx = a.X - (t.X + 0.5f), dz = a.Z - (t.Z + 0.5f);
                    float d2 = dx * dx + dz * dz;
                    if (d2 <= bestD) { bestD = d2; best = a; }
                }
                if (best == null) continue;
                best.Hp -= GameData.TurretDamage * (t.KindValue == 1 ? 2f : 1f);   // 穿甲彈傷害加倍
                t.Ammo--;
                t.Cooldown = GameData.TurretInterval;
                if (best.Hp <= 0f) { best.Alive = false; AliensKilled++; }
            }
        }

        private void AlienTick(float dt)
        {
            if (Aliens.Count == 0) return;

            // 目標：最近的建築（正式版會換成 flow-field；這裡先用最近目標搜尋 + 空間限制）
            for (int i = 0; i < Aliens.Count; i++)
            {
                var a = Aliens[i];
                if (!a.Alive) continue;
                Building target = null;
                float bestD = float.MaxValue;
                int scanned = 0;
                foreach (var kv in Buildings)
                {
                    var b = kv.Value;
                    if (b.Kind == BuildingKind.Lamp) continue;
                    float dx = b.X - a.X, dz = b.Z - a.Z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestD) { bestD = d2; target = b; }
                    if (++scanned > 400) break;
                }
                if (target == null) continue;

                if (bestD <= 2.2f)
                {
                    a.Cooldown -= dt;
                    if (a.Cooldown <= 0f)
                    {
                        target.Hp -= a.Damage;
                        a.Cooldown = 1.0f;
                        if (target.Hp <= 0f) _scratchKeys.Add(Key(target.X, target.Z));
                    }
                }
                else
                {
                    float d = MathF.Sqrt(bestD);
                    if (d > 1e-4f)
                    {
                        float mx = (target.X - a.X) / d, mz = (target.Z - a.Z) / d;
                        a.X += mx * a.Speed * dt;
                        a.Z += mz * a.Speed * dt;
                        // 模型正面 = -Z → yaw = atan2(-dx, -dz)；平滑轉向讓動畫自然
                        float want = MathF.Atan2(-mx, -mz);
                        float diff = MathF.IEEERemainder(want - a.Yaw, MathF.Tau);
                        a.Yaw += diff * Math.Min(1f, dt * 6f);
                        a.Moving = true;
                    }
                    else a.Moving = false;
                }
            }

            for (int i = 0; i < _scratchKeys.Count; i++)
            {
                if (Buildings.Remove(_scratchKeys[i])) Alert("destroyed:");
            }
            _scratchKeys.Clear();

            for (int i = Aliens.Count - 1; i >= 0; i--) if (!Aliens[i].Alive) Aliens.RemoveAt(i);
        }

        private void WaveTick(float dt)
        {
            // 教學還沒完成時完全不生成怪物（也讓已存在的怪物留在原處）
            if (!MonstersEnabled)
            {
                _waveTimer = 25f;
                return;
            }
            _waveTimer -= dt;
            if (_waveTimer > 0f) return;
            // 基地還沒成形（<3 棟建築）時不倒數：給玩家充分準備時間，別一開局就挨打
            if (Buildings.Count < 3) { _waveTimer = Planet.ThreatFirstWaveSeconds; return; }

            WaveIndex++;
            ThreatLevel = ComputeThreatLevel();
            int count = (int)((3f + WaveIndex * 1.6f) * Planet.ThreatScale * ThreatMultiplier * ThreatLevel) + Buildings.Count / 30;
            count = Math.Clamp(count, 3, 64);
            SpawnWave(count);
            Alert("wave:" + WaveIndex + " (" + count + ")");
            _waveTimer = MathF.Max(50f, 170f - WaveIndex * 7f);
        }

        /// <summary>依玩家實力算動態難度：砲塔/建築數/研究/擊殺越多，蟲潮越兇，但用對數壓制避免滾雪球。</summary>
        private float ComputeThreatLevel()
        {
            int buildings = 0, turrets = 0, research = 0;
            foreach (var kv in Buildings)
            {
                buildings++;
                if (kv.Value.Kind == BuildingKind.Turret) turrets++;
            }
            for (int i = 0; i < Research.Length; i++) research += Research[i];
            float power = 3f + buildings * 0.4f + turrets * 2.2f + research * 1.2f + AliensKilled * 0.04f;
            return 1f + MathF.Log(1f + power * 0.09f);
        }

        private void SpawnWave(int count)
        {
            float cx = 0f, cz = 0f;
            bool hasDepot = false;
            foreach (var kv in Buildings)
                if (kv.Value.Kind == BuildingKind.Depot) { cx = kv.Value.X; cz = kv.Value.Z; hasDepot = true; break; }
            if (!hasDepot)
            {
                foreach (var kv in Buildings) { cx += kv.Value.X; cz += kv.Value.Z; }
                if (Buildings.Count > 0) { cx /= Buildings.Count; cz /= Buildings.Count; }
            }

            float radius = 46f;
            for (int i = 0; i < count; i++)
            {
                float ang = _rng.Range(0f, MathF.Tau);
                var kind = PickAlienKind();
                var s = AlienDefs.Base(kind);
                Aliens.Add(new Alien
                {
                    X = cx + MathF.Cos(ang) * radius,
                    Z = cz + MathF.Sin(ang) * radius,
                    Hp = (s.hp * Planet.ThreatScale * Planet.ThreatHpMult + WaveIndex * 1.6f) * ThreatMultiplier * ThreatLevel,
                    Speed = s.speed * Planet.ThreatSpeedMult,
                    Damage = (s.damage + WaveIndex * 0.35f) * Planet.ThreatDmgMult * ThreatMultiplier,
                    Kind = kind,
                    Radius = s.radius,
                });
            }
        }

        private static int AlienTier(AlienKind k) => k switch
        {
            AlienKind.Runner => 1, AlienKind.Spitter => 2, AlienKind.Brute => 3, AlienKind.Elite => 4, _ => 0,
        };

        private static int AlienUnlockWave(AlienKind k) => k switch
        {
            AlienKind.Runner => 2, AlienKind.Spitter => 4, AlienKind.Brute => 6, AlienKind.Elite => 9, _ => 0,
        };

        /// <summary>從「這顆星球的蟲池」挑一隻：波次解鎖 + 越高階的蟲種越後期越常出現。</summary>
        private AlienKind PickAlienKind()
        {
            var pool = Planet.AlienPool;
            if (pool == null || pool.Length == 0)
                pool = new[] { AlienKind.Grunt, AlienKind.Runner, AlienKind.Spitter, AlienKind.Brute, AlienKind.Elite };

            float total = 0f;
            var cands = new List<(AlienKind kind, float weight)>();
            foreach (var k in pool)
            {
                if (WaveIndex < AlienUnlockWave(k)) continue;
                float w = 1f + AlienTier(k) * (0.3f + WaveIndex * 0.12f);
                cands.Add((k, w));
                total += w;
            }
            if (cands.Count == 0) return AlienKind.Grunt;

            float roll = _rng.NextFloat() * total;
            float acc = 0f;
            for (int i = 0; i < cands.Count; i++)
            {
                acc += cands[i].weight;
                if (roll <= acc) return cands[i].kind;
            }
            return cands[cands.Count - 1].kind;
        }

        /// <summary>開發用：在指定位置附近直接生成怪物（驗證模型外觀）。</summary>
        public void DebugSpawnAliens(int count, float cx, float cz, float radius = 9f)
        {
            var kinds = (AlienKind[])Enum.GetValues(typeof(AlienKind));
            for (int i = 0; i < count; i++)
            {
                float ang = i / (float)MathF.Max(1, count) * MathF.Tau;
                var kind = kinds[i % kinds.Length];
                var s = AlienDefs.Base(kind);
                Aliens.Add(new Alien
                {
                    X = cx + MathF.Cos(ang) * radius,
                    Z = cz + MathF.Sin(ang) * radius,
                    Hp = s.hp,
                    Speed = s.speed,
                    Damage = s.damage,
                    Kind = kind,
                    Radius = s.radius,
                });
            }
        }

        // ── 存檔 ────────────────────────────────────────────────
        public void SaveTo(string path)
        {
            using var fs = File.Create(path);
            using var w = new BinaryWriter(fs);
            w.Write(SaveVersion);
            w.Write(Seed);
            w.Write(PlanetIndex);
            w.Write(Elapsed);
            w.Write(TickCount);
            w.Write((byte)Weather.Current);
            w.Write(Weather.Time);
            w.Write(Weather.ChangeIn);
            w.Write(BatteryStored);
            w.Write(AliensKilled);
            w.Write(WaveIndex);
            w.Write(_waveTimer);
            w.Write(Coins);                                                // v6
            w.Write(LoanDebt);                                             // v7
            w.Write(UnlockedMask);                                         // v8 已解鎖星球
            for (int i = 0; i < GameData.ItemSlots; i++) { w.Write(Hub[i]); w.Write(Signals[i]); }
            for (int i = 0; i < Research.Length; i++) w.Write(Research[i]);   // v3
            w.Write(Pins.Count);                                              // v4
            for (int i = 0; i < Pins.Count; i++) { w.Write(Pins[i].x); w.Write(Pins[i].z); }

            w.Write(Buildings.Count);
            foreach (var kv in Buildings)
            {
                var b = kv.Value;
                w.Write((byte)b.Kind); w.Write(b.X); w.Write(b.Z); w.Write((byte)b.Facing);
                w.Write(b.RecipeIndex); w.Write(b.Ammo); w.Write(b.Hp); w.Write(b.Enabled);
                w.Write(b.Threshold); w.Write(b.ChannelIn); w.Write(b.ChannelOut);
                w.Write(b.LinkKey); w.Write(b.KindValue);
                w.Write(b.In0Count); w.Write(b.In1Count); w.Write(b.OutCount);
                w.Write(b.Energy); w.Write(b.Progress); w.Write(b.Producing);
            }

            w.Write(Belts.Count);
            foreach (var kv in Belts)
            {
                var t = kv.Value;
                w.Write(t.X); w.Write(t.Z); w.Write((byte)t.Facing);
                _scratchItemIdx.Clear();
                int cur = t.Head;
                while (cur >= 0) { _scratchItemIdx.Add(cur); cur = _itNext[cur]; }
                w.Write(_scratchItemIdx.Count);
                // 必須「由車頭往車尾」寫；讀取時依同順序插回尾端，鏈結順序才會一致
                for (int i = 0; i < _scratchItemIdx.Count; i++)
                { w.Write(_itKind[_scratchItemIdx[i]]); w.Write(_itPos[_scratchItemIdx[i]]); }
            }

            w.Write(Aliens.Count);
            foreach (var a in Aliens) { w.Write(a.X); w.Write(a.Z); w.Write(a.Hp); w.Write(a.Damage); w.Write(a.Speed); w.Write((byte)a.Kind); }
        }

        public bool LoadFrom(string path, out string error)
        {
            error = null;
            try
            {
                using var fs = File.OpenRead(path);
                using var r = new BinaryReader(fs);
                int version = r.ReadInt32();
                if (version < 1 || version > SaveVersion) { error = "存檔版本不支援: " + version; return false; }

                // 清空目前狀態
                Buildings.Clear(); Belts.Clear(); Aliens.Clear();
                ItemCount = 0; _freeCount = 0; _itemHigh = 0; Alerts.Clear();

                Seed = r.ReadInt32();
                PlanetIndex = r.ReadInt32();
                Planet = Planets.Get(PlanetIndex);
                Terrain = new Terrain(Seed, Planet);
                Weather = new WeatherSystem(Seed, Planet);
                Elapsed = r.ReadSingle();
                TickCount = r.ReadInt64();
                Weather.Current = (WeatherKind)r.ReadByte();
                Weather.Time = r.ReadSingle();
                Weather.ChangeIn = r.ReadSingle();
                BatteryStored = r.ReadSingle();
                AliensKilled = r.ReadInt32();
                WaveIndex = r.ReadInt32();
                _waveTimer = r.ReadSingle();
                Coins = version >= 6 ? r.ReadInt64() : GameData.StartCoins;
                LoanDebt = version >= 7 ? r.ReadInt64() : 0;
                UnlockedMask = version >= 8 ? r.ReadInt64() : 1;
                int slots = version >= 8 ? GameData.ItemSlots : 16;
                for (int i = 0; i < slots; i++) { Hub[i] = r.ReadInt32(); Signals[i] = r.ReadInt32(); }
                for (int i = 0; i < Research.Length; i++) Research[i] = 0;
                if (version >= 3)
                    for (int i = 0; i < Research.Length; i++) Research[i] = r.ReadInt32();
                Pins.Clear();
                if (version >= 4)
                {
                    int pcount = r.ReadInt32();
                    for (int i = 0; i < pcount; i++) Pins.Add((r.ReadInt32(), r.ReadInt32()));
                }

                int bcount = r.ReadInt32();
                for (int i = 0; i < bcount; i++)
                {
                    var b = new Building
                    {
                        Kind = (BuildingKind)r.ReadByte(), X = r.ReadInt32(), Z = r.ReadInt32(),
                        Facing = (Dir)r.ReadByte(),
                    };
                    b.RecipeIndex = r.ReadByte(); b.Ammo = r.ReadByte(); b.Hp = r.ReadSingle();
                    b.Enabled = r.ReadBoolean(); b.Threshold = r.ReadInt32();
                    b.ChannelIn = r.ReadByte(); b.ChannelOut = r.ReadByte();
                    b.LinkKey = r.ReadInt32(); b.KindValue = r.ReadInt32();
                    b.In0Count = r.ReadByte(); b.In1Count = r.ReadByte(); b.OutCount = r.ReadByte();
                    b.Energy = r.ReadSingle(); b.Progress = r.ReadSingle(); b.Producing = r.ReadBoolean();
                    Buildings[Key(b.X, b.Z)] = b;
                }

                int tcount = r.ReadInt32();
                for (int i = 0; i < tcount; i++)
                {
                    var t = new BeltTile { X = r.ReadInt32(), Z = r.ReadInt32(), Facing = (Dir)r.ReadByte() };
                    int n = r.ReadInt32();
                    for (int j = 0; j < n; j++)
                    {
                        var kind = (ItemId)r.ReadByte();
                        float pos = r.ReadSingle();
                        BeltInsert(t, kind, pos);
                    }
                    Belts[Key(t.X, t.Z)] = t;
                }

                if (version >= 2)
                {
                    int acount = r.ReadInt32();
                    for (int i = 0; i < acount; i++)
                    {
                        var a = new Alien
                        {
                            X = r.ReadSingle(), Z = r.ReadSingle(), Hp = r.ReadSingle(),
                            Damage = r.ReadSingle(), Speed = r.ReadSingle(),
                        };
                        a.Kind = version >= 5 ? (AlienKind)r.ReadByte() : AlienKind.Grunt;
                        a.Radius = AlienDefs.Base(a.Kind).radius;
                        Aliens.Add(a);
                    }
                }
                Alert("ui.loaded");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
