---
name: unity-bridge
description: 通过本地 HTTP 桥驱动 Unity Editor 做验证闭环——编译并读编译错误、读 Console 日志、进出 PlayMode、截图、跑 EditMode/PlayMode 测试、查场景层级。当需要确认 C# 改动能否编译、是否运行报错、功能是否跑通时使用。需 Unity 打开本工程。
---

# unity-bridge —— AI 验证桥（stagehand-lite）

让 AI 在**运行中的 Unity**里闭环验证：写完代码自己编译、读报错、跑起来看效果，不必每次让人去 Editor 手动跑。

## 架构
- Editor 侧：`Assets/Editor/AiBridge/`（`AiBridgeServer.cs` 内嵌 HttpListener，端口 **17900**；`AiBridgeWindow.cs` 控制面板；`AiBridgeTests.cs` 跑测试）。`[InitializeOnLoad]` 自动启动。
- AI 侧：`scripts/bridge.py`（纯标准库 CLI 客户端）。

## 前置
1. Unity 打开本工程（`e:\CIGA\Client`）。
2. AiBridge 已启动：菜单 `Window/AI Bridge` 看状态，默认随 Editor 自动启动。
3. 先 `python .claude/skills/unity-bridge/scripts/bridge.py health` 确认连通。

## 命令
| 命令 | 作用 |
|------|------|
| `bridge.py health` | 探活：Unity 版本、是否编译中/播放中 |
| `bridge.py compile` | 触发刷新+编译，等结果，打印 error/warning（有 error 退出码 1） |
| `bridge.py console [--level error\|warn\|all] [--limit N]` | 读 Console 缓冲日志 |
| `bridge.py play` / `stop` | 进/出 PlayMode |
| `bridge.py screenshot [--path X.png]` | 截 GameView（PlayMode 下有效） |
| `bridge.py test [--mode edit\|play]` | 跑 TestRunner 测试，打印通过/失败 |
| `bridge.py hierarchy` | 当前场景层级树 |
| `bridge.py status` | 编译/播放状态 + 最近编译结果 |

调用：`python .claude/skills/unity-bridge/scripts/bridge.py <命令>`。

## 典型用法
- 改完 C# → `compile` 确认 0 error → 必要时 `play` + `console --level error` 看运行时报错 → `stop`。
- 写了测试 → `test --mode edit`。
- auto-test / execute-prp 的运行时验证(B) 都经本桥。

## 注意
- `compile` / `play` 会触发**域重载**，HTTP 连接可能短暂中断；CLI 已内置容错轮询，正常。
- Unity 没开或桥没启动时，CLI 报连接失败。此时回退：`scripts/compile_batch.py`（用 dotnet 对已生成的 .sln 做语法级编译，仅供 Unity 不可用时粗检），或请用户在 Editor 手动 Refresh。
- 端口 17900 固定，避开团队 UnitySkills 的 8090/18091。
