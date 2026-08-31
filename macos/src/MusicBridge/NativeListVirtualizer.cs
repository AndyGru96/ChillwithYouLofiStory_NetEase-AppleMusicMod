using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MusicBridge;

internal sealed class NativeListVirtualizer : MonoBehaviour
{
	internal sealed class VirtualOwned : MonoBehaviour
	{
	}

	internal sealed class DragProxy : MonoBehaviour, IBeginDragHandler, IEventSystemHandler, IDragHandler, IEndDragHandler
	{
		public NativeListVirtualizer Owner;

		public Component Row;

		public void OnBeginDrag(PointerEventData ev)
		{
			if (Owner != null && !Owner._dead)
			{
				Owner.DragBegin(Row);
			}
		}

		public void OnDrag(PointerEventData ev)
		{
			if (Owner != null && !Owner._dead)
			{
				Owner.DragMove(ev);
			}
		}

		public void OnEndDrag(PointerEventData ev)
		{
			if (Owner != null && !Owner._dead)
			{
				Owner.DragEnd();
			}
		}
	}

	private const int BufferRows = 3;

	private const string SegmentTopName = "MusicBridgeVirtualTop";

	private const string SegmentBottomName = "MusicBridgeVirtualBottom";

	private static Type _viewType;

	private static Type _rowType;

	private static Type _audioType;

	private static Type _serviceType;

	private static Type _facilityType;

	private static FieldInfo _fPrefab;

	private static FieldInfo _fParent;

	private static FieldInfo _fScroll;

	private static FieldInfo _fFacility;

	private static FieldInfo _fButtonList;

	private static FieldInfo _fPlayingList;

	private static FieldInfo _fRowAudio;

	private static FieldInfo _fRowTitle;

	private static FieldInfo _fRowArtist;

	private static FieldInfo _fRowPaused;

	private static FieldInfo _fRowRemove;

	private static FieldInfo _fRowTrigger;

	private static MethodInfo _mSetup;

	private static MethodInfo _mFavImage;

	private static MethodInfo _mCandImage;

	private static MethodInfo _mBackImage;

	private static MethodInfo _mStateIcon;

	private static FieldInfo _fAudioTitle;

	private static FieldInfo _fAudioCredit;

	private static FieldInfo _fAudioTag;

	private static FieldInfo _fAudioUuid;

	private static MethodInfo _mSwapAfter;

	private static MethodInfo _mGetService;

	private static MethodInfo _mGetPlayingMusic;

	private static MethodInfo _mGetIsPaused;

	private static bool _resolved;

	private static bool _resolveOk;

	private static bool _abandoned;

	private bool _regionVisible = true;

	private object _view;

	private IList _buttonList;

	private GameObject _prefab;

	private RectTransform _content;

	private ScrollRect _scroll;

	private object _facility;

	private object _playingList;

	private PropertyInfo _listCount;

	private MethodInfo _listItem;

	private RectTransform _topSpacer;

	private RectTransform _bottomSpacer;

	private readonly List<Component> _pool = new List<Component>();

	private readonly Stack<Component> _free = new Stack<Component>();

	private readonly Dictionary<int, Component> _bound = new Dictionary<int, Component>();

	private readonly List<int> _scratch = new List<int>();

	private readonly Vector3[] _corners = new Vector3[4];

	private float _stride = 64f;

	private float _rowHeight = 64f;

	private float _spacing;

	private int _lastFirst = -1;

	private int _lastCount = -1;

	private int _total;

	private bool _strideVerified;

	private bool _dead;

	private List<object> _dragOrder;

	private int _dragFrom = -1;

	private int _dragTo = -1;

	private bool _seedWasLocal;

	private static bool _dragProxyReported;

	internal static NativeListVirtualizer Active { get; private set; }

	public static void Install(Harmony harmony)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		try
		{
			if (!Resolve())
			{
				BridgeLog.Warn("原生列表虚拟化：游戏成员对不上，不启用。");
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(_viewType, "ViewPlayList", (Type[])null, (Type[])null);
			MethodInfo methodInfo2 = AccessTools.Method(_viewType, "ScrollToPlayingMusic", (Type[])null, (Type[])null);
			if (methodInfo == null)
			{
				BridgeLog.Warn("原生列表虚拟化：找不到 ViewPlayList，不启用。");
				return;
			}
			harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(AccessTools.Method(typeof(NativeListVirtualizer), "ViewPlayList_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			if (methodInfo2 != null)
			{
				harmony.Patch((MethodBase)methodInfo2, new HarmonyMethod(AccessTools.Method(typeof(NativeListVirtualizer), "ScrollTo_Prefix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
			else
			{
				BridgeLog.Warn("原生列表虚拟化：找不到 ScrollToPlayingMusic，虚拟化时不会自动滚到当前曲目。");
			}
			BridgeLog.Info("原生列表虚拟化已挂钩（超过 " + MusicBridgeOptions.Current.Local.VirtualizeThreshold + " 首时接管）。");
		}
		catch (Exception ex)
		{
			BridgeLog.Error("原生列表虚拟化安装失败：" + ex);
		}
	}

	private static bool Resolve()
	{
		if (_resolved)
		{
			return _resolveOk;
		}
		_resolved = true;
		try
		{
			_viewType = AccessTools.TypeByName("Bulbul.MusicPlayListView") ?? AccessTools.TypeByName("MusicPlayListView");
			_rowType = AccessTools.TypeByName("Bulbul.MusicPlayListButtons") ?? AccessTools.TypeByName("MusicPlayListButtons");
			_audioType = AccessTools.TypeByName("Bulbul.GameAudioInfo") ?? AccessTools.TypeByName("GameAudioInfo");
			_serviceType = AccessTools.TypeByName("MusicService") ?? AccessTools.TypeByName("Bulbul.MusicService");
			_facilityType = AccessTools.TypeByName("Bulbul.FacilityMusic") ?? AccessTools.TypeByName("FacilityMusic");
			if (_viewType == null || _rowType == null || _audioType == null || _serviceType == null || _facilityType == null)
			{
				return false;
			}
			_fPrefab = AccessTools.Field(_viewType, "_playListButtonsPrefab");
			_fParent = AccessTools.Field(_viewType, "_playListButtonsParent");
			_fScroll = AccessTools.Field(_viewType, "_scrollRect");
			_fFacility = AccessTools.Field(_viewType, "_facilityMusic");
			_fButtonList = AccessTools.Field(_viewType, "_playListButtonList");
			_fPlayingList = AccessTools.Field(_viewType, "_playingList");
			_fRowAudio = AccessTools.Field(_rowType, "_audioInfo");
			_fRowTitle = AccessTools.Field(_rowType, "_musicTitleText");
			_fRowArtist = AccessTools.Field(_rowType, "_artistNameText");
			_fRowPaused = AccessTools.Field(_rowType, "isPaused");
			_fRowRemove = AccessTools.Field(_rowType, "removeInteractableUI");
			_fRowTrigger = AccessTools.Field(_rowType, "reorderTrigger");
			_mSetup = AccessTools.Method(_rowType, "Setup", (Type[])null, (Type[])null);
			_mFavImage = AccessTools.Method(_rowType, "SetFavoriteImage", (Type[])null, (Type[])null);
			_mCandImage = AccessTools.Method(_rowType, "SetPlayCandidateImage", (Type[])null, (Type[])null);
			_mBackImage = AccessTools.Method(_rowType, "UpdateBackImage", (Type[])null, (Type[])null);
			_mStateIcon = AccessTools.Method(_rowType, "UpdatePlayStateIcon", (Type[])null, (Type[])null);
			_fAudioTitle = AccessTools.Field(_audioType, "Title");
			_fAudioCredit = AccessTools.Field(_audioType, "Credit");
			_fAudioTag = AccessTools.Field(_audioType, "Tag");
			_fAudioUuid = AccessTools.Field(_audioType, "UUID");
			_mSwapAfter = AccessTools.Method(_serviceType, "SwapAfter", (Type[])null, (Type[])null);
			_mGetService = AccessTools.PropertyGetter(_facilityType, "MusicService");
			_mGetPlayingMusic = AccessTools.PropertyGetter(_facilityType, "PlayingMusic");
			_mGetIsPaused = AccessTools.PropertyGetter(_facilityType, "IsPaused");
			_resolveOk = _fPrefab != null && _fParent != null && _fScroll != null && _fFacility != null && _fButtonList != null && _fPlayingList != null && _fRowAudio != null && _fRowTitle != null && _fRowArtist != null && _fRowPaused != null && _fRowRemove != null && _mSetup != null && _mFavImage != null && _mCandImage != null && _mBackImage != null && _mStateIcon != null && _fAudioTitle != null && _fAudioCredit != null && _fAudioTag != null && _mSwapAfter != null && _mGetService != null && _mGetPlayingMusic != null;
			return _resolveOk;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("原生列表虚拟化：成员解析异常：" + ex);
			return false;
		}
	}

	private static bool ViewPlayList_Prefix(object __instance)
	{
		try
		{
			if (_abandoned)
			{
				return true;
			}
			Component component = __instance as Component;
			if (component == null)
			{
				return true;
			}
			NativeListVirtualizer nativeListVirtualizer = component.GetComponent<NativeListVirtualizer>();
			int num = CountOf(__instance);
			int virtualizeThreshold = MusicBridgeOptions.Current.Local.VirtualizeThreshold;
			if (!MusicBridgeOptions.Current.Local.VirtualizeNativeList || num <= virtualizeThreshold)
			{
				if (nativeListVirtualizer != null)
				{
					nativeListVirtualizer.Teardown();
				}
				return true;
			}
			if (nativeListVirtualizer == null || nativeListVirtualizer._dead)
			{
				if (nativeListVirtualizer != null)
				{
					nativeListVirtualizer.Teardown();
					UnityEngine.Object.Destroy(nativeListVirtualizer);
				}
				nativeListVirtualizer = component.gameObject.AddComponent<NativeListVirtualizer>();
				if (!nativeListVirtualizer.Attach(__instance))
				{
					_abandoned = true;
					BridgeLog.Warn("虚拟化无法接管播放列表（字段或布局参数取不到），本次运行保持原生渲染。");
					nativeListVirtualizer.Teardown();
					UnityEngine.Object.Destroy(nativeListVirtualizer);
					return true;
				}
			}
			nativeListVirtualizer.Rebuild();
			return false;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("虚拟化接管失败，本次交还原生渲染：" + ex);
			return true;
		}
	}

	private static bool ScrollTo_Prefix(object __instance, string __0)
	{
		try
		{
			Component component = __instance as Component;
			if (component == null)
			{
				return true;
			}
			NativeListVirtualizer component2 = component.GetComponent<NativeListVirtualizer>();
			if (component2 == null || component2._dead)
			{
				return true;
			}
			component2.ScrollToTitle(__0);
			return false;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("虚拟化滚动定位失败：" + ex.Message);
			return true;
		}
	}

	private static int CountOf(object view)
	{
		object value = _fPlayingList.GetValue(view);
		if (value == null)
		{
			return 0;
		}
		PropertyInfo propertyInfo = AccessTools.Property(value.GetType(), "Count");
		if (propertyInfo == null)
		{
			return 0;
		}
		return (int)propertyInfo.GetValue(value, null);
	}

	private bool Attach(object view)
	{
		Active = this;
		_view = view;
		_prefab = _fPrefab.GetValue(view) as GameObject;
		GameObject gameObject = _fParent.GetValue(view) as GameObject;
		_scroll = _fScroll.GetValue(view) as ScrollRect;
		_facility = _fFacility.GetValue(view);
		_buttonList = _fButtonList.GetValue(view) as IList;
		_playingList = _fPlayingList.GetValue(view);
		if (_prefab == null || gameObject == null || _scroll == null || _facility == null || _buttonList == null || _playingList == null)
		{
			return false;
		}
		_content = gameObject.GetComponent<RectTransform>();
		if (_content == null)
		{
			return false;
		}
		Type type = _playingList.GetType();
		_listCount = AccessTools.Property(type, "Count");
		_listItem = AccessTools.Method(type, "get_Item", new Type[1] { typeof(int) }, (Type[])null);
		if (_listCount == null || _listItem == null)
		{
			return false;
		}
		RectTransform component = _prefab.GetComponent<RectTransform>();
		if (component == null)
		{
			return false;
		}
		_rowHeight = ((component.sizeDelta.y > 1f) ? component.sizeDelta.y : component.rect.height);
		if (_rowHeight <= 1f)
		{
			return false;
		}
		VerticalLayoutGroup component2 = gameObject.GetComponent<VerticalLayoutGroup>();
		_spacing = ((component2 != null) ? component2.spacing : 0f);
		_stride = _rowHeight + _spacing;
		if (_stride <= 1f)
		{
			return false;
		}
		if (Mathf.Abs(_spacing) > 0.01f)
		{
			BridgeLog.Warn("虚拟化不接管：布局组 spacing=" + _spacing.ToString("0.##") + "，当前实现只支持 0，交还原生渲染。");
			return false;
		}
		BridgeLog.Info("虚拟化接管播放列表：行高=" + _rowHeight.ToString("0.##") + " 行距=" + _stride.ToString("0.##") + "。");
		return true;
	}

	private int Total()
	{
		try
		{
			return (int)_listCount.GetValue(_playingList, null);
		}
		catch
		{
			return 0;
		}
	}

	private object ItemAt(int index)
	{
		if (_dragOrder != null)
		{
			if (index < 0 || index >= _dragOrder.Count)
			{
				return null;
			}
			return _dragOrder[index];
		}
		try
		{
			return _listItem.Invoke(_playingList, new object[1] { index });
		}
		catch
		{
			return null;
		}
	}

	public void Rebuild()
	{
		if (_dead)
		{
			return;
		}
		object value = _fPlayingList.GetValue(_view);
		if (value != null && value != _playingList)
		{
			_playingList = value;
			_listCount = AccessTools.Property(value.GetType(), "Count");
			_listItem = AccessTools.Method(value.GetType(), "get_Item", new Type[1] { typeof(int) }, (Type[])null);
			if (_listCount == null || _listItem == null)
			{
				Abandon("播放列表类型缺少 Count / 索引器");
				return;
			}
		}
		DestroyNativeRows();
		EnsureSpacers();
		_total = Total();
		if (!_seedWasLocal && _pool.Count > 0 && HasLocalTrack())
		{
			BridgeLog.Info("行池重建：建池时还没有本地导入曲目，删除按钮未订阅，现在补上。");
			foreach (Component item in _pool)
			{
				if (item != null)
				{
					UnityEngine.Object.DestroyImmediate(item.gameObject);
				}
			}
			_pool.Clear();
			_free.Clear();
			_bound.Clear();
		}
		_lastFirst = -1;
		_lastCount = -1;
		ReleaseAll();
		if (_regionVisible)
		{
			RefreshVisible();
		}
	}

	private void DestroyNativeRows()
	{
		if (_buttonList == null)
		{
			return;
		}
		for (int i = 0; i < _buttonList.Count; i++)
		{
			Component component = _buttonList[i] as Component;
			if (!(component == null) && !_pool.Contains(component))
			{
				UnityEngine.Object.DestroyImmediate(component.gameObject);
			}
		}
		_buttonList.Clear();
		foreach (Component item in _pool)
		{
			if (item != null)
			{
				_buttonList.Add(item);
			}
		}
	}

	private void EnsureSpacers()
	{
		if (_topSpacer == null)
		{
			_topSpacer = CreateSpacer("MusicBridgeVirtualTop");
		}
		if (_bottomSpacer == null)
		{
			_bottomSpacer = CreateSpacer("MusicBridgeVirtualBottom");
		}
	}

	private RectTransform CreateSpacer(string name)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(_content, worldPositionStays: false);
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.sizeDelta = new Vector2(0f, 0f);
		gameObject.AddComponent<VirtualOwned>();
		return rectTransform;
	}

	internal static void TickActive()
	{
		NativeListVirtualizer active = Active;
		if (active != null && !active._dead)
		{
			active.Tick();
		}
	}

	internal static void SetRegionVisible(bool visible)
	{
		NativeListVirtualizer active = Active;
		if (!(active == null) && !active._dead && active._regionVisible != visible)
		{
			active._regionVisible = visible;
			active.ApplyRegionVisibility();
		}
	}

	private void ApplyRegionVisibility()
	{
		if (_topSpacer != null)
		{
			_topSpacer.gameObject.SetActive(_regionVisible);
		}
		if (_bottomSpacer != null)
		{
			_bottomSpacer.gameObject.SetActive(_regionVisible);
		}
		foreach (KeyValuePair<int, Component> item in _bound)
		{
			if (item.Value != null)
			{
				item.Value.gameObject.SetActive(_regionVisible);
			}
		}
		MarkDirty();
	}

	private void Tick()
	{
		if (_dead || !_regionVisible)
		{
			return;
		}
		try
		{
			if (_view == null || _content == null)
			{
				Abandon("视图或容器已消失");
				return;
			}
			if (Total() != _total)
			{
				Rebuild();
				return;
			}
			RefreshVisible();
			VerifyStrideOnce();
		}
		catch (Exception ex)
		{
			BridgeLog.Error("虚拟化刷新异常：" + ex);
			Abandon("刷新过程抛出异常");
		}
	}

	private void VerifyStrideOnce()
	{
		if (_strideVerified || _bound.Count < 2)
		{
			return;
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		float num3 = 0f;
		float num4 = 0f;
		foreach (KeyValuePair<int, Component> item in _bound)
		{
			RectTransform rectTransform = ((item.Value != null) ? (item.Value.transform as RectTransform) : null);
			if (!(rectTransform == null))
			{
				float y = rectTransform.anchoredPosition.y;
				if (item.Key < num)
				{
					num2 = num;
					num4 = num3;
					num = item.Key;
					num3 = y;
				}
				else if (item.Key < num2)
				{
					num2 = item.Key;
					num4 = y;
				}
			}
		}
		if (num == int.MaxValue || num2 == int.MaxValue)
		{
			return;
		}
		float num5 = Mathf.Abs(num3 - num4) / (float)(num2 - num);
		if (!(num5 < 1f))
		{
			_strideVerified = true;
			if (Mathf.Abs(num5 - _stride) > 0.5f)
			{
				Abandon("自检未通过：实测行距 " + num5.ToString("0.##") + " 与计算值 " + _stride.ToString("0.##") + " 不一致，等距假设不成立");
			}
			else
			{
				BridgeLog.Info("虚拟化自检通过：实测行距 " + num5.ToString("0.##") + "。");
			}
		}
	}

	private void RefreshVisible()
	{
		if (_total <= 0)
		{
			ReleaseAll();
			ApplySpacers(0, 0);
			return;
		}
		if (!Measure(out var scrolledPast, out var viewHeight))
		{
			scrolledPast = 0f;
			viewHeight = _rowHeight * 12f;
		}
		if (!VirtualWindow.Compute(_total, _stride, scrolledPast, viewHeight, 3, out var firstIndex, out var count))
		{
			ReleaseAll();
			ApplySpacers(0, 0);
		}
		else
		{
			if (firstIndex == _lastFirst && count == _lastCount)
			{
				return;
			}
			_lastFirst = firstIndex;
			_lastCount = count;
			_scratch.Clear();
			foreach (KeyValuePair<int, Component> item in _bound)
			{
				if (item.Key < firstIndex || item.Key >= firstIndex + count)
				{
					_scratch.Add(item.Key);
				}
			}
			for (int i = 0; i < _scratch.Count; i++)
			{
				Component component = _bound[_scratch[i]];
				if (component != null)
				{
					component.gameObject.SetActive(value: false);
					_free.Push(component);
				}
				_bound.Remove(_scratch[i]);
			}
			for (int j = firstIndex; j < firstIndex + count; j++)
			{
				if (!_bound.ContainsKey(j))
				{
					Component component2 = TakeRow();
					if (component2 == null)
					{
						Abandon("建不出行对象");
						return;
					}
					_bound[j] = component2;
					Bind(component2, j);
				}
			}
			ApplySpacers(firstIndex, count);
			ApplySiblingOrder(firstIndex, count);
		}
	}

	private void ApplySpacers(int first, int count)
	{
		if (!(_topSpacer == null) && !(_bottomSpacer == null))
		{
			SetSpacer(_topSpacer, first);
			SetSpacer(_bottomSpacer, Mathf.Max(0, _total - first - count));
		}
	}

	private void SetSpacer(RectTransform spacer, int rows)
	{
		if (_regionVisible && !spacer.gameObject.activeSelf)
		{
			spacer.gameObject.SetActive(value: true);
		}
		float num = ((rows <= 0) ? 0f : ((float)rows * _rowHeight + (float)(rows - 1) * _spacing));
		if (Mathf.Abs(spacer.sizeDelta.y - num) > 0.01f)
		{
			spacer.sizeDelta = new Vector2(spacer.sizeDelta.x, num);
			MarkDirty();
		}
	}

	private void ApplySiblingOrder(int first, int count)
	{
		int num = BridgePanel.RowRegionStartIndex(_content);
		if (num < 0)
		{
			num = 0;
		}
		bool flag = false;
		flag |= PlaceAt(_topSpacer.transform, num++);
		for (int i = first; i < first + count; i++)
		{
			if (_bound.TryGetValue(i, out var value) && !(value == null))
			{
				flag |= PlaceAt(value.transform, num++);
			}
		}
		if (flag | PlaceAt(_bottomSpacer.transform, num))
		{
			MarkDirty();
		}
	}

	private static bool PlaceAt(Transform t, int index)
	{
		if (t == null)
		{
			return false;
		}
		if (index < 0)
		{
			index = 0;
		}
		int num = ((t.parent != null) ? (t.parent.childCount - 1) : 0);
		if (index > num)
		{
			index = num;
		}
		if (t.GetSiblingIndex() == index)
		{
			return false;
		}
		t.SetSiblingIndex(index);
		return true;
	}

	private void MarkDirty()
	{
		if (!(_content == null))
		{
			LayoutRebuilder.MarkLayoutForRebuild(_content);
		}
	}

	private bool Measure(out float scrolledPast, out float viewHeight)
	{
		scrolledPast = 0f;
		viewHeight = 0f;
		RectTransform rectTransform = ((_scroll != null) ? _scroll.viewport : null);
		if (rectTransform == null || _topSpacer == null)
		{
			return false;
		}
		float y = _content.lossyScale.y;
		if (y <= 0.0001f)
		{
			return false;
		}
		rectTransform.GetWorldCorners(_corners);
		float y2 = _corners[1].y;
		float y3 = _corners[0].y;
		viewHeight = (y2 - y3) / y;
		if (viewHeight <= 1f)
		{
			return false;
		}
		_topSpacer.GetWorldCorners(_corners);
		float y4 = _corners[1].y;
		scrolledPast = (y4 - y2) / y;
		return true;
	}

	private Component TakeRow()
	{
		while (_free.Count > 0)
		{
			Component component = _free.Pop();
			if (!(component == null))
			{
				component.gameObject.SetActive(value: true);
				return component;
			}
		}
		return CreateRow();
	}

	private Component CreateRow()
	{
		try
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(_prefab, _content, worldPositionStays: false);
			Vector3 localPosition = gameObject.transform.localPosition;
			localPosition.z = 0f;
			gameObject.transform.localPosition = localPosition;
			gameObject.transform.localScale = Vector3.one;
			Component component = gameObject.GetComponent(_rowType);
			if (component == null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
				return null;
			}
			object obj = SeedTrack();
			if (obj == null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
				return null;
			}
			_mSetup.Invoke(component, new object[2] { obj, _facility });
			if (gameObject.GetComponent<VirtualOwned>() == null)
			{
				gameObject.AddComponent<VirtualOwned>();
			}
			AttachDragProxy(component);
			_pool.Add(component);
			if (_buttonList != null && !_buttonList.Contains(component))
			{
				_buttonList.Add(component);
			}
			return component;
		}
		catch (Exception ex)
		{
			BridgeLog.Error("虚拟化建行失败：" + ex);
			return null;
		}
	}

	private object SeedTrack()
	{
		object obj = null;
		for (int i = 0; i < _total; i++)
		{
			object obj2 = ItemAt(i);
			if (obj2 == null)
			{
				continue;
			}
			if (obj == null)
			{
				obj = obj2;
			}
			try
			{
				if ((Convert.ToInt32(_fAudioTag.GetValue(obj2)) & 0x10) != 0)
				{
					_seedWasLocal = true;
					return obj2;
				}
			}
			catch
			{
			}
		}
		return obj;
	}

	private bool HasLocalTrack()
	{
		for (int i = 0; i < _total; i++)
		{
			object obj = ItemAt(i);
			if (obj == null)
			{
				continue;
			}
			try
			{
				if ((Convert.ToInt32(_fAudioTag.GetValue(obj)) & 0x10) != 0)
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private void Bind(Component row, int index)
	{
		object obj = ItemAt(index);
		if (obj == null)
		{
			_bound.Remove(index);
			row.gameObject.SetActive(value: false);
			_free.Push(row);
			return;
		}
		_fRowAudio.SetValue(row, obj);
		TextMeshProUGUI textMeshProUGUI = _fRowTitle.GetValue(row) as TextMeshProUGUI;
		TextMeshProUGUI textMeshProUGUI2 = _fRowArtist.GetValue(row) as TextMeshProUGUI;
		if (textMeshProUGUI != null)
		{
			textMeshProUGUI.text = LocalTrackNumbering.Decorate(obj);
		}
		if (textMeshProUGUI2 != null)
		{
			textMeshProUGUI2.text = (_fAudioCredit.GetValue(obj) as string) ?? "";
		}
		Component component = _fRowRemove.GetValue(row) as Component;
		if (component != null)
		{
			int num = Convert.ToInt32(_fAudioTag.GetValue(obj));
			component.gameObject.SetActive((num & 0x10) != 0);
		}
		bool flag = ((_mGetPlayingMusic != null) ? _mGetPlayingMusic.Invoke(_facility, null) : null) == obj && _mGetIsPaused != null && (bool)_mGetIsPaused.Invoke(_facility, null);
		_fRowPaused.SetValue(row, flag);
		Invoke(_mFavImage, row);
		Invoke(_mCandImage, row);
		Invoke(_mBackImage, row);
		Invoke(_mStateIcon, row);
		Place(row, index);
	}

	private static void Invoke(MethodInfo m, Component row)
	{
		string text = ((m != null) ? m.Name : "?");
		try
		{
			if (m != null)
			{
				m.Invoke(row, null);
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("行刷新调用失败（" + text + "）：" + ex.Message);
		}
	}

	private void Place(Component row, int index)
	{
		RectTransform rectTransform = row.transform as RectTransform;
		if (!(rectTransform == null) && Mathf.Abs(rectTransform.sizeDelta.y - _rowHeight) > 0.01f)
		{
			rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, _rowHeight);
		}
	}

	private void ReleaseAll()
	{
		foreach (KeyValuePair<int, Component> item in _bound)
		{
			if (item.Value != null)
			{
				item.Value.gameObject.SetActive(value: false);
				_free.Push(item.Value);
			}
		}
		_bound.Clear();
		_lastFirst = -1;
		_lastCount = -1;
	}

	public void ScrollToTitle(string title)
	{
		if (string.IsNullOrEmpty(title) || _scroll == null)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < _total; i++)
		{
			object obj = ItemAt(i);
			if (obj != null && string.Equals(_fAudioTitle.GetValue(obj) as string, title, StringComparison.Ordinal))
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			ScrollToIndex(num);
		}
	}

	private void ScrollToIndex(int index)
	{
		RectTransform viewport = _scroll.viewport;
		if (!(viewport == null) && !(_content == null))
		{
			float height = viewport.rect.height;
			float height2 = _content.rect.height;
			if (!(height2 <= height + 1f))
			{
				float value = Mathf.Max(0f, height2 - (float)_total * _stride) + (float)index * _stride + _rowHeight * 0.5f - height * 0.5f;
				value = Mathf.Clamp(value, 0f, height2 - height);
				_scroll.verticalNormalizedPosition = 1f - value / (height2 - height);
			}
		}
	}

	private void AttachDragProxy(Component row)
	{
		try
		{
			Component component = ((_fRowTrigger != null) ? (_fRowTrigger.GetValue(row) as Component) : null);
			GameObject gameObject = ((component != null) ? component.gameObject : row.gameObject);
			DragProxy dragProxy = gameObject.GetComponent<DragProxy>();
			if (dragProxy == null)
			{
				dragProxy = gameObject.AddComponent<DragProxy>();
			}
			dragProxy.Owner = this;
			dragProxy.Row = row;
			if (!_dragProxyReported)
			{
				_dragProxyReported = true;
				Graphic component2 = gameObject.GetComponent<Graphic>();
				BridgeLog.Info("拖动代理已挂载：宿主=" + gameObject.name + "（" + ((component != null) ? "行内拖动柄" : "整行兜底，reorderTrigger 未赋值") + "） 激活=" + gameObject.activeInHierarchy + " 可射线命中=" + ((component2 != null) ? component2.raycastTarget.ToString() : "无 Graphic，收不到指针事件"));
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("虚拟化拖动代理挂载失败：" + ex.Message);
		}
	}

	private int IndexOfRow(Component row)
	{
		foreach (KeyValuePair<int, Component> item in _bound)
		{
			if ((object)item.Value == row)
			{
				return item.Key;
			}
		}
		return -1;
	}

	internal void DragBegin(Component row)
	{
		_dragFrom = IndexOfRow(row);
		if (_dragFrom >= 0)
		{
			_dragOrder = new List<object>(_total);
			for (int i = 0; i < _total; i++)
			{
				_dragOrder.Add(_listItem.Invoke(_playingList, new object[1] { i }));
			}
			_dragTo = _dragFrom;
		}
	}

	internal void DragMove(PointerEventData ev)
	{
		if (_dragOrder != null && _dragFrom >= 0 && !(_scroll == null) && !(_scroll.viewport == null) && RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, ev.position, ev.pressEventCamera, out var localPoint))
		{
			float num = _content.rect.yMax - localPoint.y;
			float num2 = Mathf.Max(0f, _content.rect.height - (float)_total * _stride);
			int num3 = Mathf.Clamp(Mathf.FloorToInt((num - num2) / _stride), 0, _total - 1);
			if (num3 != _dragTo)
			{
				object item = _dragOrder[_dragTo];
				_dragOrder.RemoveAt(_dragTo);
				_dragOrder.Insert(num3, item);
				_dragTo = num3;
				_lastFirst = -1;
				_lastCount = -1;
				ReleaseAll();
				RefreshVisible();
			}
		}
	}

	internal void DragEnd()
	{
		try
		{
			if (_dragOrder != null && _dragFrom >= 0 && _dragTo >= 0 && _dragTo != _dragFrom)
			{
				object obj = _dragOrder[_dragTo];
				object obj2 = ((_dragTo > 0) ? _dragOrder[_dragTo - 1] : null);
				object obj3 = _mGetService.Invoke(_facility, null);
				if (obj3 != null)
				{
					_mSwapAfter.Invoke(obj3, new object[2] { obj, obj2 });
				}
			}
		}
		catch (Exception ex)
		{
			BridgeLog.Error("虚拟化排序提交失败：" + ex);
		}
		finally
		{
			_dragOrder = null;
			_dragFrom = -1;
			_dragTo = -1;
			_lastFirst = -1;
			_lastCount = -1;
			ReleaseAll();
			RefreshVisible();
		}
	}

	private void Abandon(string why)
	{
		_abandoned = true;
		BridgeLog.Warn("虚拟化已放弃，播放列表交还游戏原生渲染：" + why);
		Teardown();
	}

	public void Teardown()
	{
		if (_dead)
		{
			return;
		}
		_dead = true;
		if ((object)Active == this)
		{
			Active = null;
		}
		try
		{
			foreach (Component item in _pool)
			{
				if (item != null)
				{
					UnityEngine.Object.DestroyImmediate(item.gameObject);
				}
			}
			_pool.Clear();
			_free.Clear();
			_bound.Clear();
			if (_buttonList != null)
			{
				_buttonList.Clear();
			}
			if (_topSpacer != null)
			{
				UnityEngine.Object.DestroyImmediate(_topSpacer.gameObject);
			}
			if (_bottomSpacer != null)
			{
				UnityEngine.Object.DestroyImmediate(_bottomSpacer.gameObject);
			}
			_topSpacer = null;
			_bottomSpacer = null;
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("虚拟化退出清理异常：" + ex.Message);
		}
	}

	private void OnDestroy()
	{
		Teardown();
	}
}
