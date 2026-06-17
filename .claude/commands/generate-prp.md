---
description: 由 refined-prd 生成三文件 PRP（requirements/design/tasks）（PRP 第 2 阶段）
argument-hint: <feature>
---

# /generate-prp —— 生成三文件 PRP

把 `ai-docs/PRPs/<feature>/refined-prd.md` 拆成可执行的三文件 PRP。

feature：$ARGUMENTS

## 步骤
1. 读 `refined-prd.md`。无则提示先 `/refine-prd`。
2. **代码库研究**：找项目中类似实现（Grep/Glob 已有模块）、确认要复用的接口/基类、读相关 `ai-docs/modules/*` 文档。
3. **架构选型**：决定用 MonoBehaviour / ScriptableObject / 普通类，模块目录、命名空间 `Ciga.<模块>`、程序集归属。记成 ADR（决策+理由）。
4. **集中确认清单**：把所有待定问题（A 需求范围 / B 数据缺失 / C 技术方案 / D 外部依赖）汇总，用 AskUserQuestion **一次性**问。
5. **注入已知陷阱**：扫 `ai-shared/pitfalls.md`，把与本任务相关、且 lint 查不出的条目 verbatim 写进 design.md「已知陷阱」（lint 能查的不注入，避免稀释）。
6. 生成三文件（模板见 `ai-docs/PRPs/_templates/`）：
   - `requirements.md`：目标、交互流程、验收标准（编号 SC-1…SC-N）
   - `design.md`：文件规划、架构选型/ADR、接口表、已知陷阱、ABC 验证清单
   - `tasks.md`：任务分组（N.M 编号）、每个任务追溯到某条 SC、文件引用、参考实现
7. 任务 ≥6 或预估 ≥400 行时建议拆子 PRP。

完成后提示：`/validate-prp <feature>`。
