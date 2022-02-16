using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Ranching;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Ranching : BaseUnityPlugin
{
	private const string ModName = "Ranching";
	private const string ModVersion = "1.0.0";
	private const string ModGUID = "org.bepinex.plugins.ranching";

	private static readonly Skill ranching = new("Ranching", "ranching.png");

	public void Awake()
	{
		ranching.Description.English("Reduces the time required to tame animals and increases item yield of tamed animals.");
		ranching.Name.German("Viehhaltung");
		ranching.Description.German("Reduziert die Zeit, die benötigt wird, um ein Tier zu zähmen und erhöht die Ausbeute von gezähmten Tieren.");
		ranching.Configurable = true;

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	public class PlayerAwake
	{
		private static void Postfix(Player __instance)
		{
			__instance.m_nview.Register("Ranching IncreaseSkill", (long _, int factor) => __instance.RaiseSkill("Ranching", factor));
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	public class PlayerUpdate
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				__instance.m_nview.GetZDO().Set("Ranching Skill", __instance.GetSkillFactor(Skill.fromName("Ranching")));
			}
		}
	}

	[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
	public class TamedCreatureDied
	{
		private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
		{
			if (__instance.m_character.IsTamed())
			{
				if (Player.GetClosestPlayer(__instance.transform.position, 50f) is { } closestPlayer)
				{
					if (Random.Range(0f, 1f) < closestPlayer.m_nview.GetZDO().GetFloat("Ranching Skill") * ranching.SkillEffectFactor)
					{
						for (int i = 0; i < __result.Count; ++i)
						{
							__result[i] = new KeyValuePair<GameObject, int>(__result[i].Key, __result[i].Value * 2);
						}
					}

					closestPlayer.m_nview.InvokeRPC("Ranching IncreaseSkill", 35);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Tameable), nameof(Tameable.DecreaseRemainingTime))]
	public class TameFaster
	{
		private static void Prefix(Tameable __instance, ref float time)
		{
			if (Player.GetClosestPlayer(__instance.transform.position, 10f) is { } closestPlayer)
			{
				time *= 1 + closestPlayer.m_nview.GetZDO().GetFloat("Ranching Skill") * ranching.SkillEffectFactor * 2;
				if (Random.Range(0, 10) == 0)
				{
					closestPlayer.m_nview.InvokeRPC("Ranching IncreaseSkill", 5);
				}
			}
		}
	}

	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class SetTamingFlag
	{
		public static bool gettingTamed = false;

		private static void Prefix(MonsterAI __instance)
		{
			if (__instance.m_character.GetComponent<Tameable>()?.GetTameness() > 0)
			{
				gettingTamed = true;
			}
		}

		private static void Finalizer() => gettingTamed = false;
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetStealthFactor))]
	public static class DoNotAlert
	{
		private static bool Prefix(Player __instance, ref float __result)
		{
			if (SetTamingFlag.gettingTamed && __instance.m_nview.GetZDO().GetFloat("Ranching Skill") >= 0.2f)
			{
				__result = 0f;
				return false;
			}
			return true;
		}
	}
}
