# CLAUDE.zh.md — DJMAX Random Selector V（中文摘要）

## 项目概述

DJMAX Random Selector V 是一款 Windows 桌面应用，用于在音乐游戏 **DJMAX RESPECT V** 中随机选曲。用户设定过滤条件（按键模式、难度、等级范围、DLC 分类），程序随机选出一首曲目并通过模拟键盘输入自动在游戏中导航到该曲目。

---

## 解决方案结构

```
DjmaxRandomSelectorV.sln
├── DjmaxRandomSelectorV/      # WPF 应用（net6.0-windows，WinExe）
└── Dmrsv.RandomSelector/      # 核心逻辑库（net6.0-windows）
```

### 核心库 `Dmrsv.RandomSelector`

与平台无关的过滤与选曲逻辑。

| 文件 | 作用 |
|---|---|
| `Track.cs` | 不可变记录：曲目元数据 + 谱面数组 + IsPlayable + TitleEn（可空英文标题） |
| `Pattern.cs` | 不可变记录：MusicInfo + 按键数 + 难度 + 等级 |
| `MusicInfo.cs` | 共享元数据（Id、Title、Composer、Category） |
| `BasicFilter.cs` | 基于查询的过滤（按键、难度、分类、等级范围） |
| `AdvancedFilter.cs` | 基于播放列表的过滤 |
| `SelectorWithHistory.cs` | 排除近期已选曲目的随机选择器 |
| `PatternPicker.cs` | 根据等级偏好（最低/最高）选取谱面策略 |
| `Locator.cs` | 通过 Win32 SendInput 在游戏内模拟键盘导航 |
| `TitleComparer.cs` | 标题排序比较器（支持韩文/英文/符号） |
| `Enums/GameLanguage.cs` | 游戏语言枚举：`Korean`（韩语）/ `English`（英语） |

### WPF 应用 `DjmaxRandomSelectorV`

基于 Caliburn.Micro 的 MVVM 应用。

| 文件 | 作用 |
|---|---|
| `Bootstrapper.cs` | IoC 容器（SimpleContainer），应用启动/关闭 |
| `RandomSelector.cs` | 串联 过滤 → 选取 → 导航 流水线 |
| `TrackDB.cs` | 加载管理所有曲目，处理 DLC 所有权 |
| `ExecutionHelper.cs` | Win32 全局热键注册 + WndProc 消息钩子 |
| `FileManager.cs` | JSON 导入/导出 + HTTP 请求 |
| `UpdateManager.cs` | 版本检查，下载 AllTrackList.json + appdata.json |
| `Dmrsv3Configuration.cs` | 用户配置模型（序列化为 Config.json） |
| `SettingViewModel.cs` | 设置面板 ViewModel（DLC、热键、语言等） |

---

## 关键架构概念

### MVVM + Caliburn.Micro

- 视图命名：`FooView.xaml` 自动与 `FooViewModel.cs` 配对。
- 所有 ViewModel 通过反射（`Name.EndsWith("ViewModel")`）自动注册为按需实例。
- 单例服务：`IWindowManager`、`IEventAggregator`、`IFileManager`、`Dmrsv3Configuration`、`TrackDB`。

### 事件聚合器（消息通信）

| 消息 | 方向 | 用途 |
|---|---|---|
| `FilterMessage` | ViewModel → RandomSelector | 过滤条件变更 |
| `SettingMessage` | SettingViewModel → RandomSelector | DLC、延迟、语言等设置变更 |
| `FilterOptionMessage` | ViewModel → RandomSelector | 模式/辅助/等级偏好变更 |
| `PatternMessage` | RandomSelector → HistoryViewModel | 已选谱面（用于历史显示） |

### 选曲流水线

按下热键（默认 **F7**）时：

```
ExecutionHelper.HwndHook()
  └─► RandomSelector.Start()
        ├─ IFilter.Filter(TrackDB.Playable)   → IEnumerable<Pattern>
        ├─ PatternPicker.Pick(patterns)        → 按等级偏好去重/选取
        ├─ SelectorWithHistory.Select(list)    → 随机选择，排除近期曲目
        └─ Locator.Locate(pattern)             → 模拟键盘在游戏内导航
```

Alt + 热键重复**上一次**选曲，无需重新随机。

### Locator（游戏内导航）

`Locator.cs` 通过 `user32.dll SendInput` 模拟键盘操作：

1. 重置音乐光标（同时按下两个 Shift 键）。
2. 按标题首字母键跳转至对应分组。
3. 上下导航到精确曲目。
4. 按 Ctrl + 数字键切换按键模式（4B/5B/6B/8B）。
5. 向右导航至正确难度。
6. 可选按 F5 自动开始。

游戏需处于**自由模式**、**All Track** 分类、**按标题 A→Z 排序**。

---

## 多语言兼容性修复

### 真正的问题根源

原始代码存在**两层**语言兼容性问题，均与 `Locator`（游戏内自动导航）相关：

#### 问题一：标题替换

687首曲目中有 54 首含韩文。在韩语模式下，这些曲目以韩文标题显示（如 `비상 ~Stay With Me~`）。切换到**英语模式**后，游戏会将它们替换为英文翻译标题（如 `Stay With Me`）。

`Locator.MakeLocations` 依赖标题首字母计算每首曲目在游戏列表中的位置。若仍用韩文标题（`비` → `#` 分组），但游戏实际显示英文标题（`S` → `s` 分组），位置计算完全错误，导航直接失败。

#### 问题二：排列顺序

- **韩语模式**：非字母起始曲目（韩文）归入 `#` 分组，位于列表**最前**（A–Z 之前）。重置光标（Shift+Shift）后停在列表首部（即 `#` 分组开头）。
- **英语模式**：翻译后的曲目插入对应字母分组，`#` 分组（仅剩真正无法翻译的条目）移至列表**末尾**（A–Z 之后）。重置后光标停在字母分组开头（如 `a`），而非 `#`。

`Dmrsv3AppData.cs` 中已有 `// TODO: title converter for multi-language support` 注释，说明原开发者早已预判此问题。

---

### 修复方案

分两层修复，完全无需维护代码之外的任何手动配置：

#### 第一层：英文标题数据（`appdata.json`）

在 `appdata.json`（从 GitHub 自动下载的数据文件）中新增 `englishTitles` 字典，存储 48 首韩文首字曲目的英文标题：

```json
"englishTitles": {
    "0":   "Stay With Me",
    "48":  "Seollaim",
    "101": "Ask the Wind",
    "246": "Confession, Flower, Wolf",
    ...
}
```

- 新增 DLC 时只需更新 `appdata.json`，代码无需改动。
- 已经以字母起始的混合标题（如 `Eternal Memory ~소녀의 꿈~`、`I want You ~반짝 반짝 Sunshine~`）无需映射，本已正确处理。

代码链路：

```
appdata.json
  └─► Dmrsv3AppData.EnglishTitles (Dictionary<int, string>)
        └─► TrackDB.MergeEnglishTitles()   ← 启动时调用（Bootstrapper.cs）
              └─► Track.TitleEn (string?)  ← 合并到 Track 记录

Locator.MakeLocations():
  getEffectiveTitle(t) = GameLanguage == Korean ? t.Title : (t.TitleEn ?? t.Title)
  → 用有效标题计算分组和位置
```

#### 第二层：排序与导航调整（回退兜底）

对于 `englishTitles` 中未覆盖的曲目，仍需处理排列顺序差异：

**`TitleComparer`**（`GameLanguage` 参数控制）：

```csharp
// 英语模式：非字母起始字符排在 A-Z 之后（优先级 5 > 字母优先级 4）
if (idx == 0)
    return _gameLanguage == GameLanguage.Korean ? 1 : 5;
```

**`Locator.MakeLocations`**（英语模式 `#` 分组强制负索引）：

```csharp
// 英语模式下 '#' 在列表末尾，始终用 "按 'a' 键后向上回绕" 路径导航
if (GameLanguage != GameLanguage.Korean && initial == '#')
    return index - count;
```

---

### 涉及文件（共 13 个）

| 文件 | 变更内容 |
|---|---|
| `Dmrsv.RandomSelector/Enums/GameLanguage.cs` | **新增** `GameLanguage` 枚举（Korean / English） |
| `Dmrsv.RandomSelector/Track.cs` | 新增 `TitleEn` 可空属性 |
| `Dmrsv.RandomSelector/TitleComparer.cs` | 接受 `GameLanguage`；英语模式调整非字母字符排序优先级 |
| `Dmrsv.RandomSelector/Locator.cs` | `GameLanguage` 属性；`getEffectiveTitle` 委托；`#` 分组强制负索引 |
| `DjmaxRandomSelectorV/Dmrsv3AppData.cs` | 新增 `EnglishTitles` 字典，移除 TODO 注释 |
| `DjmaxRandomSelectorV/TrackDB.cs` | 新增 `MergeEnglishTitles()` 方法 |
| `DjmaxRandomSelectorV/Bootstrapper.cs` | 启动时调用 `MergeEnglishTitles` |
| `DjmaxRandomSelectorV/Dmrsv3Configuration.cs` | 持久化 `GameLanguage`（默认 Korean） |
| `DjmaxRandomSelectorV/Messages/SettingMessage.cs` | 携带 `GameLanguage`，Apply 时重建导航位置 |
| `DjmaxRandomSelectorV/RandomSelector.cs` | 初始化及设置变更时同步 `_locator.GameLanguage` |
| `DjmaxRandomSelectorV/ViewModels/SettingViewModel.cs` | `IsEnglishLanguage` 属性，绑定配置读写 |
| `DjmaxRandomSelectorV/Views/SettingView.xaml` | 设置面板新增 **"GAME LANGUAGE (ENGLISH)"** 开关 |
| `DjmaxRandomSelectorV/DMRSV3_Data/appdata.json` | 新增 `englishTitles`，包含 48 首曲目的英文标题 |

### 用户操作

在设置（Settings）面板中找到 **GAME LANGUAGE (ENGLISH)** 开关：

- **关闭**（默认）→ 韩语模式。
- **开启** → 英语模式：启用英文标题导航 + 调整排列顺序。

与游戏内语言设置保持一致即可，无需任何手动配置。

### 维护说明（新 DLC）

当新 DLC 包含韩文首字标题的曲目时：在 `appdata.json` 的 `englishTitles` 字典中添加对应条目（`"trackId": "English Title"`）即可，代码无需改动。

---

## 构建与运行

### 前提条件

- .NET 6 SDK（含 Windows Desktop 工作负载，`net6.0-windows`）
- Windows 操作系统（依赖 WPF 和 Win32 P/Invoke）

### 构建

```sh
dotnet build DjmaxRandomSelectorV.sln
```

### 运行

```sh
dotnet run --project DjmaxRandomSelectorV/DjmaxRandomSelectorV.csproj
```

应用通过命名 `Mutex`（"DjmaxRandomSelectorV"）防止多开。首次启动会从网络下载 `AllTrackList.json` 和 `appdata.json`。

### 发布（单文件可执行）

```sh
dotnet publish DjmaxRandomSelectorV/DjmaxRandomSelectorV.csproj -c Release -r win-x64
```

---

## 开发规范

- **ViewModel 命名**：`<Name>ViewModel`，Caliburn.Micro 自动与 `<Name>View.xaml` 配对。
- **消息类型**：不可变事件用 `record`，需要修改的用 `class`（如 `SettingMessage`）。
- **序列化**：`System.Text.Json`，`JsonSerializerDefaults.Web`（驼峰命名，大小写不敏感）。
- **新增过滤条件**：在 `BasicFilter.cs` 添加属性 → 注册 `IsUpdated = true` → 在 ViewModel 中 `NotifyOfPropertyChange()` → 更新 XAML 绑定。

---

## 外部依赖

| 依赖 | 版本 | 用途 |
|---|---|---|
| Caliburn.Micro | 4.0.212 | MVVM 框架、IoC 容器、事件聚合器 |
| CsvHelper | 30.0.1 | CSV 解析（旧版 AllTrackList.csv） |
| Microsoft.CSharp | 4.7.0 | 动态绑定支持 |

## 远程数据源

| URL | 用途 |
|---|---|
| `https://v-archive.net/db/songs.json` | 完整曲目/谱面数据库 |
| `https://raw.githubusercontent.com/YoKi141/djmax-random-selector-v/main/Version3.txt` | 版本检查 |
| `https://raw.githubusercontent.com/YoKi141/djmax-random-selector-v/main/appdata.json` | 分类 / DLC / 关联碟数据 |
