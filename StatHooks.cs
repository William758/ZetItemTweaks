using System;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;
using System.Reflection;

namespace TPDespair.ZetItemTweaks
{
	internal static class StatHooks
	{
		public static FieldInfo StatsDirty;

		internal static void Init()
		{
			MovementSpeedHook();
			DamageHook();
			ShieldHook();
			HealthHook();
			AttackSpeedHook();
			RegenHook();

			StatsDirty = typeof(CharacterBody).GetField("statsDirty", BindingFlags.Instance | BindingFlags.NonPublic);
			LateStatHook();

			//OnLateSetup += LogRecalcStats;
		}


		
		private static void MovementSpeedHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 74;
				const int multValue = 75;
				const int divValue = 76;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchLdloc(divValue),
					x => x.MatchDiv(),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);

					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (RoseBuckler.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.SprintArmor);
								if (count > 0)
								{
									if (RoseBuckler.MaxMomentum.Value > 0 && self.HasBuff(RoseBuckler.MomentumBuff) && RoseBuckler.BaseMomentumMove.Value > 0f)
									{
										float mult = self.GetBuffCount(RoseBuckler.MomentumBuff) / (float)RoseBuckler.MaxMomentum.Value;
										value += (RoseBuckler.BaseMomentumMove.Value + (RoseBuckler.StackMomentumMove.Value * (count - 1))) * mult;
									}

									if (RoseBuckler.BaseMove.Value > 0f)
									{
										value += RoseBuckler.BaseMove.Value + (RoseBuckler.StackMove.Value * (count - 1));
									}
								}
							}

							if (Pearl.appliedChanges && Pearl.BaseMove.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
								if (count > 0)
								{
									value += Pearl.BaseMove.Value + (Pearl.StackMove.Value * (count - 1));
								}
							}

							if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseMove.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
								if (count > 0)
								{
									value += IrradiantPearl.BaseMove.Value + (IrradiantPearl.StackMove.Value * (count - 1));
								}
							}

							if (WarHorn.appliedChanges && WarHorn.BaseBuffMove.Value > 0f)
							{
								if (self.HasBuff(RoR2Content.Buffs.Energized))
								{
									int count = inventory.GetItemCount(RoR2Content.Items.EnergizedOnEquipmentUse);
									count = Mathf.Max(1, count);

									value += WarHorn.BaseBuffMove.Value + (WarHorn.StackBuffMove.Value * (count - 1));
								}
							}

							if (HuntersHarpoon.appliedChanges)
							{
								int count = inventory.GetItemCount(DLC1Content.Items.MoveSpeedOnKill);
								if (count > 0)
								{
									float mult = self.GetBuffCount(DLC1Content.Buffs.KillMoveSpeed) / 5f;

									float defaultValue = 1.25f * mult;
									float targetValue = (HuntersHarpoon.BaseBuffMove.Value + (HuntersHarpoon.StackBuffMove.Value * (count - 1))) * mult;

									value += targetValue - defaultValue;
								}
							}

							if (RedWhip.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.SprintOutOfCombat);
								if (count > 0)
								{
									if (self.HasBuff(RoR2Content.Buffs.WhipBoost))
									{
										float defaultValue = 0.3f * count;
										float targetValue = RedWhip.BaseSafeMove.Value + (RedWhip.StackSafeMove.Value * (count - 1));

										value += targetValue - defaultValue;
									}

									if (RedWhip.BaseMove.Value > 0f)
									{
										value += RedWhip.BaseMove.Value + (RedWhip.StackMove.Value * (count - 1));
									}
								}
							}
						}

						if (BerzerkersPauldron.appliedChanges && BerzerkersPauldron.BuffMove.Value > 0f)
						{
							int count = self.GetBuffCount(BerzerkersPauldron.MultiKillBuff);
							if (count > 0)
							{
								value += BerzerkersPauldron.BuffMove.Value * count;
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);

					c.Emit(OpCodes.Ldloc, baseValue);
				}
				else
				{
					LogWarn("MovementSpeedHook Failed");
				}
			};
		}
		
		private static void DamageHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 78;
				const int multValue = 79;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (Pearl.appliedChanges && Pearl.BaseDamage.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
								if (count > 0)
								{
									value += Pearl.BaseDamage.Value + (Pearl.StackDamage.Value * (count - 1));
								}
							}

							if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseDamage.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
								if (count > 0)
								{
									value += IrradiantPearl.BaseDamage.Value + (IrradiantPearl.StackDamage.Value * (count - 1));
								}
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);
					/*
					c.Index += 4;

					// multiplier
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
					*/
				}
				else
				{
					LogWarn("DamageHook Failed");
				}
			};
		}
		
		private static void ShieldHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int shieldValue = 64;

				bool found = c.TryGotoNext(
					x => x.MatchMul(),
					x => x.MatchAdd(),
					x => x.MatchStloc(shieldValue)
				);

				if (found)
				{
					c.Index += 3;

					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, shieldValue);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Callvirt, typeof(CharacterBody).GetMethod("get_maxHealth"));
					c.EmitDelegate<Func<CharacterBody, float, float, float>>((self, shield, health) =>
					{
						float healthFraction = 0f;

						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (PlasmaShrimp.appliedChanges)
							{
								int count = inventory.GetItemCount(DLC1Content.Items.MissileVoid);
								if (count > 0)
								{
									float targetValue = PlasmaShrimp.BaseHealthAsShield.Value + (PlasmaShrimp.StackHealthAsShield.Value * (count - 1));

									healthFraction += targetValue - 0.1f;
								}
							}

							if (PersonalShieldGenerator.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.PersonalShield);
								if (count > 0)
								{
									float defaultValue = 0.08f * count;
									float targetValue = PersonalShieldGenerator.BaseHealthAsShield.Value + (PersonalShieldGenerator.StackHealthAsShield.Value * (count - 1));

									healthFraction += targetValue - defaultValue;
								}
							}
						}

						if (healthFraction != 0f)
						{
							shield += health * healthFraction;
						}

						return shield;
					});
					c.Emit(OpCodes.Stloc, shieldValue);
				}
				else
				{
					LogWarn("ShieldHook Failed");
				}
			};
		}
		
		private static void HealthHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 62;
				const int multValue = 63;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// add
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (TitanicKnurl.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Knurl);
								if (count > 0)
								{
									float defaultValue = 40f * count;
									float targetValue = TitanicKnurl.BaseHealth.Value + (TitanicKnurl.StackHealth.Value * (count - 1));

									value += targetValue - defaultValue;
								}
							}

							if (BensRainCoat.appliedChanges && BensRainCoat.BaseHealth.Value > 0f)
							{
								int count = inventory.GetItemCount(DLC1Content.Items.ImmuneToDebuff);
								if (count > 0)
								{
									value += BensRainCoat.BaseHealth.Value + (BensRainCoat.StackHealth.Value * (count - 1));
								}
							}

							if (BisonSteak.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.FlatHealth);
								if (count > 0)
								{
									float defaultValue = 25f * count;
									float targetValue = BisonSteak.BaseHealth.Value + (BisonSteak.StackHealth.Value * (count - 1));

									value += targetValue - defaultValue;
								}
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
					
					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (TitanicKnurl.appliedChanges && TitanicKnurl.BaseHealthPercent.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Knurl);
								if (count > 0)
								{
									value += TitanicKnurl.BaseHealthPercent.Value + (TitanicKnurl.StackHealthPercent.Value * (count - 1));
								}
							}

							if (BensRainCoat.appliedChanges && BensRainCoat.BaseHealthPercent.Value > 0f)
							{
								int count = inventory.GetItemCount(DLC1Content.Items.ImmuneToDebuff);
								if (count > 0)
								{
									value += BensRainCoat.BaseHealthPercent.Value + (BensRainCoat.StackHealthPercent.Value * (count - 1));
								}
							}

							if (Pearl.appliedChanges && Pearl.BaseHealth.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
								if (count > 0)
								{
									value += Pearl.BaseHealth.Value + (Pearl.StackHealth.Value * (count - 1));
								}
							}

							if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseHealth.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
								if (count > 0)
								{
									value += IrradiantPearl.BaseHealth.Value + (IrradiantPearl.StackHealth.Value * (count - 1));
								}
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);
					/*
					c.Index += 4;

					// multiplier
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
					*/
				}
				else
				{
					LogWarn("HealthHook Failed");
				}
			};
		}

		private static void AttackSpeedHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int baseValue = 82;
				const int multValue = 83;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(baseValue),
					x => x.MatchLdloc(multValue),
					x => x.MatchMul(),
					x => x.MatchStloc(baseValue)
				);

				if (found)
				{
					// increase
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, multValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (Pearl.appliedChanges && Pearl.BaseAtkSpd.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
								if (count > 0)
								{
									value += Pearl.BaseAtkSpd.Value + (Pearl.StackAtkSpd.Value * (count - 1));
								}
							}

							if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseAtkSpd.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
								if (count > 0)
								{
									value += IrradiantPearl.BaseAtkSpd.Value + (IrradiantPearl.StackAtkSpd.Value * (count - 1));
								}
							}

							if (WarHorn.appliedChanges)
							{
								if (self.HasBuff(RoR2Content.Buffs.Energized))
								{
									int count = inventory.GetItemCount(RoR2Content.Items.EnergizedOnEquipmentUse);
									count = Mathf.Max(1, count);

									float targetValue = WarHorn.BaseBuffAtkSpd.Value + (WarHorn.StackBuffAtkSpd.Value * (count - 1));

									value += targetValue - 0.7f;
								}
							}
						}

						if (PredatoryInstincts.appliedChanges)
						{
							int count = self.GetBuffCount(RoR2Content.Buffs.AttackSpeedOnCrit);
							if (count > 0)
							{
								float defaultValue = 0.12f * count;
								float targetValue = PredatoryInstincts.BuffAtkSpd.Value * count;

								value += targetValue - defaultValue;
							}
						}

						if (BerzerkersPauldron.appliedChanges && BerzerkersPauldron.BuffAtkSpd.Value > 0f)
						{
							int count = self.GetBuffCount(BerzerkersPauldron.MultiKillBuff);
							if (count > 0)
							{
								value += BerzerkersPauldron.BuffAtkSpd.Value * count;
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, multValue);
					
					c.Index += 4;

					// multiplier
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, baseValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						if (ChronoBauble.appliedChanges && ChronoBauble.AtkSlow.Value != 0f)
						{
							if (self.HasBuff(RoR2Content.Buffs.Slow60))
							{
								float delta = Mathf.Abs(ChronoBauble.AtkSlow.Value);
								value *= 1f - Mathf.Min(0.9f, delta);
							}
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, baseValue);
				}
				else
				{
					LogWarn("AttackSpeedHook Failed");
				}
			};
		}

		private static void RegenHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				const int lvlScaling = 66;
				const int knurlValue = 67;
				const int crocoValue = 70;
				const int multValue = 72;

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(1f),
					x => x.MatchStloc(multValue)
				);

				if (found)
				{
					// add (affected by lvl regen scaling and ignites)
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, knurlValue);
					c.Emit(OpCodes.Ldloc, lvlScaling);
					c.EmitDelegate<Func<CharacterBody, float, float, float>>((self, value, scaling) =>
					{
						float amount = 0f;

						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (TitanicKnurl.appliedChanges)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Knurl);
								if (count > 0)
								{
									float defaultValue = 1.6f * count;
									float targetValue = TitanicKnurl.BaseRegen.Value + (TitanicKnurl.StackRegen.Value * (count - 1));

									amount += targetValue - defaultValue;
								}
							}

							if (BisonSteak.appliedChanges)
							{
								int BuffCount = self.GetBuffCount(JunkContent.Buffs.MeatRegenBoost);
								if (BuffCount > 0)
								{
									int itemCount = Mathf.Max(1, inventory.GetItemCount(RoR2Content.Items.FlatHealth));

									float targetValue = (BisonSteak.BaseKillRegen.Value + (BisonSteak.StackKillRegen.Value * (itemCount - 1))) * BuffCount;

									amount += targetValue - 2f;
								}
							}

							if (Pearl.appliedChanges && Pearl.BaseRegen.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
								if (count > 0)
								{
									amount += Pearl.BaseRegen.Value + (Pearl.StackRegen.Value * (count - 1));
								}
							}

							if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseRegen.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
								if (count > 0)
								{
									amount += IrradiantPearl.BaseRegen.Value + (IrradiantPearl.StackRegen.Value * (count - 1));
								}
							}

							if (CautiousSlug.appliedChanges && self.outOfDanger)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.HealWhileSafe);
								if (count > 0)
								{
									float defaultValue = 3f * count;
									float targetValue = CautiousSlug.BaseSafeRegen.Value + (CautiousSlug.StackSafeRegen.Value * (count - 1));

									amount += targetValue - defaultValue;
								}
							}

							if (LeptonDaisy.appliedChanges && LeptonDaisy.BaseHoldoutRegen.Value > 0f)
							{
								int count = self.GetBuffCount(LeptonDaisy.RegenBuff);
								if (count > 0)
								{
									amount += LeptonDaisy.BaseHoldoutRegen.Value + (LeptonDaisy.StackHoldoutRegen.Value * (count - 1));
								}
							}
						}

						if (amount != 0f)
						{
							value += amount * scaling;
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, knurlValue);

					// add percent (unaffected by lvl regen scaling and ignites)
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldloc, crocoValue);
					c.EmitDelegate<Func<CharacterBody, float, float>>((self, value) =>
					{
						float amount = 0f;

						Inventory inventory = self.inventory;
						if (inventory)
						{
							if (TitanicKnurl.appliedChanges && TitanicKnurl.BaseRegenFraction.Value > 0f)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.Knurl);
								if (count > 0)
								{
									amount += TitanicKnurl.BaseRegenFraction.Value + (TitanicKnurl.StackRegenFraction.Value * (count - 1));
								}
							}

							if (BensRainCoat.appliedChanges && BensRainCoat.BaseSafeRegenFraction.Value > 0f && self.outOfDanger)
							{
								int count = inventory.GetItemCount(DLC1Content.Items.ImmuneToDebuff);
								if (count > 0)
								{
									amount += BensRainCoat.BaseSafeRegenFraction.Value + (BensRainCoat.StackSafeRegenFraction.Value * (count - 1));
								}
							}

							if (CautiousSlug.appliedChanges && CautiousSlug.BaseSafeRegenFraction.Value > 0f && self.outOfDanger)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.HealWhileSafe);
								if (count > 0)
								{
									amount += CautiousSlug.BaseSafeRegenFraction.Value + (CautiousSlug.StackSafeRegenFraction.Value * (count - 1));
								}
							}

							if (LeptonDaisy.appliedChanges && LeptonDaisy.BaseHoldoutRegenFraction.Value > 0f)
							{
								int count = self.GetBuffCount(LeptonDaisy.RegenBuff);
								if (count > 0)
								{
									amount += LeptonDaisy.BaseHoldoutRegenFraction.Value + (LeptonDaisy.StackHoldoutRegenFraction.Value * (count - 1));
								}
							}
						}

						if (amount > 0f && AllowPercentHealthRegen(self))
						{
							value += self.maxHealth * amount;
						}

						return value;
					});
					c.Emit(OpCodes.Stloc, crocoValue);
				}
				else
				{
					LogWarn("RegenHook Failed");
				}
			};
		}

		private static bool AllowPercentHealthRegen(CharacterBody body)
		{
			TeamIndex teamIndex = body.teamComponent.teamIndex;

			if (teamIndex == TeamIndex.Player || body.outOfDanger)
			{
				if (!body.HasBuff(RoR2Content.Buffs.HiddenInvincibility) && !body.HasBuff(RoR2Content.Buffs.Immune))
				{
					if (body.baseNameToken != "ARTIFACTSHELL_BODY_NAME" && body.baseNameToken != "TITANGOLD_BODY_NAME")
					{
						return true;
					}
				}
			}

			return false;
		}



		private static void LateStatHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStfld(StatsDirty)
				);

				if (found)
				{
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Action<CharacterBody>>((self) =>
					{
						DirectStatHook(self);
					});
				}
				else
				{
					LogWarn("LateStatHook Failed");
				}
			};
		}

		private static void DirectStatHook(CharacterBody self)
		{
			if (self)
			{
				self.armor += GetArmorDelta(self);

				float crit = GetCritDelta(self);
				float critMult = GetCritMultDelta(self);

				if (crit != 0)
				{
					Inventory inventory = self.inventory;
					if (inventory)
					{
						if (inventory.GetItemCount(DLC1Content.Items.ConvertCritChanceToCritDamage) != 0)
						{
							critMult += crit * 0.01f;
							crit = 0f;
						}
					}
				}

				self.crit += crit;
				self.critMultiplier += critMult;

				float cooldownFlat = GetCooldownFlat(self);
				float cooldownMult = GetCooldownMult(self);

				if (cooldownMult != 1f || cooldownFlat != 0f)
				{
					SkillLocator skillLocator = self.skillLocator;

					if (skillLocator.primary)
					{
						skillLocator.primary.cooldownScale *= cooldownMult;
						skillLocator.primary.flatCooldownReduction += cooldownFlat;
					}
					if (skillLocator.secondary)
					{
						skillLocator.secondary.cooldownScale *= cooldownMult;
						skillLocator.secondary.flatCooldownReduction += cooldownFlat;
					}
					if (skillLocator.utility)
					{
						skillLocator.utility.cooldownScale *= cooldownMult;
						skillLocator.utility.flatCooldownReduction += cooldownFlat;
					}
					if (skillLocator.special)
					{
						skillLocator.special.cooldownScale *= cooldownMult;
						skillLocator.special.flatCooldownReduction += cooldownFlat;
					}
				}
			}
		}

		private static float GetArmorDelta(CharacterBody self)
		{
			float armor = 0f;

			if (ShatteringJustice.appliedChanges)
			{
				if (self.HasBuff(RoR2Content.Buffs.Pulverized))
				{
					armor -= ShatteringJustice.ArmorReduction.Value - 60f;
				}
			}

			Inventory inventory = self.inventory;
			if (inventory)
			{
				if (Aegis.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.BarrierOnOverHeal);
					if (count > 0)
					{
						if (Aegis.BaseArmor.Value > 0f)
						{
							armor += Aegis.BaseArmor.Value + (Aegis.StackArmor.Value * (count - 1));
						}

						if (Aegis.BaseBarrierArmor.Value > 0f)
						{
							if (self.HasBuff(Aegis.BarrierBuff))
							{
								armor += Aegis.BaseBarrierArmor.Value + (Aegis.StackBarrierArmor.Value * (count - 1));
							}
						}
					}
				}

				if (TitanicKnurl.appliedChanges && TitanicKnurl.BaseArmor.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.Knurl);
					if (count > 0)
					{
						armor += TitanicKnurl.BaseArmor.Value + (TitanicKnurl.StackArmor.Value * (count - 1));
					}
				}

				if (RoseBuckler.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.SprintArmor);
					if (count > 0)
					{
						if (self.isSprinting)
						{
							armor -= 30f * count;
						}

						if (RoseBuckler.MaxMomentum.Value > 0 && RoseBuckler.BaseMomentumArmor.Value > 0f)
						{
							float mult = self.GetBuffCount(RoseBuckler.MomentumBuff) / (float)RoseBuckler.MaxMomentum.Value;
							armor += (RoseBuckler.BaseMomentumArmor.Value + (RoseBuckler.StackMomentumArmor.Value * (count - 1))) * mult;
						}

						if (RoseBuckler.BaseArmor.Value > 0f)
						{
							armor += RoseBuckler.BaseArmor.Value + (RoseBuckler.StackArmor.Value * (count - 1));
						}
					}
				}

				if (Pearl.appliedChanges && Pearl.BaseArmor.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
					if (count > 0)
					{
						armor += Pearl.BaseArmor.Value + (Pearl.StackArmor.Value * (count - 1));
					}
				}

				if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseArmor.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
					if (count > 0)
					{
						armor += IrradiantPearl.BaseArmor.Value + (IrradiantPearl.StackArmor.Value * (count - 1));
					}
				}

				if (RedWhip.appliedChanges && RedWhip.BaseArmor.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.SprintOutOfCombat);
					if (count > 0)
					{
						armor += RedWhip.BaseArmor.Value + (RedWhip.StackArmor.Value * (count - 1));
					}
				}
			}

			return armor;
		}

		private static float GetCritDelta(CharacterBody self)
		{
			float crit = 0f;

			Inventory inventory = self.inventory;
			if (inventory)
			{
				if (ShatterSpleen.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.BleedOnHitAndExplode);
					if (count > 0)
					{
						float targetValue = ShatterSpleen.BaseCrit.Value + (ShatterSpleen.StackCrit.Value * (count - 1));

						crit += targetValue - 5f;
					}
				}

				if (LaserScope.appliedChanges && LaserScope.BaseCrit.Value != 0f)
				{
					int count = inventory.GetItemCount(DLC1Content.Items.CritDamage);
					if (count > 0)
					{
						crit += LaserScope.BaseCrit.Value + (LaserScope.StackCrit.Value * (count - 1));
					}
				}

				if (PredatoryInstincts.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.AttackSpeedOnCrit);
					if (count > 0)
					{
						float targetValue = PredatoryInstincts.BaseCrit.Value + (PredatoryInstincts.StackCrit.Value * (count - 1));

						crit += targetValue - 5f;
					}
				}

				if (HarvestersScythe.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.HealOnCrit);
					if (count > 0)
					{
						float targetValue = HarvestersScythe.BaseCrit.Value + (HarvestersScythe.StackCrit.Value * (count - 1));

						crit += targetValue - 5f;
					}
				}

				if (LensMakersGlasses.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.CritGlasses);
					if (count > 0)
					{
						float defaultValue = 10f * count;
						float targetValue = LensMakersGlasses.BaseCrit.Value + (LensMakersGlasses.StackCrit.Value * (count - 1));

						crit += targetValue - defaultValue;
					}
				}

				if (Pearl.appliedChanges && Pearl.BaseCrit.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
					if (count > 0)
					{
						crit += Pearl.BaseCrit.Value + (Pearl.StackCrit.Value * (count - 1));
					}
				}

				if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseCrit.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
					if (count > 0)
					{
						crit += IrradiantPearl.BaseCrit.Value + (IrradiantPearl.StackCrit.Value * (count - 1));
					}
				}
			}

			return crit;
		}

		private static float GetCritMultDelta(CharacterBody self)
		{
			float critMult = 0f;

			Inventory inventory = self.inventory;
			if (inventory)
			{
				if (LaserScope.appliedChanges)
				{
					int count = inventory.GetItemCount(DLC1Content.Items.CritDamage);
					if (count > 0)
					{
						float defaultValue = 1f * count;
						float targetValue = LaserScope.BaseCritMult.Value + (LaserScope.StackCritMult.Value * (count - 1));

						critMult += targetValue - defaultValue;
					}
				}
			}

			return critMult;
		}

		private static float GetCooldownFlat(CharacterBody self)
		{
			float cooldown = 0f;

			Inventory inventory = self.inventory;
			if (inventory)
			{
				if (AlienHead.appliedChanges && AlienHead.BaseFlatCooldown.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.AlienHead);
					if (count > 0)
					{
						cooldown += AlienHead.BaseFlatCooldown.Value + (AlienHead.StackFlatCooldown.Value * (count - 1));
					}
				}
			}

			return cooldown;
		}

		private static float GetCooldownMult(CharacterBody self)
		{
			float mult = 1f;
			float additiveReduction = 0f;

			Inventory inventory = self.inventory;
			if (inventory)
			{
				if (Pearl.appliedChanges && Pearl.BaseCooldown.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.Pearl);
					if (count > 0)
					{
						additiveReduction += Pearl.BaseCooldown.Value + (Pearl.StackCooldown.Value * (count - 1));
					}
				}

				if (IrradiantPearl.appliedChanges && IrradiantPearl.BaseCooldown.Value > 0f)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.ShinyPearl);
					if (count > 0)
					{
						additiveReduction += IrradiantPearl.BaseCooldown.Value + (IrradiantPearl.StackCooldown.Value * (count - 1));
					}
				}

				if (AlienHead.appliedChanges)
				{
					int count = inventory.GetItemCount(RoR2Content.Items.AlienHead);
					if (count > 0)
					{
						float defaultValue = Mathf.Pow(0.75f, count);
						float targetValue = 1f - Mathf.Abs(AlienHead.BaseCooldown.Value);
						targetValue *= Mathf.Pow(1f - Mathf.Abs(AlienHead.StackCooldown.Value), count - 1);

						mult *= targetValue / defaultValue;
					}
				}
			}

			if (additiveReduction > 0f)
			{
				mult *= 1f - (Util.ConvertAmplificationPercentageIntoReductionPercentage(additiveReduction * 100f) / 100f);
			}

			return mult;
		}


		/*
		private static void LogRecalcStats()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				LogWarn(il);
			};
		}
		*/
	}
}
