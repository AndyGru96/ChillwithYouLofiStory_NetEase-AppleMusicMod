using System;
using System.IO;
using System.Reflection;

namespace MusicBridge;

internal static class BridgePaths
{
	private static string _root;

	public static string Root
	{
		get
		{
			if (_root != null)
			{
				return _root;
			}
			_root = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
			return _root;
		}
	}

	public static string Logs => EnsureDir(Path.Combine(Root, "logs"));

	public static string Config => EnsureDir(Path.Combine(Root, "config"));

	public static string Resolve(params string[] relativeParts)
	{
		string text = Root;
		foreach (string path in relativeParts)
		{
			text = Path.Combine(text, path);
		}
		string fullPath = Path.GetFullPath(text);
		string value = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(value, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("MusicBridge boundary violation: " + fullPath);
		}
		return fullPath;
	}

	public static string ValidateWritePath(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			throw new ArgumentException("MusicBridge: 写入路径为空。");
		}
		string fullPath = Path.GetFullPath(path);
		string text = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!fullPath.StartsWith(text, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("MusicBridge boundary violation: " + fullPath);
		}
		string text2 = Root.TrimEnd(Path.DirectorySeparatorChar);
		string[] array = fullPath.Substring(text.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		foreach (string text3 in array)
		{
			if (text3.Length != 0)
			{
				text2 = Path.Combine(text2, text3);
				FileAttributes attributes;
				try
				{
					attributes = File.GetAttributes(text2);
				}
				catch
				{
					break;
				}
				if ((attributes & FileAttributes.ReparsePoint) != 0)
				{
					throw new InvalidOperationException("MusicBridge boundary violation: 路径上存在重解析点，拒绝写入。");
				}
			}
		}
		return fullPath;
	}

	private static string EnsureDir(string path)
	{
		string text = ValidateWritePath(path);
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		return text;
	}
}
