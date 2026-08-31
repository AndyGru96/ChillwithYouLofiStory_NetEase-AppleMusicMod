using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace MusicBridge;

internal static class BridgeLog
{
	private static readonly object Gate = new object();

	private static ManualLogSource _console;

	private static string _dir;

	private static string _file;

	private static DateTime _fileDay = DateTime.MinValue;

	private static readonly Dictionary<string, DateTime> LastWarning = new Dictionary<string, DateTime>();

	private const string Prefix = "musicbridge-";

	private static readonly Regex DayFilePattern = new Regex("^musicbridge-(\\d{4}-\\d{2}-\\d{2})(\\.\\d+)?\\.log$", RegexOptions.IgnoreCase);

	private static readonly Regex AbsolutePath = new Regex("[A-Za-z]:\\\\[^\\s\"'()（），,;]{2,240}", RegexOptions.Compiled);

	private static bool Verbose
	{
		get
		{
			try
			{
				return MusicBridgeOptions.Current.Debug.VerboseListeningHistory;
			}
			catch
			{
				return false;
			}
		}
	}

	public static void Init(ManualLogSource console)
	{
		_console = console;
		try
		{
			_dir = BridgePaths.Logs;
			lock (Gate)
			{
				OpenForToday();
				int num = 0;
				string text = "?";
				try
				{
					using Process process = Process.GetCurrentProcess();
					num = process.Id;
					text = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
				}
				catch
				{
				}
				File.AppendAllText(_file, Environment.NewLine + "===== MusicBridge session start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  游戏进程 PID=" + num + "  进程启动于 " + text + " =====" + Environment.NewLine, Encoding.UTF8);
				PurgeExpired();
			}
		}
		catch (Exception ex)
		{
			ManualLogSource console2 = _console;
			if (console2 != null)
			{
				console2.LogWarning((object)("[MusicBridge] 无法打开日志文件: " + ex.Message));
			}
			_file = null;
		}
	}

	public static void Info(string message)
	{
		Write("INFO ", message, isError: false);
	}

	public static void Warn(string message)
	{
		Write("WARN ", message, isError: false);
	}

	public static void Error(string message)
	{
		Write("ERROR", message, isError: true);
	}

	public static void History(string message)
	{
		if (Verbose)
		{
			Info(message);
		}
	}

	public static string Redact(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "(空)";
		}
		if (Verbose)
		{
			return value;
		}
		uint num = 2166136261u;
		foreach (char c in value)
		{
			num ^= c;
			num *= 16777619;
		}
		return "名#" + num.ToString("X8").Substring(0, 4);
	}

	public static void WarnThrottled(string key, string message, TimeSpan interval)
	{
		lock (Gate)
		{
			if (LastWarning.TryGetValue(key, out var value) && DateTime.UtcNow - value < interval)
			{
				return;
			}
			LastWarning[key] = DateTime.UtcNow;
		}
		Warn(message);
	}

	private static string Scrub(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			return message;
		}
		try
		{
			string root = BridgePaths.Root;
			if (!string.IsNullOrEmpty(root))
			{
				message = message.Replace(root, "<插件目录>");
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(folderPath))
			{
				message = message.Replace(folderPath, "<用户目录>");
			}
			message = AbsolutePath.Replace(message, delegate(Match m)
			{
				string value = m.Value;
				int num = value.LastIndexOf('\\');
				string text = ((num >= 0 && num + 1 < value.Length) ? value.Substring(num + 1) : "");
				return (text.Length <= 0) ? "<路径>" : ("<路径>\\" + text);
			});
		}
		catch
		{
		}
		return message;
	}

	private static void Write(string level, string message, bool isError)
	{
		message = Scrub(message);
		string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message;
		try
		{
			if (isError)
			{
				ManualLogSource console = _console;
				if (console != null)
				{
					console.LogError((object)("[MusicBridge] " + message));
				}
			}
			else
			{
				ManualLogSource console2 = _console;
				if (console2 != null)
				{
					console2.LogInfo((object)("[MusicBridge] " + message));
				}
			}
		}
		catch
		{
		}
		if (_file == null)
		{
			return;
		}
		try
		{
			lock (Gate)
			{
				if (OpenForToday())
				{
					File.AppendAllText(_file, Environment.NewLine + "===== 跨日续写 " + DateTime.Now.ToString("yyyy-MM-dd") + " =====" + Environment.NewLine, Encoding.UTF8);
				}
				RotateIfOversized();
				File.AppendAllText(_file, text + Environment.NewLine, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}

	private static bool OpenForToday()
	{
		DateTime date = DateTime.Now.Date;
		if (_file != null && _fileDay == date)
		{
			return false;
		}
		bool result = _file != null;
		_fileDay = date;
		_file = Path.Combine(_dir, "musicbridge-" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
		return result;
	}

	private static void RotateIfOversized()
	{
		if (string.IsNullOrEmpty(_file) || !File.Exists(_file) || new FileInfo(_file).Length < MusicBridgeOptions.Current.Shared.LogMaximumFileBytes)
		{
			return;
		}
		string text = Path.Combine(_dir, "musicbridge-" + _fileDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		for (int i = 1; i < 1000; i++)
		{
			string text2 = text + "." + i + ".log";
			if (!File.Exists(text2))
			{
				File.Move(_file, text2);
				break;
			}
		}
	}

	private static void PurgeExpired()
	{
		int logRetainDays;
		try
		{
			logRetainDays = MusicBridgeOptions.Current.Shared.LogRetainDays;
		}
		catch
		{
			return;
		}
		if (logRetainDays < 1)
		{
			return;
		}
		DateTime dateTime = DateTime.Now.Date.AddDays(-logRetainDays);
		string fullPath;
		string[] files;
		try
		{
			fullPath = Path.GetFullPath(_dir);
			files = Directory.GetFiles(_dir, "musicbridge-*.log");
		}
		catch
		{
			return;
		}
		int num = 0;
		string[] array = files;
		foreach (string path in array)
		{
			Match match = DayFilePattern.Match(Path.GetFileName(path));
			if (!match.Success || !DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) || result >= dateTime)
			{
				continue;
			}
			string fullPath2;
			try
			{
				fullPath2 = Path.GetFullPath(path);
			}
			catch
			{
				continue;
			}
			if (fullPath2.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath2, _file, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					File.Delete(fullPath2);
					num++;
				}
				catch
				{
				}
			}
		}
		if (num > 0)
		{
			Info("日志清理：删除了 " + num + " 个超过 " + logRetainDays + " 天的日志文件。");
		}
	}
}
