---
description: 校验三文件 PRP 的结构与一致性（PRP 第 3 阶段质量门禁）
argument-hint: <feature>
---

# /validate-prp —— PRP 校验

对 `ai-docs/PRPs/<feature>/` 的三文件做结构化检查，过不了不进 execute。

feature：$ARGUMENTS

## 检查项（🔴 错误必须修 / 🟡 警告建议修）
**requirements.md**
- 🔴 有「目标」「验收标准」章节；SC 用 `SC-N` 连续编号
**design.md**
- 🔴 有「文件规划」「架构选型/ADR」「ABC 验证清单」
- 🟡 「已知陷阱」存在（无相关陷阱可写"无"）
- 🔴 文件规划里的路径在 `Assets/Scripts/<模块>/` 下、命名空间 `Ciga.*`
**tasks.md**
- 🔴 任务用 `N.M` 连续编号
- 🔴 每个任务追溯到某条 `SC-N`（不允许任务不服务任何需求）
- 🟡 任务引用的文件在 design 的文件规划里出现
**跨文件**
- 🔴 每条 `SC-N` 至少被一个任务覆盖（无遗漏需求）
- 🟡 design 接口表与 tasks 引用一致

## 输出
逐项列 ✅/🔴/🟡 + 具体位置。结尾给「通过 / N 个错误待修」。有 🔴 时**不放行** execute，列出修复建议。
