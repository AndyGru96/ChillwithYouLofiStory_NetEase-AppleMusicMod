using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MusicBridge;

internal static class LocalPersistence
{
	private sealed class Live
	{
		public IList Tracks;

		public List<string> Order;

		public List<string> Favorites;

		public List<string> Excluded;

		public bool Valid
		{
			get
			{
				if (Tracks != null && Order != null && Favorites != null)
				{
					return Excluded != null;
				}
				return false;
			}
		}
	}

	private sealed class Pending
	{
		public bool Projected;

		public bool Wrote;

		public IList Tracks;

		public List<object> TracksBackup;

		public List<string> Order;

		public List<string> Favorites;

		public List<string> Excluded;

		public List<string> OrderBackup;

		public List<string> FavoritesBackup;

		public List<string> ExcludedBackup;
	}

	public const int NativeKeepCount = 100;

	private static bool _installed;

	private static bool _merged;

	private static bool _deleting;

	private static int _depth;

	private static long _lastFingerprint;

	private static Type _saveManagerType;

	private static PropertyInfo _instanceProp;

	private static PropertyInfo _musicSettingProp;

	private static PropertyInfo _localMusicSettingProp;

	private static FieldInfo _localAudioDatasField;

	private static FieldInfo _playlistOrderField;

	private static FieldInfo _favoriteField;

	private static FieldInfo _excludedField;

	private static Type _localAudioDataType;

	private static FieldInfo _filePathField;

	private static FieldInfo _uuidField;

	private static Pending _pending;

	private static readonly TimeSpan MinimumWriteInterval = TimeSpan.FromMilliseconds(750.0);

	private static DateTime _lastWriteAt = DateTime.MinValue;

	private static int _lastProjectionFrom = -1;

	public static bool Ready { get; private set; }

	public static bool IsProjecting { get; private set; }

	public static void Install(Harmony harmony)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected O, but got Unknown
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Expected O, but got Unknown
		//IL_015c: Expected O, but got Unknown
		if (_installed)
		{
			return;
		}
		_installed = true;
		try
		{
			if (!ResolveTypes())
			{
				BridgeLog.Warn("本地超额存储：游戏类型或字段对不上，功能不启用。");
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(_saveManagerType, "SaveMusicSetting", (Type[])null, (Type[])null);
			MethodInfo methodInfo2 = AccessTools.Method(_saveManagerType, "SaveLocalMusicSetting", (Type[])null, (Type[])null);
			MethodInfo methodInfo3 = AccessTools.PropertySetter(_saveManagerType, "LocalMusicSetting");
			MethodInfo methodInfo4 = AccessTools.Method(_saveManagerType, "DeleteAllSaveData", (Type[])null, (Type[])null);
			if (methodInfo == null || methodInfo2 == null || methodInfo3 == null)
			{
				BridgeLog.Warn("本地超额存储：存档出口不齐（保存或安装点缺失），功能不启用。");
				return;
			}
			HarmonyMethod val = new HarmonyMethod(AccessTools.Method(typeof(LocalPersistence), "Save_Prefix", (Type[])null, (Type[])null));
			HarmonyMethod val2 = new HarmonyMethod(AccessTools.Method(typeof(LocalPersistence), "Save_Finalizer", (Type[])null, (Type[])null));
			harmony.Patch((MethodBase)methodInfo, val, (HarmonyMethod)null, (HarmonyMethod)null, val2, (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo2, val, (HarmonyMethod)null, (HarmonyMethod)null, val2, (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo3, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(LocalPersistence), "SetLocalMusicSetting_Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			if (methodInfo4 != null)
			{
				harmony.Patch((MethodBase)methodInfo4, new HarmonyMethod(AccessTools.Method(typeof(LocalPersistence), "DeleteAll_Prefix", (Type[])null, (Type[])null)), new HarmonyMethod(AccessTools.Method(typeof(LocalPersistence), "DeleteAll_Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
			else
			{
				BridgeLog.Warn("本地超额存储：找不到 DeleteAllSaveData，删档时侧载库不会自动清空。");
			}
			Ready = true;
			BridgeLog.Info("本地超额存储：持久化协调器已就位（原生保留上限 " + 100 + " 首）。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("本地超额存储安装失败：" + ex);
			Ready = false;
		}
	}

	private static bool ResolveTypes()
	{
		_saveManagerType = AccessTools.TypeByName("Bulbul.SaveDataManager") ?? AccessTools.TypeByName("SaveDataManager");
		_localAudioDataType = AccessTools.TypeByName("Bulbul.LocalAudioData") ?? AccessTools.TypeByName("LocalAudioData");
		Type type = AccessTools.TypeByName("Bulbul.LocalMusicSetting") ?? AccessTools.TypeByName("LocalMusicSetting");
		Type type2 = AccessTools.TypeByName("Bulbul.MusicSettingV2") ?? AccessTools.TypeByName("MusicSettingV2");
		if (_saveManagerType == null || _localAudioDataType == null || type == null || type2 == null)
		{
			return false;
		}
		_instanceProp = AccessTools.Property(_saveManagerType, "Instance");
		_musicSettingProp = AccessTools.Property(_saveManagerType, "MusicSetting");
		_localMusicSettingProp = AccessTools.Property(_saveManagerType, "LocalMusicSetting");
		_localAudioDatasField = AccessTools.Field(type, "LocalAudioDatas");
		_playlistOrderField = AccessTools.Field(type2, "PlaylistOrder");
		_favoriteField = AccessTools.Field(type2, "FavoriteAudioUUIDs");
		_excludedField = AccessTools.Field(type2, "ExcludedFromPlaylistUUIDs");
		_filePathField = AccessTools.Field(_localAudioDataType, "FilePath");
		_uuidField = AccessTools.Field(_localAudioDataType, "UUID");
		if (_instanceProp != null && _musicSettingProp != null && _localMusicSettingProp != null && _localAudioDatasField != null && _playlistOrderField != null && _favoriteField != null && _excludedField != null && _filePathField != null)
		{
			return _uuidField != null;
		}
		return false;
	}

	private static Live Read()
	{
		Live live = new Live();
		try
		{
			object value = _instanceProp.GetValue(null, null);
			if (value == null)
			{
				return live;
			}
			object value2 = _localMusicSettingProp.GetValue(value, null);
			object value3 = _musicSettingProp.GetValue(value, null);
			if (value2 != null)
			{
				live.Tracks = _localAudioDatasField.GetValue(value2) as IList;
			}
			if (value3 != null)
			{
				live.Order = _playlistOrderField.GetValue(value3) as List<string>;
				live.Favorites = _favoriteField.GetValue(value3) as List<string>;
				live.Excluded = _excludedField.GetValue(value3) as List<string>;
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("读取存档对象失败：" + ex.GetType().Name + " " + ex.Message);
		}
		return live;
	}

	public static IList LiveTracks()
	{
		if (!Ready)
		{
			return null;
		}
		try
		{
			object value = _instanceProp.GetValue(null, null);
			if (value == null)
			{
				return null;
			}
			object value2 = _localMusicSettingProp.GetValue(value, null);
			return (value2 == null) ? null : (_localAudioDatasField.GetValue(value2) as IList);
		}
		catch
		{
			return null;
		}
	}

	public static string UuidOf(object localAudioData)
	{
		if (localAudioData == null || _uuidField == null)
		{
			return null;
		}
		try
		{
			return _uuidField.GetValue(localAudioData) as string;
		}
		catch
		{
			return null;
		}
	}

	private static List<string> TrackIds(IList tracks)
	{
		List<string> list = new List<string>(tracks?.Count ?? 0);
		if (tracks == null)
		{
			return list;
		}
		foreach (object track in tracks)
		{
			if (track != null)
			{
				string text = _uuidField.GetValue(track) as string;
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(text);
				}
			}
		}
		return list;
	}

	private static LocalLibrarySnapshot Capture(Live live)
	{
		LocalLibrarySnapshot localLibrarySnapshot = new LocalLibrarySnapshot
		{
			NativeKeepCount = 100
		};
		foreach (object track in live.Tracks)
		{
			if (track != null)
			{
				localLibrarySnapshot.Tracks.Add(new LocalTrackEntry
				{
					FilePath = (_filePathField.GetValue(track) as string),
					UUID = (_uuidField.GetValue(track) as string)
				});
			}
		}
		localLibrarySnapshot.NativeProjection = LocalLibraryMerge.Project(localLibrarySnapshot.TrackIds(), 100);
		localLibrarySnapshot.PlaylistOrder = new List<string>(live.Order);
		localLibrarySnapshot.FavoriteAudioUUIDs = new List<string>(live.Favorites);
		localLibrarySnapshot.ExcludedFromPlaylistUUIDs = new List<string>(live.Excluded);
		return localLibrarySnapshot;
	}

	private static long Fingerprint(LocalLibrarySnapshot snap)
	{
		long h = 1469598103934665603L;
		Absorb(ref h, snap.Tracks.Count);
		foreach (LocalTrackEntry track in snap.Tracks)
		{
			Absorb(ref h, track.UUID);
			Absorb(ref h, track.FilePath);
		}
		Absorb(ref h, snap.PlaylistOrder);
		Absorb(ref h, snap.FavoriteAudioUUIDs);
		Absorb(ref h, snap.ExcludedFromPlaylistUUIDs);
		return h;
	}

	private static void Absorb(ref long h, List<string> list)
	{
		Absorb(ref h, list.Count);
		foreach (string item in list)
		{
			Absorb(ref h, item);
		}
	}

	private static void Absorb(ref long h, string s)
	{
		if (s == null)
		{
			h = (h ^ 0x5BF03635) * 1099511628211L;
			return;
		}
		for (int i = 0; i < s.Length; i++)
		{
			h = (h ^ s[i]) * 1099511628211L;
		}
		h = (h ^ 0x9E3779B9u) * 1099511628211L;
	}

	private static void Absorb(ref long h, int v)
	{
		h = (h ^ v) * 1099511628211L;
	}

	private static void Save_Prefix(MethodBase __originalMethod)
	{
		try
		{
			if (!Ready || _depth++ > 0)
			{
				return;
			}
			_pending = null;
			Live live = Read();
			if (!live.Valid)
			{
				return;
			}
			Pending pending = (_pending = new Pending
			{
				Tracks = live.Tracks,
				Order = live.Order,
				Favorites = live.Favorites,
				Excluded = live.Excluded
			});
			bool flag = live.Tracks.Count > 100;
			if (flag)
			{
				pending.TracksBackup = new List<object>(live.Tracks.Count);
				foreach (object track in live.Tracks)
				{
					pending.TracksBackup.Add(track);
				}
				pending.OrderBackup = new List<string>(live.Order);
				pending.FavoritesBackup = new List<string>(live.Favorites);
				pending.ExcludedBackup = new List<string>(live.Excluded);
				pending.Projected = true;
			}
			bool flag2 = __originalMethod != null && __originalMethod.Name == "SaveLocalMusicSetting";
			bool num = flag || LocalLibraryStore.Exists();
			DateTime utcNow = DateTime.UtcNow;
			if (num && (flag2 || utcNow - _lastWriteAt >= MinimumWriteInterval))
			{
				LocalLibrarySnapshot localLibrarySnapshot = Capture(live);
				long num2 = Fingerprint(localLibrarySnapshot);
				if (num2 != _lastFingerprint)
				{
					pending.Wrote = LocalLibraryStore.WritePending(localLibrarySnapshot);
					if (!pending.Wrote)
					{
						LocalImportPolicy.Disable("侧载快照写入失败");
					}
					else
					{
						_lastFingerprint = num2;
						_lastWriteAt = utcNow;
					}
				}
				else
				{
					_lastWriteAt = utcNow;
				}
			}
			if (!pending.Projected)
			{
				return;
			}
			IsProjecting = true;
			List<string> allTrackIds = TrackIds(live.Tracks);
			List<string> list = LocalLibraryMerge.Project(allTrackIds, 100);
			HashSet<string> drop = LocalLibraryMerge.OverflowSet(allTrackIds, list);
			HashSet<string> hashSet = new HashSet<string>(list);
			List<object> list2 = new List<object>();
			foreach (object item in pending.TracksBackup)
			{
				string text = ((item == null) ? null : (_uuidField.GetValue(item) as string));
				if (text != null && hashSet.Contains(text))
				{
					list2.Add(item);
				}
			}
			live.Tracks.Clear();
			foreach (object item2 in list2)
			{
				live.Tracks.Add(item2);
			}
			if (pending.TracksBackup.Count != _lastProjectionFrom)
			{
				_lastProjectionFrom = pending.TracksBackup.Count;
				BridgeLog.Info("原生存档投影：内存 " + pending.TracksBackup.Count + " 首 → 写入 " + list2.Count + " 首，超额 " + (pending.TracksBackup.Count - list2.Count) + " 首留在侧载库。");
			}
			Replace(live.Order, LocalLibraryMerge.Filter(pending.OrderBackup, drop));
			Replace(live.Favorites, LocalLibraryMerge.Filter(pending.FavoritesBackup, drop));
			Replace(live.Excluded, LocalLibraryMerge.Filter(pending.ExcludedBackup, drop));
		}
		catch (Exception ex)
		{
			BridgeLog.Error("保存前置处理失败：" + ex);
			LocalImportPolicy.Disable("保存前置处理异常");
		}
	}

	private static void Save_Finalizer()
	{
		try
		{
			if (!Ready || --_depth > 0)
			{
				return;
			}
			if (_depth < 0)
			{
				_depth = 0;
			}
			Pending pending = _pending;
			_pending = null;
			if (pending == null)
			{
				return;
			}
			if (pending.Projected)
			{
				try
				{
					pending.Tracks.Clear();
					foreach (object item in pending.TracksBackup)
					{
						pending.Tracks.Add(item);
					}
					Replace(pending.Order, pending.OrderBackup);
					Replace(pending.Favorites, pending.FavoritesBackup);
					Replace(pending.Excluded, pending.ExcludedBackup);
				}
				catch (Exception ex)
				{
					BridgeLog.Error("投影后恢复内存失败：" + ex);
					LocalImportPolicy.Disable("投影恢复失败");
				}
				finally
				{
					IsProjecting = false;
				}
			}
			if (pending.Wrote)
			{
				LocalLibraryStore.Commit();
			}
		}
		catch (Exception ex2)
		{
			BridgeLog.Error("保存收尾失败：" + ex2);
		}
	}

	private static void Replace(List<string> target, List<string> content)
	{
		target.Clear();
		target.AddRange(content);
	}

	private static void DeleteAll_Prefix()
	{
		_deleting = true;
	}

	private static void DeleteAll_Postfix()
	{
		_deleting = false;
		try
		{
			LocalLibraryStore.Wipe();
			_lastFingerprint = 0L;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("删档后清空侧载库失败：" + ex);
		}
	}

	private static void SetLocalMusicSetting_Postfix()
	{
		try
		{
			if (!Ready || _merged || _deleting)
			{
				return;
			}
			_merged = true;
			Live live = Read();
			if (!live.Valid)
			{
				BridgeLog.Warn("启动合并跳过：存档对象未就绪。");
				return;
			}
			LocalLibraryStore.WriteBaselineOnce(Capture(live));
			BridgeLog.Info("本地导入曲目：原生存档中 " + live.Tracks.Count + " 首（不含游戏自带音乐）。");
			LocalLibrarySnapshot localLibrarySnapshot = LocalLibraryStore.Load();
			if (localLibrarySnapshot == null)
			{
				_lastFingerprint = Fingerprint(Capture(live));
				BridgeLog.Info("没有侧载库，本次按原生存档原样运行（首次安装即是如此）。");
				return;
			}
			List<string> list = TrackIds(live.Tracks);
			List<string> list2 = LocalLibraryMerge.MergeTracks(list, localLibrarySnapshot.TrackIds(), localLibrarySnapshot.NativeProjection);
			int num = list2.Count - list.Count;
			if (num > 0)
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				foreach (object track in live.Tracks)
				{
					string text = ((track == null) ? null : (_uuidField.GetValue(track) as string));
					if (!string.IsNullOrEmpty(text) && !dictionary.ContainsKey(text))
					{
						dictionary[text] = track;
					}
				}
				Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
				foreach (LocalTrackEntry track2 in localLibrarySnapshot.Tracks)
				{
					if (track2 != null && !string.IsNullOrEmpty(track2.UUID) && !dictionary2.ContainsKey(track2.UUID))
					{
						dictionary2[track2.UUID] = track2.FilePath;
					}
				}
				List<object> list3 = new List<object>(list2.Count);
				foreach (string item in list2)
				{
					string value2;
					if (dictionary.TryGetValue(item, out var value))
					{
						list3.Add(value);
					}
					else if (dictionary2.TryGetValue(item, out value2) && !string.IsNullOrEmpty(value2))
					{
						list3.Add(Activator.CreateInstance(_localAudioDataType, value2, item));
					}
				}
				live.Tracks.Clear();
				foreach (object item2 in list3)
				{
					live.Tracks.Add(item2);
				}
			}
			HashSet<string> hashSet = new HashSet<string>(list2);
			foreach (string item3 in live.Order)
			{
				if (!string.IsNullOrEmpty(item3))
				{
					hashSet.Add(item3);
				}
			}
			Replace(live.Order, LocalLibraryMerge.MergeOrder(live.Order, localLibrarySnapshot.PlaylistOrder, hashSet));
			Replace(live.Favorites, LocalLibraryMerge.MergeOrder(live.Favorites, localLibrarySnapshot.FavoriteAudioUUIDs, hashSet));
			Replace(live.Excluded, LocalLibraryMerge.MergeOrder(live.Excluded, localLibrarySnapshot.ExcludedFromPlaylistUUIDs, hashSet));
			_lastFingerprint = Fingerprint(Capture(live));
			BridgeLog.Info("启动合并完成：原生 " + list.Count + " 首，恢复超额 " + ((num > 0) ? num : 0) + " 首，合计 " + live.Tracks.Count + " 首。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("启动合并失败，本次按原生存档运行：" + ex);
			LocalImportPolicy.Disable("启动合并异常");
		}
	}
}
