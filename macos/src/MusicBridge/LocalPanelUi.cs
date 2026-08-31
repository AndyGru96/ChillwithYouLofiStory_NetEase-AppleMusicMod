using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class LocalPanelUi
{
	private static GameObject _root;

	private static GameObject _listRoot;

	private static GameObject _topControls;

	private static GameObject _statusBar;

	private static TextMeshProUGUI _statusHead;

	private static MarqueeText _statusDetail;

	private static bool _rebuildQueued;

	private static int _renderLimit = 60;

	private static string _lastSignature;

	public static bool IsBuilt => _root != null;

	private static float TrackRowHeight => MusicBridgeOptions.Current.UI.TrackRowHeightPixels;

	public static void Build(Transform topParent, Transform listParent)
	{
		if (!(_root != null))
		{
			_topControls = UiKit.CreateColumn(topParent, "LocalTopControls", 5f);
			_statusBar = UiKit.CreateStatusRowWithMarquee(_topControls.transform, "LocalStatusBar", 22f, UiKit.GameArtistFontSize, out _statusHead, out _statusDetail);
			_root = new GameObject("LocalPanel");
			_root.transform.SetParent(listParent, worldPositionStays: false);
			_root.AddComponent<RectTransform>();
			_root.AddComponent<LayoutElement>().flexibleWidth = 1f;
			VerticalLayoutGroup verticalLayoutGroup = _root.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			verticalLayoutGroup.spacing = 2f;
			_listRoot = _root;
			PanelRows.MarkOwned(_listRoot, MusicProvider.GameBuiltIn);
			RequestRebuild();
		}
	}

	public static void SetVisible(bool visible)
	{
		if (_root != null && _root.activeSelf != visible)
		{
			_root.SetActive(visible);
		}
		if (_topControls != null && _topControls.activeSelf != visible)
		{
			_topControls.SetActive(visible);
		}
	}

	public static void RequestRebuild()
	{
		_rebuildQueued = true;
	}

	public static void Tick()
	{
		SyncStatusBar();
		if (!(_root == null) && _root.activeInHierarchy)
		{
			IList<LocalTrack> tracks = LocalMusicSource.Tracks;
			LocalTrack playing = LocalMusicSource.Playing;
			string text = tracks.Count + "\u0001" + ((playing != null) ? playing.Title : "") + "\u0001" + _renderLimit;
			if (_rebuildQueued || !(text == _lastSignature))
			{
				_lastSignature = text;
				_rebuildQueued = false;
				Rebuild(tracks, playing);
			}
		}
	}

	private static void Rebuild(IList<LocalTrack> tracks, LocalTrack playing)
	{
		for (int num = _listRoot.transform.childCount - 1; num >= 0; num--)
		{
			Object.Destroy(_listRoot.transform.GetChild(num).gameObject);
		}
		LocalMusicSource.EnsureCaptured();
		if (!LocalMusicSource.Available || tracks.Count == 0)
		{
			return;
		}
		int num2 = 0;
		for (int i = 0; i < tracks.Count; i++)
		{
			if (tracks[i].IsImported)
			{
				num2++;
			}
		}
		foreach (string item in Notes(num2))
		{
			PanelRows.SectionLabel(_listRoot.transform, "LocalNote", item, 0f);
		}
	}

	private static IEnumerable<string> Notes(int imported)
	{
		int keep = 100;
		if (!LocalImportPolicy.Unlimited)
		{
			yield return "游戏自带的导入上限为 " + keep + " 首。可在配置里开启 Local.UnlimitedImport 解除。";
		}
		else if (imported > keep)
		{
			yield return "游戏存档最多保留 " + keep + " 首导入曲目，超出的部分由本插件单独保管。";
			yield return "列表里 #1–#" + keep + " 卸载插件后仍然保留，#" + (keep + 1) + " 以后会随插件一起移除。";
		}
		else
		{
			yield return "游戏存档最多保留 " + keep + " 首导入曲目，超出的部分将由本插件单独保管。";
		}
	}

	private static void SyncStatusBar()
	{
		if (_statusBar == null || _statusHead == null)
		{
			return;
		}
		string text2;
		string text;
		if (!LocalMusicSource.Available)
		{
			text = "本地音乐";
			text2 = "尚未读到游戏播放列表，进入房间后会自动出现";
		}
		else
		{
			IList<LocalTrack> tracks = LocalMusicSource.Tracks;
			int num = 0;
			for (int i = 0; i < tracks.Count; i++)
			{
				if (tracks[i].IsImported)
				{
					num++;
				}
			}
			int num2 = num - 100;
			if (num <= 0)
			{
				text = "尚未导入本地音乐";
				text2 = "在游戏的音乐面板里点「导入」添加";
			}
			else
			{
				text = "已导入 " + num + " 首" + ((num2 > 0) ? ("（超额 " + num2 + "）") : "");
				text2 = NowPlayingDetail();
			}
		}
		text += " - ";
		if (_statusHead.text != text)
		{
			_statusHead.text = text;
		}
		if (_statusDetail != null)
		{
			_statusDetail.SetContent(text2 ?? "");
		}
	}

	private static string NowPlayingDetail()
	{
		LocalTrack playing = LocalMusicSource.Playing;
		if (playing == null || string.IsNullOrEmpty(playing.Title))
		{
			return "在下方列表里点一首开始播放";
		}
		string text = LocalTrackNumbering.Format(LocalTrackNumbering.NumberOf(playing.Raw));
		string text2 = NeteasePanelUi.ModeSuffix(LocalMusicSource.IsShuffle, LocalMusicSource.IsRepeatOne);
		bool gameThinksItIsPlaying = GameNowPlayingBar.GameThinksItIsPlaying;
		return (gameThinksItIsPlaying ? "正在播放" : "已暂停") + ((text.Length > 0) ? (" · " + text) : "") + " · " + playing.Title + text2;
	}

	public static void ResetState()
	{
		_renderLimit = 60;
		_lastSignature = null;
		RequestRebuild();
	}
}
