using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MusicBridge;

internal sealed class VirtualTrackList : MonoBehaviour
{
	private const int BufferRows = 4;

	private RectTransform _segment;

	private LayoutElement _layout;

	private RectTransform _viewport;

	private IVirtualTrackSource _src;

	private float _pitch = 59f;

	private float _rowHeight = 56f;

	private float _indent;

	private readonly List<PanelRows.TrackRow> _pool = new List<PanelRows.TrackRow>();

	private readonly Stack<PanelRows.TrackRow> _free = new Stack<PanelRows.TrackRow>();

	private readonly Dictionary<int, PanelRows.TrackRow> _bound = new Dictionary<int, PanelRows.TrackRow>();

	private readonly List<int> _scratch = new List<int>();

	private readonly Vector3[] _segCorners = new Vector3[4];

	private readonly Vector3[] _vpCorners = new Vector3[4];

	private int _lastFirst = -1;

	private int _lastCount = -1;

	private long _lastNowPlayingId = -1L;

	public int ItemCount
	{
		get
		{
			if (_src == null)
			{
				return 0;
			}
			return _src.Count;
		}
	}

	public static VirtualTrackList Create(Transform parent, float rowHeight, float spacing, float indent)
	{
		GameObject obj = new GameObject("VirtualTracks");
		obj.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rectTransform = obj.AddComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(1f, 1f);
		rectTransform.pivot = new Vector2(0.5f, 1f);
		LayoutElement layoutElement = obj.AddComponent<LayoutElement>();
		layoutElement.flexibleHeight = 0f;
		VirtualTrackList virtualTrackList = obj.AddComponent<VirtualTrackList>();
		virtualTrackList._segment = rectTransform;
		virtualTrackList._layout = layoutElement;
		virtualTrackList._rowHeight = rowHeight;
		virtualTrackList._pitch = rowHeight + spacing;
		virtualTrackList._indent = indent;
		virtualTrackList._viewport = BridgePanel.ListViewport;
		return virtualTrackList;
	}

	public void SetItems(IVirtualTrackSource source)
	{
		_src = source;
		_lastFirst = -1;
		_lastCount = -1;
		_lastNowPlayingId = -1L;
		foreach (KeyValuePair<int, PanelRows.TrackRow> item in _bound)
		{
			PanelRows.UnbindTrackRow(item.Value);
		}
		_bound.Clear();
		_free.Clear();
		foreach (PanelRows.TrackRow item2 in _pool)
		{
			if (item2.Root != null)
			{
				item2.Root.SetActive(value: false);
				_free.Push(item2);
			}
		}
		float num = VirtualWindow.SegmentHeight(ItemCount, _pitch);
		_layout.preferredHeight = num;
		_layout.minHeight = num;
		_segment.sizeDelta = new Vector2(_segment.sizeDelta.x, num);
		RefreshVisibleRange();
	}

	private void LateUpdate()
	{
		if (_viewport == null)
		{
			_viewport = BridgePanel.ListViewport;
		}
		RefreshVisibleRange();
	}

	public void RefreshVisibleRange()
	{
		if (_segment == null || _src == null)
		{
			return;
		}
		int count = _src.Count;
		if (count == 0)
		{
			ReleaseAll();
			return;
		}
		if (!MeasureViewport(out var scrolledPast, out var viewHeight))
		{
			scrolledPast = 0f;
			viewHeight = _rowHeight * 12f;
		}
		if (!VirtualWindow.Compute(count, _pitch, scrolledPast, viewHeight, 4, out var firstIndex, out var count2))
		{
			ReleaseAll();
			return;
		}
		long currentId = _src.CurrentId;
		bool flag = firstIndex != _lastFirst || count2 != _lastCount;
		bool flag2 = currentId != _lastNowPlayingId;
		if (!flag && !flag2)
		{
			return;
		}
		_lastFirst = firstIndex;
		_lastCount = count2;
		_lastNowPlayingId = currentId;
		if (flag)
		{
			_scratch.Clear();
			foreach (KeyValuePair<int, PanelRows.TrackRow> item in _bound)
			{
				if (item.Key < firstIndex || item.Key >= firstIndex + count2)
				{
					_scratch.Add(item.Key);
				}
			}
			for (int i = 0; i < _scratch.Count; i++)
			{
				PanelRows.TrackRow trackRow = _bound[_scratch[i]];
				PanelRows.UnbindTrackRow(trackRow);
				if (trackRow.Root != null)
				{
					trackRow.Root.SetActive(value: false);
					_free.Push(trackRow);
				}
				_bound.Remove(_scratch[i]);
			}
		}
		for (int j = firstIndex; j < firstIndex + count2; j++)
		{
			bool flag3 = currentId != 0L && _src.IdAt(j) == currentId;
			if (_bound.TryGetValue(j, out var value))
			{
				if (value.IsHighlighted != flag3)
				{
					PanelRows.ApplyTrackRowHighlight(value, flag3);
				}
			}
			else
			{
				value = TakeRow();
				_bound[j] = value;
				_src.Bind(value, j, flag3);
				Place(value, j);
			}
		}
	}

	public void RefreshNowPlaying()
	{
		if (_src == null)
		{
			return;
		}
		long currentId = _src.CurrentId;
		if (currentId == _lastNowPlayingId)
		{
			return;
		}
		_lastNowPlayingId = currentId;
		int count = _src.Count;
		foreach (KeyValuePair<int, PanelRows.TrackRow> item in _bound)
		{
			int key = item.Key;
			if (key >= 0 && key < count)
			{
				bool flag = currentId != 0L && _src.IdAt(key) == currentId;
				if (item.Value.IsHighlighted != flag)
				{
					PanelRows.ApplyTrackRowHighlight(item.Value, flag);
				}
			}
		}
	}

	private void Place(PanelRows.TrackRow row, int index)
	{
		RectTransform rect = row.Rect;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = new Vector2(_indent, rect.offsetMin.y);
		rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
		rect.sizeDelta = new Vector2(rect.sizeDelta.x, _rowHeight);
		rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f - VirtualWindow.OffsetOf(index, _pitch));
	}

	private PanelRows.TrackRow TakeRow()
	{
		while (_free.Count > 0)
		{
			PanelRows.TrackRow trackRow = _free.Pop();
			if (!(trackRow.Root == null))
			{
				trackRow.Root.SetActive(value: true);
				return trackRow;
			}
		}
		PanelRows.TrackRow trackRow2 = PanelRows.CreateTrackRow(_segment, _rowHeight, 0f, Activate);
		LayoutElement component = trackRow2.Root.GetComponent<LayoutElement>();
		if (component != null)
		{
			component.ignoreLayout = true;
		}
		_pool.Add(trackRow2);
		return trackRow2;
	}

	private void Activate(int absoluteIndex)
	{
		if (_src != null && absoluteIndex >= 0 && absoluteIndex < _src.Count)
		{
			_src.Activate(absoluteIndex);
		}
	}

	private void ReleaseAll()
	{
		if (_bound.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<int, PanelRows.TrackRow> item in _bound)
		{
			PanelRows.UnbindTrackRow(item.Value);
			if (item.Value.Root != null)
			{
				item.Value.Root.SetActive(value: false);
				_free.Push(item.Value);
			}
		}
		_bound.Clear();
		_lastFirst = -1;
		_lastCount = -1;
	}

	private bool MeasureViewport(out float scrolledPast, out float viewHeight)
	{
		scrolledPast = 0f;
		viewHeight = 0f;
		if (_viewport == null || _segment == null)
		{
			return false;
		}
		float y = _segment.lossyScale.y;
		if (y <= 0.0001f)
		{
			return false;
		}
		_segment.GetWorldCorners(_segCorners);
		_viewport.GetWorldCorners(_vpCorners);
		float y2 = _segCorners[1].y;
		float y3 = _vpCorners[1].y;
		float y4 = _vpCorners[0].y;
		viewHeight = (y3 - y4) / y;
		if (viewHeight <= 1f)
		{
			return false;
		}
		scrolledPast = (y2 - y3) / y;
		return true;
	}

	public void Dispose()
	{
		ReleaseAll();
		_free.Clear();
		_pool.Clear();
		if (_segment != null)
		{
			Object.Destroy(_segment.gameObject);
		}
	}
}
