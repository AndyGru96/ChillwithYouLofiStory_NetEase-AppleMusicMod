using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MusicBridge;

internal static class LocalMusicSource
{
	internal static object Service;

	internal static object Facility;

	private static readonly List<LocalTrack> Cache = new List<LocalTrack>();

	private static readonly IList<LocalTrack> CacheView = Cache.AsReadOnly();

	private static int _cacheStamp = -1;

	private static Type _svcType;

	private static PropertyInfo _pPlaying;

	private static PropertyInfo _pShuffle;

	private static PropertyInfo _pRepeatOne;

	private static PropertyInfo _pPlaylist;

	private static MethodInfo _mProgress;

	private static MethodInfo _mSeek;

	private static MethodInfo _mPause;

	private static Type _rawType;

	private static FieldInfo _fTitle;

	private static FieldInfo _fCredit;

	private static FieldInfo _fClip;

	private static FieldInfo _fTag;

	private static FieldInfo _fLocalPath;

	private static int _captureStamp = -1;

	private static int _invokeDepth;

	private static Type _facilityType;

	private static readonly Dictionary<string, MethodInfo> FacilityMethods = new Dictionary<string, MethodInfo>();

	public static bool Available
	{
		get
		{
			EnsureCaptured();
			return Service != null;
		}
	}

	private static bool Bound
	{
		get
		{
			EnsureCaptured();
			if (Service == null)
			{
				return false;
			}
			BindService();
			return true;
		}
	}

	public static object PlayingRaw
	{
		get
		{
			if (!Bound)
			{
				return null;
			}
			return Prop(_pPlaying);
		}
	}

	public static bool IsShuffle
	{
		get
		{
			object obj = (Bound ? Prop(_pShuffle) : null);
			if (obj is bool)
			{
				return (bool)obj;
			}
			return false;
		}
	}

	public static bool IsRepeatOne
	{
		get
		{
			object obj = (Bound ? Prop(_pRepeatOne) : null);
			if (obj is bool)
			{
				return (bool)obj;
			}
			return false;
		}
	}

	public static float Progress
	{
		get
		{
			if (!Bound || _mProgress == null)
			{
				return 0f;
			}
			try
			{
				object obj = _mProgress.Invoke(Service, null);
				return (obj is float) ? ((float)obj) : 0f;
			}
			catch
			{
				return 0f;
			}
		}
	}

	public static LocalTrack Playing
	{
		get
		{
			object playingRaw = PlayingRaw;
			if (playingRaw != null)
			{
				return FromRaw(playingRaw, -1);
			}
			return null;
		}
	}

	public static double PlayingDuration
	{
		get
		{
			LocalTrack playing = Playing;
			if (playing == null)
			{
				return 0.0;
			}
			if (playing.DurationSeconds > 0.0)
			{
				return playing.DurationSeconds;
			}
			return LocalFileDuration.Get(playing.LocalPath);
		}
	}

	public static IList<LocalTrack> Tracks
	{
		get
		{
			int num = Time.frameCount / 30;
			if (num == _cacheStamp)
			{
				return CacheView;
			}
			_cacheStamp = num;
			Cache.Clear();
			object obj = (Bound ? Prop(_pPlaylist) : null);
			if (obj is IEnumerable)
			{
				int num2 = 0;
				foreach (object item in (IEnumerable)obj)
				{
					if (item != null)
					{
						Cache.Add(FromRaw(item, num2));
						num2++;
					}
				}
			}
			return CacheView;
		}
	}

	internal static bool Invoking => _invokeDepth > 0;

	private static void BindService()
	{
		Type type = Service.GetType();
		if (!(type == _svcType))
		{
			_svcType = type;
			_pPlaying = type.GetProperty("PlayingMusic");
			_pShuffle = type.GetProperty("IsShuffle");
			_pRepeatOne = type.GetProperty("IsRepeatOneMusic");
			_pPlaylist = type.GetProperty("CurrentPlayList");
			_mProgress = type.GetMethod("GetCurrentMusicProgress", Type.EmptyTypes);
			_mSeek = type.GetMethod("SetMusicProgress", new Type[1] { typeof(float) });
			_mPause = type.GetMethod("Pause", Type.EmptyTypes);
		}
	}

	public static void EnsureCaptured()
	{
		if (Facility != null || Time.frameCount / 30 == _captureStamp)
		{
			return;
		}
		_captureStamp = Time.frameCount / 30;
		try
		{
			Facility = FindInScene("FacilityMusic");
		}
		catch
		{
		}
	}

	private static object FindInScene(string typeName)
	{
		Type type = AccessTools.TypeByName(typeName) ?? AccessTools.TypeByName("Bulbul." + typeName);
		if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type))
		{
			return null;
		}
		UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(type);
		foreach (UnityEngine.Object obj in array)
		{
			Component component = obj as Component;
			if (component != null && component.gameObject.scene.IsValid())
			{
				return obj;
			}
		}
		return null;
	}

	private static object Prop(PropertyInfo p)
	{
		if (Service == null || p == null)
		{
			return null;
		}
		try
		{
			return p.GetValue(Service, null);
		}
		catch
		{
			return null;
		}
	}

	public static void SeekNormalized(float t)
	{
		if (!Bound || _mSeek == null)
		{
			return;
		}
		try
		{
			float num = Mathf.Clamp(t, 0.002f, 0.998f);
			EnterInvoke();
			try
			{
				_mSeek.Invoke(Service, new object[1] { num });
			}
			finally
			{
				ExitInvoke();
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("跳转游戏进度失败：" + ex.Message);
		}
	}

	public static bool TryGet(out string title, out string credit, out float progress)
	{
		title = null;
		credit = null;
		progress = 0f;
		LocalTrack playing = Playing;
		if (playing == null || string.IsNullOrEmpty(playing.Title))
		{
			return false;
		}
		title = playing.Title;
		credit = playing.Credit;
		progress = Progress;
		return true;
	}

	private static LocalTrack FromRaw(object raw, int index)
	{
		LocalTrack localTrack = new LocalTrack
		{
			Index = index,
			Raw = raw
		};
		try
		{
			Type type = raw.GetType();
			if (type != _rawType)
			{
				_rawType = type;
				_fTitle = type.GetField("Title");
				_fCredit = type.GetField("Credit");
				_fClip = type.GetField("AudioClip");
				_fTag = type.GetField("Tag");
				_fLocalPath = type.GetField("LocalPath");
			}
			localTrack.Title = ((_fTitle != null) ? ((_fTitle.GetValue(raw) as string) ?? "") : "");
			localTrack.Credit = ((_fCredit != null) ? ((_fCredit.GetValue(raw) as string) ?? "") : "");
			localTrack.LocalPath = ((_fLocalPath != null) ? ((_fLocalPath.GetValue(raw) as string) ?? "") : "");
			AudioClip audioClip = ((_fClip != null) ? (_fClip.GetValue(raw) as AudioClip) : null);
			if (audioClip != null)
			{
				localTrack.DurationSeconds = audioClip.length;
			}
			else
			{
				LocalFileDuration.TryGetCached(localTrack.LocalPath, out localTrack.DurationSeconds);
			}
			if (_fTag != null)
			{
				localTrack.IsImported = (Convert.ToInt32(_fTag.GetValue(raw)) & 0x10) != 0;
			}
		}
		catch
		{
		}
		return localTrack;
	}

	private static void EnterInvoke()
	{
		_invokeDepth++;
	}

	private static void ExitInvoke()
	{
		if (_invokeDepth > 0)
		{
			_invokeDepth--;
		}
	}

	public static bool InvokeFacility(string method)
	{
		EnsureCaptured();
		if (Facility == null)
		{
			BridgeLog.Warn("游戏 FacilityMusic 实例尚未捕获，按钮无效。");
			return false;
		}
		try
		{
			Type type = Facility.GetType();
			if (type != _facilityType)
			{
				_facilityType = type;
				FacilityMethods.Clear();
			}
			if (!FacilityMethods.TryGetValue(method, out var value))
			{
				value = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				FacilityMethods[method] = value;
			}
			if (value == null)
			{
				BridgeLog.Warn("游戏没有方法 " + method);
				return false;
			}
			EnterInvoke();
			try
			{
				value.Invoke(Facility, null);
			}
			finally
			{
				ExitInvoke();
			}
			return true;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("调用游戏 " + method + " 失败：" + ex.Message);
			return false;
		}
	}

	public static void Pause()
	{
		if (!Bound || _mPause == null)
		{
			return;
		}
		try
		{
			_mPause.Invoke(Service, null);
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("暂停游戏播放器失败：" + ex.Message);
		}
	}
}
