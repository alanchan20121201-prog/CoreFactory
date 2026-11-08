// ============================================================================
//  TechTree：里程碑科技树（4 条线 × 5 节点 = 20 节点）。
//  设计要点：
//   · 与现有 Research[4]（无限升级 +6%）并存：科技树给「质变」效果（倍率/解锁）
//   · 每个节点有前置条件 + 科研包成本，解锁后永久生效
//   · 效果直接改 SimWorld 的倍率属性，不改核心生产逻辑
// ============================================================================
using System;

namespace CoreFactory.Core
{
    public enum TechLine : byte
    {
        Belt = 0,       // 传送带线
        Machine = 1,    // 机器线
        Mining = 2,     // 采矿线
        Power = 3,      // 发电线
    }

    public sealed class TechNode
    {
        public int Id;              // 全局唯一（0..19）
        public TechLine Line;
        public int Tier;            // 0..4（树的层级）
        public string NameKey;
        public string DescKey;
        public int CostScience;     // 消耗科研包数
        public int RequireLevel;    // 前置：同线 Research 等级
    }

    public static class TechTree
    {
        // 效果倍率：每条线每个层级解锁后给 SimWorld 的加成
        // 传送带线：+8% 带速/节点
        // 机器线：+10% 机器速度/节点
        // 采矿线：+12% 采矿速度/节点
        // 发电线：+15% 发电量/节点

        public static readonly TechNode[] Nodes = BuildNodes();

        private static TechNode[] BuildNodes()
        {
            var arr = new TechNode[20];
            for (int line = 0; line < 4; line++)
            {
                for (int tier = 0; tier < 5; tier++)
                {
                    int id = line * 5 + tier;
                    arr[id] = new TechNode
                    {
                        Id = id,
                        Line = (TechLine)line,
                        Tier = tier,
                        NameKey = $"tech.{line}.{tier}",
                        DescKey = $"tech.desc.{line}.{tier}",
                        CostScience = 30 + tier * 45,
                        RequireLevel = tier == 0 ? 0 : tier * 2,
                    };
                }
            }
            return arr;
        }

        /// <summary>解锁一个科技节点：需科研包足够 + 前置满足 + 未解锁。</summary>
        public static bool TryUnlock(SimWorld w, int nodeId, out string err)
        {
            err = "";
            if (nodeId < 0 || nodeId >= Nodes.Length) { err = "tech.bad"; return false; }
            var node = Nodes[nodeId];
            if (w.IsTechUnlocked(nodeId)) { err = "tech.already"; return false; }
            if (w.Research[(int)node.Line] < node.RequireLevel) { err = "tech.need_research"; return false; }
            if (w.Hub[(int)ItemId.Science] < node.CostScience) { err = "tech.need_science"; return false; }

            w.Hub[(int)ItemId.Science] -= node.CostScience;
            w.UnlockTechNode(nodeId);
            return true;
        }
    }
}
