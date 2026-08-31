using System;
using System.Collections.Generic;

namespace MusicBridge;

internal static class PlaylistAssembly
{
	internal struct BatchRange
	{
		public int Start;

		public int Count;
	}

	public static List<BatchRange> Split(int total, int batchSize)
	{
		List<BatchRange> list = new List<BatchRange>();
		if (total <= 0)
		{
			return list;
		}
		if (batchSize <= 0)
		{
			batchSize = total;
		}
		for (int i = 0; i < total; i += batchSize)
		{
			int num = total - i;
			if (num > batchSize)
			{
				num = batchSize;
			}
			list.Add(new BatchRange
			{
				Start = i,
				Count = num
			});
		}
		return list;
	}

	public static List<long> MissingIds(IList<long> requested, IList<long> returnedIds)
	{
		List<long> list = new List<long>();
		if (requested == null)
		{
			return list;
		}
		HashSet<long> hashSet = new HashSet<long>();
		if (returnedIds != null)
		{
			foreach (long returnedId in returnedIds)
			{
				hashSet.Add(returnedId);
			}
		}
		foreach (long item in requested)
		{
			if (!hashSet.Contains(item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static string Summary(int declared, int loaded, int missing, bool aborted)
	{
		if (aborted)
		{
			if (loaded <= 0)
			{
				return "加载未完成，点这里重试";
			}
			return "已加载 " + loaded + " / " + declared + " 首，加载未完成，点这里重试";
		}
		if (loaded <= 0)
		{
			if (missing <= 0)
			{
				return "这个歌单是空的";
			}
			return "这个歌单的 " + missing + " 首曲目都已失效";
		}
		if (missing > 0)
		{
			return "共 " + loaded + " 首（另有 " + missing + " 首已失效，不显示）";
		}
		return "共 " + loaded + " 首（已全部加载）";
	}

	public static string RowKey(bool isAlbum, long id)
	{
		return (isAlbum ? "A" : "P") + id;
	}

	public static int FirstPlayable(int count, int start, bool wrap, bool forward, Func<int, bool> isPlayable, out int skipped)
	{
		skipped = 0;
		if (count <= 0 || isPlayable == null)
		{
			return -1;
		}
		if (start < 0 || start >= count)
		{
			if (!wrap)
			{
				return -1;
			}
			start = (start % count + count) % count;
		}
		for (int i = 0; i < count; i++)
		{
			int num = (forward ? (start + i) : (start - i));
			if (num < 0 || num >= count)
			{
				if (!wrap)
				{
					return -1;
				}
				num = (num % count + count) % count;
			}
			if (isPlayable(num))
			{
				return num;
			}
			skipped++;
		}
		return -1;
	}
}
