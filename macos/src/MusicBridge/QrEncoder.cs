using System;
using System.Text;

namespace MusicBridge;

internal static class QrEncoder
{
	private sealed class BitBuffer
	{
		private readonly byte[] _bytes;

		public int Length { get; private set; }

		public BitBuffer(int capacityBits)
		{
			_bytes = new byte[(capacityBits + 7) / 8];
		}

		public void Append(int value, int bitCount)
		{
			for (int num = bitCount - 1; num >= 0; num--)
			{
				if (((value >> num) & 1) != 0)
				{
					_bytes[Length >> 3] |= (byte)(1 << 7 - (Length & 7));
				}
				Length++;
			}
		}

		public byte[] ToBytes(int count)
		{
			byte[] array = new byte[count];
			Buffer.BlockCopy(_bytes, 0, array, 0, Math.Min(count, _bytes.Length));
			return array;
		}
	}

	private static readonly int[] DataCodewordsL;

	private static readonly int[] EccCodewordsL;

	private static readonly int[] AlignmentCenter;

	private const int MaxVersion = 6;

	private static readonly byte[] GfExp;

	private static readonly byte[] GfLog;

	private static readonly bool[] FinderLike;

	public static bool[,] Encode(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		int num = -1;
		for (int i = 1; i <= 6; i++)
		{
			if (bytes.Length + 2 <= DataCodewordsL[i])
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return null;
		}
		int num2 = DataCodewordsL[num];
		int num3 = EccCodewordsL[num];
		int num4 = 17 + 4 * num;
		byte[] array = BuildDataCodewords(bytes, num2);
		byte[] src = ReedSolomon(array, num3);
		byte[] array2 = new byte[num2 + num3];
		Buffer.BlockCopy(array, 0, array2, 0, num2);
		Buffer.BlockCopy(src, 0, array2, num2, num3);
		bool[,] result = null;
		int num5 = int.MaxValue;
		for (int j = 0; j < 8; j++)
		{
			bool[,] array3 = new bool[num4, num4];
			bool[,] reserved = new bool[num4, num4];
			DrawFunctionPatterns(array3, reserved, num, num4);
			PlaceData(array3, reserved, array2, num4);
			ApplyMask(array3, reserved, j, num4);
			DrawFormatInfo(array3, j, num4);
			int num6 = Penalty(array3, num4);
			if (num6 < num5)
			{
				num5 = num6;
				result = array3;
			}
		}
		return result;
	}

	private static byte[] BuildDataCodewords(byte[] data, int dataCount)
	{
		BitBuffer bitBuffer = new BitBuffer(dataCount * 8);
		bitBuffer.Append(4, 4);
		bitBuffer.Append(data.Length, 8);
		foreach (byte value in data)
		{
			bitBuffer.Append(value, 8);
		}
		int num = dataCount * 8;
		int bitCount = Math.Min(4, num - bitBuffer.Length);
		bitBuffer.Append(0, bitCount);
		while (bitBuffer.Length % 8 != 0)
		{
			bitBuffer.Append(0, 1);
		}
		byte[] array = bitBuffer.ToBytes(dataCount);
		bool flag = true;
		for (int j = bitBuffer.Length / 8; j < dataCount; j++)
		{
			array[j] = (byte)(flag ? 236 : 17);
			flag = !flag;
		}
		return array;
	}

	static QrEncoder()
	{
		DataCodewordsL = new int[7] { 0, 19, 34, 55, 80, 108, 136 };
		EccCodewordsL = new int[7] { 0, 7, 10, 15, 20, 26, 36 };
		AlignmentCenter = new int[7] { 0, -1, 18, 22, 26, 30, 34 };
		GfExp = new byte[512];
		GfLog = new byte[256];
		FinderLike = new bool[11]
		{
			true, false, true, true, true, false, true, false, false, false,
			false
		};
		int num = 1;
		for (int i = 0; i < 255; i++)
		{
			GfExp[i] = (byte)num;
			GfLog[num] = (byte)i;
			num <<= 1;
			if ((num & 0x100) != 0)
			{
				num ^= 0x11D;
			}
		}
		for (int j = 255; j < 512; j++)
		{
			GfExp[j] = GfExp[j - 255];
		}
	}

	private static byte GfMul(byte a, byte b)
	{
		if (a == 0 || b == 0)
		{
			return 0;
		}
		return GfExp[GfLog[a] + GfLog[b]];
	}

	private static byte[] ReedSolomon(byte[] data, int eccCount)
	{
		byte[] array = new byte[eccCount + 1];
		array[0] = 1;
		for (int i = 0; i < eccCount; i++)
		{
			for (int num = i + 1; num > 0; num--)
			{
				array[num] = (byte)(array[num - 1] ^ GfMul(array[num], GfExp[i]));
			}
			array[0] = GfMul(array[0], GfExp[i]);
		}
		byte[] array2 = new byte[eccCount];
		for (int j = 0; j < data.Length; j++)
		{
			byte b = (byte)(data[j] ^ array2[0]);
			Array.Copy(array2, 1, array2, 0, eccCount - 1);
			array2[eccCount - 1] = 0;
			for (int k = 0; k < eccCount; k++)
			{
				array2[k] ^= GfMul(array[eccCount - 1 - k], b);
			}
		}
		return array2;
	}

	private static void DrawFunctionPatterns(bool[,] m, bool[,] reserved, int version, int size)
	{
		DrawFinder(m, reserved, 0, 0, size);
		DrawFinder(m, reserved, size - 7, 0, size);
		DrawFinder(m, reserved, 0, size - 7, size);
		for (int i = 8; i < size - 8; i++)
		{
			bool flag = (m[i, 6] = i % 2 == 0);
			reserved[i, 6] = true;
			m[6, i] = flag;
			reserved[6, i] = true;
		}
		int num = AlignmentCenter[version];
		if (num > 0)
		{
			DrawAlignment(m, reserved, num, num);
		}
		m[8, size - 8] = true;
		reserved[8, size - 8] = true;
		for (int j = 0; j <= 8; j++)
		{
			if (j != 6)
			{
				reserved[j, 8] = true;
				reserved[8, j] = true;
			}
		}
		for (int k = 0; k < 8; k++)
		{
			reserved[size - 1 - k, 8] = true;
			reserved[8, size - 1 - k] = true;
		}
	}

	private static void DrawFinder(bool[,] m, bool[,] reserved, int x0, int y0, int size)
	{
		for (int i = -1; i <= 7; i++)
		{
			for (int j = -1; j <= 7; j++)
			{
				int num = x0 + j;
				int num2 = y0 + i;
				if (num >= 0 && num2 >= 0 && num < size && num2 < size)
				{
					bool flag = j >= 0 && j <= 6 && i >= 0 && i <= 6 && (j == 0 || j == 6 || i == 0 || i == 6 || (j >= 2 && j <= 4 && i >= 2 && i <= 4));
					m[num, num2] = flag;
					reserved[num, num2] = true;
				}
			}
		}
	}

	private static void DrawAlignment(bool[,] m, bool[,] reserved, int cx, int cy)
	{
		for (int i = -2; i <= 2; i++)
		{
			for (int j = -2; j <= 2; j++)
			{
				bool flag = Math.Max(Math.Abs(j), Math.Abs(i)) != 1;
				m[cx + j, cy + i] = flag;
				reserved[cx + j, cy + i] = true;
			}
		}
	}

	private static void PlaceData(bool[,] m, bool[,] reserved, byte[] codewords, int size)
	{
		int num = 0;
		int num2 = codewords.Length * 8;
		bool flag = true;
		for (int num3 = size - 1; num3 >= 1; num3 -= 2)
		{
			if (num3 == 6)
			{
				num3 = 5;
			}
			for (int i = 0; i < size; i++)
			{
				int num4 = (flag ? (size - 1 - i) : i);
				for (int j = 0; j < 2; j++)
				{
					int num5 = num3 - j;
					if (!reserved[num5, num4])
					{
						bool flag2 = false;
						if (num < num2)
						{
							flag2 = ((codewords[num >> 3] >> 7 - (num & 7)) & 1) == 1;
							num++;
						}
						m[num5, num4] = flag2;
					}
				}
			}
			flag = !flag;
		}
	}

	private static bool MaskCondition(int mask, int x, int y)
	{
		return mask switch
		{
			0 => (y + x) % 2 == 0,
			1 => y % 2 == 0,
			2 => x % 3 == 0,
			3 => (y + x) % 3 == 0,
			4 => (y / 2 + x / 3) % 2 == 0,
			5 => y * x % 2 + y * x % 3 == 0,
			6 => (y * x % 2 + y * x % 3) % 2 == 0,
			7 => ((y + x) % 2 + y * x % 3) % 2 == 0,
			_ => false,
		};
	}

	private static void ApplyMask(bool[,] m, bool[,] reserved, int mask, int size)
	{
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				if (!reserved[j, i] && MaskCondition(mask, j, i))
				{
					m[j, i] = !m[j, i];
				}
			}
		}
	}

	private static void DrawFormatInfo(bool[,] m, int mask, int size)
	{
		int num = 8 | mask;
		int num2 = num;
		for (int i = 0; i < 10; i++)
		{
			num2 = (num2 << 1) ^ (((num2 >> 9) & 1) * 1335);
		}
		int value = ((num << 10) | num2) ^ 0x5412;
		for (int j = 0; j <= 5; j++)
		{
			m[8, j] = GetBit(value, j);
		}
		m[8, 7] = GetBit(value, 6);
		m[8, 8] = GetBit(value, 7);
		m[7, 8] = GetBit(value, 8);
		for (int k = 9; k <= 14; k++)
		{
			m[14 - k, 8] = GetBit(value, k);
		}
		for (int l = 0; l < 8; l++)
		{
			m[size - 1 - l, 8] = GetBit(value, l);
		}
		for (int n = 8; n < 15; n++)
		{
			m[8, size - 15 + n] = GetBit(value, n);
		}
		m[8, size - 8] = true;
	}

	private static bool GetBit(int value, int index)
	{
		return ((value >> index) & 1) == 1;
	}

	private static int Penalty(bool[,] m, int size)
	{
		int num = 0;
		for (int i = 0; i < size; i++)
		{
			num += RunPenalty(m, size, i, horizontal: true);
			num += RunPenalty(m, size, i, horizontal: false);
		}
		for (int j = 0; j < size - 1; j++)
		{
			for (int k = 0; k < size - 1; k++)
			{
				if (m[k, j] == m[k + 1, j] && m[k, j] == m[k, j + 1] && m[k, j] == m[k + 1, j + 1])
				{
					num += 3;
				}
			}
		}
		for (int l = 0; l < size; l++)
		{
			for (int n = 0; n < size; n++)
			{
				if (n + 10 < size && MatchFinderLike(m, n, l, horizontal: true))
				{
					num += 40;
				}
				if (l + 10 < size && MatchFinderLike(m, n, l, horizontal: false))
				{
					num += 40;
				}
			}
		}
		int num2 = 0;
		for (int num3 = 0; num3 < size; num3++)
		{
			for (int num4 = 0; num4 < size; num4++)
			{
				if (m[num4, num3])
				{
					num2++;
				}
			}
		}
		int num5 = num2 * 100 / (size * size);
		return num + Math.Abs(num5 - 50) / 5 * 10;
	}

	private static int RunPenalty(bool[,] m, int size, int line, bool horizontal)
	{
		int num = 0;
		int num2 = 1;
		bool flag = (horizontal ? m[0, line] : m[line, 0]);
		for (int i = 1; i < size; i++)
		{
			bool flag2 = (horizontal ? m[i, line] : m[line, i]);
			if (flag2 == flag)
			{
				num2++;
				continue;
			}
			if (num2 >= 5)
			{
				num += 3 + (num2 - 5);
			}
			flag = flag2;
			num2 = 1;
		}
		if (num2 >= 5)
		{
			num += 3 + (num2 - 5);
		}
		return num;
	}

	private static bool MatchFinderLike(bool[,] m, int x, int y, bool horizontal)
	{
		for (int i = 0; i < 11; i++)
		{
			if ((horizontal ? m[x + i, y] : m[x, y + i]) != FinderLike[i])
			{
				return MatchReversed(m, x, y, horizontal);
			}
		}
		return true;
	}

	private static bool MatchReversed(bool[,] m, int x, int y, bool horizontal)
	{
		for (int i = 0; i < 11; i++)
		{
			if ((horizontal ? m[x + i, y] : m[x, y + i]) != FinderLike[10 - i])
			{
				return false;
			}
		}
		return true;
	}
}
