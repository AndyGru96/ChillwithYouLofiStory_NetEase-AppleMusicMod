using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBridge;

internal static class TextMatch
{
	private static readonly string[] DerivativeMarkers = new string[18]
	{
		"cover", "翻唱", "翻奏", "伴奏", "instrumental", "karaoke", "卡拉ok", "remix", "混音", "live",
		"现场", "演唱会", "钢琴版", "吉他版", "改编", "纯音乐版", "acoustic", "unplugged"
	};

	public static string Normalize(string s, MatchStrength strength)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return strength switch
		{
			MatchStrength.Exact => s.Trim().ToLowerInvariant(),
			MatchStrength.Loose => Squash(s, keepBracketContent: true).Normalize(NormalizationForm.FormKC),
			_ => Squash(s, keepBracketContent: false).Normalize(NormalizationForm.FormKC),
		};
	}

	private static string Squash(string s, bool keepBracketContent)
	{
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		int num = 0;
		foreach (char c in s)
		{
			bool flag = c == '(' || c == '（' || c == '[' || c == '【';
			bool flag2 = c == ')' || c == '）' || c == ']' || c == '】';
			if (!keepBracketContent)
			{
				if (flag)
				{
					num++;
					continue;
				}
				if (flag2)
				{
					if (num > 0)
					{
						num--;
					}
					continue;
				}
				if (num > 0)
				{
					continue;
				}
			}
			if (!char.IsWhiteSpace(c))
			{
				UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark && unicodeCategory != UnicodeCategory.SpacingCombiningMark && !char.IsPunctuation(c) && !char.IsSymbol(c))
				{
					stringBuilder.Append(char.ToLowerInvariant(c));
				}
			}
		}
		return stringBuilder.ToString();
	}

	public static string Canon(string s)
	{
		return Normalize(s, MatchStrength.Canonical);
	}

	public static string Loose(string s)
	{
		return Normalize(s, MatchStrength.Loose);
	}

	public static bool AliasExact(string field, string canonicalWant)
	{
		if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(canonicalWant))
		{
			return false;
		}
		string[] array = field.Split('/');
		for (int i = 0; i < array.Length; i++)
		{
			if (Canon(array[i]) == canonicalWant)
			{
				return true;
			}
		}
		return false;
	}

	public static bool Equals(string a, string b, MatchStrength strength)
	{
		string text = Normalize(a, strength);
		string text2 = Normalize(b, strength);
		if (text.Length > 0)
		{
			return text == text2;
		}
		return false;
	}

	public static bool Contains(string a, string b, MatchStrength strength)
	{
		string text = Normalize(a, strength);
		string text2 = Normalize(b, strength);
		if (text.Length == 0 || text2.Length == 0)
		{
			return false;
		}
		if (!(text == text2) && !text.Contains(text2))
		{
			return text2.Contains(text);
		}
		return true;
	}

	public static int Rate(string field, IList<string> candidates, MatchStrength strength)
	{
		if (string.IsNullOrEmpty(field) || candidates == null)
		{
			return 0;
		}
		string text = Normalize(field, strength);
		if (text.Length == 0)
		{
			return 0;
		}
		int result = 0;
		foreach (string candidate in candidates)
		{
			string text2 = Normalize(candidate, strength);
			if (text2.Length != 0)
			{
				if (text == text2)
				{
					return 2;
				}
				if (text.Contains(text2) || text2.Contains(text))
				{
					result = 1;
				}
			}
		}
		return result;
	}

	public static int RateMultiValue(string field, IList<string> candidates, MatchStrength strength)
	{
		if (string.IsNullOrEmpty(field) || candidates == null)
		{
			return 0;
		}
		int result = 0;
		string[] array = field.Split('/');
		for (int i = 0; i < array.Length; i++)
		{
			switch (Rate(array[i], candidates, strength))
			{
			case 2:
				return 2;
			case 1:
				result = 1;
				break;
			}
		}
		return result;
	}

	public static List<string> ArtistTokens(string value)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(value))
		{
			return list;
		}
		string[] array = Regex.Split(Regex.Replace(value, "\\s+(?:feat\\.?|featuring|with)\\s+", "/", RegexOptions.IgnoreCase), "\\s*(?:/|&|＆|、|,|，|\\band\\b)\\s*", RegexOptions.IgnoreCase);
		for (int i = 0; i < array.Length; i++)
		{
			string text = Normalize(array[i], MatchStrength.Canonical);
			if (text.Length > 0 && !list.Contains(text))
			{
				list.Add(text);
			}
		}
		return list;
	}

	public static int RateArtists(string field, IList<string> candidates)
	{
		int num = RateMultiValue(field, candidates, MatchStrength.Canonical);
		List<string> list = ArtistTokens(field);
		if (list.Count == 0 || candidates == null)
		{
			return num;
		}
		foreach (string candidate in candidates)
		{
			List<string> list2 = ArtistTokens(candidate);
			if (list2.Count == 0)
			{
				continue;
			}
			if (SameSet(list, list2))
			{
				return 2;
			}
			foreach (string item in list)
			{
				if (list2.Contains(item))
				{
					num = Math.Max(num, 1);
					break;
				}
			}
		}
		return num;
	}

	private static bool SameSet(List<string> a, List<string> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}
		foreach (string item in a)
		{
			if (!b.Contains(item))
			{
				return false;
			}
		}
		return true;
	}

	public static bool MentionsArtist(string text, IList<string> artists)
	{
		if (string.IsNullOrWhiteSpace(text) || artists == null)
		{
			return false;
		}
		string text2 = Normalize(text, MatchStrength.Loose);
		foreach (string artist in artists)
		{
			foreach (string item in ArtistTokens(artist))
			{
				if (item.Length < 2)
				{
					continue;
				}
				bool flag = true;
				string text3 = item;
				for (int i = 0; i < text3.Length; i++)
				{
					if (!char.IsDigit(text3[i]))
					{
						flag = false;
						break;
					}
				}
				if (!flag && text2.Contains(item))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsDerivativeVersion(string title, string album)
	{
		foreach (string item in BracketSegments(title))
		{
			if (HasDerivativeMarker(item))
			{
				return true;
			}
		}
		return HasDerivativeMarker(album);
	}

	public static bool HasDerivativeMarker(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = Normalize(text, MatchStrength.Loose);
		string[] derivativeMarkers = DerivativeMarkers;
		foreach (string value in derivativeMarkers)
		{
			if (text2.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	public static List<string> BracketSegments(string s)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(s))
		{
			return list;
		}
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		foreach (char c in s)
		{
			switch (c)
			{
			case '(':
			case '[':
			case '《':
			case '【':
			case '（':
				if (num == 0)
				{
					stringBuilder.Length = 0;
				}
				num++;
				break;
			case ')':
			case ']':
			case '》':
			case '】':
			case '）':
				if (num > 0 && --num == 0 && stringBuilder.Length > 0)
				{
					list.Add(stringBuilder.ToString());
				}
				break;
			default:
				if (num > 0)
				{
					stringBuilder.Append(c);
				}
				break;
			}
		}
		return list;
	}

	public static string StripTrailingAlbum(string smtcArtist)
	{
		if (string.IsNullOrEmpty(smtcArtist))
		{
			return "";
		}
		int num = smtcArtist.IndexOf('—');
		if (num <= 0)
		{
			return smtcArtist;
		}
		return smtcArtist.Substring(0, num);
	}
}
