// 一個區塊（32×32 格）的資料本體：地形快取 + 實體索引 + 檢視節點把手。
// 地形是 seed 的純函式 → 載入時重算、不進存檔（沿用 Core 的「存檔 = 種子 + 增量」原則）。
using System.Collections.Generic;
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public sealed class Chunk
    {
        public readonly int Cx, Cz;
        public readonly int Key;                 // 打包鍵（與 SimWorld.Key 同規則）
        public readonly int OriginX, OriginZ;    // 區塊左上角的格座標

        // ── 地形快取（每格 1 byte；32×32 = 1024 格 ×4 個 1KB 陣列）──
        public byte[] TerrainKind;               // 0 = 平地, 1 = 礦脈
        public byte[] OreKind;                   // ItemId
        public byte[] OreRich;                   // 豐富度 1..
        public byte[] Shade;                     // 高度著色 0..255（渲染層做明暗變化）
        public bool TerrainReady;

        // ── 實體索引（存 SimWorld 的打包鍵；由 ChunkManager 維護）──
        public readonly List<int> BuildingKeys = new();
        public readonly List<int> BeltKeys = new();
        public bool IndexValid;

        public Node2D View;                      // ChunkView2D（headless 測試時為 null）
        public bool HasEntities => BuildingKeys.Count > 0 || BeltKeys.Count > 0;

        public Chunk(int cx, int cz)
        {
            Cx = cx;
            Cz = cz;
            Key = GridCoord.ChunkKey(cx, cz);
            OriginX = cx * GridCoord.ChunkSize;
            OriginZ = cz * GridCoord.ChunkSize;
        }

        /// <summary>從 Terrain（純函式）重算整個區塊的地形快取。每格 ~4 次雜訊取樣，1024 格成本可忽略。</summary>
        public void FillTerrain(Terrain terrain)
        {
            const int N = GridCoord.ChunkSize;
            TerrainKind ??= new byte[N * N];
            OreKind ??= new byte[N * N];
            OreRich ??= new byte[N * N];
            Shade ??= new byte[N * N];

            float baseH = terrain.Planet.BaseHeight;
            float invAmp = 1f / Mathf.Max(0.001f, terrain.Planet.Amplitude * 1.5f);
            for (int lz = 0; lz < N; lz++)
            {
                int wz = OriginZ + lz;
                int row = lz * N;
                for (int lx = 0; lx < N; lx++)
                {
                    int i = row + lx;
                    float h = terrain.HeightAt(OriginX + lx, wz);
                    float t = Mathf.Clamp((h - baseH) * invAmp * 0.5f + 0.5f, 0f, 1f);
                    Shade[i] = (byte)(t * 255f);
                    if (terrain.ResourceAt(OriginX + lx, wz, out var ore, out int rich))
                    {
                        TerrainKind[i] = 1;
                        OreKind[i] = (byte)ore;
                        OreRich[i] = (byte)Mathf.Min(255, rich);
                    }
                }
            }
            TerrainReady = true;
        }
    }
}
