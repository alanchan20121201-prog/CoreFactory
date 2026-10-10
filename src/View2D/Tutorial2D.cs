// 新手教學：循序目標導引。玩家每完成一步，自動前進到下一步並提示。
// 每個目標除了「目標」還有「怎麼做」的詳細提示，確保新手知道每一步的操作方法
// （尤其是最容易卡住的：如何取得砲彈、如何把砲彈送進砲塔）。
using System;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class Tutorial2D
    {
        public struct Objective
        {
            public string Text;                 // 目標（短）
            public string Hint;                 // 怎麼做（詳細步驟）
            public Func<SimWorld, bool> Done;   // 完成條件
        }

        /// <summary>
        /// 完整新手閉環：
        /// 採礦 → 傳送帶 → 熔煉 → 倉庫 → 賣錢 → 供電 → 組裝機 → 齒輪 → 砲彈 → 砲塔 → 送彈 → 科研包 → 研究 → 防禦實戰
        /// </summary>
        public static readonly Objective[] List =
        {
            new Objective {
                Text = "① 蓋第一台「採礦機」（按 2）",
                Hint = "1. 找一塊有顏色的小石頭（礦脈：棕=鐵、橙=銅、黑=煤、白=石）\n" +
                       "2. 按鍵盤 2 選「採礦機」\n" +
                       "3. 滑鼠移到礦脈上，會出現半透明預覽（綠箭頭＝出貨方向）\n" +
                       "4. 左鍵放下（會扣金幣；金幣不足請先按 G 賣礦）\n" +
                       "　　R 鍵可旋轉出貨方向",
                Done = w => Count(w, BuildingKind.Miner) >= 1 },

            new Objective {
                Text = "② 接 3 段「傳送帶」（按 1）把礦運出來",
                Hint = "1. 按 1 選「傳送帶」\n" +
                       "2. 從採礦機的【綠色箭頭那一面】往外連著左鍵拖/點 3 格\n" +
                       "3. 帶面上的 V 形條紋就是物品流動方向，方向錯了按 R 旋轉",
                Done = w => w.BeltCount >= 3 },

            new Objective {
                Text = "③ 放「熔煉爐」（按 3）把礦煉成板",
                Hint = "1. 按 3 選「熔煉爐」\n" +
                       "2. 放在傳送帶尾端，讓【藍色箭頭＝進料口】對準帶子\n" +
                       "3. 熔煉爐會【自動辨識】送上帶子的礦種：鐵礦→鐵板、銅礦→銅板、鈦礦→鈦板\n" +
                       "4. 想固定熔某一種，對熔煉爐按 Ctrl+滾輪 手動指定配方",
                Done = w => Count(w, BuildingKind.Smelter) >= 1 },

            new Objective {
                Text = "④ 再拉傳送帶接到「倉庫」（按 5）",
                Hint = "1. 熔煉爐的【綠色箭頭＝出貨口】接傳送帶\n" +
                       "2. 帶尾放「倉庫」（按 5），成品會自動入庫\n" +
                       "3. 之後就能按 G 把庫存賣成金幣",
                Done = w => Count(w, BuildingKind.Depot) >= 1 },

            new Objective {
                Text = "⑤ 按 G 開「交易面板」賣東西換金幣",
                Hint = "1. 按 G 開交易面板\n" +
                       "2. 每樣東西右邊有「賣出」按鈕（礦價會上下浮動 ±10%）\n" +
                       "3. 金幣不夠時，面板底部可以向銀行貸款（會算利息，要還）",
                Done = w => w.Coins > 0 && w.SoldCount > 0 },

            new Objective {
                Text = "⑥ 放「太陽能板」（按 6）供電",
                Hint = "1. 按 6 選「太陽能板」放在空地\n" +
                       "2. 沒電時整座工廠效率會暴跌（頂列「效率」會顯示）\n" +
                       "3. 晚上沒太陽 → 可再放「蓄電池」存白天多餘的電",
                Done = w => Count(w, BuildingKind.SolarPanel) >= 1 },

            new Objective {
                Text = "⑦ 放「組裝機」（按 4）做零件",
                Hint = "1. 按 4 選「組裝機」\n" +
                       "2. Ctrl + 滾輪 可切換配方（齒輪／電路／砲彈／科研包）\n" +
                       "3. 它的【藍箭頭】要接進料帶、【綠箭頭】接出貨帶",
                Done = w => Count(w, BuildingKind.Assembler) >= 1 },

            new Objective {
                Text = "⑧ 做出「齒輪」：組裝機配方切到齒輪（鐵板×2）",
                Hint = "1. 選組裝機 → Ctrl+滾輪 切到「齒輪」配方\n" +
                       "2. 用傳送帶把「鐵板」送進組裝機的【藍色進料口】\n" +
                       "3. 齒輪是所有進階物品（電路／砲彈／科研包）的基礎",
                Done = w => AnyRecipeOut(w, ItemId.Gear) },

            new Objective {
                Text = "⑨ 【取得砲彈】組裝機切到「砲彈」配方（齒輪＋銅板）",
                Hint = "1. 對「組裝機」按 Ctrl + 滾輪，切換到【砲彈】配方\n" +
                       "2. 砲彈材料＝「齒輪」+「銅板」（各 1 個，產出 2 發）\n" +
                       "3. 銅板＝銅礦經熔煉爐（熔煉爐會自動辨識銅礦）；齒輪＝鐵板×3 經組裝機\n" +
                       "4. 兩條帶子（齒輪、銅板）都要送進同一台組裝機的【藍色進料口】",
                Done = w => AnyRecipeOut(w, ItemId.Ammo) },

            new Objective {
                Text = "⑩ 放「砲塔」（按 9）準備防守",
                Hint = "1. 按 9 選「防禦砲塔」，放在基地外圍（蟲來的方向）\n" +
                       "2. ★新蓋好的砲塔會【免費附贈 20 發彈藥】，馬上就能防守\n" +
                       "3. 之後要靠傳送帶送「砲彈」進它的【藍色進料口】補給（一次補滿 30 發）",
                Done = w => Count(w, BuildingKind.Turret) >= 1 },

            new Objective {
                Text = "⑪ 把「砲彈」用傳送帶送進砲塔",
                Hint = "1. 從做砲彈的組裝機【綠色出貨口】拉傳送帶到砲塔【藍色進料口】\n" +
                       "2. 成功後右側資訊欄會顯示「彈藥 N」（一開始就有 20 發免費彈藥）\n" +
                       "3. 蟲潮來時砲塔會自動開火；彈藥打完又沒帶子補給＝不會射擊",
                Done = w => HasAmmo(w) },

            new Objective {
                Text = "⑫ 做出「科研包」（齒輪＋電路）",
                Hint = "1. 另一台組裝機切到「科研包」配方（齒輪 + 電路）\n" +
                       "2. 電路＝銅板 + 齒輪（組裝機做）\n" +
                       "3. 所以完整鏈：鐵礦→鐵板→齒輪；銅礦→銅板；齒輪+銅板→電路；齒輪+電路→科研包",
                Done = w => AnyRecipeOut(w, ItemId.Science) },

            new Objective {
                Text = "⑬ 按 U 開「研究」消耗科研包永久升級",
                Hint = "1. 按 U 開研究面板，四條線：帶速／機器／採礦／電力\n" +
                       "2. 每次升級成本以 1.6 倍遞增 → 越後面越貴，要擴產線才供得起\n" +
                       "3. 升級永久有效，是「無限玩不無聊」的核心",
                Done = w => ResearchSum(w) > 0 },

            new Objective {
                Text = "⑭ 撐過第一波蟲潮（記得按 F5 存檔）",
                Hint = "1. 頂列會顯示「下波 XX 秒」倒數\n" +
                       "2. 蟲會從地圖外圍衝進來啃建築 → 砲塔要有子彈\n" +
                       "3. 按 F5 隨時存檔；設定（Esc）裡可回到主選單\n" +
                       "4. 小地圖（M）可看礦脈與基地全貌",
                Done = w => w.WaveIndex >= 1 },
        };

        public static int Index;   // 目前進行到第幾個目標

        public static void Reset() => Index = 0;

        public static bool Completed => Index >= List.Length;

        public static string Current => Index < List.Length ? List[Index].Text : "新手教學完成！自由擴張你的異星工廠吧";

        public static string CurrentHint => Index < List.Length ? List[Index].Hint : "";

        private static int Count(SimWorld w, BuildingKind kind)
        {
            int n = 0;
            foreach (var kv in w.Buildings) if (kv.Value.Kind == kind) n++;
            return n;
        }

        private static int ResearchSum(SimWorld w)
            => w.Research[0] + w.Research[1] + w.Research[2] + w.Research[3];

        private static bool AnyRecipeOut(SimWorld w, ItemId item)
        {
            foreach (var kv in w.Buildings)
                if (kv.Value.Kind == BuildingKind.Assembler && GameData.GetRecipe(kv.Value).Out == item) return true;
            return false;
        }

        private static bool HasAmmo(SimWorld w)
        {
            foreach (var kv in w.Buildings)
                if (kv.Value.Kind == BuildingKind.Turret && kv.Value.Ammo > 0) return true;
            return false;
        }

        /// <summary>由 Main2D 每幀呼叫：若目前目標已完成，前進並回傳新目標（已完成整列則回傳 null）。</summary>
        public static string Advance(SimWorld w)
        {
            int advanced = 0;
            while (Index < List.Length && List[Index].Done(w))
            {
                Index++;
                advanced++;
            }
            if (advanced == 0) return null;
            return Current;
        }
    }
}
