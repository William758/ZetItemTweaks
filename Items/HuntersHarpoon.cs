using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class HuntersHarpoon
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "HuntersHarpoon";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseBuffMove { get; set; }
		public static ConfigEntry<float> StackBuffMove { get; set; }
		public static ConfigEntry<float> BaseBuffDuration { get; set; }
		public static ConfigEntry<float> StackBuffDuration { get; set; }



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
			BaseBuffMove = ConfigEntry(
				itemIdentifier, "BaseBuffMove", 1.25f,
				"Movement speed gained from maximum buff."
			);
			StackBuffMove = ConfigEntry(
				itemIdentifier, "StackBuffMove", 0f,
				"Movement speed gained from maximum buff per item stack."
			);
			BaseBuffDuration = ConfigEntry(
				itemIdentifier, "BaseBuffDuration", 2f,
				"Buff duration gained from item."
			);
			StackBuffDuration = ConfigEntry(
				itemIdentifier, "StackBuffDuration", 1f,
				"Buff duration gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DurationHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("HARPOON", "Killing an enemy increases <style=cIsUtility>movement speed</style> by {0}, fading over {1} seconds.");
				RegisterToken("ITEM_MOVESPEEDONKILL_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("HARPOON"),
				ScalingText(BaseBuffMove.Value, StackBuffMove.Value, "percent", "cIsUtility"),
				ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "flat", "cIsUtility")
			);

			return output;
		}



		private static void DurationHook()
		{
			IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int countIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(DLC1Content.Items), "MoveSpeedOnKill"),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount"),
					x => x.MatchStloc(out countIndex)
				);

				if (found)
				{
					found = c.TryGotoNext(
						x => x.MatchLdcR4(1f),
						x => x.MatchLdloc(out _),
						x => x.MatchConvR4(),
						x => x.MatchLdcR4(0.5f),
						x => x.MatchMul(),
						x => x.MatchAdd()
					);

					if (found)
					{
						c.Index += 6;

						c.Emit(OpCodes.Pop);
						c.Emit(OpCodes.Ldloc, countIndex);
						c.EmitDelegate<Func<int, float>>((count) =>
						{
							return BaseBuffDuration.Value + StackBuffDuration.Value * (count - 1);
						});
					}
					else
					{
						LogWarn(itemIdentifier + " :: DurationHook:duration Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: DurationHook:ItemCount Failed!");
				}
			};
		}
	}
}
