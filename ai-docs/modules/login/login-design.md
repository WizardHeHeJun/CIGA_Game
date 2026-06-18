# 登录界面 — 需求与设计梳理

> 状态：草案（需求收集阶段，待决策点确认后转入 PRP / `/dev`）
> 适用工程：CIGA Client（Unity 2022.3.62f2 · URP 2D · TextMeshPro）
> 最后更新：2026-06-17

---

## 1. 背景与现状

当前工程是**从零起步**搭第一个界面，关键事实（已核对）：

| 项 | 现状 | 影响 |
|----|------|------|
| UI 框架 | **无**（无 asmdef、无 dll、无 UI/Framework/Plugins 目录、全分支全历史无 UI 提交） | 登录界面要么直接用内置 uGUI 裸搭，要么先搭一层轻量 UI 基座 |
| UI 技术栈 | Unity 内置 **uGUI**（Canvas/Button/InputField）+ **TextMeshPro**（已装） | 文本统一用 `TMP_Text`/`TMP_InputField`，禁用旧版 `UnityEngine.UI.Text` |
| 输入系统 | **com.unity.inputsystem 1.7.0** 已装 | Tab 切焦点、回车提交可走新输入系统 |
| 场景 | 只有 `Assets/Res/Scene/GameMain.unity`（唯一构建场景，基本空白） | 登录放 GameMain 内 / 独立 Login 场景，需决策 |
| 命名空间 / 程序集 | 规范要求 `Ciga.*`；`Ciga.Game.asmdef` **尚未实际创建** | 新建 UI 脚本需带命名空间；是否拆 asmdef 待定 |
| 后端 | 无任何网络层 / 账号服务 | MVP 先做前端骨架，登录动作触发事件/打日志，不接网络 |

---

## 2. 目标与范围

**目标**：玩家进入游戏时看到一个登录界面，能输入凭据、点击登录、得到反馈（成功进入 / 失败提示）。

**MVP 范围（先做）**：
- 一个可视的登录界面（输入框 + 登录按钮 + 提示文本）
- 前端交互骨架：空值校验、按钮可用态、错误提示
- 登录动作触发一个事件 / 回调（暂不接后端），成功后切到主场景/主界面

**暂不做（后续）**：真实账号校验、网络请求、Token 持久化、第三方 SDK、记住密码、自动登录、注册/找回密码流程。

---

## 3. 需要哪些东西（清单）

### 3.1 UI 结构（uGUI 层级）
- `Canvas`（Screen Space - Overlay 或 Camera）+ `CanvasScaler`（按分辨率自适应，建议 Scale With Screen Size + 参考分辨率）+ `GraphicRaycaster`
- `EventSystem`（含输入模块；用新输入系统则挂 `InputSystemUIInputModule`）
- 登录面板根节点 `LoginPanel`
  - 背景 `Image`（全屏底图 / 纯色）
  - 标题 `TMP_Text`（游戏名 / "登录"）
  - 账号输入框 `TMP_InputField`（占位提示文本）
  - 密码输入框 `TMP_InputField`（Content Type = Password）
  - 登录按钮 `Button` + 子 `TMP_Text`
  - 错误/状态提示 `TMP_Text`（默认隐藏）
  - （可选）第三方/访客登录按钮区

### 3.2 美术资源（需提供 / 占位）
- TMP 中文字体资源（**关键**：中文需生成 TMP Font Asset + 字符集，否则中文显示方块）
- 背景图、按钮九宫格图、输入框底图、图标（可先用纯色占位）
- 若用 SpriteAtlas，注意合批

### 3.3 代码结构（建议）
- 命名空间：`Ciga.UI.Login`（或并入 `Ciga.Game`）
- `LoginPanel`（MonoBehaviour）：持有 UI 引用（`[SerializeField] private`），处理交互、校验、触发事件
- `LoginController` / `LoginService`（普通类）：登录逻辑入口，MVP 阶段返回模拟结果，后续替换为真实请求
- 事件：`event Action<LoginResult>`，面板订阅 → 更新 UI；外部订阅 → 切场景
- 数据：`LoginResult`（成功/失败 + 错误码/消息）的简单结构
- 规范：禁裸 public 字段、控制流加大括号、提交前清 `[DEBUG]`/调试 `Debug.Log`

### 3.4 输入与交互细节
- Tab / 方向键在输入框间切焦点；回车提交
- 提交时禁用登录按钮防重复点击；完成后恢复
- 空输入 / 格式错误 → 即时提示，不发请求

### 3.5 场景集成与流程
- 决策：登录是**独立 Login 场景**（启动 → Login → 加载 GameMain）还是 **GameMain 内的一个面板**
- 启动流程：构建首场景 → 显示登录 → 登录成功 → 进入主流程
- 注意：之前删了 SampleScene，构建列表仅 `GameMain`；若加独立 Login 场景需补进 `EditorBuildSettings`

### 3.6 本地化（可选）
- 文案集中管理，便于多语言；TMP 字体需覆盖目标语言字符集

---

## 4. 决策点（需你 / 策划拍板）

> 这些直接改变工作量与结构，定了才好进入实现。

1. **登录方式**（可多选）：① 账号+密码 ② 手机号+验证码 ③ 第三方（微信/Apple/Google，先占位） ④ 访客/快速进入
2. **实现/交付方式**：① 代码动态生成 UI（可立即 AiBridge 跑通验证，无需手动拖拽） ② 我出脚本+你在编辑器拖搭 ③ 走完整 `/dev` PRP 流程
3. **要不要先搭轻量 UI 框架**：① 登录裸搭，先快 ② 先搭最小 UI 基座（面板基类 + 显隐/层级管理），登录作为第一个面板接入
4. **场景形态**：独立 Login 场景 / GameMain 内面板
5. **是否接后端**：MVP 纯前端骨架 / 一开始就对接登录接口（需接口约定）
6. **命名空间/程序集**：并入 `Ciga.Game` / 新建 `Ciga.UI`（及是否拆独立 asmdef）

---

## 5. 任务拆分（MVP，待决策点确认后细化）

- [ ] 确认上述决策点
- [ ] （如选）搭轻量 UI 基座：面板基类 + Manager
- [ ] 准备 TMP 中文字体资源
- [ ] 实现 `LoginPanel` UI（结构 + 引用 + 交互）
- [ ] 实现 `LoginController`/`LoginService`（MVP 模拟登录）
- [ ] 输入校验 + 错误提示 + 按钮态
- [ ] 登录成功 → 进入主流程（切场景/隐藏面板）
- [ ] AiBridge 编译 + PlayMode 跑通验证
- [ ] 自审查（命名空间、SerializeField、清调试日志）后提交

---

## 6. 验收标准（MVP Done 的定义）

- 启动后能看到登录界面，中文正常显示（无方块）
- 空账号/空密码点击登录 → 出现提示、不进入
- 填入任意非空凭据点击登录 → 走"成功"路径 → 进入主流程
- 提交过程中按钮禁用、无重复触发
- 代码符合项目规范，编译零错误，无调试埋点残留

---

## 7. 开放问题

- 是否需要"记住账号/自动登录"？（涉及本地存储）
- 登录失败的错误码/文案规范？
- 第三方登录是否本期就要接 SDK，还是只放占位按钮？
- 美术资源由谁提供、何时到位？（影响能否做到"成品"而非占位）
