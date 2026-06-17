---
name: create-mono
description: 生成符合本项目规范的 MonoBehaviour 脚本骨架（命名空间 Ciga.*、SerializeField 私有字段、生命周期、自动署名）。新建组件/控制器时用，避免每次手敲样板还踩规范坑。
---

# create-mono

按本项目 C#/Unity 规范生成 MonoBehaviour 骨架。

## 入参
- 类名（PascalCase，如 `PlayerController`）
- 目标模块/目录（如 `Player` → `Assets/Scripts/Player/`）
- 一句话职责

## 步骤
1. 路径：`Assets/Scripts/<模块>/<类名>.cs`，命名空间 `Ciga.<模块>`。
2. 先按 [`../author-signature/SKILL.md`](../author-signature/SKILL.md) 取作者/日期。
3. 按下方模板生成，删掉用不到的生命周期方法（**不留空方法**）。
4. 让 Unity 编译验证：`python .claude/skills/unity-bridge/scripts/bridge.py compile`。
5. 若该 GameObject 需要 Inspector 配置，提醒用户在场景/预制体上挂载并连引用。

## 模板
```csharp
// ------------------------------------------------------------
// <类名>.cs
// Author : <作者名>
// Created: <YYYY-MM-DD>
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.<模块>
{
    /// <summary><一句话职责></summary>
    public class <类名> : MonoBehaviour
    {
        [SerializeField] private float _example = 1f;

        private void Awake()
        {
            // 缓存自身组件引用、初始化自身状态
        }

        private void OnEnable()
        {
            // 订阅事件（与 OnDisable 成对）
        }

        private void OnDisable()
        {
            // 反订阅事件
        }
    }
}
```

## 规范要点（由 csharp-lint / rules 兜底）
- 私有字段 `_camelCase` + `[SerializeField]`，不要 public 可变字段。
- 热路径（Update 等）不 GetComponent / Find / new 集合。
- 事件成对注册/反注册。
