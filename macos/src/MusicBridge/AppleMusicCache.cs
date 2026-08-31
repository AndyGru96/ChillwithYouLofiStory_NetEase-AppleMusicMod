using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MusicBridge;

internal static class AppleMusicCache
{
	internal const int CurrentVersion = 3;

	private const int PreviousVersion = 2;

	private const string FinalName = "applemusic_library.macos.json";

	private const string PendingName = "applemusic_library.macos.pending.json";

	private static string FinalPath => Path.Combine(BridgePaths.Config, "applemusic_library.macos.json");

	private static string PendingPath => Path.Combine(BridgePaths.Config, "applemusic_library.macos.pending.json");

	public static bool Exists()
	{
		try
		{
			return File.Exists(FinalPath);
		}
		catch
		{
			return false;
		}
	}

	public static bool SavePending(List<AmPlaylist> tree, string account)
	{
		try
		{
			AmCacheFile value = new AmCacheFile
			{
				Account = account,
				SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
				Fingerprint = Fingerprint(tree),
				Nodes = ToCache(tree)
			};
			AtomicFile.WriteAllText(PendingPath, JsonConvert.SerializeObject(value, Formatting.Indented));
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 写入 pending 缓存失败：" + ex.Message);
			return false;
		}
	}

	public static bool Commit(List<AmPlaylist> tree, string account, out AmValidation result)
	{
		result = Validate(tree);
		BridgeLog.Info("[AM] 本次同步指纹 = " + result.Fingerprint + "（节点 " + result.NodeCount + "，曲目 " + result.TrackCount + "）");
		if (!result.Ok)
		{
			BridgeLog.Warn("[AM] 缓存校验未通过，**保留上一次的完整数据**。问题 " + result.Problems.Count + " 条，列前 10 条：");
			for (int i = 0; i < result.Problems.Count && i < 10; i++)
			{
				BridgeLog.Warn("[AM]   · " + result.Problems[i]);
			}
			return false;
		}
		try
		{
			if (!SavePending(tree, account))
			{
				return false;
			}
			AtomicFile.Replace(PendingPath, FinalPath);
			BridgeLog.Info("[AM] 缓存已提交：" + result.NodeCount + " 个节点，" + result.TrackCount + " 首曲目，读取失败的歌单 " + result.FailedPlaylists + " 个。");
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 提交缓存失败：" + ex.Message);
			return false;
		}
	}

	public static AmValidation Validate(List<AmPlaylist> tree)
	{
		AmValidation amValidation = new AmValidation();
		if (tree == null)
		{
			amValidation.Problems.Add("树是空的");
			amValidation.HasStructuralProblem = true;
			amValidation.Ok = false;
			return amValidation;
		}
		HashSet<string> ids = new HashSet<string>();
		List<string> list = new List<string>();
		Walk(tree, null, 0, amValidation, ids, list, new HashSet<string>());
		if (list.Count > 0)
		{
			amValidation.Problems.Add("节点 ID 重复 " + list.Count + " 个，例：" + list[0]);
			amValidation.HasStructuralProblem = true;
		}
		if (amValidation.FailedPlaylists > 0)
		{
			amValidation.Problems.Add("有 " + amValidation.FailedPlaylists + " 个歌单未能完整读取，本次不提交");
		}
		amValidation.Fingerprint = Fingerprint(tree);
		amValidation.Ok = amValidation.Problems.Count == 0;
		return amValidation;
	}

	public static string Fingerprint(List<AmPlaylist> tree)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Fold(tree, stringBuilder);
		uint num = 2166136261u;
		string text = stringBuilder.ToString();
		for (int i = 0; i < text.Length; i++)
		{
			num ^= text[i];
			num *= 16777619;
		}
		return num.ToString("X8") + "/" + text.Length;
	}

	private static void Fold(List<AmPlaylist> list, StringBuilder sb)
	{
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			AmPlaylist amPlaylist = list[i];
			AppendField(sb, amPlaylist.PersistentId);
			AppendField(sb, amPlaylist.ParentId);
			AppendField(sb, amPlaylist.Name);
			sb.Append(amPlaylist.IsFolder ? 'D' : 'P').Append('#').Append(amPlaylist.Order)
				.Append('#')
				.Append(i)
				.Append('#')
				.Append(amPlaylist.DeclaredCount)
				.Append('#')
				.Append((int)amPlaylist.TrackState)
				.Append('#')
				.Append(amPlaylist.Tracks.Count)
				.Append('|');
			for (int j = 0; j < amPlaylist.Tracks.Count; j++)
			{
				AmTrack amTrack = amPlaylist.Tracks[j];
				sb.Append(j).Append('#').Append(amTrack.RowIndex)
					.Append('#');
				AppendField(sb, amTrack.PersistentId);
				AppendField(sb, amTrack.Name);
				AppendField(sb, amTrack.Artists);
				AppendField(sb, amTrack.Album);
				AppendField(sb, amTrack.DurationText);
				sb.Append(';');
			}
			sb.Append('}');
			Fold(amPlaylist.Children, sb);
		}
	}

	private static void AppendField(StringBuilder sb, string value)
	{
		value = value ?? "";
		sb.Append(value.Length).Append(':').Append(value)
			.Append('|');
	}

	private static void Walk(List<AmPlaylist> list, string parentId, int depth, AmValidation v, HashSet<string> ids, List<string> dupes, HashSet<string> ancestors)
	{
		if (depth > 12)
		{
			v.Problems.Add("层级超过 12 层，疑似父子循环");
			v.HasStructuralProblem = true;
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			AmPlaylist amPlaylist = list[i];
			v.NodeCount++;
			if (string.IsNullOrEmpty(amPlaylist.PersistentId))
			{
				v.Problems.Add("有节点没有 ID：" + amPlaylist.Name);
				v.HasStructuralProblem = true;
				continue;
			}
			if (!ids.Add(amPlaylist.PersistentId))
			{
				dupes.Add(amPlaylist.PersistentId);
			}
			if (ancestors.Contains(amPlaylist.PersistentId))
			{
				v.Problems.Add("检测到父子循环：" + amPlaylist.Name);
				v.HasStructuralProblem = true;
				continue;
			}
			if (amPlaylist.ParentId != parentId)
			{
				v.Problems.Add("ParentId 不一致：" + amPlaylist.Name + " 记的是 " + (amPlaylist.ParentId ?? "(null)"));
				v.HasStructuralProblem = true;
			}
			if (amPlaylist.Order != i)
			{
				v.Problems.Add("同级 Order 不连续：" + amPlaylist.Name + " 应为 " + i + " 实为 " + amPlaylist.Order);
				v.HasStructuralProblem = true;
			}
			if (!amPlaylist.IsFolder)
			{
				if (amPlaylist.TrackState != AmTrackState.Loaded && amPlaylist.TrackState != AmTrackState.Empty)
				{
					v.FailedPlaylists++;
					v.Problems.Add("『" + amPlaylist.Name + "』状态为 " + amPlaylist.TrackState.ToString() + (string.IsNullOrEmpty(amPlaylist.TracksError) ? "" : ("：" + amPlaylist.TracksError)));
				}
				else if (amPlaylist.DeclaredCount < 0)
				{
					v.FailedPlaylists++;
					v.Problems.Add("『" + amPlaylist.Name + "』没有读到页头声明数量，禁止标记完整");
				}
				else if (amPlaylist.TrackState == AmTrackState.Empty && (amPlaylist.DeclaredCount != 0 || amPlaylist.Tracks.Count != 0))
				{
					v.FailedPlaylists++;
					v.Problems.Add("『" + amPlaylist.Name + "』空歌单判据与数量不一致");
				}
				else if (amPlaylist.TrackState == AmTrackState.Loaded && (amPlaylist.DeclaredCount == 0 || amPlaylist.Tracks.Count != amPlaylist.DeclaredCount))
				{
					v.FailedPlaylists++;
					v.Problems.Add("『" + amPlaylist.Name + "』声明 " + amPlaylist.DeclaredCount + " 首，实际 " + amPlaylist.Tracks.Count + " 首");
				}
				v.TrackCount += amPlaylist.Tracks.Count;
			}
			if (amPlaylist.Children.Count > 0)
			{
				ancestors.Add(amPlaylist.PersistentId);
				Walk(amPlaylist.Children, amPlaylist.PersistentId, depth + 1, v, ids, dupes, ancestors);
				ancestors.Remove(amPlaylist.PersistentId);
			}
		}
	}

	private static void BackupBeforeMigration(int fromVersion)
	{
		try
		{
			if (File.Exists(FinalPath))
			{
				string text = FinalPath + ".v" + fromVersion + ".bak";
				if (!File.Exists(text))
				{
					File.Copy(FinalPath, text);
					BridgeLog.Info("[AM] 迁移前已备份旧缓存到 " + Path.GetFileName(text));
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 迁移前备份失败（不影响迁移）：" + ex.Message);
		}
	}

	public static List<AmPlaylist> Load(string expectedAccount)
	{
		try
		{
			if (!File.Exists(FinalPath))
			{
				return null;
			}
			AmCacheFile amCacheFile = JsonConvert.DeserializeObject<AmCacheFile>(File.ReadAllText(FinalPath));
			if (amCacheFile == null || amCacheFile.Nodes == null)
			{
				return null;
			}
			if (amCacheFile.Version != 3 && amCacheFile.Version != 2)
			{
				BridgeLog.Warn("[AM] 缓存版本 " + amCacheFile.Version + " 不受当前版本 " + 3 + " 支持，不加载也不删除。");
				return null;
			}
			if (string.IsNullOrEmpty(expectedAccount))
			{
				BridgeLog.Warn("[AM] 当前账号尚未确认，为避免串号，本次不加载账号缓存。");
				return null;
			}
			if (!string.Equals(amCacheFile.Account, expectedAccount, StringComparison.Ordinal))
			{
				BridgeLog.Info("[AM] 缓存账号与当前账号不一致，不复用，需要重新同步。");
				return null;
			}
			List<AmPlaylist> list = FromCache(amCacheFile.Nodes, 0, null);
			AmValidation amValidation = Validate(list);
			string text = Fingerprint(list);
			if (amValidation.HasStructuralProblem)
			{
				BridgeLog.Warn("[AM] 缓存结构损坏，不加载。首个问题：" + ((amValidation.Problems.Count > 0) ? amValidation.Problems[0] : "未知"));
				return null;
			}
			if (!amValidation.Ok)
			{
				BridgeLog.Info("[AM] 缓存里有 " + amValidation.FailedPlaylists + " 个歌单未读全，仍照常加载（点「更新播放列表」可补齐）。");
			}
			if (amCacheFile.Version == 3 && !string.Equals(amCacheFile.Fingerprint, text, StringComparison.Ordinal))
			{
				BridgeLog.Warn("[AM] 缓存指纹不匹配，不加载（记录=" + (amCacheFile.Fingerprint ?? "(无)") + "，实际=" + text + "）。");
				return null;
			}
			if (amCacheFile.Version == 2)
			{
				BackupBeforeMigration(2);
				BridgeLog.Info("[AM] 已校验旧版 v" + 2 + " 缓存，将迁移到 v" + 3 + "。");
				SaveFinalMigrated(list, amCacheFile.Account, text);
			}
			BridgeLog.Info("[AM] 已载入当前账号缓存，保存于 " + amCacheFile.SavedAt);
			return list;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 读取缓存失败：" + ex.Message);
			return null;
		}
	}

	public static List<AmPlaylist> LoadPending(string expectedAccount)
	{
		try
		{
			if (!File.Exists(PendingPath))
			{
				return null;
			}
			TimeSpan timeSpan = DateTime.Now - File.GetLastWriteTime(PendingPath);
			if (timeSpan > MusicBridgeOptions.Current.Apple.PendingCacheMaximumAge)
			{
				BridgeLog.Info("[AM] 上一份未完成数据已是 " + (int)timeSpan.TotalHours + " 小时前的，不续扫，全部重读。");
				return null;
			}
			AmCacheFile amCacheFile = JsonConvert.DeserializeObject<AmCacheFile>(File.ReadAllText(PendingPath));
			if (amCacheFile == null || amCacheFile.Nodes == null)
			{
				return null;
			}
			if ((amCacheFile.Version != 3 && amCacheFile.Version != 2) || string.IsNullOrEmpty(expectedAccount))
			{
				return null;
			}
			if (!string.Equals(amCacheFile.Account, expectedAccount, StringComparison.Ordinal))
			{
				return null;
			}
			return FromCache(amCacheFile.Nodes, 0, null);
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 读取续扫数据失败：" + ex.Message);
			return null;
		}
	}

	public static string CachedAccount()
	{
		try
		{
			if (!File.Exists(FinalPath))
			{
				return null;
			}
			AmCacheFile amCacheFile = JsonConvert.DeserializeObject<AmCacheFile>(File.ReadAllText(FinalPath));
			return (amCacheFile != null && (amCacheFile.Version == 3 || amCacheFile.Version == 2)) ? amCacheFile.Account : null;
		}
		catch
		{
			return null;
		}
	}

	public static void Clear()
	{
		string[] array = new string[2] { FinalPath, PendingPath };
		foreach (string path in array)
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
					BridgeLog.Info("[AM] 已删除 " + Path.GetFileName(path));
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("[AM] 删除缓存失败：" + ex.Message);
			}
		}
	}

	private static List<AmCachedNode> ToCache(List<AmPlaylist> src)
	{
		List<AmCachedNode> list = new List<AmCachedNode>();
		if (src == null)
		{
			return list;
		}
		foreach (AmPlaylist item in src)
		{
			AmCachedNode amCachedNode = new AmCachedNode
			{
				Name = item.Name,
				Id = item.PersistentId,
				ParentId = item.ParentId,
				Order = item.Order,
				IsFolder = item.IsFolder,
				DeclaredCount = item.DeclaredCount,
				Summary = item.Summary,
				TrackState = item.TrackState.ToString(),
				Children = ((item.Children.Count > 0) ? ToCache(item.Children) : null)
			};
			if (item.Tracks.Count > 0)
			{
				amCachedNode.Tracks = new List<AmCachedTrack>();
				foreach (AmTrack track in item.Tracks)
				{
					amCachedNode.Tracks.Add(new AmCachedTrack
					{
						Name = track.Name,
						Artists = track.Artists,
						Album = track.Album,
						DurationText = track.DurationText,
						PersistentId = track.PersistentId,
						RowIndex = track.RowIndex
					});
				}
			}
			list.Add(amCachedNode);
		}
		return list;
	}

	private static List<AmPlaylist> FromCache(List<AmCachedNode> src, int depth, string parentId)
	{
		return FromCache(src, depth, parentId, new List<string>());
	}

	private static List<AmPlaylist> FromCache(List<AmCachedNode> src, int depth, string parentId, List<string> ancestors)
	{
		List<AmPlaylist> list = new List<AmPlaylist>();
		if (src == null)
		{
			return list;
		}
		for (int i = 0; i < src.Count; i++)
		{
			AmCachedNode amCachedNode = src[i];
			AmPlaylist amPlaylist = new AmPlaylist
			{
				Name = amCachedNode.Name,
				PersistentId = amCachedNode.Id,
				ParentId = parentId,
				Order = i,
				IsFolder = amCachedNode.IsFolder,
				Depth = depth,
				DeclaredCount = amCachedNode.DeclaredCount,
				Summary = amCachedNode.Summary,
				AncestorIds = new List<string>(ancestors)
			};
			try
			{
				amPlaylist.TrackState = (AmTrackState)Enum.Parse(typeof(AmTrackState), amCachedNode.TrackState ?? "Unknown");
			}
			catch
			{
				amPlaylist.TrackState = AmTrackState.Unknown;
			}
			if (amCachedNode.Children != null && amCachedNode.Children.Count > 0)
			{
				if (amPlaylist.IsFolder)
				{
					ancestors.Add(amCachedNode.Id);
					amPlaylist.Children.AddRange(FromCache(amCachedNode.Children, depth + 1, amCachedNode.Id, ancestors));
					ancestors.RemoveAt(ancestors.Count - 1);
				}
				else
				{
					amPlaylist.Children.AddRange(FromCache(amCachedNode.Children, depth + 1, amCachedNode.Id, ancestors));
				}
				amPlaylist.ChildrenLoaded = true;
			}
			if (amCachedNode.Tracks != null && amCachedNode.Tracks.Count > 0)
			{
				foreach (AmCachedTrack track in amCachedNode.Tracks)
				{
					amPlaylist.Tracks.Add(new AmTrack
					{
						Name = track.Name,
						Artists = track.Artists,
						Album = track.Album,
						DurationText = track.DurationText,
						PersistentId = track.PersistentId,
						RowIndex = track.RowIndex
					});
				}
				amPlaylist.TracksComplete = amPlaylist.TrackState == AmTrackState.Loaded || amPlaylist.TrackState == AmTrackState.Empty;
			}
			list.Add(amPlaylist);
		}
		return list;
	}

	private static void SaveFinalMigrated(List<AmPlaylist> tree, string account, string fingerprint)
	{
		try
		{
			AmCacheFile value = new AmCacheFile
			{
				Version = 3,
				Account = account,
				SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
				Fingerprint = fingerprint,
				Nodes = ToCache(tree)
			};
			AtomicFile.WriteAllText(FinalPath, JsonConvert.SerializeObject(value, Formatting.Indented));
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[AM] 旧缓存迁移写入失败，当前会话仍使用已校验数据：" + ex.Message);
		}
	}
}
