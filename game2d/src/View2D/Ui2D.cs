// Ui2D：2D 版完整遊戲介面（中文）。
//  · 主選單（繼續遊戲 / 新遊戲選星球）＋ 失敗畫面
//  · 頂列（金幣/電力/研究/波次/動態威脅）＋ 建造工具列 ＋ 懸停資訊欄
//  · 交易面板（G，賣礦換金幣）＋ 星球遷移（P）＋ 研究（U）＋ 圖鑑（F1）＋ 說明（H）＋ 設定（Esc）
using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class Ui2D : CanvasLayer
    {
        private SimWorld _world;
        private ChunkManager _chunks;
        private WorldGrid2D _grid;
        private CameraController2D _cam;

        // ── 建造選擇狀態（Main2D 讀取）────────────────────────
        public BuildingKind SelectedTool = BuildingKind.Belt;
        public bool DemolishMode;
        public Dir Facing = Dir.East;
        public byte RecipeIndex;

        /// <summary>取貨模式：從倉庫拿出一種物品、用左鍵放到場上（ItemId.None = 未啟用）。</summary>
        public ItemId WithdrawItem = ItemId.None;
        public bool WithdrawMode => WithdrawItem != ItemId.None;

        public bool UiHidden;

        // ── 節點 ────────────────────────────────────────────────
        private Control _root;
        private Label _topBar;
        private Label _infoTitle;
        private Label _infoTool;
        private Label _infoBody;
        private PanelContainer _infoPanel;
        private VBoxContainer _feedBox;
        private PanelContainer _researchPanel;
        private VBoxContainer _researchBox;
        private PanelContainer _codexPanel;
        private PanelContainer _helpPanel;
        private PanelContainer _settingsPanel;
        private PanelContainer _titlePanel;
        private Button _continueButton;
        private PanelContainer _sellPanel;
        private PanelContainer _livePanel;
        private VBoxContainer _liveBox;
        private Label _liveEventLabel;
        private Label _liveContractLabel;
        private Label _liveHintLabel;
        private float _liveTimer;
        private readonly List<Label> _sellStockLabels = new();
        private readonly List<Label> _sellPriceLabels = new();
        private Label _debtLabel;
        private PanelContainer _planetPanel;
        private VBoxContainer _planetList;
        private PanelContainer _defeatPanel;
        private Minimap2D _minimap;
        // 成就 / 科技樹 / 跨星球傳送門
        private PanelContainer _achPanel;
        private VBoxContainer _achBox;
        private Label _achProgressLabel;
        private readonly List<(Label name, Label desc, Label prog)> _achRows = new();
        private PanelContainer _techPanel;
        private VBoxContainer _techBox;
        private PanelContainer _portalPanel;
        private VBoxContainer _portalBox;
        private OptionButton _portalPlanetBtn;
        private OptionButton _portalItemBtn;
        private SpinBox _portalCountSpin;
        private float _achTimer;
        private GridContainer _toolbar;
        private Label _selectionLabel;
        private Label _objectiveLabel;
        private Label _objectiveHint;
        private PanelContainer _objectivePanel;
        private readonly List<Button> _toolButtons = new();

        // ── 倉庫取貨 ────────────────────────────────────────────
        private readonly List<(ItemId item, Button btn)> _withdrawButtons = new();

        // ── 星球卡片 ────────────────────────────────────────────
        private readonly List<Action> _planetCardUpdaters = new();
        private int _planetListPlanet = -1;
        private const float PlanetThumbSize = 78f;

        // ── 蟲潮待確認面板 ──────────────────────────────────────
        private PanelContainer _wavePanel;
        private Label _waveTitleLabel;
        private Label _waveBodyLabel;

        private readonly List<(string text, float ttl)> _toasts = new();
        private int _lastBuildingCount = -1;
        private float _refreshTimer;

        // ── 給 Main2D 的回呼 ───────────────────────────────────
        public event Action ContinueRequested;
        public event Action<int> NewGameRequested;    // 星球 index
        public event Action<int> TravelRequested;     // 星球 index
        public event Action<int> PortalRequested;     // 開通傳送門（星球 index）
        public event Action LoadRequested;
        public event Action MenuRequested;            // 回到主選單
        public event Action WaveAccepted;             // 蟲潮警報：玩家按下「立即迎戰」
        public event Action WaveDelayed;              // 蟲潮警報：玩家按下「延後」
        public Action<string> PlaySfx;                 // 由 Main2D 接到 Audio2D

        public bool TitleVisible => _titlePanel != null && _titlePanel.Visible;
        public bool DefeatVisible => _defeatPanel != null && _defeatPanel.Visible;

        private static readonly (BuildingKind kind, string label)[] Tools =
        {
            (BuildingKind.Belt, "1 傳送帶"),
            (BuildingKind.Miner, "2 採礦機"),
            (BuildingKind.Smelter, "3 熔煉爐"),
            (BuildingKind.Assembler, "4 組裝機"),
            (BuildingKind.Depot, "5 倉庫"),
            (BuildingKind.SolarPanel, "6 太陽能"),
            (BuildingKind.ThermalGenerator, "7 火力"),
            (BuildingKind.Battery, "8 蓄電池"),
            (BuildingKind.Turret, "9 砲塔"),
            (BuildingKind.Lamp, "0 照明"),
            (BuildingKind.SensorNode, "感測器"),
            (BuildingKind.ComparatorNode, "比較器"),
            (BuildingKind.ActuatorNode, "執行器"),
        };

        public void Setup(SimWorld world, ChunkManager chunks, WorldGrid2D grid, CameraController2D cam)
        {
            _world = world;
            _chunks = chunks;
            _grid = grid;
            _cam = cam;
            Fonts2D.Init();
            BuildAll();
        }

        public void SetWorld(SimWorld world, ChunkManager chunks, WorldGrid2D grid, CameraController2D cam)
        {
            // 換星球／讀檔：清除取貨模式與蟲潮警報面板（舊世界的狀態不能留著）
            ClearWithdraw(true);
            HideWavePanel();
            _planetListPlanet = -1;
            _world = world;
            _chunks = chunks;
            _grid = grid;
            _cam = cam;
            _toasts.Clear();
            _lastBuildingCount = -1;
            // 重建小地圖（它快取了世界/相機參考與礦脈）
            if (_minimap != null)
            {
                _root.RemoveChild(_minimap);
                _minimap.QueueFree();
                BuildMinimap();
            }
        }

        private void BuildAll()
        {
            _root = new Control();
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _root.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_root);
            _toolButtons.Clear();
            _sellStockLabels.Clear();
            _sellPriceLabels.Clear();
            _withdrawButtons.Clear();
            _planetCardUpdaters.Clear();
            _planetListPlanet = -1;

            BuildTopBar();
            BuildToolbar();
            BuildInfoPanel();
            BuildFeed();
            BuildObjectives();
            BuildResearchPanel();
            BuildCodexPanel();
            BuildHelpPanel();
            BuildSettingsPanel();
            BuildMinimap();
            BuildSellPanel();
            BuildLivePanel();
            BuildAchPanel();
            BuildTechPanel();
            BuildPortalPanel();
            BuildPlanetPanel();
            BuildWavePanel();
            BuildTitleMenu();
            BuildDefeatPanel();
            BuildHints();
        }

        public void Rebuild()
        {
            bool s = _settingsPanel.Visible, r = _researchPanel.Visible, c = _codexPanel.Visible,
                 h = _helpPanel.Visible, m = _minimap.Visible, sell = _sellPanel.Visible, pl = _planetPanel.Visible;
            _root.QueueFree();
            BuildAll();
            _settingsPanel.Visible = s; _researchPanel.Visible = r; _codexPanel.Visible = c;
            _helpPanel.Visible = h; _minimap.Visible = m; _sellPanel.Visible = sell; _planetPanel.Visible = pl;
            RefreshToolbar();
        }

        // ── 頂列 ────────────────────────────────────────────────
        private void BuildTopBar()
        {
            _topBar = Fonts2D.Label("", 14, new Color(0.92f, 1f, 0.95f));
            _topBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _topBar.Position = new Vector2(10, 8);
            _root.AddChild(_topBar);
        }

        // ── 工具列 ──────────────────────────────────────────────
        private void BuildToolbar()
        {
            _toolbar = new GridContainer { Columns = 7 };
            _toolbar.AddThemeConstantOverride("h_separation", 4);
            _toolbar.AddThemeConstantOverride("v_separation", 4);
            _toolbar.AnchorLeft = 0.5f; _toolbar.AnchorTop = 1f;
            _toolbar.AnchorRight = 0.5f; _toolbar.AnchorBottom = 1f;
            _toolbar.OffsetTop = -76f; _toolbar.OffsetBottom = -8f;
            _toolbar.GrowHorizontal = Control.GrowDirection.Both;
            _toolbar.GrowVertical = Control.GrowDirection.Begin;
            _root.AddChild(_toolbar);

            for (int i = 0; i < Tools.Length; i++)
            {
                var t = Tools[i];
                var b = MakeToolButton(t.label);
                int idx = i;
                b.Pressed += () => SelectTool(Tools[idx].kind);
                _toolbar.AddChild(b);
                _toolButtons.Add(b);
            }
            var demolish = MakeToolButton("X 拆除");
            demolish.Pressed += ToggleDemolish;
            _toolbar.AddChild(demolish);
            _toolButtons.Add(demolish);

            RefreshToolbar();
        }

        private Button MakeToolButton(string text)
        {
            var b = new Button { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(76, 30) };
            b.AddThemeFontOverride("font", Fonts2D.Small);
            b.AddThemeFontSizeOverride("font_size", 12);
            return b;
        }

        public void SelectTool(BuildingKind kind)
        {
            ClearWithdraw(true);
            SelectedTool = kind;
            DemolishMode = false;
            if (kind != BuildingKind.Smelter && kind != BuildingKind.Assembler) RecipeIndex = 0;
            RefreshToolbar();
        }

        public void ToggleDemolish()
        {
            if (!DemolishMode) ClearWithdraw(true);   // 拆除模式與取貨模式互斥
            DemolishMode = !DemolishMode;
            RefreshToolbar();
            Toast(DemolishMode ? Loc.T("ui.tool.demolish_mode_on") : Loc.T("ui.tool.build_mode_on"));
        }

        // ── 取貨模式（把倉庫裡的物品拿出來放到場上）────────────
        /// <summary>點「取出」：同一種再按一次 = 取消。</summary>
        public void PickWithdraw(ItemId item)
        {
            if (item == ItemId.None || _world == null) return;
            if (WithdrawItem == item) { ClearWithdraw(false); return; }
            if (_world.Hub[(int)item] <= 0)
            {
                PlaySfx?.Invoke("error");
                Toast(string.Format(Loc.T("ui.withdraw.empty_hub"), Loc.Item(item)));
                return;
            }
            ClearWithdraw(true);
            WithdrawItem = item;
            DemolishMode = false;
            PlaySfx?.Invoke("place");
            Toast(string.Format(Loc.T("ui.withdraw.enter"), Loc.Item(item)));
            RefreshToolbar();
        }

        public void ClearWithdraw(bool quiet)
        {
            if (WithdrawItem == ItemId.None) return;
            WithdrawItem = ItemId.None;
            RefreshToolbar();
            if (!quiet) Toast(Loc.T("ui.withdraw.cancel"));
        }

        public void CycleTool(int dir)
        {
            int total = Tools.Length + 1;
            int cur = DemolishMode ? Tools.Length : -1;
            if (cur < 0)
                for (int i = 0; i < Tools.Length; i++)
                    if (Tools[i].kind == SelectedTool) { cur = i; break; }
            cur = (cur + dir + total) % total;
            if (cur == Tools.Length) DemolishMode = true;
            else { DemolishMode = false; SelectTool(Tools[cur].kind); }
            RefreshToolbar();
        }

        private void RefreshToolbar()
        {
            for (int i = 0; i < _toolButtons.Count; i++)
            {
                bool on = DemolishMode ? i == _toolButtons.Count - 1
                                       : i < Tools.Length && Tools[i].kind == SelectedTool;
                _toolButtons[i].ButtonPressed = on;
            }
            UpdateSelectionLabel();
        }

        public void UpdateSelectionLabel()
        {
            if (_selectionLabel == null) return;
            if (WithdrawMode)
            {
                int stock = _world != null ? _world.Hub[(int)WithdrawItem] : 0;
                _selectionLabel.Text = $"▶ {Loc.T("ui.withdraw.mode")}：{Loc.Item(WithdrawItem)} ×{stock}　{Loc.T("ui.withdraw.mode_hint")}";
                return;
            }
            string name = DemolishMode ? Loc.T("ui.tool.demolish") : Loc.Building(SelectedTool);
            _selectionLabel.Text = $"▶ {name}　方向 {Loc.T("dir." + (int)Facing)}　(R 旋轉" +
                (SelectedTool == BuildingKind.Smelter || SelectedTool == BuildingKind.Assembler
                    ? " · Ctrl+滾輪 換配方" : "") + ")";
        }

        // ── 資訊欄 ──────────────────────────────────────────────
        private void BuildInfoPanel()
        {
            _infoPanel = new PanelContainer();
            _infoPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.06f, 0.08f, 0.12f, 0.92f), new Color(1f, 1f, 1f, 0.16f)));
            _infoPanel.AnchorLeft = 1f; _infoPanel.AnchorRight = 1f;
            _infoPanel.OffsetLeft = -300f; _infoPanel.OffsetRight = -10f;
            _infoPanel.OffsetTop = 140f; _infoPanel.OffsetBottom = 470f;
            _root.AddChild(_infoPanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 4);
            _infoPanel.AddChild(v);
            _infoTitle = Fonts2D.Label(Loc.T("ui.info_title"), 14, new Color(0.6f, 0.9f, 1f));
            v.AddChild(_infoTitle);

            // 放置前就顯示所選建築的完整屬性（造價/回收/耗電/進出料/說明）
            _infoTool = Fonts2D.Label("", 12, new Color(1f, 0.92f, 0.6f));
            _infoTool.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _infoTool.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(_infoTool);
            v.AddChild(new HSeparator());

            _infoBody = Fonts2D.Label("", 12, new Color(0.85f, 0.9f, 0.95f));
            _infoBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _infoBody.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(_infoBody);
        }

        // ── 事件饋送 ────────────────────────────────────────────
        private void BuildFeed()
        {
            _feedBox = new VBoxContainer();
            _feedBox.AddThemeConstantOverride("separation", 2);
            _feedBox.AnchorLeft = 1f; _feedBox.AnchorRight = 1f;
            _feedBox.OffsetLeft = -300f; _feedBox.OffsetRight = -10f;
            _feedBox.OffsetTop = 8f; _feedBox.OffsetBottom = 132f;
            _root.AddChild(_feedBox);
        }

        public void Toast(string text) => _toasts.Add((text, 6f));

        // ── 教學目標 ────────────────────────────────────────────
        private void BuildObjectives()
        {
            _objectivePanel = new PanelContainer();
            _objectivePanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.04f, 0.06f, 0.09f, 0.85f), new Color(1f, 0.8f, 0.4f, 0.4f)));
            _objectivePanel.AnchorLeft = 0f; _objectivePanel.AnchorRight = 0f;
            _objectivePanel.OffsetLeft = 10f; _objectivePanel.OffsetRight = 420f;
            _objectivePanel.OffsetTop = 84f; _objectivePanel.OffsetBottom = 300f;
            _root.AddChild(_objectivePanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 4);
            _objectivePanel.AddChild(v);
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);
            h.AddChild(Fonts2D.Label("新手教學", 13, new Color(1f, 0.8f, 0.4f)));
            var skipBtn = new Button { Text = "跳過教學", CustomMinimumSize = new Vector2(78, 22) };
            skipBtn.AddThemeFontOverride("font", Fonts2D.Small);
            skipBtn.AddThemeFontSizeOverride("font_size", 11);
            skipBtn.Pressed += () =>
            {
                Tutorial2D.Index = Tutorial2D.List.Length;
                Settings2D.TutorialDone = true; Settings2D.Save();
                Toast("已跳過新手教學（設定裡可重新開始）");
            };
            h.AddChild(skipBtn);
            v.AddChild(h);

            _objectiveLabel = Fonts2D.Label("", 13, new Color(1f, 0.95f, 0.75f));
            _objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _objectiveLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(_objectiveLabel);

            _objectiveHint = Fonts2D.Label("", 12, new Color(0.78f, 0.84f, 0.9f));
            _objectiveHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _objectiveHint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(_objectiveHint);
        }

        // ── 研究面板 ────────────────────────────────────────────
        private void BuildResearchPanel()
        {
            _researchPanel = new PanelContainer();
            _researchPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.95f), new Color(1f, 1f, 1f, 0.18f)));
            _researchPanel.AnchorLeft = 1f; _researchPanel.AnchorRight = 1f;
            _researchPanel.OffsetLeft = -380f; _researchPanel.OffsetRight = -10f;
            _researchPanel.OffsetTop = 60f; _researchPanel.OffsetBottom = 340f;
            _researchPanel.Visible = false;
            _root.AddChild(_researchPanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            _researchPanel.AddChild(v);
            _researchBox = v;

            v.AddChild(Fonts2D.Label(Loc.T("ui.research"), 15, new Color(0.7f, 0.85f, 1f)));
            var hint = Fonts2D.Label(Loc.T("ui.research.hint"), 11, new Color(0.6f, 0.65f, 0.7f));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(hint);
            var sci = Fonts2D.Label("科研包 = 齒輪 + 電路（組裝機，Ctrl+滾輪 切到科研包配方）", 11, new Color(0.85f, 0.75f, 1f));
            sci.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            sci.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(sci);
            v.AddChild(Fonts2D.Label($"{Loc.T("ui.science_stock")}: —", 12, new Color(0.9f, 0.9f, 0.6f)));
            BuildResearchButtons(v);
        }

        private void BuildResearchButtons(VBoxContainer v)
        {
            var names = new[] { "ui.research.belt", "ui.research.machine", "ui.research.miner", "ui.research.power" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = new Button { CustomMinimumSize = new Vector2(0, 34) };
                b.AddThemeFontOverride("font", Fonts2D.Small);
                b.AddThemeFontSizeOverride("font_size", 13);
                b.Alignment = HorizontalAlignment.Left;
                b.Pressed += () => { if (_world.TryResearch(idx)) { Toast($"{Loc.T(names[idx])} {Loc.T("ui.research.done")}"); PlaySfx?.Invoke("research"); } else Toast(Loc.T("ui.research.lack")); };
                v.AddChild(b);
            }
        }

        // ── 圖鑑 ────────────────────────────────────────────────
        private void BuildCodexPanel()
        {
            _codexPanel = new PanelContainer();
            _codexPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.96f), new Color(1f, 1f, 1f, 0.18f)));
            _codexPanel.AnchorLeft = 0f; _codexPanel.AnchorRight = 0f;
            _codexPanel.OffsetLeft = 10f; _codexPanel.OffsetRight = 400f;
            _codexPanel.OffsetTop = 60f; _codexPanel.OffsetBottom = -40f;
            _codexPanel.Visible = false;
            _root.AddChild(_codexPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _codexPanel.AddChild(scroll);

            var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            v.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(v);

            v.AddChild(Fonts2D.Label(Loc.T("codex.title"), 16, new Color(0.7f, 0.9f, 1f)));
            AddCodexSection(v, Loc.T("codex.items"));
            for (int i = 1; i < GameData.ItemSlots; i++)
            {
                if ((ItemId)i == ItemId.None) continue;
                AddCodexEntry(v, Loc.Item((ItemId)i), Loc.T("item.desc." + i), Palette2D.Item((ItemId)i));
            }
            AddCodexSection(v, Loc.T("codex.buildings"));
            for (int i = 1; i <= 13; i++) AddCodexEntry(v, Loc.Building((BuildingKind)i), Loc.T("building.desc." + i), Palette2D.Building((BuildingKind)i));
            AddCodexSection(v, "蟲潮");
            for (int i = 0; i <= 9; i++) AddCodexEntry(v, Loc.T("alien." + i), Loc.T("alien.desc." + i), Palette2D.AlienBody((AlienKind)i));
        }

        private static void AddCodexSection(VBoxContainer v, string title)
        {
            v.AddChild(new HSeparator());
            v.AddChild(Fonts2D.Label(title, 14, new Color(1f, 0.8f, 0.4f)));
        }

        private static void AddCodexEntry(VBoxContainer v, string name, string desc, Color swatch)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var dot = new ColorRect { Color = swatch, CustomMinimumSize = new Vector2(12, 12) };
            row.AddChild(dot);
            row.AddChild(Fonts2D.Label(name, 13, new Color(0.95f, 0.98f, 1f)));
            v.AddChild(row);
            var d = Fonts2D.Label(desc, 12, new Color(0.7f, 0.75f, 0.8f));
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            d.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(d);
        }

        // ── 說明 ────────────────────────────────────────────────
        private void BuildHelpPanel()
        {
            _helpPanel = new PanelContainer();
            _helpPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.96f), new Color(1f, 1f, 1f, 0.18f)));
            _helpPanel.AnchorLeft = 0.5f; _helpPanel.AnchorRight = 0.5f;
            _helpPanel.AnchorTop = 0.5f; _helpPanel.AnchorBottom = 0.5f;
            _helpPanel.OffsetLeft = -320f; _helpPanel.OffsetRight = 320f;
            _helpPanel.OffsetTop = -240f; _helpPanel.OffsetBottom = 240f;
            _helpPanel.Visible = false;
            _root.AddChild(_helpPanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);
            _helpPanel.AddChild(v);

            v.AddChild(Fonts2D.Label(Loc.T("ui.controls"), 16, new Color(0.7f, 0.9f, 1f)));
            var ctrl = Fonts2D.Label(
                "WASD/方向鍵 平移　·　滾輪 縮放　·　中鍵拖曳 平移\n" +
                "1-9,0 選工具　·　[ ] 循環　·　左鍵 放置　·　右鍵/X 拆除\n" +
                "R 旋轉　·　Ctrl+滾輪 換配方　·　C/V 複製貼上藍圖\n" +
                "G 交易　·　P 星球　·　U 研究　·　F1 圖鑑　·　H 說明\n" +
                "F5 存檔　·　Esc 設定　·　F2 隱藏介面　·　F3 格線　·　空白 暫停",
                13, new Color(0.85f, 0.9f, 0.95f));
            ctrl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            v.AddChild(ctrl);

            var how = Fonts2D.Label(Loc.T("help.first"), 12, new Color(0.75f, 0.8f, 0.85f));
            how.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            how.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(how);

            var dir = Fonts2D.Label(Loc.T("help.dir"), 12, new Color(0.6f, 0.85f, 0.7f));
            dir.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            dir.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(dir);

            var chain = Fonts2D.Label("製作鏈：鐵礦→鐵板→齒輪｜銅礦→銅板→電路｜齒輪+銅板→砲彈｜齒輪+電路→科研包",
                12, new Color(0.9f, 0.8f, 0.6f));
            chain.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            chain.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(chain);
        }

        // ── 設定面板 ────────────────────────────────────────────
        private void BuildSettingsPanel()
        {
            _settingsPanel = new PanelContainer();
            _settingsPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.97f), new Color(1f, 1f, 1f, 0.2f)));
            _settingsPanel.AnchorLeft = 0.5f; _settingsPanel.AnchorRight = 0.5f;
            _settingsPanel.AnchorTop = 0.5f; _settingsPanel.AnchorBottom = 0.5f;
            _settingsPanel.OffsetLeft = -260f; _settingsPanel.OffsetRight = 260f;
            _settingsPanel.OffsetTop = -250f; _settingsPanel.OffsetBottom = 250f;
            _settingsPanel.Visible = false;
            _root.AddChild(_settingsPanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 10);
            _settingsPanel.AddChild(v);

            v.AddChild(Fonts2D.Label("設定（Esc 關閉）", 16, new Color(0.7f, 0.9f, 1f)));

            var langRow = new HBoxContainer();
            langRow.AddThemeConstantOverride("separation", 8);
            langRow.AddChild(Fonts2D.Label("語言", 13, new Color(0.85f, 0.9f, 0.95f)));
            var langBtn = new Button { Text = Loc.SelfName(Loc.Current), CustomMinimumSize = new Vector2(120, 28) };
            langBtn.AddThemeFontOverride("font", Fonts2D.Small);
            langBtn.Pressed += () => { Loc.Cycle(); Settings2D.Language = Loc.Current; Settings2D.Save(); Rebuild(); };
            langRow.AddChild(langBtn);
            v.AddChild(langRow);

            v.AddChild(AddSlider("音樂音量", 0f, 1f, 0.05f, Settings2D.MusicVolume,
                val => { Settings2D.MusicVolume = val; Settings2D.Save(); }));
            v.AddChild(AddSlider("音效音量", 0f, 1f, 0.05f, Settings2D.SfxVolume,
                val => { Settings2D.SfxVolume = val; Settings2D.Save(); }));
            v.AddChild(AddSlider("蟲潮威脅倍率", 0.5f, 3f, 0.1f, Settings2D.ThreatMultiplier,
                val => { Settings2D.ThreatMultiplier = val; if (_world != null) _world.ThreatMultiplier = val; Settings2D.Save(); }));

            var optRow = new HBoxContainer();
            optRow.AddThemeConstantOverride("separation", 8);
            var fsBtn = new Button { Text = "全螢幕", ToggleMode = true, ButtonPressed = Settings2D.Fullscreen, CustomMinimumSize = new Vector2(90, 28) };
            fsBtn.Pressed += () => { Settings2D.Fullscreen = fsBtn.ButtonPressed; DisplayServer.WindowSetMode(Settings2D.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed); Settings2D.Save(); };
            optRow.AddChild(fsBtn);
            var gridBtn = new Button { Text = "格線", ToggleMode = true, ButtonPressed = Settings2D.ShowGrid, CustomMinimumSize = new Vector2(90, 28) };
            gridBtn.Pressed += () => { Settings2D.ShowGrid = gridBtn.ButtonPressed; ChunkView2D.ShowGrid = Settings2D.ShowGrid; _chunks?.QueueTerrainRedraws(); Settings2D.Save(); };
            optRow.AddChild(gridBtn);
            v.AddChild(optRow);

            // 蟲潮是否要手動確認（預設開）
            var waveRow = new HBoxContainer();
            waveRow.AddThemeConstantOverride("separation", 8);
            waveRow.AddChild(Fonts2D.Label(Loc.T("settings.wave_confirm"), 13, new Color(0.85f, 0.9f, 0.95f)));
            var waveBtn = new Button
            {
                Text = Settings2D.WaveConfirm ? Loc.T("ui.on") : Loc.T("ui.off"),
                ToggleMode = true,
                ButtonPressed = Settings2D.WaveConfirm,
                CustomMinimumSize = new Vector2(64, 26),
            };
            waveBtn.AddThemeFontOverride("font", Fonts2D.Small);
            waveBtn.AddThemeFontSizeOverride("font_size", 12);
            waveBtn.Pressed += () =>
            {
                Settings2D.WaveConfirm = waveBtn.ButtonPressed;
                Settings2D.Save();
                waveBtn.Text = Settings2D.WaveConfirm ? Loc.T("ui.on") : Loc.T("ui.off");
                if (_world != null) _world.WaveRequiresConfirm = Settings2D.WaveConfirm;
            };
            waveRow.AddChild(waveBtn);
            v.AddChild(waveRow);

            var tutBtn = new Button { Text = "重新開始教學", CustomMinimumSize = new Vector2(0, 30) };
            tutBtn.Pressed += () => { Tutorial2D.Reset(); Settings2D.TutorialDone = false; Settings2D.Save(); Toast("已重啟新手教學"); };
            v.AddChild(tutBtn);

            // 中途退出：回主選單（自動存檔）/ 離開遊戲
            var exitRow = new HBoxContainer();
            exitRow.AddThemeConstantOverride("separation", 8);
            var menuBtn = new Button { Text = "回到主選單", CustomMinimumSize = new Vector2(120, 30) };
            menuBtn.AddThemeFontOverride("font", Fonts2D.Small);
            menuBtn.Pressed += () => MenuRequested?.Invoke();
            exitRow.AddChild(menuBtn);
            var quitBtn = new Button { Text = "離開遊戲", CustomMinimumSize = new Vector2(120, 30) };
            quitBtn.AddThemeFontOverride("font", Fonts2D.Small);
            quitBtn.Pressed += () => GetTree().Quit();
            exitRow.AddChild(quitBtn);
            v.AddChild(exitRow);
        }

        private static VBoxContainer AddSlider(string label, float min, float max, float step, float value, Action<float> onChanged)
        {
            var box = new VBoxContainer();
            var lbl = Fonts2D.Label(label + $"　{value:F2}", 12, new Color(0.85f, 0.9f, 0.95f));
            box.AddChild(lbl);
            var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(0, 22) };
            s.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            s.ValueChanged += v => { lbl.Text = label + $"　{v:F2}"; onChanged((float)v); };
            box.AddChild(s);
            return box;
        }

        // ── 小地圖 ──────────────────────────────────────────────
        private void BuildMinimap()
        {
            _minimap = new Minimap2D();
            _minimap.Setup(_world, _cam);
            _minimap.AnchorLeft = 1f; _minimap.AnchorRight = 1f;
            _minimap.OffsetLeft = -252f; _minimap.OffsetRight = -10f;
            _minimap.OffsetTop = 10f; _minimap.OffsetBottom = 242f;
            _minimap.Visible = false;
            _root.AddChild(_minimap);
        }

        // ── 交易面板（賣礦換金幣）──────────────────────────────
        private void BuildSellPanel()
        {
            _sellPanel = new PanelContainer();
            _sellPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.96f), new Color(1f, 0.85f, 0.4f, 0.35f)));
            _sellPanel.AnchorLeft = 0.5f; _sellPanel.AnchorRight = 0.5f;
            _sellPanel.AnchorTop = 0.5f; _sellPanel.AnchorBottom = 0.5f;
            _sellPanel.OffsetLeft = -260f; _sellPanel.OffsetRight = 260f;
            _sellPanel.OffsetTop = -280f; _sellPanel.OffsetBottom = 280f;
            _sellPanel.Visible = false;
            _root.AddChild(_sellPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _sellPanel.AddChild(scroll);
            var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            v.AddThemeConstantOverride("separation", 5);
            scroll.AddChild(v);

            v.AddChild(Fonts2D.Label(Loc.T("ui.sell") + "　　" + Loc.T("ui.sell_hint") + "（價格會隨市場浮動）", 14, new Color(1f, 0.85f, 0.4f)));

            for (int i = 1; i < GameData.ItemSlots; i++)
            {
                var item = (ItemId)i;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                var dot = new ColorRect { Color = Palette2D.Item(item), CustomMinimumSize = new Vector2(12, 12) };
                row.AddChild(dot);
                row.AddChild(Fonts2D.Label(Loc.Item(item), 13, new Color(0.9f, 0.95f, 1f)));
                var price = Fonts2D.Label("$0", 12, new Color(1f, 0.9f, 0.5f));
                row.AddChild(price);
                _sellPriceLabels.Add(price);
                var stock = Fonts2D.Label("×0", 12, new Color(0.7f, 0.75f, 0.8f));
                row.AddChild(stock);
                _sellStockLabels.Add(stock);
                var sellBtn = new Button { Text = "賣出", CustomMinimumSize = new Vector2(70, 26) };
                sellBtn.AddThemeFontOverride("font", Fonts2D.Small);
                int idx = i;
                sellBtn.Pressed += () => SellAll((ItemId)idx);
                row.AddChild(sellBtn);
                v.AddChild(row);
            }

            // ── 銀行 / 貸款 ──
            v.AddChild(new HSeparator());
            v.AddChild(Fonts2D.Label("銀行：可貸款，但債務每 60 秒 +5% 利息", 12, new Color(0.9f, 0.75f, 0.6f)));
            _debtLabel = Fonts2D.Label("債務 $0", 13, new Color(1f, 0.7f, 0.6f));
            v.AddChild(_debtLabel);
            var loanRow = new HBoxContainer();
            loanRow.AddThemeConstantOverride("separation", 8);
            var borrow = new Button { Text = "借 $1000", CustomMinimumSize = new Vector2(100, 28) };
            borrow.AddThemeFontOverride("font", Fonts2D.Small);
            borrow.Pressed += () => { if (_world.TakeLoan(1000)) { PlaySfx?.Invoke("coin"); Toast("已貸款 $1000"); } else Toast("貸款額度已滿（上限 $20000）"); };
            loanRow.AddChild(borrow);
            var repay = new Button { Text = "還 $1000", CustomMinimumSize = new Vector2(100, 28) };
            repay.AddThemeFontOverride("font", Fonts2D.Small);
            repay.Pressed += () => { if (_world.RepayLoan(1000)) { PlaySfx?.Invoke("coin"); Toast("已還款 $1000"); } else Toast("沒有足夠金幣或無債務"); };
            loanRow.AddChild(repay);
            v.AddChild(loanRow);

            // ── 倉庫取貨：把庫存拿出來，用左鍵放到傳送帶或建築 ──
            v.AddChild(new HSeparator());
            v.AddChild(Fonts2D.Label(Loc.T("ui.withdraw.title"), 14, new Color(0.6f, 0.9f, 1f)));
            var withdrawHint = Fonts2D.Label(Loc.T("ui.withdraw.hint"), 11, new Color(0.62f, 0.68f, 0.74f));
            withdrawHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            withdrawHint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(withdrawHint);

            var wGrid = new GridContainer { Columns = 2 };
            wGrid.AddThemeConstantOverride("h_separation", 4);
            wGrid.AddThemeConstantOverride("v_separation", 4);
            v.AddChild(wGrid);
            for (int i = 1; i < GameData.ItemSlots; i++)
            {
                var wItem = (ItemId)i;
                var wb = new Button { CustomMinimumSize = new Vector2(228, 26) };
                wb.AddThemeFontOverride("font", Fonts2D.Small);
                wb.AddThemeFontSizeOverride("font_size", 12);
                // 用物品主色當字色 → 等於一個「色塊」，而且不需要任何貼圖
                wb.AddThemeColorOverride("font_color", Palette2D.Item(wItem).Lightened(0.25f));
                int idx = i;
                wb.Pressed += () => PickWithdraw((ItemId)idx);
                wGrid.AddChild(wb);
                _withdrawButtons.Add((wItem, wb));
            }
        }

        // ── 市場 / 委託 / 世界事件（中期節奏）────────────────────
        // ── 成就面板（J 鍵）────────────────────────────────────
        private void BuildAchPanel()
        {
            _achPanel = new PanelContainer();
            _achPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.06f, 0.06f, 0.10f, 0.96f), new Color(1f, 0.8f, 0.3f, 0.4f)));
            _achPanel.AnchorLeft = 0.5f; _achPanel.AnchorRight = 0.5f;
            _achPanel.AnchorTop = 0.5f; _achPanel.AnchorBottom = 0.5f;
            _achPanel.OffsetLeft = -320f; _achPanel.OffsetRight = 320f;
            _achPanel.OffsetTop = -300f; _achPanel.OffsetBottom = 300f;
            _achPanel.Visible = false;
            _root.AddChild(_achPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _achPanel.AddChild(scroll);
            _achBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            _achBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_achBox);
        }

        private void RefreshAchPanel()
        {
            if (_achBox == null || _world == null) return;
            for (int i = _achBox.GetChildCount() - 1; i >= 0; i--)
                _achBox.GetChild(i).QueueFree();
            _achRows.Clear();

            _achBox.AddChild(Fonts2D.Label(Loc.T("ui.ach.title"), 16, new Color(1f, 0.85f, 0.4f)));
            _achProgressLabel = Fonts2D.Label("", 13, new Color(0.8f, 0.85f, 0.9f));
            _achBox.AddChild(_achProgressLabel);
            _achBox.AddChild(new HSeparator());

            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var a = Achievements.All[i];
                bool done = _world.IsAchUnlocked(i);
                int cur = Math.Min(a.Progress(_world), a.Target);

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                var name = Fonts2D.Label(Loc.T(a.NameKey), 13, done ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.65f, 0.7f));
                name.CustomMinimumSize = new Vector2(160, 0);
                row.AddChild(name);
                var desc = Fonts2D.Label(Loc.T(a.DescKey), 11, new Color(0.7f, 0.75f, 0.8f));
                desc.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                row.AddChild(desc);
                var prog = Fonts2D.Label(cur + "/" + a.Target, 11, done ? new Color(0.5f, 1f, 0.5f) : new Color(0.8f, 0.8f, 0.8f));
                prog.CustomMinimumSize = new Vector2(70, 0);
                prog.HorizontalAlignment = HorizontalAlignment.Right;
                row.AddChild(prog);
                _achBox.AddChild(row);
                _achRows.Add((name, desc, prog));
            }

            _achTimer = 0f;
            UpdateAchValues(0f);
        }

        private void UpdateAchValues(float dt)
        {
            if (_world == null || _achPanel == null || !_achPanel.Visible) return;
            _achTimer -= dt;
            if (_achTimer > 0f) return;
            _achTimer = 0.25f;

            int unlocked = _world.AchUnlockedCount;
            if (_achProgressLabel != null)
                _achProgressLabel.Text = string.Format(Loc.T("ui.ach.progress"), unlocked, Achievements.Count);

            for (int i = 0; i < _achRows.Count && i < Achievements.All.Length; i++)
            {
                var a = Achievements.All[i];
                bool done = _world.IsAchUnlocked(i);
                int cur = Math.Min(a.Progress(_world), a.Target);
                var row = _achRows[i];
                row.prog.Text = cur + "/" + a.Target;
                row.prog.AddThemeColorOverride("font_color", done ? new Color(0.5f, 1f, 0.5f) : new Color(0.8f, 0.8f, 0.8f));
                row.name.AddThemeColorOverride("font_color", done ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.65f, 0.7f));
            }
        }

        // ── 科技樹面板（T 鍵）──────────────────────────────────
        private void BuildTechPanel()
        {
            _techPanel = new PanelContainer();
            _techPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.08f, 0.12f, 0.96f), new Color(0.4f, 0.7f, 1f, 0.4f)));
            _techPanel.AnchorLeft = 0.5f; _techPanel.AnchorRight = 0.5f;
            _techPanel.AnchorTop = 0.5f; _techPanel.AnchorBottom = 0.5f;
            _techPanel.OffsetLeft = -340f; _techPanel.OffsetRight = 340f;
            _techPanel.OffsetTop = -280f; _techPanel.OffsetBottom = 280f;
            _techPanel.Visible = false;
            _root.AddChild(_techPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _techPanel.AddChild(scroll);
            _techBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            _techBox.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_techBox);
        }

        private void RefreshTechPanel()
        {
            if (_techBox == null || _world == null) return;
            for (int i = _techBox.GetChildCount() - 1; i >= 0; i--)
                _techBox.GetChild(i).QueueFree();

            _techBox.AddChild(Fonts2D.Label(Loc.T("ui.tech.title"), 16, new Color(0.5f, 0.8f, 1f)));
            _techBox.AddChild(new HSeparator());

            string[] lineNames = { "Belt", "Machine", "Mining", "Power" };
            Color[] lineColors = {
                new Color(0.4f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.4f),
                new Color(1f, 0.85f, 0.3f), new Color(0.5f, 0.7f, 1f),
            };

            for (int line = 0; line < 4; line++)
            {
                _techBox.AddChild(Fonts2D.Label(lineNames[line], 14, lineColors[line]));

                var grid = new GridContainer { Columns = 5 };
                grid.AddThemeConstantOverride("h_separation", 8);
                grid.AddThemeConstantOverride("v_separation", 4);

                for (int tier = 0; tier < 5; tier++)
                {
                    int nodeId = line * 5 + tier;
                    var node = TechTree.Nodes[nodeId];
                    bool unlocked = _world.IsTechUnlocked(nodeId);
                    bool canUnlock = !unlocked
                        && _world.Research[line] >= node.RequireLevel
                        && _world.Hub[(int)ItemId.Science] >= node.CostScience;

                    var vbox = new VBoxContainer();
                    vbox.CustomMinimumSize = new Vector2(110, 60);

                    var btn = new Button();
                    btn.Text = unlocked ? "OK " + Loc.T(node.NameKey) : Loc.T(node.NameKey);
                    btn.CustomMinimumSize = new Vector2(110, 36);
                    btn.AddThemeFontSizeOverride("font_size", 11);
                    btn.AddThemeColorOverride("font_color",
                        unlocked ? new Color(0.5f, 1f, 0.5f) :
                        canUnlock ? new Color(0.4f, 0.8f, 1f) : new Color(0.5f, 0.55f, 0.6f));
                    if (!unlocked)
                    {
                        int id = nodeId;
                        btn.Pressed += () =>
                        {
                            if (TechTree.TryUnlock(_world, id, out _))
                            {
                                PlaySfx?.Invoke("research");
                                RefreshTechPanel();
                            }
                            else Toast(Loc.T("tech.need_science"));
                        };
                    }
                    else btn.Disabled = true;

                    vbox.AddChild(btn);
                    var cost = Fonts2D.Label(unlocked ? Loc.T("ui.tech.unlocked") : node.CostScience + " sci", 9, new Color(0.65f, 0.7f, 0.75f));
                    vbox.AddChild(cost);
                    grid.AddChild(vbox);
                }
                _techBox.AddChild(grid);
            }
        }

        // ── 跨星球傳送門面板（K 鍵）────────────────────────────
        private void BuildPortalPanel()
        {
            _portalPanel = new PanelContainer();
            _portalPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.08f, 0.05f, 0.12f, 0.96f), new Color(0.8f, 0.4f, 1f, 0.4f)));
            _portalPanel.AnchorLeft = 0.5f; _portalPanel.AnchorRight = 0.5f;
            _portalPanel.AnchorTop = 0.5f; _portalPanel.AnchorBottom = 0.5f;
            _portalPanel.OffsetLeft = -280f; _portalPanel.OffsetRight = 280f;
            _portalPanel.OffsetTop = -220f; _portalPanel.OffsetBottom = 220f;
            _portalPanel.Visible = false;
            _root.AddChild(_portalPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _portalPanel.AddChild(scroll);
            _portalBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            _portalBox.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_portalBox);
        }

        private void RefreshPortalPanel()
        {
            if (_portalBox == null || _world == null) return;
            for (int i = _portalBox.GetChildCount() - 1; i >= 0; i--)
                _portalBox.GetChild(i).QueueFree();

            _portalBox.AddChild(Fonts2D.Label(Loc.T("ui.portal.xfer.title"), 16, new Color(0.8f, 0.5f, 1f)));
            var hint = Fonts2D.Label(Loc.T("ui.portal.xfer.hint"), 11, new Color(0.65f, 0.7f, 0.75f));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _portalBox.AddChild(hint);
            _portalBox.AddChild(new HSeparator());

            var planetRow = new HBoxContainer();
            planetRow.AddThemeConstantOverride("separation", 6);
            planetRow.AddChild(Fonts2D.Label(Loc.T("ui.portal.xfer.target"), 13, new Color(0.8f, 0.7f, 0.9f)));
            _portalPlanetBtn = new OptionButton();
            _portalPlanetBtn.CustomMinimumSize = new Vector2(180, 30);
            for (int i = 0; i < Planets.All.Length; i++)
            {
                if (i == _world.PlanetIndex) continue;
                if (!_world.IsUnlocked(i)) continue;
                _portalPlanetBtn.AddItem(Loc.Planet(Planets.All[i]), i);
            }
            planetRow.AddChild(_portalPlanetBtn);
            _portalBox.AddChild(planetRow);

            var itemRow = new HBoxContainer();
            itemRow.AddThemeConstantOverride("separation", 6);
            itemRow.AddChild(Fonts2D.Label(Loc.T("ui.market.stock"), 13, new Color(0.8f, 0.7f, 0.9f)));
            _portalItemBtn = new OptionButton();
            _portalItemBtn.CustomMinimumSize = new Vector2(180, 30);
            for (int i = 1; i < GameData.ItemSlots; i++)
            {
                var item = (ItemId)i;
                int stock = _world.Hub[i];
                if (stock <= 0) continue;
                _portalItemBtn.AddItem(Loc.Item(item) + " (" + stock + ")", i);
            }
            itemRow.AddChild(_portalItemBtn);
            _portalBox.AddChild(itemRow);

            var countRow = new HBoxContainer();
            countRow.AddThemeConstantOverride("separation", 6);
            countRow.AddChild(Fonts2D.Label("Count", 13, new Color(0.8f, 0.7f, 0.9f)));
            _portalCountSpin = new SpinBox();
            _portalCountSpin.MinValue = 1;
            _portalCountSpin.MaxValue = 9999;
            _portalCountSpin.Value = 1;
            _portalCountSpin.CustomMinimumSize = new Vector2(100, 30);
            countRow.AddChild(_portalCountSpin);
            _portalBox.AddChild(countRow);

            var sendBtn = new Button();
            sendBtn.Text = Loc.T("ui.portal.xfer.send");
            sendBtn.CustomMinimumSize = new Vector2(140, 36);
            sendBtn.AddThemeFontOverride("font", Fonts2D.Small);
            sendBtn.Pressed += () =>
            {
                if (_portalPlanetBtn.Selected < 0 || _portalItemBtn.Selected < 0) { Toast("select item"); return; }
                int targetPlanet = _portalPlanetBtn.GetSelectedId();
                int itemIdx = _portalItemBtn.GetSelectedId();
                int count = (int)_portalCountSpin.Value;
                var item = (ItemId)itemIdx;
                if (_world.TransferHubItems(item, count, targetPlanet, out string err))
                {
                    PlaySfx?.Invoke("click");
                    Toast(string.Format(Loc.T("ui.portal.xfer.ok"), Loc.Item(item), count));
                    RefreshPortalPanel();
                }
                else Toast(Loc.T(err));
            };
            _portalBox.AddChild(sendBtn);
        }

        private void BuildLivePanel()
        {
            _livePanel = new PanelContainer();
            _livePanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.96f), new Color(0.45f, 0.95f, 0.85f, 0.4f)));
            _livePanel.AnchorLeft = 0.5f; _livePanel.AnchorRight = 0.5f;
            _livePanel.AnchorTop = 0.5f; _livePanel.AnchorBottom = 0.5f;
            _livePanel.OffsetLeft = -300f; _livePanel.OffsetRight = 300f;
            _livePanel.OffsetTop = -290f; _livePanel.OffsetBottom = 290f;
            _livePanel.Visible = false;
            _root.AddChild(_livePanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _livePanel.AddChild(scroll);
            _liveBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            _liveBox.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_liveBox);
        }

        /// <summary>面板每次開啟時重建；之後只更新數字，避免每 0.15 秒重建控件。</summary>
        private void RefreshLivePanel()
        {
            if (_liveBox == null || _world == null) return;
            for (int i = _liveBox.GetChildCount() - 1; i >= 0; i--)
                _liveBox.GetChild(i).QueueFree();

            _liveBox.AddChild(Fonts2D.Label(Loc.T("ui.events"), 16, new Color(0.5f, 1f, 0.85f)));

            _liveEventLabel = Fonts2D.Label("", 13, new Color(0.75f, 0.85f, 0.95f));
            _liveEventLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _liveEventLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _liveBox.AddChild(_liveEventLabel);

            _liveBox.AddChild(new HSeparator());
            _liveBox.AddChild(Fonts2D.Label(Loc.T("ui.contract.title"), 15, new Color(1f, 0.88f, 0.5f)));

            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 8);
            _liveContractLabel = Fonts2D.Label("", 13, new Color(0.95f, 0.92f, 0.8f));
            _liveContractLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _liveContractLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            head.AddChild(_liveContractLabel);

            var accept = new Button { Text = Loc.T("ui.contract.accept"), CustomMinimumSize = new Vector2(92, 28) };
            accept.AddThemeFontOverride("font", Fonts2D.Small);
            accept.Pressed += () =>
            {
                if (_world.AcceptContract()) { PlaySfx?.Invoke("click"); RefreshLivePanel(); }
            };
            head.AddChild(accept);

            var deliver = new Button { Text = Loc.T("ui.contract.deliver"), CustomMinimumSize = new Vector2(92, 28) };
            deliver.AddThemeFontOverride("font", Fonts2D.Small);
            deliver.Pressed += () =>
            {
                int moved = _world.DeliverContract();
                if (moved > 0) { PlaySfx?.Invoke("coin"); RefreshLivePanel(); }
                else Toast(Loc.T("ui.contract.need_more"));
            };
            head.AddChild(deliver);
            _liveBox.AddChild(head);

            _liveHintLabel = Fonts2D.Label("", 12, new Color(0.66f, 0.72f, 0.78f));
            _liveHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _liveHintLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _liveBox.AddChild(_liveHintLabel);

            _liveBox.AddChild(new HSeparator());
            var hint = Fonts2D.Label(Loc.T("ui.market.hint"), 11, new Color(0.6f, 0.66f, 0.72f));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _liveBox.AddChild(hint);

            var grid = new GridContainer { Columns = 5 };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 3);
            _liveBox.AddChild(grid);
            grid.AddChild(Fonts2D.Label("", 11, new Color(0.8f, 0.85f, 0.9f)));
            grid.AddChild(Fonts2D.Label(Loc.T("ui.market.price"), 11, new Color(1f, 0.9f, 0.55f)));
            grid.AddChild(Fonts2D.Label(Loc.T("ui.market.saturation"), 11, new Color(0.9f, 0.7f, 0.6f)));
            grid.AddChild(Fonts2D.Label(Loc.T("ui.market.stock"), 11, new Color(0.7f, 0.8f, 0.9f)));
            grid.AddChild(Fonts2D.Label("", 11, new Color(0.8f, 0.85f, 0.9f)));

            for (int i = 1; i < GameData.ItemSlots; i++)
            {
                var item = (ItemId)i;
                var name = Fonts2D.Label(Loc.Item(item), 12, Palette2D.Item(item).Lightened(0.3f));
                grid.AddChild(name);
                var price = Fonts2D.Label("$0", 12, new Color(1f, 0.92f, 0.6f));
                grid.AddChild(price);
                var sat = Fonts2D.Label("0%", 12, new Color(0.9f, 0.7f, 0.6f));
                grid.AddChild(sat);
                var stock = Fonts2D.Label("0", 12, new Color(0.72f, 0.82f, 0.92f));
                grid.AddChild(stock);

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 3);
                foreach (var (label, count) in new[] { ("1", 1), ("10", 10), ("∞", -1) })
                {
                    var b = new Button { Text = label, CustomMinimumSize = new Vector2(30, 24) };
                    b.AddThemeFontOverride("font", Fonts2D.Small);
                    b.AddThemeFontSizeOverride("font_size", 11);
                    int n = count;
                    b.Pressed += () => SellFromMarket(item, n);
                    row.AddChild(b);
                }
                grid.AddChild(row);

                _liveRows.Add((item, price, sat, stock));
            }

            _liveTimer = 0f;
            UpdateLiveValues(0f);
        }

        private readonly List<(ItemId item, Label price, Label sat, Label stock)> _liveRows = new();

        private void SellFromMarket(ItemId item, int requested)
        {
            int avail = _world.Hub[(int)item];
            if (avail <= 0) { Toast(Loc.T("ui.market.empty")); return; }
            int count = requested < 0 ? avail : Math.Min(requested, avail);
            long before = _world.Coins;
            if (_world.Sell(item, count))
            {
                PlaySfx?.Invoke("coin");
                Toast(string.Format(Loc.T("ui.market.sold"), Loc.Item(item), count, _world.Coins - before));
            }
        }

        /// <summary>每 0.25 秒更新一次數字：世界事件倒數、委託進度、價格與飽和度。</summary>
        private void UpdateLiveValues(float dt)
        {
            if (_world == null || _livePanel == null || !_livePanel.Visible) return;
            _liveTimer -= dt;
            if (_liveTimer > 0f) return;
            _liveTimer = 0.25f;

            if (_liveEventLabel != null)
            {
                if (_world.PendingCrisis != CrisisKind.None)
                {
                    _liveEventLabel.Text = "⚠ " + string.Format(Loc.T("event.warning"),
                        Loc.T("event." + (int)_world.PendingCrisis)) + $"  ({(_world.PendingCrisisIn):0}s)";
                    _liveEventLabel.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.35f));
                }
                else if (_world.CrisisActive)
                {
                    _liveEventLabel.Text = "▶ " + Loc.T("event." + (int)_world.CurrentCrisis)
                        + $"  ({_world.CrisisRemaining:0}s)\n" + Loc.T("event.desc." + (int)_world.CurrentCrisis);
                    _liveEventLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
                }
                else
                {
                    _liveEventLabel.Text = Loc.T("event.ended") + $"  ({Math.Max(0f, _world.NextCrisisIn - 30f):0}s)";
                    _liveEventLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.78f, 0.85f));
                }
            }

            if (_liveContractLabel != null)
            {
                var c = _world.Contract;
                if (c == null)
                {
                    _liveContractLabel.Text = Loc.T("ui.contract.none");
                }
                else if (c.Accepted)
                {
                    _liveContractLabel.Text = string.Format(Loc.T("ui.contract.body"),
                        Loc.Item(c.Item), c.Required, c.Reward, Math.Max(0, (int)c.Remaining)) + "\n"
                        + string.Format(Loc.T("ui.contract.delivered"), c.Delivered, c.Required);
                }
                else
                {
                    _liveContractLabel.Text = string.Format(Loc.T("ui.contract.body"),
                        Loc.Item(c.Item), c.Required, c.Reward, Math.Max(0, (int)c.Remaining));
                }
            }

            if (_liveHintLabel != null)
                _liveHintLabel.Text = string.Format(Loc.T("ui.contract.done_count"),
                    _world.ContractsCompleted, _world.ContractsFailed);

            foreach (var (item, price, sat, stock) in _liveRows)
            {
                int p = _world.ItemPrice(item);
                bool hot = _world.IsHot(item);
                price.Text = (hot ? "★" : "") + "$" + p;
                price.AddThemeColorOverride("font_color", hot ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.92f, 0.6f));
                float s = _world.SaturationPercent(item);
                sat.Text = $"{s:0}%";
                sat.AddThemeColorOverride("font_color", s > 60f ? new Color(1f, 0.45f, 0.4f) : new Color(0.9f, 0.75f, 0.6f));
                stock.Text = _world.Hub[(int)item].ToString();
            }
        }

        private void SellAll(ItemId item)
        {
            int stock = _world.Hub[(int)item];
            if (stock <= 0) { Toast("沒有可賣的 " + Loc.Item(item)); return; }
            int unit = _world.ItemPrice(item);
            if (_world.Sell(item, stock))
            {
                PlaySfx?.Invoke("coin");
                Toast($"賣出 {Loc.Item(item)} ×{stock}　+${unit * stock}");
            }
        }

        // ── 星球遷移 ────────────────────────────────────────────
        private void BuildPlanetPanel()
        {
            _planetPanel = new PanelContainer();
            _planetPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.05f, 0.07f, 0.11f, 0.96f), new Color(0.4f, 0.8f, 1f, 0.4f)));
            _planetPanel.AnchorLeft = 0.5f; _planetPanel.AnchorRight = 0.5f;
            _planetPanel.AnchorTop = 0.5f; _planetPanel.AnchorBottom = 0.5f;
            _planetPanel.OffsetLeft = -300f; _planetPanel.OffsetRight = 300f;
            _planetPanel.OffsetTop = -220f; _planetPanel.OffsetBottom = 220f;
            _planetPanel.Visible = false;
            _root.AddChild(_planetPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _planetPanel.AddChild(scroll);
            var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            outer.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(outer);
            outer.AddChild(Fonts2D.Label(Loc.T("ui.travel_hint"), 14, new Color(0.6f, 0.85f, 1f)));
            _planetList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            _planetList.AddThemeConstantOverride("separation", 6);
            outer.AddChild(_planetList);
        }

        /// <summary>每次開啟星球面板都重建：解鎖狀態、金幣與債務會即時反映。</summary>
        private void RefreshPlanetList()
        {
            if (_planetList == null || _world == null) return;
            for (int i = _planetList.GetChildCount() - 1; i >= 0; i--)
                _planetList.GetChild(i).QueueFree();
            _planetCardUpdaters.Clear();
            _planetListPlanet = _world.PlanetIndex;

            for (int i = 0; i < Planets.All.Length; i++)
            {
                BuildPlanetCard(Planets.All[i], i);
                if (i < Planets.All.Length - 1) _planetList.AddChild(new HSeparator());
            }
            RefreshPlanetCards();
        }

        /// <summary>只更新會隨遊戲狀態變動的東西（按鈕三態、禁用原因），不重建整份版面。</summary>
        private void RefreshPlanetCards()
        {
            if (_planetList == null || _world == null) return;
            if (_planetListPlanet != _world.PlanetIndex) { RefreshPlanetList(); return; }
            for (int i = 0; i < _planetCardUpdaters.Count; i++) _planetCardUpdaters[i]?.Invoke();
        }

        private void BuildPlanetCard(PlanetDef p, int idx)
        {
            bool here = idx == _world.PlanetIndex;

            // ── 卡片外框：目前所在用金色高亮邊 ──
            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(
                here ? new Color(0.10f, 0.13f, 0.18f, 0.98f) : new Color(0.05f, 0.07f, 0.11f, 0.96f),
                here ? new Color(1f, 0.85f, 0.35f, 0.90f) : new Color(0.40f, 0.80f, 1f, 0.28f)));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            card.AddChild(row);

            row.AddChild(new PlanetThumb(p, here));

            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
            info.AddThemeConstantOverride("separation", 3);
            info.CustomMinimumSize = new Vector2(432, 0);
            row.AddChild(info);

            // ── 名稱 + 狀態標記 ──
            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 8);
            titleRow.AddChild(Fonts2D.Label($"{idx + 1}. {Loc.Planet(p)}", 16,
                here ? new Color(1f, 0.90f, 0.50f) : new Color(0.85f, 0.95f, 1f)));
            var tag = Fonts2D.Label("", 11, new Color(0.75f, 0.80f, 0.88f));
            titleRow.AddChild(tag);
            info.AddChild(titleRow);

            var desc = Fonts2D.Label(Loc.T(p.DescKey ?? ""), 12, new Color(0.70f, 0.76f, 0.84f));
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            info.AddChild(desc);

            info.AddChild(Fonts2D.Label(
                $"{Loc.T("ui.planet.threat")} ×{p.ThreatScale:F1}　{Loc.T("ui.planet.sun")} ×{p.SolarMultiplier:F1}　" +
                $"{Loc.T("ui.planet.ore_price")} ×{p.PriceMultiplier:F1}　{Loc.T("ui.planet.build_cost")} ×{p.CostMultiplier:F1}",
                12, new Color(0.98f, 0.88f, 0.58f)));

            // ── 專屬礦物（色塊 + 名稱 + 賣價）──
            var ores = new List<(string, Color)>();
            var seen = new HashSet<ItemId>();
            if (p.Ores != null)
                for (int i = 0; i < p.Ores.Length; i++)
                {
                    if (!seen.Add(p.Ores[i].item)) continue;
                    ores.Add(($"{Loc.Item(p.Ores[i].item)}　${_world.ItemPrice(p.Ores[i].item)}",
                              Palette2D.Item(p.Ores[i].item)));
                }
            info.AddChild(BuildSwatchGrid(Loc.T("ui.planet.ores"), ores, 2));

            // ── 專屬蟲種（色塊 + 名稱）──
            var aliens = new List<(string, Color)>();
            if (p.AlienPool != null)
                for (int i = 0; i < p.AlienPool.Length; i++)
                    aliens.Add((Loc.T("alien." + (int)p.AlienPool[i]), Palette2D.AlienBody(p.AlienPool[i])));
            info.AddChild(BuildSwatchGrid(Loc.T("ui.planet.aliens"), aliens, 3));

            // ── 傳送門三態 ──
            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            var go = new Button { CustomMinimumSize = new Vector2(196, 30) };
            go.AddThemeFontOverride("font", Fonts2D.Small);
            go.AddThemeFontSizeOverride("font_size", 13);
            var reason = Fonts2D.Label("", 11, new Color(1f, 0.70f, 0.62f));
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            reason.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            actionRow.AddChild(go);
            actionRow.AddChild(reason);
            info.AddChild(actionRow);

            go.Pressed += () =>
            {
                bool unlocked = _world.IsUnlocked(idx);
                if (idx == _world.PlanetIndex) return;                      // 目前所在：按鈕是停用的
                if (unlocked) { PlaySfx?.Invoke("travel"); TravelRequested?.Invoke(idx); return; }
                if (_world.LoanDebt > 0)
                { PlaySfx?.Invoke("error"); Toast(Loc.T("ui.portal.debt_first") + _world.LoanDebt); return; }
                if (_world.Coins < p.PortalCost)
                { PlaySfx?.Invoke("error"); Toast(Loc.T("ui.portal.lack") + (p.PortalCost - _world.Coins)); return; }
                PlaySfx?.Invoke("travel");
                PortalRequested?.Invoke(idx);
            };

            _planetCardUpdaters.Add(() =>
            {
                bool unlocked = _world.IsUnlocked(idx);
                bool nowHere = idx == _world.PlanetIndex;

                tag.Text = nowHere ? Loc.T("ui.planet.here")
                         : unlocked ? Loc.T("ui.planet.unlocked")
                         : Loc.T("ui.planet.locked");
                tag.AddThemeColorOverride("font_color",
                    nowHere ? new Color(1f, 0.80f, 0.35f)
                  : unlocked ? new Color(0.55f, 0.95f, 0.62f)
                             : new Color(0.70f, 0.72f, 0.80f));

                if (nowHere)
                {
                    go.Disabled = true;
                    go.Text = Loc.T("ui.planet.here");
                    reason.Text = "";
                    return;
                }
                if (unlocked)
                {
                    // ① 已解鎖 → 綠色「前往」
                    go.Disabled = false;
                    go.Text = Loc.T("ui.portal.goto");
                    go.AddThemeColorOverride("font_color", new Color(0.55f, 1f, 0.62f));
                    reason.Text = "";
                    return;
                }

                long lack = p.PortalCost - _world.Coins;
                bool poor = _world.Coins < p.PortalCost;
                bool debt = _world.LoanDebt > 0;
                go.Text = Loc.T("ui.portal.open") + " $" + p.PortalCost;

                if (debt || poor)
                {
                    // ③ 買不起或有欠款 → 灰色 disabled + 明確原因
                    go.Disabled = true;
                    go.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.78f));
                    reason.Text = debt ? Loc.T("ui.portal.debt_first") + _world.LoanDebt
                                       : Loc.T("ui.portal.lack") + Math.Max(0, lack);
                }
                else
                {
                    // ② 未解鎖但買得起 → 「開通傳送門 $N」
                    go.Disabled = false;
                    go.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.40f));
                    reason.Text = "";
                }
            });

            _planetList.AddChild(card);
        }

        /// <summary>一組「色塊 + 名稱」小格（用 GridContainer 限寬，避免面板被撐破）。</summary>
        private static Control BuildSwatchGrid(string title, List<(string name, Color color)> entries, int columns)
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 2);
            box.AddChild(Fonts2D.Label(title + "：", 11, new Color(0.62f, 0.72f, 0.85f)));

            if (entries.Count == 0) return box;

            var grid = new GridContainer { Columns = Math.Max(1, columns) };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 2);
            foreach (var e in entries)
            {
                var cell = new HBoxContainer();
                cell.AddThemeConstantOverride("separation", 4);
                var dot = new ColorRect { Color = e.color, CustomMinimumSize = new Vector2(10, 10) };
                cell.AddChild(dot);
                cell.AddChild(Fonts2D.Label(e.name, 11, new Color(0.88f, 0.93f, 0.98f)));
                grid.AddChild(cell);
            }
            box.AddChild(grid);
            return box;
        }

        // ── 主選單 ──────────────────────────────────────────────
        private void BuildTitleMenu()
        {
            _titlePanel = new PanelContainer();
            _titlePanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.04f, 0.06f, 0.10f, 0.98f), new Color(0.4f, 0.7f, 1f, 0.4f)));
            _titlePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _titlePanel.Visible = false;
            _root.AddChild(_titlePanel);

            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _titlePanel.AddChild(center);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 14);
            center.AddChild(v);

            v.AddChild(Fonts2D.Label("星核工廠：無盡深空 2D", 30, new Color(0.7f, 0.9f, 1f)));
            v.AddChild(Fonts2D.Label("異星自動化工廠 × 蟲潮生存", 14, new Color(0.6f, 0.7f, 0.8f)));

            _continueButton = new Button { Text = Loc.T("ui.continue"), CustomMinimumSize = new Vector2(260, 40) };
            _continueButton.AddThemeFontOverride("font", Fonts2D.Main);
            _continueButton.Pressed += () => ContinueRequested?.Invoke();
            v.AddChild(_continueButton);

            // 新遊戲：只能從第一顆星球開始（其他星球未來用傳送門解鎖）
            var newGame = new Button { Text = Loc.T("ui.new_game"), CustomMinimumSize = new Vector2(260, 40) };
            newGame.AddThemeFontOverride("font", Fonts2D.Main);
            newGame.Pressed += () => NewGameRequested?.Invoke(0);
            v.AddChild(newGame);

            var quit = new Button { Text = "離開遊戲", CustomMinimumSize = new Vector2(260, 36) };
            quit.AddThemeFontOverride("font", Fonts2D.Small);
            quit.Pressed += () => GetTree().Quit();
            v.AddChild(quit);
        }

        public void ShowTitle(bool hasSave)
        {
            _continueButton.Text = hasSave ? Loc.T("ui.continue") : Loc.T("ui.no_save");
            _continueButton.Disabled = !hasSave;
            _titlePanel.Visible = true;
        }

        public void HideTitle() { if (_titlePanel != null) _titlePanel.Visible = false; }

        // ── 失敗畫面 ────────────────────────────────────────────
        private void BuildDefeatPanel()
        {
            _defeatPanel = new PanelContainer();
            _defeatPanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.14f, 0.03f, 0.03f, 0.96f), new Color(1f, 0.3f, 0.3f, 0.6f)));
            _defeatPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _defeatPanel.Visible = false;
            _root.AddChild(_defeatPanel);

            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _defeatPanel.AddChild(center);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 12);
            center.AddChild(v);

            v.AddChild(Fonts2D.Label(Loc.T("ui.defeat"), 26, new Color(1f, 0.5f, 0.5f)));
            v.AddChild(Fonts2D.Label(Loc.T("ui.defeat_hint"), 14, new Color(0.9f, 0.8f, 0.8f)));

            var restart = new Button { Text = "重新開始 (R)", CustomMinimumSize = new Vector2(240, 40) };
            restart.AddThemeFontOverride("font", Fonts2D.Main);
            restart.Pressed += () => NewGameRequested?.Invoke(_world != null ? _world.PlanetIndex : 0);
            v.AddChild(restart);

            var load = new Button { Text = "載入上次存檔 (L)", CustomMinimumSize = new Vector2(240, 36) };
            load.AddThemeFontOverride("font", Fonts2D.Small);
            load.Pressed += () => LoadRequested?.Invoke();
            v.AddChild(load);
        }

        public void ShowDefeat() { if (_defeatPanel != null) _defeatPanel.Visible = true; }
        public void HideDefeat() { if (_defeatPanel != null) _defeatPanel.Visible = false; }

        // ── 蟲潮警報：等玩家按下「迎戰」才真的生成 ──────────────
        private void BuildWavePanel()
        {
            _wavePanel = new PanelContainer();
            _wavePanel.AddThemeStyleboxOverride("panel", Fonts2D.PanelStyle(new Color(0.15f, 0.06f, 0.06f, 0.97f), new Color(1f, 0.35f, 0.30f, 0.75f)));
            _wavePanel.AnchorLeft = 0.5f; _wavePanel.AnchorRight = 0.5f;
            _wavePanel.AnchorTop = 0.5f; _wavePanel.AnchorBottom = 0.5f;
            _wavePanel.OffsetLeft = -230f; _wavePanel.OffsetRight = 230f;
            _wavePanel.OffsetTop = -110f; _wavePanel.OffsetBottom = 110f;
            _wavePanel.Visible = false;
            _root.AddChild(_wavePanel);

            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 8);
            _wavePanel.AddChild(v);

            _waveTitleLabel = Fonts2D.Label(Loc.T("ui.wave.confirm_title"), 18, new Color(1f, 0.62f, 0.52f));
            v.AddChild(_waveTitleLabel);

            _waveBodyLabel = Fonts2D.Label("", 14, new Color(1f, 0.92f, 0.88f));
            _waveBodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _waveBodyLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(_waveBodyLabel);

            var hint = Fonts2D.Label(Loc.T("ui.wave.confirm_hint"), 11, new Color(0.76f, 0.72f, 0.70f));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            v.AddChild(hint);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var fight = new Button { Text = Loc.T("ui.wave.fight"), CustomMinimumSize = new Vector2(170, 38) };
            fight.AddThemeFontOverride("font", Fonts2D.Small);
            fight.AddThemeFontSizeOverride("font_size", 14);
            fight.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
            fight.Pressed += () => WaveAccepted?.Invoke();
            row.AddChild(fight);

            var delay = new Button { Text = Loc.T("ui.wave.delay"), CustomMinimumSize = new Vector2(150, 38) };
            delay.AddThemeFontOverride("font", Fonts2D.Small);
            delay.AddThemeFontSizeOverride("font_size", 13);
            delay.Pressed += () => WaveDelayed?.Invoke();
            row.AddChild(delay);
            v.AddChild(row);
        }

        /// <summary>與 SimWorld.WavePending 同步：pending 才彈出、pending 結束就收掉。</summary>
        private void SyncWavePanel()
        {
            if (_world == null || _wavePanel == null) return;
            if (_world.WavePending)
            {
                if (_wavePanel.Visible || TitleVisible || DefeatVisible) return;
                _waveTitleLabel.Text = Loc.T("ui.wave.confirm_title");
                _waveBodyLabel.Text = string.Format(Loc.T("ui.wave.confirm_body"),
                    _world.WaveIndex + 1, _world.WavePendingCount, _world.WavePendingThreat.ToString("F1"));
                _wavePanel.Visible = true;
                PlaySfx?.Invoke("alert");
            }
            else if (_wavePanel.Visible)
            {
                _wavePanel.Visible = false;
            }
        }

        /// <summary>由 Main2D 在真的生成蟲潮／延後之後呼叫，立刻收掉面板。</summary>
        public void HideWavePanel() { if (_wavePanel != null) _wavePanel.Visible = false; }

        // ── 底部提示 ────────────────────────────────────────────
        private void BuildHints()
        {
            _selectionLabel = Fonts2D.Label("", 13, new Color(0.6f, 0.9f, 0.6f));
            _selectionLabel.AnchorLeft = 0f; _selectionLabel.AnchorTop = 1f; _selectionLabel.AnchorBottom = 1f;
            _selectionLabel.OffsetLeft = 10f; _selectionLabel.OffsetTop = -34f; _selectionLabel.OffsetBottom = -8f;
            _root.AddChild(_selectionLabel);

            var controls = Fonts2D.Label("右鍵拆除 · X 拆除模式 · G 交易 · Y 市場/委託 · P 星球 · F5 存檔 · Esc 設定", 12, new Color(0.6f, 0.65f, 0.7f));
            controls.AnchorLeft = 0f; controls.AnchorRight = 0.5f;
            controls.AnchorTop = 1f; controls.AnchorBottom = 1f;
            controls.OffsetLeft = 10f; controls.OffsetTop = -58f; controls.OffsetBottom = -38f;
            _root.AddChild(controls);

            UpdateSelectionLabel();
        }

        // ── 開關面板（互斥：開一個自動關其他）──────────────────
        public void CloseModals()
        {
            _researchPanel.Visible = false;
            _codexPanel.Visible = false;
            _helpPanel.Visible = false;
            _settingsPanel.Visible = false;
            _sellPanel.Visible = false;
            _livePanel.Visible = false;
            _planetPanel.Visible = false;
            _achPanel.Visible = false;
            _techPanel.Visible = false;
            _portalPanel.Visible = false;
        }

        private void OpenExclusive(PanelContainer p)
        {
            bool target = !p.Visible;
            CloseModals();
            p.Visible = target;
        }

        public void ToggleResearch() => OpenExclusive(_researchPanel);
        public void ToggleCodex() => OpenExclusive(_codexPanel);
        public void ToggleHelp() => OpenExclusive(_helpPanel);
        public void ToggleSettings() => OpenExclusive(_settingsPanel);
        public void ToggleSell() => OpenExclusive(_sellPanel);
        public void ToggleLive()
        {
            OpenExclusive(_livePanel);
            if (_livePanel.Visible) RefreshLivePanel();
        }
        public void TogglePlanet()
        {
            OpenExclusive(_planetPanel);
            if (_planetPanel.Visible) RefreshPlanetList();
        }
        public void ToggleAch()
        {
            OpenExclusive(_achPanel);
            if (_achPanel.Visible) RefreshAchPanel();
        }
        public void ToggleTech()
        {
            OpenExclusive(_techPanel);
            if (_techPanel.Visible) RefreshTechPanel();
        }
        public void TogglePortal()
        {
            OpenExclusive(_portalPanel);
            if (_portalPanel.Visible) RefreshPortalPanel();
        }
        /// <summary>外部（例如解鎖成功後）強制重建星球清單。</summary>
        public void RefreshPlanet() { if (_planetPanel.Visible) RefreshPlanetList(); }
        public void ToggleMinimap() => _minimap.Visible = !_minimap.Visible;   // 角落覆蓋層，不互斥
        public bool SettingsOpen => _settingsPanel.Visible;
        public bool ModalOpen => _settingsPanel.Visible || _helpPanel.Visible || _sellPanel.Visible
                                 || _livePanel.Visible || _achPanel.Visible || _techPanel.Visible
                                 || _portalPanel.Visible
                                 || _planetPanel.Visible || _titlePanel.Visible || _defeatPanel.Visible
                                 || (_wavePanel != null && _wavePanel.Visible);
        public void SetUiHidden(bool h) { UiHidden = h; _root.Visible = !h; }

        // ── 每幀刷新 ────────────────────────────────────────────
        public override void _Process(double delta)
        {
            if (_world == null || UiHidden) return;

            SyncWavePanel();
            UpdateLiveValues((float)delta);

            _minimap?.QueueRedraw();   // 小地圖實時更新

            _refreshTimer -= (float)delta;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.15f;
                RefreshTopBar();
                RefreshInfo();
                RefreshFeed();
                RefreshResearch();
                RefreshSellStocks();
                if (_planetPanel != null && _planetPanel.Visible) RefreshPlanetCards();
                if (WithdrawMode)
                {
                    UpdateSelectionLabel();
                    if (_world.Hub[(int)WithdrawItem] <= 0)
                    {
                        ClearWithdraw(true);
                        Toast(string.Format(Loc.T("ui.withdraw.stock_out"), Loc.Item(WithdrawItem)));
                    }
                }
            }

            if (_lastBuildingCount >= 0 && _world.Buildings.Count < _lastBuildingCount)
                Toast(Loc.T("ui.building_destroyed"));
            _lastBuildingCount = _world.Buildings.Count;

            string advanced = Tutorial2D.Advance(_world);
            if (advanced != null) Toast(advanced);
            if (Tutorial2D.Completed && !Settings2D.TutorialDone)
            {
                Settings2D.TutorialDone = true;
                Settings2D.Save();
                Toast("新手教學完成！接下來自由擴張你的異星工廠");
            }
            if (_objectivePanel != null) _objectivePanel.Visible = !Tutorial2D.Completed;
            if (_objectiveLabel != null)
            {
                string prog = Tutorial2D.Completed ? "完成" : $"{Tutorial2D.Index + 1}/{Tutorial2D.List.Length}";
                _objectiveLabel.Text = $"({prog}) " + Tutorial2D.Current;
            }
            if (_objectiveHint != null) _objectiveHint.Text = Tutorial2D.CurrentHint;
        }

        private void RefreshTopBar()
        {
            var w = _world;
            string daynight = w.Weather.IsNight ? Loc.T("ui.night") : Loc.T("ui.daytime");
            float clock = w.Weather.Time / WeatherSystem.DayLength;
            int hh = (int)(clock * 24f) % 24, mm = (int)(clock * 1440f) % 60;
            string next = w.WavePending ? Loc.T("ui.wave.pending") : $"{Loc.T("ui.wave_next")} {w.WaveTimer:F0}s";
            _topBar.Text =
                $"星核工廠：無盡深空 2D　{Loc.Planet(w.Planet)}　{hh:D2}:{mm:D2} {daynight}　{Loc.Weather(w.Weather.Current)}\n" +
                $"金幣 {w.Coins}{(w.LoanDebt > 0 ? "　債務 " + w.LoanDebt : "")}　發電 {w.PowerGenerated:F0}　耗電 {w.PowerUsed:F0}　電池 {w.BatteryStored:F0}/{GameData.BatteryCapacity:F0}　效率 {(w.Efficiency * 100f):F0}%\n" +
                $"科研 帶{w.Research[0]} 機{w.Research[1]} 採{w.Research[2]} 電{w.Research[3]}　波次 {w.WaveIndex}　{next}　威脅 {w.ThreatLevel:F1}×" +
                EventBanner(w);
        }

        /// <summary>頂列右側的即時狀態：世界事件、委託與熱門貨。</summary>
        private string EventBanner(SimWorld w)
        {
            if (w.PendingCrisis != CrisisKind.None)
                return "\n⚠ " + string.Format(Loc.T("event.warning"), Loc.T("event." + (int)w.PendingCrisis))
                       + $" {w.PendingCrisisIn:0}s";
            if (w.CrisisActive)
                return "\n▶ " + Loc.T("event." + (int)w.CurrentCrisis) + $" {w.CrisisRemaining:0}s";
            var c = w.Contract;
            if (c != null && c.Accepted)
                return "\n" + Loc.T("ui.contract") + "：" + Loc.Item(c.Item) + $" {c.Delivered}/{c.Required}  {Math.Max(0, (int)c.Remaining)}s";
            return "\n" + Loc.T("ui.market.hot") + "：" + Loc.Item(w.HotItem) + " ×1.5";
        }

        private void RefreshInfo()
        {
            Vector2 mouseW = _cam.ScreenToWorld(GetViewport().GetMousePosition());
            int tx = GridCoord.WorldToTileX(mouseW.X);
            int tz = GridCoord.WorldToTileZ(mouseW.Y);
            string desc = DescribeHover(tx, tz);
            _infoBody.Text = desc;
            if (_infoTool != null) _infoTool.Text = DescribeTool();
        }

        /// <summary>放置前就讓玩家看到所選建築的完整屬性（造價/回收/耗電/進出料/說明）。</summary>
        private string DescribeTool()
        {
            if (_world == null) return "";
            if (DemolishMode)
                return "【拆除模式】左鍵拆除建築／傳送帶\n（右鍵任何時候都可直接拆除）\n按 X 或 Esc 回到建造模式";

            var kind = SelectedTool;
            var sb = new StringBuilder();
            sb.Append("▶ 準備放置：").Append(Loc.Building(kind)).Append('\n');
            sb.Append("造價 ").Append(_world.BuildingPrice(kind)).Append(" 金幣　拆除回收 ").Append(_world.SellValue(kind)).Append('\n');

            float pw = GameData.PowerDemand(kind);
            if (pw > 0f) sb.Append("耗電 ").Append(pw.ToString("F0")).Append("（沒電會變慢/停擺）\n");

            switch (kind)
            {
                case BuildingKind.SolarPanel:
                    sb.Append("發電：白天供電（×").Append(_world.Planet.SolarMultiplier.ToString("F1")).Append(" 日照）\n");
                    break;
                case BuildingKind.ThermalGenerator:
                    sb.Append("發電：燒煤發電（進料口餵煤）\n");
                    break;
                case BuildingKind.Battery:
                    sb.Append("儲電：白天存電、晚上放電\n");
                    break;
                case BuildingKind.Miner:
                    sb.Append("必須蓋在礦脈上（採出速度 ×豐度）\n");
                    break;
                case BuildingKind.Smelter:
                case BuildingKind.Assembler:
                {
                    var r = GameData.GetRecipe(new Building { Kind = kind, RecipeIndex = RecipeIndex });
                    if (r.Seconds > 0f)
                    {
                        sb.Append("配方：");
                        sb.Append(Loc.Item(r.In0)).Append("×").Append(r.N0);
                        if (r.In1 != ItemId.None) sb.Append(" + ").Append(Loc.Item(r.In1)).Append("×").Append(r.N1);
                        sb.Append(" → ").Append(Loc.Item(r.Out)).Append("×").Append(r.NOut)
                          .Append("（").Append(r.Seconds.ToString("F1")).Append("s）\n");
                    }
                    sb.Append("按 Ctrl+滾輪 換配方");
                    if (kind == BuildingKind.Smelter) sb.Append("（沒指定時會自動辨識送上帶子的礦種）");
                    sb.Append('\n');
                    break;
                }
                case BuildingKind.Turret:
                    sb.Append("彈藥：出廠免費附贈 ").Append(GameData.TurretStartAmmo).Append(" 發\n");
                    sb.Append("補給：傳送帶餵「砲彈」（組裝機做：齒輪＋銅板）\n");
                    break;
            }

            if (kind == BuildingKind.Belt)
                sb.Append("方向＝輸送方向（帶面 V 紋指示，R 旋轉）\n");
            else
            {
                if (Palette2D.HasOutput(kind)) sb.Append("出貨口＝綠箭頭\n");
                if (Palette2D.HasInput(kind)) sb.Append("進料口＝藍箭頭（沒接到料就不會生產）\n");
            }
            sb.Append(Loc.T("building.desc." + (int)kind));
            return sb.ToString();
        }

        private string DescribeHover(int tx, int tz)
        {
            var sb = new StringBuilder();
            if (!GridCoord.InBounds(tx, tz)) return "";

            var b = _world.BuildingAt(tx, tz);
            if (b != null)
            {
                sb.Append(Loc.Building(b.Kind)).Append("（").Append(Loc.T("dir." + (int)b.Facing)).Append("）\n");
                sb.Append("造價 ").Append(_world.BuildingPrice(b.Kind)).Append(" 金幣　回收 ").Append(_world.SellValue(b.Kind)).Append('\n');
                if (b.Kind == BuildingKind.Miner)
                {
                    _world.Terrain.ResourceAt(tx, tz, out var ore, out int rich);
                    sb.Append("挖掘：").Append(Loc.Item(ore)).Append("（豐度 ").Append(rich).Append("）\n");
                }
                else if (b.IsCrafter)
                {
                    var r = GameData.GetRecipe(b);
                    if (r.Seconds > 0f)
                    {
                        sb.Append("配方：").Append(Loc.Item(r.Out));
                        if (b.Producing) sb.Append("　" + (b.Progress / r.Seconds * 100f).ToString("F0")).Append("%");
                        sb.Append('\n');
                    }
                }
                else if (b.IsLogic)
                {
                    sb.Append("通道 ").Append(b.ChannelIn).Append("→").Append(b.ChannelOut)
                      .Append("　數值 ").Append(b.SignalOut).Append("　閾值 ").Append(b.Threshold).Append('\n');
                }
                if (b.Kind == BuildingKind.Turret) sb.Append("彈藥 ").Append(b.Ammo).Append('\n');
                if (b.Blocked) sb.Append("⚠ 出貨堵塞\n");
                if (!b.Enabled) sb.Append("（已關閉）\n");
                sb.Append('\n').Append(Loc.T("building.desc." + (int)b.Kind));
                return sb.ToString();
            }

            var t = _world.BeltAt(tx, tz);
            if (t != null)
            {
                sb.Append(Loc.Building(BuildingKind.Belt)).Append("（").Append(Loc.T("dir." + (int)t.Facing)).Append("）\n");
                sb.Append("帶上物品 ").Append(t.Count).Append('\n');
                sb.Append('\n').Append(Loc.T("building.desc.2"));
                return sb.ToString();
            }

            if (_world.Terrain.ResourceAt(tx, tz, out var o2, out int r2))
            {
                sb.Append(Loc.Item(o2)).Append("（豐度 ").Append(r2).Append("）　賣價 $").Append(_world.ItemPrice(o2)).Append('\n');
                sb.Append('\n').Append(Loc.T("item.desc." + (int)o2));
                sb.Append('\n').Append(Loc.T("ui.ore_hint"));
                return sb.ToString();
            }

            sb.Append("空地\n高度 ").Append(_world.Terrain.HeightAt(tx, tz).ToString("F1"));
            return sb.ToString();
        }

        private void RefreshFeed()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.ttl -= 0.15f;
                if (t.ttl <= 0f) _toasts.RemoveAt(i);
                else _toasts[i] = t;
            }
            foreach (Node n in _feedBox.GetChildren()) n.QueueFree();
            int shown = 0;
            for (int i = _toasts.Count - 1; i >= 0 && shown < 8; i--, shown++)
                _feedBox.AddChild(Fonts2D.Label(_toasts[i].text, 12, new Color(1f, 0.95f, 0.7f)));
        }

        private void RefreshResearch()
        {
            var names = new[] { "ui.research.belt", "ui.research.machine", "ui.research.miner", "ui.research.power" };
            int bi = 0;
            foreach (Node n in _researchBox.GetChildren())
            {
                if (n is Button btn)
                {
                    int i = bi++;
                    if (i < names.Length)
                        btn.Text = $"{Loc.T(names[i])}　{Loc.T("ui.research.level")} {_world.Research[i]}　—　{_world.ResearchCost(i)} {Loc.Item(ItemId.Science)}";
                }
            }
        }

        private void RefreshSellStocks()
        {
            for (int i = 0; i < _sellStockLabels.Count && i < GameData.ItemSlots - 1; i++)
            {
                _sellStockLabels[i].Text = "×" + _world.Hub[i + 1];
                if (i < _sellPriceLabels.Count) _sellPriceLabels[i].Text = "$" + _world.ItemPrice((ItemId)(i + 1));
            }
            if (_debtLabel != null) _debtLabel.Text = "債務 $" + _world.LoanDebt;

            for (int i = 0; i < _withdrawButtons.Count; i++)
            {
                var (item, btn) = _withdrawButtons[i];
                int n = _world.Hub[(int)item];
                btn.Text = $"{Loc.Item(item)} ×{n}";
                btn.Disabled = n <= 0;
            }
        }

        // ── 星球縮圖（純程序化向量：球體 + 明暗漸層 + 明暗環 + 高光）──
        private sealed partial class PlanetThumb : Control
        {
            private readonly PlanetDef _planet;
            private readonly bool _here;

            public PlanetThumb(PlanetDef p, bool here)
            {
                _planet = p;
                _here = here;
                CustomMinimumSize = new Vector2(PlanetThumbSize, PlanetThumbSize);
                MouseFilter = MouseFilterEnum.Ignore;
            }

            public override void _Draw()
            {
                if (_planet == null) return;
                float side = Mathf.Min(Size.X, Size.Y);
                if (side < 8f) return;

                var c = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
                float r = side * 0.5f - 4f;

                var baseC = new Color(_planet.R, _planet.G, _planet.B).Lightened(0.18f);
                var lightC = baseC.Lightened(0.45f);
                var darkC = baseC.Darkened(0.60f);

                // 大氣光暈
                DrawCircle(c, r + 3f, new Color(lightC.R, lightC.G, lightC.B, 0.10f));
                DrawCircle(c, r + 1.5f, new Color(lightC.R, lightC.G, lightC.B, 0.16f));

                // 球體本體
                DrawCircle(c, r, baseC);

                // 明暗漸層（光源在左上 → 往右下疊暗層）
                var dir = new Vector2(0.72f, 0.70f);
                for (int i = 1; i <= 12; i++)
                {
                    float t = i / 12f;
                    DrawCircle(c + dir * (r * 0.88f * t), r * (1f - 0.02f * i),
                        new Color(darkC.R, darkC.G, darkC.B, 0.075f));
                }

                // 明暗環（緯度環帶，讓星球讀起來像氣體巨行星）
                for (int i = 0; i < 3; i++)
                {
                    float rr = r * (0.34f + i * 0.22f);
                    DrawArc(c, rr, 0.18f * MathF.PI, 0.94f * MathF.PI, 24,
                        new Color(darkC.R, darkC.G, darkC.B, 0.18f), 1.6f);
                    DrawArc(c, rr, 1.18f * MathF.PI, 1.94f * MathF.PI, 24,
                        new Color(lightC.R, lightC.G, lightC.B, 0.16f), 1.6f);
                }

                // 高光（左上）
                DrawCircle(c + new Vector2(-r * 0.36f, -r * 0.36f), r * 0.30f, new Color(1f, 1f, 1f, 0.18f));
                DrawCircle(c + new Vector2(-r * 0.42f, -r * 0.44f), r * 0.13f, new Color(1f, 1f, 1f, 0.34f));

                // 輪廓線
                DrawCircle(c, r, new Color(1f, 1f, 1f, 0.20f), false, 1.5f);
                if (_here)
                    DrawCircle(c, r + 3f, new Color(1f, 0.85f, 0.35f, 0.55f), false, 1.5f);
            }
        }

        // ── 小地圖（內嵌控制項）────────────────────────────────
        private sealed partial class Minimap2D : Control
        {
            private SimWorld _world;
            private CameraController2D _cam;
            private readonly List<(int x, int z, ItemId ore)> _ore = new();
            private bool _scanned;
            private const int MapSize = 232;
            private const float Half = 116f;
            private const float MapScale = 0.9f;

            public void Setup(SimWorld w, CameraController2D cam)
            {
                _world = w;
                _cam = cam;
                CustomMinimumSize = new Vector2(MapSize, MapSize);
            }

            private void ScanOre()
            {
                if (_scanned) return;
                _scanned = true;
                for (int z = -130; z <= 130; z += 2)
                    for (int x = -130; x <= 130; x += 2)
                        if (_world.Terrain.ResourceAt(x, z, out var ore, out _) && ore != ItemId.None)
                            _ore.Add((x, z, ore));
            }

            public override void _Draw()
            {
                ScanOre();
                DrawRect(new Rect2(0, 0, MapSize, MapSize), new Color(0.04f, 0.05f, 0.07f, 0.94f));

                foreach (var o in _ore)
                {
                    var p = Map(o.x, o.z);
                    DrawRect(new Rect2(p.X - 1f, p.Y - 1f, 2f, 2f), Palette2D.Item(o.ore));
                }
                foreach (var kv in _world.Buildings)
                {
                    var p = Map(kv.Value.X, kv.Value.Z);
                    DrawRect(new Rect2(p.X - 2f, p.Y - 2f, 4f, 4f), Palette2D.Building(kv.Value.Kind));
                }
                foreach (var pin in _world.Pins)
                    DrawCircle(Map(pin.x, pin.z), 3f, new Color(1f, 0.9f, 0.3f));

                var cm = MapF(_cam.Position.X / GridCoord.TileSizeF, _cam.Position.Y / GridCoord.TileSizeF);
                DrawRect(new Rect2(cm - new Vector2(6f, 6f), new Vector2(12f, 12f)), new Color(1f, 1f, 1f, 0.85f), false, 1.5f);
                DrawRect(new Rect2(0, 0, MapSize, MapSize), new Color(1f, 1f, 1f, 0.25f), false, 1.5f);
            }

            private static Vector2 Map(int x, int z) => new(Half + x * MapScale, Half + z * MapScale);
            private static Vector2 MapF(float x, float z) => new(Half + x * MapScale, Half + z * MapScale);

            public override void _GuiInput(InputEvent e)
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    int x = (int)Mathf.Round((mb.Position.X - Half) / MapScale);
                    int z = (int)Mathf.Round((mb.Position.Y - Half) / MapScale);
                    _cam.SnapTo(GridCoord.TileCenterWorld(x, z));
                }
            }
        }
    }
}
