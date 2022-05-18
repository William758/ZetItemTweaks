using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class Aegis
	{
		public static List<string> autoCompatList = new List<string> { "com.kking117.FlatItemBuff", "com.Ben.BenBalanceMod" };

		public static string itemIdentifier = "Aegis";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseOverheal { get; set; }
		public static ConfigEntry<float> StackOverheal { get; set; }
		public static ConfigEntry<float> BaseArmor { get; set; }
		public static ConfigEntry<float> StackArmor { get; set; }
		public static ConfigEntry<float> BaseBarrierArmor { get; set; }
		public static ConfigEntry<float> StackBarrierArmor { get; set; }



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
			BaseOverheal = ConfigEntry(
				itemIdentifier, "BaseOverheal", 0.5f,
				"Overheal barrier gained from item."
			);
			StackOverheal = ConfigEntry(
				itemIdentifier, "StackOverheal", 0.5f,
				"Overheal barrier gained from item per stack."
			);
			BaseArmor = ConfigEntry(
				itemIdentifier, "BaseArmor", 30f,
				"Armor gained from item."
			);
			StackArmor = ConfigEntry(
				itemIdentifier, "StackArmor", 15f,
				"Armor gained from item per stack."
			);
			BaseBarrierArmor = ConfigEntry(
				itemIdentifier, "BaseBarrierArmor", 0f,
				"Armor gained while barrier is active from item."
			);
			StackBarrierArmor = ConfigEntry(
				itemIdentifier, "StackBarrierArmor", 0f,
				"Armor gained while barrier is active from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			OverhealHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("OVERHEAL_BARRIER", "Excess healing generates <style=cIsHealing>barrier</style> for {0} of the amount you <style=cIsHealing>healed</style>.");
				RegisterFragment("BARRIER_ARMOR", "\nIncreases <style=cIsHealing>armor</style> by {0} while <style=cIsHealing>barrier</style> is active.");
				RegisterToken("ITEM_BARRIERONOVERHEAL_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("OVERHEAL_BARRIER"),
				ScalingText(BaseOverheal.Value, StackOverheal.Value, "percent", "cIsHealing")
			);
			if (BaseArmor.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_ARMOR"),
					ScalingText(BaseArmor.Value, StackArmor.Value, "flat", "cIsHealing")
				);
			}
			if (BaseBarrierArmor.Value > 0f)
			{
				output += String.Format(
					TextFragment("BARRIER_ARMOR"),
					ScalingText(BaseBarrierArmor.Value, StackBarrierArmor.Value, "flat", "cIsHealing")
				);
			}

			return output;
		}



		private static void OverhealHook()
		{
			IL.RoR2.HealthComponent.Heal += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdflda("RoR2.HealthComponent", "itemCounts"),
					x => x.MatchLdfld("RoR2.HealthComponent/ItemCounts", "barrierOnOverHeal"),
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(0.5f),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 5;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, float>>((healthComponent) =>
					{
						int count = healthComponent.itemCounts.barrierOnOverHeal;

						return BaseOverheal.Value + StackOverheal.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: OverhealHook Failed!");
				}
			};
		}
	}
}
