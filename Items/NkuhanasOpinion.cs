using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class NkuhanasOpinion
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "NkuhanasOpinion";
		public static bool appliedChanges = false;

		private static bool StoreSoulEnergyHookSuccess = false;
		private static bool DisableDefaultHookSuccess = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseStore { get; set; }
		public static ConfigEntry<float> StackStore { get; set; }
		public static ConfigEntry<float> BaseCapacity { get; set; }
		public static ConfigEntry<float> StackCapacity { get; set; }
		public static ConfigEntry<float> ThresholdFraction { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
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
			BaseStore = ConfigEntry(
				itemIdentifier, "BaseStore", 1f,
				"Soul energy gained from healing."
			);
			StackStore = ConfigEntry(
				itemIdentifier, "StackStore", 1f,
				"Soul energy gained from healing per stack."
			);
			BaseCapacity = ConfigEntry(
				itemIdentifier, "BaseCapacity", 1f,
				"Health as soul energy capacity."
			);
			StackCapacity = ConfigEntry(
				itemIdentifier, "StackCapacity", 0f,
				"Health as soul energy capacity per stack."
			);
			ThresholdFraction = ConfigEntry(
				itemIdentifier, "ThresholdFraction", 0.1f,
				"Healing threshold to fire DevilOrb."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 2.5f,
				"Soul energy as damage."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0f,
				"Soul energy as damage per stack."
			);
			BaseRange = ConfigEntry(
				itemIdentifier, "BaseRange", 40f,
				"DevilOrb range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 0f,
				"DevilOrb range gained from item per stack."
			);
			Blacklist = ConfigEntry(
				itemIdentifier, "Blacklist", false,
				"Add AIBlacklist ItemTag to item."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList, Feedback.LogAll)) return;

			bool modified = false;

			ItemDef itemDef = RoR2Content.Items.NovaOnHeal;

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
				ModifiedItemDefCount++;
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			StoreSoulEnergyHook();
			DisableDefaultHook();
			DevilOrbHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("STORE_SOULENERGY", "Store {0} of healing as <style=cIsHealing>Soul Energy</style>.");
				RegisterFragment("SOULENERGY_THRESHOLD", "\nAfter your <style=cIsHealing>Soul Energy</style> reaches {0} of your <style=cIsHealing>maximum health</style>,");
				RegisterFragment("DEVILORB", " fire a <style=cIsDamage>skull</style> at an enemy within {0} that deals {1} of your <style=cIsHealing>Soul Energy</style> as damage.");
				RegisterToken("ITEM_NOVAONHEAL_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output;
			if (StoreSoulEnergyHookSuccess)
			{
				output = String.Format(
					TextFragment("STORE_SOULENERGY"),
					ScalingText(BaseStore.Value, StackStore.Value, "percent", "cIsHealing")
				);
			}
			else
			{
				output = String.Format(
					TextFragment("STORE_SOULENERGY"),
					ScalingText(1f, 1f, "percent", "cIsHealing")
				);
			}

			if (DisableDefaultHookSuccess)
			{
				output += String.Format(
					TextFragment("SOULENERGY_THRESHOLD"),
					ScalingText(ThresholdFraction.Value, "percent", "cIsHealing")
				);
				output += String.Format(
					TextFragment("DEVILORB"),
					ScalingText(BaseRange.Value, StackRange.Value, "distance", "cIsDamage"),
					ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsHealing")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("SOULENERGY_THRESHOLD"),
					ScalingText(0.1f, "percent", "cIsHealing")
				);
				output += String.Format(
					TextFragment("DEVILORB"),
					ScalingText(40f, "distance", "cIsDamage"),
					ScalingText(2.5f, "percent", "cIsHealing")
				);
			}

			return output;
		}



		private static void StoreSoulEnergyHook()
		{
			IL.RoR2.HealthComponent.Heal += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdarg(0), // used by stfld devilOrbHealPool
					x => x.MatchLdarg(0), // used by delegate
					// Removed Instructions
					x => x.MatchLdfld<HealthComponent>("devilOrbHealPool"),
					x => x.MatchLdarg(1),
					x => x.MatchLdarg(0),
					x => x.MatchLdflda<HealthComponent>("itemCounts"),
					x => x.MatchLdfld("RoR2.HealthComponent/ItemCounts", "novaOnHeal"),
					x => x.MatchConvR4(),
					x => x.MatchMul(),
					x => x.MatchAdd(),
					x => x.MatchLdarg(0),
					x => x.MatchCallOrCallvirt<HealthComponent>("get_fullCombinedHealth"),
					x => x.MatchCallOrCallvirt<Mathf>("Min"),
					// ---
					x => x.MatchStfld<HealthComponent>("devilOrbHealPool")
				);

				if (found)
				{
					c.Index += 2;

					c.RemoveRange(11);

					c.Emit(OpCodes.Ldarg, 1);
					c.EmitDelegate<Func<HealthComponent, float, float>>((healthComponent, amount) =>
					{
						int count = healthComponent.itemCounts.novaOnHeal;

						float mult = BaseStore.Value + StackStore.Value * (count - 1);
						float capacity = BaseCapacity.Value + StackCapacity.Value * (count - 1);

						float pool = healthComponent.devilOrbHealPool + amount * mult;
						return Mathf.Min(pool, healthComponent.fullCombinedHealth * capacity);
					});

					StoreSoulEnergyHookSuccess = true;
				}
				else
				{
					LogWarn(itemIdentifier + " :: StoreSoulEnergyHook Failed!");
				}
			};
		}

		private static void DisableDefaultHook()
		{
			IL.RoR2.HealthComponent.ServerFixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<HealthComponent>("devilOrbHealPool"),
					x => x.MatchLdcR4(0f),
					x => x.MatchBleUn(out _)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldc_R4, -1f);

					DisableDefaultHookSuccess = true;
				}
				else
				{
					LogWarn(itemIdentifier + " :: DisableDefaultHook Failed!");
				}
			};
		}

		private static void DevilOrbHook()
		{
			On.RoR2.HealthComponent.ServerFixedUpdate += (orig, self) =>
			{
				orig(self);

				if (self.alive && DisableDefaultHookSuccess)
				{
					if (self.devilOrbHealPool > 0f)
					{
						float threshold = self.fullCombinedHealth * ThresholdFraction.Value;
						if (self.devilOrbHealPool > threshold)
						{
							self.devilOrbTimer -= Time.fixedDeltaTime;

							if (self.devilOrbTimer <= 0f)
							{
								self.devilOrbHealPool -= threshold;
								self.devilOrbTimer += 0.1f;

								int count = Mathf.Max(self.itemCounts.novaOnHeal, 1);

								float damageMult = BaseDamage.Value + StackDamage.Value * (count - 1);
								float range = BaseRange.Value + StackRange.Value * (count - 1);

								FireDevilOrb(self, threshold * damageMult, range);
							}
						}
					}
				}
			};
		}

		private static void FireDevilOrb(HealthComponent healthComponent, float damage, float range)
		{
			CharacterBody body = healthComponent.body;
			if (body)
			{
				DevilOrb devilOrb = new DevilOrb();
				// - was causing nullref with bottled chaos? when initialization simplified. didnt even have a nkuhanas. wat.
				devilOrb.origin = body.aimOriginTransform.position;
				devilOrb.damageValue = damage;
				devilOrb.teamIndex = TeamComponent.GetObjectTeam(healthComponent.gameObject);
				devilOrb.attacker = healthComponent.gameObject;
				devilOrb.damageColorIndex = DamageColorIndex.Poison;
				devilOrb.scale = 1f;
				devilOrb.procChainMask.AddProc(ProcType.HealNova);
				devilOrb.effectType = DevilOrb.EffectType.Skull;

				HurtBox hurtBox = devilOrb.PickNextTarget(devilOrb.origin, range);
				if (hurtBox)
				{
					devilOrb.target = hurtBox;
					devilOrb.isCrit = Util.CheckRoll(body.crit, body.master);
					OrbManager.instance.AddOrb(devilOrb);
				}
			}
		}
	}
}
