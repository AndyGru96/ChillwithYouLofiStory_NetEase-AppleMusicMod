using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class PanelRows
{
	internal sealed class TrackRow
	{
		public GameObject Root;

		public RectTransform Rect;

		public Image Background;

		public TextMeshProUGUI Lead;

		public Image Cover;

		public TextMeshProUGUI Title;

		public MarqueeText TitleMarquee;

		public TextMeshProUGUI Subtitle;

		public TextMeshProUGUI Trailing;

		public int BoundIndex = -1;

		public long BoundTrackId;

		public int BindGeneration;

		public Color NormalTitleColor;

		public bool IsHighlighted;

		public string PendingCoverUrl;

		public int PendingCoverSize;

		public Action<Sprite> PendingCoverCallback;

		public string HeldCoverKey;

		public Action<int> OnActivate;
	}

	internal struct ListRow
	{
		public string Name;

		public float Height;

		public float Indent;

		public Color Background;

		public string Lead;

		public float LeadWidth;

		public Color LeadColor;

		public float LeadFontSize;

		public float CoverSize;

		public string CoverUrl;

		public int CoverRequestSize;

		public string Title;

		public bool TitleBold;

		public Color TitleColor;

		public string Subtitle;

		public Color SubtitleColor;

		public string Trailing;

		public Action OnClick;
	}

	internal struct RowParts
	{
		public GameObject Row;

		public Image Background;

		public TextMeshProUGUI Lead;

		public TextMeshProUGUI Title;

		public TextMeshProUGUI Trailing;
	}

	internal static void MarkOwned(GameObject go, MusicProvider owner)
	{
		if (!(go == null))
		{
			(go.GetComponent<MusicBridgeOwned>() ?? go.AddComponent<MusicBridgeOwned>()).Owner = owner;
		}
	}

	private static MusicProvider OwnerOf(Transform listRoot)
	{
		Transform transform = listRoot;
		while (transform != null)
		{
			MusicBridgeOwned component = transform.GetComponent<MusicBridgeOwned>();
			if (component != null)
			{
				return component.Owner;
			}
			transform = transform.parent;
		}
		return MusicProvider.Netease;
	}

	public static GameObject NewRow(Transform listRoot, string name, float height, float indent, Color bg)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(listRoot, worldPositionStays: false);
		MarkOwned(gameObject, OwnerOf(listRoot));
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = height;
		layoutElement.minHeight = height;
		Image image = gameObject.AddComponent<Image>();
		image.sprite = UiSprites.Rounded;
		image.type = Image.Type.Sliced;
		image.color = bg;
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = true;
		horizontalLayoutGroup.spacing = 7f;
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		horizontalLayoutGroup.padding = new RectOffset((int)indent + 8, 14, 0, 0);
		return gameObject;
	}

	private static void ApplyRowButtonColors(Button btn, float highlight)
	{
		ColorBlock colors = btn.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(highlight, highlight, highlight, 1f);
		colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
		colors.selectedColor = Color.white;
		btn.colors = colors;
	}

	private static void LockWidth(Component c, float width)
	{
		LayoutElement component = c.GetComponent<LayoutElement>();
		component.preferredWidth = width;
		component.minWidth = width;
		component.flexibleWidth = 0f;
	}

	public static void SectionLabel(Transform listRoot, string name, string text, float indent)
	{
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(NewRow(listRoot, name, 22f, indent, new Color(0f, 0f, 0f, 0f)).transform, text, UiKit.GameArtistFontSize, TextAnchor.MiddleLeft);
		textMeshProUGUI.color = UiKit.TextSecondary;
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 1f;
	}

	public static void StatusRow(Transform listRoot, string name, string text, float indent, Action retry = null)
	{
		GameObject gameObject = NewRow(listRoot, name, 24f, indent, new Color(0f, 0f, 0f, 0f));
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(gameObject.transform, text, UiKit.GameArtistFontSize, TextAnchor.MiddleLeft);
		textMeshProUGUI.color = UiKit.TextFaint;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 1f;
		if (retry != null)
		{
			UiKit.CreatePillButton(gameObject.transform, "重试", filled: false, UiKit.LineColor, 20f, 44f).onClick.AddListener(delegate
			{
				retry();
			});
		}
	}

	public static void ActionRow(Transform listRoot, string name, string text, float indent, Action onClick)
	{
		GameObject gameObject = NewRow(listRoot, name, 26f, indent, new Color(1f, 1f, 1f, 0.05f));
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(gameObject.transform, text, UiKit.GameArtistFontSize, TextAnchor.MiddleCenter);
		textMeshProUGUI.color = UiKit.TextSecondary;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 1f;
		Button button = gameObject.AddComponent<Button>();
		button.targetGraphic = gameObject.GetComponent<Image>();
		button.onClick.AddListener(delegate
		{
			onClick();
		});
	}

	public static void GroupHeader(Transform listRoot, string name, string title, bool expanded, Action onToggle, bool showRefresh = false, Action onRefresh = null)
	{
		GameObject gameObject = NewRow(listRoot, name, 32f, 0f, new Color(1f, 1f, 1f, 0.1f));
		LockWidth(UiKit.CreateText(gameObject.transform, Arrow(expanded), UiKit.GameTitleFontSize, TextAnchor.MiddleCenter), 26f);
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateGameStyleText(gameObject.transform, title, isTitle: true);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 1f;
		Button button = gameObject.AddComponent<Button>();
		button.targetGraphic = gameObject.GetComponent<Image>();
		ApplyRowButtonColors(button, 1.3f);
		button.onClick.AddListener(delegate
		{
			onToggle();
		});
		if (showRefresh && onRefresh != null)
		{
			UiKit.CreatePillButton(gameObject.transform, "刷新", filled: false, UiKit.LineColor, 22f, 48f).onClick.AddListener(delegate
			{
				onRefresh();
			});
		}
	}

	public static string Arrow(bool expanded)
	{
		if (!expanded)
		{
			return UiKit.Glyph("▶", ">");
		}
		return UiKit.Glyph("▼", "v");
	}

	public static void ApplyTrackRowHighlight(TrackRow h, bool isCurrent)
	{
		if (h != null && !(h.Background == null))
		{
			h.IsHighlighted = isCurrent;
			h.Background.color = (isCurrent ? UiKit.NowPlayingTint : new Color(1f, 1f, 1f, 0.03f));
			if (h.Lead != null)
			{
				h.Lead.text = (isCurrent ? UiKit.Glyph("▶", ">") : (h.BoundIndex + 1).ToString("00"));
				h.Lead.color = (isCurrent ? UiKit.NowPlayingText : UiKit.TextFaint);
			}
			if (h.Title != null)
			{
				h.Title.color = (isCurrent ? UiKit.NowPlayingText : h.NormalTitleColor);
				h.Title.fontStyle = (isCurrent ? FontStyles.Bold : FontStyles.Normal);
			}
		}
	}

	public static TrackRow CreateTrackRow(Transform parent, float height, float indent, Action<int> onActivate)
	{
		TrackRow trackRow = new TrackRow();
		trackRow.Root = NewRow(parent, "TrackRow", height, indent, new Color(1f, 1f, 1f, 0.03f));
		trackRow.Rect = trackRow.Root.GetComponent<RectTransform>();
		trackRow.Background = trackRow.Root.GetComponent<Image>();
		trackRow.Lead = UiKit.CreateText(trackRow.Root.transform, "", UiKit.GameArtistFontSize, TextAnchor.MiddleCenter);
		LockWidth(trackRow.Lead, 32f);
		trackRow.Cover = UiKit.CreateSquareCover(trackRow.Root.transform, 32f);
		GameObject gameObject = new GameObject("Col");
		gameObject.transform.SetParent(trackRow.Root.transform, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 40f;
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		verticalLayoutGroup.spacing = 0f;
		gameObject.AddComponent<RectMask2D>();
		trackRow.Title = UiKit.CreateGameStyleMarquee(gameObject.transform, "", isTitle: true, UiKit.GameTitleAutoMax + 2f);
		trackRow.TitleMarquee = trackRow.Title.GetComponent<MarqueeText>();
		trackRow.Subtitle = UiKit.CreateGameStyleText(gameObject.transform, "", isTitle: false);
		trackRow.Trailing = UiKit.CreateGameStyleText(trackRow.Root.transform, "", isTitle: false, TextAnchor.MiddleRight);
		trackRow.Trailing.color = UiKit.TextFaint;
		LockWidth(trackRow.Trailing, 52f);
		trackRow.OnActivate = onActivate;
		Button button = trackRow.Root.AddComponent<Button>();
		button.targetGraphic = trackRow.Background;
		ApplyRowButtonColors(button, 1.8f);
		TrackRow captured = trackRow;
		button.onClick.AddListener(delegate
		{
			if (captured.BoundIndex >= 0 && captured.OnActivate != null)
			{
				captured.OnActivate(captured.BoundIndex);
			}
		});
		return trackRow;
	}

	public static void BindTrackRow(TrackRow h, int absoluteIndex, bool isCurrent, long trackId, string title, string subtitle, string trailing, bool playable, string coverUrl, int coverSize)
	{
		if (h == null)
		{
			return;
		}
		CancelCover(h);
		h.BoundIndex = absoluteIndex;
		h.BoundTrackId = trackId;
		int gen = ++h.BindGeneration;
		h.NormalTitleColor = ((!playable) ? UiKit.TextFaint : UiKit.GameTitleColor);
		ApplyTrackRowHighlight(h, isCurrent);
		if (h.TitleMarquee != null)
		{
			h.TitleMarquee.SetContentDeferred(title ?? "");
		}
		else
		{
			h.Title.text = title ?? "";
		}
		h.Subtitle.text = subtitle ?? "";
		h.Subtitle.color = (playable ? UiKit.TextFaint : new Color(0.95f, 0.45f, 0.35f, 0.9f));
		h.Trailing.text = trailing ?? "";
		h.Cover.sprite = null;
		h.Cover.color = UiKit.CoverPlaceholder;
		h.Cover.gameObject.SetActive(!string.IsNullOrEmpty(coverUrl) || coverSize > 0);
		if (string.IsNullOrEmpty(coverUrl))
		{
			return;
		}
		int num = ((coverSize > 0) ? coverSize : 40);
		Action<Sprite> action = null;
		string key = CoverCache.KeyOf(coverUrl, num);
		action = delegate(Sprite sprite)
		{
			if (h.BindGeneration == gen && !(h.Cover == null) && !(sprite == null))
			{
				h.Cover.sprite = sprite;
				h.Cover.color = Color.white;
				ReleaseHeldCover(h);
				CoverCache.Acquire(key);
				h.HeldCoverKey = key;
			}
		};
		h.PendingCoverUrl = coverUrl;
		h.PendingCoverSize = num;
		h.PendingCoverCallback = action;
		CoverCache.Request(coverUrl, num, action);
	}

	private static void CancelCover(TrackRow h)
	{
		if (h != null)
		{
			if (h.PendingCoverCallback != null)
			{
				CoverCache.Cancel(h.PendingCoverUrl, h.PendingCoverSize, h.PendingCoverCallback);
				h.PendingCoverUrl = null;
				h.PendingCoverCallback = null;
			}
			ReleaseHeldCover(h);
		}
	}

	private static void ReleaseHeldCover(TrackRow h)
	{
		if (h != null && h.HeldCoverKey != null)
		{
			CoverCache.Release(h.HeldCoverKey);
			h.HeldCoverKey = null;
		}
	}

	public static void BindTrackRow(TrackRow h, TrackInfo t, int absoluteIndex, bool isCurrent)
	{
		if (h != null && t != null)
		{
			string text = t.Artists ?? "";
			if (!string.IsNullOrEmpty(t.Album))
			{
				text = text + " · " + t.Album;
			}
			if (!t.Playable)
			{
				text = (t.UnplayableReason ?? "不可播放") + " · " + text;
			}
			h.Root.name = "TR_" + t.Id;
			BindTrackRow(h, absoluteIndex, isCurrent, t.Id, t.Name, text, t.DurationText, t.Playable, t.CoverUrl, 40);
		}
	}

	public static void UnbindTrackRow(TrackRow h)
	{
		if (h != null)
		{
			h.BindGeneration++;
			CancelCover(h);
			h.BoundIndex = -1;
			h.BoundTrackId = 0L;
			if (h.TitleMarquee != null)
			{
				h.TitleMarquee.SetContent("");
			}
			h.Title.text = "";
			h.Subtitle.text = "";
			h.Trailing.text = "";
			h.Lead.text = "";
			h.Cover.sprite = null;
			h.Cover.color = UiKit.CoverPlaceholder;
			MarqueeText.ResetOn(h.Title);
		}
	}

	public static GameObject BuildListRow(Transform listRoot, ListRow d)
	{
		RowParts parts;
		return BuildListRow(listRoot, d, out parts);
	}

	public static GameObject BuildListRow(Transform listRoot, ListRow d, out RowParts parts)
	{
		parts = default(RowParts);
		GameObject gameObject = (parts.Row = NewRow(listRoot, d.Name, d.Height, d.Indent, d.Background));
		parts.Background = gameObject.GetComponent<Image>();
		if (!string.IsNullOrEmpty(d.Lead))
		{
			TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(gameObject.transform, d.Lead, (d.LeadFontSize > 0f) ? d.LeadFontSize : UiKit.GameArtistFontSize, TextAnchor.MiddleCenter);
			textMeshProUGUI.color = d.LeadColor;
			LockWidth(textMeshProUGUI, (d.LeadWidth > 0f) ? d.LeadWidth : 32f);
			parts.Lead = textMeshProUGUI;
		}
		if (d.CoverSize > 0f)
		{
			Image image = UiKit.CreateSquareCover(gameObject.transform, d.CoverSize);
			if (string.IsNullOrEmpty(d.CoverUrl))
			{
				image.color = UiKit.CoverPlaceholder;
			}
			else
			{
				CoverCache.Apply(image, d.CoverUrl, d.CoverRequestSize, UiKit.CoverPlaceholder);
			}
		}
		GameObject gameObject2 = new GameObject("Col");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject2.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 40f;
		VerticalLayoutGroup verticalLayoutGroup = gameObject2.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		verticalLayoutGroup.spacing = 0f;
		gameObject2.AddComponent<RectMask2D>();
		TextMeshProUGUI textMeshProUGUI2 = UiKit.CreateGameStyleMarquee(gameObject2.transform, d.Title, isTitle: true, UiKit.GameTitleAutoMax + 2f);
		textMeshProUGUI2.color = d.TitleColor;
		if (d.TitleBold)
		{
			textMeshProUGUI2.fontStyle = FontStyles.Bold;
		}
		parts.Title = textMeshProUGUI2;
		UiKit.CreateGameStyleText(gameObject2.transform, d.Subtitle ?? "", isTitle: false).color = d.SubtitleColor;
		if (!string.IsNullOrEmpty(d.Trailing))
		{
			TextMeshProUGUI textMeshProUGUI3 = UiKit.CreateGameStyleText(gameObject.transform, d.Trailing, isTitle: false, TextAnchor.MiddleRight);
			textMeshProUGUI3.color = UiKit.TextFaint;
			LockWidth(textMeshProUGUI3, 52f);
			parts.Trailing = textMeshProUGUI3;
		}
		if (d.OnClick != null)
		{
			Button button = gameObject.AddComponent<Button>();
			button.targetGraphic = gameObject.GetComponent<Image>();
			ApplyRowButtonColors(button, 1.8f);
			Action click = d.OnClick;
			button.onClick.AddListener(delegate
			{
				click();
			});
		}
		return gameObject;
	}
}
