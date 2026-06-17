# 业务模块目录（Level 2）

每个业务模块在 `ai-docs/modules/<module>/` 下维护文档，标准结构见 [`_TEMPLATE/`](_TEMPLATE/)。

## 三件套约定

| 文档 | 命名 | 何时读 |
|------|------|--------|
| 模块入口 | `_knowledge-index.md` | 进模块前先读，决定深读哪份 |
| 模块指南 | `<module>-module-guide.md` | 改模块内部逻辑前 |
| 外部 API | `<module>-external-api.md` | 跨模块调用本模块时 |
| 扩展指南 | `<module>-extension-guide.md` | 给模块加新子类型 / 扩展点时 |

## 已有模块

_（暂无。新建模块时复制 `_TEMPLATE/` 并填充，然后在此登记。）_
