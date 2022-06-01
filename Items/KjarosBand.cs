using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class KjarosBand
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "KjarosBand";
		public static bool appliedChanges = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
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
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 3f,
				"Fire tornado damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 2f,
				"Fire tornado damage gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				DamageHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("FIRE_TORNADO_TRIGGER", "Hits that deal <style=cIsDamage>more than 400% damage</style> also blasts enemies with a <style=cIsDamage>runic flame tornado</style>,");
				RegisterFragment("FIRE_TORNADO_DAMAGE", " dealing {0} TOTAL damage over time. Recharges every <style=cIsUtility>10</style> seconds.");
				RegisterToken("ITEM_FIRERING_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = TextFragment("FIRE_TORNADO_TRIGGER");
			output += String.Format(
				TextFragment("FIRE_TORNADO_DAMAGE"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			return output;
		}



		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("FireRing")),
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

		private static void DamageHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchLdcI4(0),
					x => x.MatchBle(out _)
				);

				if (found)
				{
					found = c.TryGotoNext(
						x => x.MatchLdloc(ItemCountLocIndex),
						x => x.MatchConvR4(),
						x => x.MatchMul(),
						x => x.MatchStloc(out _)
					);

					if (found)
					{
						c.Index += 3;

						c.Emit(OpCodes.Pop);
						c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
						c.EmitDelegate<Func<int, float>>((count) =>
						{
							return BaseDamage.Value + StackDamage.Value * (count - 1);
						});
					}
					else
					{
						LogWarn(itemIdentifier + " :: DamageHook Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed! - Could not find count > 0.");
				}
			};
		}
	}
}
