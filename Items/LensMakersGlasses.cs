using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class LensMakersGlasses
	{
		public static List<string> autoCompatList = new List<string> { "com.NetherCrowCSOLYOO.ClassicCritAdd" };

		public static string itemIdentifier = "LensMakersGlasses";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }



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
				itemIdentifier, "BaseCrit", 10f,
				"Critical strike chance gained from item."
			);
			StackCrit = ConfigEntry(
				itemIdentifier, "StackCrit", 10f,
				"Critical strike chance gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("GLASSES_PICKUP", "Increases critical strikes chance.");
				RegisterToken("ITEM_CRITGLASSES_DESC", DescriptionText());
				RegisterToken("ITEM_CRITGLASSES_PICKUP", TextFragment("GLASSES_PICKUP"));

				targetLanguage = "pt-BR";

				RegisterFragment("GLASSES_PICKUP", "Aumenta a chance de acertos críticos.");
				RegisterToken("ITEM_CRITGLASSES_DESC", DescriptionText());
				RegisterToken("ITEM_CRITGLASSES_PICKUP", TextFragment("GLASSES_PICKUP"));

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("STAT_CRIT", true),
				ScalingText(BaseCrit.Value, StackCrit.Value, "chance", "cIsDamage")
			);

			return output;
		}
	}
}
