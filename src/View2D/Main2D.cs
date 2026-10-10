// Main2D：2D 版接線點。
//  · 主選單（繼續 / 新遊戲選星球）、自動/手動存讀檔、星球遷移、失敗畫面
//  · 建造互動、晝夜天氣、中文 UI、程序化音樂音效、新手教學、金幣經濟
// 開發參數（放在 -- 之後）：--selftest / --shot=PREFIX / --seed=N / --planet=N / --rig / --aliens=N
using System;
using System.Collections.Generic;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed partial class Main2D : Node2D
    {
        private const string DataPackPath = "res://data/core.json";
        private const string UserOverridePath = "user://data/core.json";
        private const string SavePath = "user://save.bin";
        private const float TickDt = 1f / 60f;

        private SimWorld _world;
        private ChunkManager _chunks;
        private WorldGrid2D _grid;
        private CameraController2D _cam;
        private Overlay2D _overlay;
        private Ambient2D _ambient;
        private Audio2D _audio;
        private Ui2D _ui;
        private Node2D _worldRoot;

        private double _acc;
        private int _maxSteps = 4;
        private bool _paused;
        private bool _inMenu;
        private bool _defeated;
        private bool _simTicked;
        private bool _hadBase;

        private string _clipboard;
        private float _autoSaveTimer;
        private int _rigX, _rigZ;

        private int _lastBuildingCount = -1;
        private int _lastWave;
        private int _lastResearchSum;

        // ── 開發參數 ──
        private bool _selfTest;
        private bool _noRig = true;
        private string _shotPrefix;
        private int _seed = 20261010;
        private int _planet;
        private int _aliensDemo;
        private int _frame;
        private int _shotIndex;
        private readonly int[] _shotFrames = { 60, 160, 280 };

        public override void _Ready()
        {
            ParseArgs();

            if (_selfTest)
            {
                int fail = SelfTest2D.Run();
                GD.Print($"[selftest] RESULT: {(fail == 0 ? "ALL PASS" : fail + " CHECK(S) FAILED")}");
                GetTree().Quit(fail == 0 ? 0 : 1);
                return;
            }

            LoadContentPack();
            Settings2D.Load();
            Loc.Set(Settings2D.Language);
            ChunkView2D.ShowGrid = Settings2D.ShowGrid;
            Tutorial2D.Index = Settings2D.TutorialDone ? Tutorial2D.List.Length : 0;
            if (Settings2D.Fullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);

            _audio = new Audio2D { Name = "Audio" };
            AddChild(_audio);
            _audio.Setup();

            _cam = new CameraController2D { Name = "Camera" };
            AddChild(_cam);
            _overlay = new Overlay2D { Name = "Overlay", ZIndex = 100 };   // 高 ZIndex：永遠畫在地形之上（蟲/幽靈/游標）
            AddChild(_overlay);
            _ambient = new Ambient2D { Name = "Ambient" };
            AddChild(_ambient);
            _ambient.Setup(null);

            StartWorld(_planet, false);

            _ui = new Ui2D { Name = "UI" };
            AddChild(_ui);
            _ui.Setup(_world, _chunks, _grid, _cam);
            _ui.PlaySfx = n => _audio.PlaySfx(n);
            _ui.ContinueRequested += ContinueGame;
            _ui.NewGameRequested += NewGame;
            _ui.TravelRequested += Travel;
            _ui.PortalRequested += UnlockPortal;
            _ui.LoadRequested += LoadGame;
            _ui.MenuRequested += BackToMenu;

            if (_aliensDemo > 0) _world.DebugSpawnAliens(_aliensDemo, _rigX + 8f, _rigZ + 6f, 7f);

            bool devMode = _shotPrefix != null || !_noRig || _aliensDemo > 0;
            if (devMode)
            {
                if (DisplayServer.GetName() == "headless") Engine.TimeScale = 30.0;
                else if (_shotPrefix != null) Engine.TimeScale = 4.0;
            }
            else
            {
                _ui.ShowTitle(HasSave());
                _inMenu = true;
                _paused = true;
            }

            GD.Print($"[CoreFactory2D] ready seed={_seed} planet={_planet} buildings={_world.BuildingCount} belts={_world.BeltCount}");
        }

        // ── 世界生命週期 ────────────────────────────────────────
        private void StartWorld(int planetIndex, bool carry)
        {
            long coins = 0; int[] hub = null, research = null; int kills = 0; long unlocked = 1;
            if (carry && _world != null)
            {
                coins = _world.Coins;
                hub = (int[])_world.Hub.Clone();
                research = (int[])_world.Research.Clone();
                kills = _world.AliensKilled;
                unlocked = _world.UnlockedMask;
            }

            _planet = planetIndex;
            _world = new SimWorld(_seed, planetIndex);
            _world.ThreatMultiplier = Settings2D.ThreatMultiplier;
            if (carry)
            {
                _world.Coins = coins;
                for (int i = 0; i < hub.Length; i++) _world.Hub[i] = hub[i];
                for (int i = 0; i < 4; i++) _world.Research[i] = research[i];
                _world.AliensKilled = kills;
                _world.UnlockedMask = unlocked | 1;   // 起始星球永遠已解鎖
            }

            if (_worldRoot != null) { _worldRoot.Free(); _worldRoot = null; }
            _worldRoot = new Node2D { Name = "World" };
            AddChild(_worldRoot);
            _chunks = new ChunkManager(_world, _worldRoot);
            _grid = new WorldGrid2D(_world, _chunks);

            _rigX = _rigZ = 0;
            bool snapped = false;
            if (!_noRig && StarterRig2D.Build(_world, out int px, out int pz, out _))
            {
                _rigX = px; _rigZ = pz;
                _cam.SnapTo(GridCoord.TileCenterWorld(px, pz));
                snapped = true;
            }
            if (!snapped)
            {
                if (StarterRig2D.FindOre(_world, out int ox, out int oz))
                    _cam.SnapTo(GridCoord.TileCenterWorld(ox, oz));
                else
                    _cam.SnapTo(Vector2.Zero);
            }

            _overlay.Setup(_world, _cam);
            _ambient.SetWorld(_world);

            _lastBuildingCount = _world.Buildings.Count;
            _lastWave = _world.WaveIndex;
            _lastResearchSum = _world.Research[0] + _world.Research[1] + _world.Research[2] + _world.Research[3];
            _clipboard = null;
            _defeated = false;
            _hadBase = _world.Buildings.Count >= 3;
            _acc = 0;
        }

        private void ContinueGame()
        {
            if (!HasSave()) { _ui.Toast(Loc.T("ui.no_save")); return; }
            var w = new SimWorld(1, 0);
            if (!w.LoadFrom(ProjectSettings.GlobalizePath(SavePath), out string err))
            { _ui.Toast("載入失敗：" + err); return; }
            _seed = w.Seed;
            _world = w;
            _world.ThreatMultiplier = Settings2D.ThreatMultiplier;
            _planet = w.PlanetIndex;
            BuildLoadedView();
            _ui.HideTitle();
            _inMenu = false; _paused = false; _defeated = false;
            _ui.Toast("已載入存檔");
            _audio.PlaySfx("research");
        }

        private void NewGame(int planetIndex)
        {
            StartWorld(planetIndex, false);
            _ui.SetWorld(_world, _chunks, _grid, _cam);
            _ui.HideTitle(); _ui.HideDefeat();
            _inMenu = false; _paused = false; _defeated = false;
            Tutorial2D.Index = 0;
            Settings2D.TutorialDone = false;
        }

        private void UnlockPortal(int planetIndex)
        {
            if (_world == null) return;
            if (_world.TryUnlockPortal(planetIndex, out string err))
            {
                var p = Planets.All[planetIndex];
                _ui.Toast($"傳送門已開通：{Loc.Planet(p)}（花費 ${p.PortalCost}）");
                _audio.PlaySfx("research");
                _ui.RefreshPlanet();      // 立刻把「前往」按鈕換上來
            }
            else
            {
                _audio.PlaySfx("error");
                _ui.Toast(err switch
                {
                    "ui.portal.debt" => $"還有 ${_world.LoanDebt} 欠款，必須先還清才能開通傳送門",
                    "ui.portal.poor" => $"金幣不足，需要 ${Planets.All[planetIndex].PortalCost}",
                    "ui.portal.already" => "此星球已解鎖",
                    _ => "無法開通傳送門",
                });
            }
        }

        private void Travel(int planetIndex)
        {
            if (_world != null && planetIndex == _world.PlanetIndex) { _ui.Toast("已在此星球"); return; }
            if (_world != null && !_world.IsUnlocked(planetIndex))
            {
                _audio.PlaySfx("error");
                _ui.Toast("尚未開通傳送門（星球面板 P 裡可花費金幣開通，且不能有欠款）");
                return;
            }
            StartWorld(planetIndex, true);
            _ui.SetWorld(_world, _chunks, _grid, _cam);
            _ui.HideDefeat();
            _inMenu = false; _paused = false; _defeated = false;
            _ui.Toast("已遷移到 " + Loc.Planet(_world.Planet));
            _audio.PlaySfx("travel");
        }

        private void BuildLoadedView()
        {
            if (_worldRoot != null) { _worldRoot.Free(); _worldRoot = null; }
            _worldRoot = new Node2D { Name = "World" };
            AddChild(_worldRoot);
            _chunks = new ChunkManager(_world, _worldRoot);
            _grid = new WorldGrid2D(_world, _chunks);

            if (StarterRig2D.FindOre(_world, out int ox, out int oz))
                _cam.SnapTo(GridCoord.TileCenterWorld(ox, oz));
            else
                _cam.SnapTo(Vector2.Zero);

            _overlay.Setup(_world, _cam);
            _ambient.SetWorld(_world);
            _ui.SetWorld(_world, _chunks, _grid, _cam);
            _lastBuildingCount = _world.Buildings.Count;
            _lastWave = _world.WaveIndex;
            _lastResearchSum = _world.Research[0] + _world.Research[1] + _world.Research[2] + _world.Research[3];
            _hadBase = _world.Buildings.Count >= 3;
            _clipboard = null;
        }

        private void BackToMenu()
        {
            if (_world != null && !_inMenu)
            {
                try { _world.SaveTo(ProjectSettings.GlobalizePath(SavePath)); } catch { }
            }
            _ui.CloseModals();
            _inMenu = true; _paused = true;
            _ui.ShowTitle(HasSave());
        }

        // ── 存讀檔 ──────────────────────────────────────────────
        private static bool HasSave() => FileAccess.FileExists(SavePath);

        private void SaveGame()
        {
            if (_world == null || _inMenu) return;
            try { _world.SaveTo(ProjectSettings.GlobalizePath(SavePath)); } catch { return; }
            _ui.Toast(Loc.T("ui.saved"));
            _audio.PlaySfx("coin");
        }

        private void LoadGame()
        {
            if (!HasSave()) { _ui.Toast(Loc.T("ui.no_save")); return; }
            var w = new SimWorld(1, 0);
            if (!w.LoadFrom(ProjectSettings.GlobalizePath(SavePath), out string err))
            { _ui.Toast("載入失敗：" + err); return; }
            _seed = w.Seed; _planet = w.PlanetIndex;
            _world = w;
            _world.ThreatMultiplier = Settings2D.ThreatMultiplier;
            BuildLoadedView();
            _ui.HideDefeat();
            _defeated = false; _paused = false; _inMenu = false;
            _ui.Toast("已載入存檔");
        }

        public override void _Notification(int what)
        {
            if (what == Node.NotificationWMCloseRequest)
            {
                if (_world != null && !_inMenu)
                {
                    try { _world.SaveTo(ProjectSettings.GlobalizePath(SavePath)); } catch { }
                }
                GetTree().Quit();
            }
        }

        // ── 主循環 ──────────────────────────────────────────────
        public override void _Process(double delta)
        {
            if (_world == null) return;
            _frame++;

            if (!_inMenu && !_defeated)
            {
                _autoSaveTimer += (float)delta;
                if (_autoSaveTimer >= 30f)
                {
                    _autoSaveTimer = 0f;
                    try { _world.SaveTo(ProjectSettings.GlobalizePath(SavePath)); } catch { }
                }
            }

            _simTicked = false;
            if (!_paused && !_inMenu && !_defeated)
            {
                _acc += delta;
                int steps = 0;
                while (_acc >= TickDt && steps < _maxSteps)
                {
                    _world.Tick(TickDt);
                    _acc -= TickDt;
                    steps++;
                }
                if (steps >= _maxSteps) _acc = 0;
                _simTicked = steps > 0;
            }

            if (_simTicked) _chunks.EnsureFresh();
            _chunks.Update(CameraWorldRect(), _simTicked ? 2 : 4);
            if (!_paused) _chunks.QueueEntityRedraws();
            _overlay.QueueRedraw();

            UpdateGhost();
            DetectEvents();
            HandleShots();
        }

        private void DetectEvents()
        {
            if (_world.Buildings.Count >= 3) _hadBase = true;

            if (_lastBuildingCount >= 0 && _world.Buildings.Count < _lastBuildingCount)
                _audio.PlaySfx("destroy");
            _lastBuildingCount = _world.Buildings.Count;

            if (_hadBase && _world.Buildings.Count == 0 && !_defeated && !_inMenu)
            {
                _defeated = true;
                _paused = true;
                _ui.ShowDefeat();
                _audio.PlaySfx("destroy");
            }

            if (_world.WaveIndex > _lastWave) _audio.PlaySfx("alert");
            _lastWave = _world.WaveIndex;

            int rs = _world.Research[0] + _world.Research[1] + _world.Research[2] + _world.Research[3];
            if (rs > _lastResearchSum) _audio.PlaySfx("research");
            _lastResearchSum = rs;
        }

        private void UpdateGhost()
        {
            Vector2 mouse = GetViewport().GetMousePosition();
            Vector2 w = _cam.ScreenToWorld(mouse);
            int gx = GridCoord.WorldToTileX(w.X);
            int gz = GridCoord.WorldToTileZ(w.Y);

            if (_ui.DemolishMode)
            {
                _overlay.ClearGhost();
                return;
            }
            bool ok = GridCoord.InBounds(gx, gz) && !_world.IsOccupied(gx, gz);
            if (ok && _ui.SelectedTool == BuildingKind.Miner)
                ok = _world.Terrain.ResourceAt(gx, gz, out var ore, out _) && ore != ItemId.None;
            _overlay.SetGhost(_ui.SelectedTool, _ui.Facing, gx, gz, ok);
        }

        private Rect2 CameraWorldRect()
        {
            Vector2 size = GetViewportRect().Size / _cam.Zoom;
            return new Rect2(_cam.Position - size * 0.5f, size);
        }

        // ── 輸入 ────────────────────────────────────────────────
        public override void _UnhandledInput(InputEvent e)
        {
            if (_world == null) return;

            if (e is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left) OnLeftClick(mb.Position);
                else if (mb.ButtonIndex == MouseButton.Right) OnRightClick(mb.Position);
                else if (mb.ButtonIndex == MouseButton.WheelUp && Input.IsKeyPressed(Key.Ctrl)) ChangeRecipe(1);
                else if (mb.ButtonIndex == MouseButton.WheelDown && Input.IsKeyPressed(Key.Ctrl)) ChangeRecipe(-1);
                return;
            }

            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;

            if (_defeated)
            {
                if (k.Keycode == Key.R) { NewGame(_planet); }
                else if (k.Keycode == Key.L) { LoadGame(); }
                return;
            }
            if (_inMenu) return;

            switch (k.Keycode)
            {
                case Key.Key1: _ui.SelectTool(BuildingKind.Belt); break;
                case Key.Key2: _ui.SelectTool(BuildingKind.Miner); break;
                case Key.Key3: _ui.SelectTool(BuildingKind.Smelter); break;
                case Key.Key4: _ui.SelectTool(BuildingKind.Assembler); break;
                case Key.Key5: _ui.SelectTool(BuildingKind.Depot); break;
                case Key.Key6: _ui.SelectTool(BuildingKind.SolarPanel); break;
                case Key.Key7: _ui.SelectTool(BuildingKind.ThermalGenerator); break;
                case Key.Key8: _ui.SelectTool(BuildingKind.Battery); break;
                case Key.Key9: _ui.SelectTool(BuildingKind.Turret); break;
                case Key.Key0: _ui.SelectTool(BuildingKind.Lamp); break;
                case Key.Bracketleft: _ui.CycleTool(-1); break;
                case Key.Bracketright: _ui.CycleTool(1); break;
                case Key.R: _ui.Facing = Dirs.Rotate(_ui.Facing, 1); _ui.UpdateSelectionLabel(); break;
                case Key.X: _ui.ToggleDemolish(); break;
                case Key.C: CopyBlueprint(); break;
                case Key.V: PasteBlueprint(); break;
                case Key.G: _ui.ToggleSell(); break;
                case Key.P: _ui.TogglePlanet(); break;
                case Key.F5: SaveGame(); break;
                case Key.M: _ui.ToggleMinimap(); break;
                case Key.U: _ui.ToggleResearch(); break;
                case Key.H: _ui.ToggleHelp(); break;
                case Key.F1: _ui.ToggleCodex(); break;
                case Key.F2: _ui.SetUiHidden(!_ui.UiHidden); break;
                case Key.F3:
                    ChunkView2D.ShowGrid = !ChunkView2D.ShowGrid;
                    Settings2D.ShowGrid = ChunkView2D.ShowGrid;
                    Settings2D.Save();
                    _chunks.QueueTerrainRedraws();
                    break;
                case Key.Escape: _ui.ToggleSettings(); break;
                case Key.Space:
                    _paused = !_paused;
                    _ui.Toast(_paused ? "已暫停" : "繼續");
                    break;
            }
        }

        private void OnLeftClick(Vector2 screenPos)
        {
            if (_ui.ModalOpen || _inMenu || _defeated) return;
            Vector2 w = _cam.ScreenToWorld(screenPos);
            int x = GridCoord.WorldToTileX(w.X), z = GridCoord.WorldToTileZ(w.Y);
            if (!GridCoord.InBounds(x, z)) return;

            if (_ui.DemolishMode)
            {
                if (_world.Remove(x, z)) { _chunks.MarkTileChanged(x, z); _audio.PlaySfx("demolish"); }
                return;
            }

            if (_world.TryPlace(_ui.SelectedTool, x, z, _ui.Facing, _ui.RecipeIndex, -1, out string err))
            {
                _chunks.MarkTileChanged(x, z);
                _audio.PlaySfx("place");
            }
            else
            {
                _ui.Toast(Loc.T(string.IsNullOrEmpty(err) ? "ui.invalid_place" : err));
                _audio.PlaySfx("error");
            }
        }

        private void OnRightClick(Vector2 screenPos)
        {
            if (_ui.ModalOpen || _inMenu || _defeated) return;
            Vector2 w = _cam.ScreenToWorld(screenPos);
            int x = GridCoord.WorldToTileX(w.X), z = GridCoord.WorldToTileZ(w.Y);
            if (!GridCoord.InBounds(x, z)) return;
            if (_world.Remove(x, z)) { _chunks.MarkTileChanged(x, z); _audio.PlaySfx("demolish"); }
        }

        private void ChangeRecipe(int dir)
        {
            int n = GameData.RecipeCount(_ui.SelectedTool);
            if (n <= 0) { _ui.Toast("此工具沒有配方"); return; }
            _ui.RecipeIndex = (byte)(((_ui.RecipeIndex + dir) % n + n) % n);
            var probe = new Building { Kind = _ui.SelectedTool, RecipeIndex = _ui.RecipeIndex };
            _ui.Toast("配方 → " + Loc.Item(GameData.GetRecipe(probe).Out));
        }

        private void CopyBlueprint()
        {
            int ax = GridCoord.WorldToTileX(_cam.Position.X);
            int az = GridCoord.WorldToTileZ(_cam.Position.Y);
            var entries = new List<Blueprint.Entry>();
            foreach (var kv in _world.Buildings)
            {
                var b = kv.Value;
                int dx = b.X - ax, dz = b.Z - az;
                if (Mathf.Abs(dx) <= 8 && Mathf.Abs(dz) <= 8)
                    entries.Add(new Blueprint.Entry { Kind = b.Kind, X = dx, Z = dz, Facing = b.Facing, RecipeIndex = b.RecipeIndex, KindValue = b.KindValue });
            }
            if (entries.Count == 0) { _ui.Toast(Loc.T("ui.copy_empty")); return; }
            _clipboard = Blueprint.Encode(entries);
            _ui.Toast($"{Loc.T("ui.copied")} ({entries.Count})");
        }

        private void PasteBlueprint()
        {
            if (string.IsNullOrEmpty(_clipboard)) { _ui.Toast(Loc.T("ui.copy_empty")); return; }
            if (!Blueprint.TryDecode(_clipboard, out var entries, out _)) { _ui.Toast("藍圖無效"); return; }

            Vector2 w = _cam.ScreenToWorld(GetViewport().GetMousePosition());
            int px = GridCoord.WorldToTileX(w.X), pz = GridCoord.WorldToTileZ(w.Y);
            int placed = 0;
            foreach (var e in entries)
            {
                int x = px + e.X, z = pz + e.Z;
                if (_world.TryPlace(e.Kind, x, z, e.Facing, e.RecipeIndex, e.KindValue, out _))
                {
                    _chunks.MarkTileChanged(x, z);
                    placed++;
                }
            }
            _ui.Toast($"{Loc.T("ui.pasted")} ({placed})");
            _audio.PlaySfx("place");
        }

        // ── 截圖 ────────────────────────────────────────────────
        private void HandleShots()
        {
            if (_shotPrefix == null) return;
            if (DisplayServer.GetName() == "headless") return;
            if (_shotIndex >= _shotFrames.Length || _frame < _shotFrames[_shotIndex]) return;

            var img = GetViewport().GetTexture().GetImage();
            string path = _shotPrefix + _shotIndex + ".png";
            Error err = img.SavePng(path);
            GD.Print($"[shot] frame={_frame} path={path} err={err}");
            _shotIndex++;
            if (_shotIndex >= _shotFrames.Length) GetTree().Quit();
        }

        // ── 資料包 ──────────────────────────────────────────────
        private void LoadContentPack()
        {
            if (!FileAccess.FileExists(UserOverridePath))
            {
                DirAccess.MakeDirRecursiveAbsolute("user://data");
                string res = FileAccess.GetFileAsString(DataPackPath);
                if (!string.IsNullOrWhiteSpace(res))
                {
                    using var f = FileAccess.Open(UserOverridePath, FileAccess.ModeFlags.Write);
                    f?.StoreString(res);
                }
            }
            string baseJson = FileAccess.GetFileAsString(DataPackPath);
            if (ContentJson.Load(baseJson, out string err)) GD.Print("[2d] content pack loaded");
            else GD.PrintErr("[2d] content pack failed: " + err);

            string userJson = FileAccess.GetFileAsString(UserOverridePath);
            if (!string.IsNullOrWhiteSpace(userJson) && ContentJson.Load(userJson, out _, false)) GD.Print("[2d] user override applied");
        }

        private void ParseArgs()
        {
            foreach (string raw in OS.GetCmdlineUserArgs())
            {
                string a = (raw ?? string.Empty).Trim();
                if (a.Length == 0) continue;

                if (a == "--selftest") _selfTest = true;
                else if (a == "--rig") _noRig = false;
                else if (a == "--norig") _noRig = true;
                else if (a.StartsWith("--shot=")) _shotPrefix = a.Substring("--shot=".Length);
                else if (a.StartsWith("--seed=") && int.TryParse(a.Substring("--seed=".Length), out int sv)) _seed = sv;
                else if (a.StartsWith("--planet=") && int.TryParse(a.Substring("--planet=".Length), out int pv)) _planet = pv;
                else if (a.StartsWith("--aliens=") && int.TryParse(a.Substring("--aliens=".Length), out int av)) _aliensDemo = av;
            }
            _planet = Mathf.Clamp(_planet, 0, Planets.All.Length - 1);
        }
    }
}
