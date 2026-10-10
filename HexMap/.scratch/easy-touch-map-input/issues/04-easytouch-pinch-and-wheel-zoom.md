# 04 — 接入 EasyTouch Pinch 与鼠标滚轮缩放

**What to build:** 玩家可以以双指 Pinch 围绕开始中点拉近或拉远正交 Map 相机；桌面开发者可以以鼠标滚轮围绕指针缩放，两个入口均通过相同的 Map 输入路由生效。

**Blocked by:** 01 — 扩展 Map 镜头距离路由契约; 02 — 让模式 Handler 控制正交相机; 03 — 以 EasyTouch 统一 Tap 与单指 Swipe.

**Status:** ready-for-agent

- [ ] 第二指开始时锁定 Pinch 起始中点并投递零增量标记；后续归一化 Pinch 增量保持同一锚点并经过可配置死区过滤。
- [ ] 任一 Pinch 起始触点位于 UI 时整次 Pinch 被拒绝；Pinch 开始会结束单指手势，且手势取消或模式切换不会留下残余状态。
- [ ] 鼠标滚轮在非 UI 区域以指针为锚点，经相同回调缩放，并保留每格 1.15 倍的手感。
- [ ] 旧的直接 Unity Input 缩放适配器被移除；测试覆盖固定锚点、符号方向、UI 拒绝、边界消费及滚轮路由。
