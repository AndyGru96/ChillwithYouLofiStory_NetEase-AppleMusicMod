using System.Collections.Generic;

namespace MusicBridge;

internal static class LocalLibraryMerge
{
	public static List<string> Project(IList<string> allTrackIds, int keep)
	{
		List<string> list = new List<string>();
		if (allTrackIds == null)
		{
			return list;
		}
		if (keep < 0)
		{
			keep = 0;
		}
		for (int i = 0; i < allTrackIds.Count; i++)
		{
			if (list.Count >= keep)
			{
				break;
			}
			string text = allTrackIds[i];
			if (!string.IsNullOrEmpty(text))
			{
				list.Add(text);
			}
		}
		return list;
	}

	public static List<string> MergeTracks(IList<string> nativeIds, IList<string> snapshotIds, IList<string> lastNativeProjection)
	{
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>();
		if (nativeIds != null)
		{
			foreach (string nativeId in nativeIds)
			{
				if (!string.IsNullOrEmpty(nativeId) && hashSet.Add(nativeId))
				{
					list.Add(nativeId);
				}
			}
		}
		HashSet<string> hashSet2 = new HashSet<string>();
		if (lastNativeProjection != null)
		{
			foreach (string item in lastNativeProjection)
			{
				if (!string.IsNullOrEmpty(item))
				{
					hashSet2.Add(item);
				}
			}
		}
		if (snapshotIds != null)
		{
			foreach (string snapshotId in snapshotIds)
			{
				if (!string.IsNullOrEmpty(snapshotId) && !hashSet.Contains(snapshotId) && !hashSet2.Contains(snapshotId) && hashSet.Add(snapshotId))
				{
					list.Add(snapshotId);
				}
			}
		}
		return list;
	}

	public static List<string> MergeOrder(IList<string> nativeOrder, IList<string> snapshotOrder, ICollection<string> existingIds)
	{
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>();
		if (snapshotOrder != null && existingIds != null)
		{
			foreach (string item in snapshotOrder)
			{
				if (!string.IsNullOrEmpty(item) && existingIds.Contains(item) && hashSet.Add(item))
				{
					list.Add(item);
				}
			}
		}
		if (nativeOrder != null)
		{
			foreach (string item2 in nativeOrder)
			{
				if (!string.IsNullOrEmpty(item2) && hashSet.Add(item2))
				{
					list.Add(item2);
				}
			}
		}
		return list;
	}

	public static List<string> Filter(IList<string> list, ICollection<string> drop)
	{
		List<string> list2 = new List<string>();
		if (list == null)
		{
			return list2;
		}
		for (int i = 0; i < list.Count; i++)
		{
			string text = list[i];
			if (drop == null || text == null || !drop.Contains(text))
			{
				list2.Add(text);
			}
		}
		return list2;
	}

	public static HashSet<string> OverflowSet(IList<string> allTrackIds, ICollection<string> projected)
	{
		HashSet<string> hashSet = new HashSet<string>();
		if (allTrackIds == null)
		{
			return hashSet;
		}
		foreach (string allTrackId in allTrackIds)
		{
			if (!string.IsNullOrEmpty(allTrackId) && (projected == null || !projected.Contains(allTrackId)))
			{
				hashSet.Add(allTrackId);
			}
		}
		return hashSet;
	}
}
