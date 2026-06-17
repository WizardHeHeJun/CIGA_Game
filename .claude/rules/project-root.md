---
description: 项目根规则、目录结构、全局约定（始终生效）
globs: []
alwaysApply: true
---

# 项目根规则

## 目录结构
```
Assets/
  Scripts/         # 业务 C# 代码（程序集 Ciga.Game，命名空间 Ciga.*）
  Editor/          # 编辑器扩展（含 AiBridge 验证桥）
  Scenes/ Settings/ # 场景与 URP 设置（不随意改）
ai-docs/           # 项目业务知识（modules 三件套 / PRPs / evals）
ai-shared/         # 框架自身 / 通用知识 / pitfalls / evolution
.claude/           # Harness：rules / hooks / commands / skills / agents
```

## 全局约定
- 业务脚本一律放 `Assets/Scripts/<模块>/`，命名空间 `Ciga.<模块>`。
- 编辑器专用代码放 `Assets/Editor/` 或带 Editor asmdef 的目录，禁止把 `UnityEditor` 引用进运行时程序集。
- 新增资源文件（.cs/.asmdef/文件夹）若 Unity 未运行，需补 `.meta`（含唯一 GUID）。
- 临时调试埋点统一打 `[DEBUG]` 标记，便于收尾批量清除。
- 改动较大或跨模块时走 PRP 工作流（`/dev`），不要直接长篇自由发挥。
