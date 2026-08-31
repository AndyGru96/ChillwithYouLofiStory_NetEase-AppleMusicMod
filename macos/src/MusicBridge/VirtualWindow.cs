namespace MusicBridge;

internal static class VirtualWindow
{
	public static bool Compute(int itemCount, float pitch, float scrolledPast, float viewHeight, int buffer, out int firstIndex, out int count)
	{
		firstIndex = 0;
		count = 0;
		if (itemCount <= 0)
		{
			return false;
		}
		if (pitch <= 0f)
		{
			firstIndex = 0;
			count = itemCount;
			return true;
		}
		if (viewHeight <= 0f)
		{
			return false;
		}
		if (buffer < 0)
		{
			buffer = 0;
		}
		float num = (float)itemCount * pitch;
		if (scrolledPast >= num)
		{
			return false;
		}
		if (scrolledPast + viewHeight <= 0f)
		{
			return false;
		}
		int num2 = (int)(((scrolledPast < 0f) ? 0f : scrolledPast) / pitch) - buffer;
		if (num2 < 0)
		{
			num2 = 0;
		}
		int num3 = (int)((scrolledPast + viewHeight) / pitch) + 1 + buffer;
		if (num3 > itemCount)
		{
			num3 = itemCount;
		}
		if (num3 <= num2)
		{
			return false;
		}
		firstIndex = num2;
		count = num3 - num2;
		return true;
	}

	public static float OffsetOf(int index, float pitch)
	{
		return (float)index * pitch;
	}

	public static float SegmentHeight(int itemCount, float pitch)
	{
		if (itemCount <= 0 || pitch <= 0f)
		{
			return 0f;
		}
		return (float)itemCount * pitch;
	}
}
