# PRPs（Program Requirements Prompt 产物）

每个功能一个子目录 `ai-docs/PRPs/<feature>/`，由编排层命令生成：

```
<feature>/
├── refined-prd.md        # /refine-prd 产出：程序视角的需求拆解
├── requirements.md       # /generate-prp：目标、交互流程、验收条件(SC-N)
├── design.md             # /generate-prp：架构选型、接口、已知陷阱、ABC 验证清单
├── tasks.md              # /generate-prp：任务分组(N.M)、追溯 SC、文件引用
└── progress.json         # /execute-prp：执行进度（跨 session 恢复）
```

模板见 [`_templates/`](_templates/)。完整流程见 `.claude/commands/dev.md`。
