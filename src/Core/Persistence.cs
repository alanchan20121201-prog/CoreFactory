// 藍圖（版本化 + 壓縮）與 zip 工具
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CoreFactory.Core
{
    public static class Zip
    {
        public static byte[] Compress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) gz.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return outMs.ToArray();
        }
    }

    /// <summary>
    /// 藍圖字串：CFBP1:base64(gzip(text))。
    /// 版本號在 header 裡 —— 未來改格式時可以在這裡加遷移，而不是讓舊藍圖靜默失效。
    /// </summary>
    public static class Blueprint
    {
        public const string Header = "CFBP";
        public const int FormatVersion = 1;

        public struct Entry
        {
            public BuildingKind Kind;
            public int X, Z;
            public Dir Facing;
            public byte RecipeIndex;
            public int KindValue;
        }

        public static string Encode(IReadOnlyList<Entry> entries)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append(FormatVersion).Append('\n');
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.Append((int)e.Kind).Append(',')
                  .Append(e.X).Append(',')
                  .Append(e.Z).Append(',')
                  .Append((int)e.Facing).Append(',')
                  .Append(e.RecipeIndex).Append(',')
                  .Append(e.KindValue).Append('\n');
            }
            return Header + FormatVersion + ":" + Convert.ToBase64String(Zip.Compress(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        public static bool TryDecode(string text, out List<Entry> entries, out string error)
        {
            entries = new List<Entry>();
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "empty"; return false; }
            text = text.Trim();
            int colon = text.IndexOf(':');
            string head = colon > 0 ? text.Substring(0, colon) : "";
            if (!head.StartsWith(Header)) { error = "bad header"; return false; }
            if (!int.TryParse(head.Substring(Header.Length), out int ver)) { error = "bad version"; return false; }
            if (ver < 1 || ver > FormatVersion) { error = "unsupported version " + ver; return false; }

            byte[] raw;
            try { raw = Zip.Decompress(Convert.FromBase64String(text.Substring(colon + 1))); }
            catch (Exception ex) { error = "decode: " + ex.Message; return false; }

            string body = Encoding.UTF8.GetString(raw);
            string[] lines = body.Split('\n');
            for (int i = 1; i < lines.Length; i++)   // 第 0 行是 header
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] f = line.Split(',');
                if (f.Length < 6) continue;
                if (!int.TryParse(f[0], out int kind)) continue;
                if (!int.TryParse(f[1], out int x)) continue;
                if (!int.TryParse(f[2], out int z)) continue;
                if (!int.TryParse(f[3], out int facing)) continue;
                if (!int.TryParse(f[4], out int recipe)) continue;
                if (!int.TryParse(f[5], out int kv)) continue;
                entries.Add(new Entry
                {
                    Kind = (BuildingKind)kind, X = x, Z = z,
                    Facing = (Dir)(facing & 3), RecipeIndex = (byte)recipe, KindValue = kv,
                });
            }
            return true;
        }
    }
}
