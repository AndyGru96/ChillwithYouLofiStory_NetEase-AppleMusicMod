using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MusicBridge;

internal static class GameNowPlayingBar
{
	private sealed class PlayButtonRef
	{
		public Image Image;

		public Sprite Play;

		public Sprite Pause;

		public Sprite NoMusic;
	}

	private struct ShuffleRef
	{
		public Image Image;

		public Sprite On;

		public Sprite Off;
	}

	private const string LyricObjectName = "MusicBridgeGameLyric";

	private static object _musicUi;
    private static FieldInfo _draggingProgressField;

	private static TextMeshProUGUI _titleText;

	private static TextMeshProUGUI _artistText;

	private static TextMeshProUGUI _lyricText;

	private static Slider _progressSlider;

	private static Slider _volumeSlider;

	private static Image _playPauseImage;

	private static Sprite _playSprite;

	private static Sprite _pauseSprite;

	private static Image _shuffleImage;

	private static Sprite _shuffleOn;

	private static Sprite _shuffleOff;

	private static Image _loopImage;

	private static Sprite _loopOn;

	private static Sprite _loopOff;

	private static string _gameTitleBackup;

	private static string _gameArtistBackup;

	private static bool _taken;

	private static bool _wasDragging;

	private static float _lastGameVolume = -1f;

	private static readonly HashSet<int> DirectBoundButtons = new HashSet<int>();

	private static PlayButtonRef[] _allPlayButtons;

	private static float _rescanAt;

	private static ShuffleRef[] _allShuffleButtons;

	private static float _shuffleRescanAt;

	public static bool IsTakenOver => _taken;

	public static bool GameThinksItIsPlaying
	{
		get
		{
			if (_playPauseImage == null || _pauseSprite == null)
			{
				return false;
			}
			return _playPauseImage.sprite == _pauseSprite;
		}
	}

	public static Sprite GamePlaySprite => _playSprite;

	public static Sprite GamePauseSprite => _pauseSprite;

	public static Sprite GamePrevSprite { get; private set; }

	public static Sprite GameNextSprite { get; private set; }

	public static Sprite GameVolumeSprite { get; private set; }

	internal static float GameVolume
	{
		get
		{
			if (!(_volumeSlider != null))
			{
				return -1f;
			}
			return _volumeSlider.value;
		}
	}

	public static void Attach(object ui)
	{
		Component component = ui as Component;
		if (component == null || (_musicUi == ui && _titleText != null))
		{
			return;
		}
		_musicUi = ui;
        _draggingProgressField = ui == null ? null : AccessTools.Field(ui.GetType(), "isDraggingProgressSlider");
		try
		{
			Traverse obj = Traverse.Create(ui);
			_titleText = obj.Field("_musicTitleText").GetValue<TextMeshProUGUI>();
			_artistText = obj.Field("_artistNameText").GetValue<TextMeshProUGUI>();
			_progressSlider = obj.Field("musicProgressSlider").GetValue<Slider>();
			_volumeSlider = obj.Field("_volumeSlider").GetValue<Slider>();
			_playPauseImage = obj.Field("_playOrPauseButtonImage").GetValue<Image>();
			_playSprite = obj.Field("_playButtonSprite").GetValue<Sprite>();
			_pauseSprite = obj.Field("_pauseButtonSprite").GetValue<Sprite>();
			_shuffleImage = obj.Field("_shuffleChangeButtonImage").GetValue<Image>();
			_shuffleOn = obj.Field("_shuffleButtonSprite").GetValue<Sprite>();
			_shuffleOff = obj.Field("_notShuffleBUttonSprite").GetValue<Sprite>();
			_loopImage = obj.Field("_loopChangeButtonImage").GetValue<Image>();
			_loopOn = obj.Field("_loopButtonSprite").GetValue<Sprite>();
			_loopOff = obj.Field("_notLoopButtonSprite").GetValue<Sprite>();
			Image value = obj.Field("_backButtonImage").GetValue<Image>();
			Image value2 = obj.Field("_nextButtonImage").GetValue<Image>();
			if (value != null)
			{
				GamePrevSprite = value.sprite;
			}
			if (value2 != null)
			{
				GameNextSprite = value2.sprite;
			}
			Button value3 = obj.Field("_switchMuteButton").GetValue<Button>();
			if (value3 != null)
			{
				Image image = value3.GetComponent<Image>() ?? value3.GetComponentInChildren<Image>();
				if (image != null)
				{
					GameVolumeSprite = image.sprite;
				}
			}
			BindDirectFallback(_playPauseImage, "Bulbul.FacilityMusic.OnClickButtonPlayOrPauseMusic", BridgePatches.DirectPlayPause);
			BindDirectFallback(value2, "Bulbul.FacilityMusic.OnClickButtonSkip", BridgePatches.DirectNext);
			BindDirectFallback(value, "Bulbul.FacilityMusic.OnClickButtonBack", BridgePatches.DirectPrevious);
			BindDirectFallback(_shuffleImage, "Bulbul.FacilityMusic.OnClickButtonShuffleChange", BridgePatches.DirectShuffle);
			BindDirectFallback(_loopImage, "Bulbul.FacilityMusic.OnClickButtonChangeLoop", BridgePatches.DirectLoop);
			BridgePanel.RestyleTransportButtons();
			LogBottomBarLayout();
			GameHudLayout.Attach((_progressSlider != null) ? _progressSlider.transform : component.transform);
			if (_titleText == null || _artistText == null)
			{
				BridgeLog.Warn("游戏底部播放条的文字组件没找到，无法接管显示。");
				return;
			}
			_taken = false;
			_lastGameVolume = -1f;
			_gameTitleBackup = _titleText.text;
			_gameArtistBackup = _artistText.text;
			EnsureLyricLine();
			BridgeLog.Info("已接上游戏自带底部播放条：进度条=" + (_progressSlider != null) + " 音量=" + (_volumeSlider != null) + " 播放键=" + (_playPauseImage != null) + " 随机=" + (_shuffleImage != null) + " 循环=" + (_loopImage != null));
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("接管游戏底部播放条失败：" + ex.Message);
		}
	}

	private static void BindDirectFallback(Image image, string harmonyCapability, UnityAction action)
	{
		if (BridgePatches.Has(harmonyCapability) || image == null || action == null)
		{
			return;
		}
		Button componentInParent = image.GetComponentInParent<Button>();
		if (!(componentInParent == null))
		{
			int instanceID = componentInParent.GetInstanceID();
			if (DirectBoundButtons.Add(instanceID))
			{
				componentInParent.onClick.AddListener(action);
				BridgeLog.Warn("Harmony 挂钩缺失，已直接接管按钮 onClick：" + harmonyCapability);
			}
		}
	}

	private static void EnsureLyricLine()
	{
		if (_lyricText != null || _artistText == null)
		{
			return;
		}
		Transform transform = ((_progressSlider != null) ? _progressSlider.transform.parent : _artistText.transform.parent);
		if (transform == null)
		{
			return;
		}
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			Transform child = transform.GetChild(num);
			if (child != null && child.name == "MusicBridgeGameLyric")
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
		RectTransform alignTo = ((_progressSlider != null) ? ((RectTransform)_progressSlider.transform) : _artistText.rectTransform);
		_lyricText = MarqueeText.CreateClippedLyric(transform, _artistText, alignTo, 30f);
		if (!(_lyricText == null))
		{
			_lyricText.transform.parent.name = "MusicBridgeGameLyric";
			_lyricText.fontSize = UiKit.GameTitleAutoMax;
			_lyricText.color = new Color(1f, 1f, 1f, 0.9f);
		}
	}

	private static void LogBottomBarLayout()
	{
		try
		{
			if (_progressSlider == null)
			{
				BridgeLog.Info("[底栏] 没有进度条，跳过诊断。");
				return;
			}
			Transform transform = _progressSlider.transform;
			while (transform != null && transform.name != "MostFrontArea")
			{
				transform = transform.parent;
			}
			if (transform != null)
			{
				for (int i = 0; i < transform.childCount; i++)
				{
					RectTransform rectTransform = transform.GetChild(i) as RectTransform;
					if (!(rectTransform == null))
					{
						BridgeLog.Info("[底栏] MostFrontArea 子[" + i + "] " + rectTransform.name + " active=" + rectTransform.gameObject.activeSelf + " anchorMin=" + rectTransform.anchorMin.ToString() + " anchorMax=" + rectTransform.anchorMax.ToString() + " pos=" + rectTransform.anchoredPosition.ToString() + " size=" + rectTransform.rect.size.ToString());
					}
				}
			}
			Transform transform2 = _progressSlider.transform;
			for (int j = 0; j < 4; j++)
			{
				if (!(transform2 != null))
				{
					break;
				}
				HorizontalLayoutGroup component = transform2.GetComponent<HorizontalLayoutGroup>();
				VerticalLayoutGroup component2 = transform2.GetComponent<VerticalLayoutGroup>();
				GridLayoutGroup component3 = transform2.GetComponent<GridLayoutGroup>();
				BridgeLog.Info("[底栏] 层级[" + j + "] " + transform2.name + " 子节点=" + transform2.childCount + " HLG=" + (component != null) + " VLG=" + (component2 != null) + " Grid=" + (component3 != null));
				if (component != null || component2 != null || component3 != null)
				{
					for (int k = 0; k < transform2.childCount && k < 12; k++)
					{
						Transform child = transform2.GetChild(k);
						LayoutElement component4 = child.GetComponent<LayoutElement>();
						BridgeLog.Info("[底栏]     子[" + k + "] " + child.name + " active=" + child.gameObject.activeSelf + " ignoreLayout=" + (component4 != null && component4.ignoreLayout));
					}
				}
				transform2 = transform2.parent;
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[底栏] 诊断失败：" + ex.Message);
		}
	}

	internal static void SyncShuffleIcon(bool on)
	{
		UpdateAllShuffleButtons(on);
	}

	internal static void SetGameVolume(float volume)
	{
		if (!(_volumeSlider == null))
		{
			float num = Mathf.Clamp01(volume);
			if (!Mathf.Approximately(_volumeSlider.value, num))
			{
				_volumeSlider.value = num;
			}
		}
	}

	internal static void SyncLoopIcon(bool on)
	{
		if (!(_loopImage == null))
		{
			Sprite sprite = (on ? _loopOn : _loopOff);
			if (sprite != null && _loopImage.sprite != sprite)
			{
				_loopImage.sprite = sprite;
			}
		}
	}

	private static void UpdateAllShuffleButtons(bool on)
	{
		if (Time.unscaledTime >= _shuffleRescanAt)
		{
			_shuffleRescanAt = Time.unscaledTime + 3f;
			List<ShuffleRef> list = new List<ShuffleRef>();
			try
			{
				foreach (Component item in FindGameComponents("MusicUI", "Bulbul.MusicUI"))
				{
					if (!(item == null) && item.gameObject.scene.IsValid())
					{
						Traverse val = Traverse.Create((object)item);
						Image value = val.Field("_shuffleChangeButtonImage").GetValue<Image>();
						if (!(value == null))
						{
							list.Add(new ShuffleRef
							{
								Image = value,
								On = val.Field("_shuffleButtonSprite").GetValue<Sprite>(),
								Off = val.Field("_notShuffleBUttonSprite").GetValue<Sprite>()
							});
						}
					}
				}
				foreach (Component item2 in FindGameComponents("Bulbul.MusicPlayListTabUI", "MusicPlayListTabUI"))
				{
					if (!(item2 == null) && item2.gameObject.scene.IsValid())
					{
						Traverse val2 = Traverse.Create((object)item2);
						Image value2 = val2.Field("shuffleChangeButtonImage").GetValue<Image>();
						if (!(value2 == null))
						{
							list.Add(new ShuffleRef
							{
								Image = value2,
								On = val2.Field("enableShuffleSprite").GetValue<Sprite>(),
								Off = val2.Field("disableShuffleSprite").GetValue<Sprite>()
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("扫描随机播放按钮失败：" + ex.Message);
			}
			int num = ((_allShuffleButtons == null) ? (-1) : _allShuffleButtons.Length);
			_allShuffleButtons = list.ToArray();
			if (_allShuffleButtons.Length != num)
			{
				BridgeLog.Info("已接管随机播放图标的按钮数量 = " + _allShuffleButtons.Length);
			}
		}
		if (_allShuffleButtons == null)
		{
			return;
		}
		ShuffleRef[] allShuffleButtons = _allShuffleButtons;
		for (int i = 0; i < allShuffleButtons.Length; i++)
		{
			ShuffleRef shuffleRef = allShuffleButtons[i];
			if (!(shuffleRef.Image == null))
			{
				Sprite sprite = (on ? shuffleRef.On : shuffleRef.Off);
				if (sprite != null && shuffleRef.Image.sprite != sprite)
				{
					shuffleRef.Image.sprite = sprite;
				}
			}
		}
	}

	private static void UpdateAllPlayButtons(Sprite fallbackPlay, Sprite fallbackPause, bool playing, bool bridgeActive)
	{
		if (Time.unscaledTime >= _rescanAt)
		{
			_rescanAt = Time.unscaledTime + 3f;
			List<PlayButtonRef> list = new List<PlayButtonRef>();
			try
			{
				foreach (Component item in FindGameComponents("MusicUI", "Bulbul.MusicUI"))
				{
					if (!(item == null) && item.gameObject.scene.IsValid())
					{
						Traverse val = Traverse.Create((object)item);
						Image value = val.Field("_playOrPauseButtonImage").GetValue<Image>();
						if (!(value == null))
						{
							list.Add(new PlayButtonRef
							{
								Image = value,
								Play = (val.Field("_playButtonSprite").GetValue<Sprite>() ?? fallbackPlay),
								Pause = (val.Field("_pauseButtonSprite").GetValue<Sprite>() ?? fallbackPause),
								NoMusic = val.Field("_noMusicPlayButtonSprite").GetValue<Sprite>()
							});
						}
					}
				}
				foreach (Component item2 in FindGameComponents("Bulbul.MusicPlayListTabUI", "MusicPlayListTabUI"))
				{
					if (!(item2 == null) && item2.gameObject.scene.IsValid())
					{
						Traverse val2 = Traverse.Create((object)item2);
						Image value2 = val2.Field("playOrPauseButtonImage").GetValue<Image>();
						if (!(value2 == null))
						{
							list.Add(new PlayButtonRef
							{
								Image = value2,
								Play = (val2.Field("playButtonSprite").GetValue<Sprite>() ?? fallbackPlay),
								Pause = (val2.Field("pauseButtonSprite").GetValue<Sprite>() ?? fallbackPause),
								NoMusic = val2.Field("noMusicPlayButtonSprite").GetValue<Sprite>()
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("扫描播放按钮失败：" + ex.Message);
			}
			int num = ((_allPlayButtons == null) ? (-1) : _allPlayButtons.Length);
			_allPlayButtons = list.ToArray();
			if (_allPlayButtons.Length != num)
			{
				BridgeLog.Info("已接管播放/暂停图标的按钮数量 = " + _allPlayButtons.Length);
			}
		}
		if (_allPlayButtons == null)
		{
			return;
		}
		PlayButtonRef[] allPlayButtons = _allPlayButtons;
		foreach (PlayButtonRef playButtonRef in allPlayButtons)
		{
			if (playButtonRef.Image == null)
			{
				continue;
			}
			Sprite sprite;
			if (bridgeActive)
			{
				sprite = (playing ? playButtonRef.Pause : playButtonRef.Play);
			}
			else
			{
				if (!(playButtonRef.NoMusic != null) || !(playButtonRef.Image.sprite == playButtonRef.NoMusic))
				{
					continue;
				}
				sprite = playButtonRef.Play;
			}
			if (sprite != null && playButtonRef.Image.sprite != sprite)
			{
				playButtonRef.Image.sprite = sprite;
			}
		}
	}

	private static IEnumerable<Component> FindGameComponents(params string[] typeNames)
	{
		Type type = null;
		for (int i = 0; i < typeNames.Length; i++)
		{
			type = AccessTools.TypeByName(typeNames[i]);
			if (type != null)
			{
				break;
			}
		}
		if (type == null)
		{
			yield break;
		}
		UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(type);
		for (int j = 0; j < array.Length; j++)
		{
			Component component = array[j] as Component;
			if (component != null)
			{
				yield return component;
			}
		}
	}

	public static void Tick()
	{
		if (_titleText == null || _artistText == null)
		{
			return;
		}
		IMusicModule current = MusicModules.Current;
		bool flag = current.Id == MusicProvider.GameBuiltIn;
		if (flag || !current.HasTrack)
		{
			if (_taken)
			{
				_taken = false;
				_titleText.text = _gameTitleBackup ?? "";
				_artistText.text = _gameArtistBackup ?? "";
				if (_lyricText != null)
				{
					_lyricText.text = "";
				}
				BridgeLog.Info("底部播放条已交还给游戏。");
			}
			if (!flag)
			{
				UpdateAllPlayButtons(_playSprite, _pauseSprite, playing: false, bridgeActive: false);
			}
			return;
		}
		if (!_taken)
		{
			_taken = true;
			_gameTitleBackup = _titleText.text;
			_gameArtistBackup = _artistText.text;
			BridgeLog.Info("MusicBridge 接管游戏底部播放条。");
		}
		string text = current.StatusPrefix + current.Title;
		if (_titleText.text != text)
		{
			_titleText.text = text;
		}
		string text2 = current.Artist ?? "";
		if (_artistText.text != text2)
		{
			_artistText.text = text2;
		}
		if (_lyricText != null)
		{
			string text3 = "";
			if (current.SupportsLyrics)
			{
				text3 = LyricsEngine.GetDisplayText(current.Position, out var _);
			}
			if (_lyricText.text != text3)
			{
				_lyricText.text = text3;
			}
		}
		if (_progressSlider != null)
		{
			if (_progressSlider.interactable != current.CanSeek)
			{
				_progressSlider.interactable = current.CanSeek;
			}
			bool flag2 = false;
			try
			{
				flag2 = _draggingProgressField != null && (bool)_draggingProgressField.GetValue(_musicUi);
			}
			catch
			{
			}
			double duration = current.Duration;
			if (flag2)
			{
				_wasDragging = true;
			}
			else
			{
				if (_wasDragging)
				{
					_wasDragging = false;
					if (duration > 0.0 && current.CanSeek)
					{
						current.Seek((double)_progressSlider.value * duration);
					}
				}
				float num = ((duration > 0.0) ? Mathf.Clamp01((float)(current.Position / duration)) : 0f);
				if (Mathf.Abs(_progressSlider.value - num) > 0.0005f)
				{
					_progressSlider.value = num;
				}
			}
		}
		if (_volumeSlider != null)
		{
			float value = _volumeSlider.value;
			if (_lastGameVolume < 0f)
			{
				_lastGameVolume = value;
			}
			if (Mathf.Abs(value - _lastGameVolume) > 0.001f)
			{
				current.SetVolume(value);
				_lastGameVolume = value;
				BridgePanel.SyncVolumeSlider(value);
			}
		}
		UpdateAllPlayButtons(_playSprite, _pauseSprite, current.IsPlaying, bridgeActive: true);
		UpdateAllShuffleButtons(current.Shuffle);
		if (_loopImage != null && _loopOn != null && _loopOff != null)
		{
			Sprite sprite = (current.RepeatOne ? _loopOn : _loopOff);
			if (_loopImage.sprite != sprite)
			{
				_loopImage.sprite = sprite;
			}
		}
	}
}
