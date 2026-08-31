# 原生运行库来源与构建记录

日期：2026-08-31。适配目标：Chill With You 1.16.1 / Unity 2022.3.62f2 Mono，Apple Silicon macOS。

## 来源

原版 BepInEx 5.4.23.5 所附 Harmony/MonoMod 在本机 arm64 的 `DetourHelper.GetIdentifiable` 初始化失败。当前用以下固定提交的社区兼容实现，不能标为 BepInEx 官方稳定 ARM 版本：

- [BepInEx 社区分支](https://github.com/bbauti/BepInEx/tree/105b4f06d16b23d221cde22062e48d3c0fb9a9dd)，提交 `105b4f06d16b23d221cde22062e48d3c0fb9a9dd`。
- [BepInEx.Harmony 社区分支](https://github.com/bbauti/BepInEx.Harmony/tree/f16083f4e78f50320afad47a5d0e1c5474a582f5)，提交 `f16083f4e78f50320afad47a5d0e1c5474a582f5`。
- 背景：[BepInEx issue 1303](https://github.com/BepInEx/BepInEx/issues/1303)、[BepInEx.Harmony PR 4](https://github.com/BepInEx/BepInEx.Harmony/pull/4)。
- HarmonyX 2.16.1、MonoMod.RuntimeDetour 25.3.4、MonoMod.Utils 25.0.12；其他传递依赖由固定项目还原。构建输出有 MonoMod.Core、Backports、ILHelpers、Iced 等必要依赖。
- Doorstop 的 `libdoorstop.dylib` 仍取自官方 [BepInEx macOS universal 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) 包，未宣称升级到其他 Doorstop 版本。

## 可复现修改

`build-native-runtime.sh` 校验源码归档 SHA-256、解包并应用 `patches/native-runtime.patch` 后，用 .NET SDK 8 发布 net35 运行库。

补丁移除该社区分支中 Valheim 版本探测和 `Game.isModded` 反射设置，以及不适用于本项目的 Thunderstore 包版本提示；替换为明确的 MusicBridge 社区构建标记。保留 ARM 兼容与 Harmony 互操作实现，不修改游戏源文件。

归档 SHA-256：

| 归档 | SHA-256 |
| --- | --- |
| BepInEx 源码 | `0121fd146256e7a681b937e288e13a13b5c9ece9f867df12a38e65ea97133c95` |
| BepInEx.Harmony 源码 | `b8fd8d9d646eff844e08962ecd7c545cc0f47151bc73a714760ec8dc0ed10be0` |
| 官方 macOS 包 | `01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323` |

## 许可与发布边界

固定 BepInEx 和 BepInEx.Harmony 源码根目录的 LICENSE 都标为 MIT；原 Windows 分发目录另含 LGPL-2.1 文本，予以保留，不能把它简单等同于此次所有新依赖的许可证。运行包还保留上述两份源码许可证和来源说明。其他依赖遵守各自随附许可。

此次贡献仅包含 Mac 适配源码、脚本与文档，不新增预编译运行包。源码构建所需第三方内容由脚本从指定来源下载；来源及许可记录见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。若另行分发运行包，应一并保留各组件许可和通知，不得混入游戏 DLL、用户账号信息、日志或缓存。

## Steam 入口

采用 [BepInEx 的 Steam 启动选项接入方式](https://github.com/BepInEx/bepinex-docs/blob/master/articles/advanced/steam_interop.md)：Steam 调用 `launch-core.sh --steam %command%`，该脚本接收 Steam 原始游戏路径和参数，选择本机架构，设置当前进程加载环境后 exec 游戏。无独立 GUI、无常驻启动器，游戏程序保持原样。

原始运行库及 Steam 选项保存在本机忽略目录 `.local/`；回滚方式见 README。
