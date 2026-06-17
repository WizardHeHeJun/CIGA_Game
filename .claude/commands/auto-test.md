---
description: 经 Unity 验证桥跑运行时验证（run/verify）
argument-hint: [run|verify] <场景或功能描述>
---

# /auto-test —— 运行时验证闭环

用 unity-bridge 在运行的 Unity 里验证，不必每次让人手动跑。

输入：$ARGUMENTS

## 前置
`python .claude/skills/unity-bridge/scripts/bridge.py health` 确认桥连通（Unity 需打开本工程）。

## 模式
- **run（清障）**：目标是"能跑起来不报错"。
  1. `bridge.py compile` → 有 error 先修到 0。
  2. `bridge.py play` 进 PlayMode。
  3. `bridge.py console --level error` 看运行时报错。
  4. 有报错 → 走 `/bug-fix` 定位修复 → `bridge.py stop` 后重来，直到干净。
- **verify（功能验证）**：给定场景/功能描述，检查效果。
  1. 进对应场景、必要时 `play`。
  2. `bridge.py screenshot` 截图，AI 看图判断 UI/表现是否符合描述。
  3. 有 EditMode/PlayMode 测试则 `bridge.py test --mode edit|play`。
  4. `bridge.py hierarchy` 核对场景结构。

## 收尾
退出 PlayMode（`bridge.py stop`）。把验证中发现的稳定结论/避坑写法考虑 `/learn` 沉淀。

## 桥不可用时
提示用户打开 Unity 并启动 AiBridge（Window/AI Bridge），或用 `scripts/compile_batch.py` 做语法级粗检。
