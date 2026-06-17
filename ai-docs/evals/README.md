# Evals — 给 AI 行为写的回归测试

用例放 `cases/*.json`。每条用例给 AI 一个**自然语言任务**（不是"测试是否违反 X 规则"——那样会被刻意规避），再用确定性检查校验产出。

## 用例结构

```json
{
  "id": "EVAL-001",
  "name": "MonoBehaviour 命名与序列化规范",
  "category": "regression",
  "task": "在 Assets/Scripts/Sample 下创建一个 Spinner 组件，让物体匀速旋转，转速可在 Inspector 配置",
  "deterministic_checks": [
    { "type": "grep", "pattern": "namespace Ciga", "files": ["**/Spinner.cs"], "expect": "present" },
    { "type": "grep", "pattern": "\\[SerializeField\\]\\s+private", "files": ["**/Spinner.cs"], "expect": "present" },
    { "type": "grep", "pattern": "public\\s+float\\s+\\w+\\s*;", "files": ["**/Spinner.cs"], "expect": "absent" },
    { "type": "compile", "expect": "no_errors" }
  ]
}
```

- `category`：`regression`（必须通过，≥95%）或 `capability`（追踪趋势）
- `compile` 检查经 Unity 验证桥（`.claude/skills/unity-bridge`）

运行：`/run-evals`；从 rules/pitfalls 自动派生缺失用例：`/sync-evals`。
