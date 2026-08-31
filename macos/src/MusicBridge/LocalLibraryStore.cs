using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MusicBridge;

internal static class LocalLibraryStore
{
	private const long MaximumFileBytes = 67108864L;

	private static string Committed => BridgePaths.Resolve("data", "local-library.json");

	private static string Pending => BridgePaths.Resolve("data", "local-library.pending.json");

	private static string Backup => BridgePaths.Resolve("data", "local-library.bak.json");

	private static string NativeBaseline => BridgePaths.Resolve("data", "native-baseline.json");

	public static bool Healthy { get; private set; } = true;

	public static long LastGeneration { get; private set; }

	public static bool Exists()
	{
		try
		{
			return File.Exists(Committed) || File.Exists(Pending);
		}
		catch
		{
			return false;
		}
	}

	public static void MarkUnhealthy(string why)
	{
		if (Healthy)
		{
			Healthy = false;
			BridgeLog.Error("侧载库不可用，已停止超额导入：" + why);
		}
	}

	public static LocalLibrarySnapshot Load()
	{
		string[] array = new string[3] { Pending, Committed, Backup };
		foreach (string path in array)
		{
			LocalLibrarySnapshot localLibrarySnapshot = TryReadOne(path);
			if (localLibrarySnapshot != null)
			{
				LastGeneration = localLibrarySnapshot.Generation;
				BridgeLog.Info("侧载库已读取：曲目 " + ((localLibrarySnapshot.Tracks != null) ? localLibrarySnapshot.Tracks.Count : 0) + " 首，代次 " + localLibrarySnapshot.Generation + "，来源=" + Path.GetFileName(path) + "。");
				return localLibrarySnapshot;
			}
		}
		return null;
	}

	private static LocalLibrarySnapshot TryReadOne(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			if (new FileInfo(path).Length > 67108864)
			{
				BridgeLog.Warn("侧载库文件超过上限，已忽略：" + Path.GetFileName(path));
				return null;
			}
			LocalLibrarySnapshot localLibrarySnapshot = JsonConvert.DeserializeObject<LocalLibrarySnapshot>(File.ReadAllText(path));
			if (localLibrarySnapshot == null)
			{
				return null;
			}
			if (localLibrarySnapshot.SchemaVersion != 1)
			{
				BridgeLog.Warn("侧载库 SchemaVersion=" + localLibrarySnapshot.SchemaVersion + "，当前只接受 " + 1 + "，已忽略：" + Path.GetFileName(path));
				return null;
			}
			if (localLibrarySnapshot.Tracks == null)
			{
				localLibrarySnapshot.Tracks = new List<LocalTrackEntry>();
			}
			if (localLibrarySnapshot.NativeProjection == null)
			{
				localLibrarySnapshot.NativeProjection = new List<string>();
			}
			if (localLibrarySnapshot.PlaylistOrder == null)
			{
				localLibrarySnapshot.PlaylistOrder = new List<string>();
			}
			if (localLibrarySnapshot.FavoriteAudioUUIDs == null)
			{
				localLibrarySnapshot.FavoriteAudioUUIDs = new List<string>();
			}
			if (localLibrarySnapshot.ExcludedFromPlaylistUUIDs == null)
			{
				localLibrarySnapshot.ExcludedFromPlaylistUUIDs = new List<string>();
			}
			return localLibrarySnapshot;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("侧载库读取失败（" + Path.GetFileName(path) + "）：" + ex.GetType().Name + " " + ex.Message);
			return null;
		}
	}

	public static bool WritePending(LocalLibrarySnapshot snapshot)
	{
		if (snapshot == null)
		{
			return false;
		}
		try
		{
			snapshot.SchemaVersion = 1;
			snapshot.Generation = LastGeneration + 1;
			AtomicFile.WriteAllText(Pending, JsonConvert.SerializeObject(snapshot, Formatting.None));
			return true;
		}
		catch (Exception ex)
		{
			MarkUnhealthy("写 pending 失败：" + ex.GetType().Name + " " + ex.Message);
			return false;
		}
	}

	public static bool Commit()
	{
		try
		{
			if (!File.Exists(Pending))
			{
				return false;
			}
			if (File.Exists(Committed))
			{
				string text = BridgePaths.ValidateWritePath(Backup);
				if (File.Exists(text))
				{
					File.Delete(text);
				}
				File.Move(BridgePaths.ValidateWritePath(Committed), text);
			}
			AtomicFile.Replace(Pending, Committed);
			LastGeneration++;
			return true;
		}
		catch (Exception ex)
		{
			MarkUnhealthy("提交失败：" + ex.GetType().Name + " " + ex.Message);
			return false;
		}
	}

	public static void WriteBaselineOnce(LocalLibrarySnapshot native)
	{
		try
		{
			if (native != null)
			{
				string nativeBaseline = NativeBaseline;
				if (!File.Exists(nativeBaseline))
				{
					AtomicFile.WriteAllText(nativeBaseline, JsonConvert.SerializeObject(native, Formatting.Indented));
					BridgeLog.Info("已保存原生存档基线（首次投影前的原样备份）。");
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("原生存档基线写入失败：" + ex.GetType().Name + " " + ex.Message);
		}
	}

	public static void Wipe()
	{
		string[] array = new string[3] { Pending, Committed, Backup };
		foreach (string path in array)
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(BridgePaths.ValidateWritePath(path));
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("清空侧载库失败：" + ex.GetType().Name + " " + ex.Message);
			}
		}
		LastGeneration = 0L;
		BridgeLog.Info("侧载库已随游戏删档一并清空。");
	}
}
