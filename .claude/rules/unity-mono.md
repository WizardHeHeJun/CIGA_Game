---
description: MonoBehaviour / ScriptableObject 生命周期与事件规范
globs: ["Assets/Scripts/**/*.cs"]
alwaysApply: false
---

# Unity MonoBehaviour 规范

## 生命周期
- `Awake`：自我初始化、缓存自身组件引用
- `Start`：依赖其他对象的初始化（此时别人也 Awake 过了）
- `OnEnable`/`OnDisable`：订阅/反订阅事件、注册/注销
- `OnDestroy`：释放资源、取消订阅、停止协程
- **不要留空的生命周期方法**（空 `Update(){}` 也有调用开销）—— 删掉

## 事件与回调
- 事件**成对**注册/反注册：`OnEnable` 订阅，`OnDisable`/`OnDestroy` 反订阅，否则泄漏
- 协程：持有引用以便 `StopCoroutine`；对象销毁时协程自动停，但跨对象的要手动停
- 计时用 `Time.deltaTime` 累加或协程，不要靠帧数

## ScriptableObject
- 用于配置数据 / 共享资产，不要在其中存运行时易变状态（资产是共享的）
- 运行时状态放 MonoBehaviour 或普通类实例

## 引用获取
- 序列化引用优先（`[SerializeField]` 在 Inspector 拖）；其次 `Awake` 里 `GetComponent` 缓存
- 避免 `GameObject.Find` / `FindObjectOfType`（慢、脆）；必要时只在初始化期用一次
