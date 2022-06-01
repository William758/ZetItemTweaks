using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class AlienHead
	{
		internal static Sprite PickupIcon;

		public static List<string> autoCompatList = new List<string> { "com.Borbo.GreenAlienHead" };

		public static string itemIdentifier = "AlienHead";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCooldown { get; set; }
		public static ConfigEntry<float> StackCooldown { get; set; }
		public static ConfigEntry<float> BaseFlatCooldown { get; set; }
		public static ConfigEntry<float> StackFlatCooldown { get; set; }
		public static ConfigEntry<bool> GreenTier { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnItemCatalogPreInit += ModifyItem;
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
			BaseCooldown = ConfigEntry(
				itemIdentifier, "BaseCooldown", 0.1f,
				"Cooldown reduction gained from item."
			);
			StackCooldown = ConfigEntry(
				itemIdentifier, "StackCooldown", 0.1f,
				"Cooldown reduction gained from item per stack."
			);
			BaseFlatCooldown = ConfigEntry(
				itemIdentifier, "BaseFlatCooldown", 0f,
				"Cooldown deduction gained from item."
			);
			StackFlatCooldown = ConfigEntry(
				itemIdentifier, "StackFlatCooldown", 0f,
				"Cooldown deduction gained from item per stack."
			);
			GreenTier = ConfigEntry(
				itemIdentifier, "GreenTier", true,
				"Change item to green tier."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			bool modified = false;

			ItemDef itemDef = RoR2Content.Items.AlienHead;

			if (GreenTier.Value)
			{
				itemDef._itemTierDef = LegacyResourcesAPI.Load<ItemTierDef>("ItemTierDefs/Tier2Def");
				if (PickupIcon) itemDef.pickupIconSprite = PickupIcon;

				modified = true;
			}

			if (modified)
			{
				ModifyCount++;
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("COOLDOWN_DEDUCT", "\nReduces <style=cIsUtility>skill cooldowns</style> by {0} seconds.");
				RegisterToken("ITEM_ALIENHEAD_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseCooldown.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_COOLDOWN", true),
					ScalingText(BaseCooldown.Value, StackCooldown.Value, "percent", "cIsUtility")
				);
			}
			if (BaseFlatCooldown.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("COOLDOWN_DEDUCT", true),
					ScalingText(BaseFlatCooldown.Value, StackFlatCooldown.Value, "flat", "cIsUtility")
				);
			}

			return output;
		}
	}
}
