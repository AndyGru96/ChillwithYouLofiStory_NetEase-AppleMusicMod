using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MusicBridge;

internal static class UiKit
{
	private static TMP_FontAsset _tmpFont;

	public static readonly Color PanelColor = new Color(0.03f, 0.03f, 0.055f, 0.55f);

	public static readonly Color PanelDeep = new Color(0.03f, 0.03f, 0.055f, 0.8f);

	public static readonly Color DockOpaque = new Color(0.055f, 0.05f, 0.085f, 1f);

	public static readonly Color LineColor = new Color(1f, 1f, 1f, 0.85f);

	public static readonly Color LineSoft = new Color(1f, 1f, 1f, 0.32f);

	public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.65f);

	public static readonly Color TextFaint = new Color(1f, 1f, 1f, 0.45f);

	public static readonly Color CoverPlaceholder = new Color(0.16f, 0.15f, 0.2f, 1f);

	public static readonly Color NeteaseAccent = new Color(0.86f, 0.18f, 0.18f, 1f);

	public static readonly Color AppleAccent = new Color(0.98f, 0.22f, 0.34f, 1f);

	public static readonly Color LocalAccent = new Color(0.45f, 0.38f, 0.72f, 1f);

	public static readonly Color NowPlayingTint = new Color(0.45f, 0.72f, 1f, 0.28f);

	public static readonly Color NowPlayingText = new Color(0.72f, 0.87f, 1f, 1f);

	private static Font _font;

	private const string RequiredGlyphs = "网易云音乐播放列表连接未";

	private static readonly HashSet<char> _reportedMissingGlyphs = new HashSet<char>();

	public static string TmpFontDescription { get; private set; } = "(未解析)";

	public static float GameTitleFontSize { get; private set; } = 24f;

	public static float GameArtistFontSize { get; private set; } = 16f;

	public static Color GameTitleColor { get; private set; } = Color.white;

	public static Color GameArtistColor { get; private set; } = new Color(1f, 1f, 1f, 0.75f);

	public static bool GameTitleAutoSize { get; private set; } = true;

	public static float GameTitleAutoMin { get; private set; } = 11f;

	public static float GameTitleAutoMax { get; private set; } = 24f;

	public static bool GameArtistAutoSize { get; private set; } = true;

	public static float GameArtistAutoMin { get; private set; } = 9f;

	public static float GameArtistAutoMax { get; private set; } = 16f;

	public static string FontDescription { get; private set; } = "(未解析)";

	public static void ResolveTmpFont()
	{
		if (_tmpFont != null)
		{
			return;
		}
		try
		{
			TMP_FontAsset tMP_FontAsset = null;
			TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
			foreach (TMP_FontAsset tMP_FontAsset2 in array)
			{
				if (!(tMP_FontAsset2 == null))
				{
					if (tMP_FontAsset == null)
					{
						tMP_FontAsset = tMP_FontAsset2;
					}
					if (tMP_FontAsset2.characterTable != null && tMP_FontAsset.characterTable != null && tMP_FontAsset2.characterTable.Count > tMP_FontAsset.characterTable.Count)
					{
						tMP_FontAsset = tMP_FontAsset2;
					}
				}
			}
			if (tMP_FontAsset != null)
			{
				_tmpFont = tMP_FontAsset;
				TmpFontDescription = "游戏 TMP 字体 " + tMP_FontAsset.name + "（字符数 " + ((tMP_FontAsset.characterTable != null) ? tMP_FontAsset.characterTable.Count : 0) + "）";
				BridgeLog.Info("文字：使用 " + TmpFontDescription);
			}
			else
			{
				BridgeLog.Warn("未找到游戏的 TMP 字体资源，文字将使用 TMP 默认字体。");
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("解析 TMP 字体失败：" + ex.Message);
		}
	}

	public static void AdoptGameTextStyle()
	{
		try
		{
			Component component = null;
			Type type = AccessTools.TypeByName("Bulbul.MusicPlayListButtons") ?? AccessTools.TypeByName("MusicPlayListButtons");
			if (type == null)
			{
				BridgeLog.Info("未找到游戏歌曲行类型，文字规格沿用默认值。");
				return;
			}
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(type);
			for (int i = 0; i < array.Length; i++)
			{
				Component component2 = array[i] as Component;
				if (!(component2 == null))
				{
					if (component2.gameObject.scene.IsValid())
					{
						component = component2;
						break;
					}
					if (component == null)
					{
						component = component2;
					}
				}
			}
			if (component == null)
			{
				BridgeLog.Info("场景内暂无游戏歌曲行，文字规格沿用默认值。");
				return;
			}
			TextMeshProUGUI value = Traverse.Create((object)component).Field("_musicTitleText").GetValue<TextMeshProUGUI>();
			TextMeshProUGUI value2 = Traverse.Create((object)component).Field("_artistNameText").GetValue<TextMeshProUGUI>();
			if (value == null)
			{
				BridgeLog.Info("游戏歌曲行没有标题文字组件，沿用默认值。");
				return;
			}
			GameTitleAutoSize = value.enableAutoSizing;
			GameTitleFontSize = ((value.fontSize > 0f) ? value.fontSize : 24f);
			if (value.enableAutoSizing)
			{
				GameTitleAutoMin = value.fontSizeMin;
				GameTitleAutoMax = value.fontSizeMax;
				GameTitleFontSize = value.fontSizeMax;
			}
			if (GameTitleFontSize < 12f)
			{
				GameTitleFontSize = 24f;
			}
			GameTitleColor = value.color;
			if (value.font != null)
			{
				_tmpFont = value.font;
			}
			if (value2 != null)
			{
				GameArtistAutoSize = value2.enableAutoSizing;
				GameArtistFontSize = ((value2.fontSize > 0f) ? value2.fontSize : 16f);
				if (value2.enableAutoSizing)
				{
					GameArtistAutoMin = value2.fontSizeMin;
					GameArtistAutoMax = value2.fontSizeMax;
					GameArtistFontSize = value2.fontSizeMax;
				}
				if (GameArtistFontSize < 9f)
				{
					GameArtistFontSize = 16f;
				}
				GameArtistColor = value2.color;
			}
			BridgeLog.Info("已抄用游戏歌曲行文字规格：来源=" + (component.gameObject.scene.IsValid() ? "场景实例" : "预制体") + "  标题 " + GameTitleFontSize + "px(autoSize=" + value.enableAutoSizing + ", min=" + value.fontSizeMin + ", max=" + value.fontSizeMax + ")  歌手 " + GameArtistFontSize + "px  字体 " + ((value.font != null) ? value.font.name : "?") + "  行缩放 " + component.transform.localScale.x);
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("抄用游戏文字规格失败：" + ex.Message);
		}
	}

	private static TextAlignmentOptions ToTmpAlign(TextAnchor a)
	{
		return a switch
		{
			TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
			TextAnchor.UpperCenter => TextAlignmentOptions.Top,
			TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
			TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
			TextAnchor.MiddleRight => TextAlignmentOptions.Right,
			TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
			TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
			TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
			_ => TextAlignmentOptions.Left,
		};
	}

	public static void ApplyTmpFont(TextMeshProUGUI t)
	{
		if (t != null && _tmpFont != null)
		{
			t.font = _tmpFont;
		}
	}

	public static TextMeshProUGUI AddTextComponent(GameObject host)
	{
		if (host == null)
		{
			return null;
		}
		bool activeSelf = host.activeSelf;
		if (activeSelf)
		{
			host.SetActive(value: false);
		}
		TextMeshProUGUI textMeshProUGUI = host.AddComponent<TextMeshProUGUI>();
		if (_tmpFont != null)
		{
			textMeshProUGUI.font = _tmpFont;
		}
		if (activeSelf)
		{
			host.SetActive(value: true);
		}
		return textMeshProUGUI;
	}

	private static void ApplyFont(TextMeshProUGUI t, float size, TextAnchor anchor)
	{
		if (_tmpFont != null)
		{
			t.font = _tmpFont;
		}
		t.fontSize = size;
		t.alignment = ToTmpAlign(anchor);
		t.color = Color.white;
		t.raycastTarget = false;
		t.enableWordWrapping = false;
		t.overflowMode = TextOverflowModes.Overflow;
		t.richText = false;
	}

	public static void ResolveFont()
	{
		if (_font != null)
		{
			return;
		}
		try
		{
			Font[] array = Resources.FindObjectsOfTypeAll<Font>();
			foreach (Font font in array)
			{
				if (!(font == null) && font.dynamic && HasAll(font, "网易云音乐播放列表连接未") && font.HasCharacter('A') && font.HasCharacter('0'))
				{
					_font = font;
					FontDescription = "游戏内字体 " + font.name;
					BridgeLog.Info("字体：使用游戏内字体 " + font.name);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("枚举游戏字体失败：" + ex.Message);
		}
		try
		{
			_font = Font.CreateDynamicFontFromOSFont(new string[] { "PingFang SC", "Hiragino Sans GB", "Heiti SC", "Arial Unicode MS", "Helvetica" }, 16);
			if (_font != null)
			{
				FontDescription = "系统字体 " + _font.name;
				BridgeLog.Info("字体：游戏内字体缺少中文字形，改用系统字体 " + _font.name);
				return;
			}
		}
		catch (Exception ex2)
		{
			BridgeLog.Warn("创建系统字体失败：" + ex2.Message);
		}
		_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		FontDescription = "内置 Arial（无中文字形）";
		BridgeLog.Warn("字体：退回内置 Arial，中文可能无法显示。");
	}

	private static bool HasAll(Font f, string glyphs)
	{
		foreach (char c in glyphs)
		{
			if (!f.HasCharacter(c))
			{
				return false;
			}
		}
		return true;
	}

	public static string Glyph(string preferred, string fallback)
	{
		if (_tmpFont == null)
		{
			return preferred;
		}
		try
		{
			foreach (char c in preferred)
			{
				if (!_tmpFont.HasCharacter(c))
				{
					if (_reportedMissingGlyphs.Add(c))
					{
						string[] obj = new string[5] { "字形缺失 U+", null, null, null, null };
						int num = c;
						obj[1] = num.ToString("X4");
						obj[2] = "，改用替代文本 \"";
						obj[3] = fallback;
						obj[4] = "\"（后续同码位不再重复记录）。";
						BridgeLog.Info(string.Concat(obj));
					}
					return fallback;
				}
			}
			return preferred;
		}
		catch
		{
			return fallback;
		}
	}

	public static RectTransform NewRect(string name, Transform parent)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		return gameObject.AddComponent<RectTransform>();
	}

	public static TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, TextAnchor anchor)
	{
		GameObject gameObject = new GameObject("Text");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = fontSize + 8f;
		layoutElement.minHeight = fontSize + 8f;
		TextMeshProUGUI textMeshProUGUI = AddTextComponent(gameObject);
		ApplyFont(textMeshProUGUI, fontSize, anchor);
		textMeshProUGUI.text = content;
		return textMeshProUGUI;
	}

	public static TextMeshProUGUI CreateGameStyleText(Transform parent, string content, bool isTitle, TextAnchor anchor = TextAnchor.MiddleLeft)
	{
		float num = (isTitle ? GameTitleAutoMax : GameArtistAutoMax);
		TextMeshProUGUI textMeshProUGUI = CreateText(parent, content, num, anchor);
		if (isTitle ? GameTitleAutoSize : GameArtistAutoSize)
		{
			textMeshProUGUI.enableAutoSizing = true;
			textMeshProUGUI.fontSizeMin = (isTitle ? GameTitleAutoMin : GameArtistAutoMin);
			textMeshProUGUI.fontSizeMax = num;
		}
		textMeshProUGUI.color = (isTitle ? GameTitleColor : GameArtistColor);
		return textMeshProUGUI;
	}

	public static Image CreateSquareCover(Transform parent, float size)
	{
		GameObject gameObject = new GameObject("Cover");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = size;
		layoutElement.minWidth = size;
		layoutElement.flexibleWidth = 0f;
		layoutElement.preferredHeight = size;
		layoutElement.minHeight = size;
		layoutElement.flexibleHeight = 0f;
		GameObject gameObject2 = new GameObject("CoverImage");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform rectTransform = gameObject2.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2(size, size);
		Image image = gameObject2.AddComponent<Image>();
		image.sprite = UiSprites.Rounded;
		image.type = Image.Type.Sliced;
		image.raycastTarget = false;
		return image;
	}

	public static TextMeshProUGUI CreateGameStyleMarquee(Transform parent, string content, bool isTitle, float lineHeight)
	{
		GameObject gameObject = new GameObject(isTitle ? "TitleViewport" : "SubViewport");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform viewport = gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;
		layoutElement.preferredHeight = lineHeight;
		layoutElement.minHeight = lineHeight;
		layoutElement.flexibleHeight = 0f;
		gameObject.AddComponent<RectMask2D>();
		GameObject gameObject2 = new GameObject("Text");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.AddComponent<RectTransform>();
		TextMeshProUGUI textMeshProUGUI = AddTextComponent(gameObject2);
		ApplyFont(textMeshProUGUI, isTitle ? GameTitleAutoMax : GameArtistAutoMax, TextAnchor.MiddleLeft);
		textMeshProUGUI.color = (isTitle ? GameTitleColor : GameArtistColor);
		RectTransform rectTransform = textMeshProUGUI.rectTransform;
		rectTransform.anchorMin = new Vector2(0f, 0f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 0.5f);
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2(4000f, 0f);
		MarqueeText marqueeText = MarqueeText.AttachSeamless(textMeshProUGUI, viewport, MarqueeText.RowGap);
		if (marqueeText != null)
		{
			marqueeText.SetContent(content ?? "");
		}
		else
		{
			textMeshProUGUI.text = content ?? "";
		}
		return textMeshProUGUI;
	}

	public static TextMeshProUGUI CreateStretchText(Transform parent, string content, float fontSize, TextAnchor anchor)
	{
		GameObject gameObject = new GameObject("Text");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		TextMeshProUGUI textMeshProUGUI = AddTextComponent(gameObject);
		ApplyFont(textMeshProUGUI, fontSize, anchor);
		textMeshProUGUI.text = content;
		return textMeshProUGUI;
	}

	public static GameObject CreateStatusRowWithMarquee(Transform parent, string name, float height, float fontSize, out TextMeshProUGUI head, out MarqueeText detail)
	{
		GameObject gameObject = CreateRow(parent, name, height, 0f);
		head = CreateText(gameObject.transform, "", fontSize, TextAnchor.MiddleLeft);
		head.color = TextSecondary;
		LayoutElement component = head.GetComponent<LayoutElement>();
		component.flexibleWidth = 0f;
		component.minWidth = 0f;
		head.enableWordWrapping = false;
		head.overflowMode = TextOverflowModes.Overflow;
		GameObject gameObject2 = new GameObject("DetailViewport");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform viewport = gameObject2.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject2.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;
		layoutElement.preferredHeight = height;
		gameObject2.AddComponent<RectMask2D>();
		GameObject gameObject3 = new GameObject("DetailText");
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		gameObject3.AddComponent<RectTransform>();
		TextMeshProUGUI textMeshProUGUI = AddTextComponent(gameObject3);
		ApplyFont(textMeshProUGUI, fontSize, TextAnchor.MiddleLeft);
		textMeshProUGUI.color = TextSecondary;
		textMeshProUGUI.text = "";
		RectTransform rectTransform = textMeshProUGUI.rectTransform;
		rectTransform.anchorMin = new Vector2(0f, 0f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 0.5f);
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2(4000f, 0f);
		detail = MarqueeText.AttachSeamless(textMeshProUGUI, viewport);
		return gameObject;
	}

	public static GameObject CreateRow(Transform parent, string name, float height, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft, RectOffset padding = null)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = height;
		layoutElement.minHeight = height;
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.spacing = spacing;
		horizontalLayoutGroup.childAlignment = alignment;
		if (padding != null)
		{
			horizontalLayoutGroup.padding = padding;
		}
		return gameObject;
	}

	public static GameObject CreateColumn(Transform parent, string name, float spacing, RectOffset padding = null)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.spacing = spacing;
		if (padding != null)
		{
			verticalLayoutGroup.padding = padding;
		}
		ContentSizeFitter contentSizeFitter = gameObject.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return gameObject;
	}

	public static void CreateRule(Transform parent, float flexibleWidth = 1f)
	{
		GameObject gameObject = new GameObject("Rule");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = flexibleWidth;
		layoutElement.preferredHeight = 1f;
		layoutElement.minHeight = 1f;
		Image image = gameObject.AddComponent<Image>();
		image.color = LineSoft;
		image.raycastTarget = false;
	}

	public static void CreateSpacer(Transform parent, float flexibleWidth = 1f)
	{
		GameObject gameObject = new GameObject("Spacer");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = flexibleWidth;
		layoutElement.minWidth = 0f;
	}

	public static Button CreateCircleButton(Transform parent, string label, float size, bool solid, Color? ringColor = null)
	{
		GameObject gameObject = new GameObject("Btn_" + label);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = size;
		layoutElement.minWidth = size;
		layoutElement.flexibleWidth = 0f;
		layoutElement.preferredHeight = size;
		layoutElement.minHeight = size;
		layoutElement.flexibleHeight = 0f;
		Image image = gameObject.AddComponent<Image>();
		image.sprite = (solid ? UiSprites.Circle : UiSprites.Ring);
		image.preserveAspect = true;
		image.color = (solid ? Color.white : (ringColor ?? LineColor));
		Button button = gameObject.AddComponent<Button>();
		ColorBlock colors = button.colors;
		if (solid)
		{
			button.targetGraphic = image;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
			colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
			colors.selectedColor = Color.white;
		}
		else
		{
			RectTransform rectTransform = NewRect("PressFill", gameObject.transform);
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.sizeDelta = Vector2.zero;
			Image image2 = rectTransform.gameObject.AddComponent<Image>();
			image2.sprite = UiSprites.Circle;
			image2.preserveAspect = true;
			image2.raycastTarget = false;
			button.targetGraphic = image2;
			colors.normalColor = new Color(1f, 1f, 1f, 0f);
			colors.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
			colors.pressedColor = new Color(1f, 1f, 1f, 0.45f);
			colors.selectedColor = new Color(1f, 1f, 1f, 0f);
		}
		button.colors = colors;
		TextMeshProUGUI textMeshProUGUI = CreateStretchText(gameObject.transform, label, size * 0.38f, TextAnchor.MiddleCenter);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.color = (solid ? new Color(0.05f, 0.05f, 0.09f, 1f) : Color.white);
		return button;
	}

	public static Button CreatePillButton(Transform parent, string label, bool filled, Color accent, float height = 28f, float preferredWidth = -1f)
	{
		GameObject gameObject = new GameObject("Btn_" + label);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = height;
		layoutElement.minHeight = height;
		if (preferredWidth > 0f)
		{
			layoutElement.preferredWidth = preferredWidth;
			layoutElement.minWidth = preferredWidth;
			layoutElement.flexibleWidth = 0f;
		}
		Image image = gameObject.AddComponent<Image>();
		image.sprite = (filled ? UiSprites.Pill : UiSprites.PillOutline);
		image.type = Image.Type.Sliced;
		image.color = (filled ? accent : LineColor);
		Button button = gameObject.AddComponent<Button>();
		ColorBlock colors = button.colors;
		if (filled)
		{
			button.targetGraphic = image;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
			colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
			colors.selectedColor = Color.white;
		}
		else
		{
			RectTransform rectTransform = NewRect("PressFill", gameObject.transform);
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.sizeDelta = Vector2.zero;
			Image image2 = rectTransform.gameObject.AddComponent<Image>();
			image2.sprite = UiSprites.Pill;
			image2.type = Image.Type.Sliced;
			image2.raycastTarget = false;
			button.targetGraphic = image2;
			colors.normalColor = new Color(1f, 1f, 1f, 0f);
			colors.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
			colors.pressedColor = new Color(1f, 1f, 1f, 0.45f);
			colors.selectedColor = new Color(1f, 1f, 1f, 0f);
		}
		button.colors = colors;
		TextMeshProUGUI textMeshProUGUI = CreateStretchText(gameObject.transform, label, GameArtistFontSize, TextAnchor.MiddleCenter);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.color = (filled ? new Color(0.05f, 0.05f, 0.09f, 1f) : Color.white);
		return button;
	}

	public static Slider CreateBarSlider(Transform parent, bool interactable, float preferredWidth = -1f)
	{
		GameObject gameObject = new GameObject("BarSlider");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = 16f;
		layoutElement.minHeight = 16f;
		if (preferredWidth > 0f)
		{
			layoutElement.preferredWidth = preferredWidth;
			layoutElement.minWidth = preferredWidth;
			layoutElement.flexibleWidth = 0f;
		}
		else
		{
			layoutElement.flexibleWidth = 1f;
		}
		gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
		Slider slider = gameObject.AddComponent<Slider>();
		slider.interactable = interactable;
		slider.transition = Selectable.Transition.None;
		RectTransform rectTransform = NewRect("Background", gameObject.transform);
		rectTransform.anchorMin = new Vector2(0f, 0.5f);
		rectTransform.anchorMax = new Vector2(1f, 0.5f);
		rectTransform.sizeDelta = new Vector2(0f, 6f);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = UiSprites.Bar;
		image.type = Image.Type.Sliced;
		image.color = new Color(1f, 1f, 1f, 0.22f);
		RectTransform rectTransform2 = NewRect("FillArea", gameObject.transform);
		rectTransform2.anchorMin = new Vector2(0f, 0.5f);
		rectTransform2.anchorMax = new Vector2(1f, 0.5f);
		rectTransform2.sizeDelta = new Vector2(0f, 6f);
		RectTransform rectTransform3 = NewRect("Fill", rectTransform2.transform);
		rectTransform3.anchorMin = new Vector2(0f, 0f);
		rectTransform3.anchorMax = new Vector2(0f, 1f);
		rectTransform3.sizeDelta = new Vector2(10f, 0f);
		Image image2 = rectTransform3.gameObject.AddComponent<Image>();
		image2.sprite = UiSprites.Bar;
		image2.type = Image.Type.Sliced;
		image2.color = LineColor;
		slider.fillRect = rectTransform3;
		slider.targetGraphic = image2;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = 0f;
		return slider;
	}

	public static TMP_InputField CreateSearchInput(Transform parent, string placeholderText)
	{
		GameObject gameObject = new GameObject("SearchInput");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.preferredHeight = 30f;
		layoutElement.minHeight = 30f;
		Image image = gameObject.AddComponent<Image>();
		image.sprite = UiSprites.Pill;
		image.type = Image.Type.Sliced;
		image.color = new Color(1f, 1f, 1f, 0.12f);
		RectTransform rectTransform = NewRect("Viewport", gameObject.transform);
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(12f, 2f);
		rectTransform.offsetMax = new Vector2(-12f, -2f);
		rectTransform.gameObject.AddComponent<RectMask2D>();
		RectTransform rectTransform2 = NewRect("Placeholder", rectTransform.transform);
		rectTransform2.anchorMin = Vector2.zero;
		rectTransform2.anchorMax = Vector2.one;
		rectTransform2.sizeDelta = Vector2.zero;
		TextMeshProUGUI textMeshProUGUI = AddTextComponent(rectTransform2.gameObject);
		ApplyFont(textMeshProUGUI, GameArtistFontSize, TextAnchor.MiddleLeft);
		textMeshProUGUI.text = placeholderText;
		textMeshProUGUI.color = TextFaint;
		RectTransform rectTransform3 = NewRect("Text", rectTransform.transform);
		rectTransform3.anchorMin = Vector2.zero;
		rectTransform3.anchorMax = Vector2.one;
		rectTransform3.sizeDelta = Vector2.zero;
		TextMeshProUGUI textMeshProUGUI2 = AddTextComponent(rectTransform3.gameObject);
		ApplyFont(textMeshProUGUI2, GameArtistFontSize, TextAnchor.MiddleLeft);
		TMP_InputField tMP_InputField = gameObject.AddComponent<TMP_InputField>();
		tMP_InputField.targetGraphic = image;
		tMP_InputField.textViewport = rectTransform;
		tMP_InputField.textComponent = textMeshProUGUI2;
		tMP_InputField.placeholder = textMeshProUGUI;
		tMP_InputField.caretWidth = 2;
		tMP_InputField.caretColor = Color.white;
		tMP_InputField.selectionColor = new Color(NeteaseAccent.r, NeteaseAccent.g, NeteaseAccent.b, 0.4f);
		tMP_InputField.lineType = TMP_InputField.LineType.SingleLine;
		return tMP_InputField;
	}

	public static void AddPressCallbacks(GameObject go, Action onDown, Action onUp)
	{
		EventTrigger obj = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerDown
		};
		entry.callback.AddListener(delegate
		{
			if (onDown != null)
			{
				onDown();
			}
		});
		obj.triggers.Add(entry);
		EventTrigger.Entry entry2 = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerUp
		};
		entry2.callback.AddListener(delegate
		{
			if (onUp != null)
			{
				onUp();
			}
		});
		obj.triggers.Add(entry2);
	}

	public static void AddHoverCallbacks(GameObject go, Action onEnter, Action onExit)
	{
		EventTrigger obj = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerEnter
		};
		entry.callback.AddListener(delegate
		{
			onEnter?.Invoke();
		});
		obj.triggers.Add(entry);
		EventTrigger.Entry entry2 = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerExit
		};
		entry2.callback.AddListener(delegate
		{
			onExit?.Invoke();
		});
		obj.triggers.Add(entry2);
	}
}
