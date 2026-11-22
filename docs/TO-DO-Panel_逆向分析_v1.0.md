# TO-DO Panel 逆向分析（v1.3.0）

> 目的：为自研「待办 / 账号密码 / 随笔」精简版 Windows 工具提供可信的实现依据。
> 分析对象：https://xiaopu-ai.github.io/TO-DO-Panel/ 对应仓库源码树（v1.3.0）。
> 标注体系：〔源码〕= 直接读到代码/配置的事实；〔文档〕= 仓库 README/ADR 的声明；〔推断〕= 由代码结构推出的结论，未经运行验证；〔平台机制〕= Electron/Windows 的既有行为常识。

---

## 0. 一句话结论

它是一套**单进程 Electron 应用**：主进程只负责「原生窗口 + 文件 + 系统能力」，界面全部在渲染层的两个页面里（常驻「岛」`island.html` 与主面板 `index.html`）；**业务数据没有数据库**，唯一写入方是渲染层的 `localStorage`，主进程把整份 localStorage 快照镜像成 `workspace.json`。屏幕顶部那条「刘海」不是 Web 组件，而是**一个透明无边框置顶的原生窗口**，靠 `setBounds` 在两套尺寸间瞬切、靠 CSS 做形变。

参考项目**没有**实现你要求的「待办完成后自动清理 + Markdown 留底」——它只把 `done` 置位，条目永久留在列表里（见 §7.3）。这部分是我们必须新增的，不是照搬。

---

## 1. 取证方式与边界

| 项 | 值 |
|---|---|
| 源码树 digest | `sha256:fc5b6ba49f297d426c94d7911d18b39dc71049e2052f94fe41742dd53d15f0c7` |
| 样本路径 | `.rea/TO-DO-Panel-main`（取证材料不随公开仓库分发） |
| 版本 | `package.json` → `to-do-panel@1.3.0` |
| REA 语义图 | `analyze_javascript_application` → parent `ev_1a7b074e6119c77f55dacbac8690c30a483496bb7b478695cbe6db0ba27cd54e`，summary `ev_df5f9e1ed6741935fb778f0f7718223ffd45961db638a10c50d21d33b263a4e1`，graph `jsrg_9828b10f6c616593ff2ead61596d3bbd5e19beb3274191e6b2393ae966332e27` |
| 图规模 | 12 个文件被静态解析，76,846 AST 节点，55.27 万字节文本，53,970 语义节点 / 54,591 关系，399 条 finding，0 解析失败 |
| 证据包 | `.rea/tdp-evidence.json`（97 MB，含完整语义图；不随公开仓库分发） |

已证与未证的边界：

- 〔事实〕源码树是从 `codeload.github.com` 经 `gh-proxy.com` 镜像取得的 v1.3.0 tar.gz，24,843,401 字节，解压完整。
- 〔事实〕`package.json` 中 `build.asar = false`，即打包后不生成 `app.asar`，安装目录里就是**明文源码文件**。
- 〔推断〕因此安装包内的 JS 与源码树内容一致——但**未做逐字节比对**：`setup.exe`（116,354,602 B，NSIS）没有解开，本机无 7z/7za，npm registry 不可达，装不了解包器。若要闭环，可在有 7z 的机器上 `7z l setup.exe`→取 `$PLUGINSPACE/app-64.7z` 比对 digest。
- 〔事实〕REA 的 MCP 进程在 Windows 上不可用于 JS 分析：`capability_unavailable`，原因 `REA Windows native self-test failed: Create private object relative to its admitted parent failed [...Temp\rea-native-probe-…] (Win32 5): 拒绝访问`。同一台机器上 shell 自己写 Temp 是成功的，属该进程令牌受限，非目标问题。
- 〔事实〕改用 REA CLI 后成功：`rea analyze-javascript-application <dir> --detail summary --evidence_output …`（6.8.0），全部 12 个源文件解析成功。

---

## 2. 进程与窗口拓扑

〔源码〕主进程共创建三个 `BrowserWindow`，都从 `loadFile` 加载本地 HTML，不加载远程页面。

| 窗口 | 创建位置 | 角色 | 关键参数 |
|---|---|---|---|
| `islandWindow` | `main.js:889 createIslandWindow()` | 常驻顶部「岛」：AI 用量耳朵、任务完成/待办到期提醒、悬停明细 | `frame:false, transparent:true, backgroundColor:'#00000000', resizable:false, movable:false, focusable:false, alwaysOnTop:true, skipTaskbar:true, hasShadow:false, fullscreenable:false, roundedCorners:false, show:false`，`webPreferences: { contextIsolation:true, nodeIntegration:false, sandbox:true, backgroundThrottling:false }` |
| `mainWindow` | `main.js:1237` | 主面板（六个 Tab + 设置），折叠时就是那条 200×38 的贴顶条 | 同上的透明/无边框/置顶/不进任务栏；注释明确「必须显式给透明底色，只写 `transparent` 时 BrowserWindow 仍保留不透明默认底色」 |
| `permissionsWindow` | `main.js:2101` | 首次运行的权限/欢迎引导 | `alwaysOnTop:true`，加载 `renderer/permissions.html` |

〔源码〕三处都是 `setAlwaysOnTop(true, 'screen-saver')`（岛为 `('screen-saver', 1)`）；`darwin` 额外 `setVisibleOnAllWorkspaces(true,{visibleOnFullScreen:true})`（`main.js:923`、`main.js:1271`）。

〔源码〕`main.js:190` 的注释记录了一条实测结论：macOS 菜单栏会拦截其高度带内的点击，但 `'screen-saver'` 级别的窗口不被拦截，折叠条仍可点。这是它能贴在菜单栏/刘海位置还点得到的原因。

〔源码〕`main.js:75`：`app.setPath('userData', app.commandLine.getSwitchValue('user-data-dir') || LEGACY_USER_DATA_PATH)`，其中 `LEGACY_USER_DATA_PATH = path.join(app.getPath('appData'), 'Dynamic Panel')`——appId 已改成 `com.dynamicpanel.app`，但数据目录**沿用旧名**，为了不丢历史数据。

启动流程（`main.js:4000` 附近）：`createTray()` → 主面板 + 岛 → 单实例锁 → 托盘菜单随状态刷新。

---

## 3. 顶部条（"留海"）是怎么做出来的

### 3.1 尺寸与坐标：全部写死的常量

〔源码〕`main.js:186-253` 与 `platform.js: panelBounds()`：

| 常量 | 值 | 说明 |
|---|---|---|
| `COLLAPSED_WIDTH` | 200 | 折叠条宽（win32 由 `panelBounds` 的 `Math.min(200, area.width)` 决定） |
| `COLLAPSED_MIN_HEIGHT` | 38 | 折叠条高；macOS 用 `menuBarHeight`（≈物理刘海高），win32 直接用 38 |
| `EXPANDED_WIDTH` | 1240 | 展开面板宽 |
| `EXPANDED_PANEL_HEIGHT` | 540 | 展开面板内容高 |
| `EXPANDED_CHROME_Y` | 76 | 顶栏 chrome 高度（8 padding + 40 topbar + 12 margin + 16 bottom） |
| `SCREEN_MARGIN` | 24 | 宽度超屏时的两侧安全边 |
| `ISLAND_CANVAS_WIDTH` | 380 | 岛窗口画布宽度（含两侧透明区） |
| `ISLAND_EAR_WIDTH` | 62 | 岛两侧「耳朵」宽 |
| `ISLAND_ALERT_BODY_HEIGHT` | 64 | 提醒从刘海向下长出的高度 |
| `ISLAND_PEEK_MAX_HEIGHT` | 300 | 悬停明细最大高度 |
| `COLLAPSE_WATCHDOG_MS` | 650 | 折叠兜底定时器 |
| `ISLAND_HOVER_POLL_MS` | 60 | 悬停轮询间隔 |

〔源码〕`platform.js: panelBounds(platform, display, expanded)`：

```js
const area  = platform === 'win32' ? display.workArea : display.bounds;
const strip = platform === 'win32' ? 38 : Math.max(0, display.workArea.y - display.bounds.y) || 38;
const width  = expanded ? Math.max(1, Math.min(1240, display.workArea.width - (platform === 'win32' ? 48 : 24))) : Math.min(200, area.width);
const height = expanded ? Math.min(616, Math.max(strip, area.height - 24)) : strip;
return { x: Math.round(area.x + (area.width - width) / 2), y: area.y, width, height };
```

要点：

- **Windows 用 `workArea` 而不是 `bounds`**，所以任务栏在顶部时折叠条会落在任务栏下方、不会盖住它；macOS 用 `bounds`，高度取菜单栏高，正好等于物理刘海。
- `y = area.y`，即**贴着工作区最上沿**；宽度居中。
- 折叠 200×38、展开最多 1240×616，且被工作区尺寸 clamp。

### 3.2 展开/折叠：原生窗口瞬切 + 渲染层做形变

〔源码〕`main.js:403` 注释与 `applyMode()`：

- **一律 `setBounds` 瞬时切换**，不用系统窗口动画：注释写明「系统动画 resize 会持续重绘 web 内容（卡顿）」。原生窗口只提供一块透明画布，用户看到的「从小条长成面板」由渲染层 CSS 负责。
- 状态机只有 `collapsed` / `expanded` 两态（`currentMode`），六个 Tab **共用同一展开尺寸**，切 Tab 不改窗口边界，避免宽高跳变。
- 折叠是双向事务：渲染层动画结束 → `window:begin-collapse` → 主进程置 `setIgnoreMouseEvents(true)` 并起 650 ms watchdog；watchdog 到点仍处于 expanded 就强制 `applyMode('collapsed')`（防渲染层崩掉卡在展开态）。`collapseGeneration` 递增用于作废过期回调。

### 3.3 点击穿透：透明区不吃鼠标

〔源码〕`main.js:801-830`、`syncIsland()`：

- live 形态下窗口比可见的「刘海 + 耳朵」宽（画布 380），两侧透明区必须放行点击：`setIgnoreMouseEvents(true, { forward: true })`。
- 因为窗口设了 `focusable:false` 且被忽略鼠标，**无法收到 mouseenter**，所以改用 60 ms 定时器轮询 `screen.getCursorScreenPoint()` 与 `islandHotRect` 求交（`pollIslandHover`），命中才 `setIgnoreMouseEvents(false)` 接管。
- `hotRect` 是「真正是岛的那一块」的屏幕坐标；live 形态只算刘海 + 耳朵，展开后是整窗。

### 3.4 岛的形态是推导出来的，不是命令出来的

〔源码〕`main.js:736 getIslandLayout()`：`有提醒 → alert；悬停明细 → peek；有会话 → live；否则 hidden`。
`hidden` 时窗口 `hide()`；三种可见形态**横向位置与宽度恒定，只改高度**——注释说明这是为了消除「窗口换位置」和「页面换布局」不同帧生效导致的耳朵图标重影。

〔源码〕提醒、待办到期、AI 任务完成都复用同一个岛窗口（`enqueueTaskNotification`），不是新开通知窗口；`render-process-gone` 时直接 `destroy()` 让下次重建。

---

## 4. 进程边界与 IPC

〔源码〕`preload.js` / `preload-island.js` / `preload-permissions.js` 用 `contextBridge.exposeInMainWorld` 暴露对象（`notchAPI` / `islandAPI`），渲染层 `nodeIntegration:false` + `sandbox:true`，只能走 IPC。

〔源码〕REA 从 AST 里抽出**87 个暴露方法**，通道按域前缀分组（摘录）：

```
window:*      set-mode / metrics / metrics-changed / set-tab / request-collapse / begin-collapse
workspace:*   get / choose / open / load-data / save-data / changed
todos:*       schedule-reminders / reminded / (读取走 workspace 快照)
todo-inbox:*  ready / import / complete
task-notification:* show / hide / queue / activate / dismissed / hover
island:*      layout / peek / usage / open-panel / hover
credentials:* list / get / save / delete-many / copy
clipboard:*   new-entry / paste / write / readImage / deleteImages
recordings:*  save / read / delete / reveal
transcription:* start / audio / event / finish / get-config / set-config
music:*       status / control / choose / changed          (仅 macOS 有实现)
links:*       inspect
windows:*     list / focus                                 (仅 macOS 有实现)
settings:*    get / changed / set-feature / set-auto-launch / set-default-tab / set-shortcut
shell:*       openExternal / openPath / open-privacy-settings
app:*         flush-done / flush-pending / record-shortcut / open-clip / open-api-settings
```

〔源码〕`preload.js:1-8` 有一段值得抄的防御：

```js
function subscribe(channel, handler) {
  ipcRenderer.on(channel, handler);
  return () => ipcRenderer.removeListener(channel, handler);
}
```

注释：所有 `on*` 订阅统一经此注册并回传退订函数，渲染层重新初始化时若不退订，同一条通知会被回调多次。

〔源码〕主进程侧对每个 handler 都做形状校验与长度截断（如 `settings:set-feature`、`todos:schedule-reminders` 里 `String(...).slice(0,160)`、`todos:recent` 直接返回内存数组）。

---

## 5. 数据层：localStorage 是唯一事实源

这是本项目最反直觉、也最值得决策的一项设计。

〔源码〕`main.js:1806` `workspace:save-data`：

```js
const serialized = JSON.stringify(normalizePortableStorage(storage));
if (Buffer.byteLength(serialized) > 8 * 1024 * 1024) return false;   // 整库 8MB 上限
writeJsonFile(destination, { version: 1, updatedAt: Date.now(), localStorage: portableStorage });
```

即：渲染层把**整份 localStorage 的键值快照**（`{ key: stringValue }`）通过 IPC 交给主进程，主进程原样落成 `<workspaceRoot>/workspace.json`。

〔源码〕`renderer/app.js:14-48` 配套的三件事：

1. `collectLocalStorageSnapshot()` 遍历 `localStorage.length` 收集全部键；
2. 给 `Storage.prototype` 的 `setItem/removeItem/clear` **打补丁**置 `workspaceDirty = true`——注释写着「此前每 2 秒无条件把全部键值过 IPC（折叠、空闲时也一样），主进程再整体 parse/stringify 比对」，属于修过的性能问题；
3. `hydratePortableWorkspace()` 启动时读回快照，**只在本地键不存在时**导入（`localStorage.getItem(key) === null`），用 `sessionStorage['notch-workspace-hydrated']` 防重复导入，导入后 `location.reload()` 让编辑器拿到正确初始值。

〔源码〕主进程还有 `workspacePersistenceGate.shouldWrite(payload, destination)` 去重，避免内容没变也落盘；工作区根目录可改（`workspace:choose` + `workspace-settings.json`），默认 `app.getPath('userData')`，且 `workspace:get` 会返回 `portable: workspaceRoot() !== app.getPath('userData')`。

主进程持有的其他文件（`main.js:219-230`、`2804`、`3117`）：

```
<userData>/app-settings.json              应用设置
<userData>/workspace-settings.json        工作区路径
<workspaceRoot>/workspace.json            整份 localStorage 镜像
<workspaceRoot>/todo-inbox/               外部写入的待办投放目录
<workspaceRoot>/todo-inbox/processed/     处理完的归档 + .report.json
<userData>/credentials.vault.json         账号密码保险库（密文）
<userData>/transcription-settings.json    语音转写配置
<workspaceRoot>/clipboard-images/         剪贴板图片
<workspaceRoot>/recordings/               录音
```

〔推断〕代价与风险（对我们的取舍有用）：整库快照 = 每次写入都是 O(全库大小) 的序列化 + 一次 IPC + 一次整文件重写；单键超限或单条巨大文本会连带整库写失败（返回 `false`，渲染层可感知但代码里多为忽略）；两次进程同时改同一工作区没有合并逻辑，只有「键不存在才导入」的粗保护。

---

## 6. 写盘与并发：`writeJsonFile`

〔源码〕`main.js:1369-1390`：`readJsonFile(path, fallback)` 容错读、`writeJsonFile(path, value)` 落盘。全部数据文件都走这一对函数（设置、工作区、保险库、收件箱报告），没有第三方存储库。

〔源码〕待办收件箱（`main.js:1830-1950`）展示了他们处理「外部进程写文件」的完整套路，可直接借鉴：

- 每 2000 ms 扫一次 `todo-inbox/*.json`；
- 文件最后一次改动后**静置 1000 ms** 才读（避开写到一半）；
- 单文件上限 1 MB；
- 读前算 `sha256`，归档前**再算一次**比对：内容变了就不移走（注释：脚本又改写了同名文件，新内容还没导入，不能被当作已处理）；
- 读到后交给渲染层纯函数合并（主进程不碰业务数据），带 token 的 ACK 机制：30 s 无 ACK 就重发，稳定 id 保证不重复添加；60 s 重试；
- 成功后 `rename` 进 `processed/`，同名冲突自动改名，并写 `<名>.report.json` 记录结果。

---

## 7. 待办：模型、完成、清理（关键差异）

### 7.1 数据模型

〔源码〕`renderer/domain.js:createTodo()`：

```js
{ id: String(id || `todo-${Date.now().toString(36)}`),
  text: normalizedText,
  done: false,
  createdAt: Number,
  deadline: new Date(deadlineMs).toISOString(),
  remindedAt: 0 }
```

- `text` 与 `deadline` 都必填，`Date.parse` 失败即返回 `null`（不建条目）。
- `updateTodo()` 保留 `done`；`deadline` 一改就把 `remindedAt` 归零（重新提醒）。

〔源码〕存储键与四个工作流（`renderer/app.js:1-8`）：

```js
const STORAGE_KEY = 'notch-todo-data';
const PRIORITIES = ['P0','P1','P2','P3'];
const TODO_CATEGORY_KEY = 'notch-todo-category-names-v1';
const TODO_CATEGORY_DEFAULTS = { P0:'课程', P1:'自媒体&写作', P2:'Vibe coding', P3:'日常' };
```

数据形状 `{ P0:[todo…], P1:[…], P2:[…], P3:[…] }`。四个「优先级」被产品化为**可改名的工作流**（默认名如上），改的只是显示名。

〔源码〕默认截止时间：没给时间时落在**当天 23:30**（`renderer/app.js:1170`、`domain.js:562`、`domain.js:641` 三处一致，含导入路径）。

### 7.2 排序与计数

〔源码〕`domain.js:364 sortTodosForDisplay()`：`未完成在前` → `deadline 升序`（无效值视为 `+Infinity`）→ `createdAt` → `id` 字符串比较。计数只算未完成（`app.js:365 updateCount`）。

〔源码〕列表每 60 s 刷一次相对时间，但**折叠时不刷**（置 `todoListsStale`，下次展开再刷），且「正在行内改名时不重建 innerHTML」，否则会冲掉输入——`app.js:377-395`。

### 7.3 完成与清理：**参考项目没有自动清理**

〔源码〕`renderer/app.js:449 toggleTodo()` 全文只做：`list[idx].done = !list[idx].done` → `saveData()` → 重渲染 → 勾选时 `flashCheckboxPop` 一个动画。**没有删除、没有归档、没有导出。**

〔源码〕已完成条目仍参与列表渲染，只是加 `.done` 样式（`todoItemHtml` 的 `doneClass`）；移除只能手动：单条 `deleteTodo()` 或勾选后批量删（`todo-bulk`）。

〔源码〕`deleteTodo()` 是**乐观删除 + 5 s 撤销**：先把元素从 DOM 移除、插入 `saveData`，弹出带「撤销」的 toast，回调里按原 index 插回（`showStatusToast(..., {duration:5000})`）。

> **结论**：你要求的「完成后自动清理 + 本地 Markdown 留底」在 v1.3.0 里**不存在**。整个仓库没有任何 `.md` 落盘代码（`grep` 只命中 README、测试夹具、以及渲染层的 Markdown 预览器）。这是我们必须自己设计的新增能力，方案文档 v0.1 里 D2 的待确认项不能靠"参考项目怎么做"来定。

### 7.4 提醒：主进程单定时器链

〔源码〕`main.js:265 TODO_REMINDER_LEAD_MS = 60*60*1000`（提前 1 小时）。

- 渲染层每次 `saveData` 后调 `scheduleTodoReminders(all todos)` 把全量待办推给主进程（主进程只留 `{id,text,deadline,done,remindedAt}`，各字段截断 160 字）。
- 主进程 `scheduleNextTodoReminder()` 遍历，算出最近的到期点只挂**一个** `setTimeout`，到点 `fireTodoReminder()` → 塞进任务通知队列 → 同时 `webContents.send('todo:reminded', {id, deadline, remindedAt})` 让渲染层把 `remindedAt` 写回，保证重启不重复提醒。
- 提醒文案固定「将在 1 小时内截止」。

〔推断〕只在应用运行时有效：没有登录级常驻服务，关掉应用提醒就没了。

---

## 8. 账号密码：safeStorage 单 blob 保险库

〔源码〕`main.js:223 CREDENTIALS_VAULT_FILE = 'credentials.vault.json'`，`main.js:2804-2860`：

```js
if (!safeStorage.isEncryptionAvailable()) return [];              // 读：不可用则返回空
const decoded = safeStorage.decryptString(Buffer.from(envelope.payload, 'base64'));
// 写：
const payload = safeStorage.encryptString(JSON.stringify(rows)).toString('base64');
```

- 文件形态是 `{ payload: "<base64 密文>" }`——**整库一个密文块**，不是逐条加密。
- 门禁：`isEncryptionAvailable()` 为 false 时读返回 `[]`、写返回 `false` 并回 `{ok:false,error:'secure_storage_unavailable'}`，UI 有对应状态。
- 〔平台机制〕Electron `safeStorage` 在 Windows 走 DPAPI（绑定当前用户账户），在 macOS 走 Keychain。因此**换用户/换机即无法解密**，且没有主密码、没有二次认证——能登进这个 Windows 账户的人就能调 `credentials:get` 把明文读走。
- IPC 面：`list / get / save / delete-many / copy`（`copy` 是写入剪贴板）。
- 〔推断〕不足：无逐条密钥、无主密码、无自动锁定、无审计；单 blob 意味着改一条就要重写全库；并发写会互相覆盖。

对我们方案的意义：这正好是方案文档 D1 三候选里「系统凭据库/系统级加密」那一档的**现成参考实现**，但它的信任边界是"Windows 登录会话"，不是"用户主密码"。若你要更强边界（换机可迁移、多设备、可设主密码），需要自己加密（如 libsodium/age 之类）+ 用户口令派生密钥，参考项目没有这块。

---

## 9. 随笔：Markdown 源码 + 手写预览器

〔源码〕`renderer/app.js:1653-1654`：

```js
const NOTE_KEY = 'notch-home-note';           // 首页速记的当前草稿（纯文本）
const NOTE_ARCHIVE_KEY = 'notch-note-archive-v1'; // 归档数组，最多 200 条
```

- 数据源永远是 textarea 里的**原始 Markdown 字符串**；预览只用 DOM API + `textContent` 构建，不引第三方 Markdown 库（`app.js:1650` 注释明说）。
- 安全：`safeMarkdownUrl()` 白名单校验链接协议，`escapeHtml()` 处理用户文本，预览 `innerHTML` 不拼接未转义内容。
- 编辑器防抖保存，`persistNotesEditor()` 里 `notes.slice(0, 200)` 截断归档；`NOTE_ACTIVE_ARCHIVE_KEY` 记录当前编辑的是哪一条，同步回 `NOTE_KEY`。
- 离开/隐藏页面时 `flushNotesEditorSave()` 强制落盘。

〔推断〕局限：200 条上限是硬截断，第 201 条会静默丢弃最旧的；所有笔记塞进一个 localStorage 键，接近单键体积上限时整库保存会失败；**没有 `.md` 文件落地**，也就没有外部可读、可 grep、可用别的编辑器打开的副本——这正是你要补的「随笔方便记笔记 + 本地保存」的短板。

---

## 10. 平台能力开关

〔源码〕`platform.js: capabilities(platform)`：

```js
{ platform,
  unavailableHomeModules: platform === 'darwin' ? [] : ['music','windows'],
  automaticPaste: platform === 'darwin',      // 自动粘贴要靠 osascript 抓「上一个前台应用」
  autoLaunch: platform === 'darwin' || platform === 'win32' }
```

〔源码〕`effectiveHiddenModules()` 还处理了一个迁移坑：Mac 工作区搬到 Windows 后，若「可见模块全是当前平台不可用的」，就强制放开一个可用模块，避免首页空白。

〔源码〕macOS 专属链路靠 `execFile('/usr/bin/osascript')`（REA 图里 5 处子进程节点，`main.js:2514/2681/2780/3929`、`main/notch-geometry.js:63`）——读窗口标题、判断刘海、控制音乐。Windows 上这些全部不work，所以直接不暴露。

〔源码〕开机自启只有一行：`app.setLoginItemSettings({ openAtLogin: enabled, openAsHidden: false })`（`main.js:1348`），Windows/macOS 通用。

〔源码〕快捷键 `setPanelShortcut()`：`globalShortcut.register(shortcut, …)`，特殊值 `'Space'` 走「悬停即展开」的另一条路径而非全局热键；校验器只允许 `modifiers + [A-Z0-9|F1-F24|Space|Tab|Escape|方向键|Home|End|PageUp/Down|Backspace|Delete|Enter]`。

〔源码〕托盘 `createTray()`：`new Tray(createNotchTrayIcon())` + `setContextMenu`，点击托盘在窗口不可见时 `repositionWindow(getTargetDisplay())` 再 `show()`；菜单随状态 `refreshTrayMenu()` 刷新。

〔源码〕AI 通知入口是一条本机 HTTP 服务：`127.0.0.1:43821`，路径 `/notify/<source>`，来源白名单 `{codex, gpt, claude}`，其余 404（`main.js:256-266`）；配套脚本在 `scripts/codex-notify.js`、`claude-notify.js`、`claude-statusline.js`。

---

## 11. 代码规模与分层

〔源码〕字节数（v1.3.0）：

| 文件 | 大小 | 角色 |
|---|---|---|
| `main.js` | 151,875 B | 主进程单体：窗口/几何/托盘/快捷键/IPC/文件/保险库/转写/通知服务 |
| `renderer/workspace.js` | 124,100 B | 主面板业务 UI（待办/笔记/剪贴/链接/录音/密钥各页） |
| `renderer/app.js` | 149,898 B | 面板壳、Tab、岛联动、Markdown 预览、设置 |
| `renderer/domain.js` | 50,311 B | **纯函数域层**（UMD：`module.exports` + `window.NotchDomain`，可被主进程与渲染层共用） |
| `renderer/styles.css` | 164,870 B | 全部样式与动效 |
| `main-services.js` | 27,779 B | 服务层（AI/转写等） |
| `preload*.js` | ~6.5 KB ×3 | contextBridge 白名单 |
| `platform.js` | ~1.5 KB | 平台策略（尺寸/能力开关） |
| `main/notch-geometry.js` `permissions.js` `music-players.js` `agent-usage.js` | 3–18 KB | 刘海几何、权限、音乐、AI 用量 |

〔事实〕ADR 0001 记录：曾尝试用 Swift 6 / SwiftUI + AppKit 重写 macOS 外壳（`native/`），2026-08-25 被推翻并移除，理由是原生壳只实现了折叠态与紧凑提醒，真正的产品界面是约 12,900 行 Electron 渲染层——**外壳原生化的收益抵不过双栈维护成本**。这条对「我们要不要用 Tauri 重写」有直接参考价值（见 §12）。

〔推断〕分层其实是「一个主进程单体 + 一个大渲染层 + 一个纯函数域层 + 一个平台策略模块」。真正带来可测试性的是 `domain.js`（`tests/domain.test.js` 直接 require 它）与 `platform.js`；`main.js` 里窗口几何与业务混在一起，是后期加功能最痛的地方。

---

## 12. 对我们这个精简工具的可用结论

### 12.1 可直接借鉴（机制级，与参考项目的多余功能无关）

1. **顶部条 = 透明无边框置顶窗口 + `setBounds` 瞬切 + CSS 形变**：Windows 上 `y = workArea.y`、折叠 200×38、展开 1240×616 且 clamp 到工作区；这一套数值已经过它自己踩坑验证。
2. **透明区点击穿透必须配 `setIgnoreMouseEvents(true,{forward:true})` + 定时轮询光标**（因为 `focusable:false` 收不到 mouseenter），轮询 60 ms 够用。
3. **折叠要有 watchdog 兜底**（650 ms），否则渲染层异常会永久卡在展开态。
4. **`contextBridge` + 通道前缀分组 + `subscribe()` 返退订函数**，以及主进程 handler 全部做形状校验与字段截断。
5. **外部文件导入的幂等套路**：静置窗口 + 读前读后两次 sha256 + ACK 超时重发 + 归档到 `processed/` + 结果报告。
6. **提醒用"单定时器链 + remindedAt 去重 + 回写渲染层"**，重启不重复提醒。
7. **删除用乐观更新 + 5 s 撤销 toast**，比二次确认弹窗体验好。
8. **托盘 + 全局快捷键 + 置顶条三套唤出方式共存**，不是二选一。

### 12.2 明确不要抄

- **整份 localStorage 快照镜像成 JSON**：我们的数据量虽小，但「每次写入重写全库」+「单键巨大文本拖死整库」+「无并发合并」是硬伤。待办/随笔这类条目化数据用「一条一文件」或「SQLite」更稳，也更便于你要求的 Markdown 留底。
- **一个 `main.js` 15 万字节**：窗口几何、文件、AI、转写、通知服务全在一个文件里。
- **六个功能域 + 录音 + 剪贴板 + 音乐 + 窗口联动**：这是参考项目"功能偏多"的根源，我们只要三类。
- **`safeStorage` 单 blob 保险库**：可以做 D1 的候选实现，但要知道它的边界只是"Windows 登录会话"，换机不可迁移、无主密码。

### 12.3 与方案文档 v0.1 的对齐情况

| 方案条目 | 参考项目实况 | 处理 |
|---|---|---|
| 不做屏幕中间"留海" | 它做的正是贴顶居中条（200×38 / 展开 1240×616） | 它的**几何数值**可借鉴，但我们的主界面是常规窗口，顶部条只作可选唤出入口 |
| D3 隐藏/唤出（侧边栏/托盘/快捷键） | 三套并存：托盘菜单、`globalShortcut` 可配、悬停即展开 | 三候选都属于"已有人做过"的成熟路径，**仍需你裁决**，不因参考项目如此就视为已定 |
| D2 完成即清理 + Markdown 留底 | **不存在**：`done` 只置位，永不清理，无 `.md` 输出 | 纯新增能力，必须你定参数（清理时机/文件位置/格式/是否留全史） |
| D1 账号密码存储 | `safeStorage` 加密整库 blob → `credentials.vault.json` | 可作为"系统级加密"候选的参考实现；**未定，仍待你裁决** |
| D5 随笔是否落 .md | 不落盘，只在 localStorage（草稿键 + 200 条归档键） | 正是我们要补的差距 |

---

## 13. 未证实项（不得当结论用）

1. 安装包内文件与源码树逐字节一致——**未验证**（未解包 NSIS），仅由 `build.asar=false` 推断。
2. Windows 实机上的观感/兼容性（多显示器 DPI 混合、任务栏位置在左/右、全屏应用遮挡）——只读了代码里的处理分支，未运行验证。代码里对 `screen.getPrimaryDisplay`/`display-metrics-changed` 有监听，但混合 DPI 下的实际表现未知。
3. `'screen-saver'` 置顶级别与 Windows 专注助手/全屏独占游戏的实际交互——未验证。
4. 性能数字（展开耗时、整库保存耗时）——代码注释里有"展开卡顿"的历史描述，但无基准数据。
5. `safeStorage` 在目标机器上是否 `isEncryptionAvailable()` 恒为 true（取决于用户配置/域策略）——未验证，代码对它做了降级分支。
