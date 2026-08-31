# 启动前检查探针（开发用）

此工具仅用于隔离检查，不能放进正常运行的游戏插件目录。它无论成功或失败都会退出进程。

先构建 Mac 插件和运行库；再在独立游戏副本旁准备单独的 BepInEx 目录，把原生 core、插件 DLL 和 music.js 复制进去。编译此工程后，把 `MusicBridge.Probe.dll` 放入该副本的 `BepInEx/core`，将 Doorstop 的目标设置为探针 DLL。

运行时需要设置 `CHILL_PROBE_MANAGED` 为副本的 Managed 目录，`CHILL_PROBE_LOG` 为独立日志文件，并按正常加载方式设置 Doorstop 环境。使用 `-batchmode -nographics`，外层进程应设定超时；不要把探针的启动设置写入 Steam。

探针会检查 Harmony、Music 自动化、临时钥匙串、网易云匿名二维码接口及游戏挂钩方法，可能触发系统 Music 自动化授权。需要网络和已可访问的音乐 App；不播放歌曲，不进行网易云登录。仅在独立游戏副本中测试，避免中断正在游玩的进程。
