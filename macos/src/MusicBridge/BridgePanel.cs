using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class BridgePanel
{
	internal class BridgeSectionKeeper : MonoBehaviour
	{
		private int _pending;

		private float _lastWidth = -1f;

        private float _nextMaintenanceAt;

		private void OnEnable()
		{
			_pending = 5;
		}

		private void LateUpdate()
		{
            // Layout/list maintenance does not need to run at the game's render rate.
            if (Time.unscaledTime >= _nextMaintenanceAt)
            {
                _nextMaintenanceAt = Time.unscaledTime + 0.1f;
			TickRefreshCooldown();
			GameHudLayout.Tick();
			KeepSiblingOrder();
			ApplyGameRowVisibility(_provider == MusicProvider.GameBuiltIn);
			SyncTopSpacer();
			NativeListVirtualizer.TickActive();
			NeteasePanelUi.Tick();
			AppleMusicPanelUi.Tick();
			LocalPanelUi.Tick();
			WatchLayoutAnomaly();
			if (_provider == MusicProvider.AppleMusic)
			{
				RefreshAppleUi();
			}
            }
			if (_pending > 0)
			{
				_pending--;
				Realign();
				return;
			}
			RectTransform rectTransform = base.transform.parent as RectTransform;
			if (!(rectTransform == null))
			{
				float width = rectTransform.rect.width;
				if (_lastWidth < 0f)
				{
					_lastWidth = width;
				}
				else if (Mathf.Abs(width - _lastWidth) > 1f)
				{
					_lastWidth = width;
					_pending = 3;
				}
			}
		}
	}

	private const string SectionName = "MusicBridgeSection";

	private static GameObject _section;

	private static ScrollRect _scrollRect;

	private static GameObject _bodyRoot;

	private static Image _tabNeteaseBg;

	private static TextMeshProUGUI _tabNeteaseLabel;

	private static Image _tabAppleBg;

	private static Image _tabLocalBg;

	private static TextMeshProUGUI _tabLocalLabel;

	private static GameObject _localPanel;

	private static TextMeshProUGUI _tabAppleLabel;

	private static GameObject _neteasePanel;

	private static GameObject _applePanel;

	private static TextMeshProUGUI _serviceTitleText;

	private static TextMeshProUGUI _neteaseStatusText;

	private static TextMeshProUGUI _appleStatusText;

	private static TextMeshProUGUI _appleHintText;

	private static Button _appleConnectButton;

	private static TextMeshProUGUI _appleConnectLabel;

	private static Button _appleSyncButton;

	private static GameObject _neteaseNormalView;

	private static TextMeshProUGUI _neteaseHintText;

	private static Button _connectButton;

	private static TextMeshProUGUI _connectButtonLabel;

	private static GameObject _logoutRow;

	private static GameObject _loginCard;

	private static Image _qrImage;

	private static Button _refreshButton;

	private static LayoutElement _qrLayout;

	private static LayoutElement _qrRowLayout;

	private static TextMeshProUGUI _qrStateText;

	private static Texture2D _qrTexture;

	private static Sprite _qrSprite;

	private static int _renderedQrVersion = -1;

	private static bool _subscribed;

	private static TextMeshProUGUI _collapseLabel;

	private static bool _expanded = true;

	private static bool _autoCollapsedOnce;

	private static Image _coverImage;

	private static TextMeshProUGUI _nowTitleText;

	private static TextMeshProUGUI _nowArtistText;

	private static TextMeshProUGUI _lyricsText;

	private static Slider _volumeSlider;

	private static Slider _progressSlider;

	private static TextMeshProUGUI _positionText;

	private static TextMeshProUGUI _durationText;

	private static Button _playPauseButton;

	private static TextMeshProUGUI _playPauseLabel;

	private static bool _isScrubbing;

	private static long _lyricsTrackId = -1L;

	private static MusicProvider _provider = MusicProvider.Netease;

	private const string TopDockName = "MusicBridgeTopDock";

	private static GameObject _topDock;

	private static GameObject _topSpacer;

	private static bool _loggedSpacerOnce;

	private const string DockName = "MusicBridgeDock";

	private const float DockHeight = 150f;

	private const float SegmentMinWidth = 130f;

	private static GameObject _dock;

	private static GameObject _bottomSpacer;

	private const float RequestCooldownSeconds = 2f;

	private static float _requestCooldownUntil;

	private static string _nowKey;

	private static Button _prevButton;

	private static Button _nextButton;

	private static Image _prevIcon;

	private static Image _playIcon;

	private static Image _nextIcon;

	private static Image _volumeIcon;

	public static MusicProvider ActiveAudioSource => PlaybackCoordinator.Active;

	internal static RectTransform ListViewport
	{
		get
		{
			if (_scrollRect == null)
			{
				return null;
			}
			if (!(_scrollRect.viewport != null))
			{
				return _scrollRect.transform as RectTransform;
			}
			return _scrollRect.viewport;
		}
	}

	public static bool IsAlive => _section != null;

	public static MusicProvider CurrentProvider => _provider;

	public static void ClaimAudio(MusicProvider who)
	{
		PlaybackCoordinator.Claim(who);
	}

	private static void OnTrackChanged(TrackInfo _)
	{
		NeteasePanelUi.RefreshNowPlaying();
	}

	private static void Subscribe()
	{
		if (!_subscribed)
		{
			NeteaseService.StateChanged += RefreshNeteaseUi;
			NeteaseLibrary.Changed += NeteasePanelUi.RequestRebuild;
			if (AudioPlayer.Instance != null)
			{
				AudioPlayer.Instance.TrackChanged += OnTrackChanged;
			}
			_subscribed = true;
		}
	}

	internal static void Unsubscribe()
	{
		if (_subscribed)
		{
			NeteaseService.StateChanged -= RefreshNeteaseUi;
			NeteaseLibrary.Changed -= NeteasePanelUi.RequestRebuild;
			if (AudioPlayer.Instance != null)
			{
				AudioPlayer.Instance.TrackChanged -= OnTrackChanged;
			}
			_subscribed = false;
			BridgeLog.Info("事件订阅已全部注销。");
		}
	}

	internal static string ProviderName(MusicProvider p)
	{
		return p switch
		{
			MusicProvider.Netease => "网易云",
			MusicProvider.AppleMusic => "Apple Music",
			_ => "本地音乐",
		};
	}

	public static void Inject(ScrollRect scrollRect, GameObject buttonsParent)
	{
		if (buttonsParent == null)
		{
			BridgeLog.Warn("注入跳过：_playListButtonsParent 为 null。");
			return;
		}
		Transform transform = buttonsParent.transform;
		if (_section != null && _section.transform.parent == transform)
		{
			BridgeLog.Info("注入跳过：区块已存在于当前列表容器（幂等）。");
			Realign();
			return;
		}
		if (_section != null)
		{
			BridgeLog.Info("列表容器已重建，销毁旧区块并重新注入。");
			UnityEngine.Object.Destroy(_section);
			_section = null;
		}
		DestroyStrays(transform);
		try
		{
			_scrollRect = scrollRect;
			UiKit.ResolveTmpFont();
			UiKit.AdoptGameTextStyle();
			LogHostLayout(transform);
			Build(transform);
			LogSelfLayout();
			BridgeLog.Info("MusicBridge 区块注入完成。父节点=" + transform.name + "，字体=" + UiKit.TmpFontDescription);
		}
		catch (Exception ex)
		{
			BridgeLog.Error("注入失败：" + ex);
			if (_section != null)
			{
				UnityEngine.Object.Destroy(_section);
				_section = null;
			}
		}
	}

	private static void LogHostLayout(Transform parent)
	{
		try
		{
			RectTransform rectTransform = parent as RectTransform;
			VerticalLayoutGroup component = parent.GetComponent<VerticalLayoutGroup>();
			ContentSizeFitter component2 = parent.GetComponent<ContentSizeFitter>();
			Canvas componentInParent = parent.GetComponentInParent<Canvas>();
			BridgeLog.Info("[布局] 父容器 " + parent.name + " rect=" + ((rectTransform != null) ? rectTransform.rect.ToString() : "n/a") + " 子节点数=" + parent.childCount);
			BridgeLog.Info("[布局] 父 VLG " + ((component == null) ? "无" : ("ctrlW=" + component.childControlWidth + " ctrlH=" + component.childControlHeight + " expW=" + component.childForceExpandWidth + " expH=" + component.childForceExpandHeight + " spacing=" + component.spacing + " padding=" + component.padding.left + "," + component.padding.right)));
			BridgeLog.Info("[布局] 父 ContentSizeFitter " + ((component2 == null) ? "无" : ("h=" + component2.horizontalFit.ToString() + " v=" + component2.verticalFit)));
			if (_scrollRect != null && _scrollRect.content != null)
			{
				BridgeLog.Info("[布局] ScrollRect.content=" + _scrollRect.content.name + " rect=" + _scrollRect.content.rect.ToString() + " viewport=" + ((_scrollRect.viewport != null) ? _scrollRect.viewport.rect.ToString() : "n/a"));
			}
			if (componentInParent != null)
			{
				BridgeLog.Info("[布局] Canvas=" + componentInParent.name + " scaleFactor=" + componentInParent.scaleFactor + " renderMode=" + componentInParent.renderMode);
			}
			for (int i = 0; i < parent.childCount && i < 3; i++)
			{
				RectTransform rectTransform2 = parent.GetChild(i) as RectTransform;
				if (rectTransform2 != null)
				{
					BridgeLog.Info("[布局] 现有子节点[" + i + "] " + rectTransform2.name + " rect=" + rectTransform2.rect.ToString() + " anchorMin=" + rectTransform2.anchorMin.ToString() + " anchorMax=" + rectTransform2.anchorMax.ToString() + " offsetMin=" + rectTransform2.offsetMin.ToString() + " offsetMax=" + rectTransform2.offsetMax.ToString());
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[布局] 宿主诊断失败：" + ex.Message);
		}
	}

	private static void LogSelfLayout()
	{
		try
		{
			if (_section == null)
			{
				return;
			}
			RectTransform component = _section.GetComponent<RectTransform>();
			BridgeLog.Info("[布局] 本区块 rect=" + component.rect.ToString() + " anchorMin=" + component.anchorMin.ToString() + " anchorMax=" + component.anchorMax.ToString() + " offsetMin=" + component.offsetMin.ToString() + " offsetMax=" + component.offsetMax.ToString() + " sizeDelta=" + component.sizeDelta.ToString());
			for (int i = 0; i < _section.transform.childCount; i++)
			{
				RectTransform rectTransform = _section.transform.GetChild(i) as RectTransform;
				if (rectTransform != null)
				{
					BridgeLog.Info("[布局]   子 " + rectTransform.name + " active=" + rectTransform.gameObject.activeSelf + " rect=" + rectTransform.rect.ToString() + " pos=" + rectTransform.anchoredPosition.ToString());
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[布局] 自身诊断失败：" + ex.Message);
		}
	}

	private static void DestroyStrays(Transform parent)
	{
		for (int num = parent.childCount - 1; num >= 0; num--)
		{
			Transform child = parent.GetChild(num);
			if (child != null && child.name == "MusicBridgeSection")
			{
				BridgeLog.Warn("发现残留区块，已销毁以保证不重复注入。");
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
	}

	private static void Build(Transform parent)
	{
		_section = new GameObject("MusicBridgeSection");
		_section.transform.SetParent(parent, worldPositionStays: false);
		_section.transform.SetAsFirstSibling();
		RectTransform rectTransform = _section.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.sizeDelta = new Vector2(ResolveHostWidth(parent), 0f);
		Image image = _section.AddComponent<Image>();
		image.sprite = UiSprites.Rounded;
		image.type = Image.Type.Sliced;
		image.color = UiKit.PanelColor;
		VerticalLayoutGroup verticalLayoutGroup = _section.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.spacing = 6f;
		verticalLayoutGroup.padding = new RectOffset(10, 10, 8, 10);
		ContentSizeFitter contentSizeFitter = _section.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_section.AddComponent<BridgeSectionKeeper>();
		BuildDockedHeader();
		BuildTopSpacer(parent);
		NeteasePanelUi.Build((_topDock != null) ? _topDock.transform : _section.transform, _section.transform);
		NeteasePanelUi.SetVisible(visible: false);
		AppleMusicPanelUi.Build((_topDock != null) ? _topDock.transform : _section.transform, _section.transform);
		AppleMusicPanelUi.SetVisible(visible: false);
		BuildDockedPlayer();
		BuildBottomSpacer(parent);
		Subscribe();
		ApplyProvider(_provider, log: false);
		ApplyExpanded(_expanded, log: false);
		RefreshNeteaseUi();
		Realign();
	}

	private static void BuildHeader(Transform parent)
	{
		GameObject gameObject = UiKit.CreateRow(parent, "HeaderRow", 30f, 10f);
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(gameObject.transform, "MUSIC BRIDGE", UiKit.GameArtistFontSize, TextAnchor.MiddleLeft);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.color = UiKit.TextSecondary;
		LayoutElement component = textMeshProUGUI.GetComponent<LayoutElement>();
		component.preferredWidth = 130f;
		component.minWidth = 130f;
		component.flexibleWidth = 0f;
		UiKit.CreateSpacer(gameObject.transform);
		Button button = UiKit.CreatePillButton(gameObject.transform, CollapseLabel(expanded: true), filled: false, UiKit.LineColor, 28f, 96f);
		_collapseLabel = button.GetComponentInChildren<TextMeshProUGUI>();
		if (_collapseLabel != null)
		{
			_collapseLabel.fontSize = 12f;
		}
		button.onClick.AddListener(ToggleExpanded);
	}

	private static void BuildSegmentedControl(Transform parent)
	{
		GameObject gameObject = new GameObject("Segmented");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = 38f;
		layoutElement.minHeight = 38f;
		Image image = gameObject.AddComponent<Image>();
		image.sprite = UiSprites.PillOutline;
		image.type = Image.Type.Sliced;
		image.color = UiKit.LineSoft;
		image.raycastTarget = false;
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.childForceExpandWidth = true;
		horizontalLayoutGroup.childForceExpandHeight = true;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.spacing = 4f;
		horizontalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		CreateTab(gameObject.transform, UiKit.Glyph("●", "*") + " 网易云", MusicProvider.Netease, out _tabNeteaseBg, out _tabNeteaseLabel);
		CreateTab(gameObject.transform, "Apple Music " + UiKit.Glyph("♪", "*"), MusicProvider.AppleMusic, out _tabAppleBg, out _tabAppleLabel);
		CreateTab(gameObject.transform, "本地音乐", MusicProvider.GameBuiltIn, out _tabLocalBg, out _tabLocalLabel);
	}

	private static void CreateTab(Transform parent, string label, MusicProvider provider, out Image bgOut, out TextMeshProUGUI labelOut)
	{
		GameObject gameObject = new GameObject("Tab_" + provider);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.AddComponent<RectTransform>();
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 130f;
		layoutElement.minHeight = 34f;
		Image image = gameObject.AddComponent<Image>();
		image.sprite = UiSprites.Pill;
		image.type = Image.Type.Sliced;
		image.color = new Color(1f, 1f, 1f, 0f);
		Button button = gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
		colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
		colors.selectedColor = Color.white;
		button.colors = colors;
		button.onClick.AddListener(delegate
		{
			ApplyProvider(provider, log: true);
		});
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateStretchText(gameObject.transform, label, 15f, TextAnchor.MiddleCenter);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		bgOut = image;
		labelOut = textMeshProUGUI;
	}

	private static void BuildBody(Transform parent)
	{
		_bodyRoot = UiKit.CreateColumn(parent, "BodyRoot", 6f, new RectOffset(0, 0, 2, 2));
		_serviceTitleText = UiKit.CreateText(_bodyRoot.transform, "网易云音乐", 15f, TextAnchor.MiddleLeft);
		_serviceTitleText.fontStyle = FontStyles.Bold;
		GameObject repairRow = UiKit.CreateRow(_bodyRoot.transform, "AudioRepair", 30f, 8f);
		UiKit.CreatePillButton(repairRow.transform, "修复游戏声音", filled: false, UiKit.LineColor, 30f, 150f).onClick.AddListener(AudioOutputRecovery.RequestManual);
		_neteasePanel = UiKit.CreateColumn(_bodyRoot.transform, "NeteasePanel", 6f);
		_neteaseNormalView = UiKit.CreateColumn(_neteasePanel.transform, "NormalView", 6f);
		_neteaseStatusText = UiKit.CreateText(_neteaseNormalView.transform, "未连接", 13f, TextAnchor.MiddleLeft);
		_neteaseStatusText.color = UiKit.TextSecondary;
		_neteaseHintText = UiKit.CreateText(_neteaseNormalView.transform, "尚未建立 MusicBridge 自己的网易云会话。", 12f, TextAnchor.UpperLeft);
		_neteaseHintText.color = UiKit.TextFaint;
		_neteaseHintText.enableWordWrapping = true;
		_neteaseHintText.GetComponent<LayoutElement>().preferredHeight = 38f;
		_neteaseHintText.GetComponent<LayoutElement>().minHeight = 38f;
		GameObject gameObject = UiKit.CreateRow(_neteaseNormalView.transform, "ActionRow", 32f, 8f);
		_connectButton = UiKit.CreatePillButton(gameObject.transform, "连接网易云", filled: true, UiKit.NeteaseAccent, 32f, 128f);
		_connectButtonLabel = _connectButton.GetComponentInChildren<TextMeshProUGUI>();
		_connectButton.onClick.AddListener(OnConnectNeteaseClicked);
		_logoutRow = new GameObject("LogoutSlot");
		_logoutRow.transform.SetParent(gameObject.transform, worldPositionStays: false);
		_logoutRow.AddComponent<RectTransform>();
		LayoutElement layoutElement = _logoutRow.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = 104f;
		layoutElement.minWidth = 104f;
		layoutElement.flexibleWidth = 0f;
		layoutElement.preferredHeight = 32f;
		layoutElement.minHeight = 32f;
		HorizontalLayoutGroup horizontalLayoutGroup = _logoutRow.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = true;
		horizontalLayoutGroup.childForceExpandHeight = true;
		UiKit.CreatePillButton(_logoutRow.transform, "退出登录", filled: false, UiKit.LineColor, 32f).onClick.AddListener(OnLogoutClicked);
		_logoutRow.SetActive(value: false);
		UiKit.CreateSpacer(gameObject.transform);
		BuildLoginCard(_neteasePanel.transform);
		_applePanel = UiKit.CreateColumn(_bodyRoot.transform, "ApplePanel", 6f);
		_appleStatusText = UiKit.CreateText(_applePanel.transform, "未连接", 13f, TextAnchor.MiddleLeft);
		_appleStatusText.color = UiKit.TextSecondary;
		_appleHintText = UiKit.CreateText(_applePanel.transform, "", 12f, TextAnchor.UpperLeft);
		_appleHintText.color = UiKit.TextFaint;
		_appleHintText.enableWordWrapping = true;
		_appleHintText.GetComponent<LayoutElement>().preferredHeight = 86f;
		_appleHintText.GetComponent<LayoutElement>().minHeight = 86f;
		GameObject gameObject2 = UiKit.CreateRow(_applePanel.transform, "AmActionRow", 32f, 8f);
		_appleConnectButton = UiKit.CreatePillButton(gameObject2.transform, "连接 Apple Music", filled: true, UiKit.AppleAccent, 32f, 156f);
		_appleConnectLabel = _appleConnectButton.GetComponentInChildren<TextMeshProUGUI>();
		_appleConnectButton.onClick.AddListener(OnAppleConnectClicked);
		_appleSyncButton = UiKit.CreatePillButton(gameObject2.transform, "更新播放列表", filled: false, UiKit.LineColor, 32f, 140f);
		_appleSyncButton.onClick.AddListener(OnAppleSyncClicked);
		_appleSyncButton.gameObject.SetActive(value: false);
		UiKit.CreateSpacer(gameObject2.transform);
		_localPanel = UiKit.CreateColumn(_bodyRoot.transform, "LocalPanel", 6f);
		LocalPanelUi.Build((_topDock != null) ? _topDock.transform : _section.transform, _localPanel.transform);
		_localPanel.SetActive(value: false);
	}

	private static void OnAppleConnectClicked()
	{
		if (AppleMusicService.ConnState == AmConnState.Connected)
		{
			BridgeLog.Info("用户点击了“断开连接”（Apple Music）。");
			PlaybackCoordinator.Relinquish(MusicProvider.AppleMusic, MusicProvider.Netease);
			AppleMusicService.Disconnect();
		}
		else if (AppleMusicService.ConnState != AmConnState.Connecting)
		{
			BridgeLog.Info("用户点击了“连接 Apple Music”。");
			AppleMusicService.BeginConnect(force: true);
		}
		RefreshAppleUi();
	}

	private static void OnAppleSyncClicked()
	{
		if (!AppleMusicService.PlaylistsLoading)
		{
			BridgeLog.Info("用户点击了“更新播放列表”：完整扫描并写缓存。");
			AppleMusicService.SyncLibrary();
			RefreshAppleUi();
		}
	}

	private static void RefreshAppleUi()
	{
		if (_appleStatusText == null)
		{
			return;
		}
		string text;
		string text2;
		string text3;
		switch (AppleMusicService.ConnState)
		{
		case AmConnState.Connected:
			text = UiKit.Glyph("●", "*") + " 已连接" + (string.IsNullOrEmpty(AppleMusicService.AccountName) ? "" : (" · " + AppleMusicService.AccountName));
			if (AppleMusicService.PlaylistsLoading)
			{
				if (!string.IsNullOrEmpty(AppleMusicService.ScanProgress))
				{
					text = text + " · " + AppleMusicService.ScanProgress;
				}
				text2 = "第一次连接，正在后台读取播放列表并保存缓存。\n不需要把音乐窗口置于前台。";
				text3 = "正在扫描…";
			}
			else
			{
				text2 = "断开只是让 MusicBridge 不再读取 Apple Music：不会关闭 Apple Music，也不会打断它正在播放的歌。\n歌单缓存会保留，重新连接是秒开的，不用再扫一次。";
				text3 = "断开连接";
			}
			break;
		case AmConnState.Connecting:
			text = "正在连接 Apple Music…";
			text2 = "正在通过 macOS 自动化接口连接「音乐」。";
			text3 = "连接中…";
			break;
		case AmConnState.Failed:
			text = AppleMusicService.StatusText ?? "连接失败";
			text2 = "请先打开 macOS「音乐」App，并允许自动化控制。窗口可以最小化。\n确认好之后点「重试连接」。";
			text3 = "重试连接";
			break;
		default:
			text = "未连接";
			text2 = "连接后会读取你 Apple Music 里的文件夹、播放列表和歌曲。\n首次连接在后台扫描一次并保存缓存，之后直接读取缓存。\n歌曲声音由 Apple Music 应用自身发出（DRM 限制），不在游戏内解码。";
			text3 = "连接 Apple Music";
			break;
		}
        if (_appleStatusText.text != text) _appleStatusText.text = text;
		if (_appleHintText != null)
		{
            if (_appleHintText.text != text2) _appleHintText.text = text2;
		}
		if (_appleConnectLabel != null)
		{
            if (_appleConnectLabel.text != text3) _appleConnectLabel.text = text3;
		}
		if (_appleConnectButton != null)
		{
			_appleConnectButton.interactable = AppleMusicService.ConnState != AmConnState.Connecting && !AppleMusicService.PlaylistsLoading;
		}
		if (_appleSyncButton != null)
		{
			bool flag = AppleMusicService.ConnState == AmConnState.Connected;
			if (_appleSyncButton.gameObject.activeSelf != flag)
			{
				_appleSyncButton.gameObject.SetActive(flag);
			}
			_appleSyncButton.interactable = flag && !AppleMusicService.PlaylistsLoading;
		}
	}

	private static void BuildLoginCard(Transform parent)
	{
		_loginCard = UiKit.CreateColumn(parent, "NeteaseLoginCard", 6f, new RectOffset(10, 10, 8, 10));
		Image image = _loginCard.AddComponent<Image>();
		image.sprite = UiSprites.RoundedOutline;
		image.type = Image.Type.Sliced;
		image.color = UiKit.LineSoft;
		image.raycastTarget = true;
		GameObject gameObject = UiKit.CreateRow(_loginCard.transform, "CardHeader", 22f, 8f);
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateText(gameObject.transform, "扫码登录网易云", 13f, TextAnchor.MiddleLeft);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 1f;
		UiKit.CreatePillButton(gameObject.transform, UiKit.Glyph("×", "X"), filled: false, UiKit.LineColor, 22f, 30f).onClick.AddListener(OnCloseLoginCardClicked);
		GameObject gameObject2 = UiKit.CreateRow(_loginCard.transform, "QrRow", 205f, 0f, TextAnchor.MiddleCenter);
		_qrRowLayout = gameObject2.GetComponent<LayoutElement>();
		RectTransform rectTransform = UiKit.NewRect("Qr", gameObject2.transform);
		_qrLayout = rectTransform.gameObject.AddComponent<LayoutElement>();
		_qrLayout.preferredWidth = 205f;
		_qrLayout.minWidth = 205f;
		_qrLayout.flexibleWidth = 0f;
		_qrLayout.preferredHeight = 205f;
		_qrLayout.minHeight = 205f;
		_qrLayout.flexibleHeight = 0f;
		_qrImage = rectTransform.gameObject.AddComponent<Image>();
		_qrImage.color = Color.white;
		_qrImage.raycastTarget = false;
		_qrImage.preserveAspect = true;
		_qrStateText = UiKit.CreateText(_loginCard.transform, "正在创建二维码…", 12f, TextAnchor.MiddleCenter);
		_qrStateText.color = UiKit.TextSecondary;
		GameObject gameObject3 = UiKit.CreateRow(_loginCard.transform, "CardActions", 28f, 8f, TextAnchor.MiddleCenter);
		_refreshButton = UiKit.CreatePillButton(gameObject3.transform, "刷新二维码", filled: false, UiKit.LineColor, 28f, 108f);
		_refreshButton.onClick.AddListener(OnRefreshQrClicked);
		UiKit.CreatePillButton(gameObject3.transform, "关闭", filled: false, UiKit.LineColor, 28f, 72f).onClick.AddListener(OnCloseLoginCardClicked);
		_loginCard.SetActive(value: false);
	}

	private static void BuildNowPlayingBar(Transform parent)
	{
		GameObject gameObject = UiKit.CreateRow(parent, "NowPlayingBar", 80f, 12f);
		RectTransform rectTransform = UiKit.NewRect("Cover", gameObject.transform);
		LayoutElement layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = 76f;
		layoutElement.minWidth = 76f;
		layoutElement.flexibleWidth = 0f;
		layoutElement.preferredHeight = 76f;
		layoutElement.minHeight = 76f;
		layoutElement.flexibleHeight = 0f;
		_coverImage = rectTransform.gameObject.AddComponent<Image>();
		_coverImage.sprite = UiSprites.Rounded;
		_coverImage.type = Image.Type.Sliced;
		_coverImage.color = UiKit.CoverPlaceholder;
		_coverImage.raycastTarget = false;
		GameObject gameObject2 = new GameObject("TrackText");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.AddComponent<RectTransform>();
		LayoutElement layoutElement2 = gameObject2.AddComponent<LayoutElement>();
		layoutElement2.flexibleWidth = 1f;
		layoutElement2.minWidth = 60f;
		VerticalLayoutGroup verticalLayoutGroup = gameObject2.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		verticalLayoutGroup.spacing = 1f;
		gameObject2.AddComponent<RectMask2D>();
		_nowTitleText = UiKit.CreateGameStyleText(gameObject2.transform, "未播放", isTitle: true);
		_nowTitleText.fontStyle = FontStyles.Bold;
		_nowArtistText = UiKit.CreateGameStyleText(gameObject2.transform, UiKit.Glyph("—", "-"), isTitle: false);
		GameObject gameObject3 = new GameObject("Transport");
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject3.AddComponent<RectTransform>();
		LayoutElement layoutElement3 = gameObject3.AddComponent<LayoutElement>();
		layoutElement3.preferredWidth = 152f;
		layoutElement3.minWidth = 152f;
		layoutElement3.flexibleWidth = 0f;
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject3.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
		horizontalLayoutGroup.spacing = 10f;
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		_prevButton = UiKit.CreateCircleButton(gameObject3.transform, UiKit.Glyph("◀", "|<"), 36f, solid: false);
		Button prevButton = _prevButton;
		_playPauseButton = UiKit.CreateCircleButton(gameObject3.transform, UiKit.Glyph("❚❚", "||"), 46f, solid: true);
		_playPauseLabel = _playPauseButton.GetComponentInChildren<TextMeshProUGUI>();
		_nextButton = UiKit.CreateCircleButton(gameObject3.transform, UiKit.Glyph("▶", ">|"), 36f, solid: false);
		Button nextButton = _nextButton;
		prevButton.onClick.AddListener(delegate
		{
			BridgeLog.Info("点击上一首。");
			MusicTransport.ClaimSelected();
			MusicTransport.Previous();
		});
		_playPauseButton.onClick.AddListener(delegate
		{
			BridgeLog.Info("点击播放/暂停。");
			MusicTransport.ClaimSelected();
			MusicTransport.TogglePlayPause();
		});
		nextButton.onClick.AddListener(delegate
		{
			BridgeLog.Info("点击下一首。");
			MusicTransport.ClaimSelected();
			MusicTransport.Next();
		});
		RectTransform rectTransform2 = UiKit.NewRect("VolumeIcon", gameObject.transform);
		LayoutElement layoutElement4 = rectTransform2.gameObject.AddComponent<LayoutElement>();
		layoutElement4.preferredWidth = 26f;
		layoutElement4.minWidth = 26f;
		layoutElement4.flexibleWidth = 0f;
		layoutElement4.preferredHeight = 26f;
		layoutElement4.minHeight = 26f;
		layoutElement4.flexibleHeight = 0f;
		_volumeIcon = rectTransform2.gameObject.AddComponent<Image>();
		_volumeIcon.raycastTarget = false;
		_volumeIcon.preserveAspect = true;
		_volumeIcon.color = new Color(1f, 1f, 1f, 0.85f);
		_volumeSlider = UiKit.CreateBarSlider(gameObject.transform, interactable: true, 76f);
		_volumeSlider.value = 0.7f;
		_volumeSlider.onValueChanged.AddListener(delegate(float v)
		{
			MusicTransport.SetVolume(v);
		});
	}

	private static void BuildDockedHeader()
	{
		if (_scrollRect == null)
		{
			return;
		}
		Transform transform = ((_scrollRect.viewport != null) ? _scrollRect.viewport.transform : _scrollRect.transform);
		if (transform == null)
		{
			return;
		}
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			Transform child = transform.GetChild(num);
			if (child != null && child.name == "MusicBridgeTopDock")
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
		_topDock = new GameObject("MusicBridgeTopDock");
		_topDock.transform.SetParent(transform, worldPositionStays: false);
		_topDock.transform.SetAsLastSibling();
		RectTransform rectTransform = _topDock.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(1f, 1f);
		rectTransform.pivot = new Vector2(0.5f, 1f);
		rectTransform.offsetMin = new Vector2(0f, 0f);
		rectTransform.offsetMax = new Vector2(-14f, 0f);
		Image image = _topDock.AddComponent<Image>();
		image.sprite = UiSprites.Rounded;
		image.type = Image.Type.Sliced;
		image.color = UiKit.DockOpaque;
		_topDock.AddComponent<RectMask2D>();
		VerticalLayoutGroup verticalLayoutGroup = _topDock.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.spacing = 5f;
		verticalLayoutGroup.padding = new RectOffset(10, 12, 6, 6);
		ContentSizeFitter contentSizeFitter = _topDock.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		BuildHeader(_topDock.transform);
		BuildSegmentedControl(_topDock.transform);
		BuildBody(_topDock.transform);
		BridgeLog.Info("顶部固定停靠层已创建（挂在 " + transform.name + " 顶部）。");
	}

	private static void BuildTopSpacer(Transform content)
	{
		_topSpacer = new GameObject("MusicBridgeTopSpacer");
		_topSpacer.transform.SetParent(content, worldPositionStays: false);
		RectTransform rectTransform = _topSpacer.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.sizeDelta = new Vector2(10f, 0f);
		_topSpacer.transform.SetAsFirstSibling();
	}

	private static void ApplyGameRowVisibility(bool visible)
	{
		if (_section == null)
		{
			return;
		}
		Transform parent = _section.transform.parent;
		if (parent == null)
		{
			return;
		}
		try
		{
			NativeListVirtualizer.SetRegionVisible(visible);
			for (int i = 0; i < parent.childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (child == null)
				{
					continue;
				}
				switch (child.name)
				{
				case "MusicBridgeSection":
				case "MusicBridgeTopDock":
				case "MusicBridgeDock":
				case "MusicBridgeTopSpacer":
				case "MusicBridgeBottomSpacer":
					continue;
				}
				if (!(child.GetComponent<NativeListVirtualizer.VirtualOwned>() != null) && child.gameObject.activeSelf != visible)
				{
					child.gameObject.SetActive(visible);
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("切换游戏列表显隐失败：" + ex.Message);
		}
	}

	internal static int RowRegionStartIndex(Transform container)
	{
		if (_section == null || container == null)
		{
			return 0;
		}
		if (_section.transform.parent != container)
		{
			return 0;
		}
		return _section.transform.GetSiblingIndex() + 1;
	}

	internal static void KeepSiblingOrder()
	{
		if (_section == null)
		{
			return;
		}
		Transform parent = _section.transform.parent;
		if (parent == null)
		{
			return;
		}
		try
		{
			int num = parent.childCount - 1;
			if (_topSpacer != null && _topSpacer.transform.parent == parent && _topSpacer.transform.GetSiblingIndex() != 0)
			{
				_topSpacer.transform.SetAsFirstSibling();
			}
			if (_section.transform.GetSiblingIndex() != 1 && parent.childCount > 1)
			{
				_section.transform.SetSiblingIndex(1);
			}
			if (_bottomSpacer != null && _bottomSpacer.transform.parent == parent && _bottomSpacer.transform.GetSiblingIndex() != num)
			{
				_bottomSpacer.transform.SetAsLastSibling();
				num = parent.childCount - 1;
			}
			if (_dock != null && _dock.transform.parent == parent && _dock.transform.GetSiblingIndex() != num)
			{
				_dock.transform.SetAsLastSibling();
				num = parent.childCount - 1;
			}
			if (_topDock != null && _topDock.transform.parent == parent && _topDock.transform.GetSiblingIndex() != num)
			{
				_topDock.transform.SetAsLastSibling();
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("维持兄弟顺序失败：" + ex.Message);
		}
	}

	internal static void SyncTopSpacer()
	{
		if (_topSpacer == null || _topDock == null)
		{
			return;
		}
		try
		{
			RectTransform rectTransform = (RectTransform)_topDock.transform;
			RectTransform rectTransform2 = (RectTransform)_topSpacer.transform;
			if (!_topDock.activeInHierarchy)
			{
				if (Mathf.Abs(rectTransform2.sizeDelta.y) > 0.5f)
				{
					rectTransform2.sizeDelta = new Vector2(rectTransform2.sizeDelta.x, 0f);
					if (_scrollRect != null && _scrollRect.content != null)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
					}
				}
				return;
			}
			float height = rectTransform.rect.height;
			if (height <= 1f)
			{
				return;
			}
			RectTransform rectTransform3 = (RectTransform)_topSpacer.transform;
			float num = height + 6f;
			if (Mathf.Abs(rectTransform3.sizeDelta.y - num) > 0.5f)
			{
				rectTransform3.sizeDelta = new Vector2(rectTransform3.sizeDelta.x, num);
				if (_scrollRect != null && _scrollRect.content != null)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
				}
				if (!_loggedSpacerOnce)
				{
					_loggedSpacerOnce = true;
					BridgeLog.Info("顶部垫片高度已跟随停靠层：" + num.ToString("0") + "px");
				}
			}
		}
		catch
		{
		}
	}

	public static void SetTopDockVisible(bool visible)
	{
		if (_topDock != null && _topDock.activeSelf != visible)
		{
			_topDock.SetActive(visible);
		}
	}

	private static void BuildDockedPlayer()
	{
		if (_scrollRect == null)
		{
			BridgeLog.Warn("没有 ScrollRect，无法创建停靠播放栏。");
			return;
		}
		Transform parent = _scrollRect.transform.parent;
		if (parent == null)
		{
			BridgeLog.Warn("ScrollRect 没有父节点，无法创建停靠播放栏。");
			return;
		}
		for (int num = parent.childCount - 1; num >= 0; num--)
		{
			Transform child = parent.GetChild(num);
			if (child != null && child.name == "MusicBridgeDock")
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
		_dock = new GameObject("MusicBridgeDock");
		_dock.transform.SetParent(parent, worldPositionStays: false);
		_dock.transform.SetAsLastSibling();
		RectTransform rectTransform = _dock.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 0f);
		rectTransform.anchorMax = new Vector2(1f, 0f);
		rectTransform.pivot = new Vector2(0.5f, 0f);
		rectTransform.offsetMin = new Vector2(0f, -20f);
		rectTransform.offsetMax = new Vector2(0f, 130f);
		Image image = _dock.AddComponent<Image>();
		image.sprite = UiSprites.Rounded;
		image.type = Image.Type.Sliced;
		image.color = UiKit.DockOpaque;
		VerticalLayoutGroup verticalLayoutGroup = _dock.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.spacing = 2f;
		verticalLayoutGroup.padding = new RectOffset(10, 12, 4, 4);
		BuildNowPlayingBar(_dock.transform);
		BuildProgressRow(_dock.transform);
		BuildLyricsRow(_dock.transform);
		BridgeLog.Info("停靠播放栏已创建（挂在 " + parent.name + " 底部，高度 " + 150f + "）。");
	}

	private static void BuildBottomSpacer(Transform content)
	{
		_bottomSpacer = new GameObject("MusicBridgeBottomSpacer");
		_bottomSpacer.transform.SetParent(content, worldPositionStays: false);
		RectTransform rectTransform = _bottomSpacer.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.sizeDelta = new Vector2(10f, 176f);
		_bottomSpacer.transform.SetAsLastSibling();
	}

	public static void SetDockVisible(bool visible)
	{
		if (_dock != null && _dock.activeSelf != visible)
		{
			_dock.SetActive(visible);
		}
	}

	private static void BuildProgressRow(Transform parent)
	{
		GameObject gameObject = UiKit.CreateRow(parent, "ProgressRow", 20f, 6f);
		_positionText = UiKit.CreateGameStyleText(gameObject.transform, "0:00", isTitle: false, TextAnchor.MiddleCenter);
		_positionText.color = UiKit.TextFaint;
		_positionText.GetComponent<LayoutElement>().preferredWidth = 46f;
		_positionText.GetComponent<LayoutElement>().flexibleWidth = 0f;
		_progressSlider = UiKit.CreateBarSlider(gameObject.transform, interactable: true);
		_progressSlider.value = 0f;
		UiKit.AddPressCallbacks(_progressSlider.gameObject, delegate
		{
			_isScrubbing = true;
		}, delegate
		{
			_isScrubbing = false;
			MusicTransport.ClaimSelected();
			MusicTransport.SeekNormalized(_progressSlider.value);
		});
		_durationText = UiKit.CreateGameStyleText(gameObject.transform, "0:00", isTitle: false, TextAnchor.MiddleCenter);
		_durationText.color = UiKit.TextFaint;
		_durationText.GetComponent<LayoutElement>().preferredWidth = 46f;
		_durationText.GetComponent<LayoutElement>().flexibleWidth = 0f;
	}

	private static void BuildLyricsRow(Transform parent)
	{
		if (_progressSlider != null)
		{
			GameObject gameObject = UiKit.CreateRow(parent, "LyricsRow", 30f, 0f);
			HorizontalLayoutGroup component = gameObject.GetComponent<HorizontalLayoutGroup>();
			if (component != null)
			{
				component.padding = new RectOffset(52, 52, 0, 0);
			}
			RectTransform rectTransform = UiKit.NewRect("LyricViewport", gameObject.transform);
			LayoutElement layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
			layoutElement.flexibleWidth = 1f;
			layoutElement.preferredHeight = 28f;
			layoutElement.minHeight = 28f;
			rectTransform.gameObject.AddComponent<RectMask2D>();
			RectTransform rectTransform2 = UiKit.NewRect("LyricText", rectTransform.transform);
			rectTransform2.anchorMin = new Vector2(0f, 0.5f);
			rectTransform2.anchorMax = new Vector2(0f, 0.5f);
			rectTransform2.pivot = new Vector2(0f, 0.5f);
			rectTransform2.anchoredPosition = Vector2.zero;
			rectTransform2.sizeDelta = new Vector2(4000f, 28f);
			_lyricsText = UiKit.AddTextComponent(rectTransform2.gameObject);
			_lyricsText.fontSize = UiKit.GameTitleAutoMax;
			_lyricsText.enableAutoSizing = false;
			_lyricsText.enableWordWrapping = false;
			_lyricsText.overflowMode = TextOverflowModes.Overflow;
			_lyricsText.alignment = TextAlignmentOptions.Left;
			_lyricsText.color = new Color(1f, 1f, 1f, 0.9f);
			_lyricsText.raycastTarget = false;
			_lyricsText.text = "歌词：未连接音乐服务";
			UiKit.ApplyTmpFont(_lyricsText);
			MarqueeText.Attach(_lyricsText, rectTransform);
		}
		else
		{
			BuildLyricsRowFallback(parent);
		}
	}

	private static void BuildLyricsRowFallback(Transform parent)
	{
		GameObject gameObject = UiKit.CreateRow(parent, "LyricsRow", 32f, 8f);
		TextMeshProUGUI textMeshProUGUI = UiKit.CreateGameStyleText(gameObject.transform, UiKit.Glyph("♪", "~"), isTitle: true, TextAnchor.MiddleCenter);
		textMeshProUGUI.color = UiKit.TextFaint;
		textMeshProUGUI.GetComponent<LayoutElement>().preferredWidth = 22f;
		textMeshProUGUI.GetComponent<LayoutElement>().flexibleWidth = 0f;
		_lyricsText = UiKit.CreateGameStyleText(gameObject.transform, "歌词：未连接音乐服务", isTitle: true);
		_lyricsText.GetComponent<LayoutElement>().flexibleWidth = 1f;
		_lyricsText.color = UiKit.TextFaint;
		_lyricsText.GetComponent<LayoutElement>().flexibleWidth = 1f;
	}

	private static void ApplyProvider(MusicProvider provider, bool log)
	{
		_provider = provider;
		bool flag = provider == MusicProvider.Netease;
		bool flag2 = provider == MusicProvider.AppleMusic;
		bool flag3 = provider == MusicProvider.GameBuiltIn;
		SetTab(_tabNeteaseBg, _tabNeteaseLabel, flag, UiKit.NeteaseAccent);
		SetTab(_tabAppleBg, _tabAppleLabel, flag2, UiKit.AppleAccent);
		SetTab(_tabLocalBg, _tabLocalLabel, flag3, UiKit.LocalAccent);
		if (_serviceTitleText != null)
		{
			_serviceTitleText.text = (flag ? "网易云音乐" : (flag2 ? "Apple Music" : "本地音乐"));
		}
		if (_neteasePanel != null)
		{
			_neteasePanel.SetActive(flag);
		}
		if (_applePanel != null)
		{
			_applePanel.SetActive(flag2);
		}
		NeteasePanelUi.SetVisible(flag);
		if (_localPanel != null)
		{
			_localPanel.SetActive(flag3);
		}
		LocalPanelUi.SetVisible(flag3);
		if (flag3)
		{
			LocalPanelUi.RequestRebuild();
		}
		ApplyGameRowVisibility(flag3);
		if (!flag && NeteaseService.CardState != QrCardState.Hidden)
		{
			NeteaseService.CancelLogin("切换到其他音乐服务");
			if (_loginCard != null)
			{
				_loginCard.SetActive(value: false);
			}
			if (_neteaseNormalView != null)
			{
				_neteaseNormalView.SetActive(value: true);
			}
			ReleaseQrTexture();
		}
		RefreshNeteaseUi();
		AppleMusicPanelUi.SetVisible(flag2 && AppleMusicPanelUi.IsBuilt);
		if (flag2)
		{
			if (!AppleMusicService.UserDisconnected)
			{
				AppleMusicService.BeginConnect(force: false);
			}
			AppleMusicPanelUi.RequestRebuild();
			RefreshAppleUi();
		}
		if (log)
		{
			BridgeLog.Info("服务切换：当前标签 = " + ProviderName(provider) + "，发声权 = " + ProviderName(ActiveAudioSource));
		}
		Realign();
		SyncTopSpacer();
	}

	private static void SetTab(Image bg, TextMeshProUGUI label, bool on, Color accent)
	{
		if (bg != null)
		{
			bg.color = (on ? accent : new Color(1f, 1f, 1f, 0f));
		}
		if (label != null)
		{
			label.color = (on ? new Color(1f, 1f, 1f, 1f) : UiKit.TextSecondary);
		}
	}

	private static string CollapseLabel(bool expanded)
	{
		if (!expanded)
		{
			return "展开 " + UiKit.Glyph("▼", "v");
		}
		return "收起 " + UiKit.Glyph("▲", "^");
	}

	private static void ToggleExpanded()
	{
		ApplyExpanded(!_expanded, log: true);
	}

	private static void ApplyExpanded(bool expanded, bool log)
	{
		_expanded = expanded;
		if (_bodyRoot != null)
		{
			_bodyRoot.SetActive(expanded);
		}
		if (_collapseLabel != null)
		{
			_collapseLabel.text = CollapseLabel(expanded);
		}
		if (log)
		{
			BridgeLog.Info("播放列表区域：" + (expanded ? "展开" : "收起"));
		}
		Realign();
	}

	private static bool TryBeginQrRequest(string source)
	{
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime < _requestCooldownUntil)
		{
			BridgeLog.Info("二维码申请被防抖拦截（" + source + "，冷却剩余 " + (_requestCooldownUntil - unscaledTime).ToString("0.0") + " 秒）。");
			return false;
		}
		_requestCooldownUntil = unscaledTime + 2f;
		SetRefreshInteractable(value: false);
		BridgeLog.Info("二维码申请：" + source + "（旧轮询已作废，只保留一条）。");
		NeteaseService.BeginLogin();
		RefreshNeteaseUi();
		return true;
	}

	private static void OnConnectNeteaseClicked()
	{
		TryBeginQrRequest("用户点击“连接网易云”");
	}

	private static void OnRefreshQrClicked()
	{
		TryBeginQrRequest("用户点击“刷新二维码”");
	}

	private static void SetRefreshInteractable(bool value)
	{
		if (!(_refreshButton == null) && _refreshButton.interactable != value)
		{
			_refreshButton.interactable = value;
			TextMeshProUGUI componentInChildren = _refreshButton.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.color = (value ? Color.white : UiKit.TextFaint);
			}
		}
	}

	internal static void TickNowPlaying()
	{
		if (_nowTitleText == null)
		{
			return;
		}
		IMusicModule selected = MusicModules.Selected;
		bool hasTrack = selected.HasTrack;
		string text = selected.Id.ToString() + "\u0001" + (hasTrack ? (selected.Title ?? "") : "");
		if (text != _nowKey)
		{
			_nowKey = text;
			if (selected.SupportsLyrics && hasTrack)
			{
				SyncLyricsForCurrent();
			}
			else
			{
				LyricsEngine.Reset();
				_lyricsTrackId = -1L;
			}
			if (_coverImage != null)
			{
				if (hasTrack)
				{
					selected.ApplyCover(_coverImage);
				}
				else
				{
					_coverImage.sprite = null;
					_coverImage.color = UiKit.CoverPlaceholder;
				}
			}
		}
		else if (hasTrack && _coverImage != null && _coverImage.sprite == null)
		{
			selected.ApplyCover(_coverImage);
		}
		if (!hasTrack)
		{
			_nowTitleText.text = "未播放";
			_nowArtistText.text = selected.IdleHint;
		}
		else
		{
			_nowTitleText.text = selected.StatusPrefix + selected.Title;
			_nowArtistText.text = selected.Artist ?? UiKit.Glyph("—", "-");
		}
		bool flag = hasTrack && selected.IsPlaying;
		if (_playIcon != null)
		{
			Sprite sprite = (flag ? GameNowPlayingBar.GamePauseSprite : GameNowPlayingBar.GamePlaySprite);
			if (sprite != null && _playIcon.sprite != sprite)
			{
				_playIcon.sprite = sprite;
			}
		}
		else if (_playPauseLabel != null)
		{
			string text2 = (flag ? UiKit.Glyph("❚❚", "||") : UiKit.Glyph("▶", ">"));
			if (_playPauseLabel.text != text2)
			{
				_playPauseLabel.text = text2;
			}
		}
		double position = selected.Position;
		double duration = selected.Duration;
		bool flag2 = hasTrack && selected.CanSeek;
		if (_progressSlider != null)
		{
			if (_progressSlider.interactable != flag2)
			{
				_progressSlider.interactable = flag2;
			}
			if (!_isScrubbing)
			{
				_progressSlider.value = ((duration > 0.0) ? Mathf.Clamp01((float)(position / duration)) : 0f);
			}
		}
		double num = ((_isScrubbing && duration > 0.0 && _progressSlider != null) ? ((double)_progressSlider.value * duration) : position);
		if (_positionText != null)
		{
			_positionText.text = FormatTime((float)num);
		}
		if (_durationText != null)
		{
			_durationText.text = FormatTime((float)duration);
		}
		if (_lyricsText != null)
		{
			bool changed;
			string text3 = ((!selected.SupportsLyrics) ? "歌词：该模块不提供歌词" : (hasTrack ? LyricsEngine.GetDisplayText(num, out changed) : "歌词：未播放"));
			if (_lyricsText.text != text3)
			{
				_lyricsText.text = text3;
			}
		}
	}

    private static float _nextTransportUiAt;


	internal static void TickAlways()
	{
		try
		{
			PlaybackCoordinator.TickAutoPause();
            if (Time.unscaledTime < _nextTransportUiAt) return;
            _nextTransportUiAt = Time.unscaledTime + 0.05f; // 20 Hz UI; audio playback is independent.
			LocalAudioMemory.Tick();
			TickNowPlaying();
			GameNowPlayingBar.Tick();
			TickVolumeReadback();
			bool panelVisible = (_provider == MusicProvider.AppleMusic && _section != null && _section.activeInHierarchy) || PlaybackCoordinator.Active == MusicProvider.AppleMusic;
			AppleMusicService.TickPolling(Time.unscaledTime, panelVisible);
			AppleMusicService.TickVolume(Time.unscaledTime);
			AppleMusicService.TickLyrics(Time.unscaledTime, panelVisible);
		}
		catch (Exception ex)
		{
			BridgeLog.Error("常驻刷新异常：" + ex.Message);
		}
	}

	private static void SyncLyricsForCurrent()
	{
		AudioPlayer instance = AudioPlayer.Instance;
		TrackInfo trackInfo = ((instance != null) ? instance.CurrentTrack : null);
		if (trackInfo != null && trackInfo.Id != _lyricsTrackId)
		{
			_lyricsTrackId = trackInfo.Id;
			LyricsEngine.LoadFor(trackInfo);
		}
	}

	internal static void RestyleTransportButtons()
	{
		try
		{
			Sprite sprite = GameNowPlayingBar.GameNextSprite ?? GameNowPlayingBar.GamePrevSprite;
			ApplyGameIcon(_prevButton, ref _prevIcon, sprite, mirror: true);
			ApplyGameIcon(_nextButton, ref _nextIcon, sprite);
			ApplyGameIcon(_playPauseButton, ref _playIcon, GameNowPlayingBar.GamePauseSprite);
			if (_volumeIcon != null && GameNowPlayingBar.GameVolumeSprite != null)
			{
				_volumeIcon.sprite = GameNowPlayingBar.GameVolumeSprite;
			}
			if (_playIcon != null || _prevIcon != null || _nextIcon != null)
			{
				BridgeLog.Info("侧边栏传输按键已换用游戏原生图标。");
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("替换传输按键图标失败：" + ex.Message);
		}
	}

	private static void ApplyGameIcon(Button button, ref Image icon, Sprite sprite, bool mirror = false)
	{
		if (button == null || sprite == null)
		{
			return;
		}
		if (icon == null)
		{
			TextMeshProUGUI componentInChildren = button.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.enabled = false;
			}
			Image component = button.GetComponent<Image>();
			if (component != null)
			{
				component.color = new Color(1f, 1f, 1f, 0f);
			}
			Transform transform = button.transform.Find("PressFill");
			if (transform != null)
			{
				Image component2 = transform.GetComponent<Image>();
				if (component2 != null)
				{
					component2.color = new Color(1f, 1f, 1f, 0f);
				}
			}
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
			colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
			RectTransform rectTransform = UiKit.NewRect("GameIcon", button.transform);
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.offsetMin = new Vector2(2f, 2f);
			rectTransform.offsetMax = new Vector2(-2f, -2f);
			icon = rectTransform.gameObject.AddComponent<Image>();
			icon.raycastTarget = false;
			icon.preserveAspect = true;
			button.targetGraphic = icon;
		}
		icon.sprite = sprite;
		icon.color = Color.white;
		icon.rectTransform.localScale = (mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one);
	}

	internal static void SyncVolumeSlider(float value)
	{
		if (!(_volumeSlider == null) && Mathf.Abs(_volumeSlider.value - value) > 0.001f)
		{
			_volumeSlider.SetValueWithoutNotify(value);
		}
	}

	private static void TickVolumeReadback()
	{
		if (!(_volumeSlider == null))
		{
			float volume = MusicModules.Current.Volume;
			if (!(volume < 0f))
			{
				SyncVolumeSlider(Mathf.Clamp01(volume));
			}
		}
	}

	private static string FormatTime(float seconds)
	{
		if (seconds <= 0f || float.IsNaN(seconds))
		{
			return "0:00";
		}
		int num = Mathf.FloorToInt(seconds);
		return num / 60 + ":" + (num % 60).ToString("00");
	}

	internal static void TickRefreshCooldown()
	{
		if (!(_refreshButton == null))
		{
			SetRefreshInteractable(Time.unscaledTime >= _requestCooldownUntil && NeteaseService.CardState != QrCardState.Creating);
		}
	}

	private static void OnCloseLoginCardClicked()
	{
		NeteaseService.CancelLogin("用户关闭登录卡片");
		RefreshNeteaseUi();
	}

	private static void OnLogoutClicked()
	{
		BridgeLog.Info("用户点击了“退出登录”。");
		NeteaseService.Logout();
		RefreshNeteaseUi();
	}

	public static void RefreshNeteaseUi()
	{
		if (_neteasePanel == null)
		{
			return;
		}
		try
		{
			bool flag = NeteaseService.CardState != QrCardState.Hidden;
			bool num = NeteaseService.ConnState == NeteaseConnState.Connected;
			if (_loginCard != null && _loginCard.activeSelf != flag)
			{
				_loginCard.SetActive(flag);
			}
			if (_neteaseNormalView != null && _neteaseNormalView.activeSelf == flag)
			{
				_neteaseNormalView.SetActive(!flag);
			}
			if (num && !_autoCollapsedOnce)
			{
				_autoCollapsedOnce = true;
				ApplyExpanded(expanded: false, log: false);
				BridgeLog.Info("已连接：账号信息区自动收起（点“展开”可查看/退出登录）。");
			}
			if (flag)
			{
				UpdateLoginCard();
			}
			else
			{
				ReleaseQrTexture();
			}
			UpdateNormalView();
			Realign();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("刷新网易云面板失败：" + ex.Message);
		}
	}

	private static void UpdateNormalView()
	{
		if (!(_neteaseStatusText == null))
		{
			bool flag = false;
			string text = "连接网易云";
			string text2;
			string text3;
			switch (NeteaseService.ConnState)
			{
			case NeteaseConnState.Connected:
				text2 = UiKit.Glyph("●", "*") + " 已连接 · " + (string.IsNullOrEmpty(NeteaseService.Nickname) ? "已登录账号" : NeteaseService.Nickname);
				text3 = "会话已保存到 macOS 钥匙串，重启游戏会自动恢复。";
				flag = true;
				text = "重新扫码";
				break;
			case NeteaseConnState.Restoring:
				text2 = "正在恢复会话…";
				text3 = "正在用已保存的会话校验登录状态。";
				break;
			case NeteaseConnState.NeedsReconnect:
				text2 = "会话失效，需要重新连接";
				text3 = "保存的会话已被服务端判定为无效。会话文件仍保留，点“退出登录”可清除。";
				flag = true;
				break;
			case NeteaseConnState.NetworkUnavailable:
				text2 = "网络不可用，稍后重试";
				text3 = "无法连接网易云服务端。已保存的会话未被删除。";
				flag = true;
				break;
			case NeteaseConnState.SessionCorrupted:
				text2 = "会话文件无法解密";
				text3 = "文件已保留未删除。可点“退出登录”清除后重新扫码。";
				flag = true;
				break;
			default:
				text2 = "未连接";
				text3 = "点击“连接网易云”，用手机网易云扫码建立 MusicBridge 自己的会话。";
				break;
			}
			_neteaseStatusText.text = text2;
			if (_neteaseHintText != null)
			{
				_neteaseHintText.text = text3;
			}
			if (_connectButtonLabel != null)
			{
				_connectButtonLabel.text = text;
			}
			if (_logoutRow != null && _logoutRow.activeSelf != flag)
			{
				_logoutRow.SetActive(flag);
			}
		}
	}

	private static void UpdateLoginCard()
	{
		if (!(_qrStateText == null))
		{
			switch (NeteaseService.CardState)
			{
			case QrCardState.Creating:
				_qrStateText.text = "正在创建二维码…";
				break;
			case QrCardState.WaitingScan:
				_qrStateText.text = "等待扫码 · 请用手机网易云 App 扫描";
				break;
			case QrCardState.ScannedWaitingConfirm:
				_qrStateText.text = "已扫码，等待手机确认";
				break;
			case QrCardState.Success:
				_qrStateText.text = "登录成功";
				break;
			case QrCardState.Expired:
				_qrStateText.text = "二维码已过期 · 点“刷新二维码”重新获取";
				break;
			case QrCardState.NetworkError:
				_qrStateText.text = "网络错误 · 无法连接网易云服务端";
				break;
			case QrCardState.Failed:
				_qrStateText.text = "登录失败 · 请重试";
				break;
			}
			string qrPayload = NeteaseService.QrPayload;
			if (!string.IsNullOrEmpty(qrPayload) && (_renderedQrVersion != NeteaseService.QrPayloadVersion || !(_qrSprite != null)))
			{
				BuildQrTexture(qrPayload);
				_renderedQrVersion = NeteaseService.QrPayloadVersion;
			}
		}
	}

	private static void BuildQrTexture(string payload)
	{
		try
		{
			bool[,] array = QrEncoder.Encode(payload);
			if (array == null)
			{
				BridgeLog.Warn("二维码内容超出编码器容量。");
				return;
			}
			int length = array.GetLength(0);
			int num = Mathf.Clamp(250 / (length + 8), 4, 10);
			int num2 = (length + 8) * num;
			ReleaseQrTexture();
			Texture2D texture2D = new Texture2D(num2, num2, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array2 = new Color32[num2 * num2];
			Color32 color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			Color32 color2 = new Color32(0, 0, 0, byte.MaxValue);
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = color;
			}
			for (int j = 0; j < length; j++)
			{
				for (int k = 0; k < length; k++)
				{
					if (!array[k, j])
					{
						continue;
					}
					int num3 = (k + 4) * num;
					int num4 = (length - 1 - j + 4) * num;
					for (int l = 0; l < num; l++)
					{
						for (int m = 0; m < num; m++)
						{
							array2[(num4 + l) * num2 + (num3 + m)] = color2;
						}
					}
				}
			}
			texture2D.SetPixels32(array2);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			_qrTexture = texture2D;
			_qrSprite = Sprite.Create(texture2D, new Rect(0f, 0f, num2, num2), new Vector2(0.5f, 0.5f), 100f);
			if (_qrImage != null)
			{
				_qrImage.sprite = _qrSprite;
				_qrImage.color = Color.white;
			}
			if (_qrLayout != null)
			{
				_qrLayout.preferredWidth = num2;
				_qrLayout.minWidth = num2;
				_qrLayout.preferredHeight = num2;
				_qrLayout.minHeight = num2;
			}
			if (_qrRowLayout != null)
			{
				_qrRowLayout.preferredHeight = num2;
				_qrRowLayout.minHeight = num2;
			}
			BridgeLog.Info("二维码已渲染（" + length + "x" + length + " 模块，每模块 " + num + "px，纹理与显示均为 " + num2 + "px，内容不记录）。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("渲染二维码失败：" + ex.Message);
		}
	}

	private static void ReleaseQrTexture()
	{
		if (_qrImage != null)
		{
			_qrImage.sprite = null;
		}
		if (_qrSprite != null)
		{
			UnityEngine.Object.Destroy(_qrSprite);
			_qrSprite = null;
		}
		if (_qrTexture != null)
		{
			UnityEngine.Object.Destroy(_qrTexture);
			_qrTexture = null;
		}
		_renderedQrVersion = -1;
	}

	private static void OnTransportClicked(string what)
	{
		BridgeLog.Info("播放控制按钮被点击：" + what + "（当前无已连接的音乐服务，未发出任何指令）");
	}

	private static float ResolveHostWidth(Transform parent)
	{
		float num = 0f;
		RectTransform rectTransform = parent as RectTransform;
		if (rectTransform != null)
		{
			num = rectTransform.rect.width;
		}
		if (num <= 1f && _scrollRect != null && _scrollRect.viewport != null)
		{
			num = _scrollRect.viewport.rect.width;
		}
		if (num <= 1f && _scrollRect != null)
		{
			num = ((RectTransform)_scrollRect.transform).rect.width;
		}
		return num;
	}

	internal static void WatchLayoutAnomaly()
	{
	}

	public static void Realign()
	{
		if (_section == null)
		{
			return;
		}
		try
		{
			RectTransform component = _section.GetComponent<RectTransform>();
			Transform parent = _section.transform.parent;
			if (parent == null)
			{
				return;
			}
			component.anchorMin = new Vector2(0f, 1f);
			component.anchorMax = new Vector2(0f, 1f);
			component.pivot = new Vector2(0f, 1f);
			float num = ResolveHostWidth(parent) - 14f;
			if (num > 1f && Mathf.Abs(component.sizeDelta.x - num) > 0.5f)
			{
				component.sizeDelta = new Vector2(num, component.sizeDelta.y);
			}
			if (_topSpacer != null)
			{
				_topSpacer.transform.SetAsFirstSibling();
			}
			if (_bottomSpacer != null)
			{
				_bottomSpacer.transform.SetAsLastSibling();
			}
			if (_dock != null)
			{
				_dock.transform.SetAsLastSibling();
			}
			if (_topDock != null)
			{
				_topDock.transform.SetAsLastSibling();
			}
			SyncTopSpacer();
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(component);
			if (_scrollRect != null && _scrollRect.content != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
			}
			TMP_Text[] componentsInChildren = _section.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].SetAllDirty();
			}
			if (_dock != null)
			{
				componentsInChildren = _dock.GetComponentsInChildren<TMP_Text>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].SetAllDirty();
				}
			}
			componentsInChildren = _section.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].SetAllDirty();
			}
			if (_dock != null)
			{
				componentsInChildren = _dock.GetComponentsInChildren<TMP_Text>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].SetAllDirty();
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("重排失败：" + ex.Message);
		}
	}

	public static void OnPlaylistActivated()
	{
		if (_section == null)
		{
			BridgeLog.Warn("播放列表打开，但 MusicBridge 区块尚未注入（等待 MusicPlayListView.Setup）。");
			return;
		}
		_section.SetActive(value: true);
		Realign();
		SetDockVisible(visible: true);
		SetTopDockVisible(visible: true);
		BridgeLog.Info("播放列表打开：MusicBridge 区块已显示。");
	}

	public static void OnPlaylistDeactivated()
	{
		if (!(_section == null))
		{
			SetDockVisible(visible: false);
			SetTopDockVisible(visible: false);
			_section.SetActive(value: false);
			BridgeLog.Info("播放列表关闭：MusicBridge 区块已隐藏。");
		}
	}

	public static int CountSectionsInScene()
	{
		int num = 0;
		RectTransform[] array = Resources.FindObjectsOfTypeAll<RectTransform>();
		foreach (RectTransform rectTransform in array)
		{
			if (rectTransform != null && rectTransform.name == "MusicBridgeSection" && rectTransform.gameObject.scene.IsValid())
			{
				num++;
			}
		}
		return num;
	}
}
