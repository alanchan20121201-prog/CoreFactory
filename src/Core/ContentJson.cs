// 資料包載入器：把 data/core.json 的內容覆蓋到 GameData 上。
// 內建資料在 res://data/core.json；玩家可在 user://data/core.json 放覆蓋檔（改平衡不必重新編譯）。
using System;
using System.Text.Json;

namespace CoreFactory.Core
{
    public static class ContentJson
    {
        /// <summary>
        /// 載入 JSON 資料包；resetFirst=false 時可疊加覆蓋（玩家自訂檔只寫要改的欄位）。
        /// 失敗時保留原值，回傳 false 並帶出原因。
        /// </summary>
        public static bool Load(string json, out string error, bool resetFirst = true)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "empty"; return false; }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) { error = "root must be object"; return false; }

                if (resetFirst) GameData.ResetDefaults();

                ReadFloat(root, "beltSpeed", v => GameData.BeltSpeed = v);
                ReadFloat(root, "minerSeconds", v => GameData.MinerSeconds = v);
                ReadFloat(root, "solarPower", v => GameData.SolarPower = v);
                ReadFloat(root, "thermalPower", v => GameData.ThermalPower = v);
                ReadFloat(root, "batteryCapacity", v => GameData.BatteryCapacity = v);
                ReadFloat(root, "turretRange", v => GameData.TurretRange = v);
                ReadFloat(root, "turretInterval", v => GameData.TurretInterval = v);
                ReadFloat(root, "turretDamage", v => GameData.TurretDamage = v);

                if (root.TryGetProperty("recipes", out var recipes) && recipes.ValueKind == JsonValueKind.Object)
                {
                    if (recipes.TryGetProperty("smelter", out var sm)) GameData.SmelterRecipes = ReadRecipes(sm);
                    if (recipes.TryGetProperty("assembler", out var asm)) GameData.AssemblerRecipes = ReadRecipes(asm);
                }

                if (root.TryGetProperty("costs", out var costs) && costs.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in costs.EnumerateObject())
                    {
                        if (!Enum.TryParse<BuildingKind>(prop.Name, true, out var kind)) continue;
                        if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                        var list = new System.Collections.Generic.List<(ItemId, int)>();
                        foreach (var pair in prop.Value.EnumerateArray())
                        {
                            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2) continue;
                            var it = pair[0].GetString();
                            if (!Enum.TryParse<ItemId>(it, true, out var item)) continue;
                            list.Add((item, pair[1].GetInt32()));
                        }
                        GameData.Costs[kind] = list.ToArray();
                    }
                }

                if (root.TryGetProperty("powerDemand", out var power) && power.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in power.EnumerateObject())
                    {
                        if (!Enum.TryParse<BuildingKind>(prop.Name, true, out var kind)) continue;
                        GameData.PowerUse[kind] = prop.Value.GetSingle();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (resetFirst) GameData.ResetDefaults();
                return false;
            }
        }

        private static void ReadFloat(JsonElement root, string name, Action<float> set)
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
                set(el.GetSingle());
        }

        private static Recipe[] ReadRecipes(JsonElement arr)
        {
            if (arr.ValueKind != JsonValueKind.Array) return Array.Empty<Recipe>();
            var list = new System.Collections.Generic.List<Recipe>();
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var r = new Recipe
                {
                    In0 = ParseItem(e, "in"),
                    N0 = (byte)GetInt(e, "n", 1),
                    In1 = ParseItem(e, "in2"),
                    N1 = (byte)GetInt(e, "n2", 0),
                    Out = ParseItem(e, "out"),
                    NOut = (byte)GetInt(e, "nout", 1),
                    Seconds = GetFloat(e, "sec", 1.5f),
                };
                if (r.Out == ItemId.None || r.Seconds <= 0.01f) continue;
                list.Add(r);
            }
            return list.ToArray();
        }

        private static ItemId ParseItem(JsonElement obj, string key)
        {
            if (!obj.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String) return ItemId.None;
            return Enum.TryParse<ItemId>(el.GetString(), true, out var item) ? item : ItemId.None;
        }

        private static int GetInt(JsonElement obj, string key, int def)
            => obj.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : def;

        private static float GetFloat(JsonElement obj, string key, float def)
            => obj.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetSingle() : def;
    }
}
