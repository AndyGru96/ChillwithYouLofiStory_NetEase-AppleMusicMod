using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MusicBridge;

internal sealed class CoverCache : MonoBehaviour
{
	private sealed class Entry
	{
		public Texture2D Texture;

		public Sprite Sprite;

		public long DecodedBytes;

		public int RefCount;

		public LinkedListNode<string> Node;
	}

	private static CoverCache _instance;

	private const int MaxEvictPerInsert = 2;

	private const int MaxDestroyPerFrame = 4;

	private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();

	private static readonly LinkedList<string> Lru = new LinkedList<string>();

	private static int _destroyFrame = -1;

	private static int _destroyedThisFrame;

	private static readonly Dictionary<string, List<Action<Sprite>>> Pending = new Dictionary<string, List<Action<Sprite>>>();

	private static readonly List<string> Waiting = new List<string>();

	private static int _active;

	private static long _decodedBytes;

	private static int _generation;

	private static readonly HashSet<UnityWebRequest> ActiveRequests = new HashSet<UnityWebRequest>();

	private static int MaxEntries => MusicBridgeOptions.Current.Shared.CoverMaximumEntries;

	private static int MaxConcurrent => MusicBridgeOptions.Current.Shared.CoverMaximumConcurrentDownloads;

	public static void Initialize()
	{
		if (!(_instance != null))
		{
			GameObject obj = new GameObject("MusicBridge_CoverCache");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.hideFlags = HideFlags.HideAndDontSave;
			_instance = obj.AddComponent<CoverCache>();
		}
	}

	private static string Sized(string url, int size)
	{
		return url + (url.Contains("?") ? "&" : "?") + "param=" + size + "y" + size;
	}

	public static string KeyOf(string url, int size)
	{
		if (!string.IsNullOrEmpty(url))
		{
			return Sized(url, size);
		}
		return null;
	}

	public static void Acquire(string key)
	{
		if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var value))
		{
			value.RefCount++;
			Touch(key, value);
		}
	}

	public static void Release(string key)
	{
		if (!string.IsNullOrEmpty(key) && Cache.TryGetValue(key, out var value) && value.RefCount > 0)
		{
			value.RefCount--;
		}
	}

	private static void Touch(string key, Entry e)
	{
		if (e.Node == null)
		{
			e.Node = Lru.AddFirst(key);
		}
		else if (e.Node.List == Lru && Lru.First != e.Node)
		{
			Lru.Remove(e.Node);
			Lru.AddFirst(e.Node);
		}
	}

	public static void Request(string url, int size, Action<Sprite> onReady)
	{
		if (string.IsNullOrEmpty(url) || onReady == null)
		{
			return;
		}
		Initialize();
		string text = Sized(url, size);
		List<Action<Sprite>> value2;
		if (Cache.TryGetValue(text, out var value) && value.Sprite != null)
		{
			Touch(text, value);
			onReady(value.Sprite);
		}
		else if (Pending.TryGetValue(text, out value2))
		{
			value2.Add(onReady);
			int num = Waiting.IndexOf(text);
			if (num >= 0 && num != Waiting.Count - 1)
			{
				Waiting.RemoveAt(num);
				Waiting.Add(text);
			}
		}
		else
		{
			Pending[text] = new List<Action<Sprite>> { onReady };
			Waiting.Add(text);
			_instance.Pump();
		}
	}

	public static void Cancel(string url, int size, Action<Sprite> onReady)
	{
		if (string.IsNullOrEmpty(url) || onReady == null)
		{
			return;
		}
		string text = Sized(url, size);
		if (Pending.TryGetValue(text, out var value))
		{
			value.Remove(onReady);
			if (value.Count <= 0 && Waiting.Remove(text))
			{
				Pending.Remove(text);
			}
		}
	}

	private void Pump()
	{
		while (_active < MaxConcurrent && Waiting.Count > 0)
		{
			int index = Waiting.Count - 1;
			string url = Waiting[index];
			Waiting.RemoveAt(index);
			_active++;
			StartCoroutine(Download(url, _generation));
		}
	}

	private IEnumerator Download(string url, int generation)
	{
		try
		{
			UnityWebRequest req = null;
			string text = null;
			try
			{
				req = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
				req.timeout = (int)Math.Ceiling(MusicBridgeOptions.Current.Shared.CoverDownloadTimeout.TotalSeconds);
			}
			catch (Exception ex)
			{
				text = ex.GetType().Name + ": " + ex.Message;
			}
			if (text != null || req == null)
			{
				BridgeLog.Warn("封面请求创建失败（" + (text ?? "未知") + "）主机=" + SafeHost(url));
				FlushWaiters(url, null);
				yield break;
			}
			using (req)
			{
				ActiveRequests.Add(req);
				yield return req.SendWebRequest();
				ActiveRequests.Remove(req);
				if (generation != _generation)
				{
					FlushWaiters(url, null);
					yield break;
				}
				Sprite sprite = null;
				if (req.result != UnityWebRequest.Result.Success)
				{
					BridgeLog.Warn("封面下载失败 result=" + req.result.ToString() + " http=" + req.responseCode + " 主机=" + SafeHost(url));
				}
				if (req.result == UnityWebRequest.Result.Success)
				{
					try
					{
						Texture2D content = DownloadHandlerTexture.GetContent(req);
						if (content != null)
						{
							content.wrapMode = TextureWrapMode.Clamp;
							sprite = Sprite.Create(content, new Rect(0f, 0f, content.width, content.height), new Vector2(0.5f, 0.5f), 100f);
							if (Cache.TryGetValue(url, out var value))
							{
								DropEntry(url, value);
							}
							long num = (long)content.width * (long)content.height * 4;
							Entry entry = new Entry
							{
								Texture = content,
								Sprite = sprite,
								DecodedBytes = num
							};
							Cache[url] = entry;
							entry.Node = Lru.AddFirst(url);
							_decodedBytes += num;
							Evict();
						}
					}
					catch (Exception ex2)
					{
						BridgeLog.Warn("封面解码失败：" + ex2.Message);
					}
				}
				FlushWaiters(url, sprite);
			}
		}
		finally
		{
			CoverCache coverCache = this;
			if (_active > 0)
			{
				_active--;
			}
			try
			{
				coverCache.Pump();
			}
			catch (Exception ex3)
			{
				BridgeLog.Warn("封面队列继续调度失败：" + ex3.Message);
			}
		}
	}

	private static void FlushWaiters(string url, Sprite sprite)
	{
		if (!Pending.TryGetValue(url, out var value))
		{
			return;
		}
		Pending.Remove(url);
		foreach (Action<Sprite> item in value)
		{
			try
			{
				item(sprite);
			}
			catch (Exception ex)
			{
				BridgeLog.Warn("封面回调异常：" + ex.Message);
			}
		}
	}

	private static string SafeHost(string url)
	{
		try
		{
			return new Uri(url).Host;
		}
		catch
		{
			return "?";
		}
	}

	private static void Evict()
	{
		if (_destroyFrame != Time.frameCount)
		{
			_destroyFrame = Time.frameCount;
			_destroyedThisFrame = 0;
		}
		long coverMaximumDecodedBytes = MusicBridgeOptions.Current.Shared.CoverMaximumDecodedBytes;
		int num = 2;
		while (num > 0 && _destroyedThisFrame < 4 && (Cache.Count > MaxEntries || _decodedBytes > coverMaximumDecodedBytes))
		{
			LinkedListNode<string> linkedListNode = Lru.Last;
			Entry entry = null;
			string key = null;
			while (linkedListNode != null)
			{
				if (Cache.TryGetValue(linkedListNode.Value, out var value) && value.RefCount == 0)
				{
					entry = value;
					key = linkedListNode.Value;
					break;
				}
				linkedListNode = linkedListNode.Previous;
			}
			if (entry != null)
			{
				DropEntry(key, entry);
				num--;
				_destroyedThisFrame++;
				continue;
			}
			break;
		}
	}

	private static void DropEntry(string key, Entry entry)
	{
		if (entry != null)
		{
			Cache.Remove(key);
			if (entry.Node != null && entry.Node.List == Lru)
			{
				Lru.Remove(entry.Node);
			}
			entry.Node = null;
			_decodedBytes = Math.Max(0L, _decodedBytes - entry.DecodedBytes);
			if (entry.Sprite != null)
			{
				UnityEngine.Object.Destroy(entry.Sprite);
			}
			if (entry.Texture != null)
			{
				UnityEngine.Object.Destroy(entry.Texture);
			}
		}
	}

	public static void Clear()
	{
		Plugin.RunOnMainThread(delegate
		{
			_generation++;
			foreach (UnityWebRequest activeRequest in ActiveRequests)
			{
				try
				{
					activeRequest.Abort();
				}
				catch
				{
				}
			}
			ActiveRequests.Clear();
			Waiting.Clear();
			Pending.Clear();
			foreach (KeyValuePair<string, Entry> item in Cache)
			{
				if (item.Value.Sprite != null)
				{
					UnityEngine.Object.Destroy(item.Value.Sprite);
				}
				if (item.Value.Texture != null)
				{
					UnityEngine.Object.Destroy(item.Value.Texture);
				}
			}
			Cache.Clear();
			Lru.Clear();
			_decodedBytes = 0L;
			BridgeLog.Info("封面缓存已清空。");
		});
	}

	public static void Apply(Image image, string url, int size, Color placeholder)
	{
		if (image == null)
		{
			return;
		}
		image.sprite = null;
		image.color = placeholder;
		if (string.IsNullOrEmpty(url))
		{
			return;
		}
		Request(url, size, delegate(Sprite sprite)
		{
			if (!(image == null) && !(sprite == null))
			{
				image.sprite = sprite;
				image.color = Color.white;
			}
		});
	}
}
