using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class TitanicKnurl
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "TitanicKnurl";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHealth { get; set; }
		public static ConfigEntry<float> StackHealth { get; set; }
		public static ConfigEntry<float> BaseHealthPercent { get; set; }
		public static ConfigEntry<float> StackHealthPercent { get; set; }
		public static ConfigEntry<float> BaseArmor { get; set; }
		public static ConfigEntry<float> StackArmor { get; set; }
		public static ConfigEntry<float> BaseRegenFraction { get; set; }
		public static ConfigEntry<float> StackRegenFraction { get; set; }
		public static ConfigEntry<float> BaseRegen { get; set; }
		public static ConfigEntry<float> StackRegen { get; set; }



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
			BaseHealth = ConfigEntry(
				itemIdentifier, "BaseHealth", 100f,
				"Health gained from item."
			);
			StackHealth = ConfigEntry(
				itemIdentifier, "StackHealth", 50f,
				"Health gained from item per stack."
			);
			BaseHealthPercent = ConfigEntry(
				itemIdentifier, "BaseHealthPercent", 0f,
				"Health percent gained from item."
			);
			StackHealthPercent = ConfigEntry(
				itemIdentifier, "StackHealthPercent", 0f,
				"Health percent gained from item per stack."
			);
			BaseArmor = ConfigEntry(
				itemIdentifier, "BaseArmor", 0f,
				"Armor gained from item."
			);
			StackArmor = ConfigEntry(
				itemIdentifier, "StackArmor", 0f,
				"Armor gained from item per stack."
			);
			BaseRegenFraction = ConfigEntry(
				itemIdentifier, "BaseRegenFraction", 0f,
				"Health percent regeneration gained from item. 0.01 = 1% regeneration."
			);
			StackRegenFraction = ConfigEntry(
				itemIdentifier, "StackRegenFraction", 0f,
				"Health percent regeneration gained from item per stack."
			);
			BaseRegen = ConfigEntry(
				itemIdentifier, "BaseRegen", 1.6f,
				"Health regeneration gained from item."
			);
			StackRegen = ConfigEntry(
				itemIdentifier, "StackRegen", 1.6f,
				"Health regeneration gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterToken("ITEM_KNURL_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";
			if (BaseHealth.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_HEALTH", true),
					ScalingText(BaseHealth.Value, StackHealth.Value, "flat", "cIsHealing")
				);
			}
			if (BaseHealthPercent.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_HEALTH", true),
					ScalingText(BaseHealthPercent.Value, StackHealthPercent.Value, "percent", "cIsHealing")
				);
			}
			if (BaseArmor.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_ARMOR"),
					ScalingText(BaseArmor.Value, StackArmor.Value, "flat", "cIsHealing")
				);
			}
			if (BaseRegenFraction.Value > 0)
			{
				output += String.Format(
					TextFragment("STAT_REGENERATION"),
					ScalingText(BaseRegenFraction.Value, StackRegenFraction.Value, "percentregen", "cIsHealing")
				);
			}
			if (BaseRegen.Value > 0)
			{
				output += String.Format(
					TextFragment("STAT_REGENERATION"),
					ScalingText(BaseRegen.Value, StackRegen.Value, "flatregen", "cIsHealing")
				);
			}

			return output;
		}
	}
}
