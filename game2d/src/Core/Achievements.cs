// ============================================================================
//  Achievements：成就与里程碑目标（18 个）。
//  设计要点：
//   · 纯 C#，零 Godot 依赖
//   · 每个成就有「条件谓词 + 当前进度 + 目标值」，Tick 时统一检测
//   · 达成后通过 Alert 队列通知 UI（"ach:N"），并可存档
//   · 进度数值直接读 SimWorld 现有计数器（AliensKilled / SoldCount / Research 等）
// ============================================================================
using System;
using System.Collections.Generic;

namespace CoreFactory.Core
{
    public sealed class AchievementDef
    {
        public int Id;
        public string NameKey;       // Localization key
        public string DescKey;
        public int Target;           // 目标值（达到即解锁）
        public Func<SimWorld, int> Progress;   // 当前进度
    }

    public static class Achievements
    {
        public static readonly AchievementDef[] All =
        {
            // ── 建造里程碑 ──
            new() { Id = 0,  NameKey = "ach.0.name",  DescKey = "ach.0.desc",  Target = 1,   Progress = w => w.Buildings.Count >= 1 ? 1 : 0 },
            new() { Id = 1,  NameKey = "ach.1.name",  DescKey = "ach.1.desc",  Target = 20,  Progress = w => w.Buildings.Count },
            new() { Id = 2,  NameKey = "ach.2.name",  DescKey = "ach.2.desc",  Target = 50,  Progress = w => w.Buildings.Count },
            new() { Id = 3,  NameKey = "ach.3.name",  DescKey = "ach.3.desc",  Target = 100, Progress = w => w.Buildings.Count },

            // ── 经济 ──
            new() { Id = 4,  NameKey = "ach.4.name",  DescKey = "ach.4.desc",  Target = 10000,  Progress = w => (int)Math.Min(int.MaxValue, w.Coins) },
            new() { Id = 5,  NameKey = "ach.5.name",  DescKey = "ach.5.desc",  Target = 100000, Progress = w => (int)Math.Min(int.MaxValue, w.Coins) },
            new() { Id = 6,  NameKey = "ach.6.name",  DescKey = "ach.6.desc",  Target = 1000,   Progress = w => w.SoldCount },
            new() { Id = 7,  NameKey = "ach.7.name",  DescKey = "ach.7.desc",  Target = 5,      Progress = w => w.ContractsCompleted },

            // ── 研究 ──
            new() { Id = 8,  NameKey = "ach.8.name",  DescKey = "ach.8.desc",  Target = 1,   Progress = w => w.Research[0] + w.Research[1] + w.Research[2] + w.Research[3] >= 1 ? 1 : 0 },
            new() { Id = 9,  NameKey = "ach.9.name",  DescKey = "ach.9.desc",  Target = 20,  Progress = w => w.Research[0] + w.Research[1] + w.Research[2] + w.Research[3] },
            new() { Id = 10, NameKey = "ach.10.name", DescKey = "ach.10.desc", Target = 10,  Progress = w => w.TechNodesUnlocked },

            // ── 战斗 ──
            new() { Id = 11, NameKey = "ach.11.name", DescKey = "ach.11.desc", Target = 10,   Progress = w => w.AliensKilled },
            new() { Id = 12, NameKey = "ach.12.name", DescKey = "ach.12.desc", Target = 100,  Progress = w => w.AliensKilled },
            new() { Id = 13, NameKey = "ach.13.name", DescKey = "ach.13.desc", Target = 10,   Progress = w => w.WaveIndex },

            // ── 探索 ──
            new() { Id = 14, NameKey = "ach.14.name", DescKey = "ach.14.desc", Target = 2,   Progress = w => w.UnlockedCount },
            new() { Id = 15, NameKey = "ach.15.name", DescKey = "ach.15.desc", Target = 5,   Progress = w => w.UnlockedCount },
            new() { Id = 16, NameKey = "ach.16.name", DescKey = "ach.16.desc", Target = 20,  Progress = w => w.ItemsTransferred },

            // ── 资深 ──
            new() { Id = 17, NameKey = "ach.17.name", DescKey = "ach.17.desc", Target = 3600, Progress = w => (int)w.Elapsed },
        };

        public const int Count = 18;
    }
}
