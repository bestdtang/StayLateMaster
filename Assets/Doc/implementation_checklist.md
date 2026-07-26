# 实现清单（Implementation Checklist）

本清单已按当前代码、场景与配置重新核对。它记录的是**已验证实现状态与剩余工作**，不再把旧设计数值当作运行时事实。

## 使用说明与事实来源

- `[x]`：已在代码/场景中验证；`[ ]`：尚未完成或仍需验收。
- **运行时数值权威来源**：`Assets/Scenes/SampleScene.unity` 中 `RunController` 绑定的 `Assets/Data/DefaultGameBalanceConfig.asset`。
- `Assets/Scripts/Data/GameBalanceConfig.cs` 中的字段初值只是未绑定 Asset 时的回退值。
- 可调倍率、阈值、时长统一引用配置字段名；本清单不写死容易过时的数值。
- 当前结论：**核心玩法逻辑已完成到火热时间**；**C1 开始菜单**与**局内教程开局流程**已接入；**Audio 框架（C4）**已落地，待挂载 Clip；局内教程/退出 HUD 按钮已接入；**C3 胜负全屏页**已接入。

---

## A. 已验证完成：核心玩法逻辑

### A1. 项目、数值与单局状态

- [x] 目录与基础资源结构：`Assets/Scripts/{Core,Stats,Pendulum,Phone,Interest,Recommendation,HotStreak,Visual,UI,Data,Audio}`、`Assets/{Prefabs,Art,Audio,Doc}`。
- [x] Unity 2022.3 LTS、URP 2D 与 Physics2D 基础设置可用。
- [x] `Stats/HappinessMeter.cs`：快乐值 0–100，加减后 clamp；满值触发胜利事件；场景 `HappinessBar` 已绑定；≥85 时 fill 白/原色闪烁（`StatBarBlinkView`）；支持 `SetFillDriveExternal` 供 MiddleLayer lag 视图驱动 Fill。
- [x] `UI/HappinessMiddleLayerView.cs`：加快乐 MiddleLayer 先跳、Fill Retarget 后追；减快乐 Fill 先落、MiddleLayer 后追；满值立刻 Snap 双层；提升/缺失两色、不闪烁；火热连滑连续追赶；加快乐 scale punch（不低于 1）；减快乐仅向右水平抖动后回原点。
- [x] `Stats/FatigueMeter.cs`：疲劳值 0–102，只增不减；自然增速、加速度、上限与尾段衰减均由 `GameBalanceConfig` 注入；满值触发失败事件；场景 `TiredBar` 已绑定；≥85 时 fill 白/原色闪烁（`StatBarBlinkView`）；暴露 `OnModifiersChanged` / `TryGetBlinkRateMultiplier` 供 MiddleLayer。
- [x] `UI/FatigueMiddleLayerView.cs`：`TiredBar/MiddleLayer` 始终比 Fill 长 `_leadAmount`（未满值时不超过 0.99）；Blink 修饰三态（减缓/自然/加速）换色 + 慢/中/快颜色 pulse；三态独立 fillAmount 呼吸幅度与半周期；TiredBar 根节点三档 scaleY 蠕动（普通：无 Blink 或减缓；加速：Blink mult>1；疯狂：超级疲劳），仅 Y 轴 Yoyo；疲劳为 0 时 MiddleLayer 隐藏但根节点仍普通蠕动。
- [x] `Stats/CrazyFatigueController.cs`：连续 3 次完全失败进入超级疲劳（`CrazyFatigue` 增速 × 配置、钟摆周期略快）；成功或偏早/偏晚眨眼 1 次解除；`OnEntered`/`OnExited` 供后续视觉。
- [x] 疲劳临时倍率按 `FatigueModifierSource` 管理：同源最新覆盖、跨源相乘、到期移除；`ClearModifier` 供超级疲劳退出。
- [x] `Core/GameState.cs`：`Intro / Playing / HotStreak / Paused / Win / Lose` 状态已定义。
- [x] `Core/RunController.cs`：重置并启动单局、下发 Balance；`EvaluateEndGame` / `EndRun` 集中胜负（快乐 ≥100 / 疲劳 ≥102，同帧双满快乐优先）；停止输入与玩法并发出胜负事件。
- [x] `RestartRun()` / `PrepareRestartCountdown()` 已提供逻辑入口；结算页 Restart → 321 倒计时 → `BeginRun()`。

### A2. 钟摆眨眼

- [x] `Pendulum/PendulumController.cs`：钟摆持续往返、判定区随时间缩小、任意眨眼判定后刷新、缩尽自动判完全失败。
- [x] 判定区难度阶段由 `ZonePhase2AtFatigue` / `ZonePhase3AtFatigue` 驱动，不与视觉疲劳分段绑定。
- [x] 二阶段反侧随机刷新并缩小满宽；三阶段不规则角加速度摆动；中心与半宽被钳制在轨道范围内。
- [x] `Pendulum/BlinkInput.cs`：空格触发成功 / 偏早偏晚 / 完全失败三态；未缩尽且不按键时不判罚。
- [x] 三态疲劳倍率与持续时间读取 `BlinkSuccessRateMultiplier`、`BlinkEarlyLateRateMultiplier`、`BlinkMissRateMultiplier`、`BlinkModifierDuration`。
- [x] 眨眼在 `Playing` 与 `HotStreak` 均接受；`Intro / Paused / Win / Lose` 不接受。火热期间疲劳仍增长，眨眼倍率照常生效。

### A3. 手机内容与筛选

- [x] `Data/TopicId.cs`：5 个 Topic（萌宠、人物、美食、汽车、足球）。
- [x] `Data/CardSpriteLibrary.cs` + `Data/PickedCard.cs`：单一 Sprite Library SO 与轻量运行时抽卡结果。
- [x] `Phone/CardView.cs` + `Assets/Prefabs/CardPrefab.prefab`：展示卡片、正确/错误差异化飞出动画、结束后销毁。
- [x] `Phone/SwipeInput.cs`：左右方向键输入；胜负和火热结束缓冲期间可禁用。
- [x] `Phone/FeedSpawner.cs`：滑走时立即生成下一张；进入火热的触发帧会替换预生成的非兴趣卡。
- [x] `Phone/SwipeFilterHandler.cs`：四种 Topic/方向组合判定；正确兴趣右滑加快乐；错误操作断连击并按当前疲劳计算扣分。

### A4. 兴趣与推荐流

- [x] `Interest/InterestManager.cs`：随机切换当前兴趣、避免连续相同 Topic、提前发出 `OnSwitchPreview`；实际切换间隔以场景 Inspector 为准。
- [x] `UI/InterestBubbleView.cs`：显示当前 Topic 图标；`OnSwitchPreview` 时 `BubbleSmall` 长大预告，切换时收起并抖动 `BubbleImage`。
- [x] `Recommendation/TopicWeights.cs`：四种滑卡结果训练长期权重；兴趣亲和倍率、权重增量与下限均由 Balance 配置。
- [x] `Recommendation/CardPicker.cs`：按训练权重 × 当前兴趣亲和抽取 Topic，支持兴趣硬保底与 Sprite 防重复。
- [x] 兴趣切换不清空旧训练权重，新兴趣仍能靠亲和倍率与硬保底逐渐出现。

### A5. 连击与火热时间

- [x] `Recommendation/ComboStreakCounter.cs`：正确累积、错误或超时清零；续连窗口按 `ComboBaseWindow`、`ComboWindowShrinkPerStreak`、`ComboMinWindow` 逐级缩短。
- [x] `UI/ComboStreakView.cs`：streak≥1 时显示 HotBar + 当前连击数；续连窗口内随时间缩小；逐击 scale punch；火热开始时 ButtonIcon 代替数字（动效由 Icon 自身 Animation 负责），`HotCount` 统计右滑次数并随次数放大，Bar 持续抖动，结束时大抖后消失。
- [x] `HotStreak/HotTimeController.cs`：仅按 `HotTriggerStreak` 连击阈值触发，不使用累计快乐条件。
- [x] 同一根 `HotBar` 在 `Playing` 且 streak≥1 时显示连击累积比例；满格进入 `HotStreak` 后先经 `HotPreBufferDuration` 满格缓冲（剧烈抖动），再按 `HotTimeDuration` 倒计时下降；满格时 fill 闪到可调火热色并在下降阶段保持，下次累积恢复原色。
- [x] 火热期间强制只抽当前兴趣 Topic；右滑固定增加 `HotHappinessPerSwipe`，左滑无收益也无惩罚。
- [x] 火热期间不更新长期推荐权重、硬保底或连击；兴趣定时切换暂停。
- [x] 火热结束后清除强制 Topic、切换一次兴趣，并在 `HotPostBufferDuration` 内禁用左右滑作为容错。
- [x] 胜负发生时立即中止火热与结束缓冲。

---

## B. 部分完成：场景与基础 UI

- [x] `Assets/Scenes/SampleScene.unity` 已有手机/卡片区、钟摆与判定区、兴趣气泡、快乐条（Fill + MiddleLayer 双向 lag）、疲劳条（Fill + MiddleLayer 三态 lead/pulse + 根节点 scaleY 蠕动）、HotBar、ComboNum。
- [x] 当前基础 HUD 采用场景内联组件直接绑定，无需为了形式强制新增统一 `HUD.cs`。
- [x] `HotBar` 的当前 UX 已确定：streak≥1 或火热倒计时期间显示；累积阶段随连击 scale 上涨，火热阶段持续抖动后大抖消失。
- [x] `UI/EventBannerView.cs`：`HeavyEyesBanner` 在超级疲劳进入时弹出，`PerfectFeedBanner` 在火热时间开始时弹出；各显示 1.5s 后隐藏，等待下次触发；胜负/Intro 时强制收起。
- [ ] 补齐主角/脸部画面区域（归入 juicy 表现，不阻塞基础逻辑闭环）。
- [x] 实现兴趣切换预告与切换动画：`BubbleSmall` 预告长大 + `BubbleImage` 切换位置抖动。
- [ ] 清理场景中重复系统组件，并将依赖 `FindObjectOfType` 的关键空引用改为显式绑定后做一次回归。
- [ ] 检查不同分辨率下手机、钟摆、气泡、三条数值/状态显示是否保持可读。

---

## C. 当前优先：补齐完整游戏流程

### C1. 开始菜单（独立 Scene）

- [x] 开始菜单 Scene `Assets/Scenes/StartMenu.unity` 与主玩法 Scene `SampleScene.unity` 分离；Build Settings 中 `StartMenu` 为 index 0 启动 Scene。
- [x] 菜单 Scene UI：仅 **Start** 进入主玩法（`StartMenuController`）；**不含教程**；菜单起播主 BGM（`AudioManager` 跨 Scene 续播）。
- [x] **开始游戏**：`SceneManager.LoadScene(GameScenes.Gameplay)`；玩法 Scene 加载后先显示教程 overlay，**不立即** `BeginRun()`。
- [x] 主玩法 Scene 承担开局前教程停留；`GameState.Intro` 保持到玩家点击开始并完成 321 倒计时。

### C2. 教程页与局内入口

- [x] `SampleScene` 内联 **TutorialPage** + `TutorialPageView`：进入玩法 Scene 后默认打开；**两页翻页**（Page_1 + 右箭头 → Page_2 + 左箭头）；**首次看完两页后**显示底部开始 → 隐藏教程 → **321 倒计时** → `BeginRun()`；**顶部退出** 此时隐藏。
- [x] 局内复查教程（`ShowForReview()`）：显示 **顶部退出**、隐藏底部开始；可翻两页；打开时同样 BGM ×0.6 + `tutorialIntro`；点退出恢复 BGM 并 `ResumeFromTutorial()`。
- [x] 开局 321：倒计时前隐藏钟摆 / 疲惫条 / 快乐条 / 兴趣气泡，快乐与疲惫归零；**3→钟摆、2→疲惫条、1→快乐条**（入场 tween 已写入 `TutorialPageView`：Intro1 `(0,500)` / Intro2 `(0,-300)` / Intro3 `(800,0)`，0.45s OutBounce From）；倒计时露出的 Arc **判定扇形为空**，`BeginRun`→`ResetState` 后才刷满；结束后显示兴趣气泡再开局；连击 / HotBar 仍仅 streak≥1 显示。场景上原 DOTweenAnimation 可随后删除。
- [x] 主玩法 Scene 常驻 HUD 按钮（`RunHudButtons`，**不做暂停层**）：**Tutorial** → `ShowForReview()`（打开 `TutorialPage` + 顶部 `Back`）；**Exit** → `LoadScene(StartMenu)`。
- [x] 胜负全屏页、火热结束缓冲期间隐藏局内教程/退出 HUD 按钮（`RunHudButtons`）。

### C3. 结尾全屏页（胜/负分离）

- [x] 胜利与失败分别打开**不同的全屏页面**，响应 `RunController.OnGameWon` / `OnGameLost`，并兼听 `OnStateChanged` 兜底（`UI/ResultsScreenView.cs` → 场景 `Win` / `Lose`）。
- [x] **失败页**：仅结算并展示**当下的快乐值**（`HappinessMeter.Value`；可选 TMP `HappinessValue`/`HappyNum`）；不展示疲劳、连击、火热次数、眨眼率等其他数据。
- [x] **胜利页**：不做数值结算，仅呈现胜利演出/文案即可。
- [x] 两页共用操作：**重来**（跳过教程，走开局同款 321 后再开一局）、**回到主菜单**（`SceneManager.LoadScene` 回菜单 Scene）。
- [x] 判定成立后玩法立即停住，但结算页延迟 `_showDelay`（默认 0.5s）再弹，让玩家看清快乐条冲满 / 疲劳条见底；`OnGameWon` 与 `OnStateChanged` 双触发做去重，重开时取消未弹出的延迟。
- [ ] 全屏页显示期间停止疲劳、钟摆、兴趣切换与手机输入，且不会残留火热/连击状态。
- [ ] **本阶段不做**详细战绩统计采集（正确筛选数、眨眼成功率等）；若后续需要再单独扩展。

### C4. 基础音效

- [x] `Audio/AudioManager.cs` + `Audio/RunAudioController.cs` + `Data/GameAudioConfig.asset`：SFX OneShot、单一主 BGM、疯狂疲劳 Loop 叠层；疲劳二/三阶段打哈气间歇 OneShot；`GameAudioConfig` 每条 Clip 配独立音量滑条（0–2）+ Master/SFX/BGM 总线；`AudioManager` 跨 Scene 单例。
- [x] 音效槽位：快乐增/减音（`swipeCorrect`/`swipeWrong`，随 `HappinessMeter` 变化）、胜负、眨眼、疯狂疲劳、划手机、连击、兴趣、火热、入场教程（`tutorialIntro`）、打哈气（`fatigueYawn`）。
- [x] 主 BGM `bgmMain` 从开始菜单起播并跨 Scene 续播；教程 overlay（入场 / 局内复查）期间 ×`introTutorialBgmVolumeScale`（默认 0.6），关闭后恢复；疲劳二阶段每 10s、三阶段每 5s 播 `fatigueYawn`（跨阈值立刻播一次）。
- [x] 玩法事件补充：`FeedSpawner.OnSwipeJudged`、`HotTimeController.OnHotSwipe`。
- [ ] **待资源**：在 `GameAudioConfig.asset` 挂载实际 Clip（`Assets/Audio/SFX/`、`Assets/Audio/BGM/`）。
- [ ] 为钟摆加入基础节拍提示；确保不会掩盖眨眼判定反馈。
- [ ] 火热时间环境叠层（`ambientHotStreak` 预留槽）留 D 区。

### C5. 内容素材

- [ ] 扩充 `Assets/Data/CardSpriteLibrary.asset`：当前每个 Topic 只有一张唯一图片被重复填入数组，防重复逻辑无法产生实际内容变化。
- [ ] 每个 Topic 准备足够的可辨识图片，并验证短时间内不会明显重复。
- [ ] 卡片标题/描述属于可选内容，不阻塞首个完整版本。

---

## D. Juicy / Polish（基础流程闭环后）

- [ ] `Visual/FatigueVisuals.cs`：疲劳画面重影、模糊、变暗、变色、眼皮压低等；**疲劳画面属于 juicy，不作为当前逻辑阻塞项**。
- [ ] `Visual/CharacterExpression.cs`：清醒、疲劳、火热、胜利、失败表情/状态。
- [x] `Visual/HotCrazyVisualLayers.cs`：Hot Time / Crazy Fatigue 开关 Bed、Character、RightMask 下对应子物体（`HotBed`/`HotCharacter`/`HotBackground`，`CrazyBed`/`CrazyCharacter`/`CrazyBackground`）；胜负/Intro 强制关闭。
- [ ] 正确/错误筛选强化：手机发光、表情、轻微抖动、快乐变化反馈；现有卡片飞出动画作为基础保留。
- [ ] 眨眼强化：自然眨眼、短暂清晰/变暗；判定区颜色闪烁（`PendulumController`）作为基础保留。
- [x] 钟摆区域抖动（`Visual/PendulumFeedbackView.cs`：绿/黄圈上下抖、红圈/缩尽左右抖）。
- [x] 兴趣气泡预告：切换前 `BubbleSmall` 长大；切换时 `BubbleImage` 位置抖动并收起小气泡。
- [ ] 火热时间画面、火苗、节奏与音乐增强。
- [ ] 环境音乐随疲劳变化，手机/钟摆/疲劳反馈音效分层。（主 BGM + 疲劳打哈气已实现；钟摆节拍与疲劳呼吸等待资源）
- [x] 疯狂疲劳状态机：连续 3 次完全失败触发；成功/偏早/偏晚 1 次解除；`HotCrazyVisualLayers` 已订阅 `OnEntered`/`OnExited` 切换场景层。

---

## E. 文档与配置债务

- [x] 同步 `Assets/Doc/game_design_spec.md` §17：结算页改为胜/负分离全屏页；失败仅展示当下快乐值，胜利无数值结算；两页提供「重来 / 回到主菜单」。
- [ ] 同步 `Assets/Doc/game_design_spec.md` §8：移除“连续正确 + 累计快乐触发火热”的旧规则，主文改为当前强制兴趣 + 右滑得分机制。
- [ ] 同步 `Assets/Doc/game_design_spec.md` §14：HotBar 当前在 `Playing` 常显，并在火热期间切换为倒计时，不是“仅火热时出现”。
- [ ] 同步 `.cursor/rules/gameplay-core.mdc`：眨眼倍率叙述、火热触发/刷新/结束规则以当前配置与代码为准。
- [ ] 同步 `.cursor/rules/unity-csharp-jam.mdc`：删除“火热进度仅火热时显示”的旧约束。
- [ ] 修正 checklist 之外仍写死旧判定阶段或旧倍率的代码注释（如 `PendulumController.cs`、`BlinkResult.cs`）。
- [ ] 决定并落实配置策略：让 `GameBalanceConfig.cs` 回退默认值与 `DefaultGameBalanceConfig.asset` 对齐，或明确注释两者允许不同且运行时以 Asset 为准。
- [ ] 文档中的视觉疲劳分段与钟摆判定区配置阈值保持明确解耦。

---

## F. 整合、平衡与最终验收

### F1. 工程与场景

- [ ] Unity 无编译错误，关键 Inspector 引用完整；开局、胜负、重开均无 MissingReference/NullReference。
- [ ] 跨系统通信维持 `RunController`、直接序列化引用或少量 C# event；不在 `Update` 中查找组件。
- [ ] 逐项验证 `DefaultGameBalanceConfig.asset` 的字段确实影响对应系统，避免场景覆盖或脚本回退值造成误判。

### F2. 核心体验（对照 `game_design_spec.md` §19）

- [ ] 玩家可在菜单 Scene 打开教程页，并在数秒内理解左右滑与空格眨眼。
- [ ] 滑动会明显改变后续 Topic 分布；兴趣切换后新 Topic 不会长期刷不出。
- [ ] 疲劳始终向 100 推进，眨眼只能延缓，不能永久阻止。
- [ ] 钟摆眨眼与手机筛选形成明确注意力冲突。
- [ ] 连击窗口、火热触发、火热收益和结束缓冲节奏清楚且可调。
- [ ] 火热开始后第一张可见卡即为当前兴趣，不残留预生成的非兴趣卡。
- [ ] 错误滑卡惩罚随疲劳略增但不过高；快乐与疲劳胜负不会同时重复触发。
- [x] 主玩法 Scene 常驻「教程 / 退出」按钮，可随时查看教程或返回菜单 Scene。
- [ ] 菜单 Scene → 主玩法 Scene → 胜利/失败全屏页 →「重来」或「回到主菜单」，形成完整流程。
- [ ] 基础音效能明确区分正确、错误、眨眼与胜负。（框架已接好，待挂载 Clip 后验收）
- [ ] 单局节奏紧凑，可连续重玩；最后再进行 juicy 表现与数值平衡。