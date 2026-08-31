using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace MusicBridge;

internal static class LocalImportLimit
{
	private static readonly Dictionary<string, int> ExpectedHits = new Dictionary<string, int>();

	private static readonly List<string> Applied = new List<string>();

	private static bool _shapeMismatch;

	public static bool Patched { get; private set; }

	public static void Install(Harmony harmony)
	{
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		try
		{
			Type type = AccessTools.TypeByName("MusicService") ?? AccessTools.TypeByName("Bulbul.MusicService");
			Type type2 = AccessTools.TypeByName("Bulbul.FacilityMusic") ?? AccessTools.TypeByName("FacilityMusic");
			if (type == null || type2 == null)
			{
				BridgeLog.Warn("解除导入上限：找不到 MusicService / FacilityMusic，跳过。");
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(type, "AddLocalMusicItem", (Type[])null, (Type[])null);
			MethodInfo methodInfo2 = FindStateMachineMoveNext(type2, "ImportLocalMusicAsync");
			if (methodInfo == null || methodInfo2 == null)
			{
				BridgeLog.Warn("解除导入上限：目标方法或异步状态机定位失败，跳过。");
				return;
			}
			ExpectedHits[Key(methodInfo)] = 1;
			ExpectedHits[Key(methodInfo2)] = 2;
			HarmonyMethod val = new HarmonyMethod(AccessTools.Method(typeof(LocalImportLimit), "Transpile", (Type[])null, (Type[])null));
			harmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, (HarmonyMethod)null, val, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)methodInfo2, (HarmonyMethod)null, (HarmonyMethod)null, val, (HarmonyMethod)null, (HarmonyMethod)null);
			if (_shapeMismatch || Applied.Count != 2)
			{
				BridgeLog.Warn("解除导入上限：IL 形态与实测不符，已放弃改写（导入上限保持 " + 100 + " 首）。已改写方法数=" + Applied.Count);
				Patched = false;
			}
			else
			{
				Patched = true;
				BridgeLog.Info("解除导入上限：三处计数判据已改写（" + string.Join("、", Applied.ToArray()) + "）。");
			}
		}
		catch (Exception ex)
		{
			Patched = false;
			BridgeLog.Error("解除导入上限安装失败：" + ex);
		}
	}

	internal static MethodInfo FindStateMachineMoveNext(Type owner, string methodName)
	{
		MethodInfo methodInfo = AccessTools.Method(owner, methodName, (Type[])null, (Type[])null);
		if (methodInfo == null)
		{
			return null;
		}
		AsyncStateMachineAttribute customAttribute = methodInfo.GetCustomAttribute<AsyncStateMachineAttribute>();
		if (customAttribute == null || customAttribute.StateMachineType == null)
		{
			return null;
		}
		return AccessTools.Method(customAttribute.StateMachineType, "MoveNext", (Type[])null, (Type[])null);
	}

	private static string Key(MethodBase m)
	{
		return m.DeclaringType.FullName + "::" + m.Name;
	}

	private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		List<CodeInstruction> list = new List<CodeInstruction>(instructions);
		string text = Key(__originalMethod);
		if (!ExpectedHits.TryGetValue(text, out var value))
		{
			_shapeMismatch = true;
			return list;
		}
		List<int> list2 = new List<int>();
		for (int i = 1; i < list.Count; i++)
		{
			if (IsLoad100(list[i]) && IsCountCall(list[i - 1]) && ShapeMatches(list, i))
			{
				list2.Add(i);
			}
		}
		if (list2.Count != value)
		{
			_shapeMismatch = true;
			BridgeLog.Warn("解除导入上限：" + text + " 期望命中 " + value + " 处，实际 " + list2.Count + " 处，放弃改写。");
			return list;
		}
		MethodInfo operand = AccessTools.PropertyGetter(typeof(LocalImportPolicy), "ComparisonLimit");
		foreach (int item in list2)
		{
			list[item].opcode = OpCodes.Call;
			list[item].operand = operand;
		}
		Applied.Add(__originalMethod.Name);
		return list;
	}

	private static bool IsLoad100(CodeInstruction ins)
	{
		if (ins.opcode != OpCodes.Ldc_I4_S && ins.opcode != OpCodes.Ldc_I4)
		{
			return false;
		}
		if (ins.operand == null)
		{
			return false;
		}
		try
		{
			return Convert.ToInt32(ins.operand) == 100;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsCountCall(CodeInstruction ins)
	{
		if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt)
		{
			return false;
		}
		MethodInfo methodInfo = ins.operand as MethodInfo;
		if (methodInfo != null)
		{
			return methodInfo.Name == "Count";
		}
		return false;
	}

	private static bool ShapeMatches(List<CodeInstruction> list, int i)
	{
		if (i + 1 < list.Count && (list[i + 1].opcode == OpCodes.Bge || list[i + 1].opcode == OpCodes.Bge_S))
		{
			return true;
		}
		if (i + 3 < list.Count && list[i + 1].opcode == OpCodes.Clt && list[i + 2].opcode == OpCodes.Ldc_I4_0 && list[i + 3].opcode == OpCodes.Ceq)
		{
			return true;
		}
		return false;
	}
}
