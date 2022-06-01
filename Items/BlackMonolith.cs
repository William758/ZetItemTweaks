using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class BlackMonolith
	{
		internal static Sprite PickupIcon;

		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "BlackMonolith";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> RedTier { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnItemCatalogPreInit += ModifyItem;
			}
		}

		private static void SetupConfig()
		{
			EnableChanges = ConfigEntry(
				itemIdentifier, "EnableChanges", 1,
				SectionEnableDesc
			);
			RedTier = ConfigEntry(
				itemIdentifier, "RedTier", false,
				"Change item to red tier."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			bool modified = false;

			ItemDef itemDef = FindItemDefPreCatalogInit("MysticsItems_ExtraShrineUse");
			if (itemDef)
			{
				if (RedTier.Value)
				{
					itemDef._itemTierDef = LegacyResourcesAPI.Load<ItemTierDef>("ItemTierDefs/Tier3Def");
					AssignDepricatedTier(itemDef, ItemTier.Tier3);
					if (PickupIcon) itemDef.pickupIconSprite = PickupIcon;

					modified = true;
				}

				if (modified)
				{
					ModifyCount++;
					TweakCount++;
					appliedChanges = true;
				}
			}
		}
	}
}
