---
title: 版本控制指南（git + Git LFS + Unity 合并）
type: guideline
layer: tech
generated_at: 2026-06-17
---

# 版本控制指南

本工程用 git，远程仓库 GitHub 私有库 **`CIGA_Game`**（`https://github.com/WizardHeHeJun/CIGA_Game`，默认分支 main）。

## 日常提交

```bash
git add -A
git commit -m "说明"
git push            # 已设 upstream，直接 push
```

**铁律：只用 `git push` 推送，绝不用 GitHub 网页「Upload files」拖拽整个文件夹**——网页上传无视 `.gitignore`，会把 2GB 的 `Library/` 缓存一起灌上去。

## 什么进库 / 什么不进

- 进库：`Assets/`、`Packages/`（含 `packages-lock.json`）、`ProjectSettings/`、`.claude/`、`ai-docs/`、`ai-shared/`、`CLAUDE.md`、`.gitignore`、`.gitattributes`。
- 不进库（`.gitignore` 已排除，Unity 自动重建）：`Library/`、`Temp/`、`Logs/`、`obj/`、`bin/`、`UserSettings/`、`__pycache__/`、`.claude/.state/` 等。
- 验证干净：`git ls-files | grep -i library` 应为空。

## Git LFS（二进制资产）

图片 / 音频 / 字体 / 视频等二进制走 LFS，避免仓库随美术迭代膨胀。规则在根目录 `.gitattributes`（png/jpg/psd/ase/wav/ogg/ttf/fbx/dll… 一律 `filter=lfs`）。

**前置（每台机器一次性）**：
```bash
winget install GitHub.GitLFS     # 或 https://git-lfs.com 下载安装
git lfs install                  # 把 LFS 过滤器挂到 git；装完需重开终端/VSCode 让 git-lfs 进 PATH
```
> ⚠️ 没装 git-lfs 就 `git add` 一个 png 会报 "git-lfs not found / smudge filter lfs failed"。先装。

装好后正常 `git add 图片` 即自动入 LFS。确认：
```bash
git lfs ls-files          # 列出已被 LFS 管理的文件
git lfs status
```

**配额**：GitHub 免费档 LFS = 1GB 存储 + 1GB/月流量。小型 2D 项目够用；美术量大时留意。

## Unity 资产智能合并（多人协作）

`.unity` / `.prefab` / `.asset` 等是 YAML，多人改同一文件易冲突。`.gitattributes` 标了 `merge=unityyamlmerge`，配上 Unity 自带的 UnityYAMLMerge 工具可智能合并。

**合并驱动是本地配置（不随仓库推送），每个 clone 各配一次**：
```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"<Unity安装路径>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "%O" "%B" "%A" "%A"'
git config merge.unityyamlmerge.recursive binary
```
本机 Unity 路径示例：`C:/Program Files/Unity/Hub/Editor/2022.3.62f2/`。

> 即使没配这个驱动，`merge=unityyamlmerge` 也会安全回退到默认合并，不报错；配了只是合并更聪明。

## 新人 clone 后的初始化清单

```bash
git lfs install                                  # 1. 装并初始化 LFS（先 winget 装 git-lfs）
# 2. 按上面配 unityyamlmerge 合并驱动
# 3. 用 Unity 2022.3.62f2 打开工程，Library 会自动重建
```

## 排障

| 现象 | 原因 / 处理 |
|------|------------|
| `git add` 图片报 `git-lfs not found` | 没装或没重开终端 → 装 git-lfs + `git lfs install` + 重开终端 |
| GitHub 上看到 `Library/` 等缓存 | 多半是用网页拖拽传的；改用 `git push`，已传的需 `git rm -r --cached Library && commit && push` |
| VSCode「更改」显示上万未跟踪文件 | 面板缓存陈旧 → Source Control 刷新🔄 或 Reload Window；以 `git status` 为准 |
| 提交时 `LF will be replaced by CRLF` 警告 | 无害（autocrlf 行尾转换提示）；仓库内统一存 LF |
