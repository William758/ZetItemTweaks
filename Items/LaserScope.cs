using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
    public static class LaserScope
    {
		public static List<string> autoCompatList = new List<string> { "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "LaserScope";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }
		public static ConfigEntry<float> BaseCritMult { get; set; }
		public static ConfigEntry<float> StackCritMult { get; set; }



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
			BaseCritMult = ConfigEntry(
				itemIdentifier, "BaseCritMult", 1f,
				"Critical strike multiplier gained from item."
			);
			StackCritMult = ConfigEntry(
				itemIdentifier, "StackCritMult", 1f,
				"Critical strike multiplier gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("SCOPE_PICKUP", "Critical strikes deal additional damage.");
				RegisterToken("ITEM_CRITDAMAGE_DESC", DescriptionText());
				RegisterToken("ITEM_CRITDAMAGE_PICKUP", TextFragment("SCOPE_PICKUP"));

				targetLanguage = "pt-BR";

				RegisterFragment("SCOPE_PICKUP", "Acertos críticos causam dano adicional.");
				RegisterToken("ITEM_CRITDAMAGE_DESC", DescriptionText());
				RegisterToken("ITEM_CRITDAMAGE_PICKUP", TextFragment("SCOPE_PICKUP"));

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
			if (BaseCritMult.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_CRITMULT", true),
					ScalingText(BaseCritMult.Value, StackCritMult.Value, "percent", "cIsDamage")
				);
			}

			return output;
		}
	}
}
