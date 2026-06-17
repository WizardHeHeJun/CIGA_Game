---
description: C# 编码规范（命名、序列化、GC）
globs: ["Assets/Scripts/**/*.cs", "Assets/Editor/**/*.cs"]
alwaysApply: false
---

# C# 编码规范

## 命名
- 类型 / 方法 / 属性 / 公开常量：`PascalCase`
- 局部变量 / 参数：`camelCase`
- 私有字段：`_camelCase`（下划线前缀）
- 接口：`I` 前缀（`IDamageable`）；枚举值 `PascalCase`
- 命名空间：`Ciga.<模块>`，每个文件都要有 namespace

## 字段与序列化
- **禁止** 用 `public` 可变字段暴露给 Inspector；用 `[SerializeField] private`
- 只读依赖用 `private` + 构造/Awake 注入；常量用 `const` / `static readonly`
- 公开访问用属性（`public float Speed { get; private set; }`）

## 风格
- `using` System 优先、按字母排序（见 `.editorconfig`）
- 优先 `var`（当类型显然时）；内建类型显式写（`int i`）
- 控制流必加大括号
- `async void` 仅用于事件处理器；其余用 `async Task`

## GC / 性能
- 不在 `Update/FixedUpdate/LateUpdate` 等热路径里 `new` 集合 / 闭包 / 字符串拼接
- 不在热路径里 `GetComponent` / `GameObject.Find` —— 在 `Awake`/`Start` 缓存
- 频繁创建销毁的对象走对象池
