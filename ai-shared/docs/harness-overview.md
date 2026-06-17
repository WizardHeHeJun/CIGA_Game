---
title: Harness 五层架构总览（CIGA Client 适配版）
type: model
layer: tech
generated_at: 2026-06-17
---

# 本工程的 AI 开发框架（Harness）

把团队《AI 辅助开发框架完整指南》的**五层正交架构**思路，适配到本工程（Unity 2022.3 / URP 2D / 纯 C# / git）。核心命题：在有限上下文窗口内，让 AI 每个时刻刚好看到需要的信息、调得动需要的工具、拿得到需要的反馈。

## 五层

| 层 | 回答 | 本工程的落地 |
|----|------|-------------|
| **能力层** | Agent 能做什么 | Unity 验证桥 `.claude/skills/unity-bridge`（端口 17900：编译/Console/PlayMode/截图/测试）、代码生成器（create-mono / create-so / author-signature）、git |
| **知识层** | Agent 知道什么 | `CLAUDE.md`（全局，<60 行）→ `.claude/rules/`（按 glob 加载）→ `ai-docs/modules/<模块>/` 三件套；渐进式加载，按需读 |
| **策略层** | 必须/禁止做什么 | `CLAUDE.md` 禁令 + `.claude/rules/` + `.claude/hooks/`（csharp-lint / doom-loop / doc-first-gate / precompact / stop-check）+ code-reviewer SubAgent。**机制大于自觉** |
| **编排层** | 怎么组织复杂任务 | PRP 全链路 `/dev`（refine-prd→generate-prp→validate-prp→execute-prp）、`/auto-test`、`/bug-fix`。计划与执行分离 |
| **进化层** | Harness 自我改进 | `/learn`（沉淀经验到 rules/pitfalls/memory/lint）、`/gc`（健康度）、`/sync-evals` `/run-evals`（回归） |

## 设计原则（沿用团队踩坑教训）
1. 规则是约束不是文档 —— 精简（rules <150 行、CLAUDE.md <60 行），太详细会稀释注意力。
2. 机制大于自觉 —— 规则自动加载、hook 自动检查；hook **成功静默、失败冗余**。
3. 计划与执行分离 —— PRP 产物持久化、可审查、可复用。
4. 经验闭环 —— 每次犯错转化为 pitfall + lint 规则 + Eval 用例。

## 目录地图
- `.claude/` — rules / hooks / commands / skills / agents / settings.json
- `ai-docs/` — 项目业务知识（modules 三件套、PRPs、evals）
- `ai-shared/` — 框架自身 / 通用文档、pitfalls.md、evolution 归档（跨项目可移植）

## 与团队大项目的差异
团队 Harness 跑在 Unity+Lua(ToLua)+P4 的大型项目上（50 技能/18 规则/15 hook，长期进化产物）。本工程是纯 C# 空工程起步，只搭五层的**可用骨架**，按需长大。未做：`/steal` 外部情报扫描、Wave 多 worktree 并行、完整三级 Eval。
