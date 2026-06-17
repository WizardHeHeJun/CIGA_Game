---
description: 运行 Eval 回归用例——派发 Agent 做任务，再用确定性检查 + 编译校验产出
argument-hint: [可选：用例 id 或 category]
---

# /run-evals —— 运行 Eval

验证规则体系是否真的有效（让 AI 在自然任务里不踩规范）。

范围：$ARGUMENTS（为空跑全部 regression）

## 步骤
1. 读 `ai-docs/evals/cases/*.json`，按范围筛选。
2. 对每条用例：
   - 在**隔离**子 Agent 里执行 `task`（自然语言任务），让它生成代码到一个临时/沙箱位置（如 `Assets/Scripts/_evalsbox/<id>/`）。
     - `Agent(subagent_type="general-purpose", prompt="<task>。遵守项目规范。")`
   - 用检查器校验：`python ai-docs/evals/check.py ai-docs/evals/cases/<id>.json`
     - grep present/absent 检查 + （可选）经 unity-bridge 编译校验 `compile` 维度。
3. 汇总通过率。regression 类目标 ≥95%。
4. 清理 `_evalsbox/`。

## 何时跑
- 改了 rules / lint 规则 / pitfalls 后（回归）。
- 定期看 capability 类趋势。
