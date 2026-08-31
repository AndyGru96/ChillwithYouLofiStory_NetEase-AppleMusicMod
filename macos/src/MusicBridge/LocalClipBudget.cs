using System.Collections.Generic;

namespace MusicBridge;

internal static class LocalClipBudget
{
	public static int ComputeCut(IList<long> sizesOldestFirst, int maxCount, long maxBytes)
	{
		if (sizesOldestFirst == null || sizesOldestFirst.Count == 0)
		{
			return 0;
		}
		if (maxCount < 1)
		{
			maxCount = 1;
		}
		if (maxBytes < 0)
		{
			maxBytes = 0L;
		}
		int num = 0;
		long num2 = 0L;
		for (int num3 = sizesOldestFirst.Count - 1; num3 >= 0; num3--)
		{
			long num4 = sizesOldestFirst[num3];
			if (num4 < 0)
			{
				num4 = 0L;
			}
			if (num > 0 && (num + 1 > maxCount || num2 + num4 > maxBytes))
			{
				return num3 + 1;
			}
			num++;
			num2 += num4;
		}
		return 0;
	}
}
