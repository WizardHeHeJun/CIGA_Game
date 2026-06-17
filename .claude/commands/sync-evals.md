---
description: 从 rules / pitfalls 的「必须/禁止」约束自动派生缺失的 Eval 用例
---

# /sync-evals —— 同步 Eval 用例

扫描规则与陷阱里的硬约束，为还没有回归用例覆盖的，生成 Eval。

## 步骤
1. 读 `.claude/rules/*.md`、`.claude/hooks/csharp_lint/rules.json`、`ai-shared/pitfalls.md`，提取「必须/禁止」类可检测约束。
2. 读 `ai-docs/evals/cases/*.json` 看已有覆盖。
3. 对未覆盖的约束，生成新用例（写到 `ai-docs/evals/cases/EVAL-NNN-*.json`）：
   - `task` 写成**自然语言开发任务**（如"创建一个让物体旋转的组件"），**不要**写成"测试是否违反 X"——否则 AI 会刻意规避，测出来虚高。
   - `deterministic_checks` 用 grep present/absent + compile 校验那条约束。
   - `category`: `regression`（硬约束）。
4. 输出新增了哪些用例。

用例结构见 `ai-docs/evals/README.md`。生成后用 `/run-evals` 验证。
