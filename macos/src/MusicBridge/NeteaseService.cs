using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBridge;

internal static class NeteaseService
{
	private static readonly object Gate = new object();

	private static int _generation;

	private static Thread _pollThread;

	private static Thread _restoreThread;

	private static volatile NeteaseConnState _connState = NeteaseConnState.NotConnected;

	private static volatile QrCardState _cardState = QrCardState.Hidden;

	private static volatile string _nickname = "";

	private static volatile string _qrPayload;

	private static int _qrPayloadVersion;

	public static NeteaseConnState ConnState => _connState;

	public static QrCardState CardState => _cardState;

	public static string Nickname => _nickname;

	public static string QrPayload => _qrPayload;

	public static int QrPayloadVersion => Volatile.Read(ref _qrPayloadVersion);

	public static event Action StateChanged;

	private static void Notify()
	{
		Plugin.RunOnMainThread(delegate
		{
			try
			{
				NeteaseService.StateChanged?.Invoke();
			}
			catch (Exception ex)
			{
				BridgeLog.Error("状态回调异常：" + ex.Message);
			}
		});
	}

	private static void SetConn(NeteaseConnState state, string nickname = null)
	{
		if (nickname != null)
		{
			_nickname = nickname;
		}
		if (state == NeteaseConnState.Connected)
		{
			if (!string.IsNullOrEmpty(_nickname))
			{
				DisplayNameStore.SetNetease(NeteaseLibrary.UserId, _nickname);
			}
			else
			{
				_nickname = DisplayNameStore.GetNetease(NeteaseLibrary.UserId);
			}
		}
		_connState = state;
		BridgeLog.Info("网易云连接状态 -> " + state.ToString() + (string.IsNullOrEmpty(_nickname) ? "" : "（账号已识别）"));
		Notify();
	}

	private static void SetCard(QrCardState state)
	{
		_cardState = state;
		BridgeLog.Info("登录卡片状态 -> " + state);
		Notify();
	}

	public static void BeginRestore()
	{
		lock (Gate)
		{
			if (_restoreThread == null || !_restoreThread.IsAlive)
			{
				_restoreThread = new Thread(RestoreWorker)
				{
					IsBackground = true,
					Name = "MusicBridge-Restore"
				};
				_restoreThread.Start();
			}
		}
	}

	private static void RestoreWorker()
	{
		try
		{
			string json;
			switch (SessionStore.TryLoad(out json))
			{
			case SessionStore.LoadResult.NotFound:
				BridgeLog.Info("未找到会话文件，显示未连接。");
				SetConn(NeteaseConnState.NotConnected, "");
				return;
			case SessionStore.LoadResult.Obsolete:
				SessionStore.Delete();
				BridgeLog.Info("旧格式会话文件已清除，请重新扫码登录。");
				SetConn(NeteaseConnState.NotConnected, "");
				return;
			case SessionStore.LoadResult.Corrupted:
				BridgeLog.Warn("会话文件损坏或解密失败，保留文件不删除，等待用户处理。");
				SetConn(NeteaseConnState.SessionCorrupted, "");
				return;
			}
			SetConn(NeteaseConnState.Restoring);
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			string text = "";
			try
			{
				JObject jObject = JObject.Parse(json);
				if (jObject["cookies"] is JObject jObject2)
				{
					foreach (KeyValuePair<string, JToken> item in jObject2)
					{
						dictionary[item.Key] = ((item.Value != null) ? item.Value.ToString() : "");
					}
				}
				text = jObject.Value<string>("nickname") ?? "";
			}
			catch
			{
				BridgeLog.Warn("会话内容解析失败（文件保留，不删除）。");
				SetConn(NeteaseConnState.SessionCorrupted, "");
				return;
			}
			if (dictionary.Count == 0)
			{
				BridgeLog.Warn("会话中没有可用凭证（文件保留）。");
				SetConn(NeteaseConnState.SessionCorrupted, "");
				return;
			}
			NeteaseApi.RestoreCookies(dictionary);
			AccountInfo info;
			switch (NeteaseApi.GetAccount(out info))
			{
			case AccountCheck.Valid:
				BridgeLog.Info("自动恢复成功。");
				AdoptAccount(info);
				SetConn(NeteaseConnState.Connected, info.Nickname);
				break;
			case AccountCheck.Unauthorized:
				BridgeLog.Info("会话失效，需要重新连接（文件保留，由用户决定是否清除）。");
				SetConn(NeteaseConnState.NeedsReconnect, text);
				break;
			case AccountCheck.NetworkError:
				BridgeLog.Warn("自动恢复失败：网络不可用。会话已保留，未删除任何文件。");
				SetConn(NeteaseConnState.NetworkUnavailable, text);
				break;
			default:
				BridgeLog.Warn("自动恢复失败：服务端返回异常。会话已保留。");
				SetConn(NeteaseConnState.NetworkUnavailable, text);
				break;
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Error("恢复流程异常：" + ex.GetType().Name);
			SetConn(NeteaseConnState.NetworkUnavailable);
		}
	}

	public static void BeginLogin()
	{
		lock (Gate)
		{
			_generation++;
			int myGen = _generation;
			_qrPayload = null;
			SetCard(QrCardState.Creating);
			Thread obj = new Thread((ThreadStart)delegate
			{
				LoginWorker(myGen);
			})
			{
				IsBackground = true,
				Name = "MusicBridge-QrLogin"
			};
			_pollThread = obj;
			obj.Start();
		}
	}

	public static void CancelLogin(string reason)
	{
		lock (Gate)
		{
			_generation++;
			_qrPayload = null;
		}
		if (CardState != QrCardState.Hidden)
		{
			BridgeLog.Info("登录轮询已取消（" + reason + "）。");
			SetCard(QrCardState.Hidden);
		}
	}

	private static bool IsCurrent(int gen)
	{
		lock (Gate)
		{
			return gen == _generation;
		}
	}

	private static void LoginWorker(int gen)
	{
		try
		{
			NeteaseApi.ResetCookies();
			bool networkError;
			string text = NeteaseApi.RequestUniKey(out networkError);
			if (!IsCurrent(gen))
			{
				return;
			}
			if (string.IsNullOrEmpty(text))
			{
				SetCard(networkError ? QrCardState.NetworkError : QrCardState.Failed);
				return;
			}
			_qrPayload = NeteaseApi.BuildQrPayload(text);
			Interlocked.Increment(ref _qrPayloadVersion);
			SetCard(QrCardState.WaitingScan);
			DateTime dateTime = DateTime.UtcNow.Add(MusicBridgeOptions.Current.Netease.QrLifetime);
			bool flag = false;
			while (IsCurrent(gen))
			{
				Thread.Sleep(MusicBridgeOptions.Current.Netease.QrPollInterval);
				if (!IsCurrent(gen))
				{
					break;
				}
				if (DateTime.UtcNow > dateTime)
				{
					SetCard(QrCardState.Expired);
					break;
				}
				QrStatus qrStatus = NeteaseApi.CheckQrStatus(text);
				if (!IsCurrent(gen))
				{
					break;
				}
				switch (qrStatus)
				{
				case QrStatus.ScannedWaitingConfirm:
					if (!flag)
					{
						flag = true;
						SetCard(QrCardState.ScannedWaitingConfirm);
					}
					break;
				case QrStatus.Expired:
					SetCard(QrCardState.Expired);
					return;
				case QrStatus.Success:
					OnLoginSucceeded(gen);
					return;
				case QrStatus.NetworkError:
					BridgeLog.Warn("轮询遇到网络错误，继续重试。");
					break;
				default:
					SetCard(QrCardState.Failed);
					return;
				case QrStatus.WaitingScan:
					break;
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Error("登录流程异常：" + ex.GetType().Name);
			if (IsCurrent(gen))
			{
				SetCard(QrCardState.Failed);
			}
		}
	}

	private static void OnLoginSucceeded(int gen)
	{
		if (!NeteaseApi.HasIdentityCookie())
		{
			BridgeLog.Warn("服务端回报登录成功，但没有拿到身份 cookie。");
			SetCard(QrCardState.Failed);
			return;
		}
		AccountInfo info;
		AccountCheck account = NeteaseApi.GetAccount(out info);
		if (!IsCurrent(gen))
		{
			return;
		}
		if (account != AccountCheck.Valid)
		{
			BridgeLog.Warn("登录后账号校验未通过：" + account);
			SetCard(QrCardState.Failed);
			return;
		}
		PersistSession(info);
		AdoptAccount(info);
		SetCard(QrCardState.Success);
		SetConn(NeteaseConnState.Connected, info.Nickname);
		lock (Gate)
		{
			_generation++;
		}
		Thread.Sleep(MusicBridgeOptions.Current.Netease.LoginSuccessCardLinger);
		_qrPayload = null;
		SetCard(QrCardState.Hidden);
		BridgeLog.Info("登录成功，二维码卡片已自动关闭，纹理已释放。");
	}

	private static void AdoptAccount(AccountInfo info)
	{
		NeteaseLibrary.UserId = info.UserId;
		NeteaseLibrary.Nickname = info.Nickname;
		BridgeLog.Info("已取得账号 userId（内容接口将使用它），开始加载歌单。");
		NeteaseLibrary.LoadPlaylists(force: true);
	}

	private static void PersistSession(AccountInfo info)
	{
		try
		{
			Dictionary<string, string> dictionary = NeteaseApi.ExportSessionCookies();
			JObject obj = new JObject
			{
				["cookies"] = JObject.FromObject(dictionary),
				["nickname"] = info.Nickname,
				["userId"] = info.UserId,
				["savedAtUtc"] = DateTime.UtcNow.ToString("o")
			};
			BridgeLog.Info("准备持久化会话（凭证条目 " + dictionary.Count + " 项，内容不记录）。");
			SessionStore.Save(obj.ToString(Formatting.None));
		}
		catch (Exception ex)
		{
			BridgeLog.Error("持久化会话失败：" + ex.GetType().Name);
		}
	}

	public static void Logout()
	{
		CancelLogin("用户退出登录");
		NeteaseApi.ResetCookies();
		_nickname = "";
		Plugin.RunOnMainThread(delegate
		{
			if (AudioPlayer.Instance != null)
			{
				AudioPlayer.Instance.Stop();
			}
			LyricsEngine.Reset();
			NeteasePanelUi.ResetState();
		});
		NeteaseLibrary.ClearAll();
		BridgeLog.Info("退出登录完成，会话文件删除结果：" + SessionStore.Delete() + "。未对网易云桌面客户端做任何操作。");
		SetConn(NeteaseConnState.NotConnected, "");
	}

	public static void Shutdown()
	{
		lock (Gate)
		{
			_generation++;
			_qrPayload = null;
		}
		_cardState = QrCardState.Hidden;
		BridgeLog.Info("MusicBridge 关闭：已停止所有网易云轮询。");
	}
}
