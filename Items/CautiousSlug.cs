using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class CautiousSlug
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod" };

		public static string itemIdentifier = "CautiousSlug";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseSafeRegen { get; set; }
		public static ConfigEntry<float> StackSafeRegen { get; set; }
		public static ConfigEntry<float> BaseSafeRegenFraction { get; set; }
		public static ConfigEntry<float> StackSafeRegenFraction { get; set; }



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
			BaseSafeRegen = ConfigEntry(
				itemIdentifier, "BaseSafeRegen", 3.2f,
				"Health regeneration gained while safe from item."
			);
			StackSafeRegen = ConfigEntry(
				itemIdentifier, "StackSafeRegen", 3.2f,
				"Health regeneration gained while safe from item per stack."
			);
			BaseSafeRegenFraction = ConfigEntry(
				itemIdentifier, "BaseSafeRegenFraction", 0f,
				"Health regeneration fraction gained while safe from item. 0.01 = 1% regeneration."
			);
			StackSafeRegenFraction = ConfigEntry(
				itemIdentifier, "StackSafeRegenFraction", 0f,
				"Health regeneration fraction gained while safe from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default"; 
				
				RegisterFragment("SAFE_REGENERATION", "\nIncreases <style=cIsHealing>health regeneration</style> by {0} while out of danger.");
				RegisterToken("ITEM_HEALWHILESAFE_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("SAFE_REGENERATION", "\nAumenta a <style=cIsHealing>regeneração de saúde</style> em {0} enquanto fora de perigo.");
				RegisterToken("ITEM_HEALWHILESAFE_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseSafeRegenFraction.Value > 0f)
			{
				output += String.Format(
					TextFragment("SAFE_REGENERATION", true),
					ScalingText(BaseSafeRegenFraction.Value, StackSafeRegenFraction.Value, "percentregen", "cIsHealing")
				);
			}
			if (BaseSafeRegen.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("SAFE_REGENERATION", true),
					ScalingText(BaseSafeRegen.Value, StackSafeRegen.Value, "flatregen", "cIsHealing")
				);
			}

			if (output == "") output += TextFragment("CFG_NO_EFFECT");

			return output;
		}
	}
}
