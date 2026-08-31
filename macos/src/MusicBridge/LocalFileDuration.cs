using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace MusicBridge;

internal static class LocalFileDuration
{
	private static bool _resolved;

	private static MethodInfo _mCreate;

	private static MethodInfo _mDispose;

	private static PropertyInfo _pProperties;

	private static PropertyInfo _pDuration;

	private static readonly Dictionary<string, double> Cache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

	private const int MaxCacheEntries = 4096;

	public static bool TryGetCached(string path, out double seconds)
	{
		seconds = 0.0;
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		return Cache.TryGetValue(path, out seconds);
	}

	public static double Get(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return 0.0;
		}
		if (Cache.TryGetValue(path, out var value))
		{
			return value;
		}
		double num = Read(path);
		if (Cache.Count >= 4096)
		{
			Cache.Clear();
		}
		Cache[path] = num;
		return num;
	}

	private static double Read(string path)
	{
		try
		{
			if (!Resolve() || !File.Exists(path))
			{
				return 0.0;
			}
			object obj = _mCreate.Invoke(null, new object[1] { path });
			if (obj == null)
			{
				return 0.0;
			}
			try
			{
				object value = _pProperties.GetValue(obj, null);
				if (value == null)
				{
					return 0.0;
				}
				if (!(_pDuration.GetValue(value, null) is TimeSpan { TotalSeconds: var totalSeconds }))
				{
					return 0.0;
				}
				return (totalSeconds > 0.0) ? totalSeconds : 0.0;
			}
			finally
			{
				try
				{
					if (_mDispose != null)
					{
						_mDispose.Invoke(obj, null);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
			return 0.0;
		}
	}

	private static bool Resolve()
	{
		if (_resolved)
		{
			if (_mCreate != null && _pProperties != null)
			{
				return _pDuration != null;
			}
			return false;
		}
		_resolved = true;
		try
		{
			Type type = AccessTools.TypeByName("TagLib.File");
			if (type == null)
			{
				return false;
			}
			_mCreate = AccessTools.Method(type, "Create", new Type[1] { typeof(string) }, (Type[])null);
			_mDispose = AccessTools.Method(type, "Dispose", Type.EmptyTypes, (Type[])null);
			_pProperties = AccessTools.Property(type, "Properties");
			if (_pProperties == null)
			{
				return false;
			}
			_pDuration = AccessTools.Property(_pProperties.PropertyType, "Duration");
			return _mCreate != null && _pDuration != null;
		}
		catch
		{
			return false;
		}
	}
}
