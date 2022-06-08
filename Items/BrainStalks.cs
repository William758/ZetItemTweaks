using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class BrainStalks
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "BrainStalks";
		public static bool appliedChanges = false;
		public static bool appliedRestock = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<bool> Restock { get; set; }
		public static ConfigEntry<float> BaseDuration { get; set; }
		public static ConfigEntry<float> StackDuration { get; set; }



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
			Restock = ConfigEntry(
				itemIdentifier, "Restock", true,
				"Refund skill stock on use, bypassing 0.5 second cooldown."
			);
			BaseDuration = ConfigEntry(
				itemIdentifier, "BaseDuration", 4f,
				"Duration of buff gained from item."
			);
			StackDuration = ConfigEntry(
				itemIdentifier, "StackDuration", 4f,
				"Duration of buff gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			bool applyRestock = Restock.Value;

			if (applyRestock && PluginLoaded("xyz.yekoc.SnapStalk"))
			{
				LogWarn(itemIdentifier + " :: Restock Disabled because " + LastPluginChecked + " is installed!");

				applyRestock = false;
			}

			if (applyRestock)
			{
				RestockHook();

				appliedRestock = true;
			}

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				DurationHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("BRAINSTALK_DURATION", "Upon killing an elite monster, <style=cIsDamage>enter a frenzy</style> for {0}");
				RegisterFragment("BRAINSTALK_EFFECT", " where <style=cIsUtility>skills have no cooldowns</style>.");
				RegisterToken("ITEM_KILLELITEFRENZY_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("BRAINSTALK_DURATION", true),
				ScalingText(BaseDuration.Value, StackDuration.Value, "duration", "cIsDamage")
			);
			output += TextFragment("BRAINSTALK_EFFECT");

			return output;
		}



		private static void RestockHook()
		{
			On.RoR2.Skills.SkillDef.OnExecute += (orig, self, slot) =>
			{
				orig(self, slot);

				if (!slot.beginSkillCooldownOnSkillEnd && slot.characterBody.HasBuff(RoR2Content.Buffs.NoCooldowns) && Restock.Value)
				{
					slot.RestockSteplike();
				}
			};
		}

		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("KillEliteFrenzy")),
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

		private static void DurationHook()
		{
			IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(4f),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 4;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDuration.Value + StackDuration.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: DurationHook Failed!");
				}
			};
		}
	}
}
