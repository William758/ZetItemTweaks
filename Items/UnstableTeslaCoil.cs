using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class UnstableTeslaCoil
	{
		public static List<string> autoCompatList = new List<string> { "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "UnstableTeslaCoil";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<int> BaseTarget { get; set; }
		public static ConfigEntry<int> StackTarget { get; set; }
		public static ConfigEntry<float> BaseRange { get; set; }
		public static ConfigEntry<float> StackRange { get; set; }
		public static ConfigEntry<bool> Blacklist { get; set; }



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
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 2f,
				"Lightning damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0f,
				"Lightning damage gained from item per stack."
			);
			BaseTarget = ConfigEntry(
				itemIdentifier, "BaseTarget", 3,
				"Maximum targets gained from item."
			);
			StackTarget = ConfigEntry(
				itemIdentifier, "StackTarget", 2,
				"Maximum targets gained from item per stack."
			);
			BaseRange = ConfigEntry(
				itemIdentifier, "BaseRange", 30f,
				"Lightning range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 0f,
				"Lightning range gained from item per stack."
			);
			Blacklist = ConfigEntry(
				itemIdentifier, "Blacklist", false,
				"Add AIBlacklist ItemTag to item."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList, true)) return;

			LogInfo(itemIdentifier + " :: Proceed with ModifyItem.");

			bool modified = false;

			ItemDef itemDef = RoR2Content.Items.ShockNearby;

			if (Blacklist.Value)
			{
				if (itemDef.DoesNotContainTag(ItemTag.AIBlacklist))
				{
					List<ItemTag> itemTags = itemDef.tags.ToList();
					itemTags.Add(ItemTag.AIBlacklist);

					itemDef.tags = itemTags.ToArray();

					modified = true;
				}
			}

			if (modified)
			{
				ModifyCount++;
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DamageHook();
			RangeHook();
			BounceHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("TESLA_DAMAGE", "Fire out <style=cIsDamage>lightning</style> that repeatedly deals {0} base damage.");
				RegisterFragment("TESLA_TARGETING", "\nHits up to {0} targets in a {1} radius.");
				RegisterFragment("TESLA_ALTERNATE", "\nThe Tesla Coil cycles activity {0}.");
				RegisterToken("ITEM_SHOCKNEARBY_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("TESLA_DAMAGE"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);
			output += String.Format(
				TextFragment("RAZOR_TARGETING"),
				ScalingText(BaseTarget.Value, StackTarget.Value, "flat", "cIsDamage"),
				ScalingText(BaseRange.Value, StackRange.Value, "distance", "cIsDamage")
			);
			output += String.Format(
				TextFragment("TESLA_ALTERNATE"),
				SecondText(10f, "every", "cIsDamage")
			);

			return output;
		}



		private static void DamageHook()
		{
			IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(2f),
					x => x.MatchMul(),
					x => x.MatchStfld<LightningOrb>("damageValue")
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldfld, typeof(RoR2.Items.BaseItemBodyBehavior).GetField("stack"));
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDamage.Value + StackDamage.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void RangeHook()
		{
			IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStfld<LightningOrb>("range")
				);

				if (found)
				{
					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldfld, typeof(RoR2.Items.BaseItemBodyBehavior).GetField("stack"));
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseRange.Value + StackRange.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: RangeHook Failed!");
				}
			};
		}

		private static void BounceHook()
		{
			IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStfld<LightningOrb>("bouncesRemaining")
				);

				if (found)
				{
					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldfld, typeof(RoR2.Items.BaseItemBodyBehavior).GetField("stack"));
					c.EmitDelegate<Func<int, int>>((count) =>
					{
						return 1 - (BaseTarget.Value + StackTarget.Value * (count - 1));
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: BounceHook Failed!");
				}
			};
		}
	}
}
