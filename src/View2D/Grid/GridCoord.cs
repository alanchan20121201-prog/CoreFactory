// ============================================================================
//  GridCoord：2D 版座標系統的唯一真相來源（Single Source of Truth）
//  · 直接沿用 Core 的 (X, Z) 整數格座標：X 向東、Z 向南（North = -Z）
//  · 2D 俯視角把 X 映射到螢幕 x、Z 映射到螢幕 y —— 不翻轉、不交換，Core 一行都不用改
//  · 世界單位 = 像素：1 格 = TileSize 像素（顯示縮放交給 Camera2D.Zoom）
//  · 區塊 = 32×32 格；座標軟上限 ±32760（SimWorld.Key 以 16-bit 打包，見 Core）
// ============================================================================
using Godot;
using CoreFactory.Core;

namespace CoreFactory.View2D
{
    public static class GridCoord
    {
        public const int TileSize = 32;                       // 每格像素
        public const float TileSizeF = TileSize;
        public const int ChunkBits = 5;
        public const int ChunkSize = 1 << ChunkBits;          // 32×32 格 = 1 區塊
        public const int ChunkMask = ChunkSize - 1;
        public const int TileLimit = 32760;                   // 座標軟上限（尊重 Core 的 16-bit 打包）

        // ── 格 → 區塊 ──────────────────────────────────────────
        /// <summary>算術右移 = 向下取整除法，負座標也正確。</summary>
        public static int ChunkOf(int t) => t >> ChunkBits;
        public static int TileInChunk(int t) => t & ChunkMask;

        // ── 格 ↔ 世界像素 ─────────────────────────────────────
        public static Vector2 TileCenterWorld(int x, int z) => new((x + 0.5f) * TileSizeF, (z + 0.5f) * TileSizeF);
        public static Vector2 TileOriginWorld(int x, int z) => new(x * TileSizeF, z * TileSizeF);
        public static int WorldToTileX(float wx) => Mathf.FloorToInt(wx / TileSizeF);
        public static int WorldToTileZ(float wz) => Mathf.FloorToInt(wz / TileSizeF);
        public static void WorldToTile(Vector2 w, out int x, out int z)
        {
            x = WorldToTileX(w.X);
            z = WorldToTileZ(w.Y);
        }

        public static bool InBounds(int x, int z)
            => x >= -TileLimit && x <= TileLimit && z >= -TileLimit && z <= TileLimit;

        // ── 方向（沿用 Core.Dir；2D 向量：North = -y）─────────
        public static Vector2 DirVec(Dir d) => d switch
        {
            Dir.North => new Vector2(0, -1),
            Dir.East => new Vector2(1, 0),
            Dir.South => new Vector2(0, 1),
            _ => new Vector2(-1, 0),
        };

        // ── 區塊鍵（與 SimWorld.Key 相同的 16-bit 打包，保證字典語意一致）──
        public static int ChunkKey(int cx, int cz) => SimWorld.Key(cx, cz);
    }
}
