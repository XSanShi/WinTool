# PaperTodo 逆向分析 v1.0（Windows 原生桌面便签参考实现）

> 参考对象：https://snownico0722.github.io/PaperTodo/?lang=zh → 仓库 snownico0722/PaperTodo（v4.0.0-beta2）
> 本地证据根目录：`.rea/PaperTodo-main/`（取证材料不随公开仓库分发）
>
> **标注约定**
> - 〔源码〕= 直接读到的代码事实，附 `文件:行`
> - 〔README〕= 项目自述文档事实（`README.zh.md`），未经代码逐条核对
> - 〔推断〕= 由代码结构与常量推断、未运行程序验证
> - 〔未证实〕= 明确没有验证，不得当作事实使用

---

## 0. 方法与证据边界

| 项 | 事实 |
| --- | --- |
| 源码获取 | `https://gh-proxy.com/https://codeload.github.com/snownico0722/PaperTodo/tar.gz/refs/heads/main` → 9,181,440 B，解压至 `.rea/PaperTodo-main`〔事实〕 |
| 解压损坏 | 仅 `doc/AGENTS.md` 一项 tar 路径创建失败，其余完整〔事实〕 |
| 规模 | `src/` 下 300 个 `.cs`，合计 126,831 行；仓内 `.xaml` 仅 `App.xaml` 一个，界面全部代码构建〔事实〕 |
| 未做 | 未下载 release 二进制、未运行程序、未实测 DPI/多屏/帧率〔未证实〕 |
| 官网 | `papertodo.html` 25,645 B，纯静态页（CSP `default-src 'self'`），无产品逻辑〔事实〕 |

因此：**本文所有"它怎么做"的结论都来自静态源码**；凡是需要跑起来才能确认的（观感、性能、实际手势手感）一律标〔未证实〕。

---

## 1. 它是什么（产品层）

- **一张纸 = 一个独立窗口**：`PaperWindow*` 共 50 个源文件，是产品的主体〔源码〕。
- **两种纸**：`PaperTypes.Todo = "todo"`、`PaperTypes.Note = "note"`（`src/Models.cs:8-10`）〔源码〕。
- **折叠为胶囊**：`CapsuleWidth = 92` / `CapsuleHeight = 46`（含阴影边框边距，`src/Models.cs:18-19`）〔源码〕。
- **贴边成队列**：折叠后胶囊自动贴屏幕左/右边缘，同屏同边为一列，顶部有"主胶囊"可收拢整列〔README〕+〔源码〕（`EdgeCapsuleLayout`、`MasterCapsuleWindow`）。
- **数据在程序目录**：`AppContext.BaseDirectory` 下的 `data.json` 与 `note-assets.lmdb`（`src/StateStore.cs:34`、`src/NoteImageStore.cs:43`）〔源码〕。
- **明确拒绝 Web 套壳**：README 定位"基于 WPF 原生开发，拒绝 Web 套壳"〔README〕；工程内 WebView2 仅用于插件正文宿主〔推断，`Microsoft.Web.WebView2` 引用 + `WebPaperBodySession` 文件族〕。

产品一句话：**桌面常驻的纸片便签 + 折叠贴边的胶囊 + 悬停即时预览**。功能面比 TO-DO Panel 更窄，但交互与工程复杂度更高（多屏贴边、DirectComposition 队列、插件系统）。

---

## 2. 技术栈与工程约束（`PaperTodo.csproj` 实测）

| 项 | 值 |
| --- | --- |
| 目标框架 | `net10.0-windows10.0.17763.0`（要求 Win10 1809+） |
| UI | `UseWPF` + `UseWindowsForms`（后者只为 AvalonEdit 的光标依赖） |
| 关键开关 | `AllowUnsafeBlocks`、`CETCompat=false`（规避旧系统 User Shadow Stack 启动崩溃）、`Nullable=enable` |
| DPI | `app.manifest` 用 PerMonitorV2，WinForms 告警 `WFO0003` 忽略 |
| 版本 | `4.0.0-beta2` |
| 输出 | `OutputType=WinExe`，`AppendTargetFrameworkToOutputPath=false` |

依赖与实际用途：

| 包 | 版本 | 用途〔源码注释/推断〕 |
| --- | --- | --- |
| AvalonEdit | 6.3.1.120 | 笔记正文的**源文档与渲染器** |
| Markdig | 1.5.1 | **只做语义解析**（csproj 注释明确"Markdig is the semantic parser only"） |
| Fody + Costura.Fody | 6.9.3 / 6.2.0 | 单文件打包与引用裁剪（`DisableFody` 与 SDK 压缩互斥） |
| Microsoft.Extensions.Hosting | 10.0.14 | 通用宿主/DI 骨架 |
| Microsoft.Web.WebView2 | 1.0.4314.35 | 插件正文宿主（`WebPaperBodySession`） |
| ModelContextProtocol | 2.3.1 | 本地 MCP 服务（`--mcp`） |
| Vortice.DirectComposition | 3.8.3 | 胶囊队列的合成层（免逐帧像素拷贝） |
| 原生 LMDB | `native\lmdb\bin\win-x64\papertodo_lmdb.dll` | 笔记图片库；**缺失则 `EnsurePaperTodoLmdbLibrary` 直接报错终止构建** |
| vendor/wpf-notifyicon | 子模块分叉 | 托盘图标（`Hardcodet.Wpf.TaskbarNotification`） |

工程侧值得注意的三点〔源码〕：

1. **测试可达性**：`InternalsVisibleTo` 暴露给 `PaperTodo.DesktopBenchmarks` / `PaperTodo.MaterialBenchmarks`；`tools/PaperTodo.EdgeDiagnostics` 只在 Debug 编译进产品。
2. **对比/备份目录排除**：`DefaultItemExcludes` 明确排除 `对比_StickyMD`、`输出\`、`PaperTodo_v*`、`PaperTodo-backups`、`.codex_decompile_debug`——说明作者长期用"反编译对照 + 快照回滚"方式开发。
3. **`_PaperTodoSdkCompressionEnabled` 与 Fody 互斥**，并为此切换 `IntermediateOutputPath`，避免复用被织入过的中间程序集。

---

## 3. 数据模型

### 3.1 `AppState`（`src/Models.cs:426`）

单一根对象，序列化为 `data.json`。字段按用途分组（节选，均〔源码〕）：

- **内容**：`Papers: List<PaperData>`（`[JsonRequired]`，:432）
- **外观**：`Theme`、`ColorScheme`（`ColorSchemes.Warm`）、`PaperSkin`（null = 旧版迁移哨兵）、`MaterialTransparency`、`MicaBackdropType`、`MicaAlwaysActive`、`UiFontPreset`、`TextRenderingProfile`、`Zoom`
- **文字**：`NoteTextSize/Bold`、`TodoTextSize/Bold`、`TitleTextSize/Bold`、`CapsuleTextSize/Bold`、`TodoVisualSize`
- **行为**：`AutoClearCompletedTodos`（:452）、`AutoMoveCompletedTodosToBottom`（:453）、`AutoCompressLargeImages`
- **折叠/胶囊**：`UseCapsuleMode`（:472，默认 true）、`UseDeepCapsuleMode`（:473）、`DeepCapsuleGapSize`、`UseCapsuleCollapseAll`、`UseCapsuleFreeQueues`、`CompactMasterCapsule`、`ShowDeepCapsuleWhileExpanded`、`HideEdgeCapsuleCloseButtonOnHover`
- **Markdown**：`MarkdownRenderMode`（:446，默认 `basic`）、`ImageReferenceTextMode`、`MarkdownEditAnimationEnabled`
- **待办/笔记联动**：`EnableTodoPaperLinks`、`ShowLinkedPaperName`、`AllowLongLinkedPaperTitles`、`HideLinkedPapersFromCapsules`、`RunLinkedScriptCapsulesOnClick`
- **窗口策略**：`HidePapersFromTaskbar`（默认 true）、`HidePapersFromWindowSwitcher`（默认 true）
- **标题**：`MaxTitleLength`（默认 `PaperTitles.DefaultMaxTitleLength`）

### 3.2 `PaperData`（`src/Models.cs:586-652`）

```csharp
Id, Type(=PaperTypes.Todo), Title,
X, Y, Width, Height,            // 桌面几何（DIP）
IsVisible, AlwaysOnTop, IsCollapsed, TextZoom,
BodyProviderId(="markdown"),
CapsuleSide, CapsuleMonitorDeviceName, CapsuleQueueId,   // 胶囊归属三元组
DeepCapsuleExpandedX/Y/Width/Height/DpiScale/Side/MonitorDeviceName,
Items: List<PaperItem>,         // 待办项
Content: string                 // 笔记原始 Markdown
```

关键设计〔源码注释〕：**胶囊归属用 `(CapsuleMonitorDeviceName, CapsuleSide)` 二元组标识一个队列，`CapsuleQueueId` 支持一边多队列；空 `CapsuleSide` 表示"尚未分配"，加载时继承旧版全局锚点**（:620-628）。`DeepCapsuleExpanded*` 记录"从胶囊展开时的展开矩形 + DPI 缩放"，注释强调旧值仍是 DIP、必须靠 HWND 缩放判定物理矩形（:642-645）。

### 3.3 `PaperItem`（`src/Models.cs:654-678`）

```csharp
Id(string "N"), Text, Done, Order,
LinkedPaperId | LinkedPath(+LinkedPathIsDirectory),   // 快捷跳转，互斥
ReminderAt(DateTimeOffset?), ReminderTriggered(bool)
```

**一个 `PaperData` 同时承载待办项与笔记正文**，靠 `Type` 决定渲染分支——不是两张表。这是"纸片"模型的直接后果，对我们"三个分区"的模型是反例（见 §10）。

---

## 4. 持久化与崩溃安全（最值得直接借用的一层）

### 4.1 路径约定（`src/WorkspaceModels.cs:53-71`）

| 文件 | 说明 |
| --- | --- |
| `data.json` | 主状态（1 号槽位无后缀） |
| `data.json.tmp` | 原子写临时文件，崩溃后按"orphaned"清理（`StateStore.cs:237-256`） |
| `data.backup.json` | 固定备份（可由主文件刷新，走同一原子写 `StateStore.cs:403-427`） |
| `note-assets.lmdb` | 笔记图片库（原生 LMDB） |
| `plugins/data{N}` | 插件数据 |
| `PaperTodo-backups/` | 历史快照 ZIP |

目录基址 = `AppContext.BaseDirectory`（`StateStore.cs:34`）——**程序目录即数据目录，绿色免安装**（README 亦如此声明）。

### 4.2 自动保存

- 空闲去抖 **1000 ms**，另有 **10 s 强制上限**（注释原文："Idle debounce: save ~1s after the last change. Force cap: while edits keep resetting the idle timer, still flush at least every 10s."，`src/AppController.cs:180-199`）〔源码〕
- `MarkDirty()` 重置空闲计时，`SaveNow()` 立即落盘；提醒变更走 `NotifyTodoReminderChanged(saveImmediately)`

### 4.3 原子写（`src/DurableAtomicFileWriter.cs`）

五阶段可注入故障点：`BeforeTempOpen / AfterTempWrite / AfterFlush / BeforeReplace / AfterReplace`（:6-13）。流程（:73-133）：

1. `Directory.CreateDirectory` 目标目录
2. 写 `target + ".tmp"`，`FileMode.Create` + `FileShare.None` + 16 KiB 缓冲 + `SequentialScan`
3. `stream.Flush(flushToDisk: true)` ← **真正刷盘，不是只刷托管缓冲**
4. 可选 `validateTemp(tempPath)` 校验，失败即删临时文件并抛出
5. `File.Move(temp, target, overwrite: true)`，失败按 `IOException`/`UnauthorizedAccessException` **重试 5 次、间隔 100 ms**（:135-155），与历史快照读取共用 `SnapshotReadReplaceGate` 锁避免"目标被打开导致替换失败"
6. 任何异常都尽力删除残留 `.tmp`（:160-170）

### 4.4 读取与容错（`src/StateStore.WriteSafety.cs`）

- 结构校验：根必须是 object 且 `papers` 必须是 **object 数组**（:40-49），否则抛 `JsonException`
- 编码探测：UTF-32 LE/BE → UTF-16 LE/BE → 严格 UTF-8（禁用替换字符）（:15-38）；注释说明"先测 UTF-32，因为它的 LE BOM 以 UTF-16 LE BOM 开头"
- 宽松读：允许注释 `JsonCommentHandling.Skip` 与尾逗号（:109-118）
- 外部改动检测：`ReadPrimaryHash()` 对文件做 SHA-256，与预期不符即抛 `ExternalDocumentChangedException`（:53-79）；注释明确"仅显式 merge/reload 与工作区目录事务做源校验，普通自动保存不比对"（:51-52）

### 4.5 历史快照（`src/CoreHistoryBackup.cs`）

- 目录：`PaperTodo-backups/`（:36）
- 命名：`PaperTodo-core-<yyyyMMdd-HHmmss-fffffff>.zip`（:19-20）
- 份数上限 `CopiesToKeep = 4`（:16），最小间隔 `MinimumInterval = 10 min`（:21）
- 分层保留：`LongHistoryTarget = 30 天`（替换底线 21 天）、`MediumHistoryTarget = 3 天`、`ShortHistoryTarget = 6 小时`（:22-25）
- 每个归档是 ZIP，内含对应槽位的 `data{N}.json`；读取时**只解出请求的槽位**，不碰其他槽（:41-44）

### 4.6 写入停机信号

`PersistenceWriteLifetime`（:93-107）：进程退出/崩溃边界上 `Stop()` 置位，之后任何排队写入 `ThrowIfStopped()`；注释强调"Stop 从不等待 I/O、也不分发 UI 工作，因此在崩溃边界安全；已进入内核的替换可以完成，队列里的写一个都不许开始"。

### 4.7 多工作区（我们不需要，但机制值得知道）

槽位 1..5 → `data2.json`…`data5.json`；`WorkspaceFieldScopes` 注册表把 `AppState` 每个可写属性强制归类为 `FollowMain / LocalContent / LocalLayout / MainCatalog / LegacyMigration`，并有校验保证"新加属性必须显式归类，默认 LOCAL 不继承"（`src/WorkspaceModels.cs:78-98`）〔源码〕。

---

## 5. 待办事项的实现

### 5.1 三条硬不变量〔源码〕

1. **`Order` 连续**：`NormalizeOrders` 每次变更后重排为 0..n-1（`src/TodoRules.cs:46-52`）
2. **列表永不为空**：`RemoveItems` 删空后插入一个空占位项（:54-65）——保证 UI 永远有一个可输入的"幽灵行"
3. **占位项判定唯一**：`IsPlaceholder = !HasMeaningfulContent`，而"有意义"= 有文本 ∨ 已完成 ∨ 有提醒 ∨ 有链接（:9-24）

### 5.2 排序

- `ApplyCompletedOrdering`：未完成在前、已完成在后（:120-143）
- `ApplyDoneTransitionOrdering`：勾选时把该项移到末尾；取消勾选时插到"第一个已完成项之前"（:155-198）——即未完成区的末尾
- 两者都先比对顺序是否真变，未变则返回 false，避免无意义重排与脏标记

### 5.3 完成策略（`src/TodoRules.cs:200-224`）

```csharp
if (done && autoClearCompleted) {
    RemoveItems(items, item => changed.Contains(item.Id) && item.Done);
    return true;                    // 勾选瞬间直接删，无延迟、无归档、无导出
}
return ApplyDoneTransitionOrdering(items, changedItemIds, done, autoMoveCompletedToBottom);
```

- 默认值：`AutoClearCompletedTodos = false`、`AutoMoveCompletedTodosToBottom = false`（`src/Models.cs:452-453`）→ **默认"完成就留在原地，什么也不发生"**
- 设置联动：开启"自动清除已完成"时，"已完成置底"开关置灰（`src/AppController.Settings.NotesAndTodosPage.cs:216`）
- `SetDone(item, true)` 会顺带清空 `ReminderAt` / `ReminderTriggered` 并返回"是否移除了一条提醒"（`src/TodoRules.cs:146-153`）
- 改变完成的入口有 5 处走同一策略：纸片本体（`PaperWindow.TodoLinksAndSelection.cs:742`）、预览卡（`PaperWindow.EdgeCapsulePreviewContent.cs:58`）、命令服务（`PaperCommandService.cs:309`）、MCP（`McpCommandService.cs:338`）、检查框路径（`PaperWindow.Todo.cs:737`）

**结论（对需求很关键）**：PaperTodo 的"自动清除"是**无留底删除**——`RemoveItems` 之后该条文本只可能还活在撤销栈（`MaxUndoDepth = 100`，`src/PaperWindow.cs:102`）里。这与用户"完成后要 Markdown 留底"的要求**方向相反**，不能照搬；我们只能借用它的"策略函数收口"结构。

### 5.4 撤销

`MaxUndoDepth = 100`（`src/PaperWindow.cs:102`），待办与笔记共用同一撤销栈，超限从底部丢弃（:704、:2468-2490）。

---

## 6. 边缘胶囊（"贴边小悬浮球"的成熟参考）

这一节是我们 D3（点击隐藏 → 贴边悬浮球 → 点击唤出）最直接的实现蓝本。

### 6.1 几何与动效常量

`src/EdgeCapsuleLayout.cs`：

| 常量（`EdgeCapsuleLayout.cs` 行号） | 值 | 含义 |
| --- | --- | --- |
| `ExpandedEdgeInset`（:18） | 36 | 从胶囊展开的大窗口不贴死边缘的留白 |
| `TopMargin`（:20） | 8 | 工作区上下呼吸边距 |
| `StartTopMargin`（:22） | 48 | 队列第 0 个槽位的起始高度（上方留出够手到达的空间） |
| `CornerRadius`（:24） | 12 | 胶囊圆角（与主胶囊共用） |
| `WindowChromeMargin`（:27） | 8 | 窗体外透明边距；贴墙侧不留、内侧留阴影 |
| `CapsuleCloseWidth`（:28） | 14 | 悬停时出现的关闭段宽度 |
| `HorizontalResizeMilliseconds`（:39） | 160 | 展开/收起横向补间 |
| `SlotMoveMilliseconds`（:40） | 200 | 队列内槽位换位 |
| `SlotRetractMoveMilliseconds`（:42） | 120 | 退出队列时朝主胶囊快速收回 |
| `DockingHandoffMilliseconds`（:45） / `DockingRevealMilliseconds`（:46） | 160 / 80 | 拖拽落位交接与揭幕 |

另有 `CrossQueueDragUnlockDistance = 56`（:34，跨队列拖拽解锁距离）、`OutlineThickness = 2`（:32）、`OutlineOverlap = 1`（焦点描边轮廓，见 :36 的 `OutlineSilhouetteInset` 计算）。

胶囊本体尺寸：`92 × 46` DIP（含阴影边距，`src/Models.cs:18-19`）；`CapsuleNormalMinWidth = 76`、左右内边距各 6、图标间距 4、关闭段 21（`src/PaperWindow.cs:173-178`）；展开时胶囊淡出 `ExpandCapsuleFadeOutMilliseconds = 80`（:165）。

### 6.2 状态机（`src/EdgeCapsuleModel.cs`）

正交拆成四个维度，而不是一堆布尔：

- `EdgeCapsuleSlotState`：`None / CollapsedDocked / ExpandedReserved / RetractedCollapsed / RetractedExpanded / RetractingCollapsed / RetractingExpanded`（:3-12）
- `EdgeCapsuleVisualState`：`Resting / Hovered / Active`（:14-19）
- `EdgeCapsuleGestureState`：`Idle / PendingClick / DockedReordering / FloatingTransfer / FloatingReordering / DockingHandoff / DockingReveal`（:27-40）
- 另加 `EdgeCapsulePreviewState`（Closed/Open）、`EdgeCapsuleOpenOrigin`（Normal/EdgeSlot）、`EdgeCapsulePaperForm`（Collapsed/Expanded）

**意图 reducer**：上层只描述"发生了什么"（`AttachQueue / ChangeQueuePlacement / ChangePaperForm / BeginRetraction / DetachQueue / SamplePointer / ChangePreview / BeginPointer / LoseCapture / BeginDockedReorder / MoveDragPointer / FinishPointer / …`，:142-200），由 reducer 原子推导 Slot/Visual/Gesture/Placement。注释原文："Callers describe what happened; the reducer derives Slot/Visual/Gesture/Placement atomically instead of accepting field setters."（:139-141）

**脏标记位域**：`EdgeCapsuleDirty { Presentation, Measure, Pointer, Frame, ApplyRetry, DisplayMetrics }`（:73-87），注释解释 `ApplyRetry` 用于 Windows 正在完成 per-monitor DPI 或显示拓扑交接时的瞬时失败重试，`DisplayMetrics` 则把整批显示几何推迟到手势结束（因为无法从上一次拖拽快照重放）。

### 6.3 贴边"探出/收回"

`src/ExperimentalCapsuleFollowGeometry.cs:24-25`：`CapsulePeekWidthDip = 26`、`CapsulePeekHeightDip = 12`——完全收起时只留一小段"耳朵"露出边缘；`CapsuleFollowMode` 三态：`Revealed / RetractedBehindTarget / RetractedAtScreenEdge`（:3-7）。跟随滑出 `130 ms`、收回延迟 `180 ms`（`src/PaperWindow.ExperimentalCapsuleFollow.cs:8-9`）。

### 6.4 悬停预览与意图预测（`src/AppController.EdgeCapsulePreview.cs`）

- 位移驻留 `32 ms`、拖拽后驻留 `140 ms`、拖后稳定判定 `48 ms`（:10-12）
- 指针容差 `2 DIP`（:13），走廊跟踪采样 `24 ms`（:14），激活意图采样 `16 ms`（:15）
- 光标读取失败重试 1 次（:18）
- 预览卡内可直接勾选/撤销待办、滚动查看笔记（README）

### 6.5 队列与主胶囊

- 一屏一边 = 一队列；`UseCapsuleCollapseAll` + `MasterCapsuleWindow` 收拢整列入口；主胶囊可拖动以调整队列起始高度〔README〕+〔源码〕
- 胶囊之间"顺滑接续"靠意图预测：在相邻胶囊间连续滑动时平滑切换预览目标〔README〕
- 队列合成走 DirectComposition（`Vortice.DirectComposition` + `EdgeCapsuleQueueCompositionProxy`），注释说明"每帧不拷贝像素、不依赖 Windows App SDK；无法建面时回落到普通 HWND 路径"〔源码注释〕
- 代理窗口有 `WS_EX_NOACTIVATE` 类处理与自定义 `WndProc`（`src/EdgeCapsuleQueueProxyWindow.cs:124-185`、`src/WindowNative.cs:145`）〔源码〕

> 对我们的价值：**形态逻辑（贴边 + 小尺寸 + 探出一段 + 点击/悬停展开）可以直接照抄思路，工程实现（DirectComposition 队列、多胶囊接续、拖拽换队列）远超我们需要。**

---

## 7. 笔记与 Markdown

- 渲染档位：`MarkdownRenderModes { Off = "off", Basic = "basic", Full = "full" }`，默认 `basic`（`src/Models.cs:28-45`、:446）〔源码〕；README 称"三档实时渲染"〔README〕
- 架构：**AvalonEdit 持有源文档并负责渲染，Markdig 只做语义解析**（csproj 注释）——不是"Markdown → HTML → WebView"，这是它能做到"编辑与浏览自然融合"的原因〔源码注释〕+〔推断〕
- 局部增量：`MarkdownSemanticPresentation*`（10 个文件）+ `MarkdownSemanticSnapshot*`（6 个文件）构成"只重解析改动的段落"，README 称"长笔记增量渲染、杜绝输入法卡顿"〔源码结构〕+〔README〕
- 图片：`note-assets.lmdb`（`NoteImageStore.cs:43`，60 KB 源码），图片引用文本三档显示 `always / editing / hidden`（`Models.cs:47-58`）
- 导出：
  - 单篇 `.md` 或全部打包 `.zip`（`NotePortableExport.cs:20-45`；`PaperWindow.NoteExport.cs:69-71` 的文件对话框过滤器）
  - HTML 导出（`NoteHtmlExport.cs`）
  - 图片导出时复制到同名资源目录，失败回滚已建目录与文件（`NotePortableExport.cs:46-81`）
- **待办 → Markdown 的转义规则**（`src/TodoClipboardFormatter.cs:9-89`，可直接沿用）：
  - 每项前缀 `- [ ] ` / `- [x] `
  - 项内换行 → 下一行起 **4 空格**缩进（续行）
  - 恒转义字符：`\` `` ` `` `*` `_` `{` `}` `[` `]` `<` `>` `~` `|` `&`
  - 行首首字符为 `#` `>` `+` `-` 时再转义（避免变成标题/引用/列表）
  - 形如 `1.` / `1)` 且后接空白的**有序列表分隔符**也转义

> 注：`NotePortableExport` 的"导出"是**用户手点**的动作（文件对话框 + 目标路径），仓库内**没有**任何"保存时自动写 .md"的代码路径〔源码全仓 grep `.md` 仅命中导出相关〕。

---

## 8. 提醒 / 托盘 / 热键 / 单实例 / 自启

| 能力 | 实现〔源码〕 |
| --- | --- |
| 待办提醒 | 受实验开关 `ExperimentalTodoReminders` 控制，**默认关闭**（`Models.cs:534` 为 `public bool ExperimentalTodoReminders { get; set; }`，无初始化器 → false）〔源码〕；`TodoReminderPolicy.IsPending = !Done && !ReminderTriggered && ReminderAt.HasValue`（`AppController.TodoReminders.cs:581-591`）；定时器区间 `250 ms – 1 min`；到点前 **5 s** 预告（`ReminderAdvanceNotice`）；自定义提示音 ≤ **10 MB**（`TodoReminderMaximumCustomSoundBytes`） |
| 托盘 | `Hardcodet.Wpf.TaskbarNotification.TaskbarIcon`（vendor 分叉，注释说明分叉的原因：接管任务栏弹窗激活/焦点/DPI 换算）；双击召回全部纸片（`AppController.Tray.cs:29-60`） |
| 全局热键 | `RegisterHotKey`（`GlobalHotkeys.cs:892、1028`），注释提到 `1409`（已注册冲突）需要按 owner 事务回滚（:795） |
| 单实例 | `Mutex` + 命名管道命令通道（`SingleInstanceHelper.cs:22-105`），第二实例把命令转交首实例 |
| 开机自启 | 写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run`（`SystemSettingsHelper.cs:9-63`），注释强调这是 Explorer 拥有的未文档化二进制值，首位 DWORD 表示启用状态 |
| 鼠标穿透/置底 | 走 Win32 扩展样式与 `SetWindowPos`（`WindowNative.cs`、`EdgeCapsuleQueueProxyWindow.cs`）〔源码〕 |

---

## 9. 与 TO-DO Panel 的对照

| 维度 | TO-DO Panel v1.3.0 | PaperTodo v4.0.0-beta2 |
| --- | --- | --- |
| 技术栈 | Electron 44 + 原生 JS 渲染层（约 12,900 行） | .NET 10 + WPF（126,831 行 C#） |
| 常驻形态 | 屏幕顶部"刘海"条，点击从顶展开 | 桌面纸片 + 折叠胶囊贴边 + 悬停预览卡 |
| 数据 | `localStorage` 快照镜像成 `workspace.json`；DPAPI 加密码库 | `data.json` + `data.backup.json` + ZIP 历史 + LMDB 图片 |
| 待办完成 | 只改 `done`，**永久留在列表**，只有手动删除（5 s 撤销） | 可选"置底"或"勾选即删"（无留底） |
| Markdown 留底 | 无（全仓无 `.md` 落盘） | 有**手动导出** `.md` / `.zip`，无自动归档 |
| 账号密码 | 有（`safeStorage` 单 blob） | **没有** |
| 随笔 | 草稿 + 200 条硬截断归档 | 笔记纸 + 图片 + 三档渲染 |
| 插件/扩展 | 无 | 有（`plugins/` 热插拔 + MCP） |
| 主要复杂度陷阱 | 顶部形变 + 透明窗口重绘 + 点击穿透轮询 | 贴边队列 DirectComposition + 多屏 DPI 交接 + 插件宿主 |

---

## 10. 对 Win_Tool 的可迁移结论

| 机制 | 建议 | 理由 |
| --- | --- | --- |
| 原子写（tmp → flush(true) → Move(overwrite) → 5×100 ms 重试） | **采纳** | 我们的 `.md` 留底与数据文件同样怕断电/占用，代价极低 |
| 分层历史备份（6 h / 3 d / 30 d，最多 4 份 ZIP） | **采纳（简化）** | 我们只保留"每日 1 份、留 7 份"即可，规则需用户确认（见方案 D2/D7） |
| 待办"策略函数收口"（一处判定，多入口复用） | **采纳** | 勾选入口未来会不止一个，策略必须单点 |
| 完成即清空列表 | **不采纳** | 用户要求"手动清理 + Markdown 留底"，方向相反 |
| 贴边小胶囊（92×46、圆角 12、关闭段 14、探出 26×12） | **采纳（裁剪）** | 直接对应 D3"点击隐藏 → 右下角小悬浮球" |
| 胶囊队列 / 多胶囊接续 / 跨队列拖拽 / 主胶囊 | **不采纳** | 我们只有"一个球"，队列与接续是纯增复杂度 |
| DirectComposition 合成层 | **不采纳** | 单球无需每帧合成；WPF 默认渲染足够 |
| 意图 reducer + 位域脏标记 | **部分采纳** | 只保留 `Expanded / Ball / Hidden` 三态 + 单一 reducer，避免布尔爆炸 |
| AvalonEdit + Markdig（Markdig 只做语义） | **采纳（可选）** | 若做"随笔实时渲染"这是最省心的原生路线；纯文本框也能满足需求，属选型问题 |
| 待办 → Markdown 转义规则 | **采纳** | 已给出可直接照抄的字符白名单与续行缩进规则 |
| 托盘 + 全局热键 + 单实例 + StartupApproved 自启 | **采纳** | Windows 常驻小工具的标准骨架，且自启写法比裸写 `Run` 键更可靠 |
| 多工作区（5 槽 + 字段作用域登记） | **不采纳** | 三类功能量级不需要 |
| 插件系统 / MCP / WebView2 脚本胶囊 | **不采纳** | 明确在功能范围外 |

---

## 11. 未证实项清单

1. 胶囊/纸片在 Windows 实机的观感、混合 DPI 下的清晰度、120 Hz 以上帧率是否达标〔未证实〕
2. DirectComposition 回落到普通 HWND 路径的实际触发条件与观感差异〔未证实〕
3. LMDB 图片库的并发安全边界与损坏恢复策略〔未证实〕
4. 托盘图标在 Windows 11 折叠区（溢出菜单）中的行为〔未证实〕
5. 撤销栈跨纸片/跨类型是否共享同一份快照对象（只确认了深度常量与两处裁剪点）〔未证实〕
6. `doc/AGENTS.md` 内容（解压失败）——可能含作者对架构的权威说明〔未证实〕
