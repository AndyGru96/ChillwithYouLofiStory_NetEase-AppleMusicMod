using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class AppleMusicPanelUi
{
	private sealed class AppleTrackSource : IVirtualTrackSource
	{
		private readonly AmPlaylist _owner;

		public int Count
		{
			get
			{
				if (_owner == null)
				{
					return 0;
				}
				return _owner.Tracks.Count;
			}
		}

		public long CurrentId
		{
			get
			{
				SmtcSnapshot nowPlaying = AppleMusicService.NowPlaying;
				if (nowPlaying == null || !nowPlaying.Valid)
				{
					return 0L;
				}
				return NameKey(nowPlaying.Title);
			}
		}

		public AppleTrackSource(AmPlaylist owner)
		{
			_owner = owner;
		}

		private static long NameKey(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return 0L;
			}
			long num = 1469598103934665603L;
			for (int i = 0; i < name.Length; i++)
			{
				num ^= name[i];
				num *= 1099511628211L;
			}
			if (num != 0L)
			{
				return num;
			}
			return 1L;
		}

		public long IdAt(int index)
		{
			if (_owner == null || index < 0 || index >= _owner.Tracks.Count)
			{
				return 0L;
			}
			AmTrack amTrack = _owner.Tracks[index];
			if (amTrack == null)
			{
				return 0L;
			}
			return NameKey(amTrack.Name);
		}

		public void Bind(PanelRows.TrackRow row, int index, bool isCurrent)
		{
			if (_owner == null || index < 0 || index >= _owner.Tracks.Count)
			{
				return;
			}
			AmTrack amTrack = _owner.Tracks[index];
			if (amTrack != null)
			{
				string text = amTrack.Artists ?? "";
				if (!string.IsNullOrEmpty(amTrack.Album))
				{
					text = text + " · " + amTrack.Album;
				}
				row.Root.name = "AmTR";
				PanelRows.BindTrackRow(row, index, isCurrent, IdAt(index), amTrack.Name, text, amTrack.DurationText, playable: true, null, 0);
			}
		}

		public void Activate(int index)
		{
			if (_owner != null && index >= 0 && index < _owner.Tracks.Count)
			{
				AmTrack amTrack = _owner.Tracks[index];
				if (amTrack != null)
				{
					BridgeLog.History("[AM] 点击歌曲行：" + amTrack.Name + " · " + amTrack.Artists);
					PlaybackCoordinator.MarkUserChose();
					AppleMusicService.PlayTrack(_owner, amTrack);
				}
			}
		}
	}

	private const float Indent = 0f;

	private static GameObject _root;

	private static GameObject _subEntryRow;

	private static GameObject _listRoot;

	private static AmSection _section = AmSection.Playlists;

	private static bool _playlistsExpanded = true;

	private static readonly Dictionary<AmSection, Button> SubEntryButtons = new Dictionary<AmSection, Button>();

	private static bool _rebuildQueued;

	private static GameObject _statusBar;

	private static TextMeshProUGUI _statusHead;

	private static MarqueeText _statusDetail;

	private const float ListSpacing = 3f;

	private static VirtualTrackList _virtualTracks;

	private static float FolderIndent => MusicBridgeOptions.Current.UI.FolderIndentPixels;

	private static float PlaylistRowHeight => MusicBridgeOptions.Current.UI.PlaylistRowHeightPixels;

	private static float TrackRowHeight => MusicBridgeOptions.Current.UI.TrackRowHeightPixels;

	public static bool IsBuilt => _root != null;

	public static void Build(Transform topParent, Transform listParent)
	{
		_root = UiKit.CreateColumn(topParent, "AppleTopControls", 5f);
		BuildSubEntryRow(_root.transform);
		BuildStatusBar(_root.transform);
		_listRoot = UiKit.CreateColumn(listParent, "AppleListRoot", 3f);
		PanelRows.MarkOwned(_listRoot, MusicProvider.AppleMusic);
		ApplySection(_section, log: false);
	}

	private static void BuildStatusBar(Transform parent)
	{
		_statusBar = UiKit.CreateStatusRowWithMarquee(parent, "AmStatusBar", 22f, UiKit.GameArtistFontSize, out _statusHead, out _statusDetail);
	}

	private static void SyncStatusBar()
	{
		if (!(_statusBar == null) && !(_statusHead == null))
		{
			string text = StatusDetail();
			string text2 = ((AppleMusicService.ConnState == AmConnState.Connected) ? ("已连接" + (string.IsNullOrEmpty(AppleMusicService.AccountName) ? "" : (" · " + AppleMusicService.AccountName))) : "未连接苹果音乐");
			if (!string.IsNullOrEmpty(text))
			{
				text2 += " - ";
			}
			if (_statusHead.text != text2)
			{
				_statusHead.text = text2;
			}
			if (_statusDetail != null)
			{
				_statusDetail.SetContent(text ?? "");
			}
		}
	}

	private static string StatusDetail()
	{
		string text = AppleMusicService.HintText;
		if (string.IsNullOrEmpty(text))
		{
			text = AppleMusicService.ScanProgress;
		}
		if (string.IsNullOrEmpty(text))
		{
			text = AppleMusicService.PlaylistsError;
		}
		if (string.IsNullOrEmpty(text))
		{
			text = AppleMusicService.PlaylistsError;
		}
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		SmtcSnapshot nowPlaying = AppleMusicService.NowPlaying;
		if (nowPlaying != null && nowPlaying.Valid && !string.IsNullOrEmpty(nowPlaying.Title))
		{
			string text2 = NeteasePanelUi.ModeSuffix(AppleMusicService.Shuffle, AppleMusicService.RepeatOne);
			string text3 = (AppleMusicService.RepeatOne ? "" : " · 队列播完后从头继续");
			return (nowPlaying.IsPlaying ? "正在播放" : "已暂停") + " · " + nowPlaying.Title + text2 + text3;
		}
		if (AppleMusicService.ConnState == AmConnState.Connected)
		{
			return null;
		}
		return AppleMusicService.StatusText;
	}

	private static int CountAll(List<AmPlaylist> list)
	{
		int num = 0;
		if (list == null)
		{
			return 0;
		}
		foreach (AmPlaylist item in list)
		{
			num++;
			num += CountAll(item.Children);
		}
		return num;
	}

	public static void SetVisible(bool visible)
	{
		if (_root != null && _root.activeSelf != visible)
		{
			_root.SetActive(visible);
		}
		if (_listRoot != null && _listRoot.activeSelf != visible)
		{
			_listRoot.SetActive(visible);
		}
	}

	private static void BuildSubEntryRow(Transform parent)
	{
		_subEntryRow = UiKit.CreateRow(parent, "AmSubEntries", 30f, 6f, TextAnchor.MiddleCenter);
		SubEntryButtons.Clear();
		AddSubEntry("播放列表", AmSection.Playlists, 92f);
		AddSubEntry("我喜欢", AmSection.Favourites, 76f);
	}

	private static void AddSubEntry(string label, AmSection section, float width)
	{
		Button button = UiKit.CreatePillButton(_subEntryRow.transform, label, filled: false, UiKit.LineColor, 26f, width);
		button.onClick.AddListener(delegate
		{
			ApplySection(section, log: true);
		});
		SubEntryButtons[section] = button;
	}

	private static void ApplySection(AmSection section, bool log)
	{
		_section = section;
		foreach (KeyValuePair<AmSection, Button> subEntryButton in SubEntryButtons)
		{
			Image component = subEntryButton.Value.GetComponent<Image>();
			TextMeshProUGUI componentInChildren = subEntryButton.Value.GetComponentInChildren<TextMeshProUGUI>();
			bool flag = subEntryButton.Key == section;
			if (component != null)
			{
				component.color = (flag ? UiKit.AppleAccent : UiKit.LineColor);
			}
			if (componentInChildren != null)
			{
				componentInChildren.color = (flag ? Color.white : UiKit.TextSecondary);
			}
		}
		if (log)
		{
			BridgeLog.Info("[AM] 子入口切换 -> " + section);
		}
		AppleMusicService.LoadPlaylists(force: false);
		Rebuild();
	}

	public static void RequestRebuild()
	{
		_rebuildQueued = true;
	}

	public static void Tick()
	{
		if (_rebuildQueued)
		{
			_rebuildQueued = false;
			Rebuild();
		}
		SyncStatusBar();
	}

	public static void Rebuild()
	{
		if (_listRoot == null)
		{
			return;
		}
		try
		{
			_virtualTracks = null;
			for (int num = _listRoot.transform.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.DestroyImmediate(_listRoot.transform.GetChild(num).gameObject);
			}
			if (AppleMusicService.ConnState != AmConnState.Connected)
			{
				BuildDisconnected();
			}
			else
			{
				switch (_section)
				{
				case AmSection.Playlists:
					BuildPlaylists();
					break;
				case AmSection.Favourites:
					BuildFavourites();
					break;
				}
			}
			BridgePanel.Realign();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("[AM] 重建内容区失败：" + ex.Message);
		}
	}

	private static void BuildDisconnected()
	{
		CreateStatusRow(AppleMusicService.StatusText ?? "未连接", 0f);
		CreateActionRow((AppleMusicService.ConnState == AmConnState.Connecting) ? "重新连接" : "连接 Apple Music", 0f, delegate
		{
			AppleMusicService.BeginConnect(force: true);
		});
		CreateStatusRow("需要 Apple Music 应用正在运行并已登录。", 0f);
	}

	private static void BuildPlaylists()
	{
		int count = AppleMusicService.Playlists.Count;
		CreateGroupHeader("播放列表" + ((count > 0) ? (" (" + count + ")") : ""), _playlistsExpanded, delegate
		{
			_playlistsExpanded = !_playlistsExpanded;
			RequestRebuild();
		});
		if (!_playlistsExpanded)
		{
			return;
		}
		if (AppleMusicService.PlaylistsLoading)
		{
			if (count == 0)
			{
				CreateStatusRow("同步期间 Apple Music 会占据前台，请先不要操作。", 0f);
			}
			return;
		}
		if (count == 0)
		{
			CreateActionRow("更新播放列表", 0f, delegate
			{
				AppleMusicService.SyncLibrary();
			});
			CreateStatusRow("同步会读取全部文件夹、播放列表和歌曲，期间 Apple Music 会占据前台几分钟；完成后一切走本地缓存，不再打扰。", 0f);
			return;
		}
		foreach (AmPlaylist playlist in AppleMusicService.Playlists)
		{
			BuildPlaylistNode(playlist, 0f);
		}
	}

	private static void BuildFavourites()
	{
		AmPlaylist amPlaylist = null;
		foreach (AmPlaylist playlist in AppleMusicService.Playlists)
		{
			if (playlist.Name == "Favourite Songs" || playlist.Name == "Favorite Songs" || playlist.Name == "我的最爱")
			{
				amPlaylist = playlist;
				break;
			}
		}
		if (amPlaylist == null)
		{
			CreateStatusRow("没有找到「Favourite Songs」歌单。", 0f);
			CreateActionRow("更新播放列表", 0f, delegate
			{
				AppleMusicService.SyncLibrary();
			});
		}
		else
		{
			CreateGroupHeader("我喜欢", expanded: true, delegate
			{
			});
			BuildPlaylistNode(amPlaylist, 0f);
		}
	}

	private static void BuildPlaylistNode(AmPlaylist p, float indent)
	{
		CreatePlaylistRow(p, indent, p.Expanded, delegate
		{
			AppleMusicService.ToggleExpand(p);
		});
		if (!p.Expanded)
		{
			return;
		}
		if (p.IsFolder)
		{
			float indent2 = indent + FolderIndent;
			if (p.TracksLoading && p.Children.Count == 0)
			{
				CreateStatusRow("正在展开…", indent2);
				return;
			}
			if (p.TracksError != null)
			{
				CreateStatusRow(p.TracksError, indent2, delegate
				{
					AppleMusicService.ToggleExpand(p);
				});
				return;
			}
			if (p.Children.Count == 0)
			{
				CreateStatusRow("这个文件夹是空的。", indent2);
				return;
			}
			{
				foreach (AmPlaylist child in p.Children)
				{
					BuildPlaylistNode(child, indent2);
				}
				return;
			}
		}
		float indent3 = indent + 0f;
		if (p.TracksLoading && p.Tracks.Count == 0)
		{
			CreateStatusRow("正在读取曲目…", indent3);
			return;
		}
		if (p.Tracks.Count == 0 && (p.TrackState == AmTrackState.Failed || p.TrackState == AmTrackState.Unknown))
		{
			CreateStatusRow("这个歌单上次没读到曲目。点上面的「同步」重新读取。", indent3);
			return;
		}
		if (p.TrackState == AmTrackState.Incomplete)
		{
			CreateStatusRow("这个歌单只读到 " + p.Tracks.Count + " / " + p.DeclaredCount + " 首，建议重新同步。", indent3);
		}
		if (p.Tracks.Count == 0 && p.TrackState == AmTrackState.Empty)
		{
			CreateStatusRow("这个歌单是空的。", indent3);
			return;
		}
		if (p.TracksError != null)
		{
			CreateStatusRow(p.TracksError, indent3);
			return;
		}
		if (p.Tracks.Count == 0)
		{
			CreateStatusRow("这个歌单里没有曲目。", indent3);
			return;
		}
		_virtualTracks = VirtualTrackList.Create(_listRoot.transform, TrackRowHeight, 3f, indent3);
		_virtualTracks.SetItems(new AppleTrackSource(p));
		if (!string.IsNullOrEmpty(p.Summary))
		{
			CreateStatusRow(p.Summary, indent3);
		}
		else
		{
			CreateStatusRow("共 " + p.Tracks.Count + " 首", indent3);
		}
	}

	private static void CreateGroupHeader(string title, bool expanded, Action onToggle, bool showRefresh = false, Action onRefresh = null)
	{
		PanelRows.GroupHeader(_listRoot.transform, "AmGroup_" + title, title, expanded, onToggle, showRefresh, onRefresh);
	}

	private static void CreateSectionLabel(string text, float indent)
	{
		PanelRows.SectionLabel(_listRoot.transform, "AmLabel", text, indent);
	}

	private static void CreateStatusRow(string text, float indent, Action retry = null)
	{
		PanelRows.StatusRow(_listRoot.transform, "AmStatus", text, indent, retry);
	}

	private static void CreateActionRow(string text, float indent, Action onClick)
	{
		PanelRows.ActionRow(_listRoot.transform, "AmAction", text, indent, onClick);
	}

	private static void CreatePlaylistRow(AmPlaylist p, float indent, bool expanded, Action onToggle)
	{
		string subtitle = ((!p.IsFolder) ? (p.TracksComplete ? (p.Tracks.Count + " 首") : ((!string.IsNullOrEmpty(p.Summary)) ? p.Summary : "Apple Music")) : (p.ChildrenLoaded ? ("文件夹 · " + p.Children.Count + " 项") : "文件夹"));
		PanelRows.BuildListRow(_listRoot.transform, new PanelRows.ListRow
		{
			Name = "AmPL",
			Height = PlaylistRowHeight,
			Indent = indent,
			Background = (expanded ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.045f)),
			Lead = PanelRows.Arrow(expanded),
			LeadWidth = 26f,
			LeadFontSize = UiKit.GameTitleFontSize,
			LeadColor = UiKit.GameTitleColor,
			CoverSize = 0f,
			Title = p.Name,
			TitleColor = UiKit.GameTitleColor,
			Subtitle = subtitle,
			SubtitleColor = UiKit.TextFaint,
			OnClick = onToggle
		});
	}
}
