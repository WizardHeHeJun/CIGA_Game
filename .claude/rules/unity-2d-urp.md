---
description: URP 2D 渲染、Sprite、UI、文本规范
globs: ["Assets/Scripts/**/*.cs"]
alwaysApply: false
---

# URP 2D 规范

## 渲染管线
- 本工程用 **URP 2D（Renderer2D）**，不要引入内置管线或 3D URP 专用 API（如 3D 光照/阴影组件）
- 改渲染相关代码前确认走的是 `Assets/Settings/Renderer2D.asset`

## Sprite / 排序
- 排序用 **Sorting Layer + Order in Layer**；同层内靠 Order，不要靠 Z 轴 hack
- 2D 光照用 URP 2D 的 `Light2D`（需要时），不要用 3D `Light`
- 大量同图 Sprite 用 SpriteAtlas 合批，减少 draw call

## UI
- UI 用 uGUI（Canvas）；文本一律用 **TextMeshPro**（`TMP_Text` / `TextMeshProUGUI`），不用旧版 `UnityEngine.UI.Text`
- 交互需要射线时确认 `RaycastTarget`；不需要的图形关掉以省开销

## 物理
- 2D 用 `Rigidbody2D` / `Collider2D` / `Physics2D`，不要混用 3D 物理组件
