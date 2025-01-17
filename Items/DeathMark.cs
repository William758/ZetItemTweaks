﻿using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class DeathMark
	{
		private static readonly List<DotController.DotIndex> DebuffDOT = new List<DotController.DotIndex>();
		private static DotController.DotIndex DotIndexCount = 0;

		public static List<string> autoCompatList = new List<string> { "com.Skell.DeathMarkChange", "OakPrime.DeathMarkFix", "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "DeathMark";
		public static bool appliedChanges = false;
		public static bool appliedDotDebuffFix = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<bool> EnableDotFix { get; set; }
		public static ConfigEntry<bool> PlayerTeamStack { get; set; }
		public static ConfigEntry<int> CountTrigger { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<float> DebuffDuration { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnLateSetup += DotDebuffFix;
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
			EnableDotFix = ConfigEntry(
				itemIdentifier, "EnableDotFix", true,
				"Prevent dots and their associated debuffs as counting as 2 debuffs towards deathmark."
			);
			PlayerTeamStack = ConfigEntry(
				itemIdentifier, "PlayerTeamStack", false,
				"Count total deathmarks across player team."
			);
			CountTrigger = ConfigEntry(
				itemIdentifier, "CountTrigger", 4,
				"Debuffs needed on target to trigger deathmark debuff."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.3f,
				"Damage taken increase of deathmark debuff."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.1f,
				"Damage taken increase of deathmark debuff per stack."
			);
			DebuffDuration = ConfigEntry(
				itemIdentifier, "DebuffDuration", 8f,
				"Duration of deathmark debuff."
			);
		}

		private static void DotDebuffFix()
		{
			bool applyDotDebuffFix = EnableDotFix.Value;

			if (applyDotDebuffFix && PluginLoaded("com.Nebby.ConfigurableDeathMark"))
			{
				LogWarn(itemIdentifier + " :: DotDebuffFix Disabled because " + LastPluginChecked + " is installed!");

				applyDotDebuffFix = false;
			}

			if (applyDotDebuffFix)
			{
				GatherDebuffDOT();

				if (StlocCursorIndex == 0) FindIndexHook();

				if (ItemCountLocIndex != 0)
				{
					DotFixHook();

					LogInfo(itemIdentifier + " :: DotDebuffFix Applied!");

					appliedDotDebuffFix = true;
				}
				else
				{
					LogWarn(itemIdentifier + " :: DotDebuffFix Failed!");
				}
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (StlocCursorIndex == 0) FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				CountHook();
				DurationHook();
				DamageHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default"; 
				
				RegisterFragment("DEATHMARK", "Enemies with at least {0} debuffs are <style=cIsDamage>marked for death</style> {1}, increasing damage taken by {2}.");
				RegisterFragment("DEATHMARK_PICKUP", "Enemies with at least {0} debuffs are marked for death, taking bonus damage.");
				RegisterToken("ITEM_DEATHMARK_DESC", DescriptionText());
				RegisterToken("ITEM_DEATHMARK_PICKUP", PickupText());

				targetLanguage = "pt-BR";

				RegisterFragment("DEATHMARK", "Inimigos com pelo menos {0} penalidades são <style=cIsDamage>marcados para morrer</style> {1}, aumentando o dano recebido em {2}.");
				RegisterFragment("DEATHMARK_PICKUP", "Inimigos com pelo menos {0} penalidades são marcados para morrer, recebendo bônus de dano.");
				RegisterToken("ITEM_DEATHMARK_DESC", DescriptionText());
				RegisterToken("ITEM_DEATHMARK_PICKUP", PickupText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("DEATHMARK"),
				ScalingText(CountTrigger.Value, "flat", "cIsDamage"),
				SecondText(DebuffDuration.Value, "for"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			return output;
		}

		private static string PickupText()
		{
			return String.Format(
				TextFragment("DEATHMARK_PICKUP"),
				ScalingText(CountTrigger.Value, "flat")
			);
		}



		private static void GatherDebuffDOT()
		{
			DotController.DotIndex dotIndex = 0;
			DotController.DotDef dotDef;
			int nullDotDef = 0;

			while (nullDotDef < 3)
			{
				try
				{
					// R2API throws IndexOutOfRangeException
					dotDef = DotController.GetDotDef(dotIndex);
				}
				catch(Exception e)
				{
					LogWarn(e);

					break;
				}

				if (dotDef != null)
				{
					nullDotDef = 0;
					DotIndexCount = dotIndex + 1;

					BuffDef buffDef = dotDef.associatedBuff;
					if (buffDef)
					{
						if (Array.IndexOf(BuffCatalog.debuffBuffIndices, buffDef.buffIndex) != -1)
						{
							if (!DebuffDOT.Contains(dotIndex))
							{
								LogInfo("DeathMark :: DotIndex : " + dotIndex + " - AssociatedBuff (" + buffDef.name + ") is a debuff!");
								DebuffDOT.Add(dotIndex);
							}
						}
					}
				}
				else
				{
					nullDotDef++;
				}

				dotIndex++;
			}

			LogInfo("GatherDebuffDOT :: Highest DotIndex found : " + DotIndexCount);
		}



		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("DeathMark")),
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

		private static void DotFixHook()
		{
			IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				int DotConIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchCallOrCallvirt<DotController>("FindDotController"),
					x => x.MatchStloc(out DotConIndex)
				);

				if (found)
				{
					found = c.TryGotoNext(
						x => x.MatchLdloc(ItemCountLocIndex + 1),
						x => x.MatchLdcI4(4)
					);

					if (found)
					{
						c.Index += 1;

						// embed into if statement and use loc[??]
						c.Emit(OpCodes.Ldloc, DotConIndex);
						c.EmitDelegate<Func<int, DotController, int>>((count, dotController) =>
						{
							if (dotController)
							{
							//logger.LogMessage("DeathMarkCounter : " + count);

							for (DotController.DotIndex dotIndex = 0; dotIndex < DotIndexCount; dotIndex++)
								{
									if (dotController.HasDotActive(dotIndex) && DebuffDOT.Contains(dotIndex))
									{
									// was counted twice by buff and dot checks - reduce value
									count--;
									}
								}

							//logger.LogMessage("DeathMarkCounter (fixed) : " + count);
						}

							return count;
						});
						c.Emit(OpCodes.Stloc, ItemCountLocIndex + 1);

						// restore loc[??] onto stack
						c.Emit(OpCodes.Ldloc, ItemCountLocIndex + 1);
					}
					else
					{
						LogWarn(itemIdentifier + " :: DotFixHook Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: DotFixHook:DotControllerIndex Failed!");
				}
			};
		}

		private static void CountHook()
		{
			IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex + 1),
					x => x.MatchLdcI4(4)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldc_I4, CountTrigger.Value);
				}
				else
				{
					LogWarn(itemIdentifier + " :: CountHook Failed!");
				}
			};
		}

		private static void DurationHook()
		{
			IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(out _),
					x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField("DeathMark")),
					x => x.MatchLdcR4(7f),
					x => x.MatchLdloc(out _),
					x => x.MatchConvR4(),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 6;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldc_R4, DebuffDuration.Value);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DurationHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.HealthComponent.TakeDamageProcess += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int DamageIndex = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdfld<DamageInfo>("damage"),
					x => x.MatchStloc(out DamageIndex)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Ldloc, DamageIndex);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldarg, 1);
					c.EmitDelegate<Func<float, HealthComponent, DamageInfo, float>>((damage, healthComponent, damageInfo) =>
					{
						GameObject attacker = damageInfo.attacker;
						if (!attacker) return damage;

						CharacterBody attackBody = attacker.GetComponent<CharacterBody>();
						if (!attackBody) return damage;

						CharacterBody self = healthComponent.body;
						if (!self) return damage;

						if (self.HasBuff(RoR2Content.Buffs.DeathMark))
						{
							int count = 0;

							if (PlayerTeamStack.Value && attackBody.teamComponent.teamIndex == TeamIndex.Player)
							{
								count = Util.GetItemCountForTeam(TeamIndex.Player, RoR2Content.Items.DeathMark.itemIndex, true, true);
							}
							else
							{
								Inventory inventory = attackBody.inventory;
								if (inventory) count = inventory.GetItemCount(RoR2Content.Items.DeathMark);
							}

							count = Math.Max(1, count);

							float target = 1f + BaseDamage.Value + StackDamage.Value * (count - 1);

							// undo effect of default deathmark
							damage *= target / 1.5f;
						}

						return damage;
					});
					c.Emit(OpCodes.Stloc, DamageIndex);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}
	}
}
