using UnityEngine;

namespace MusicBridge;

internal static class UiSprites
{
	private static Sprite _circle;

	private static Sprite _ring;

	private static Sprite _pill;

	private static Sprite _pillOutline;

	private static Sprite _bar;

	private static Sprite _rounded;

	private static Sprite _roundedOutline;

	public static Sprite Circle
	{
		get
		{
			if (!(_circle != null))
			{
				return _circle = Make(64, 64, 31.5f, 0f, Vector4.zero);
			}
			return _circle;
		}
	}

	public static Sprite Ring
	{
		get
		{
			if (!(_ring != null))
			{
				return _ring = Make(64, 64, 31.5f, 4f, Vector4.zero);
			}
			return _ring;
		}
	}

	public static Sprite Pill
	{
		get
		{
			if (!(_pill != null))
			{
				return _pill = Make(64, 30, 14.5f, 0f, new Vector4(15f, 0f, 15f, 0f));
			}
			return _pill;
		}
	}

	public static Sprite PillOutline
	{
		get
		{
			if (!(_pillOutline != null))
			{
				return _pillOutline = Make(64, 30, 14.5f, 2f, new Vector4(15f, 0f, 15f, 0f));
			}
			return _pillOutline;
		}
	}

	public static Sprite Bar
	{
		get
		{
			if (!(_bar != null))
			{
				return _bar = Make(16, 6, 2.5f, 0f, new Vector4(3f, 0f, 3f, 0f));
			}
			return _bar;
		}
	}

	public static Sprite Rounded
	{
		get
		{
			if (!(_rounded != null))
			{
				return _rounded = Make(24, 24, 6f, 0f, new Vector4(8f, 8f, 8f, 8f));
			}
			return _rounded;
		}
	}

	public static Sprite RoundedOutline
	{
		get
		{
			if (!(_roundedOutline != null))
			{
				return _roundedOutline = Make(24, 24, 6f, 2f, new Vector4(8f, 8f, 8f, 8f));
			}
			return _roundedOutline;
		}
	}

	private static Sprite Make(int w, int h, float radius, float stroke, Vector4 border)
	{
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		float num = (float)w * 0.5f;
		float num2 = (float)h * 0.5f;
		float num3 = num - 0.5f;
		float num4 = num2 - 0.5f;
		Color[] array = new Color[w * h];
		for (int i = 0; i < h; i++)
		{
			for (int j = 0; j < w; j++)
			{
				float a = Mathf.Abs((float)j + 0.5f - num) - (num3 - radius);
				float a2 = Mathf.Abs((float)i + 0.5f - num2) - (num4 - radius);
				float num5 = Mathf.Sqrt(Mathf.Max(a, 0f) * Mathf.Max(a, 0f) + Mathf.Max(a2, 0f) * Mathf.Max(a2, 0f)) - radius;
				float a3 = ((stroke > 0f) ? Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(num5 + stroke * 0.5f) + 0.5f) : Mathf.Clamp01(0.5f - num5));
				array[i * w + j] = new Color(1f, 1f, 1f, a3);
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
	}
}
