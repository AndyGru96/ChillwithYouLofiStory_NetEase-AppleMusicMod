using System.Globalization;

namespace MusicBridge;

internal static class DurationText
{
	public static bool TryParseSeconds(string text, out double seconds)
	{
		seconds = 0.0;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string[] array = text.Trim().Split(':');
		if (array.Length < 2 || array.Length > 3)
		{
			return false;
		}
		double num = 0.0;
		for (int i = 0; i < array.Length; i++)
		{
			if (!int.TryParse(array[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			if (i > 0 && result >= 60)
			{
				return false;
			}
			num = num * 60.0 + (double)result;
		}
		seconds = num;
		return seconds > 0.0;
	}

	public static bool LooksLikeDuration(string s)
	{
		if (string.IsNullOrEmpty(s) || s.Length < 3 || s.Length > 8)
		{
			return false;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < s.Length; i++)
		{
			switch (s[i])
			{
			case ':':
				if (num2 == 0)
				{
					return false;
				}
				if (num > 0 && num2 != 2)
				{
					return false;
				}
				num++;
				num2 = 0;
				break;
			default:
				return false;
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				if (++num2 > 2)
				{
					return false;
				}
				break;
			}
		}
		if (num >= 1 && num <= 2)
		{
			return num2 == 2;
		}
		return false;
	}
}
