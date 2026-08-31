using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal sealed class MarqueeText : MonoBehaviour
{
	private const float Speed = 30f;

	private const float StartPauseSeconds = 1.5f;

	private const float EndPauseSeconds = 0.6f;

	private TextMeshProUGUI _text;

	private RectTransform _rt;

	private RectTransform _viewport;

	private string _lastText;

	private float _offset;

	private float _pauseUntil;

	private float _neededWidth;

	private bool _widthValid;

	private const string DefaultLoopGap = "   ";

	private const string RowLoopGap = "     ";

	private bool _seamless;

	private string _loopGap = "   ";

	private string _rawContent = "";

	private float _copyWidth;

	private static bool _fontWarned;

	private float _lastAvail = -1f;

	private FontStyles _lastStyle = (FontStyles)(-1);

	public static string RowGap => "     ";

	public static MarqueeText AttachSeamless(TextMeshProUGUI text, RectTransform viewport, string loopGap = null)
	{
		MarqueeText marqueeText = Attach(text, viewport);
		if (marqueeText == null)
		{
			return null;
		}
		marqueeText._seamless = true;
		if (loopGap != null)
		{
			marqueeText._loopGap = loopGap;
		}
		return marqueeText;
	}

	public void SetContent(string raw)
	{
		raw = raw ?? "";
		if (!(raw == _rawContent))
		{
			_rawContent = raw;
			RebuildSeamless();
		}
	}

	public void SetContentDeferred(string raw)
	{
		raw = raw ?? "";
		if (!(raw == _rawContent))
		{
			_rawContent = raw;
			if (_text != null)
			{
				_text.text = raw;
			}
			_lastText = raw;
			_copyWidth = 0f;
			_offset = 0f;
			_pauseUntil = 0f;
			_widthValid = false;
			_lastAvail = -1f;
			if (_rt != null)
			{
				Apply();
			}
		}
	}

	private void RebuildSeamless()
	{
		if (!(_text == null) && !(_viewport == null))
		{
			float width = _viewport.rect.width;
			float num = ((_rawContent.Length == 0) ? 0f : _text.GetPreferredValues(_rawContent).x);
			if (width <= 1f || num <= width)
			{
				_text.text = _rawContent;
				_copyWidth = 0f;
				_offset = 0f;
			}
			else
			{
				_text.text = _rawContent + _loopGap + _rawContent;
				_copyWidth = _text.GetPreferredValues(_rawContent + _loopGap).x;
				_offset = 0f;
			}
			_lastText = _text.text;
			Apply();
		}
	}

	public static TextMeshProUGUI CreateClippedLyric(Transform parent, TextMeshProUGUI template, RectTransform alignTo, float height)
	{
		GameObject gameObject = new GameObject("LyricViewport");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		if (alignTo != null)
		{
			rectTransform.anchorMin = alignTo.anchorMin;
			rectTransform.anchorMax = alignTo.anchorMax;
			rectTransform.pivot = alignTo.pivot;
			rectTransform.sizeDelta = new Vector2(alignTo.sizeDelta.x, height);
			rectTransform.anchoredPosition = alignTo.anchoredPosition + new Vector2(0f, 0f - (alignTo.rect.height * 0.5f + height * 0.5f + 6f));
		}
		gameObject.AddComponent<RectMask2D>();
		gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		TextMeshProUGUI textMeshProUGUI;
		if (template != null)
		{
			GameObject obj = Object.Instantiate(template.gameObject, gameObject.transform);
			obj.name = "LyricText";
			textMeshProUGUI = obj.GetComponent<TextMeshProUGUI>();
		}
		else
		{
			GameObject obj2 = new GameObject("LyricText");
			obj2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			obj2.AddComponent<RectTransform>();
			textMeshProUGUI = UiKit.AddTextComponent(obj2);
		}
		if (textMeshProUGUI == null)
		{
			return null;
		}
		RectTransform rectTransform2 = textMeshProUGUI.rectTransform;
		rectTransform2.anchorMin = new Vector2(0f, 0.5f);
		rectTransform2.anchorMax = new Vector2(0f, 0.5f);
		rectTransform2.pivot = new Vector2(0f, 0.5f);
		rectTransform2.anchoredPosition = Vector2.zero;
		rectTransform2.sizeDelta = new Vector2(4000f, height);
		textMeshProUGUI.enableAutoSizing = false;
		textMeshProUGUI.enableWordWrapping = false;
		textMeshProUGUI.overflowMode = TextOverflowModes.Overflow;
		textMeshProUGUI.alignment = TextAlignmentOptions.Left;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.text = "";
		Attach(textMeshProUGUI, rectTransform);
		return textMeshProUGUI;
	}

	private static void EnsureFont(TextMeshProUGUI text)
	{
		if (!(text == null) && !(text.font != null))
		{
			UiKit.ApplyTmpFont(text);
			if (!(text.font != null) && !_fontWarned)
			{
				_fontWarned = true;
				BridgeLog.Warn("滚动文字『" + text.gameObject.name + "』没有字体，且无法补上（" + UiKit.TmpFontDescription + "）。TMP 会因此每帧告警，请检查字体解析。");
			}
		}
	}

	public static void ResetOn(TextMeshProUGUI text)
	{
		if (text == null)
		{
			return;
		}
		MarqueeText component = text.gameObject.GetComponent<MarqueeText>();
		if (!(component == null))
		{
			component._offset = 0f;
			component._pauseUntil = 0f;
			component._widthValid = false;
			component._lastText = null;
			component._lastAvail = -1f;
			component._rawContent = "";
			component._copyWidth = 0f;
			if (component._rt != null)
			{
				component.Apply();
			}
		}
	}

	public static MarqueeText Attach(TextMeshProUGUI text, RectTransform viewport)
	{
		if (text == null)
		{
			return null;
		}
		MarqueeText marqueeText = text.gameObject.GetComponent<MarqueeText>();
		if (marqueeText == null)
		{
			marqueeText = text.gameObject.AddComponent<MarqueeText>();
		}
		marqueeText._text = text;
		marqueeText._rt = text.rectTransform;
		marqueeText._viewport = viewport;
		EnsureFont(text);
		text.enableAutoSizing = false;
		text.enableWordWrapping = false;
		text.overflowMode = TextOverflowModes.Overflow;
		marqueeText._widthValid = false;
		return marqueeText;
	}

	private void LateUpdate()
	{
		if (_text == null || _rt == null || _viewport == null)
		{
			return;
		}
		if (_seamless)
		{
			TickSeamless();
			return;
		}
		if (_text.text != _lastText)
		{
			_lastText = _text.text;
			_widthValid = false;
			_offset = 0f;
			_pauseUntil = Time.unscaledTime + 1.5f;
			Apply();
			return;
		}
		float width = _viewport.rect.width;
		if (!_widthValid)
		{
			_neededWidth = _text.preferredWidth;
			_widthValid = true;
		}
		float neededWidth = _neededWidth;
		if (neededWidth <= width + 2f)
		{
			if (Mathf.Abs(_offset) > 0.01f)
			{
				_offset = 0f;
				Apply();
			}
		}
		else if (!(Time.unscaledTime < _pauseUntil))
		{
			_offset -= 30f * Time.unscaledDeltaTime;
			if (_offset <= 0f - neededWidth)
			{
				_offset = 0f;
				_pauseUntil = Time.unscaledTime + 1.5f + 0.6f;
			}
			Apply();
		}
	}

	private void TickSeamless()
	{
		float width = _viewport.rect.width;
		if (!Mathf.Approximately(width, _lastAvail))
		{
			_lastAvail = width;
			RebuildSeamless();
		}
		if (_text.fontStyle != _lastStyle)
		{
			_lastStyle = _text.fontStyle;
			RebuildSeamless();
		}
		if (!(_copyWidth <= 0f))
		{
			_offset -= 30f * Time.unscaledDeltaTime;
			if (_offset <= 0f - _copyWidth)
			{
				_offset += _copyWidth;
			}
			Apply();
		}
	}

	private void Apply()
	{
		_rt.anchoredPosition = new Vector2(_offset, _rt.anchoredPosition.y);
	}
}
