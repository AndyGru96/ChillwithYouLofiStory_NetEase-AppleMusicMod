# 第三方来源与许可说明

本次新增内容只分发 Mac 适配源码、脚本和文档，不新增游戏文件或预编译第三方运行库。原仓库已有的 Windows 发行文件保持原样。

## MusicBridge

源于 MoonFlower 的 MusicBridge 1.2.0，原仓库提交 `db8316e0dc33132c982538aad100489d4a86fb81`。上游未提供源码工程，本目录 C# 代码经 ILSpy 9.1 从 DLL 恢复后适配；不是原作者提供的源码。

插件遵循仓库根目录 [MIT LICENSE](../LICENSE)，保留原作者署名。ILSpy 仅用作恢复源码的开发工具，不随本适配分发。

## 原生加载与补丁依赖

- **BepInEx**：固定社区提交 `105b4f06d16b23d221cde22062e48d3c0fb9a9dd`，该源码根目录 [LICENSE](https://github.com/bbauti/BepInEx/blob/105b4f06d16b23d221cde22062e48d3c0fb9a9dd/LICENSE) 为 MIT。构建脚本保留其许可。
- **BepInEx.Harmony**：固定社区提交 `f16083f4e78f50320afad47a5d0e1c5474a582f5`，源码 [LICENSE](https://github.com/bbauti/BepInEx.Harmony/blob/f16083f4e78f50320afad47a5d0e1c5474a582f5/LICENSE) 为 MIT。构建脚本保留其许可。
- **HarmonyX / MonoMod / Mono.Cecil**：由上述固定项目通过 NuGet 获取；各自许可与通知见所下载包中的 nuspec / LICENSE。版本和构建来源见 [NATIVE-RUNTIME.md](NATIVE-RUNTIME.md)。不将这些依赖的许可等同于插件的 MIT 许可。
- **Doorstop**：`libdoorstop.dylib` 取自 BepInEx 官方 macOS universal 5.4.23.5 包；仅在本地构建时提取，不随本次源码贡献上传。
- 原仓库 Windows 包中的 LGPL-2.1 文本继续保留；它不应被当成此次所有新依赖统一使用的许可。

`patches/native-runtime.patch` 是对固定 BepInEx 社区源码的修改，移除不相关的 Valheim 探测并注明构建来源；保留原源码许可。

## 测试与本机引用

测试工程通过 NuGet 使用 Newtonsoft.Json 13.0.3。游戏的 Unity、TextMeshPro、Newtonsoft.Json 等 DLL 仅作为本机编译引用，项目不复制或上传这些游戏资源。

构建得到的运行目录是本地使用产物。如果将来单独制作二进制发布包，应先逐项收齐其包含的组件许可/通知，并从干净构建产物打包，不能直接分享使用过的运行目录。
