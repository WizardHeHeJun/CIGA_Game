---
name: author-signature
description: 新建 C# 文件时在头部加作者署名块（作者 + 创建日期）。create-mono / create-so 等生成器会调用；手动新建文件时也应补。
---

# author-signature

给新建的 C# 文件加统一署名块，便于日后追责（git blame 之外的快速标注）。

## 署名块格式
放在文件最顶部（using 之前）：

```csharp
// ------------------------------------------------------------
// <文件名>.cs
// Author : <作者名>
// Created: <YYYY-MM-DD>
// ------------------------------------------------------------
```

## 取值
- 作者名：取 `git config user.name`（拿不到则用系统用户名）。
  - `git -C e:/CIGA/Client config user.name`
- 日期：今天（`YYYY-MM-DD`）。

## 规则
- 只在**新建**文件时加；修改既有文件不动署名。
- 不要在署名块里写易过期信息（如"用途：临时"）；用途写在类的 XML doc 注释里。
