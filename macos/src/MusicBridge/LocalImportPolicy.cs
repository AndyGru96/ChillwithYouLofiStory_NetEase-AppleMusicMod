namespace MusicBridge;

internal static class LocalImportPolicy
{
	private static bool _wanted;

	private static string _disabledReason;

	public static bool Unlimited
	{
		get
		{
			if (_wanted && _disabledReason == null && LocalPersistence.Ready && LocalLibraryStore.Healthy)
			{
				return LocalImportLimit.Patched;
			}
			return false;
		}
	}

	public static int ComparisonLimit
	{
		get
		{
			if (!Unlimited)
			{
				return 100;
			}
			return int.MaxValue;
		}
	}

	public static void Enable()
	{
		_wanted = true;
	}

	public static void Disable(string why)
	{
		if (_disabledReason == null)
		{
			_disabledReason = why ?? "未说明";
			BridgeLog.Warn("超额导入已停用，导入上限退回 " + 100 + " 首：" + _disabledReason);
		}
	}

	public static string Describe()
	{
		if (Unlimited)
		{
			return "已启用（无导入上限，原生存档保留前 " + 100 + " 首）";
		}
		if (!_wanted)
		{
			return "未开启（配置 Local.UnlimitedImport = false）";
		}
		if (_disabledReason != null)
		{
			return "已停用：" + _disabledReason;
		}
		if (!LocalPersistence.Ready)
		{
			return "未生效：持久化协调器未就位";
		}
		if (!LocalLibraryStore.Healthy)
		{
			return "未生效：侧载库不可用";
		}
		return "未生效：IL 补丁未命中";
	}
}
