using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class ScratchTicket
	{
		internal static Sprite PickupIcon;

		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "ScratchTicket";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> GreenTier { get; set; }



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
			GreenTier = ConfigEntry(
				itemIdentifier, "GreenTier", false,
				"Change item to green tier."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			bool modified = false;

			ItemDef itemDef = FindItemDefPreCatalogInit("MysticsItems_ScratchTicket");
			if (itemDef)
			{
				if (GreenTier.Value)
				{
					itemDef._itemTierDef = LegacyResourcesAPI.Load<ItemTierDef>("ItemTierDefs/Tier2Def");
					AssignDepricatedTier(itemDef, ItemTier.Tier2);
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
