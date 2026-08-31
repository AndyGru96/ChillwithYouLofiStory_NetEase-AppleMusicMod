using System;
using System.Collections.Generic;
using System.Threading;

namespace MusicBridge;

internal static class NeteaseLibrary
{
	public static long UserId;

	public static string Nickname = "";

	public static volatile List<PlaylistInfo> MyPlaylists = new List<PlaylistInfo>();

	public static volatile List<PlaylistInfo> SubscribedPlaylists = new List<PlaylistInfo>();

	public static LoadState PlaylistsState = LoadState.Idle;

	public static string PlaylistsError;

	public static readonly PlaylistInfo LikedPlaylist = new PlaylistInfo
	{
		Id = -1L,
		Name = "我喜欢的音乐"
	};

	public static volatile List<TrackInfo> SearchSongs = new List<TrackInfo>();

	public static volatile List<PlaylistInfo> SearchPlaylists = new List<PlaylistInfo>();

	public static volatile List<PlaylistInfo> SearchAlbums = new List<PlaylistInfo>();

	public static LoadState SearchState = LoadState.Idle;

	public static string SearchError;

	public static string SearchSongsError;

	public static string SearchPlaylistsError;

	public static string SearchAlbumsError;

	public static string SearchKeyword = "";

	public static volatile List<PlaylistInfo> DailyPlaylists = new List<PlaylistInfo>();

	public static volatile List<TrackInfo> DailySongs = new List<TrackInfo>();

	public static LoadState RecommendState = LoadState.Idle;

	public static string RecommendError;

	private static int _playlistsGen;

	private static int _likedGen;

	private static int _searchGen;

	private static int _recommendGen;

	private static readonly Dictionary<string, int> TrackGen = new Dictionary<string, int>();

	private static readonly object Gate = new object();

	private static int _loadToken;

	public static bool SearchHasMore;

	public static bool SearchLoadingMore;

	private static int _searchSongOffset;

	private static int SongDetailBatch => MusicBridgeOptions.Current.Netease.SongDetailBatchSize;

	private static int SearchPageSize => MusicBridgeOptions.Current.Netease.SearchPageSize;

	public static event Action Changed;

	private static void CommitIf(Func<bool> current, Action apply)
	{
		Plugin.RunOnMainThread(delegate
		{
			if (current != null && !current())
			{
				return;
			}
			apply();
			try
			{
				if (NeteaseLibrary.Changed != null)
				{
					NeteaseLibrary.Changed();
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Error("仓库回调异常：" + ex.Message);
			}
		});
	}

	private static void Notify()
	{
		Plugin.RunOnMainThread(delegate
		{
			try
			{
				if (NeteaseLibrary.Changed != null)
				{
					NeteaseLibrary.Changed();
				}
			}
			catch (Exception ex)
			{
				BridgeLog.Error("仓库回调异常：" + ex.Message);
			}
		});
	}

	private static void Background(string name, Action work, Action onError = null)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				work();
			}
			catch (Exception ex)
			{
				BridgeLog.Error(name + " 线程异常：" + ex.GetType().Name + " " + ex.Message);
				if (onError == null)
				{
					return;
				}
				try
				{
					onError();
				}
				catch (Exception ex2)
				{
					BridgeLog.Error(name + " 异常善后失败：" + ex2.Message);
				}
			}
		});
		thread.IsBackground = true;
		thread.Name = "MusicBridge-" + name;
		thread.Start();
	}

	public static void ClearAll()
	{
		lock (Gate)
		{
			_playlistsGen++;
			_likedGen++;
			_searchGen++;
			_recommendGen++;
			TrackGen.Clear();
		}
		MyPlaylists = new List<PlaylistInfo>();
		SubscribedPlaylists = new List<PlaylistInfo>();
		LikedPlaylist.ResetTracks();
		SearchSongs = new List<TrackInfo>();
		SearchPlaylists = new List<PlaylistInfo>();
		SearchAlbums = new List<PlaylistInfo>();
		DailyPlaylists = new List<PlaylistInfo>();
		DailySongs = new List<TrackInfo>();
		PlaylistsState = LoadState.Idle;
		PlaylistsError = null;
		SearchState = LoadState.Idle;
		SearchError = null;
		SearchKeyword = "";
		SearchSongsError = (SearchPlaylistsError = (SearchAlbumsError = null));
		RecommendState = LoadState.Idle;
		RecommendError = null;
		UserId = 0L;
		Nickname = "";
		CoverCache.Clear();
		BridgeLog.Info("已清空内存中的全部账号歌单与歌曲数据。");
		Notify();
	}

	public static void LoadPlaylists(bool force)
	{
		if (UserId == 0L)
		{
			BridgeLog.Warn("尚未取得 userId，无法加载歌单。");
		}
		else
		{
			if ((!force && PlaylistsState == LoadState.Ready) || (!force && PlaylistsState == LoadState.Loading))
			{
				return;
			}
			int gen;
			lock (Gate)
			{
				gen = ++_playlistsGen;
			}
			PlaylistsState = LoadState.Loading;
			PlaylistsError = null;
			Notify();
			Background("LoadPlaylists", delegate
			{
				bool netErr;
				List<PlaylistInfo> userPlaylists = NeteaseApi.GetUserPlaylists(UserId, out netErr);
				lock (Gate)
				{
					if (gen != _playlistsGen)
					{
						BridgeLog.Info("歌单加载结果迟到，已丢弃。");
						return;
					}
				}
				Func<bool> current = delegate
				{
					lock (Gate)
					{
						return gen == _playlistsGen;
					}
				};
				if (userPlaylists == null)
				{
					CommitIf(current, delegate
					{
						PlaylistsState = LoadState.Failed;
						PlaylistsError = (netErr ? "网络错误，请重试" : "加载歌单失败");
					});
				}
				else
				{
					List<PlaylistInfo> mine = new List<PlaylistInfo>();
					List<PlaylistInfo> subscribed = new List<PlaylistInfo>();
					foreach (PlaylistInfo item in userPlaylists)
					{
						if (item.IsMine)
						{
							mine.Add(item);
						}
						else
						{
							subscribed.Add(item);
						}
					}
					CommitIf(current, delegate
					{
						MyPlaylists = mine;
						SubscribedPlaylists = subscribed;
						PlaylistsState = LoadState.Ready;
						PlaylistsError = null;
						BridgeLog.Info("歌单加载完成：自建 " + mine.Count + " 个，收藏订阅 " + subscribed.Count + " 个。");
					});
				}
			}, delegate
			{
				CommitIf(delegate
				{
					lock (Gate)
					{
						return gen == _playlistsGen;
					}
				}, delegate
				{
					PlaylistsState = LoadState.Failed;
					PlaylistsError = "加载歌单时出现异常，请重试";
				});
			});
		}
	}

	public static void LoadLikedSongs(bool force)
	{
		if (UserId == 0L || (!force && LikedPlaylist.TracksComplete) || (!force && LikedPlaylist.TracksLoading))
		{
			return;
		}
		int gen;
		lock (Gate)
		{
			gen = ++_likedGen;
		}
		int token = BeginLoad(LikedPlaylist);
		LikedPlaylist.TracksError = null;
		Notify();
		Func<bool> stillCurrent = delegate
		{
			lock (Gate)
			{
				return gen == _likedGen;
			}
		};
		Background("LoadLiked", delegate
		{
			bool netErr;
			List<long> likedSongIds = NeteaseApi.GetLikedSongIds(UserId, out netErr);
			if (!stillCurrent())
			{
				AbandonLoad(LikedPlaylist, token);
			}
			else if (likedSongIds == null)
			{
				CommitIf(stillCurrent, delegate
				{
					LikedPlaylist.TracksLoading = false;
					LikedPlaylist.TracksError = (netErr ? "网络错误，请重试" : "加载失败");
				});
			}
			else
			{
				FetchTracksInBatches(LikedPlaylist, likedSongIds, stillCurrent, token);
			}
		}, delegate
		{
			FailLoad(LikedPlaylist, token, stillCurrent);
		});
	}

	private static void FailLoad(PlaylistInfo playlist, int token, Func<bool> stillCurrent)
	{
		CommitIf(null, delegate
		{
			if (playlist.LoadToken == token)
			{
				playlist.TracksLoading = false;
				if (stillCurrent == null || stillCurrent())
				{
					playlist.TracksError = "加载曲目时出现异常，请重试";
				}
			}
		});
	}

	public static void LoadPlaylistTracks(PlaylistInfo playlist, bool force)
	{
		if (playlist == null || (!force && playlist.TracksComplete) || (!force && playlist.TracksLoading))
		{
			return;
		}
		string key = playlist.RowKey;
		int gen;
		lock (Gate)
		{
			TrackGen.TryGetValue(key, out var value);
			gen = value + 1;
			TrackGen[key] = gen;
		}
		int token = BeginLoad(playlist);
		playlist.TracksError = null;
		Notify();
		Func<bool> stillCurrent = delegate
		{
			lock (Gate)
			{
				int value2;
				return TrackGen.TryGetValue(key, out value2) && value2 == gen;
			}
		};
		Background("LoadTracks", delegate
		{
			bool netErr;
			List<long> list = (playlist.IsAlbum ? NeteaseApi.GetAlbumTrackIds(playlist.Id, out netErr) : NeteaseApi.GetPlaylistTrackIds(playlist.Id, out netErr));
			if (!stillCurrent())
			{
				BridgeLog.Info("歌单曲目结果迟到，已丢弃。");
				AbandonLoad(playlist, token);
			}
			else if (list == null)
			{
				CommitIf(stillCurrent, delegate
				{
					playlist.TracksLoading = false;
					playlist.TracksError = (netErr ? "网络错误，请重试" : "加载曲目失败");
				});
			}
			else
			{
				FetchTracksInBatches(playlist, list, stillCurrent, token);
			}
		}, delegate
		{
			FailLoad(playlist, token, stillCurrent);
		});
	}

	private static void AbandonLoad(PlaylistInfo playlist, int token)
	{
		CommitIf(null, delegate
		{
			if (playlist.LoadToken == token)
			{
				playlist.TracksLoading = false;
			}
		});
	}

	private static int BeginLoad(PlaylistInfo playlist)
	{
		int num;
		lock (Gate)
		{
			num = ++_loadToken;
		}
		playlist.LoadToken = num;
		playlist.TracksLoading = true;
		playlist.MissingCount = 0;
		playlist.LoadAborted = false;
		return num;
	}

	private static void FetchTracksInBatches(PlaylistInfo playlist, List<long> ids, Func<bool> stillCurrent, int token)
	{
		List<TrackInfo> completed = new List<TrackInfo>(ids.Count);
		List<long> list = new List<long>();
		bool flag = false;
		bool flag2 = false;
		foreach (PlaylistAssembly.BatchRange item in PlaylistAssembly.Split(ids.Count, SongDetailBatch))
		{
			if (!stillCurrent())
			{
				AbandonLoad(playlist, token);
				return;
			}
			List<long> range = ids.GetRange(item.Start, item.Count);
			bool networkError;
			List<TrackInfo> songDetails = NeteaseApi.GetSongDetails(range, out networkError);
			if (!stillCurrent())
			{
				AbandonLoad(playlist, token);
				return;
			}
			if (songDetails == null)
			{
				flag = true;
				flag2 = networkError;
				BridgeLog.Warn("歌单 " + playlist.Id + " 第 " + item.Start + " 批取详情失败（" + (networkError ? "网络错误" : "接口失败") + "），已取到 " + completed.Count + " / " + ids.Count + " 首，提交这部分。");
				break;
			}
			completed.AddRange(songDetails);
			if (songDetails.Count != range.Count)
			{
				List<long> list2 = new List<long>(songDetails.Count);
				foreach (TrackInfo item2 in songDetails)
				{
					if (item2 != null)
					{
						list2.Add(item2.Id);
					}
				}
				List<long> list3 = PlaylistAssembly.MissingIds(range, list2);
				list.AddRange(list3);
				BridgeLog.Info("歌单 " + playlist.Id + " 有 " + list3.Count + " 首查不到详情（已失效，跳过）：" + string.Join(",", list3.ConvertAll((long x) => x.ToString()).ToArray()));
			}
			BridgeLog.History("歌单『" + playlist.Name + "』后台已取得 " + completed.Count + " / " + ids.Count);
		}
		if (!stillCurrent())
		{
			AbandonLoad(playlist, token);
			return;
		}
		List<long> loadedIds = new List<long>(completed.Count);
		foreach (TrackInfo item3 in completed)
		{
			loadedIds.Add(item3.Id);
		}
		int missingCount = list.Count;
		bool wasAborted = flag;
		bool netAbort = flag2;
		CommitIf(stillCurrent, delegate
		{
			playlist.Tracks = completed;
			playlist.TrackIds = loadedIds;
			playlist.TrackCount = ids.Count;
			playlist.MissingCount = missingCount;
			playlist.LoadAborted = wasAborted;
			playlist.TracksLoading = false;
			playlist.TracksComplete = !wasAborted;
			playlist.TracksError = ((!wasAborted) ? null : ((netAbort ? "网络错误" : "接口失败") + ((completed.Count > 0) ? "，只加载了一部分" : "，未能加载曲目")));
			BridgeLog.Info("歌单 " + playlist.Id + " 装配完成：声明 " + ids.Count + " 首，实得 " + completed.Count + " 首，失效 " + missingCount + " 首，中断=" + wasAborted);
		});
	}

	public static void SearchLoadMore()
	{
		if (string.IsNullOrEmpty(SearchKeyword) || SearchLoadingMore || !SearchHasMore)
		{
			return;
		}
		int gen;
		lock (Gate)
		{
			gen = _searchGen;
		}
		SearchLoadingMore = true;
		int offset = _searchSongOffset;
		string keyword = SearchKeyword;
		Notify();
		Background("SearchMore", delegate
		{
			List<TrackInfo> songs;
			List<PlaylistInfo> playlists;
			NeteaseSearchStatus searchStatus;
			bool flag = NeteaseApi.Search(keyword, NeteaseSearchType.Song, SearchPageSize, offset, out songs, out playlists, out searchStatus);
			lock (Gate)
			{
				if (gen != _searchGen)
				{
					BridgeLog.Info("搜索翻页结果迟到，已丢弃。");
					return;
				}
			}
			Func<bool> current = delegate
			{
				lock (Gate)
				{
					return gen == _searchGen;
				}
			};
			if (!flag || songs == null)
			{
				CommitIf(current, delegate
				{
					SearchLoadingMore = false;
					SearchSongsError = ((searchStatus == NeteaseSearchStatus.NetworkError) ? "单曲：网络错误，请重试" : "单曲：加载更多失败");
					SearchError = SearchSongsError;
				});
			}
			else
			{
				HashSet<long> hashSet = new HashSet<long>();
				foreach (TrackInfo searchSong in SearchSongs)
				{
					hashSet.Add(searchSong.Id);
				}
				List<TrackInfo> merged = new List<TrackInfo>(SearchSongs);
				int added = 0;
				foreach (TrackInfo item in songs)
				{
					if (hashSet.Add(item.Id))
					{
						merged.Add(item);
						added++;
					}
				}
				CommitIf(current, delegate
				{
					SearchSongs = merged;
					_searchSongOffset = offset + songs.Count;
					SearchHasMore = songs.Count >= SearchPageSize;
					SearchLoadingMore = false;
					SearchSongsError = null;
					SearchError = null;
					BridgeLog.Info("搜索加载更多：本页 " + songs.Count + " 条，新增 " + added + "，累计 " + merged.Count + "，还有更多=" + SearchHasMore);
				});
			}
		}, delegate
		{
			CommitIf(delegate
			{
				lock (Gate)
				{
					return gen == _searchGen;
				}
			}, delegate
			{
				SearchLoadingMore = false;
				SearchSongsError = "单曲：加载更多时出现异常，请重试";
			});
		});
	}

	public static void Search(string keyword)
	{
		if (string.IsNullOrEmpty(keyword) || keyword.Trim().Length == 0)
		{
			lock (Gate)
			{
				_searchGen++;
			}
			SearchState = LoadState.Idle;
			SearchKeyword = "";
			SearchSongs = new List<TrackInfo>();
			SearchPlaylists = new List<PlaylistInfo>();
			SearchAlbums = new List<PlaylistInfo>();
			SearchHasMore = false;
			SearchLoadingMore = false;
			_searchSongOffset = 0;
			SearchError = (SearchSongsError = (SearchPlaylistsError = (SearchAlbumsError = null)));
			Notify();
			return;
		}
		int gen;
		lock (Gate)
		{
			gen = ++_searchGen;
		}
		SearchKeyword = keyword;
		SearchState = LoadState.Loading;
		SearchError = (SearchSongsError = (SearchPlaylistsError = (SearchAlbumsError = null)));
		Notify();
		Background("Search", delegate
		{
			List<TrackInfo> songs;
			List<PlaylistInfo> playlists;
			NeteaseSearchStatus songStatus;
			bool songsOk = NeteaseApi.Search(keyword, NeteaseSearchType.Song, SearchPageSize, 0, out songs, out playlists, out songStatus);
			lock (Gate)
			{
				if (gen != _searchGen)
				{
					BridgeLog.Info("搜索结果迟到，已丢弃。");
					return;
				}
			}
			List<TrackInfo> songs2;
			List<PlaylistInfo> foundPlaylists;
			NeteaseSearchStatus playlistStatus;
			bool playlistsOk = NeteaseApi.Search(keyword, NeteaseSearchType.Playlist, 10, 0, out songs2, out foundPlaylists, out playlistStatus);
			lock (Gate)
			{
				if (gen != _searchGen)
				{
					return;
				}
			}
			List<PlaylistInfo> foundAlbums;
			NeteaseSearchStatus albumStatus;
			bool albumsOk = NeteaseApi.Search(keyword, NeteaseSearchType.Album, 10, 0, out songs2, out foundAlbums, out albumStatus);
			CommitIf(delegate
			{
				lock (Gate)
				{
					return gen == _searchGen;
				}
			}, delegate
			{
				SearchSongs = ((songsOk && songs != null) ? new List<TrackInfo>(songs) : new List<TrackInfo>());
				SearchPlaylists = ((playlistsOk && foundPlaylists != null) ? new List<PlaylistInfo>(foundPlaylists) : new List<PlaylistInfo>());
				SearchAlbums = ((albumsOk && foundAlbums != null) ? new List<PlaylistInfo>(foundAlbums) : new List<PlaylistInfo>());
				_searchSongOffset = ((songsOk && songs != null) ? songs.Count : 0);
				SearchHasMore = songsOk && songs != null && songs.Count >= SearchPageSize;
				SearchLoadingMore = false;
				SearchSongsError = (songsOk ? null : SearchFailure("单曲", songStatus));
				SearchPlaylistsError = (playlistsOk ? null : SearchFailure("歌单", playlistStatus));
				SearchAlbumsError = (albumsOk ? null : SearchFailure("专辑", albumStatus));
				SearchState = ((songsOk || playlistsOk || albumsOk) ? LoadState.Ready : LoadState.Failed);
				SearchError = ((SearchState == LoadState.Failed) ? (SearchSongsError + "；" + SearchPlaylistsError + "；" + SearchAlbumsError) : null);
			});
		}, delegate
		{
			CommitIf(delegate
			{
				lock (Gate)
				{
					return gen == _searchGen;
				}
			}, delegate
			{
				SearchState = LoadState.Failed;
				SearchError = "搜索时出现异常，请重试";
			});
		});
	}

	private static string SearchFailure(string category, NeteaseSearchStatus status)
	{
		return category + "：" + ((status == NeteaseSearchStatus.NetworkError) ? "网络错误" : "搜索失败");
	}

	public static void LoadRecommend(bool force)
	{
		if ((!force && RecommendState == LoadState.Ready) || (!force && RecommendState == LoadState.Loading))
		{
			return;
		}
		int gen;
		lock (Gate)
		{
			gen = ++_recommendGen;
		}
		RecommendState = LoadState.Loading;
		RecommendError = null;
		Notify();
		Background("Recommend", delegate
		{
			bool networkError;
			string err1;
			List<PlaylistInfo> pls = NeteaseApi.GetDailyPlaylists(out networkError, out err1);
			string err2;
			List<TrackInfo> songs = NeteaseApi.GetDailySongs(out networkError, out err2);
			lock (Gate)
			{
				if (gen != _recommendGen)
				{
					return;
				}
			}
			CommitIf(delegate
			{
				lock (Gate)
				{
					return gen == _recommendGen;
				}
			}, delegate
			{
				DailyPlaylists = ((pls != null) ? new List<PlaylistInfo>(pls) : new List<PlaylistInfo>());
				DailySongs = ((songs != null) ? new List<TrackInfo>(songs) : new List<TrackInfo>());
				if (pls == null && songs == null)
				{
					RecommendState = LoadState.Failed;
					RecommendError = "推荐接口不可用：" + (err1 ?? err2 ?? "未知原因");
				}
				else
				{
					RecommendState = LoadState.Ready;
					RecommendError = ((pls == null) ? ("每日推荐歌单不可用：" + err1 + "  ") : "") + ((songs == null) ? ("每日推荐歌曲不可用：" + err2) : "");
					if (RecommendError.Length == 0)
					{
						RecommendError = null;
					}
				}
			});
		}, delegate
		{
			CommitIf(delegate
			{
				lock (Gate)
				{
					return gen == _recommendGen;
				}
			}, delegate
			{
				RecommendState = LoadState.Failed;
				RecommendError = "加载推荐时出现异常，请重试";
			});
		});
	}
}
