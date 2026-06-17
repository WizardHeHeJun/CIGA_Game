---
description: PRP 全链路统一入口——自动判断阶段并串联 refine→generate→validate→execute
argument-hint: <功能名或需求来源>
---

# /dev —— 开发统一入口

一条命令跑完整 PRP 流程，自动检测当前到哪一步。

输入：$ARGUMENTS

## 阶段自动检测（按 `ai-docs/PRPs/<feature>/` 现状）
1. 无目录 / 无 `refined-prd.md` → 执行 `/refine-prd` 阶段
2. 有 `refined-prd.md`、无三文件 → 执行 `/generate-prp` 阶段
3. 有三文件、未校验或有 🔴 → 执行 `/validate-prp` 阶段
4. 校验通过 → 执行 `/execute-prp` 阶段

每个阶段之间**停下来让用户确认**关键产物（refined-prd 一次、PRP 三文件一次），再续下一阶段——计划与执行分离，不要一口气闷到底。

## 用法
- `/dev <新需求>`：从头开始。
- `/dev <已有 feature>`：从当前阶段续。

## 何时不用 /dev
- 单文件小改 / 明确 bug → 直接对话或 `/bug-fix`。
- 中等改动、方向明确 → 直接做或用内置 plan 模式。
- PRP 适合：新功能、多文件、跨模块、需要架构决策的改动。
