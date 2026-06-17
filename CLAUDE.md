# CIGA Client — Unity 2D 游戏工程

| 维度 | 选型 |
|------|------|
| 引擎 | Unity 2022.3.62f2 |
| 渲染 | URP 2D（Renderer2D） |
| 语言 | C#，根命名空间 `Ciga.*`，业务程序集 `Ciga.Game` |
| 文本 | TextMeshPro |
| 版本控制 | git |
| AI 框架 | Harness（五层架构，见 `ai-shared/docs/harness-overview.md`） |

## 全局禁止（硬底线，每次会话生效）
- 禁止改动 / 提交 `Library/` `Temp/` `Logs/` `obj/`（Unity 生成物）
- 禁止裸 `public` 可变字段暴露 Inspector —— 用 `[SerializeField] private`
- 业务代码必须有命名空间 `Ciga.*`，禁止全局类型
- 提交前移除所有 `[DEBUG]` 临时埋点与调试用 `Debug.Log`
- 不手改 `*.meta` 的 GUID、不手改 `Assets/Settings/*` URP 资产（除非任务明确要求）

## 知识检索路由（接触 Assets/Scripts/ 业务代码前）
1. 业务模块 → 先读 `ai-docs/_catalog.md` → 模块入口 `ai-docs/modules/<模块>/_knowledge-index.md`
2. 通用技术 / 框架 → 从 `ai-shared/docs/_catalog.md` 进入
3. 模块暂无文档 → 直接读源码，并考虑 `/generate-doc` 补建
4. 轻量改动（单文件、显而易见）可跳过
细则与各类文件规范见 `.claude/rules/`（按 glob 自动加载，无需手动查）。

## 工作流入口
- 开发新功能：`/dev`（串联 refine-prd → generate-prp → validate-prp → execute-prp）
- 运行时验证：`/auto-test`（经 Unity 验证桥）
- 调试 bug：`/bug-fix`（结构化 6 阶段，日志写文件）
- 沉淀经验：`/learn`；健康检查：`/gc`；回归用例：`/sync-evals` `/run-evals`

## 能力入口
- Unity 验证桥：`.claude/skills/unity-bridge`（编译 / 读 Console / PlayMode / 截图 / 跑测试，端口 17900）
- 代码生成：`.claude/skills/create-mono`、`create-so`、`author-signature`

## 踩坑记忆
跨会话踩坑沉淀在 `ai-shared/pitfalls.md`；提交前若发现同类问题，先查它。
