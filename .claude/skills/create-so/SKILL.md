---
name: create-so
description: 生成 ScriptableObject 配置类骨架（CreateAssetMenu、SerializeField 私有字段 + 只读属性访问、命名空间 Ciga.*、自动署名）。做数据配置/共享资产时用。
---

# create-so

按规范生成 ScriptableObject 配置类。

## 入参
- 类名（如 `EnemyConfig`）
- 模块/目录
- 字段清单（名 + 类型 + 含义）

## 步骤
1. 路径：`Assets/Scripts/<模块>/<类名>.cs`，命名空间 `Ciga.<模块>`。
2. 署名见 [`../author-signature/SKILL.md`](../author-signature/SKILL.md)。
3. 字段用 `[SerializeField] private` + 公开**只读**属性访问（SO 是共享资产，不暴露可写字段）。
4. 编译验证：`bridge.py compile`。
5. 提醒用户在 `Assets/.../` 右键 `Create/<menuName>` 生成资产实例。

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
    [CreateAssetMenu(fileName = "<类名>", menuName = "Ciga/<模块>/<类名>")]
    public class <类名> : ScriptableObject
    {
        [SerializeField] private int _hp = 100;
        [SerializeField] private float _moveSpeed = 3f;

        public int Hp => _hp;
        public float MoveSpeed => _moveSpeed;
    }
}
```

## 规范要点
- **不要**在 SO 里存运行时易变状态（资产被多处共享）；运行时状态放 MonoBehaviour/普通类。
- 只读属性暴露，禁止 public 可写字段。
