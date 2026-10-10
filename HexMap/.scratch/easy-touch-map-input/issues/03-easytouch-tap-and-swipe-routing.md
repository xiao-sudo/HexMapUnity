# 03 — 以 EasyTouch 统一 Tap 与单指 Swipe

**What to build:** 玩家可以通过 EasyTouch 轻点 Map Cell 和单指 Swipe 平移相机；从 UI 开始的手势不影响 Map，第二指加入会取消候选 Tap 与单指平移。

**Blocked by:** 02 — 让模式 Handler 控制正交相机.

**Status:** ready-for-review

- [ ] EasyTouch Tap 使用按下位置交给 Dispatcher 解析 Map 点击，且保留 20 像素的 Tap 容差。
- [ ] EasyTouch Swipe 的连续屏幕增量经 Dispatcher 路由，驱动活动模式的 Map 相机平移。
- [ ] UI 起始的单指手势不会产生 Map 点击或平移；已加入多指序列的触点不会在结束时生成点击。
- [ ] 旧的直接 Unity Input 点击与拖拽适配器被移除，且回归测试覆盖 Tap、Swipe、UI 归属和多指取消。
