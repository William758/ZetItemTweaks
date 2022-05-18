using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class HarvestersScythe
	{
		public static List<string> autoCompatList = new List<string> { "Hayaku.VanillaRebalance", "com.Ben.BenBalanceMod", "com.NetherCrowCSOLYOO.ClassicCritAdd" };

		public static string itemIdentifier = "HarvestersScythe";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }
		public static ConfigEntry<float> BaseCritHeal { get; set; }
		public static ConfigEntry<float> StackCritHeal { get; set; }



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
			BaseCrit = ConfigEntry(
				itemIdentifier, "BaseCrit", 5f,
				"Critical strike chance gained from item."
			);
			StackCrit = ConfigEntry(
				itemIdentifier, "StackCrit", 0f,
				"Critical strike chance gained from item per stack."
			);
			BaseCritHeal = ConfigEntry(
				itemIdentifier, "BaseCritHeal", 8f,
				"Heal on critical strike gained from item."
			);
			StackCritHeal = ConfigEntry(
				itemIdentifier, "StackCritHeal", 4f,
				"Heal on critical strike gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			HealHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("HEAL_ON_CRIT", "\n<style=cIsDamage>Critical strikes</style> <style=cIsHealing>heal</style> for {0} <style=cIsHealing>health</style>.");
				RegisterToken("ITEM_HEALONCRIT_DESC", DescriptionText());
				RegisterToken("ITEM_HEALONCRIT_PICKUP", "Critical strikes heal you.");
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseCrit.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_CRIT", true),
					ScalingText(BaseCrit.Value, StackCrit.Value, "chance", "cIsDamage")
				);
			}

			if (output != "") output += "\n";

			output += String.Format(
				TextFragment("HEAL_ON_CRIT", true),
				ScalingText(BaseCritHeal.Value, StackCritHeal.Value, "flat", "cIsHealing")
			);

			return output;
		}



		private static void HealHook()
		{
			IL.RoR2.GlobalEventManager.OnCrit += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(4f),
					x => x.MatchLdloc(4),
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(4f),
					x => x.MatchMul(),
					x => x.MatchAdd(),
					x => x.MatchLdarg(4),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 8;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 4);
					c.Emit(OpCodes.Ldarg, 4);
					c.EmitDelegate<Func<int, float, float>>((count, procCoeff) =>
					{
						return (BaseCritHeal.Value + StackCritHeal.Value * (count - 1)) * procCoeff;
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: HealHook Failed!");
				}
			};
		}
	}
}
