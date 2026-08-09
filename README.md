# 为游戏 *Chill with You: Lo-Fi Story* 嵌入网易云音乐与 Apple Music 播放功能。


嵌入网易云音乐与 Apple Music 播放功能。\
并把游戏自带本地音乐播放功能也纳入同一套播放控制，三个音源互相独立、随时切换。\
支持歌单、搜索、播放控制和歌词都以游戏原生风格注入到游戏内的音乐面板里。

聪音也要听音乐喵！
![alt text](./assets/20251119141808_1.jpg)
---

## 安装

**安装插件**（本插件依赖于BepInEx，压缩包里已安装好了BepInEx，不需额外安装）

1. 找到游戏根目录：Steam 库中右键游戏 → 管理 → 浏览本地文件。
   该目录下应该有 `Chill With You.exe`
2. 把压缩包里的所有文件全部复制到游戏根目录里（如下图）
![alt text](./assets/948a5ae9f.png)

---

## 功能

**网易云音乐模块**

- 扫码登录 （会话保存在本 Mod 目录下并用 Windows DPAPI 按当前用户加密）
- 我的歌单 / 我喜欢 / 收藏歌单 / 搜索 / 每日推荐
- 搜索单曲、歌单、专辑（专辑结果标注类型：专辑 / EP / Single / 合集）
- 播放、暂停、上一首、下一首、随机、单曲循环、可滑块调整音量与歌曲进度
- 滚动歌词
![alt text](./assets/20260804223422_1.jpg)

**Apple Music模块** （⚠ 使用Apple Music模块 需要保持Apple Music的运行，无需置顶，但是不能最小化或者退出）

- 连接本机 Apple Music 应用，读取「文件夹 — 播放列表 — 歌曲」完整结构
- 首次连接自动扫描并写入缓存，之后一律读缓存
- 播放控制与队列（随机、单曲循环由本插件自己维护）（Apple Music模块 无法滑动歌曲进度条）
- 歌词通过网易云匹配获得，所以如果要使用 Apple Music模块 的歌词功能需要 网易云音乐模块 已登录
  支持跨语言：先用 iTunes Search API 取到同一首歌在各语言区的名称，
  再与网易云交叉比对，因此「雪の華 / 雪之华 / Yuki no Hana」这类写法差异也能匹配上
  (如果是国区/日区的苹果音乐，歌词匹配会很快；如果是英语区，中文歌和日文歌的歌名和歌手可能是英语/拼音/罗马音，匹配会较慢，并且可能由于版本不同的原因，一些歌词的时间戳无法准确匹配)
![alt text](./assets/20260804223820_1.jpg)

**本地音乐模块（游戏自带曲目/本地导入曲目 播放）**

- 把游戏原本的音乐功能也做成第三个音源模块，和上面两个平级
- 直接显示游戏自己的播放列表，不复制、不改写游戏的曲库
- 播放、暂停、上一首、下一首、随机、单曲循环、音量、进度拖动
- 所有控制都调用游戏自己的按钮方法，行为与原生按钮一致
- 无需登录，也不需要联网
![alt text](./assets/20260804223727_1.jpg)



**通用功能**

- 三个音源互相独立：每个标签只显示自己的播放列表
- 同一时刻只有一个音源出声，切换时其余自动暂停
- 接管游戏自带的底部播放条：进度、音量、播放键、随机键、循环键
  
| | 本地音乐 | 网易云 | Apple |
|---|---|---|---|
| 在播判据 | 读游戏画的播放键图标 | `State == Playing` | `AppleMusicService.IsPlaying` |
| `CanSeek` | ✅ | ✅ | ❌ |
| `SupportsLyrics` | ❌ | ✅ | ✅ |
| 控制方式 | 模拟点游戏按钮 | 自己实现 | SMTC 指令 |
| 音量通路 | 游戏原生滑条 | `AudioSource.volume` | `AppleMusicService` |

**关于退出**

- 点击游戏的退出图标时立即停止播放

## 环境要求

| 项 | 要求 |
|---|---|
| 游戏 | Chill with You: Lo-Fi Story（Steam，Windows） |
| 加载器 | BepInEx 5 「已内置」|
| 运行时 | 游戏自带的 Unity Mono，无需额外安装 |
| 网易云 | 需要你自己的账号，扫码登录 |
| Apple Music | 需要 Microsoft Store 版 Apple Music 应用正在运行并已登录 |

**⚠基于此，如果Apple Music更新、游戏更新、Unity 版本升级、网易云接口变化 等 均可能导致本插件无法正常使用。**

## 卸载

删除复制的:\
`BepInEx\`\
`/winhttp.dll / doorstop_config.ini / .doorstop_version / changelog.txt ` \
这些文件即可。

游戏存档不受影响：本插件不读写游戏存档，所有数据都在自己的文件夹内。

## 那么再见啦~
![alt text](./assets/20260802233726_1.jpg)

## 关于数据与隐私

- 所有文件只写入本插件自己的目录，不会改变游戏目录之外的任何位置
- 不修改注册表、系统环境变量、Steam 配置，也不改动其他 Mod 的文件
- 固定的接口请求只发往两个地址：`music.163.com`（网易云官方接口）和
  `itunes.apple.com`（iTunes Search API，只读 GET、不带任何凭据）
- 账号凭据只保存在 `config\netease_session.dat`，用 Windows DPAPI 按当前用户加密。


本插件使用你自己的账号访问官方接口，不会绕过 VIP、DRM 或区域限制。\
Apple Music 的声音由 Apple Music 应用自身发出（FairPlay DRM 限制），不在游戏内解码。

## 已知限制

- **Apple Music 无法跳转进度**。这是 Apple Music 的限制。
- **Apple Music 播放列表不显示封面缩略图**。自动化接口读不到位图，只有当前播放的那一首
  能通过系统媒体控件拿到封面。
- 歌词依赖网易云曲库。网易云没有对应版本时不显示歌词。
- Apple Music 应用在长时间自动化枚举下可能自行崩溃，这是它自身的缺陷。
  如果崩溃可在任务管理器关掉Apple Music，再重新运行Apple Music，本插件会记录进度并接着未扫描的部分继续扫描


## 许可
MIT，见 `LICENSE`。

本包内含 BepInEx 5.4.23.5，原样收录、未作修改。\
BepInEx 以 LGPL-2.1 授权，协议全文见 BepInEx\LICENSE，源码见 https://github.com/BepInEx/BepInEx\
「MIT 仅适用于本插件自身代码，随包分发的 BepInEx 另按 LGPL-2.1」
\
作者：**MoonFlower**
![alt text](./assets/20260802233808_1.jpg)