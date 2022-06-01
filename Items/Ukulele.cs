using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class Ukulele
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "Ukulele";
		public static bool appliedChanges = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> ProcChance { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<int> BaseTarget { get; set; }
		public static ConfigEntry<int> StackTarget { get; set; }
		public static ConfigEntry<float> BaseRange { get; set; }
		public static ConfigEntry<float> StackRange { get; set; }



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
			ProcChance = ConfigEntry(
				itemIdentifier, "ProcChance", 25f,
				"Chance to proc item effect. 25 = 25%"
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.8f,
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
				itemIdentifier, "BaseRange", 20f,
				"Lightning range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 2f,
				"Lightning range gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				ProcChanceHook();
				DamageHook();
				TargetCountHook();
				RangeHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("UKULELE_DAMAGE", "{0} chance to fire <style=cIsDamage>chain lightning</style> that deals {1} TOTAL damage.");
				RegisterFragment("UKULELE_TARGETING", "\nHits up to {0} targets within {1}.");
				RegisterToken("ITEM_CHAINLIGHTNING_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("UKULELE_DAMAGE"),
				ScalingText(ProcChance.Value, "chance", "cIsDamage"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);
			output += String.Format(
				TextFragment("UKULELE_TARGETING"),
				ScalingText(BaseTarget.Value, StackTarget.Value, "flat", "cIsDamage"),
				ScalingText(BaseRange.Value, StackRange.Value, "distance", "cIsDamage")
			);

			return output;
		}



		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("ChainLightning")),
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

		private static void ProcChanceHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(25f),
					x => x.MatchStloc(50)
				);

				if (found)
				{
					c.Index += 2;

					c.EmitDelegate<Func<float>>(() =>
					{
						return ProcChance.Value;
					});
					c.Emit(OpCodes.Stloc, 50);
				}
				else
				{
					LogWarn(itemIdentifier + " :: ProcChanceHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(0.8f),
					x => x.MatchStloc(51)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDamage.Value + StackDamage.Value * (count - 1);
					});
					c.Emit(OpCodes.Stloc, 51);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void TargetCountHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchMul(),
					x => x.MatchStfld<LightningOrb>("bouncesRemaining")
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
					c.EmitDelegate<Func<int, int>>((count) =>
					{
						return (BaseTarget.Value + StackTarget.Value * (count - 1)) - 1;
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: TargetCountHook Failed!");
				}
			};
		}

		private static void RangeHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchMul(),
					x => x.MatchConvR4(),
					x => x.MatchAdd(),
					x => x.MatchStfld<LightningOrb>("range")
				);

				if (found)
				{
					c.Index += 4;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
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
	}
}
