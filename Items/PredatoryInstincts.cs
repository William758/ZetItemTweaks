using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class PredatoryInstincts
	{
		public static List<string> autoCompatList = new List<string> { "Hayaku.VanillaRebalance", "com.NetherCrowCSOLYOO.ClassicCritAdd" };

		public static string itemIdentifier = "PredatoryInstincts";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }
		public static ConfigEntry<int> BaseBuffCount { get; set; }
		public static ConfigEntry<int> StackBuffCount { get; set; }
		public static ConfigEntry<float> BuffAtkSpd { get; set; }



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
			BaseBuffCount = ConfigEntry(
				itemIdentifier, "BaseBuffCount", 3,
				"Maximum buff count gained from item."
			);
			StackBuffCount = ConfigEntry(
				itemIdentifier, "StackBuffCount", 2,
				"Maximum buff count gained from item per stack."
			);
			BuffAtkSpd = ConfigEntry(
				itemIdentifier, "BuffAtkSpd", 0.1f,
				"Attack speed gained per buff stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			BuffCountHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("ATKSPD_ON_CRIT", "\n<style=cIsDamage>Critical strikes</style> increase <style=cIsDamage>attack speed</style> by {0}.");
				RegisterFragment("PREDATOR_CAP", "\nMaximum cap of {0} <style=cIsDamage>attack speed</style>.");
				RegisterFragment("PREDATOR_PICKUP", "Critical strikes increase attack speed.");
				RegisterToken("ITEM_ATTACKSPEEDONCRIT_DESC", DescriptionText());
				RegisterToken("ITEM_ATTACKSPEEDONCRIT_PICKUP", TextFragment("PREDATOR_PICKUP"));

				targetLanguage = "pt-BR";

				RegisterFragment("ATKSPD_ON_CRIT", "\n<style=cIsDamage>Acertos Críticos</style> aumentam a <style=cIsDamage>velocidade de ataque</style> em {0}.");
				RegisterFragment("PREDATOR_CAP", "\nLimite máximo de {0} de <style=cIsDamage>velocidade de ataque</style>.");
				RegisterFragment("PREDATOR_PICKUP", "Acertos Críticos aumentam velocidade de ataque.");
				RegisterToken("ITEM_ATTACKSPEEDONCRIT_DESC", DescriptionText());
				RegisterToken("ITEM_ATTACKSPEEDONCRIT_PICKUP", TextFragment("PREDATOR_PICKUP"));

				targetLanguage = "";
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
				TextFragment("ATKSPD_ON_CRIT", true),
				ScalingText(BuffAtkSpd.Value, "percent", "cIsDamage")
			);
			output += String.Format(
				TextFragment("PREDATOR_CAP"),
				ScalingText(BuffAtkSpd.Value * BaseBuffCount.Value, BuffAtkSpd.Value * StackBuffCount.Value, "percent", "cIsDamage")
			);

			return output;
		}



		private static void BuffCountHook()
		{
			IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(2)
				);

				if (found)
				{
					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 1);
					c.EmitDelegate<Func<int, int>>((count) =>
					{
						if (count == 0) return 1;
						return BaseBuffCount.Value + StackBuffCount.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: BuffCountHook Failed!");
				}
			};
		}
	}
}
