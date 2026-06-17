---
description: 结构化 6 阶段调试闭环（假设→埋点→复现→分析→修复→清理）
argument-hint: <bug 现象描述>
---

# /bug-fix —— 结构化调试

不要瞎猜乱改。按 6 阶段闭环定位根因，根因优先于打补丁。

bug：$ARGUMENTS

## 6 阶段
1. **假设**：基于现象列出 1~3 个可能根因，先复述 bug 确认理解一致。
2. **埋点**：在关键路径加日志，**写文件**优于只打 Console（PlayMode 跑完 Console 会丢；文件可反复读）。埋点统一加 `[DEBUG]` 前缀便于清理。
   - 经桥读运行时日志：`python .claude/skills/unity-bridge/scripts/bridge.py console --level all`
3. **复现**：真实触发（`bridge.py play`，必要时进对应场景操作）。
4. **分析**：读日志/报错验证假设。`bridge.py console --level error` 看异常堆栈。
5. **修复**：定位根因后应用**最小**改动。避免加一堆兼容补丁掩盖问题。
6. **清理**：删除所有 `[DEBUG]` 埋点（Stop hook 会在收尾提醒）。`bridge.py compile` 确认仍 0 error。

## 沉淀
若是会复发的坑（尤其 lint 查不出的），`/learn` 写进 `ai-shared/pitfalls.md`；命名/API 类可加进 `csharp_lint/rules.json`。

> 注：`/bug-fix` 是本项目结构化流程；Claude Code 内置的 `/debug` 是会话级调试入口，二者不同。
