using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal static class NeteasePanelUi
{
	private struct TrackRowVisual
	{
		public long TrackId;

		public Image Background;

		public TextMeshProUGUI Lead;

		public TextMeshProUGUI Title;

		public TextMeshProUGUI Trailing;

		public Color NormalBg;

		public string NormalLead;

		public Color NormalLeadColor;

		public Color NormalTitleColor;
	}

	private sealed class NeteaseTrackSource : IVirtualTrackSource
	{
		private readonly IList<TrackInfo> _tracks;

		private readonly QueueSource _source;

		private readonly string _sourceName;

		public int Count
		{
			get
			{
				if (_tracks == null)
				{
					return 0;
				}
				return _tracks.Count;
			}
		}

		public long CurrentId
		{
			get
			{
				AudioPlayer instance = AudioPlayer.Instance;
				if (!(instance != null) || instance.CurrentTrack == null)
				{
					return 0L;
				}
				return instance.CurrentTrack.Id;
			}
		}

		public NeteaseTrackSource(IList<TrackInfo> tracks, QueueSource source, string sourceName)
		{
			_tracks = tracks;
			_source = source;
			_sourceName = sourceName ?? "";
		}

		public long IdAt(int index)
		{
			if (_tracks == null || index < 0 || index >= _tracks.Count)
			{
				return 0L;
			}
			return _tracks[index]?.Id ?? 0;
		}

		public void Bind(PanelRows.TrackRow row, int index, bool isCurrent)
		{
			if (_tracks != null && index >= 0 && index < _tracks.Count)
			{
				PanelRows.BindTrackRow(row, _tracks[index], index, isCurrent);
			}
		}

		public void Activate(int index)
		{
			if (_tracks == null || index < 0 || index >= _tracks.Count)
			{
				return;
			}
			TrackInfo trackInfo = _tracks[index];
			if (trackInfo != null)
			{
				BridgeLog.History("点击歌曲行：" + trackInfo.Name + " · " + trackInfo.Artists + "（来源『" + _sourceName + "』第 " + (index + 1) + " 首）");
				if (AudioPlayer.Instance == null)
				{
					BridgeLog.Warn("播放器尚未初始化。");
					return;
				}
				PlaybackCoordinator.MarkUserChose();
				AudioPlayer.Instance.PlayQueue(_tracks, index, _source, _sourceName);
			}
		}
	}

	private const float Indent = 0f;

	private static GameObject _root;

	private static GameObject _subEntryRow;

	private static GameObject _listRoot;

	private static TMP_InputField _searchInput;

	private static GameObject _searchRow;

	private static NeteaseSection _section = NeteaseSection.MyPlaylists;

	private static bool _myPlaylistsExpanded;

	private static bool _subscribedExpanded;

	private static bool _likedExpanded;

	private static string _expandedRowKey;

	private static int _searchSongLimit;

	private static int _recommendSongLimit;

	private static readonly Dictionary<NeteaseSection, Button> SubEntryButtons = new Dictionary<NeteaseSection, Button>();

	private static float _searchDebounceUntil;

	private static string _pendingSearch;

	private static GameObject _statusBar;

	private static TextMeshProUGUI _statusHead;

	private static MarqueeText _statusDetail;

	internal const string Sep = " · ";

	internal const string Dash = " - ";

	private static bool _rebuildQueued;

	private static readonly List<TrackRowVisual> TrackRows = new List<TrackRowVisual>();

	private const float ListSpacing = 3f;

	private static VirtualTrackList _virtualTracks;

	private static float PlaylistRowHeight => MusicBridgeOptions.Current.UI.PlaylistRowHeightPixels;

	private static float TrackRowHeight => MusicBridgeOptions.Current.UI.TrackRowHeightPixels;

	private static int RenderPage => MusicBridgeOptions.Current.UI.RenderPageSize;

	private static int EffectiveSearchLimit
	{
		get
		{
			if (_searchSongLimit <= 0)
			{
				return RenderPage;
			}
			return _searchSongLimit;
		}
	}

	private static int EffectiveRecommendLimit
	{
		get
		{
			if (_recommendSongLimit <= 0)
			{
				return RenderPage;
			}
			return _recommendSongLimit;
		}
	}

	public static bool IsBuilt => _root != null;

	private static void ResetRenderLimits()
	{
		_searchSongLimit = 0;
		_recommendSongLimit = 0;
	}

	public static void Build(Transform topParent, Transform listParent)
	{
		_root = UiKit.CreateColumn(topParent, "NeteaseTopControls", 5f);
		BuildSubEntryRow(_root.transform);
		BuildSearchRow(_root.transform);
		BuildStatusBar(_root.transform);
		_listRoot = UiKit.CreateColumn(listParent, "ListRoot", 3f);
		PanelRows.MarkOwned(_listRoot, MusicProvider.Netease);
		ApplySection(_section, log: false);
	}

	public static void SetVisible(bool visible)
	{
		SetVisible(visible, visible);
	}

	public static void SetVisible(bool tabActive, bool _ignored)
	{
		if (_root != null && _root.activeSelf != tabActive)
		{
			_root.SetActive(tabActive);
		}
		if (_subEntryRow != null && _subEntryRow.activeSelf != tabActive)
		{
			_subEntryRow.SetActive(tabActive);
		}
		bool flag = tabActive && _section == NeteaseSection.Search;
		if (_searchRow != null && _searchRow.activeSelf != flag)
		{
			_searchRow.SetActive(flag);
		}
		if (_listRoot != null && _listRoot.activeSelf != tabActive)
		{
			_listRoot.SetActive(tabActive);
		}
	}

	private static void BuildStatusBar(Transform parent)
	{
		_statusBar = UiKit.CreateStatusRowWithMarquee(parent, "NeteaseStatusBar", 22f, UiKit.GameArtistFontSize, out _statusHead, out _statusDetail);
	}

	private static void SyncStatusBar()
	{
		if (!(_statusBar == null) && !(_statusHead == null))
		{
			string text = StatusDetail();
			string text2 = ((NeteaseService.ConnState == NeteaseConnState.Connected) ? ("已连接" + (string.IsNullOrEmpty(NeteaseService.Nickname) ? "" : (" · " + NeteaseService.Nickname))) : "未连接网易云音乐");
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

	internal static string ModeSuffix(bool shuffle, bool repeatOne)
	{
		if (shuffle && repeatOne)
		{
			return " · （随机播放 · 循环）";
		}
		if (shuffle)
		{
			return " · （随机播放）";
		}
		if (repeatOne)
		{
			return " · （循环）";
		}
		return "";
	}

	private static string StatusDetail()
	{
		AudioPlayer instance = AudioPlayer.Instance;
		if (instance != null && instance.CurrentTrack != null)
		{
			if (instance.State == PlaybackState.Loading)
			{
				return "正在缓冲 · " + instance.CurrentTrack.Name;
			}
			if (instance.State == PlaybackState.Failed)
			{
				return "播放失败 · " + instance.CurrentTrack.Name + (string.IsNullOrEmpty(instance.LastError) ? "" : (" · " + instance.LastError));
			}
			string text = ModeSuffix(instance.Shuffle, instance.RepeatOne);
			string text2 = (instance.RepeatOne ? "" : (" · " + (instance.RepeatQueue ? "队列播完后从头继续" : "队列播完后停止")));
			if (instance.State == PlaybackState.Playing)
			{
				return "正在播放 · " + instance.CurrentTrack.Name + text + text2;
			}
			if (instance.State == PlaybackState.Paused)
			{
				return "已暂停 · " + instance.CurrentTrack.Name + text + text2;
			}
		}
		foreach (PlaylistInfo myPlaylist in NeteaseLibrary.MyPlaylists)
		{
			if (myPlaylist.TracksLoading)
			{
				return "正在加载曲目 · " + myPlaylist.Name;
			}
		}
		foreach (PlaylistInfo subscribedPlaylist in NeteaseLibrary.SubscribedPlaylists)
		{
			if (subscribedPlaylist.TracksLoading)
			{
				return "正在加载曲目 · " + subscribedPlaylist.Name;
			}
		}
		if (NeteaseLibrary.LikedPlaylist.TracksLoading)
		{
			return "正在加载曲目 · 我喜欢的音乐";
		}
		switch (_section)
		{
		case NeteaseSection.Search:
			if (NeteaseLibrary.SearchState == LoadState.Loading)
			{
				return "搜索中…";
			}
			if (NeteaseLibrary.SearchLoadingMore)
			{
				return "正在加载更多搜索结果…";
			}
			if (!string.IsNullOrEmpty(NeteaseLibrary.SearchError))
			{
				return NeteaseLibrary.SearchError;
			}
			if (NeteaseLibrary.SearchState == LoadState.Ready)
			{
				return "搜索 · " + NeteaseLibrary.SearchKeyword + " · 单曲 " + NeteaseLibrary.SearchSongs.Count + " · 歌单 " + NeteaseLibrary.SearchPlaylists.Count + " · 专辑 " + NeteaseLibrary.SearchAlbums.Count;
			}
			break;
		case NeteaseSection.Recommend:
			if (NeteaseLibrary.RecommendState == LoadState.Loading)
			{
				return "正在加载每日推荐…";
			}
			if (!string.IsNullOrEmpty(NeteaseLibrary.RecommendError))
			{
				return NeteaseLibrary.RecommendError;
			}
			if (NeteaseLibrary.RecommendState == LoadState.Ready)
			{
				return "每日推荐 · 歌单 " + NeteaseLibrary.DailyPlaylists.Count + " · 单曲 " + NeteaseLibrary.DailySongs.Count;
			}
			break;
		default:
			if (NeteaseLibrary.PlaylistsState == LoadState.Loading)
			{
				return "正在加载歌单…";
			}
			if (!string.IsNullOrEmpty(NeteaseLibrary.PlaylistsError))
			{
				return NeteaseLibrary.PlaylistsError;
			}
			break;
		}
		return NeteaseService.ConnState switch
		{
			NeteaseConnState.Connected => null,
			NeteaseConnState.Restoring => "正在恢复登录…",
			NeteaseConnState.NeedsReconnect => "登录已失效，请重新扫码",
			NeteaseConnState.SessionCorrupted => "会话文件损坏，请重新扫码",
			NeteaseConnState.NetworkUnavailable => "网络不可用，登录状态已保留",
			_ => null,
		};
	}

	private static void BuildSubEntryRow(Transform parent)
	{
		_subEntryRow = UiKit.CreateRow(parent, "SubEntries", 30f, 6f, TextAnchor.MiddleCenter);
		SubEntryButtons.Clear();
		AddSubEntry("我的歌单", NeteaseSection.MyPlaylists, 82f);
		AddSubEntry("我喜欢", NeteaseSection.LikedSongs, 66f);
		AddSubEntry("收藏歌单", NeteaseSection.SubscribedPlaylists, 82f);
		AddSubEntry("搜索", NeteaseSection.Search, 54f);
		AddSubEntry("推荐", NeteaseSection.Recommend, 54f);
	}

	private static void AddSubEntry(string label, NeteaseSection section, float width)
	{
		Button button = UiKit.CreatePillButton(_subEntryRow.transform, label, filled: false, UiKit.LineColor, 26f, width);
		button.onClick.AddListener(delegate
		{
			ApplySection(section, log: true);
		});
		SubEntryButtons[section] = button;
	}

	private static void ApplySection(NeteaseSection section, bool log)
	{
		_section = section;
		foreach (KeyValuePair<NeteaseSection, Button> subEntryButton in SubEntryButtons)
		{
			Image component = subEntryButton.Value.GetComponent<Image>();
			TextMeshProUGUI componentInChildren = subEntryButton.Value.GetComponentInChildren<TextMeshProUGUI>();
			bool flag = subEntryButton.Key == section;
			if (component != null)
			{
				component.color = (flag ? UiKit.NeteaseAccent : UiKit.LineColor);
			}
			if (componentInChildren != null)
			{
				componentInChildren.color = (flag ? Color.white : UiKit.TextSecondary);
			}
		}
		if (_searchRow != null)
		{
			_searchRow.SetActive(section == NeteaseSection.Search);
		}
		if (log)
		{
			BridgeLog.Info("网易云子入口切换 -> " + section);
		}
		switch (section)
		{
		case NeteaseSection.MyPlaylists:
		case NeteaseSection.SubscribedPlaylists:
			NeteaseLibrary.LoadPlaylists(force: false);
			break;
		case NeteaseSection.LikedSongs:
			NeteaseLibrary.LoadLikedSongs(force: false);
			break;
		case NeteaseSection.Recommend:
			NeteaseLibrary.LoadRecommend(force: false);
			break;
		}
		Rebuild();
	}

	private static void BuildSearchRow(Transform parent)
	{
		_searchRow = UiKit.CreateRow(parent, "SearchRow", 30f, 6f);
		_searchInput = UiKit.CreateSearchInput(_searchRow.transform, "搜索歌曲 / 歌单 / 专辑…");
		_searchInput.onEndEdit.AddListener(delegate(string v)
		{
			SubmitSearch(v);
		});
		_searchInput.onValueChanged.AddListener(delegate(string v)
		{
			_pendingSearch = v;
			_searchDebounceUntil = Time.unscaledTime + MusicBridgeOptions.Current.UI.SearchDebounceSeconds;
		});
		UiKit.CreatePillButton(_searchRow.transform, "搜索", filled: true, UiKit.NeteaseAccent, 30f, 60f).onClick.AddListener(delegate
		{
			SubmitSearch((_searchInput != null) ? _searchInput.text : "");
		});
		_searchRow.SetActive(value: false);
	}

	private static void SubmitSearch(string keyword)
	{
		_pendingSearch = null;
		if (!string.IsNullOrEmpty(keyword) && keyword.Trim().Length != 0)
		{
			ResetRenderLimits();
			NeteaseLibrary.Search(keyword.Trim());
		}
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
		if (_pendingSearch != null && Time.unscaledTime >= _searchDebounceUntil)
		{
			string pendingSearch = _pendingSearch;
			_pendingSearch = null;
			if (!string.IsNullOrEmpty(pendingSearch) && pendingSearch.Trim().Length > 0)
			{
				NeteaseLibrary.Search(pendingSearch.Trim());
			}
		}
	}

	public static void Rebuild()
	{
		if (_listRoot == null)
		{
			return;
		}
		try
		{
			TrackRows.Clear();
			_virtualTracks = null;
			for (int num = _listRoot.transform.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.DestroyImmediate(_listRoot.transform.GetChild(num).gameObject);
			}
			switch (_section)
			{
			case NeteaseSection.MyPlaylists:
				BuildMyPlaylists();
				break;
			case NeteaseSection.LikedSongs:
				BuildLiked();
				break;
			case NeteaseSection.SubscribedPlaylists:
				BuildSubscribed();
				break;
			case NeteaseSection.Search:
				BuildSearch();
				break;
			case NeteaseSection.Recommend:
				BuildRecommend();
				break;
			}
			BridgePanel.Realign();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("重建网易云内容区失败：" + ex.Message);
		}
	}

	private static void BuildMyPlaylists()
	{
		string text = ((NeteaseLibrary.PlaylistsState == LoadState.Ready) ? (" (" + NeteaseLibrary.MyPlaylists.Count + ")") : "");
		CreateGroupHeader("我的歌单" + text, _myPlaylistsExpanded, delegate
		{
			_myPlaylistsExpanded = !_myPlaylistsExpanded;
			BridgeLog.Info("我的歌单 " + (_myPlaylistsExpanded ? "展开" : "收起"));
			if (_myPlaylistsExpanded)
			{
				NeteaseLibrary.LoadPlaylists(force: false);
			}
			Rebuild();
		}, showRefresh: true, delegate
		{
			BridgeLog.Info("手动刷新我的歌单。");
			NeteaseLibrary.LoadPlaylists(force: true);
		});
		if (!_myPlaylistsExpanded)
		{
			return;
		}
		if (NeteaseLibrary.PlaylistsState == LoadState.Loading)
		{
			CreateStatusRow("正在加载歌单…", 0f);
			return;
		}
		if (NeteaseLibrary.PlaylistsState == LoadState.Failed)
		{
			CreateStatusRow(NeteaseLibrary.PlaylistsError ?? "加载失败", 0f, delegate
			{
				NeteaseLibrary.LoadPlaylists(force: true);
			});
			return;
		}
		if (NeteaseLibrary.PlaylistsState != LoadState.Ready)
		{
			CreateStatusRow("尚未加载，点这里加载", 0f, delegate
			{
				NeteaseLibrary.LoadPlaylists(force: true);
			});
			return;
		}
		if (NeteaseLibrary.MyPlaylists.Count == 0)
		{
			CreateStatusRow("该账号还没有自建歌单", 0f);
			return;
		}
		foreach (PlaylistInfo myPlaylist in NeteaseLibrary.MyPlaylists)
		{
			BuildPlaylistNode(myPlaylist, 0f);
		}
	}

	private static void BuildSubscribed()
	{
		string text = ((NeteaseLibrary.PlaylistsState == LoadState.Ready) ? (" (" + NeteaseLibrary.SubscribedPlaylists.Count + ")") : "");
		CreateGroupHeader("收藏的歌单" + text, _subscribedExpanded, delegate
		{
			_subscribedExpanded = !_subscribedExpanded;
			if (_subscribedExpanded)
			{
				NeteaseLibrary.LoadPlaylists(force: false);
			}
			Rebuild();
		}, showRefresh: true, delegate
		{
			NeteaseLibrary.LoadPlaylists(force: true);
		});
		if (!_subscribedExpanded)
		{
			return;
		}
		if (NeteaseLibrary.PlaylistsState == LoadState.Loading)
		{
			CreateStatusRow("正在加载…", 0f);
			return;
		}
		if (NeteaseLibrary.PlaylistsState == LoadState.Failed)
		{
			CreateStatusRow(NeteaseLibrary.PlaylistsError ?? "加载失败", 0f, delegate
			{
				NeteaseLibrary.LoadPlaylists(force: true);
			});
			return;
		}
		if (NeteaseLibrary.PlaylistsState != LoadState.Ready)
		{
			CreateStatusRow("尚未加载，点这里加载", 0f, delegate
			{
				NeteaseLibrary.LoadPlaylists(force: true);
			});
			return;
		}
		if (NeteaseLibrary.SubscribedPlaylists.Count == 0)
		{
			CreateStatusRow("没有收藏的歌单", 0f);
			return;
		}
		foreach (PlaylistInfo subscribedPlaylist in NeteaseLibrary.SubscribedPlaylists)
		{
			BuildPlaylistNode(subscribedPlaylist, 0f);
		}
	}

	private static void BuildPlaylistNode(PlaylistInfo p, float indent)
	{
		string rowKey = p.RowKey;
		bool flag = _expandedRowKey == rowKey;
		CreatePlaylistRow(p, indent, flag, delegate
		{
			if (_expandedRowKey == rowKey)
			{
				_expandedRowKey = null;
				BridgeLog.History("收起歌单『" + p.Name + "』");
			}
			else
			{
				_expandedRowKey = rowKey;
				BridgeLog.History("展开歌单『" + p.Name + "』（trackCount=" + p.TrackCount + "）");
				NeteaseLibrary.LoadPlaylistTracks(p, force: false);
			}
			Rebuild();
		});
		if (flag)
		{
			BuildTrackList(p, 0f, QueueSource.Playlist);
		}
	}

	private static void ReloadTracks(PlaylistInfo p, QueueSource source)
	{
		if (source == QueueSource.LikedSongs || p == NeteaseLibrary.LikedPlaylist)
		{
			NeteaseLibrary.LoadLikedSongs(force: true);
		}
		else
		{
			NeteaseLibrary.LoadPlaylistTracks(p, force: true);
		}
	}

	private static void BuildTrackList(PlaylistInfo p, float indent, QueueSource source)
	{
		if (p.TracksLoading && p.Tracks.Count == 0)
		{
			CreateStatusRow("正在加载曲目…", indent);
			return;
		}
		if (!string.IsNullOrEmpty(p.TracksError))
		{
			CreateStatusRow(p.TracksError, indent, delegate
			{
				ReloadTracks(p, source);
			});
			if (p.Tracks.Count == 0)
			{
				return;
			}
		}
		if (p.Tracks.Count > 0)
		{
			_virtualTracks = VirtualTrackList.Create(_listRoot.transform, TrackRowHeight, 3f, indent);
			_virtualTracks.SetItems(new NeteaseTrackSource(p.Tracks, source, p.Name));
		}
		if (p.TracksLoading)
		{
			CreateStatusRow("正在加载 " + p.Tracks.Count + " / " + p.TrackCount, indent);
		}
		else if (!p.TracksComplete && p.Tracks.Count == 0 && string.IsNullOrEmpty(p.TracksError))
		{
			CreateStatusRow("尚未加载曲目，点这里加载", indent, delegate
			{
				ReloadTracks(p, source);
			});
		}
		else
		{
			CreateStatusRow(PlaylistAssembly.Summary(p.TrackCount, p.Tracks.Count, p.MissingCount, p.LoadAborted), indent, p.LoadAborted ? ((Action)delegate
			{
				ReloadTracks(p, source);
			}) : null);
		}
	}

	private static void BuildLiked()
	{
		PlaylistInfo likedPlaylist = NeteaseLibrary.LikedPlaylist;
		string text = ((likedPlaylist.TrackCount > 0) ? (" (" + likedPlaylist.TrackCount + ")") : "");
		CreateGroupHeader("我喜欢的音乐" + text, _likedExpanded, delegate
		{
			_likedExpanded = !_likedExpanded;
			if (_likedExpanded)
			{
				NeteaseLibrary.LoadLikedSongs(force: false);
			}
			Rebuild();
		}, showRefresh: true, delegate
		{
			NeteaseLibrary.LoadLikedSongs(force: true);
		});
		if (_likedExpanded)
		{
			BuildTrackList(likedPlaylist, 0f, QueueSource.LikedSongs);
		}
	}

	private static void BuildSearch()
	{
		switch (NeteaseLibrary.SearchState)
		{
		case LoadState.Idle:
			CreateStatusRow("输入关键词后搜索", 0f);
			return;
		case LoadState.Loading:
			CreateStatusRow("正在搜索…", 0f);
			return;
		case LoadState.Failed:
			CreateStatusRow(NeteaseLibrary.SearchError ?? "搜索失败", 0f, delegate
			{
				NeteaseLibrary.Search(NeteaseLibrary.SearchKeyword);
			});
			return;
		}
		bool flag = !string.IsNullOrEmpty(NeteaseLibrary.SearchSongsError) || !string.IsNullOrEmpty(NeteaseLibrary.SearchPlaylistsError) || !string.IsNullOrEmpty(NeteaseLibrary.SearchAlbumsError);
		if (NeteaseLibrary.SearchSongs.Count == 0 && NeteaseLibrary.SearchPlaylists.Count == 0 && NeteaseLibrary.SearchAlbums.Count == 0 && !flag)
		{
			CreateStatusRow("没有找到结果", 0f);
			return;
		}
		if (!string.IsNullOrEmpty(NeteaseLibrary.SearchSongsError))
		{
			CreateStatusRow(NeteaseLibrary.SearchSongsError, 0f, delegate
			{
				NeteaseLibrary.Search(NeteaseLibrary.SearchKeyword);
			});
		}
		if (NeteaseLibrary.SearchSongs.Count > 0)
		{
			CreateSectionLabel("单曲 (" + NeteaseLibrary.SearchSongs.Count + ")", 0f);
			int num = Mathf.Min(EffectiveSearchLimit, NeteaseLibrary.SearchSongs.Count);
			for (int num2 = 0; num2 < num; num2++)
			{
				CreateTrackRow(num2 + 1, NeteaseLibrary.SearchSongs[num2], 0f, NeteaseLibrary.SearchSongs, num2, QueueSource.SearchResults, "搜索结果");
			}
			if (num < NeteaseLibrary.SearchSongs.Count)
			{
				CreateActionRow("展开更多（已显示 " + num + " / " + NeteaseLibrary.SearchSongs.Count + "）", 0f, delegate
				{
					_searchSongLimit = EffectiveSearchLimit + RenderPage;
					RequestRebuild();
				});
			}
		}
		if (NeteaseLibrary.SearchLoadingMore)
		{
			CreateStatusRow("正在加载更多…", 0f);
		}
		else if (NeteaseLibrary.SearchHasMore)
		{
			CreateActionRow("加载更多（已加载 " + NeteaseLibrary.SearchSongs.Count + " 条）", 0f, NeteaseLibrary.SearchLoadMore);
		}
		if (NeteaseLibrary.SearchPlaylists.Count > 0)
		{
			CreateSectionLabel("歌单 (" + NeteaseLibrary.SearchPlaylists.Count + ")", 0f);
			foreach (PlaylistInfo searchPlaylist in NeteaseLibrary.SearchPlaylists)
			{
				BuildPlaylistNode(searchPlaylist, 0f);
			}
		}
		if (!string.IsNullOrEmpty(NeteaseLibrary.SearchPlaylistsError))
		{
			CreateStatusRow(NeteaseLibrary.SearchPlaylistsError, 0f, delegate
			{
				NeteaseLibrary.Search(NeteaseLibrary.SearchKeyword);
			});
		}
		if (NeteaseLibrary.SearchAlbums.Count > 0)
		{
			CreateSectionLabel("专辑 (" + NeteaseLibrary.SearchAlbums.Count + ")", 0f);
			foreach (PlaylistInfo searchAlbum in NeteaseLibrary.SearchAlbums)
			{
				BuildPlaylistNode(searchAlbum, 0f);
			}
		}
		if (!string.IsNullOrEmpty(NeteaseLibrary.SearchAlbumsError))
		{
			CreateStatusRow(NeteaseLibrary.SearchAlbumsError, 0f, delegate
			{
				NeteaseLibrary.Search(NeteaseLibrary.SearchKeyword);
			});
		}
	}

	private static void BuildRecommend()
	{
		if (NeteaseLibrary.RecommendState == LoadState.Loading)
		{
			CreateStatusRow("正在加载推荐…", 0f);
			return;
		}
		if (NeteaseLibrary.RecommendState == LoadState.Failed)
		{
			CreateStatusRow(NeteaseLibrary.RecommendError ?? "推荐不可用", 0f, delegate
			{
				NeteaseLibrary.LoadRecommend(force: true);
			});
			return;
		}
		if (!string.IsNullOrEmpty(NeteaseLibrary.RecommendError))
		{
			CreateStatusRow(NeteaseLibrary.RecommendError, 0f);
		}
		if (NeteaseLibrary.DailySongs.Count > 0)
		{
			CreateSectionLabel("每日推荐歌曲 (" + NeteaseLibrary.DailySongs.Count + ")", 0f);
			int num = Mathf.Min(EffectiveRecommendLimit, NeteaseLibrary.DailySongs.Count);
			for (int num2 = 0; num2 < num; num2++)
			{
				CreateTrackRow(num2 + 1, NeteaseLibrary.DailySongs[num2], 0f, NeteaseLibrary.DailySongs, num2, QueueSource.Recommendations, "每日推荐");
			}
			if (num < NeteaseLibrary.DailySongs.Count)
			{
				CreateActionRow("展开更多（已显示 " + num + " / " + NeteaseLibrary.DailySongs.Count + "）", 0f, delegate
				{
					_recommendSongLimit = EffectiveRecommendLimit + RenderPage;
					RequestRebuild();
				});
			}
		}
		if (NeteaseLibrary.DailyPlaylists.Count > 0)
		{
			CreateSectionLabel("每日推荐歌单 (" + NeteaseLibrary.DailyPlaylists.Count + ")", 0f);
			foreach (PlaylistInfo dailyPlaylist in NeteaseLibrary.DailyPlaylists)
			{
				BuildPlaylistNode(dailyPlaylist, 0f);
			}
		}
		if (NeteaseLibrary.DailySongs.Count == 0 && NeteaseLibrary.DailyPlaylists.Count == 0 && NeteaseLibrary.RecommendState == LoadState.Ready)
		{
			CreateStatusRow("推荐内容为空", 0f);
		}
	}

	private static void CreateGroupHeader(string title, bool expanded, Action onToggle, bool showRefresh = false, Action onRefresh = null)
	{
		PanelRows.GroupHeader(_listRoot.transform, "Group_" + title, title, expanded, onToggle, showRefresh, onRefresh);
	}

	private static void CreateSectionLabel(string text, float indent)
	{
		PanelRows.SectionLabel(_listRoot.transform, "Label", text, indent);
	}

	private static void CreateStatusRow(string text, float indent, Action retry = null)
	{
		PanelRows.StatusRow(_listRoot.transform, "Status", text, indent, retry);
	}

	private static void CreateActionRow(string text, float indent, Action onClick)
	{
		PanelRows.ActionRow(_listRoot.transform, "Action", text, indent, onClick);
	}

	private static void CreatePlaylistRow(PlaylistInfo p, float indent, bool expanded, Action onToggle)
	{
		string text = p.TrackCount + " 首 · " + (string.IsNullOrEmpty(p.CreatorName) ? "—" : p.CreatorName);
		if (!string.IsNullOrEmpty(p.AlbumType))
		{
			text = p.AlbumType + " · " + text;
		}
		if (p.TracksLoading)
		{
			text += " · 加载中";
		}
		else if (p.MissingCount > 0)
		{
			text = text + " · " + p.MissingCount + " 首已失效";
		}
		PanelRows.BuildListRow(_listRoot.transform, new PanelRows.ListRow
		{
			Name = "PL_" + p.Id,
			Height = PlaylistRowHeight,
			Indent = indent,
			Background = (expanded ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.045f)),
			Lead = PanelRows.Arrow(expanded),
			LeadWidth = 26f,
			LeadFontSize = UiKit.GameTitleFontSize,
			LeadColor = UiKit.GameTitleColor,
			CoverSize = 44f,
			CoverUrl = p.CoverUrl,
			CoverRequestSize = 60,
			Title = p.Name,
			TitleColor = UiKit.GameTitleColor,
			Subtitle = text,
			SubtitleColor = UiKit.GameArtistColor,
			OnClick = onToggle
		});
	}

	private static void CreateTrackRow(int order, TrackInfo t, float indent, IList<TrackInfo> queue, int index, QueueSource source, string sourceName)
	{
		bool flag = AudioPlayer.Instance != null && AudioPlayer.Instance.CurrentTrack != null && AudioPlayer.Instance.CurrentTrack.Id == t.Id;
		string text = t.Artists;
		if (!string.IsNullOrEmpty(t.Album))
		{
			text = text + " · " + t.Album;
		}
		if (!t.Playable)
		{
			text = (t.UnplayableReason ?? "不可播放") + " · " + text;
		}
		IList<TrackInfo> queueSnapshot = queue;
		Color normalBg = new Color(1f, 1f, 1f, 0.03f);
		string normalLead = order.ToString("00");
		Color normalTitleColor = ((!t.Playable) ? UiKit.TextFaint : UiKit.GameTitleColor);
		PanelRows.BuildListRow(_listRoot.transform, new PanelRows.ListRow
		{
			Name = "TR_" + t.Id,
			Height = TrackRowHeight,
			Indent = indent,
			Background = (flag ? UiKit.NowPlayingTint : new Color(1f, 1f, 1f, 0.03f)),
			Lead = (flag ? UiKit.Glyph("▶", ">") : order.ToString("00")),
			LeadWidth = 32f,
			LeadFontSize = UiKit.GameArtistFontSize,
			LeadColor = (flag ? UiKit.NowPlayingText : UiKit.TextFaint),
			CoverSize = 32f,
			CoverUrl = t.CoverUrl,
			CoverRequestSize = 40,
			Title = t.Name,
			TitleBold = flag,
			TitleColor = (flag ? UiKit.NowPlayingText : ((!t.Playable) ? UiKit.TextFaint : UiKit.GameTitleColor)),
			Subtitle = text,
			SubtitleColor = (t.Playable ? UiKit.TextFaint : new Color(0.95f, 0.45f, 0.35f, 0.9f)),
			Trailing = t.DurationText,
			OnClick = delegate
			{
				BridgeLog.History("点击歌曲行：" + t.Name + " · " + t.Artists + "（来源『" + sourceName + "』第 " + (index + 1) + " 首）");
				if (AudioPlayer.Instance == null)
				{
					BridgeLog.Warn("播放器尚未初始化。");
				}
				else
				{
					PlaybackCoordinator.MarkUserChose();
					AudioPlayer.Instance.PlayQueue(queueSnapshot, index, source, sourceName);
				}
			}
		}, out var parts);
		TrackRows.Add(new TrackRowVisual
		{
			TrackId = t.Id,
			Background = parts.Background,
			Lead = parts.Lead,
			Title = parts.Title,
			Trailing = parts.Trailing,
			NormalBg = normalBg,
			NormalLead = normalLead,
			NormalLeadColor = UiKit.TextFaint,
			NormalTitleColor = normalTitleColor
		});
	}

	internal static void RefreshNowPlaying()
	{
		if (_virtualTracks != null)
		{
			_virtualTracks.RefreshNowPlaying();
		}
		if (TrackRows.Count == 0)
		{
			return;
		}
		AudioPlayer instance = AudioPlayer.Instance;
		long num = ((instance != null && instance.CurrentTrack != null) ? instance.CurrentTrack.Id : 0);
		for (int i = 0; i < TrackRows.Count; i++)
		{
			TrackRowVisual trackRowVisual = TrackRows[i];
			if (!(trackRowVisual.Background == null))
			{
				bool flag = trackRowVisual.TrackId != 0L && trackRowVisual.TrackId == num;
				trackRowVisual.Background.color = (flag ? UiKit.NowPlayingTint : trackRowVisual.NormalBg);
				if (trackRowVisual.Lead != null)
				{
					trackRowVisual.Lead.text = (flag ? UiKit.Glyph("▶", ">") : trackRowVisual.NormalLead);
					trackRowVisual.Lead.color = (flag ? UiKit.NowPlayingText : trackRowVisual.NormalLeadColor);
				}
				if (trackRowVisual.Title != null)
				{
					trackRowVisual.Title.color = (flag ? UiKit.NowPlayingText : trackRowVisual.NormalTitleColor);
					trackRowVisual.Title.fontStyle = (flag ? FontStyles.Bold : FontStyles.Normal);
				}
			}
		}
	}

	internal static void RefreshTrackDuration(long trackId, string durationText)
	{
		for (int i = 0; i < TrackRows.Count; i++)
		{
			TrackRowVisual trackRowVisual = TrackRows[i];
			if (trackRowVisual.TrackId == trackId && !(trackRowVisual.Trailing == null) && trackRowVisual.Trailing.text != durationText)
			{
				trackRowVisual.Trailing.text = durationText;
			}
		}
	}

	public static void ResetState()
	{
		_myPlaylistsExpanded = false;
		_subscribedExpanded = false;
		_likedExpanded = false;
		_expandedRowKey = null;
		ResetRenderLimits();
		_section = NeteaseSection.MyPlaylists;
		if (_searchInput != null)
		{
			_searchInput.text = "";
		}
		Rebuild();
	}
}
