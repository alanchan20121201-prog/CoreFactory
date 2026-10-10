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
        private readonly List<Label> _sellStockLabels = new();
        private readonly List<Label> _sellPriceLabels = new();
        private Label _debtLabel;
        private PanelContainer _planetPanel;
        private VBoxContainer _planetList;
        private PanelContainer _defeatPanel;
        private Minimap2D _minimap;
        private GridContainer _toolbar;
        private Label _selectionLabel;
        private Label _objectiveLabel;
        private Label _objectiveHint;
        private PanelContainer _objectivePanel;
        private readonly List<Button> _toolButtons = new();

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
            BuildPlanetPanel();
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
            SelectedTool = kind;
            DemolishMode = false;
            if (kind != BuildingKind.Smelter && kind != BuildingKind.Assembler) RecipeIndex = 0;
            RefreshToolbar();
        }

        public void ToggleDemolish()
        {
            DemolishMode = !DemolishMode;
            RefreshToolbar();
            Toast(DemolishMode ? "拆除模式：左鍵拆除建築／傳送帶" : "建造模式：左鍵放置");
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

            for (int i = 0; i < Planets.All.Length; i++)
            {
                int idx = i;
                var p = Planets.All[i];
                bool here = i == _world.PlanetIndex;
                bool unlocked = _world.IsUnlocked(i);

                var card = new VBoxContainer();
                card.AddThemeConstantOverride("separation", 3);
                string tag = here ? "（目前所在）" : (unlocked ? "（已解鎖）" : "🔒 未解鎖");
                card.AddChild(Fonts2D.Label($"{i + 1}. {Loc.Planet(p)} {tag}", 15,
                    here ? new Color(1f, 0.9f, 0.5f) : new Color(0.85f, 0.95f, 1f)));

                var desc = Fonts2D.Label(PlanetDesc(p), 12, new Color(0.7f, 0.75f, 0.8f));
                desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                desc.SizeFlagsHorizontal = Control.SizeFlags.Fill;
                card.AddChild(desc);

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                if (!unlocked)
                {
                    var unlock = new Button { Text = $"開通傳送門（${p.PortalCost}）", CustomMinimumSize = new Vector2(180, 30) };
                    unlock.AddThemeFontOverride("font", Fonts2D.Small);
                    unlock.Pressed += () =>
                    {
                        if (_world.LoanDebt > 0) { PlaySfx?.Invoke("error"); Toast($"還有 ${_world.LoanDebt} 欠款，銀行不接受開通傳送門（先還清）"); return; }
                        if (_world.Coins < p.PortalCost) { PlaySfx?.Invoke("error"); Toast($"金幣不足：需要 ${p.PortalCost}，目前 ${_world.Coins}"); return; }
                        PlaySfx?.Invoke("travel");
                        PortalRequested?.Invoke(idx);
                    };
                    row.AddChild(unlock);
                }
                else if (!here)
                {
                    var go = new Button { Text = "前往此星球", CustomMinimumSize = new Vector2(180, 30) };
                    go.AddThemeFontOverride("font", Fonts2D.Small);
                    go.Pressed += () => { PlaySfx?.Invoke("travel"); TravelRequested?.Invoke(idx); };
                    row.AddChild(go);
                }
                card.AddChild(row);
                _planetList.AddChild(card);
                _planetList.AddChild(new HSeparator());
            }
        }

        private static string PlanetDesc(PlanetDef p)
        {
            var sb = new StringBuilder();
            sb.Append("威脅 ").Append(p.ThreatScale.ToString("F1")).Append("×　日照 ").Append(p.SolarMultiplier.ToString("F1")).Append("×\n");
            if (p.PortalCost > 0)
                sb.Append("傳送門造價 $").Append(p.PortalCost).Append("　");
            sb.Append("礦價 ×").Append(p.PriceMultiplier.ToString("F1"));
            sb.Append("　建造 ×").Append(p.CostMultiplier.ToString("F1")).Append('\n');
            sb.Append(Loc.T(p.DescKey ?? "")).Append('\n');
            sb.Append("礦：");
            var seen = new HashSet<string>();
            for (int i = 0; i < p.Ores.Length; i++)
            {
                string n = Loc.Item(p.Ores[i].item);
                if (!seen.Add(n)) continue;
                sb.Append(n).Append("　");
            }
            sb.Append("\n蟲：");
            for (int i = 0; i < (p.AlienPool?.Length ?? 0); i++)
                sb.Append(Loc.T("alien." + (int)p.AlienPool[i])).Append("　");
            return sb.ToString();
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

        // ── 底部提示 ────────────────────────────────────────────
        private void BuildHints()
        {
            _selectionLabel = Fonts2D.Label("", 13, new Color(0.6f, 0.9f, 0.6f));
            _selectionLabel.AnchorLeft = 0f; _selectionLabel.AnchorTop = 1f; _selectionLabel.AnchorBottom = 1f;
            _selectionLabel.OffsetLeft = 10f; _selectionLabel.OffsetTop = -34f; _selectionLabel.OffsetBottom = -8f;
            _root.AddChild(_selectionLabel);

            var controls = Fonts2D.Label("右鍵拆除 · X 拆除模式 · G 交易 · P 星球 · F5 存檔 · Esc 設定", 12, new Color(0.6f, 0.65f, 0.7f));
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
            _planetPanel.Visible = false;
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
        public void TogglePlanet()
        {
            OpenExclusive(_planetPanel);
            if (_planetPanel.Visible) RefreshPlanetList();
        }
        /// <summary>外部（例如解鎖成功後）強制重建星球清單。</summary>
        public void RefreshPlanet() { if (_planetPanel.Visible) RefreshPlanetList(); }
        public void ToggleMinimap() => _minimap.Visible = !_minimap.Visible;   // 角落覆蓋層，不互斥
        public bool SettingsOpen => _settingsPanel.Visible;
        public bool ModalOpen => _settingsPanel.Visible || _helpPanel.Visible || _sellPanel.Visible
                                 || _planetPanel.Visible || _titlePanel.Visible || _defeatPanel.Visible;
        public void SetUiHidden(bool h) { UiHidden = h; _root.Visible = !h; }

        // ── 每幀刷新 ────────────────────────────────────────────
        public override void _Process(double delta)
        {
            if (_world == null || UiHidden) return;

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
            _topBar.Text =
                $"星核工廠：無盡深空 2D　{Loc.Planet(w.Planet)}　{hh:D2}:{mm:D2} {daynight}　{Loc.Weather(w.Weather.Current)}\n" +
                $"金幣 {w.Coins}{(w.LoanDebt > 0 ? "　債務 " + w.LoanDebt : "")}　發電 {w.PowerGenerated:F0}　耗電 {w.PowerUsed:F0}　電池 {w.BatteryStored:F0}/{GameData.BatteryCapacity:F0}　效率 {(w.Efficiency * 100f):F0}%\n" +
                $"科研 帶{w.Research[0]} 機{w.Research[1]} 採{w.Research[2]} 電{w.Research[3]}　波次 {w.WaveIndex}　下波 {w.WaveTimer:F0}s　威脅 {w.ThreatLevel:F1}×";
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
