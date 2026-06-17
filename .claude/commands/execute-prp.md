---
description: 按三文件 PRP 执行实现 + 自审查 + ABC 验证（PRP 第 4 阶段）
argument-hint: <feature>
---

# /execute-prp —— 执行 PRP

严格按 `ai-docs/PRPs/<feature>/` 的 tasks.md 实现，拒绝跑题自由发挥。

feature：$ARGUMENTS

## 前置
先确认 `/validate-prp` 已过（无 🔴）。未过则先修。

## 上下文管理（控制膨胀）
- 常驻：requirements.md 摘要 + 进度。
- 按需：当前任务相关的 design 片段、参考实现。
- 已完成任务只留 diff 摘要。

## 执行循环（逐任务）
1. 读任务 N.M 及其追溯的 SC、design 对应接口与陷阱。
2. 实现（按规范；优先用 `/create-mono` `/create-so` 生成骨架）。
3. 进度写 `ai-docs/PRPs/<feature>/progress.json`（任务、状态、遇到的偏差与解决）。
4. 任务组完成做一次 git commit（检查点，支持断点恢复）。
5. 遇到与 PRP 设计冲突（如接口签名占用）：自主用最小改动解决并在 progress 记录原因，不静默偏离。

## 全部完成后 —— ABC 验证
- **A 静态**：`python .claude/skills/unity-bridge/scripts/bridge.py compile`（0 error；lint hook 已在每次编辑后兜底）
- **B 运行**：必要时 `bridge.py play` + `console --level error` 看运行时报错；有测试则 `bridge.py test`
- **C 架构**：派发 code-reviewer SubAgent 审查本次改动
  - `Agent(subagent_type="code-reviewer", prompt="审查 feature <X> 本次改动，对照 requirements 的 SC")`

## 自审查
对照 requirements.md 的每条 `SC-N` 逐项核对是否实现，输出自检表。遗漏的当场补。
