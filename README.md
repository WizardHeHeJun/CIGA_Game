# 旧室（Old Rooms）· 心理恐怖锚点解谜

> Unity **2022.3.62f2** · URP 2D（Renderer2D） · TextMeshPro · C#（命名空间 `Ciga.*`，业务程序集 `Ciga.Game`）
> 48H Jam 作品，当前为**正式内容版本**（美术/音频已接入，Demo 体系已全部退役）。

主角在停电的夜晚失去母亲，「家」成了不断被重构、却永远修不完整的心理模型。白天（关卡1）玩家在卧室挑选物品，把它们的**抽象特征**（颜色/形状/材质/质感/声音 五维）汇成词条池；夜晚（关卡2）房间异变，系统从池中抽出 5 个特征作为**目标锚点**，玩家要在五个恐怖场景里找到满足这些特征的物品，在倒计时和 San 值耗尽前完成锚定。

**锚定的是物品的抽象特征，不是物品本身。**

---

## 一、快速开始

| 项 | 要求 |
|------|------|
| 引擎 | Unity 2022.3.62f2（Unity Hub 打开本目录） |
| Git | 克隆前先 `git lfs install`（二进制资产走 LFS，`.gitattributes` 已配） |

1. 打开工程，等编译完成。
2. 打开场景 `Assets/Res/Scene/GameMain.unity`，点 ▶ Play。
3. 主菜单 → 开始 → 教程图（任意键） → 关卡1。

### 操作键

| 操作 | 按键 |
|------|------|
| 移动 | `WASD` / 方向键 |
| 交互（选取/拾取） | `E`（靠近物品高亮后） |
| 检视（浮字显示特征 + 播放该物品的声音特征音效） | `R` |
| 记忆石板（查看 5 个目标锚点与完成度，暂停） | `Tab` |
| 关卡2 设置弹层（继续/重开/回主菜单/退出） | `Esc` |

---

## 二、玩法流程与核心规则

```text
主菜单(GameMain) → 教程图 → 关卡1·卧室(InitRoom 相位)
  从 8 件物品中选 5 件 → 5件×5维特征进词条池 → 选满自动黑屏跳关
→ 关卡2(HorrorLevel 相位)：走廊 Hub + 4 个房间（客厅/浴室/厨房/杂物间）
  进入时：清空背包(上限8)、启动 180s 倒计时、从词条池随机抽 5 个目标锚点
  走廊四门 E 进房间，房间返回门 E 回走廊；切场景背包/锚点/倒计时全保留
→ 结算：每个目标锚点都有 ≥1 件背包物品带该特征 → 通关
         倒计时归零 / San 归零 / 背包满8仍未满足 → 失败
```

关键规则（都有对应实现，改动前先看 `GameManager`）：

- **关卡1 的物品不带入关卡2**——进关卡2 前背包清空，只有特征进词条池。
- **切换房间背包不清空**，物品拾取状态跨房间保持（`runtimeKey` 机制）。
- 拾取**错误物品**（不命中任何目标锚点）：扣 San（`MismatchLoss`）+ 红闪，且照样占背包格。
- 拾取命中**尚未覆盖**的锚点：暖音 + 浮字 + 若命中声音维叠播对应音效。
- San 在关卡2 随时间衰减（`DecayPerSec`），越低画面越暗、心跳/噪音越密、移速下降。
- **防死局**：抽目标锚点时会自动剔除「关卡2 没有任何物品能满足」的特征（见九·1）。
- 复合材质（如「木质玻璃」）是**独立单值**：不命中「木质」也不命中「玻璃」——有意的解谜设计。

---

## 三、场景与启动链

| 场景 | 角色 |
|------|------|
| `Assets/Res/Scene/GameMain.unity` | 入口。全屏美术主菜单；`SceneLoader`（DontDestroyOnLoad 单例）在此诞生：加载遮罩、BGM 循环播放、UI 点击音分发、AudioListener 兜底 |
| `Assets/Res/AnchorHorror/Bootstrap.unity` | 游戏本体。`GameManager`（DontDestroyOnLoad）+ 玩家 + 相机 + 全套 uGUI 面板。**两关全部内容在本场景内动态重建**（`__LevelRoot` 销毁重建），不做 Unity 场景切换 |

Build Settings 仅这两个场景（GameMain=0, Bootstrap=1）。`Login.unity` 存在但未启用。

---

## 四、代码结构

```text
Assets/
├── Scripts/
│   ├── AnchorHorror/          业务核心（Ciga.AnchorHorror）
│   │   ├── Core/              FeatureUnit / AnchorTarget / EventBus / 枚举（*.Generated.cs 由 CSV 生成勿手改）
│   │   ├── Config/            GlobalConfig / FeatureDatabase / ItemDatabase / LevelData / LevelSequence / ResultConfig（SO）
│   │   ├── Feature/           FeatureTag（物品五维特征组件 + IInteractable）
│   │   ├── Systems/           AnchorSystem(抽锚+满足判定) / Inventory(背包) / LevelSpawner / ItemFactory / InteractionSystem / SanitySystem
│   │   ├── Flow/              GameManager（单例·相位状态机·背包·倒计时·子场景切换·组合根）
│   │   ├── Player/            PlayerController2D + 行走动画
│   │   ├── UI/                BackpackPanel(右侧背包+翻页) / MemoryPanel(Tab石板) / CountdownPanel / InGameHudPanel / SanBarPanel / ResultScreen / TutorialPanel
│   │   └── Feedback/          SanityFeedback / MatchFeedback / CameraShake2D / FloatingText 等（表现层，只订阅 EventBus）
│   ├── Startup/               SceneLoader / MainMenuPanel / SceneNames / 启动配置 SO
│   └── UI/                    UIManager / UIFadePanel / UiClickSfxBinder 等通用件
├── Editor/
│   ├── AnchorHorror/          生成器（见「五」）+ Codegen（CSV→枚举/SO）
│   ├── AiBridge/              AI 验证桥（HTTP :17900，随编辑器自启）
│   └── Startup/               StartupSetup（启动流装配生成器）
├── Tests/AnchorHorror/        EditMode 46 条 + PlayMode（独立 asmdef）
└── Res/                       场景 / SceneArt(进包美术) / Audio / Levels(关卡资产) / UI / 配置 SO
acts/                          美术&音频原始交付目录（gitignore，不入库；生成器从这里拷图进包）
```

架构约定（改代码前必读）：

- **表现层只订阅**：`Feedback/`、UI 面板只读 `EventBus` + `GameManager.Instance`，逻辑层绝不反向依赖。
- **核心数值改动不走 EventBus**（直接引用调用），EventBus 只广播"发生了什么"。
- **SO 不存运行时状态**；运行时状态在 `GameManager` 持有的普通类实例（如 `Inventory`）。
- 枚举 **None 恒为 0**，各特征枚举第一项必须是 None（匹配链路靠 `Value==0` 剔除）。
- 物品是**自包含**的：`PlacedItem` 自带覆盖特征+美术，`itemId` 留空即可（不依赖 ItemDatabase）。

---

## 五、内容管线（交接重点：改内容不改代码）

三个真源，改完跑对应菜单（`Ciga/AnchorHorror/`），最后由测试守卫兜底：

| 真源 | 生成菜单 | 产物 |
|------|----------|------|
| `Assets/Res/AnchorHorror/AnchorFeatures.csv`（特征维度/取值/中文名/颜色/音效） | 从CSV生成特征 | `FeatureEnums.Generated.cs` + `FeatureTag.Generated.cs` + `FeatureDatabase.asset`（61 条 + 7 个声音 clip） |
| `Assets/Editor/AnchorHorror/FormalSceneArtSetup.cs` 中六个 `SceneSpec`（**场景物品的美术路径+五维特征都声明在这里**） | 生成正式关卡美术数据（并接线） | 从 `acts/` 拷图进包 + 六个 `Formal_*.asset` + `Formal_Sequence.asset` + 接线 Bootstrap |
| `Assets/Res/AnchorHorror/AnchorItems.csv`（物品目录，**当前为空**——正式物品全自包含） | 从CSV生成物品目录 | `ItemDatabase.asset`（空目录 + 兜底图） |

美术约定：**整幅画布 overlay**——每个物品的 Default/Active 图与背景同尺寸画布，运行时放原点即自动对齐；交互碰撞框由 Default 图的 **alpha 包围盒**自动计算。因此**美术导出必须保留透明通道**（生成器对覆盖率 ≥95% 的图会打告警，说明白底被拍平了）。

换图流程：替换 `acts/` 对应源图 → **删除 `Assets/Res/AnchorHorror/SceneArt/` 下的进包旧图**（生成器只在目标不存在时才拷贝）→ 重跑「生成正式关卡美术数据（并接线）」。

菜单工具一览（`Ciga/AnchorHorror/`）：

| 菜单 | 用途 |
|------|------|
| ★ 一键重建全部（装配+正式关卡数据） | 重建 Bootstrap 装配 + 正式关卡数据并接线（收尾必为正式） |
| 生成可运行装配 | 只重建 Bootstrap 场景装配（默认接正式序列） |
| 生成正式关卡美术数据（并接线） | 内容真源 → 关卡资产 + 接线 |
| 接线正式关卡数据 | 只把 Formal_Sequence 接回 Bootstrap（接线被弄乱时的恢复入口） |
| 从CSV生成特征 / 从CSV生成物品目录 | 两张 CSV 的 codegen |
| 校验关卡覆盖（死局检测） | 检查关卡2 物品特征是否覆盖关卡1（见九·1 已知报错） |
| 校验生成往返一致 | 特征 codegen 产物与表一致性核对 |

---

## 六、六场景物品配置总表（与策划表一致，测试守卫）

> 该表已固化为 `FormalSceneContentConformanceTests`（五维逐项断言），改内容 = 改 `FormalSceneArtSetup` 的 SceneSpec + 同步改该测试。

**关卡1 · 卧室（8 选 5）**：床(白/长条/布料/柔软/布触声)、衣柜(棕/方/木/粗糙/木摩擦)、梳妆台(白/方/木/光滑/木摩擦)、台灯(米黄/圆/金属/柔光/灯嗡鸣)、和母亲的合照(棕/方/**木质玻璃**/光滑/玻璃响)、窗户(透明/方/玻璃/反光/滴答)、地毯(**浅灰**/长条/布料/柔软/布触声)、椅子(白/不规则/木/光滑/木摩擦)

**关卡2 · 走廊 Hub**：门A/门B（纯视觉层，不可拾取）、地毯(深红/长条/布/纤维/布触声)、墙灯(暖黄/圆/玻璃金属/亮面/灯嗡鸣)、钥匙(金/不规则/金属/光滑/金属机械)、楼梯(灰/长条/石材/粗糙/木摩擦)

**客厅**：沙发(深灰/长条/布/柔软/布触声)、电视机(黑/方/玻璃/光滑/金属机械)、相框(棕/方/木/光滑/玻璃响)、茶几(深棕/方/木/磨损/木摩擦)、落地灯(米黄/锥形/布/柔光/灯嗡鸣)、玩具箱(彩色/方/塑料/粗糙/塑料响)

**厨房**：电饭煲(白/方/金属塑料/光滑/金属机械)、水龙头(银/弯曲/金属/反光/滴答)、冰箱(白/方/金属/哑光/金属机械)、碗柜(棕/方/木/粗糙/木摩擦)、餐桌(深棕/方/木/磨损/木摩擦)、窗户(透明/方/玻璃/光滑/玻璃响)、钟表(金/圆/金属/刻度/滴答)、水槽(银/方/金属/湿润/滴答)

**浴室**：马桶(白/圆/陶瓷/光滑/滴答)、洗衣机(白/圆/金属/哑光/金属机械)、镜子(银/方/玻璃/反光/玻璃响)、洗手台(白/方/陶瓷/光滑/滴答)、毛巾(蓝/长条/布/柔软/布触声)、排水口(黑/圆/金属/湿润/滴答)

**杂物间**：旧电饭煲(米黄/方/金属/脱漆/金属机械)、破钟(黑/圆/金属/裂纹/滴答)、纸箱(棕/方/纸/破损/木摩擦)、镜子碎片(银/不规则/玻璃/裂痕/玻璃响)、坏玩具(彩色/不规则/塑料/划痕/塑料响)、折叠椅(黑/不规则/金属/划痕/金属机械)

---

## 七、音频

| 类别 | 资源 | 接线位置 |
|------|------|----------|
| BGM（全程循环，音量 0.35） | `Res/Audio/AnchorHorror/bgm.mp3` | GameMain 场景 SceneLoader `_bgmClip` |
| UI 点击音（0.8） | `ui-click.mp3` | SceneLoader `_uiClickClip` → 各面板 UiClickSfxBinder |
| 7 个物品声音特征（R 检视/命中时播，0.7） | `Res/Audio/AnchorHorror/Items/*.mp3`（文件名含尾随空格，勿改名） | `FeatureDatabase.asset` Sound 维 7 条 entry 的 `_clip`（由 CSV audioGuid 列生成） |
| 心跳/尖锐噪音/失败音/匹配暖音/过渡低语 | **程序化生成，即正式版**（无音频资产，勿当占位替换） | SanityFeedback / MatchFeedback / GameManager |

场景相机没有烘 AudioListener——由 `SceneLoader.Awake` / `GameManager.Awake` 判空兜底补挂，删这两段会全程无声。

---

## 八、测试与验证

- **EditMode 46 条**（Window → General → Test Runner）：逻辑回归（背包/抽锚/San/撞键）、生成器落盘、**对表守卫**（六场景 40 物品五维+美术+音效 clip 逐项断言）、**接线守卫**（Bootstrap 必须指向 Formal_Sequence，被弄乱即红灯）。
- ⚠️ **EditMode 测试会触发生成器重建资产**（Bootstrap/字体图集等会被改写）。提交前甄别：`Formal_*` 只在你改了内容真源时才提交；`Bootstrap.unity`/`* SDF.asset` 的纯重排 churn 不要提交。
- **AI 验证桥**（`Assets/Editor/AiBridge/`，HTTP `127.0.0.1:17900`，随编辑器自启）：供 AI/脚本做编译、读 Console、进出 Play、截图、跑测试。CLI：
  `PYTHONIOENCODING=utf-8 PYTHONUTF8=1 python .claude/skills/unity-bridge/scripts/bridge.py compile|console|play|stop|test|screenshot|hierarchy`
- PlayMode 测试（`TwoLevelFlowPlayTests`）纯代码构造序列，覆盖 SC-1~6 流程；经桥跑结果回调易丢，建议用 Test Runner 窗口跑。

---

## 九、已知事项 / 坑

1. **内容缺口**：关卡1 的「浅灰」（地毯）与「木质玻璃」（合照）在关卡2 没有任何物品能满足。运行时已防死局（这两个特征不会被抽为目标，抽取时打日志），「校验关卡覆盖」菜单会对 Formal_Sequence 报错——**属预期**。策划在关卡2 任一场景补带这两个特征的物品即可放开。
2. **衣柜 Active 图是占位**（Default 提亮 25% 程序生成）——美术源头没画。补图：交 `acts/.../Scene1_Bedroom_v1/Active/Bedrrom_Drobe_Active.PNG` → 删进包 `SceneArt/Bedroom/Bedroom_drobe_Active.png` → 重跑生成菜单。
3. **美术导出务必保留透明通道**：曾有源图（杂物间坏玩具）白底拍平导致整屏遮挡+全屏碰撞框，已修复并加了告警拦截。
4. 生成器 `CopyImportArtSprite` **只在进包目标不存在时拷贝**——换图必须先删进包旧图（见五）。
5. **客厅茶几的 Active 源图在 acts 里命名为 `LivingRoom_Bed_Active.PNG`**（美术命名错误），生成器已按此引用；美术若改名需同步 `FormalSceneArtSetup`。
6. 关卡编辑器 / Demo 体系 / HorrorLevel 场景 / 旧物品目录已于 2026-07-08 全部移除（PR #29）——正式内容唯一产出路径就是 FormalSceneArtSetup，别找旧工具。
7. 常调数值都在 `Assets/Res/AnchorHorror/GlobalConfig.asset`：选取上限(5)/背包上限(8)/倒计时(180s)/目标锚点数(5)/San 衰减与扣血/交互半径/淡入淡出时长等，改数值零重编译。

---

## 十、版本控制

- 仓库 GitHub 私有 `CIGA_Game`，主干 `origin/main`。
- **提交一律走 PR**（从 main 切分支 → commit → push → PR → 评审合并），不要直推 main——团队多窗口/多人并行，直推易互相覆盖。
- 二进制走 **Git LFS**；**禁止 GitHub 网页拖拽上传**（会无视 .gitignore 把 Library 传上去）。
- `Library/ Temp/ Logs/ obj/` 与本地 AI 工作目录（`ai-docs/ ai-shared/ .claude/ CLAUDE.md`）均已 gitignore。
