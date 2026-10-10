// 中英雙語：所有玩家可見文字都必須走這裡（含資料表名稱），遊戲內可即時切換
using System;
using System.Collections.Generic;

namespace CoreFactory.Core
{
    public static class Loc
    {
        public static Lang Current = Lang.ZH;

        /// <summary>語言變更事件：所有 UI 都應該訂閱它並重建自己的文字（避免切換後殘留舊語言）。</summary>
        public static event Action Changed;

        public static void Set(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            Changed?.Invoke();
        }

        public static void Cycle()
        {
            Set((Lang)(((int)Current + 1) % 4));
        }

        /// <summary>依作業系統預設語言選擇介面語言（第一次啟動時使用）。</summary>
        public static Lang DetectFromSystem(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return Lang.EN;
            string l = locale.ToLowerInvariant().Replace('-', '_');
            if (l.StartsWith("ja")) return Lang.JA;
            if (l.StartsWith("zh"))
            {
                if (l.Contains("tw") || l.Contains("hk") || l.Contains("mo") || l.Contains("hant")) return Lang.TW;
                return Lang.ZH;
            }
            if (l.StartsWith("en")) return Lang.EN;
            return Lang.EN;
        }

        public static string LangCode(Lang l) => l switch
        {
            Lang.ZH => "zh",
            Lang.TW => "tw",
            Lang.EN => "en",
            _ => "ja",
        };

        public static Lang LangFromCode(string code) => (code ?? "zh").ToLowerInvariant() switch
        {
            "tw" or "zh_tw" or "hant" => Lang.TW,
            "en" => Lang.EN,
            "ja" => Lang.JA,
            _ => Lang.ZH,
        };

        public static string SelfName(Lang l) => l switch
        {
            Lang.ZH => "简体中文",
            Lang.TW => "繁體中文",
            Lang.EN => "English",
            _ => "日本語",
        };

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var v))
            {
                switch (Current)
                {
                    case Lang.ZH: return ToSimplified(v.zh);      // 來源文字是繁體
                    case Lang.TW: return v.zh;
                    case Lang.JA: return JaTable.TryGetValue(key, out var ja) ? ja : v.en;
                    default: return v.en;
                }
            }
            if (JaTable.TryGetValue(key, out var jaOnly) && Current == Lang.JA) return jaOnly;
            return key;
        }

        // ── 繁體 → 簡體：詞彙表（台灣用語→大陸用語）＋ 字對字對照表 ──
        private static readonly (string from, string to)[] WordMapS2T =
        {
            ("滑鼠", "鼠标"), ("設定", "设置"), ("選單", "菜单"), ("檔案", "文件"),
            ("預設", "默认"), ("螢幕", "屏幕"), ("網路", "网络"), ("品質", "质量"),
            ("支援", "支持"), ("最佳化", "优化"), ("載入", "加载"), ("儲存", "保存"),
            ("圖示", "图标"), ("執行", "运行"), ("教學", "教程"), ("目錄", "目录"),
            ("目前", "当前"), ("新增", "添加"), ("資訊", "信息"), ("記憶體", "内存"),
            ("硬體", "硬件"), ("軟體", "软件"), ("介面", "界面"), ("滑鼠", "鼠标"),
            ("繪製", "绘制"), ("伺服器", "服务器"), ("匯入", "导入"), ("匯出", "导出"),
            ("影片", "视频"), ("影格", "帧"), ("偵測", "检测"), ("解析度", "分辨率"),
        };

        /// <summary>繁 → 簡（每個字一對一；兩條字串長度必須一致）。</summary>
        private const string TradChars =
            "專傳佈佔倉側備儲兇內兩別刪則劃務區卻參員啟嚴國圍團圖執報場壓壞夠學實寫寬尋對層島帶幫幾庫廠張強彈彙徑從復態憶應戰戲擊擇擋據擬數斷時晝暫會東條棟樂樞樣機檔檢欄歐" +
            "氣準溫滾濟灣燈燒營爐牆狀獨環現畫異當疊發盤盡確礎礦砲種稱積穩節築簡約納純紙細終組結絕絡給統經網綴線緣編緩練緻縫縮總織繞繪繼續纜置義習" +
            "見視覽觀觸訂計訊訓記設訴診詞試詰該詳認語誤說誰調請論證識譯議讀變讓貨貯費貼資質賴賽車軟軸較載輕輝輪輯輸轉農邊遷過運遠選還邏釘針銅鋪錄錯鍵鏈鏟鏡鐘鐵鑄鑽長門閉開間閥閾關陰陸陽隊階際隨險隱雜離雲霧靜響頂項順須預頭頻顆題額顏類顯風飾餘驅驗驟體" +
            "點齒於鬥麼豐滿隻產業務動員線組織訓練實現檔標準" +
            "採敗極構歐決沒減潰無燼癱瘓籤脅脈腳膠臟蓋藍處螢衛衝補裡複襲遊鈕鋸嵐曇囪蟲敵與藥號訂開臨貼擊標實檢獎";

        private const string SimpChars =
            "专传布占仓侧备储凶内两别删则划务区却参员启严国围团图执报场压坏够学实写宽寻对层岛带帮几库厂张强弹汇径从复态忆应战戏击择挡据拟数断时昼暂会东条栋乐枢样机档检栏欧" +
            "气准温滚济湾灯烧营炉墙状独环现画异当叠发盘尽确础矿炮种称积稳节筑简约纳纯纸细终组结绝络给统经网缀线缘编缓练致缝缩总织绕绘继续缆置义习" +
            "见视览观触订计讯训记设诉诊词试诘该详认语误说谁调请论证识译议读变让货贮费贴资质赖赛车软轴较载轻辉轮辑输转农边迁过运远选还逻钉针铜铺录错键链铲镜钟铁铸钻长门闭开间阀阈关阴陆阳队阶际随险隐杂离云雾静响顶项顺须预头频颗题额颜类显风饰余驱验骤体" +
            "点齿于斗么丰满只产业务动员线组织训练实现档标准" +
            "采败极构欧决没减溃无烬瘫痪签胁脉脚胶脏盖蓝处萤卫冲补里复袭游钮锯岚昙囱虫敌与药号订开临贴击标实检奖";

        private static readonly Dictionary<char, char> TsMap = BuildTsMap();

        private static Dictionary<char, char> BuildTsMap()
        {
            var m = new Dictionary<char, char>();
            int n = Math.Min(TradChars.Length, SimpChars.Length);
            if (TradChars.Length != SimpChars.Length)
                Console.WriteLine($"[loc] 繁簡對照表長度不一致：{TradChars.Length} vs {SimpChars.Length}（以較短者為準）");
            for (int i = 0; i < n; i++) m[TradChars[i]] = SimpChars[i];
            return m;
        }

        public static string ToSimplified(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            for (int i = 0; i < WordMapS2T.Length; i++)
                s = s.Replace(WordMapS2T[i].from, WordMapS2T[i].to);
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(TsMap.TryGetValue(c, out char t) ? t : c);
            return sb.ToString();
        }

        // ── 繁體：先用詞彙表（大陸用語→台灣用語），再用字對字轉換 ──
        private static readonly (string from, string to)[] WordMap =
        {
            ("鼠标", "滑鼠"), ("键盘", "鍵盤"), ("默认", "預設"), ("内存", "記憶體"),
            ("网络", "網路"), ("界面", "介面"), ("软件", "軟體"), ("硬件", "硬體"),
            ("信息", "資訊"), ("屏幕", "螢幕"), ("打开", "開啟"), ("关闭", "關閉"),
            ("保存", "儲存"), ("加载", "載入"), ("设置", "設定"), ("图标", "圖示"),
            ("菜单", "選單"), ("视频", "影片"), ("质量", "品質"), ("支持", "支援"),
            ("运行", "執行"), ("教程", "教學"), ("文件", "檔案"), ("目录", "目錄"),
            ("打印", "列印"), ("屏幕", "螢幕"), ("优化", "最佳化"), ("服务", "服務"),
            ("应用", "應用"), ("绘制", "繪製"), ("当前", "目前"), ("添加", "新增"),
            ("移除", "移除"), ("计算", "計算"), ("暂停", "暫停"), ("恢复", "恢復"),
            ("画质", "畫質"), ("帧率", "幀率"), ("存档", "存檔"), ("读档", "讀檔"),
            ("切换", "切換"), ("任务", "任務"), ("目标", "目標"), ("自动", "自動"),
            ("手动", "手動"), ("系统", "系統"), ("版本", "版本"), ("教程", "教學"),
        };

        private static readonly Dictionary<char, char> CharMap = new()
        {
            ['设'] = '設', ['计'] = '計', ['传'] = '傳', ['带'] = '帶', ['电'] = '電',
            ['炉'] = '爐', ['铁'] = '鐵', ['铜'] = '銅', ['矿'] = '礦', ['击'] = '擊',
            ['图'] = '圖', ['标'] = '標', ['点'] = '點', ['关'] = '關', ['开'] = '開',
            ['语'] = '語', ['体'] = '體', ['简'] = '簡', ['显'] = '顯', ['线'] = '線',
            ['组'] = '組', ['织'] = '織', ['逻'] = '邏', ['辑'] = '輯', ['发'] = '發',
            ['现'] = '現', ['与'] = '與', ['应'] = '應', ['为'] = '為', ['这'] = '這',
            ['个'] = '個', ['们'] = '們', ['时'] = '時', ['间'] = '間', ['后'] = '後',
            ['头'] = '頭', ['见'] = '見', ['论'] = '論', ['认'] = '認', ['识'] = '識',
            ['输'] = '輸', ['进'] = '進', ['远'] = '遠', ['边'] = '邊', ['过'] = '過',
            ['还'] = '還', ['运'] = '運', ['转'] = '轉', ['载'] = '載', ['车'] = '車',
            ['轮'] = '輪', ['轴'] = '軸', ['键'] = '鍵', ['盘'] = '盤', ['视'] = '視',
            ['频'] = '頻', ['网'] = '網', ['络'] = '絡', ['软'] = '軟', ['务'] = '務',
            ['动'] = '動', ['态'] = '態', ['变'] = '變', ['换'] = '換', ['数'] = '數',
            ['据'] = '據', ['库'] = '庫', ['储'] = '儲', ['备'] = '備', ['复'] = '復',
            ['杂'] = '雜', ['严'] = '嚴', ['热'] = '熱', ['温'] = '溫', ['雾'] = '霧',
            ['云'] = '雲', ['风'] = '風', ['阴'] = '陰', ['昼'] = '晝', ['结'] = '結',
            ['缆'] = '纜', ['统'] = '統', ['级'] = '級', ['类'] = '類', ['种'] = '種',
            ['样'] = '樣', ['术'] = '術', ['专'] = '專', ['业'] = '業', ['员'] = '員',
            ['团'] = '團', ['队'] = '隊', ['长'] = '長', ['宽'] = '寬', ['质'] = '質',
            ['画'] = '畫', ['营'] = '營', ['区'] = '區', ['块'] = '塊', ['岛'] = '島',
            ['陆'] = '陸', ['针'] = '針', ['寻'] = '尋', ['帮'] = '幫', ['说'] = '說',
            ['学'] = '學', ['习'] = '習', ['训'] = '訓', ['练'] = '練', ['试'] = '試',
            ['验'] = '驗', ['测'] = '測', ['档'] = '檔', ['编'] = '編', ['制'] = '製',
            ['筑'] = '築', ['装'] = '裝', ['货'] = '貨', ['资'] = '資', ['产'] = '產',
            ['链'] = '鏈', ['条'] = '條', ['层'] = '層', ['骤'] = '驟', ['阶'] = '階',
            ['旧'] = '舊', ['始'] = '始', ['终'] = '終', ['继'] = '繼', ['续'] = '續',
            ['暂'] = '暫', ['缓'] = '緩', ['够'] = '夠', ['余'] = '餘', ['额'] = '額',
            ['划'] = '劃', ['纸'] = '紙', ['桥'] = '橋', ['梁'] = '樑', ['阀'] = '閥',
            ['门'] = '門', ['炼'] = '煉', ['铸'] = '鑄', ['钻'] = '鑽', ['铲'] = '鏟',
            ['舱'] = '艙', ['跃'] = '躍', ['迁'] = '遷', ['侦'] = '偵', ['险'] = '險',
            ['御'] = '禦', ['炮'] = '砲', ['弹'] = '彈', ['药'] = '藥', ['敌'] = '敵',
            ['虫'] = '蟲', ['预'] = '預', ['报'] = '報', ['乐'] = '樂', ['环'] = '環',
            ['境'] = '境', ['览'] = '覽', ['录'] = '錄', ['简'] = '簡', ['称'] = '稱',
            ['温'] = '溫', ['让'] = '讓', ['给'] = '給', ['经'] = '經', ['济'] = '濟',
            ['纳'] = '納', ['统'] = '統', ['号'] = '號', ['标'] = '標', ['态'] = '態',
        };

        public static string ToTraditional(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            for (int i = 0; i < WordMap.Length; i++)
                s = s.Replace(WordMap[i].from, WordMap[i].to);
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(CharMap.TryGetValue(c, out char t) ? t : c);
            return sb.ToString();
        }

        /// <summary>日文翻譯表（缺的項目會退回英文）。</summary>
        private static readonly Dictionary<string, string> JaTable = new()
        {
            ["item.0"] = "なし", ["item.1"] = "鉄鉱石", ["item.2"] = "銅鉱石", ["item.3"] = "石炭",
            ["item.4"] = "石", ["item.5"] = "鉄板", ["item.6"] = "銅板", ["item.7"] = "歯車",
            ["item.8"] = "回路", ["item.9"] = "研究パック", ["item.10"] = "弾薬",
            ["building.0"] = "なし", ["building.1"] = "採掘機", ["building.2"] = "ベルト",
            ["building.3"] = "精錬炉", ["building.4"] = "組立機", ["building.5"] = "倉庫",
            ["building.6"] = "太陽光パネル", ["building.7"] = "火力発電機", ["building.8"] = "蓄電池",
            ["building.9"] = "タレット", ["building.10"] = "照明", ["building.11"] = "センサー",
            ["building.12"] = "比較器", ["building.13"] = "アクチュエータ",
            ["weather.0"] = "快晴", ["weather.1"] = "曇り", ["weather.2"] = "嵐",
            ["weather.3"] = "灰の嵐", ["weather.4"] = "イオン嵐",
            ["planet.1"] = "ゼノン1号・ツンドラ辺境", ["planet.2"] = "シンダー9号・燃える荒野",
            ["ui.title"] = "コアファクトリー：無限の地平", ["ui.build"] = "建設", ["ui.rotate"] = "R 回転",
            ["ui.save"] = "保存", ["ui.load"] = "読込", ["ui.lang"] = "言語", ["ui.planet"] = "惑星",
            ["ui.switch_planet"] = "別の惑星へワープ", ["ui.design_mode"] = "設計モード (Tab)",
            ["ui.blueprint_copy"] = "設計図コピー (C)", ["ui.blueprint_paste"] = "貼り付け (V)",
            ["ui.quality"] = "画質", ["ui.quality_low"] = "低", ["ui.quality_mid"] = "中",
            ["ui.quality_high"] = "高", ["ui.quality_auto"] = "自動", ["ui.quality_custom"] = "カスタム",
            ["ui.ups"] = "更新", ["ui.fps"] = "FPS", ["ui.items"] = "ベルト上の物", ["ui.power"] = "電力",
            ["ui.battery"] = "電池", ["ui.day"] = "時刻", ["ui.weather"] = "天気", ["ui.hub"] = "倉庫",
            ["ui.research"] = "研究", ["ui.research.belt"] = "ベルト速度", ["ui.research.machine"] = "機械速度",
            ["ui.research.miner"] = "採掘速度", ["ui.research.power"] = "発電効率", ["ui.research.level"] = "Lv",
            ["ui.research.cost"] = "研究パック", ["ui.research.buy"] = "強化", ["ui.research.done"] = "研究完了",
            ["ui.research.lack"] = "研究パック不足", ["ui.science_stock"] = "研究パック在庫",
            ["ui.health"] = "工場診断", ["ui.health_ok"] = "正常", ["ui.health_blocked"] = "出口詰まり",
            ["ui.health_idle"] = "待機", ["ui.health_none"] = "機械がありません",
            ["ui.items_panel"] = "インベントリ", ["ui.labels"] = "建物アイコン (L)", ["ui.alerts"] = "イベント",
            ["ui.settings"] = "設定", ["ui.tab_video"] = "画面", ["ui.tab_audio"] = "音", ["ui.tab_game"] = "ゲーム",
            ["ui.tab_lang"] = "言語", ["ui.tab_keys"] = "操作", ["ui.preset"] = "画質プリセット",
            ["ui.render_scale"] = "描画スケール", ["ui.shadows"] = "影", ["ui.bloom"] = "ブルーム",
            ["ui.fog"] = "大気・SSAO", ["ui.msaa"] = "アンチエイリアス", ["ui.view_distance"] = "表示距離",
            ["ui.item_cap"] = "表示上限", ["ui.fullscreen"] = "全画面", ["ui.vsync"] = "垂直同期",
            ["ui.volume_master"] = "全体音量", ["ui.volume_music"] = "環境音楽", ["ui.volume_sfx"] = "効果音",
            ["ui.language"] = "言語", ["ui.sim_ups"] = "シミュレーション速度", ["ui.threat_scale"] = "脅威レベル",
            ["ui.autosave"] = "自動保存", ["ui.autosave_interval"] = "自動保存間隔（分）", ["ui.show_tips"] = "初心者ヒント",
            ["ui.reset_defaults"] = "初期化", ["ui.close"] = "閉じる", ["ui.on"] = "オン", ["ui.off"] = "オフ",
            ["menu.tagline"] = "異星で、手作業から自動帝国へ", ["menu.start"] = "ゲーム開始",
            ["menu.settings"] = "設定", ["menu.quit"] = "終了", ["menu.hint"] = "Esc でいつでも設定",
            ["ui.press_key"] = "キーを押してください…",
            ["ui.key.move_up"] = "カメラ前進", ["ui.key.move_down"] = "カメラ後退", ["ui.key.move_left"] = "カメラ左",
            ["ui.key.move_right"] = "カメラ右", ["ui.key.rotate"] = "向き変更", ["ui.key.copy"] = "設計図コピー",
            ["ui.key.paste"] = "設計図貼付", ["ui.key.design"] = "設計モード", ["ui.key.center"] = "拠点に戻る",
            ["ui.key.labels"] = "建物アイコン", ["ui.key.save"] = "保存", ["ui.key.load"] = "読込",
            ["ui.key.map"] = "マップ", ["ui.key.tutorial"] = "チュートリアル",
            ["ui.key.center"] = "拠点に戻る", ["ui.center"] = "拠点に戻る",

            ["map.title"] = "マップ（M で開閉）",
            ["map.hint"] = "左クリック=視点移動 ・ 右クリック=ピン ・ ホイール=ズーム",
            ["map.pins"] = "ピン", ["map.no_pins"] = "ピンがありません（右クリックで追加）", ["map.player"] = "あなた",

            ["tutorial.title"] = "チュートリアル", ["tutorial.skip"] = "スキップ",
            ["tutorial.show"] = "チュートリアルを表示", ["tutorial.progress"] = "進捗",
            ["tutorial.done"] = "完了！次は：生産ラインを増やす・研究・防衛。",
            ["tutorial.step.look"] = "WASD で移動、ホイールでズーム。基地を見てみよう",
            ["tutorial.step.map"] = "M でマップを開く（建物・自機・ピンが出ます）",
            ["tutorial.step.belt"] = "ベルトを5枚置く（左クリック長押し、R で回転）",
            ["tutorial.step.miner"] = "鉱脈の上に採掘機を置き、ベルトへ向ける",
            ["tutorial.step.smelter"] = "精錬炉を置く（後ろから鉱石、前へ製品）",
            ["tutorial.step.depot"] = "製品を倉庫へ戻す（在庫になります）",
            ["tutorial.step.research"] = "研究パックを貯めて「研究」で強化（永続）",
            ["tutorial.step.turret"] = "タレットを置いて弾薬を供給（敵は強くなります）",
            ["tutorial.step.done"] = "完了！? でいつでも説明を開けます",

            ["help.title"] = "遊び方", ["help.open"] = "ヘルプ",
            ["help.goal"] = "目標：手作業から始めて、自動で回る工業帝国を作る。",
            ["help.loop"] = "基本：鉱脈 → 採掘機 → ベルト → 精錬炉 → 組立機 → 倉庫（製品は在庫へ、建設費は在庫から）。",
            ["help.power"] = "電力：夜は太陽光が死にます。火力か蓄電池を。電力不足だと全部が遅くなります。",
            ["help.logic"] = "論理回路：センサー → 比較器 → アクチュエータ。初期配置は「電池30%未満で精錬炉停止」。",
            ["help.weather"] = "天気：嵐はベルトを遅くし、イオン嵐はタレットを止めます。",
            ["help.planet"] = "2つの惑星：シンダー9号は太陽光35%・脅威1.9倍・鉱石は豊富。",
            ["help.blueprint"] = "設計図：C でコピー、V で貼り付け。",
            ["help.dir"] = "方向（最重要）：各マシンには入力側と出力側があります。設置前に地面の矢印を確認 —— 緑＝出力、青＝入力。ベルトは流れる向きに模様が動きます。R か下部の「設置方向」で90°回転。緑矢印の先にベルトが無いと詰まります。",
            ["help.first"] = "初めての手順：①鉄鉱脈へ移動 →②「ベルト」を直線に数マス敷く →③「採掘機」を鉱脈に置き緑矢印をベルトへ →④ベルトの先に「精錬炉」（青矢印をベルト側に）→⑤精錬炉の出口から「倉庫」へ。倉庫の鉄板が建設材料になります。",
            ["help.deco"] = "地面の岩・クリスタル・植物は装飾で、建設の邪魔になりません。鉱脈は光る鉱石の山（鉄＝青灰、銅＝錆色、石炭＝黒、石＝灰）。カーソルを合わせると右側に名前が出ます。",
            ["help.ui"] = "UI：各パネルの上端に小さな矢印があり、押すとパネル全体が畳まれます。別のパネルを開くと隠れていたものは自動で閉じます。F2 で全UI非表示。",
            ["help.tips"] = "ヒント：建物にカーソルで詳細、研究で強化、アイコンで見分ける。",
            ["ui.lang_auto"] = "システム言語に合わせる", ["ui.lang_detected"] = "検出した言語：",

            // ── v0.4.0 追加分 ──
            ["menu.new"] = "新しいゲーム", ["menu.continue"] = "つづきから", ["menu.saves"] = "セーブ管理",
            ["menu.back"] = "戻る", ["menu.quit_to_menu"] = "メインメニューへ", ["menu.no_save"] = "（セーブがありません）",
            ["menu.export"] = "セーブを書き出す", ["menu.import"] = "セーブを読み込む", ["menu.delete"] = "削除",
            ["menu.overwrite"] = "このセーブを上書きしますか？", ["menu.play_time"] = "プレイ時間",
            ["menu.exported"] = "書き出しました：", ["menu.imported"] = "セーブを読み込みました",
            ["menu.import_fail"] = "読み込み失敗（形式が違います）",
            ["menu.continue_hint"] = "前回の続きから（位置とカメラも復元）",
            ["menu.saves_hint"] = "惑星ごとに3スロット。書き出したファイルはバックアップや共有に。",
            ["save.slot"] = "スロット", ["save.empty"] = "空", ["save.saved_at"] = "保存", ["ui.save_slot_used"] = "スロットに保存しました",
            ["ui.wave_incoming"] = "敵の襲来", ["ui.building_destroyed"] = "建物が破壊されました",
            ["ui.menu"] = "メニュー", ["ui.hide_ui"] = "UIを隠す", ["ui.ui_hidden"] = "UIを隠しました（F2で表示）",
            ["ui.panel_help"] = "ヘルプ", ["ui.panel_tutorial"] = "チュートリアル", ["ui.panel_items"] = "図鑑",
            ["ui.panel_music"] = "音楽", ["ui.panel_research"] = "研究",
            ["music.now"] = "再生中", ["music.play"] = "再生", ["music.pause"] = "一時停止", ["music.next"] = "次へ",
            ["music.volume"] = "音楽音量", ["music.track.0"] = "異星の夜明け", ["music.track.1"] = "深宇宙のこだま",
            ["music.track.2"] = "機械の脈動", ["music.track.3"] = "結晶の森",
            ["ui.detail"] = "モデル精細度", ["ui.detail_low"] = "低（基本モデル）", ["ui.detail_mid"] = "中",
            ["ui.detail_high"] = "高（精巧モデル）",
            ["ui.detail_note"] = "低は最も簡単な図形で古いPCでも動きます。高は工業模型の細部まで表示します。",
            ["ui.music_note"] = "音楽と効果音はリアルタイム生成。ここで個別に調整・オフできます。",
            ["ui.collapse_hint"] = "パネルを折りたたむ／展開",
            ["ui.facing"] = "向き",
            ["ui.facing_btn"] = "設置方向",
            ["ui.facing_btn_hint"] = "クリックで90°回転（Rキーでも可）",
            ["ui.io_in"] = "入力 ← ", ["ui.io_out"] = "出力 → ",
            ["ui.ore_here"] = "鉱脈：", ["ui.ore_hint"] = "（ここに採掘機を置く）",
            ["ui.hint_out_blocked"] = "⚠ 出力側にベルトがありません！緑の矢印の先にベルトを置いてください（Rで回転）",
            ["ui.deco_note"] = "地面の岩やクリスタルは装飾です。",
            ["ui.received"] = "受取",
            ["ui.facing_hint"] = "設置方向：オレンジ＝出力、青＝入力。R キーかこのボタンで回転",
            ["dir.0"] = "北", ["dir.1"] = "東", ["dir.2"] = "南", ["dir.3"] = "西",
            ["music.combat"] = "戦闘中", ["music.peace"] = "環境",
            ["ui.hp"] = "体力", ["ui.hp_lost"] = "敵に倒されました！",
            ["ui.hp_hint"] = "敵はプレイヤーも攻撃します。かまれるとハートが1つ減り、2つ無くなるとゲームオーバー。",
            ["ui.dead_revive"] = "拠点で復活（在庫の半分を失う）", ["ui.dead_load"] = "直前のセーブを読む",
            ["ui.dead_menu"] = "メインメニューへ",
            ["ui.cost"] = "建設費", ["ui.free"] = "材料不要", ["ui.direction"] = "向き",
            ["ui.dir.n"] = "北", ["ui.dir.e"] = "東", ["ui.dir.s"] = "南", ["ui.dir.w"] = "西",
            ["codex.title"] = "図鑑：何に使うのか", ["codex.items"] = "アイテム", ["codex.buildings"] = "建物",
            ["codex.used_in"] = "用途", ["codex.cost"] = "費用",
            ["tutorial.stages"] = "チュートリアル段階", ["tutorial.page_hint"] = "下の段階名を押すと、その部分をもう一度見られます。",
            ["tutorial.stage"] = "段階", ["tutorial.replay"] = "再生", ["tutorial.done_mark"] = "完了",
            ["tutorial.arrow_hint"] = "下部で点滅しているボタンに従おう（道具は自動で選ばれます）",
            ["tutorial.step.power"] = "火力発電機か蓄電池を建てる（夜は太陽光が死にます）",
            ["tutorial.step.codex"] = "「図鑑」を開いて各アイテムの用途を見る",
            ["tutorial.s1.name"] = "段階1：環境を知る", ["tutorial.s2.name"] = "段階2：最初の生産ライン",
            ["tutorial.s3.name"] = "段階3：材料と建設", ["tutorial.s4.name"] = "段階4：電力と研究",
            ["tutorial.s5.name"] = "段階5：防衛と拡張",
            ["ui.note_render_scale"] = "100%未満は少ないピクセルで描いて拡大。低スペックで最も効く高速化です。",
            ["ui.note_shadows"] = "オフにすると軽くなりますが、立体感が減ります。",
            ["ui.note_view_distance"] = "見える距離。大きいほど重いですが鉱脈を探しやすくなります。",
            ["ui.note_ups"] = "1秒あたりのシミュレーション回数。20に下げるとCPUを節約できます。",
            ["ui.note_threat"] = "敵の強さ倍率。のんびり建築したいときは下げましょう。",
            ["ui.note_autosave"] = "数分ごとに自動保存。落ちても進行が消えません。",
            ["ui.note_zoom"] = "ズームには上下限があり、見失ったり地面に潜ったりしません。",
            ["ui.clouds"] = "雲", ["ui.note_clouds"] = "雲が流れ、夜は星空になります。低スペックならオフに。",
            ["ui.music_autoplay"] = "ゲーム内で音楽を自動再生", ["ui.music_autoplay_note"] = "オフのときは右下のプレイヤーから再生できます。",
            ["ui.save_on_exit"] = "終了時に自動保存", ["ui.note_save_on_exit"] = "ウィンドウを閉じても進行と位置が保存され、「つづきから」で戻れます。",
            ["ui.saved_to"] = "保存しました",
            ["ui.key.rot_left"] = "視点を左回転", ["ui.key.rot_right"] = "視点を右回転",
            ["ui.key.hideui"] = "UIの表示切替",
            ["ui.design_hint"] = "設計モード：左クリック設置・中ドラッグで視点回転・Q/E 回転・ホイールでズーム",
        };

        public static string Item(ItemId i) => T("item." + (int)i);
        public static string Building(BuildingKind k) => T("building." + (int)k);
        public static string Weather(WeatherKind w) => T("weather." + (int)w);
        public static string Planet(PlanetDef p) => T(p.NameKey);

        private static readonly Dictionary<string, (string zh, string en)> Table = new()
        {
            // ── 物品 ──────────────────────────────────────────────
            ["item.0"] = ("無", "None"),
            ["item.1"] = ("鐵礦", "Iron Ore"),
            ["item.2"] = ("銅礦", "Copper Ore"),
            ["item.3"] = ("煤", "Coal"),
            ["item.4"] = ("石頭", "Stone"),
            ["item.5"] = ("鐵板", "Iron Plate"),
            ["item.6"] = ("銅板", "Copper Plate"),
            ["item.7"] = ("齒輪", "Gear"),
            ["item.8"] = ("電路", "Circuit"),
            ["item.9"] = ("科研包", "Science Pack"),
            ["item.10"] = ("砲彈", "Ammo"),
            ["item.11"] = ("鈦礦", "Titanium Ore"),
            ["item.12"] = ("鈦板", "Titanium Plate"),
            ["item.13"] = ("矽礦", "Silicon Ore"),
            ["item.14"] = ("矽晶圓", "Silicon Wafer"),
            ["item.15"] = ("晶石礦", "Crystal Ore"),
            ["item.16"] = ("晶體", "Crystal"),
            ["item.17"] = ("金礦", "Gold Ore"),
            ["item.18"] = ("金板", "Gold Plate"),
            ["item.19"] = ("星核礦", "Core Ore"),
            ["item.20"] = ("星核晶", "Core Crystal"),
            ["item.21"] = ("高級電路", "Advanced Circuit"),
            ["item.22"] = ("穿甲彈", "Heavy Ammo"),

            // ── 建築 ──────────────────────────────────────────────
            ["building.0"] = ("無", "None"),
            ["building.1"] = ("採礦機", "Miner"),
            ["building.2"] = ("傳送帶", "Belt"),
            ["building.3"] = ("熔煉爐", "Smelter"),
            ["building.4"] = ("組裝機", "Assembler"),
            ["building.5"] = ("中樞倉庫", "Depot"),
            ["building.6"] = ("太陽能板", "Solar Panel"),
            ["building.7"] = ("火力發電機", "Thermal Generator"),
            ["building.8"] = ("蓄電池", "Battery"),
            ["building.9"] = ("防禦砲塔", "Turret"),
            ["building.10"] = ("照明燈", "Lamp"),
            ["building.11"] = ("感測器", "Sensor"),
            ["building.12"] = ("比較器", "Comparator"),
            ["building.13"] = ("執行器", "Actuator"),

            // ── 天氣 ──────────────────────────────────────────────
            ["weather.0"] = ("晴朗", "Clear"),
            ["weather.1"] = ("多雲", "Cloudy"),
            ["weather.2"] = ("風暴", "Storm"),
            ["weather.3"] = ("灰燼風暴", "Ash Storm"),
            ["weather.4"] = ("離子風暴", "Ion Storm"),

            // ── 星球 ──────────────────────────────────────────────
            ["planet.1"] = ("賽農一號 · 苔原邊境", "Xenon-1 · Tundra Frontier"),
            ["planet.2"] = ("灰燼九號 · 火成荒原", "Cinder-9 · Ember Wastes"),
            ["planet.3"] = ("翡翠七星 · 矽晶沼澤", "Viridian-7 · Silicon Marsh"),
            ["planet.4"] = ("霜蝕四號 · 冰晶荒原", "Glacia-4 · Frost Wastes"),
            ["planet.5"] = ("深淵零號 · 星核遺骸", "Nyx-0 · Core Remnant"),
            ["planet.desc.1"] = ("新手起點：日照充足、蟲潮溫和，鐵銅煤石俱全。", "Starting world: plenty of sun, gentle waves, all basic ores."),
            ["planet.desc.2"] = ("日照極弱、灰燼蔽日，逼你改用火力與電池；但蘊藏鈦礦。", "Weak sun and ash skies force thermal power; titanium lies beneath."),
            ["planet.desc.3"] = ("沼澤毒氣與矽礦共生，可生產矽晶圓與高級電路；飛行蟲會繞過防線。", "Silicon marshes enable wafers and advanced circuits; flyers bypass your line."),
            ["planet.desc.4"] = ("冰晶荒原日照微弱，產晶石與金礦；霜蝕蟲冰甲極厚，需要更強火力。", "Frozen wastes yield crystal and gold; frost carapaces shrug off shells."),
            ["planet.desc.5"] = ("終局星球：星核晶價值最高，但星核獸只有穿甲彈擋得住。", "Endgame world: core crystals are priceless, but only heavy ammo stops a Titan."),

            // ── 介面 ──────────────────────────────────────────────
            ["ui.title"] = ("星核工廠：無盡深空", "Core Factory: Infinite Horizon"),
            ["ui.build"] = ("建造", "Build"),
            ["ui.delete_hint"] = ("右鍵／X 拆除", "RMB / X to remove"),
            ["ui.rotate"] = ("R 旋轉", "R rotate"),
            ["ui.save"] = ("存檔", "Save"),
            ["ui.load"] = ("讀檔", "Load"),
            ["ui.lang"] = ("English", "中文"),
            ["ui.planet"] = ("星球", "Planet"),
            ["ui.switch_planet"] = ("躍遷到另一顆星球", "Warp to the other planet"),
            ["ui.camera"] = ("視角", "Camera"),
            ["ui.design_mode"] = ("設計模式 (Tab)", "Design Mode (Tab)"),
            ["ui.first_person"] = ("第一人稱 (F)", "First Person (F)"),
            ["ui.blueprint_copy"] = ("複製藍圖 (C)", "Copy Blueprint (C)"),
            ["ui.blueprint_paste"] = ("貼上藍圖 (V)", "Paste Blueprint (V)"),
            ["ui.quality"] = ("畫質", "Quality"),
            ["ui.quality_low"] = ("低", "Low"),
            ["ui.quality_mid"] = ("中", "Mid"),
            ["ui.quality_high"] = ("高", "High"),
            ["ui.quality_auto"] = ("自動降檔", "Auto"),
            ["ui.ups"] = ("模擬", "UPS"),
            ["ui.fps"] = ("幀率", "FPS"),
            ["ui.items"] = ("帶上物品", "Items on belts"),
            ["ui.power"] = ("電力", "Power"),
            ["ui.power_gen"] = ("發電", "Gen"),
            ["ui.power_use"] = ("耗電", "Use"),
            ["ui.battery"] = ("電量", "Battery"),
            ["ui.day"] = ("時刻", "Clock"),
            ["ui.weather"] = ("天氣", "Weather"),
            ["ui.hub"] = ("倉庫", "Depot"),
            ["ui.logic"] = ("邏輯電路", "Logic"),
            ["ui.channel_in"] = ("輸入通道", "In channel"),
            ["ui.channel_out"] = ("輸出通道", "Out channel"),
            ["ui.threshold"] = ("閾值", "Threshold"),
            ["ui.value"] = ("數值", "Value"),
            ["ui.link"] = ("連結目標", "Link target"),
            ["ui.no_link"] = ("未連結", "Not linked"),
            ["ui.alerts"] = ("事件", "Events"),
            ["ui.help"] = ("WASD 移動 · 滾輪縮放 · 左鍵建造 · 右鍵拆除 · Tab 設計模式 · F 第一人稱",
                           "WASD move · Wheel zoom · LMB build · RMB remove · Tab design · F first person"),
            ["ui.saved"] = ("已存檔", "Saved"),
            ["ui.loaded"] = ("已讀檔", "Loaded"),
            ["ui.save_fail"] = ("存檔失敗", "Save failed"),
            ["ui.load_fail"] = ("讀檔失敗", "Load failed"),
            ["ui.copied"] = ("藍圖已複製到剪貼簿", "Blueprint copied to clipboard"),
            ["ui.pasted"] = ("藍圖已貼上", "Blueprint pasted"),
            ["ui.copy_empty"] = ("附近沒有可複製的建築", "No buildings nearby to copy"),
            ["ui.cost_lack"] = ("材料不足", "Not enough materials"),
            ["ui.occupied"] = ("該位置已被佔用", "Tile occupied"),
            ["ui.no_ore"] = ("這裡沒有礦脈", "No ore vein here"),
            ["ui.invalid_place"] = ("無法建造於此", "Cannot build here"),
            ["ui.planet_switched"] = ("已躍遷", "Warped"),
            ["ui.tip_title"] = ("給第一次玩的你", "First-time tips"),
            ["ui.tip_body"] = ("1) 在礦脈上放採礦機，出口朝傳送帶方向\n2) 傳送帶把礦送到熔煉爐，熔煉爐出口再接到傳送帶\n3) 把成品送回中樞倉庫會計入庫存\n4) 晚上太陽能會失效 —— 記得放火力發電機或蓄電池\n5) 用邏輯電路做「低電量自動切斷工廠」可以救你一命",
                                "1) Put a Miner on an ore vein, facing a belt\n2) Belts feed a Smelter; the Smelter outputs onto the next belt\n3) Route products back to the Depot to bank them\n4) Solar dies at night — add Thermal generators or Batteries\n5) Use logic: 'cut factory power when battery is low' saves your base"),

            // ── 主選單 ────────────────────────────────────────────
            ["menu.tagline"] = ("在異星上，從手忙腳亂到帝國自轉", "From frantic hands to a self-running empire"),
            ["menu.start"] = ("開始遊戲", "Start Game"),
            ["menu.continue"] = ("繼續遊戲", "Continue"),
            ["menu.settings"] = ("設定", "Settings"),
            ["menu.quit"] = ("離開遊戲", "Quit"),
            ["menu.hint"] = ("提示：按 Esc 隨時打開設定", "Tip: press Esc anytime for settings"),

            // ── 設定 ──────────────────────────────────────────────
            ["ui.settings"] = ("設定", "Settings"),
            ["ui.tab_video"] = ("畫面", "Video"),
            ["ui.tab_audio"] = ("音效", "Audio"),
            ["ui.tab_game"] = ("遊戲", "Game"),
            ["ui.tab_lang"] = ("語言", "Language"),
            ["ui.tab_keys"] = ("操作", "Controls"),
            ["ui.quality_custom"] = ("自訂", "Custom"),
            ["ui.preset"] = ("畫質前設", "Quality preset"),
            ["ui.render_scale"] = ("渲染縮放", "Render scale"),
            ["ui.shadows"] = ("動態陰影", "Shadows"),
            ["ui.bloom"] = ("輝光效果", "Bloom"),
            ["ui.fog"] = ("大氣霧", "Atmosphere fog"),
            ["ui.msaa"] = ("反鋸齒 (MSAA)", "Anti-aliasing (MSAA)"),
            ["ui.view_distance"] = ("視距", "View distance"),
            ["ui.item_cap"] = ("物品顯示上限", "Item render cap"),
            ["ui.fullscreen"] = ("全螢幕", "Fullscreen"),
            ["ui.vsync"] = ("垂直同步", "V-Sync"),
            ["ui.gpu_note"] = ("高配顯卡可直接選「高」：開啟陰影、輝光與大氣霧，畫面會漂亮很多。低配請選「低」或讓它自動降檔。",
                               "On a strong GPU pick High: shadows, bloom and atmosphere fog make it look much better. On weak hardware pick Low or let it auto-adjust."),
            ["ui.volume_master"] = ("總音量", "Master volume"),
            ["ui.volume_music"] = ("環境音樂", "Ambient music"),
            ["ui.volume_sfx"] = ("音效", "Sound effects"),
            ["ui.audio_note"] = ("所有音效與音樂都是即時合成的（零音檔、不佔包體）。",
                                 "All audio is synthesised at runtime — no audio files, no extra download size."),
            ["ui.language"] = ("語言", "Language"),
            ["ui.lang_note"] = ("介面語言會立即切換，不需要重開遊戲。",
                                "Interface language switches instantly, no restart needed."),
            ["ui.sim_ups"] = ("模擬頻率", "Simulation rate"),
            ["ui.threat_scale"] = ("威脅強度", "Threat level"),
            ["ui.autosave"] = ("自動存檔", "Autosave"),
            ["ui.autosave_interval"] = ("自動存檔間隔（分鐘）", "Autosave interval (minutes)"),
            ["ui.show_tips"] = ("顯示新手提示", "Show beginner tips"),
            ["ui.game_note"] = ("低配機器可把模擬頻率降到 20 UPS（省電省 CPU），畫面不受影響。",
                                "On weaker machines lower the simulation rate to 20 UPS — the visuals stay smooth."),
            ["ui.reset_defaults"] = ("恢復預設", "Reset to defaults"),
            ["ui.close"] = ("關閉", "Close"),
            ["ui.saved_ok"] = ("設定已儲存", "Settings saved"),
            ["ui.on"] = ("開", "On"),
            ["ui.off"] = ("關", "Off"),

            // ── 研究 ──────────────────────────────────────────────
            ["ui.research"] = ("研究", "Research"),
            ["ui.research.belt"] = ("傳送帶速度", "Belt speed"),
            ["ui.research.machine"] = ("機器速度", "Machine speed"),
            ["ui.research.miner"] = ("採礦速度", "Mining speed"),
            ["ui.research.power"] = ("發電效率", "Power output"),
            ["ui.research.level"] = ("等級", "Lv"),
            ["ui.research.cost"] = ("耗費科研包", "Science packs"),
            ["ui.research.buy"] = ("升級", "Upgrade"),
            ["ui.research.done"] = ("研究完成", "Research complete"),
            ["ui.research.lack"] = ("科研包不足", "Not enough science packs"),
            ["ui.research.hint"] = ("用組裝機生產科研包（齒輪 + 電路）並送回倉庫。",
                                    "Produce science packs (Gear + Circuit) in an Assembler and bank them in the Depot."),
            ["ui.science_stock"] = ("科研包庫存", "Science stock"),

            // ── 工廠體檢 ──────────────────────────────────────────
            ["ui.health"] = ("工廠體檢", "Factory health"),
            ["ui.health_ok"] = ("運轉正常", "All good"),
            ["ui.health_blocked"] = ("出貨堵塞", "Output blocked"),
            ["ui.health_idle"] = ("待機", "Idle"),
            ["ui.health_none"] = ("還沒有機器", "No machines yet"),
            ["ui.health_hint"] = ("「出貨堵塞」＝後面接不上或傳送帶滿了，接一條帶子就能解決。",
                                  "'Output blocked' means the machine can't unload — add a belt or clear the line."),

            // ── 按鍵設定 ──────────────────────────────────────────
            ["ui.rebind_hint"] = ("點右邊的按鈕，然後按下你想用的鍵（Esc 取消）。",
                                  "Click a button on the right, then press the key you want (Esc cancels)."),
            ["ui.press_key"] = ("按下新按鍵…", "Press a key…"),
            ["ui.key.move_up"] = ("鏡頭前進", "Pan forward"),
            ["ui.key.move_down"] = ("鏡頭後退", "Pan back"),
            ["ui.key.move_left"] = ("鏡頭左移", "Pan left"),
            ["ui.key.move_right"] = ("鏡頭右移", "Pan right"),
            ["ui.key.rotate"] = ("旋轉朝向", "Rotate building"),
            ["ui.key.copy"] = ("複製藍圖", "Copy blueprint"),
            ["ui.key.paste"] = ("貼上藍圖", "Paste blueprint"),
            ["ui.key.design"] = ("設計模式（俯視）", "Design mode (top-down)"),
            ["ui.key.camera"] = ("切換第一人稱", "Toggle first person"),
            ["ui.key.labels"] = ("顯示建築圖標", "Toggle building icons"),
            ["ui.key.save"] = ("存檔", "Save"),
            ["ui.key.load"] = ("讀檔", "Load"),

            // ── 圖標 / 物品欄 ─────────────────────────────────────
            ["ui.labels"] = ("顯示建築圖標", "Building icons"),
            ["ui.items_panel"] = ("物品欄", "Inventory"),
            ["ui.center"] = ("回到基地", "Center on base"),

            // ── 地圖與圖釘 ────────────────────────────────────────
            ["map.title"] = ("地圖（按 M 開關）", "Map (M to toggle)"),
            ["map.hint"] = ("左鍵點地圖 = 把鏡頭移過去 ・ 右鍵 = 放/刪圖釘 ・ 滾輪 = 縮放",
                            "LMB on map = move camera there · RMB = add/remove pin · Wheel = zoom"),
            ["map.pins"] = ("圖釘", "Pins"),
            ["map.no_pins"] = ("還沒有圖釘（右鍵放一個）", "No pins yet (right-click to add)"),
            ["map.player"] = ("你", "You"),
            ["ui.key.map"] = ("打開地圖", "Open map"),
            ["ui.key.center"] = ("回到基地", "Center on base"),
            ["ui.key.tutorial"] = ("新手教學", "Tutorial"),

            // ── 新手教學 / 幫助 ───────────────────────────────────
            ["tutorial.title"] = ("新手教學", "Tutorial"),
            ["tutorial.skip"] = ("跳過教學", "Skip tutorial"),
            ["tutorial.show"] = ("顯示教學", "Show tutorial"),
            ["tutorial.progress"] = ("進度", "Progress"),
            ["tutorial.done"] = ("教學完成！接下來：蓋更多產線、升級研究、把蟲潮擋在牆外。",
                                 "Tutorial complete! Next: build more lines, upgrade research, keep the swarm out."),
            ["tutorial.step.look"] = ("用 WASD 移動鏡頭、滾輪縮放，看看你的基地",
                                      "Move the camera with WASD and zoom with the wheel — look around your base"),
            ["tutorial.step.map"] = ("按 M 打開地圖：你的建築、角色與圖釘都在上面",
                                     "Press M to open the map: your buildings, character and pins are shown"),
            ["tutorial.step.belt"] = ("蓋 5 段傳送帶（左鍵連放、R 改方向）",
                                      "Place 5 belts (hold LMB to keep going, R to rotate)"),
            ["tutorial.step.miner"] = ("在礦脈上蓋一台採礦機，出口朝傳送帶",
                                       "Put a Miner on an ore vein, facing a belt"),
            ["tutorial.step.smelter"] = ("蓋熔煉爐：後面接礦、前面接出貨帶",
                                         "Add a Smelter: ore comes in from behind, output goes out the front"),
            ["tutorial.step.depot"] = ("把成品接回中樞倉庫（才算入庫存）",
                                       "Route the product back to the Depot to bank it"),
            ["tutorial.step.research"] = ("累積科研包，在「研究」面板升級（永久強化）",
                                          "Bank science packs and upgrade in the Research panel (permanent)"),
            ["tutorial.step.turret"] = ("蓋砲塔並接上砲彈：蟲潮會越來越大",
                                        "Build a Turret and feed it ammo — waves keep growing"),
            ["tutorial.step.done"] = ("完成！按 ? 可以隨時打開這份說明",
                                      "Done! Press ? anytime to reopen this help"),
            ["help.title"] = ("怎麼玩", "How to play"),
            ["help.open"] = ("說明", "Help"),
            ["help.goal"] = ("目標：在這顆異星上，從手動採集一路發展成能自己運轉的工業帝國。",
                             "Goal: start from manual gathering and grow into a self-running industrial empire."),
            ["help.loop"] = ("核心循環：礦脈 → 採礦機 → 傳送帶 → 熔煉爐 → 組裝機 → 中樞倉庫（成品入庫存，蓋東西從庫存扣）。",
                             "Core loop: ore vein → Miner → Belt → Smelter → Assembler → Depot (products bank into your stock; building costs come out of it)."),
            ["help.power"] = ("電力：太陽能晚上會失效，記得補火力發電機或蓄電池；電不夠所有機器都會變慢。",
                              "Power: solar dies at night — add Thermal generators or Batteries; when power is short everything slows down."),
            ["help.logic"] = ("邏輯電路：感測器（讀電池 %）→ 比較器（大於/小於/等於）→ 執行器（開關某類機器）。開局那組就是「電池 < 30% 自動停熔煉爐」。",
                              "Logic: Sensor (reads battery %) → Comparator (> < =) → Actuator (switches a machine type). The starter set cuts Smelters when the battery drops below 30%."),
            ["help.weather"] = ("天氣：風暴與灰燼風暴會讓傳送帶變慢，離子風暴會讓砲塔間歇失效 —— 記得留後備。",
                                "Weather: storms slow belts, ion storms disable turrets intermittently — plan a backup."),
            ["help.planet"] = ("兩顆星球：灰燼九號太陽能只有 35%、威脅 1.9 倍，但礦更富 —— 換星球等於換一套設計。",
                               "Two planets: Cinder-9 has only 35% solar and 1.9× threat but richer ore — a different design challenge."),
            ["help.blueprint"] = ("藍圖：C 複製附近 8 格內的建築到剪貼簿，V 貼上（可以分享給朋友）。",
                                  "Blueprints: C copies buildings within 8 tiles to your clipboard, V pastes them (shareable)."),
            ["help.dir"] = ("方向（最重要）：每台機器都有「進料」與「出貨」兩邊。放下去之前看地上的箭頭 —— 綠色＝出貨、藍色＝進料；傳送帶的帶面上有往輸送方向跑的條紋。按 R 或底部「放置方向」按鈕可以轉 90°。採礦機／熔煉爐／組裝機的綠色箭頭那一格一定要接傳送帶，不然產品會堆在機器裡。",
                            "Direction (most important): every machine has an input and an output side. Before placing, look at the arrows on the ground — green = output, blue = input; belts show moving chevrons toward the flow direction. Press R or the bottom 'Direction' button to rotate 90°. The tile the green arrow points to must have a belt, or the machine will jam."),
            ["help.first"] = ("第一次玩就照這個順序：① 走到鐵礦脈旁 → ② 選「傳送帶」，沿著一條直線鋪幾格 → ③ 選「採礦機」放在礦脈上，讓綠色箭頭對準那條傳送帶 → ④ 帶子末端放「熔煉爐」（藍箭頭朝帶子）→ ⑤ 熔爐出口接帶子到「中樞倉庫」。倉庫裡的鐵板就是建造材料。",
                               "First run: 1) walk to an iron vein. 2) Pick Belt and lay a few tiles in a straight line. 3) Pick Miner, put it on the vein and aim the green arrow at that belt. 4) Put a Smelter at the end of the belt (blue arrow facing the belt). 5) Run a belt from the smelter's output into the Depot. Iron plates banked there are your build material."),
            ["help.deco"] = ("地上的石堆、水晶與植物只是裝飾，不會阻擋建造；礦脈則是會發亮的一堆礦石（鐵＝藍灰、銅＝橙鏽、煤＝全黑、石＝灰）。滑鼠移到上面，右側會顯示那是什麼。",
                             "Rocks, crystals and plants on the ground are pure decoration — they never block building. Ore veins are the glowing piles (iron = blue-grey, copper = rusty orange, coal = black, stone = grey). Hover one and the right panel tells you what it is."),
            ["help.ui"] = ("介面：每個面板左/上端都有一個小箭嘴，按下去整個面板會收起來；開啟新面板時會自動收起被蓋住的。F2 可以一次隱藏全部介面。",
                           "Panels: each one has a tiny arrow at its top edge — click it to fold the whole panel away. Opening a panel auto-hides the ones it would cover. F2 hides the entire UI."),
            ["help.tips"] = ("小技巧：滑鼠移到建築上看詳細狀態；「研究」面板看升級；底部「顯示建築圖標」幫你認出誰是誰。",
                             "Tips: hover a building to see its state; the Research panel is for upgrades; the icons button helps you tell machines apart."),
            ["ui.lang_auto"] = ("跟隨系統語言", "Follow system language"),
            ["ui.lang_detected"] = ("偵測到系統語言：", "Detected system language: "),
            ["ui.pin_added"] = ("已放置圖釘", "Pin added"),
            ["ui.pin_removed"] = ("已移除圖釘", "Pin removed"),
            ["ui.map"] = ("地圖 (M)", "Map (M)"),
            ["ui.help_open"] = ("說明 (?)", "Help (?)"),

            // ══════════ v0.4.0 新增 ══════════
            // ── 主選單 / 存檔 ─────────────────────────────────────
            ["menu.new"] = ("新遊戲", "New game"),
            ["menu.continue"] = ("繼續遊戲", "Continue"),
            ["menu.saves"] = ("存檔管理", "Save files"),
            ["menu.back"] = ("返回", "Back"),
            ["menu.quit_to_menu"] = ("回到主選單", "Return to main menu"),
            ["menu.no_save"] = ("（還沒有存檔）", "(no save yet)"),
            ["menu.export"] = ("匯出存檔檔", "Export save file"),
            ["menu.import"] = ("匯入存檔檔", "Import save file"),
            ["menu.delete"] = ("刪除", "Delete"),
            ["menu.overwrite"] = ("覆蓋這個存檔？", "Overwrite this save?"),
            ["menu.play_time"] = ("遊玩時間", "Play time"),
            ["menu.exported"] = ("已匯出到：", "Exported to: "),
            ["menu.imported"] = ("已匯入存檔", "Save imported"),
            ["menu.import_fail"] = ("匯入失敗（檔案格式不對）", "Import failed (bad file)"),
            ["menu.continue_hint"] = ("繼續上次的進度（含位置與視角）", "Resume where you left off (position & camera included)"),
            ["menu.saves_hint"] = ("每個星球 3 個欄位；匯出的檔案可以備份或傳給朋友。",
                                   "3 slots per planet; exported files can be backed up or shared."),
            ["save.slot"] = ("欄位", "Slot"),
            ["save.empty"] = ("空", "Empty"),
            ["save.saved_at"] = ("存於", "Saved"),
            ["ui.save_slot_used"] = ("已存檔到欄位", "Saved to slot"),

            // ── 遊戲內選單 / 介面控制 ─────────────────────────────
            ["ui.wave_incoming"] = ("蟲潮來襲", "Alien wave incoming"),
            ["ui.building_destroyed"] = ("建築被摧毀", "A building was destroyed"),
            ["ui.menu"] = ("選單", "Menu"),
            ["ui.hide_ui"] = ("隱藏介面", "Hide UI"),
            ["ui.ui_hidden"] = ("介面已隱藏（F2 或點畫面顯示）", "UI hidden (F2 or click to show)"),
            ["ui.panel_help"] = ("說明", "Help"),
            ["ui.panel_tutorial"] = ("教學", "Tutorial"),
            ["ui.panel_items"] = ("圖鑑", "Codex"),
            ["ui.panel_music"] = ("音樂", "Music"),
            ["ui.panel_research"] = ("研究", "Research"),

            // ── 音樂播放器 ────────────────────────────────────────
            ["music.now"] = ("正在播放", "Now playing"),
            ["music.play"] = ("播放", "Play"),
            ["music.pause"] = ("暫停", "Pause"),
            ["music.next"] = ("下一首", "Next"),
            ["music.volume"] = ("音樂音量", "Music volume"),
            ["music.track.0"] = ("異星黎明", "Alien Dawn"),
            ["music.track.1"] = ("深空迴響", "Deep Space Echo"),
            ["music.track.2"] = ("機械律動", "Machine Pulse"),
            ["music.track.3"] = ("晶體森林", "Crystal Forest"),

            // ── 模型精細度 ────────────────────────────────────────
            ["ui.detail"] = ("模型精細度", "Model detail"),
            ["ui.detail_low"] = ("低（基本模型）", "Low (basic models)"),
            ["ui.detail_mid"] = ("中", "Mid"),
            ["ui.detail_high"] = ("高（精緻模型）", "High (detailed models)"),
            ["ui.detail_note"] = ("低：用最簡單的幾何模型，老舊電腦也能跑；高：完整的工業模型與裝飾零件。",
                                   "Low uses the simplest geometry so even old machines run it; High uses the full industrial models with detail parts."),
            ["ui.music_note"] = ("音樂與音效都是即時合成的，可在這裡單獨調整或關閉。",
                                 "Music and sound effects are synthesised at runtime — adjust or mute them here."),

            // ── 建造資訊 / 方向 ───────────────────────────────────
            ["ui.collapse_hint"] = ("折疊／展開這個面板", "Collapse / expand this panel"),
            ["ui.facing"] = ("朝向", "Facing"),
            ["ui.facing_btn"] = ("放置方向", "Direction"),
            ["ui.facing_btn_hint"] = ("按一下轉 90°（也可以按 R 鍵）", "Click to rotate 90° (or press R)"),
            ["ui.io_in"] = ("進料 ← ", "input ← "),
            ["ui.io_out"] = ("出貨 → ", "output → "),
            ["ui.ore_here"] = ("礦脈：", "Ore vein: "),
            ["ui.ore_hint"] = ("（把採礦機放在這裡）", " (put a Miner here)"),
            ["ui.hint_out_blocked"] = ("⚠ 出貨方向沒有傳送帶！綠色箭頭指的那一格要放傳送帶（R 可轉方向）",
                                       "⚠ No belt on the output side! Place a belt on the tile the green arrow points to (R rotates)."),
            ["ui.deco_note"] = ("地上的石堆與水晶是裝飾，不影響遊玩。",
                                "Rock piles and crystals on the ground are just scenery."),
            ["ui.received"] = ("收到", "Got"),
            ["ui.facing_hint"] = ("放置方向：橙色箭頭＝出貨方向、藍色箭頭＝進料方向；按 R 或這個按鈕旋轉",
                                  "Placement: orange arrow = output, blue arrow = input. Press R or this button to rotate."),
            ["dir.0"] = ("北", "North"), ["dir.1"] = ("東", "East"),
            ["dir.2"] = ("南", "South"), ["dir.3"] = ("西", "West"),
            ["music.combat"] = ("戰鬥中", "In combat"),
            ["music.peace"] = ("環境", "Ambient"),
            ["ui.hp"] = ("生命", "Health"),
            ["ui.hp_lost"] = ("你被蟲潮擊倒了！", "You were taken down by the swarm!"),
            ["ui.hp_hint"] = ("蟲潮會攻擊你本人：被咬到會少一顆心，兩顆心都沒了遊戲就結束。躲遠一點或先清掉牠們。",
                              "Aliens attack you directly: each bite costs a heart. Lose both and the run ends — stay clear or clear them out."),
            ["ui.dead_revive"] = ("在基地復活（失去一半庫存）", "Revive at the base (lose half your stock)"),
            ["ui.dead_load"] = ("讀取上一個存檔", "Load last save"),
            ["ui.dead_menu"] = ("回到主選單", "Back to main menu"),
            ["ui.cost"] = ("建造材料", "Build cost"),
            ["ui.free"] = ("不需要材料", "No materials needed"),
            ["ui.direction"] = ("方向", "Direction"),
            ["ui.dir.n"] = ("北", "North"),
            ["ui.dir.e"] = ("東", "East"),
            ["ui.dir.s"] = ("南", "South"),
            ["ui.dir.w"] = ("西", "West"),

            // ── 圖鑑（每個東西是做什麼的）─────────────────────────
            ["codex.title"] = ("圖鑑：每個東西是做什麼的", "Codex: what everything does"),
            ["codex.items"] = ("物品", "Items"),
            ["codex.buildings"] = ("建築", "Buildings"),
            ["codex.used_in"] = ("用途", "Used for"),
            ["codex.cost"] = ("造價", "Cost"),
            ["item.desc.1"] = ("最基本的礦石。熔煉成鐵板後幾乎所有建築都要用。", "The most basic ore. Smelt it into iron plates — almost everything needs them."),
            ["item.desc.2"] = ("熔煉成銅板，用來做電路。", "Smelt into copper plates for circuits."),
            ["item.desc.3"] = ("火力發電機的燃料，也可以直接燒來發電。", "Fuel for thermal generators."),
            ["item.desc.4"] = ("便宜的建材，部分配方與建築會用到。", "Cheap building material used in some recipes."),
            ["item.desc.5"] = ("由鐵礦熔煉而來，是所有機械的骨架。", "Smelted from iron ore — the skeleton of every machine."),
            ["item.desc.6"] = ("由銅礦熔煉而來，電子零件的基礎。", "Smelted from copper ore — the base for electronics."),
            ["item.desc.7"] = ("三個鐵板做成齒輪，機械、砲彈與科研包的零件。", "Three iron plates make a gear — used in machines, ammo and science packs."),
            ["item.desc.8"] = ("兩個銅板做成電路，科研包與高階建築都要。", "Two copper plates make a circuit — needed by science and advanced buildings."),
            ["item.desc.9"] = ("齒輪 + 電路做成科研包，用來在「研究」面板永久強化。", "Gear + circuit makes a science pack — spend them on permanent research upgrades."),
            ["item.desc.10"] = ("齒輪 + 銅板做成砲彈（在組裝機，Ctrl+滾輪 切配方），送進砲塔供它射擊。", "Gear + copper plate makes ammo (assembler, Ctrl+scroll to switch recipe) — feed it into turrets."),
            ["item.desc.11"] = ("只有灰燼星才有的稀有礦，熔煉成鈦板後價值極高，是致富關鍵。", "A rare ore found only on the Ash planet; smelts into high-value titanium plates."),
            ["item.desc.12"] = ("由鈦礦熔煉，賣價高昂。未來將用於高階建築。", "Smelted from titanium ore; sells for a lot of coin. Reserved for advanced buildings."),
            ["item.desc.13"] = ("翡翠七星的特產。熔煉成矽晶圓，是電子產業的原料。", "Found on Viridian-7. Smelts into silicon wafers — the raw material of electronics."),
            ["item.desc.14"] = ("由矽礦熔煉。與電路組合成「高級電路」，價值翻好幾倍。", "Smelted from silicon; combine with a circuit to make an advanced circuit worth far more."),
            ["item.desc.15"] = ("霜蝕四號的特產。熔煉成晶體，也能直接賣錢。", "Found on Glacia-4. Smelts into crystal, or just sell it as-is."),
            ["item.desc.16"] = ("由晶石礦熔煉。用來做「穿甲彈」（傷害加倍）。", "Smelted from crystal ore — the key ingredient of heavy ammo (double damage)."),
            ["item.desc.17"] = ("霜蝕四號與深淵零號才有的貴重礦。熔煉成金板。", "A precious ore on Glacia-4 and Nyx-0. Smelts into gold plates."),
            ["item.desc.18"] = ("由金礦熔煉，是中期最穩定的高價商品。", "Smelted from gold ore — a steady high-value mid-game product."),
            ["item.desc.19"] = ("深淵零號才有的終極礦脈，極其稀有。", "The rarest ore, found only on Nyx-0."),
            ["item.desc.20"] = ("由星核礦熔煉，全遊戲最值錢的商品。", "Smelted from core ore — the most valuable product in the game."),
            ["item.desc.21"] = ("電路 + 矽晶圓做成的高階零件，賣價極高。", "Circuit + wafer makes this advanced part — sells for a fortune."),
            ["item.desc.22"] = ("砲彈 + 晶體做成，傷害是普通砲彈的兩倍，對付熔岩蟲與星核獸必備。", "Ammo + crystal makes heavy ammo with double damage — essential against magma worms and Titans."),
            ["building.desc.1"] = ("蓋在礦脈上，會自動把礦挖出來放到前方的傳送帶。", "Place it on an ore vein; it mines automatically and pushes ore onto the belt in front."),
            ["building.desc.2"] = ("把物品一段一段往下游送。風暴會讓它變慢。", "Moves items downstream segment by segment. Storms slow it down."),
            ["building.desc.3"] = ("把礦石熔煉成板材：鐵礦→鐵板、銅礦→銅板、鈦礦→鈦板。會自動辨識送上帶子的礦種，也可用 Ctrl+滾輪 手動指定配方。要通電。", "Smelts ore into plates (iron/copper/titanium). It auto-detects the ore on the input belt; Ctrl+wheel sets the recipe manually. Needs power."),
            ["building.desc.4"] = ("把材料組裝成齒輪、電路、科研包、砲彈。", "Assembles gears, circuits, science packs and ammo."),
            ["building.desc.5"] = ("成品送進來就變成你的庫存；蓋東西會從庫存扣材料。", "Products delivered here become your stock; building costs are paid from it."),
            ["building.desc.6"] = ("白天發電，晚上失效；沙塵與風暴會大幅降低輸出。", "Generates power by day, dies at night; dust and storms cut output."),
            ["building.desc.7"] = ("燒煤發電，日夜都能運轉，是夜間的主力。", "Burns coal for steady day-and-night power."),
            ["building.desc.8"] = ("儲存多餘電力，讓晚上的工廠還能運轉。", "Stores surplus power so the factory keeps running at night."),
            ["building.desc.9"] = ("自動射擊靠近的蟲潮。新蓋好的砲塔會免費附贈 20 發彈藥；之後用傳送帶把「砲彈」送進藍色進料口就會自動補給（一次補滿 30 發）。", "Automatically shoots nearby aliens. A freshly built turret comes with 20 free rounds; feed ammo into the blue input port by belt to refill (30 per pickup)."),
            ["building.desc.10"] = ("照亮基地，晚上也能看清。", "Lights up your base so you can see at night."),
            ["building.desc.11"] = ("邏輯電路的第一步：讀取庫存、電量等數值送到通道。", "Logic step 1: reads a value (battery, stock, wave) into a channel."),
            ["building.desc.12"] = ("比較兩個通道的數值（大於／小於／等於）並輸出結果。", "Compares two channels (>, <, =) and outputs the result."),
            ["building.desc.13"] = ("依比較結果開關某一類機器——例如低電量時關掉熔煉爐。", "Switches a machine type based on the result — e.g. cut smelters when power is low."),
            ["building.desc.14"] = ("把物品傳送到另一顆星球的倉庫（玩家不會被傳送）。", "Sends items to the other planet's depot (players are not teleported)."),

            // ── 蟲潮種類（2D 版新增：多種怪物）─────────────────────
            ["alien.0"] = ("潮蟲", "Grunt"),
            ["alien.1"] = ("迅蟲", "Runner"),
            ["alien.2"] = ("毒液蟲", "Spitter"),
            ["alien.3"] = ("重甲蟲", "Brute"),
            ["alien.4"] = ("巢主", "Elite"),
            ["alien.5"] = ("自爆蟲", "Bomber"),
            ["alien.6"] = ("飛行蟲", "Flyer"),
            ["alien.7"] = ("霜蝕蟲", "Frost"),
            ["alien.8"] = ("熔岩蟲", "Magma"),
            ["alien.9"] = ("星核獸", "Titan"),
            ["alien.desc.0"] = ("最常見的炮灰，成群結隊衝向最近的建築。", "The common swarm drone — charges the nearest building in packs."),
            ["alien.desc.1"] = ("體型小但速度極快，專挑沒有砲塔的漏洞直插產線。", "Small but very fast — slips through gaps in your defences to hit the production line."),
            ["alien.desc.2"] = ("中速中血，會啃食建築外牆，是中期的主力威脅。", "Mid-tier threat that chews through buildings."),
            ["alien.desc.3"] = ("皮糙肉厚、走得慢但攻擊極高，能扛住好幾發砲彈。", "Slow but heavily armoured — tanks several turret shots before falling."),
            ["alien.desc.4"] = ("後期出現的菁英，接近即造成重創，務必集火。", "Late-game elite — focus fire before it reaches your machines."),
            ["alien.desc.5"] = ("衝到建築旁就自爆，單次傷害全遊戲最高——別讓牠靠近產線。", "Detonates next to your buildings for the highest single hit in the game — never let it reach the line."),
            ["alien.desc.6"] = ("最快最脆，會繞過外圍防線直搗內部機器。砲塔要往內層補。", "Fastest and frailest; slips past outer defences to hit inner machines. Layer turrets inward."),
            ["alien.desc.7"] = ("冰甲極厚，普通砲彈打很久；改用穿甲彈效果最好。", "Its icy carapace soaks up normal shells — switch to heavy ammo."),
            ["alien.desc.8"] = ("厚血高攻的熔岩巨獸，需要多座砲塔與穿甲彈同時集火。", "A tanky, hard-hitting magma brute — needs several turrets and heavy ammo."),
            ["alien.desc.9"] = ("終局 BOSS 級巨獸，血量與傷害都極誇張，只有成排的穿甲彈砲塔擋得住。", "Endgame boss: absurd HP and damage. Only rows of heavy-ammo turrets can hold it."),

            // ── 2D 版 UI 補充 ─────────────────────────────────────
            ["ui.tool.select"] = ("選擇建造工具（1-9），右鍵／X 拆除", "Pick a build tool (1-9); RMB / X to demolish"),
            ["ui.tool.demolish"] = ("拆除", "Demolish"),
            ["ui.info_title"] = ("資訊", "Info"),
            ["ui.minimap"] = ("小地圖 (M)", "Minimap (M)"),
            ["ui.research_open"] = ("研究 (U)", "Research (U)"),
            ["ui.codex_open"] = ("圖鑑 (?)", "Codex (?)"),
            ["ui.night"] = ("夜晚", "Night"),
            ["ui.daytime"] = ("白天", "Day"),
            ["ui.wave_next"] = ("下一波", "Next wave"),
            ["ui.controls"] = ("操作", "Controls"),

            // ── 金幣經濟 / 星球 / 存檔 / 失敗 ────────────────────
            ["ui.coins"] = ("金幣", "Coins"),
            ["ui.sell"] = ("交易 (G)：賣礦物換金幣", "Trade (G): sell minerals for coin"),
            ["ui.sell_hint"] = ("把庫存賣成金幣，再用金幣蓋建築。", "Sell stock for coin, then spend coin to build."),
            ["ui.travel"] = ("星球 (P)", "Planet (P)"),
            ["ui.travel_hint"] = ("遷移到另一顆星球（保留金幣與研究）。", "Travel to the other planet (keeps coin & research)."),
            ["ui.save"] = ("存檔 (F5)", "Save (F5)"),
            ["ui.saved"] = ("已存檔", "Saved"),
            ["ui.autosaved"] = ("已自動存檔", "Auto-saved"),
            ["ui.defeat"] = ("你的基地被蟲潮摧毀了", "Your base was destroyed by the swarm"),
            ["ui.defeat_hint"] = ("按 R 重新開始　或　按 L 載入上次存檔", "Press R to restart, or L to load last save"),
            ["ui.continue"] = ("繼續遊戲", "Continue"),
            ["ui.new_game"] = ("新遊戲", "New Game"),
            ["ui.choose_planet"] = ("選擇星球", "Choose Planet"),
            ["ui.no_save"] = ("尚無存檔", "No save yet"),
            ["ui.coins_lack"] = ("金幣不足", "Not enough coin"),

            // ── 教學：階段與提示 ─────────────────────────────────
            ["tutorial.stages"] = ("教學階段", "Tutorial stages"),
            ["tutorial.page_hint"] = ("點下面的階段名稱，就可以重新看那一段教學。",
                                      "Click a stage below to replay that part of the tutorial."),
            ["tutorial.stage"] = ("階段", "Stage"),
            ["tutorial.replay"] = ("重看", "Replay"),
            ["tutorial.done_mark"] = ("已完成", "Completed"),
            ["tutorial.arrow_hint"] = ("照著底部「閃爍的按鈕」做，工具會自動幫你選好",
                                       "Follow the blinking button at the bottom — the tool is auto-selected for you"),
            ["tutorial.step.power"] = ("蓋一台火力發電機或蓄電池（晚上太陽能會失效）",
                                       "Build a Thermal Generator or a Battery (solar dies at night)"),
            ["tutorial.step.codex"] = ("按「圖鑑」看看每個物品是做什麼的",
                                       "Open the Codex to see what every item does"),
            ["tutorial.s1.name"] = ("階段 1：認識環境", "Stage 1: Get your bearings"),
            ["tutorial.s2.name"] = ("階段 2：第一條產線", "Stage 2: Your first production line"),
            ["tutorial.s3.name"] = ("階段 3：材料與建造", "Stage 3: Materials and building"),
            ["tutorial.s4.name"] = ("階段 4：電力與研究", "Stage 4: Power and research"),
            ["tutorial.s5.name"] = ("階段 5：防禦與擴張", "Stage 5: Defence and expansion"),

            // ── 設定說明補充 ─────────────────────────────────────
            ["ui.note_render_scale"] = ("低於 100% 會用較少像素渲染再放大，是低配機器最有效的加速方式。",
                                        "Below 100% renders fewer pixels and upscales — the single best speed-up on weak machines."),
            ["ui.note_shadows"] = ("關掉可以省不少效能，但畫面會少一層立體感。",
                                   "Turning shadows off saves performance but the scene looks flatter."),
            ["ui.note_view_distance"] = ("看多遠。越大越耗效能，對「找礦」有幫助。",
                                         "How far you can see. Larger costs performance but helps you spot ore."),
            ["ui.note_ups"] = ("每秒模擬次數。低配降到 20 可以省 CPU，但機器動作會看起來慢一點。",
                               "Simulation ticks per second. Dropping to 20 saves CPU on weak machines; machines look slightly slower."),
            ["ui.note_threat"] = ("蟲潮的強度倍率。想輕鬆建造可以調低。",
                                  "Multiplier for alien waves. Lower it if you just want to build in peace."),
            ["ui.note_autosave"] = ("每隔幾分鐘自動存一次，斷電也不會丟進度。",
                                    "Saves automatically every few minutes so a crash never loses progress."),
            ["ui.note_zoom"] = ("鏡頭縮放有上下限，避免縮到看不見或穿進地面。",
                                "Camera zoom is clamped so you can never lose sight of your base."),
            ["ui.clouds"] = ("雲層", "Clouds"),
            ["ui.note_clouds"] = ("天空的雲會隨時間飄動，晚上還有星空。低配可以關掉。",
                                  "Clouds drift across the sky and stars come out at night. Turn off on weak hardware."),
            ["ui.music_autoplay"] = ("進入遊戲自動播放音樂", "Autoplay music in game"),
            ["ui.music_autoplay_note"] = ("關掉的話可以用畫面右下角的音樂播放器手動播放。",
                                          "If off, start it manually from the music player at the bottom-right."),
            ["ui.save_on_exit"] = ("離開遊戲時自動存檔", "Save automatically when quitting"),
            ["ui.note_save_on_exit"] = ("關掉視窗也會把進度與位置存起來，下次可以「繼續遊戲」。",
                                        "Even closing the window keeps your progress and position for Continue."),
            ["ui.saved_to"] = ("已存檔", "Saved"),
            ["ui.key.rot_left"] = ("向左轉視角", "Rotate view left"),
            ["ui.key.rot_right"] = ("向右轉視角", "Rotate view right"),
            ["ui.key.hideui"] = ("隱藏／顯示介面", "Hide / show UI"),
            ["ui.design_hint"] = ("設計模式：左鍵放置、滑鼠中鍵拖曳轉視角、Q/E 旋轉、滾輪縮放",
                                  "Design mode: LMB place · MMB drag to orbit · Q/E rotate · wheel zoom"),
        };
    }
}
