using System;
using System.IO;

namespace MusicBridge;

internal static class AudioDiskCache
{
	private static long CapacityBytes => MusicBridgeOptions.Current.Netease.AudioCacheCapacityBytes;

	private static long MaximumSingleFileBytes => MusicBridgeOptions.Current.Netease.AudioCacheMaximumFileBytes;

	private static string DirectoryPath
	{
		get
		{
			string text = BridgePaths.Resolve("cache", "audio");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			return text;
		}
	}

	public static bool TryGetUri(long songId, out string uri)
	{
		uri = null;
		try
		{
			string text = Path.Combine(DirectoryPath, songId + ".mp3");
			FileInfo fileInfo = new FileInfo(text);
			if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > MaximumSingleFileBytes)
			{
				return false;
			}
			fileInfo.LastAccessTimeUtc = DateTime.UtcNow;
			uri = new Uri(text).AbsoluteUri;
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("音频缓存读取失败：" + ex.Message);
			return false;
		}
	}

	public static void Store(long songId, byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0 || bytes.LongLength > MaximumSingleFileBytes)
		{
			return;
		}
		try
		{
			AtomicFile.WriteAllBytes(Path.Combine(DirectoryPath, songId + ".mp3"), bytes);
			Evict(CapacityBytes);
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("音频缓存写入失败：" + ex.Message);
		}
	}

	public static void Remove(long songId)
	{
		try
		{
			string path = Path.Combine(DirectoryPath, songId + ".mp3");
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("音频缓存删除失败：" + ex.Message);
		}
	}

	private static void Evict(long capacityBytes)
	{
		FileInfo[] files = new DirectoryInfo(DirectoryPath).GetFiles("*.mp3", SearchOption.TopDirectoryOnly);
		Array.Sort(files, (FileInfo a, FileInfo b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
		long num = 0L;
		FileInfo[] array = files;
		foreach (FileInfo fileInfo in array)
		{
			num += fileInfo.Length;
		}
		int num3 = 0;
		while (num > capacityBytes && num3 < files.Length)
		{
			long length = files[num3].Length;
			try
			{
				files[num3].Delete();
				num -= length;
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("音频缓存淘汰失败：" + ex.Message);
			}
			num3++;
		}
	}
}
