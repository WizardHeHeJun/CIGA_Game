---
description: 把需求/策划描述精炼成程序视角的 refined-prd（PRP 第 1 阶段）
argument-hint: <功能名或需求来源（文件/飞书链接/描述）>
---

# /refine-prd —— PRD 精炼

把面向人的需求（一句话 / 文档 / 飞书链接）翻译成**程序视角的功能拆解**，输出 `ai-docs/PRPs/<feature>/refined-prd.md`（≤200 行）。

输入：$ARGUMENTS

## 步骤
1. **确定 feature 名**（kebab/PascalCase 一致），建目录 `ai-docs/PRPs/<feature>/`。
2. **读全输入**：本地文件直接读；飞书链接用 lark 技能读；纯描述则基于描述。
3. **程序视角翻译**——把"用户看到三个按钮"这类话补成程序要处理的：
   - 状态机 / 交互流程（点击→请求→成功/失败各自表现）
   - 数据来源（配置表 / 运行时状态 / 存档）
   - 边界与异常（空数据、失败、并发、取消）
4. **盲区交互式补全**：发现需求没覆盖的点，用 AskUserQuestion **一次性**集中问用户，不要边写边碎问。
5. 输出 `refined-prd.md`，含：目标、功能拆解、交互流程、数据来源、验收标准草稿、完备度自评（轻量/标准/深度）。

## 产物模板
见 `ai-docs/PRPs/_templates/refined-prd.md`。

完成后提示：下一步 `/generate-prp <feature>`（或直接 `/dev` 自动续）。
