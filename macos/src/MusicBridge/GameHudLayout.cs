using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicBridge;

internal static class GameHudLayout
{
	private static RectTransform _leftIcons;

	private static RectTransform _centerIcons;

	private static Vector2 _originalAnchorMin;

	private static Vector2 _originalAnchorMax;

	private static Vector2 _originalPos;

	private static bool _captured;

	private static bool _applied;

	private static float _lastScreenWidth;

	public static void Attach(Transform anyChildOfMostFrontArea)
	{
		if (_captured)
		{
			return;
		}
		try
		{
			Transform transform = anyChildOfMostFrontArea;
			while (transform != null && transform.name != "MostFrontArea")
			{
				transform = transform.parent;
			}
			if (transform == null)
			{
				BridgeLog.Info("[HUD] 未找到 MostFrontArea，跳过图标重排。");
				return;
			}
			_leftIcons = transform.Find("LeftIcons") as RectTransform;
			_centerIcons = transform.Find("CenterIcons") as RectTransform;
			if (_leftIcons == null || _centerIcons == null)
			{
				BridgeLog.Info("[HUD] 未找到 LeftIcons/CenterIcons，跳过图标重排。");
				return;
			}
			_originalAnchorMin = _leftIcons.anchorMin;
			_originalAnchorMax = _leftIcons.anchorMax;
			_originalPos = _leftIcons.anchoredPosition;
			_captured = true;
			string[] obj = new string[5] { "[HUD] 已记录 LeftIcons 原始锚点 ", null, null, null, null };
			Vector2 originalAnchorMin = _originalAnchorMin;
			obj[1] = originalAnchorMin.ToString();
			obj[2] = " 位置 ";
			originalAnchorMin = _originalPos;
			obj[3] = originalAnchorMin.ToString();
			obj[4] = "（可还原）。";
			BridgeLog.Info(string.Concat(obj));
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[HUD] 初始化失败：" + ex.Message);
		}
	}

	public static void Tick()
	{
		if (_captured && !(_leftIcons == null) && !(_centerIcons == null) && (!_applied || !(Mathf.Abs((float)Screen.width - _lastScreenWidth) < 1f)))
		{
			_lastScreenWidth = Screen.width;
			Apply();
		}
	}

	private static void Apply()
	{
		try
		{
			List<RectTransform> list = ActiveChildren(_leftIcons);
			List<RectTransform> list2 = ActiveChildren(_centerIcons);
			if (list.Count == 0 || list2.Count < 2)
			{
				BridgeLog.Info("[HUD] 图标数量不足（左 " + list.Count + " 中 " + list2.Count + "），跳过重排。");
				_applied = true;
				return;
			}
			list2.Sort((RectTransform a, RectTransform b) => a.position.x.CompareTo(b.position.x));
			float num = Mathf.Abs(list2[1].position.x - list2[0].position.x);
			if (num < 1f)
			{
				BridgeLog.Info("[HUD] 间距异常，跳过重排。");
				_applied = true;
				return;
			}
			_leftIcons.anchorMin = _centerIcons.anchorMin;
			_leftIcons.anchorMax = _centerIcons.anchorMax;
			list.Sort((RectTransform a, RectTransform b) => a.position.x.CompareTo(b.position.x));
			float x = list[list.Count - 1].position.x;
			float x2 = list2[0].position.x - x - num;
			Vector3 vector = _leftIcons.parent.InverseTransformPoint(new Vector3(x2, 0f, 0f));
			Vector3 vector2 = _leftIcons.parent.InverseTransformPoint(Vector3.zero);
			float num2 = vector.x - vector2.x;
			_leftIcons.anchoredPosition = new Vector2(_leftIcons.anchoredPosition.x + num2, _leftIcons.anchoredPosition.y);
			_applied = true;
			BridgeLog.Info("[HUD] 左下图标已并入同一排：统一间距 " + num.ToString("0.0") + "，位移 " + num2.ToString("0.0") + "，屏幕宽 " + Screen.width);
		}
		catch (Exception ex)
		{
			BridgeLog.Warn("[HUD] 重排失败：" + ex.Message);
		}
	}

	private static List<RectTransform> ActiveChildren(RectTransform parent)
	{
		List<RectTransform> list = new List<RectTransform>();
		for (int i = 0; i < parent.childCount; i++)
		{
			RectTransform rectTransform = parent.GetChild(i) as RectTransform;
			if (rectTransform != null && rectTransform.gameObject.activeInHierarchy)
			{
				list.Add(rectTransform);
			}
		}
		return list;
	}

	public static void Restore()
	{
		if (_captured && !(_leftIcons == null))
		{
			_leftIcons.anchorMin = _originalAnchorMin;
			_leftIcons.anchorMax = _originalAnchorMax;
			_leftIcons.anchoredPosition = _originalPos;
			_applied = false;
			BridgeLog.Info("[HUD] 左下图标已还原为游戏原始布局。");
		}
	}
}
