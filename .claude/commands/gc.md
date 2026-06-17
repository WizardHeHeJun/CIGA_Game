---
description: Harness 健康度扫描——查引用失效、配置不一致、文档腐烂
---

# /gc —— Harness 健康度扫描

定期体检，防止 Harness 自身腐烂（规则引用的文件被删、CLAUDE.md 与实际目录不符等）。

## 扫描清单（按危害排序）
### 🔴 高（腐烂会误导 Agent）
1. **rules/commands/skills 里引用的路径是否存在**：grep 各 `.claude/**/*.md` 与 `CLAUDE.md` 中形如 `.claude/...`、`ai-docs/...`、`ai-shared/...`、`Assets/...` 的路径，逐一验证存在。
2. **CLAUDE.md 一致性**：其中提到的命令 / skill / 目录与实际 `.claude/commands`、`.claude/skills` 是否对得上。
3. **settings.json hook 脚本存在性**：注册的每个 `.claude/hooks/*.py` 文件是否存在、能否 `python -c "import ast; ast.parse(open(f).read())"` 通过语法。

### 🟡 中
4. **rules / skills / agents 的 frontmatter 合法**：必填字段齐全。
5. **ai-docs 模块文档引用的源码路径**是否仍存在。
6. **孤儿**：`ai-docs/modules/*` 里有文档但 `_catalog.md` 未登记。

### 🟢 低
7. PRP 残留：`ai-docs/PRPs/*` 已完成但未归档。

## 输出
分级报告 + 每条具体位置 + 修复建议。无问题项标 ✅。

## 何时跑
规则大改后、目录迁移后、长时间没跑后、或怀疑 Harness 行为异常时。
