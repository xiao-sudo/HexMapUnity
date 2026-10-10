# 05 — 完成 EasyTouch 接线说明与回归验证

**What to build:** 场景集成人员获得可执行的 EasyTouch 手动接线说明，并可依据回归验证确认 Tap、Swipe、Pinch、滚轮和 Map 模式切换只走新的正式输入路径。

**Blocked by:** 01 — 扩展 Map 镜头距离路由契约; 02 — 让模式 Handler 控制正交相机; 03 — 以 EasyTouch 统一 Tap 与单指 Swipe; 04 — 接入 EasyTouch Pinch 与鼠标滚轮缩放.

**Status:** ready-for-agent

- [ ] 文档说明 EasyTouch 的 Second Finger Simulation、20 像素静止容差、初始 UI 归属、活动 Handler 注册以及旧组件移除步骤。
- [ ] 文档明确 UI 归属在起点确定、不得逐帧重判，并说明构建场景由人工完成接线而不由本任务直接修改。
- [ ] 回归验证覆盖 Gameplay 与 TopDown、Map Cell 点击、单指平移、Pinch、滚轮、UI 优先、模式切换取消和相机边界。
