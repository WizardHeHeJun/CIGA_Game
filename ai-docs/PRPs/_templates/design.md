# <Feature> Design

## 文件规划
| 文件 | 类型 | 职责 |
|------|------|------|
| Assets/Scripts/<模块>/<X>.cs | MonoBehaviour / SO / 普通类 | |

## 架构选型（ADR）
- **决策**：<选了什么>
- **理由**：<为什么；考虑过的替代>
- 命名空间 `Ciga.<模块>`，程序集 `Ciga.Game`

## 接口
| 类型 | 公开成员 | 签名 | 说明 |
|------|----------|------|------|
| | | | |

## 已知陷阱
> 由 generate-prp 从 ai-shared/pitfalls.md 注入「相关且 lint 查不出」的条目；lint 能查的不写这里。

- <陷阱 / 无>

## ABC 验证清单
- A 静态：编译 0 error；lint 0 violation
- B 运行：<进哪个场景、做什么操作、期望表现 / 跑哪些测试>
- C 架构：code-reviewer 无 BLOCK
