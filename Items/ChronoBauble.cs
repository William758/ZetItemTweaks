using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class ChronoBauble
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "ChronoBauble";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> AtkSlow { get; set; }
		public static ConfigEntry<float> BaseDuration { get; set; }
		public static ConfigEntry<float> StackDuration { get; set; }


		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnLateSetup += LateSetup;
			}
		}

		private static void SetupConfig()
		{
			EnableChanges = ConfigEntry(
				itemIdentifier, "EnableChanges", 1,
				SectionEnableDesc
			);
			if (GenerateOverrideText.Value)
			{
				OverrideText = ConfigEntry(
					itemIdentifier, "OverrideText", true,
					"Replace item description text."
				);
			}
			AtkSlow = ConfigEntry(
				itemIdentifier, "AtkSlow", 0.2f,
				"Attack speed reduction of slow debuff."
			);
			AtkSlow.Value = Mathf.Min(0.9f, Mathf.Abs(AtkSlow.Value));
			BaseDuration = ConfigEntry(
				itemIdentifier, "BaseDuration", 2f,
				"Duration of slow debuff."
			);
			StackDuration = ConfigEntry(
				itemIdentifier, "StackDuration", 2f,
				"Duration of slow debuff per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			SlowDurationHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default"; 
				
				RegisterFragment("SLOW_MOVE", "Attacks <style=cIsUtility>slow</style> enemies for <style=cIsUtility>-60% movement speed</style> for {0}.");
				RegisterFragment("SLOW_BOTH", "Attacks <style=cIsUtility>slow</style> enemies for <style=cIsUtility>-60% movement speed</style> and <style=cIsUtility>-{0} attack speed</style> for {1}.");
				RegisterToken("ITEM_SLOWONHIT_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("SLOW_MOVE", "Ataques causam <style=cIsUtility>lentidão</style> nos inimigos em <style=cIsUtility>-60% de velocidade de movimento</style> por {0}.");
				RegisterFragment("SLOW_BOTH", "Ataques causam <style=cIsUtility>lentidão</style> nos inimigos em <style=cIsUtility>-60% de velocidade de movimento</style> e <style=cIsUtility>-{0} de velocidade de ataque</style> por {1}.");
				RegisterToken("ITEM_SLOWONHIT_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";
			if (AtkSlow.Value > 0f)
			{
				output += String.Format(
					TextFragment("SLOW_BOTH"),
					ScalingText(AtkSlow.Value, "percent", "cIsUtility"),
					ScalingText(BaseDuration.Value, StackDuration.Value, "duration", "cIsUtility")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("SLOW_MOVE"),
					ScalingText(BaseDuration.Value, StackDuration.Value, "duration", "cIsUtility")
				);
			}

			return output;
		}



		private static void SlowDurationHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField("Slow60")),
					x => x.MatchLdcR4(2f),
					x => x.MatchLdloc(10),
					x => x.MatchConvR4(),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 5;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 10);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return Mathf.Max(1f, BaseDuration.Value + StackDuration.Value * (count - 1));
					});
				}
				else
				{
					Debug.LogWarning(itemIdentifier + " :: SlowDurationHook Failed!");
				}
			};
		}
	}
}
