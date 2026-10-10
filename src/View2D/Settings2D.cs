// 設定：讀寫 user://settings.json（語言 / 音量 / 威脅倍率 / 全螢幕 / 格線）。
using System;
using System.Text.Json;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class Settings2D
    {
        public static Lang Language = Lang.ZH;
        public static float MusicVolume = 0.45f;
        public static float SfxVolume = 0.75f;
        public static float ThreatMultiplier = 1.0f;
        public static bool Fullscreen;
        public static bool ShowGrid = true;
        public static bool TutorialDone;

        private const string Path = "user://settings.json";

        public static void Load()
        {
            try
            {
                if (!FileAccess.FileExists(Path)) return;
                using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(Path));
                var r = doc.RootElement;
                if (r.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String)
                    Language = Loc.LangFromCode(l.GetString());
                if (r.TryGetProperty("music", out var m) && m.ValueKind == JsonValueKind.Number) MusicVolume = m.GetSingle();
                if (r.TryGetProperty("sfx", out var s) && s.ValueKind == JsonValueKind.Number) SfxVolume = s.GetSingle();
                if (r.TryGetProperty("threat", out var t) && t.ValueKind == JsonValueKind.Number) ThreatMultiplier = t.GetSingle();
                if (r.TryGetProperty("fullscreen", out var f)) Fullscreen = f.GetBoolean();
                if (r.TryGetProperty("grid", out var g)) ShowGrid = g.GetBoolean();
                if (r.TryGetProperty("tutorialDone", out var td)) TutorialDone = td.GetBoolean();
            }
            catch { /* 讀不到就維持預設 */ }
        }

        public static void Save()
        {
            try
            {
                var data = new
                {
                    language = Loc.LangCode(Language),
                    music = MusicVolume,
                    sfx = SfxVolume,
                    threat = ThreatMultiplier,
                    fullscreen = Fullscreen,
                    grid = ShowGrid,
                    tutorialDone = TutorialDone,
                };
                using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
                f?.StoreString(JsonSerializer.Serialize(data));
            }
            catch { /* 寫失敗就算了 */ }
        }
    }
}
