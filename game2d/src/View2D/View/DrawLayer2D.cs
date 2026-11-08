// 純繪製層：掛一個回呼，_Draw 時呼叫。
// 地形層 / 實體層分離——地形只在載入與選項切換時重畫；實體層才需要每 tick 重畫。
using System;
using Godot;

namespace CoreFactory.View2D
{
    public sealed partial class DrawLayer2D : Node2D
    {
        public Action<DrawLayer2D> Drawer;

        public override void _Draw() => Drawer?.Invoke(this);
    }
}
