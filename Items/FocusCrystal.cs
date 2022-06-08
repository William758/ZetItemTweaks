using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class FocusCrystal
	{
		public static List<string> autoCompatList = new List<string> { "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "FocusCrystal";
		public static bool appliedChanges = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> NearbyRange { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }



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
			NearbyRange = ConfigEntry(
				itemIdentifier, "NearbyRange", 13f,
				"Distance to be considered nearby."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.2f,
				"Nearby damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.2f,
				"Nearby damage gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				RangeHook();
				DamageHook();

				ScaleIndicator();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("NEARBY_DAMAGE", "\nIncrease damage to enemies within {0} by {1}.");
				RegisterToken("ITEM_NEARBYDAMAGEBONUS_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("NEARBY_DAMAGE", true),
				ScalingText(NearbyRange.Value, "distance", "cIsDamage"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			return output;
		}



		private static void FindIndexHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("NearbyDamageBonus")),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount"),
					x => x.MatchStloc(out ItemCountLocIndex)
				);

				if (found)
				{
					StlocCursorIndex = c.Index;
				}
				else
				{
					LogWarn(itemIdentifier + " :: FindIndexHook Failed!");
				}
			};
		}

		private static void RangeHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchCallOrCallvirt<Vector3>("get_sqrMagnitude"),
					x => x.MatchLdcR4(169f)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.EmitDelegate<Func<float>>(() =>
					{
						float range = NearbyRange.Value;
						return range * range;
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: RangeHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(out _),
					x => x.MatchLdcR4(1f),
					x => x.MatchLdloc(ItemCountLocIndex),
					// Removed Instructions
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(0.2f),
					x => x.MatchMul(),
					// ---
					x => x.MatchAdd(),
					x => x.MatchMul(),
					x => x.MatchStloc(out _)
				);

				if (found)
				{
					c.Index += 3;

					c.RemoveRange(3);

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

		private static void ScaleIndicator()
		{
			// TODO - rescale if the config has been changed in game
			GameObject focusCrystal = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/NearbyDamageBonusIndicator");
			float scale = NearbyRange.Value / 13f;
			focusCrystal.transform.localScale = new Vector3(scale, scale, scale);
		}
	}
}
