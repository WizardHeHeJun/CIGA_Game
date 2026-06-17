---
description: 程序集定义（asmdef）划分约定
globs: ["Assets/**/*.asmdef", "Assets/**/*.cs"]
alwaysApply: false
---

# 程序集（asmdef）约定

- 运行时业务代码统一在 `Ciga.Game`（`Assets/Scripts/Ciga.Game.asmdef`），命名空间 `Ciga.*`
- **编辑器代码不进运行时程序集**：放 `Assets/Editor/` 或带 `includePlatforms: [Editor]` 的独立 asmdef，否则打包报错
- 测试代码用独立 asmdef（引用 `UnityEngine.TestRunner` / `UnityEditor.TestRunner` + 被测程序集），命名 `Ciga.<X>.Tests`
- 新加 asmdef 时：在其 `references` 里显式列依赖；不要为图省事开 `autoReferenced` 滥引
- 拆分程序集的目的是隔离编译、加快增量编译；模块稳定且较大时才拆，初期都放 `Ciga.Game`
