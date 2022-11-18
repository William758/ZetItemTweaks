using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class PersonalShieldGenerator
	{
		public static List<string> autoCompatList = new List<string> { "com.Ben.BenBalanceMod" };

		public static string itemIdentifier = "PersonalShieldGenerator";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHealthAsShield { get; set; }
		public static ConfigEntry<float> StackHealthAsShield { get; set; }



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
			BaseHealthAsShield = ConfigEntry(
				itemIdentifier, "BaseHealthAsShield", 0.08f,
				"Health gained as shield from item."
			);
			StackHealthAsShield = ConfigEntry(
				itemIdentifier, "StackHealthAsShield", 0.08f,
				"Health gained as shield from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterToken("ITEM_PERSONALSHIELD_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterToken("ITEM_PERSONALSHIELD_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("STAT_HEALTH_EXTRA_SHIELD", true),
				ScalingText(BaseHealthAsShield.Value, StackHealthAsShield.Value, "percent", "cIsHealing")
			);

			return output;
		}
	}
}
