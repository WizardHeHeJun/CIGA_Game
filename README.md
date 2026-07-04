# CIGA · 心理恐怖锚点解谜（Anchor Horror）

> Unity 2022.3.62f2 · URP 2D · TextMeshPro · C#（命名空间 `Ciga.*`）
> 当前为 **48H Jam 程序框架阶段**：用**色块**占位演示，把计划玩法完整跑通（不含美术/音效资源）。

一款心理恐怖题材的「锚点解谜」小游戏。主角在停电的夜晚失去母亲，从此「家」成了不断被重构、却永远修不完整的心理模型。白天玩家触碰物品、把它们的**抽象特征**（颜色 / 形状 / 材质 / 质感 / 声音，由配置表驱动）选作「锚点」；夜晚房间异变为恐怖空间，玩家要在扭曲环境里找到匹配锚点特征的物品来维持**理智值（San）**，激活全部锚点即通关。

**锚定的是物品的抽象特征，不是物品本身。**

---

## 一、环境要求

| 项 | 要求 |
|------|------|
| 引擎 | **Unity 2022.3.62f2**（用 Unity Hub 打开本工程目录） |
| 渲染 | URP 2D（Renderer2D，工程内已配好） |
| 平台 | Windows / 编辑器内 Play 即可 |

> 二进制资产走 **Git LFS**（`.gitattributes` 已配）；克隆前请先 `git lfs install`。

---

## 二、快速开始（把色块 Demo 跑起来）

### 1. 打开工程
用 **Unity Hub → Add** 添加本目录并打开，等右下角编译转圈结束。

### 2. 一键生成演示
顶部菜单 **`Ciga` → `AnchorHorror` → `生成可运行装配`**。

它会自动完成：生成方块 sprite（`WhiteSquare.png`）、`Bootstrap` / `HorrorLevel` 两个场景、把各系统接线、加入 Build Settings，并打开 `Bootstrap` 场景。Console 出现 `可运行装配已生成…按 Play 即可联调` 即成功（幂等，可重复点）。

### 3.（建议）让演示不用干等 180 秒
初始房间只放了 4 个物品，而默认过渡条件是「候选 ≥ **8** 个 **或** 时长 ≥ **180** 秒」。为免干等：

1. `Project` 窗口进 `Assets/Res/AnchorHorror/`，单击 **`GlobalConfig`** 资产。
2. `Inspector` 里「**初始房间触发**」分组下，把 **`Candidate Threshold`** 由 `8` 改为 `4`（或把 `Time Threshold` 改小）。

### 4. 运行
点顶部 **▶ Play**。画面：中间**青色方块 = 玩家**，四周**白色方块 = 可交互物品**，**左上角白字 = 状态 HUD**。

---

## 三、操作与玩法

### 控制键

| 操作 | 按键 |
|------|------|
| 移动 | `W` `A` `S` `D` / 方向键 |
| 交互（收集 / 拿取） | `E` |
| 记忆面板（暂停） | `Tab`（见下方备注） |

### 玩法循环

1. **初始房间**：走到白方块旁（会高亮）按 `E`，把它的 4 维特征存入**候选池**；收集够数后黑屏过渡。
2. **过渡**：从候选池**随机抽 5 个特征**作为目标「锚点」，每个锚点需在关卡里找到 **1~3 个**带该特征的物品才算激活。
3. **恐怖关卡（盲摸）**：所有物品都是**一样的白方块**，看不出差别，只能走近按 `E` 试：
   - ✅ 命中（物品某特征 ∈ 未激活锚点）→ 方块**金光** + 冒**关键词浮字** + `San` 回升（一物可同时命中多个锚点，叠加回血）。
   - ❌ 一个都没命中 → 方块**红闪** + `San` **-15**。
4. `San` 还会随时间衰减（0.5/秒），越低画面越暗、相机越抖、移速下降。

### 结局

- **通关**：5 个锚点全部激活（HUD 里全打 `√`）。
- **失败**：`San` 掉到 0（来源：时间衰减 + 摸错扣分）。

### 看进度

**左上角 HUD** 常显 `阶段 / San 值 / 候选数 / 5 锚点进度（√=已锚定）`——盯它即可。

---

## 四、常见问题

| 现象 | 处理 |
|------|------|
| 方块看不见 | 重跑 `Ciga/AnchorHorror/生成可运行装配` 菜单 |
| 文字看不见 | 菜单会自动导入 TMP；或手动 `Window → TextMeshPro → Import TMP Essential Resources` |
| 收集完没反应 | 阈值还是 8；按「二·3」把 `Candidate Threshold` 改成 4，或等满 180 秒 |
| 按 Tab 后画面像卡住 | 本版未接可见面板，`Tab` 只做了 `timeScale=0` 暂停——**再按一次 `Tab` 恢复**（进度看 HUD 即可） |
| 想停止运行 | 再点一次 ▶ |

---

## 五、工程结构

```
Assets/
├── Scripts/AnchorHorror/   业务代码（命名空间 Ciga.AnchorHorror，属 Ciga.Game 程序集）
│   ├── Core/     枚举 / FeatureUnit / AnchorTarget / EventBus
│   ├── Config/   GlobalConfig / FeatureDatabase / LevelConfig（ScriptableObject）
│   ├── Feature/  FeatureTag（物品多维特征组件，维度字段由 CSV 生成，见「六」）
│   ├── Systems/  Anchor / Sanity / Interaction / LevelFeatureRegistry
│   ├── Flow/     GameManager（单例·状态机·过渡协程）
│   ├── Player/   PlayerController2D（俯视 2D 移动）
│   ├── UI/       MemoryPanel / DebugHUD
│   └── Feedback/ SanityFeedback / CameraShake2D / MatchFeedback / FloatingText（表现，只订阅）
├── Editor/AnchorHorror/    AnchorHorrorSetup（一键生成演示装配）+ Codegen（CSV→枚举/SO 生成器）+ LevelEditor
├── Tests/AnchorHorror/     EditMode 逻辑测试 + PlayMode 冒烟测试
└── Res/AnchorHorror/       Bootstrap/HorrorLevel 场景 + 3 个 SO 配置 + WhiteSquare 方块
```

**核心数值全在 `GlobalConfig`（ScriptableObject）里**——调 San 衰减 / 匹配增益 / 阈值 / 移速惩罚等，改数值零重编译。

---

## 六、特征配置表（改表加维度 / 取值，零代码）

物品特征（颜色 / 形状 / 材质 / 质感 / 声音）由**一张 CSV 配置表**驱动，策划改表即可增删维度和取值，无需程序写代码。

### 表在哪

`Assets/Res/AnchorHorror/AnchorFeatures.csv`（纯文本，Excel / VSCode / 记事本都能改）。

### 表结构

每行首列 `rowType` 区分：`@dim` = 维度声明，`@val` = 一个取值，`#` = 注释（忽略）。列依次为：

| 列 | 说明 |
|------|------|
| `rowType` | `@dim` / `@val` / `#` |
| `dimId` `dimKey` | 维度的稳定整数 ID + 英文名（PascalCase） |
| `valueId` | 该维度内取值的稳定 ID：**≥1、每维唯一、连续无空洞**；`0` 保留给 None（别写） |
| `enumMember` | 英文成员名（PascalCase，代码用） |
| `displayNameZh` | 中文名（游戏内浮字 / 记忆面板显示） |
| `colorHex` | **仅 Color 维**填 `#RRGGBB`，其他维留空（回退金色） |
| `audioGuid` | **仅 Sound 维**填（音效 GUID 或 `Audio/xxx.wav` 相对路径），留空则匹配时回退程序化暖音 |
| `note` | 备注，随便写 |

### 加一个取值（最常见）

在对应维度段末尾加一行，ID 接着往下续。例：给颜色加「青色」（Color 现到 18）：

```
@val,0,Color,19,Cyan,青色,#4FD8D8,,新增
```

### 加一个新维度

```
@dim,5,Smell,,,,,,气味维度
@val,5,Smell,1,Floral,花香,,,
```

生成后自动多出 `FeatureSmell` 枚举、`FeatureTag` 上多一个下拉、匹配链路自动支持——**`FeatureTag.cs` 一行不用改**。

### 改完必做：生成

Unity 菜单 **`Ciga` → `AnchorHorror` → `从CSV生成特征`**。它会：校验表（有错弹框告知第几行、不落盘）→ 重新生成 `FeatureEnums.Generated.cs` 与 `FeatureTag.Generated.cs` → 回填 `FeatureDatabase.asset`（中文名 / 颜色 / 音效）→ 触发编译。（旁边 `校验生成往返一致` 可核对磁盘产物是否与表同步。）

### 给物品配特征

- **直接摆场景**：物品挂 `FeatureTag`，Inspector 里逐维下拉选值。
- **数据驱动 / 关卡编辑器**：`ItemDatabase` 里配 `ItemDefinition` 默认特征，`LevelData` 的 `PlacedItem` 可勾 `OverrideFeatures` 覆盖（含声音）；关卡编辑器窗口选中物品也有这些下拉。

### 声音接真实音效

`.wav` 放 `Assets/Res/AnchorHorror/Audio/`（走 LFS）→ 把它的 GUID 或相对路径 `Audio/xxx.wav` 填进 CSV 该 Sound 行的 `audioGuid` 列 → 重跑生成。之后匹配到该声音锚点时 `MatchFeedback` 会播这个 clip（没填则回退暖音，不会崩）。

### 几条硬规矩（校验器会拦）

- `valueId ∈ [1,255]`、每维唯一、**不能写 0**。
- **旧值的 ID 别改**（改了会让已摆好的物品 / 存档错位）——只往后加新值。
- `enumMember` / `dimKey` 必须是合法英文标识符（不能中文、不能数字开头）。
- 复合材质（木质玻璃等）是**独立单值**：配「木质玻璃」的物品命中「木质玻璃」锚点，但**不**命中「木质」或「玻璃」锚点——这是有意的解谜设计。

> ⚠️ 生成物 `FeatureEnums.Generated.cs` / `FeatureTag.Generated.cs` 由表生成，**请勿手改**（下次生成会覆盖）。一句话流程：**改 `AnchorFeatures.csv` → 跑「从CSV生成特征」菜单 → 在物品 / 关卡编辑器里用新值**。

---

## 七、版本控制

仓库 `CIGA_Game`（GitHub 私有，`origin/main`）。日常：`git add -A && git commit && git push`。**只用 `git push`，不要用 GitHub 网页拖拽上传**（会无视 `.gitignore`）。Unity 生成物（`Library/Temp/Logs/obj`）与本地 AI 文档已在 `.gitignore` 排除。
