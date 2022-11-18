using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class BensRainCoat
	{
		public static GameObject ImmunityEffectPrefab;

		public static List<string> autoCompatList = new List<string> { "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "BensRainCoat";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<int> BaseImmunity { get; set; }
		public static ConfigEntry<int> StackImmunity { get; set; }
		public static ConfigEntry<bool> ImmunityCheat { get; set; }
		public static ConfigEntry<float> ImmunityDuration { get; set; }
		public static ConfigEntry<float> ImmunityCheatDuration { get; set; }
		public static ConfigEntry<float> BaseRecharge { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }
		public static ConfigEntry<float> BaseHealth { get; set; }
		public static ConfigEntry<float> StackHealth { get; set; }
		public static ConfigEntry<float> BaseHealthPercent { get; set; }
		public static ConfigEntry<float> StackHealthPercent { get; set; }
		public static ConfigEntry<float> BaseBarrierRecovery { get; set; }
		public static ConfigEntry<float> StackBarrierRecovery { get; set; }
		public static ConfigEntry<float> BaseSafeRegenFraction { get; set; }
		public static ConfigEntry<float> StackSafeRegenFraction { get; set; }
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
			BaseImmunity = ConfigEntry(
				itemIdentifier, "BaseImmunity", 2,
				"Maximum immunity charges gained from item."
			);
			StackImmunity = ConfigEntry(
				itemIdentifier, "StackImmunity", 1,
				"Maximum immunity charges gained from item per stack."
			);
			ImmunityCheat = ConfigEntry(
				itemIdentifier, "ImmunityCheat", false,
				"Debuff immunity is always active."
			);
			ImmunityDuration = ConfigEntry(
				itemIdentifier, "ImmunityDuration", 0.25f,
				"Debuff immunity duration when protection is triggered."
			);
			ImmunityCheatDuration = ConfigEntry(
				itemIdentifier, "ImmunityCheatDuration", 1f,
				"Serves as barrier recovery cooldown while cheat enabled."
			);
			BaseRecharge = ConfigEntry(
				itemIdentifier, "BaseRecharge", 5f,
				"Recharge interval for gaining immunity charge."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0.1f,
				"Recharge interval reduction per stack."
			);
			BaseHealth = ConfigEntry(
				itemIdentifier, "BaseHealth", 0f,
				"Health gained from item."
			);
			StackHealth = ConfigEntry(
				itemIdentifier, "StackHealth", 0f,
				"Health gained from item per stack."
			);
			BaseHealthPercent = ConfigEntry(
				itemIdentifier, "BaseHealthPercent", 0f,
				"Health percent gained from item."
			);
			StackHealthPercent = ConfigEntry(
				itemIdentifier, "StackHealthPercent", 0f,
				"Health percent gained from item per stack."
			);
			BaseBarrierRecovery = ConfigEntry(
				itemIdentifier, "BaseBarrierRecovery", 0.1f,
				"Barrier recovery fraction when protection is triggered."
			);
			StackBarrierRecovery = ConfigEntry(
				itemIdentifier, "StackBarrierRecovery", 0f,
				"Barrier recovery fraction per stack when protection is triggered."
			);
			BaseSafeRegenFraction = ConfigEntry(
				itemIdentifier, "BaseSafeRegenFraction", 0f,
				"Health regeneration fraction gained while safe from item. 0.01 = 1% regeneration."
			);
			StackSafeRegenFraction = ConfigEntry(
				itemIdentifier, "StackSafeRegenFraction", 0f,
				"Health regeneration fraction gained while safe from item per stack."
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

			ItemDef itemDef = DLC1Content.Items.ImmuneToDebuff;

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

			DLC1Content.Buffs.ImmuneToDebuffCooldown.canStack = true;

			ImmunityEffectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/ImmuneToDebuff/ImmuneToDebuffEffect.prefab").WaitForCompletion();

			// Attach ImmunityTracker on server only!
			AttachTrackerHook();

			DisableDefaultHook();
			TryApplyOverrideHook();

			HandleCleanseHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("IMMUNITY_CHEAT", "Become immune to <style=cIsDamage>debuffs</style>.");
				RegisterFragment("IMMUNITY_CHEAT_BARRIER", "Prevents <style=cIsDamage>debuffs</style> and instead generates {0} <style=cIsHealing>barrier</style>.");
				RegisterFragment("IMMUNITY_CHARGES", "Maximum of {0} <style=cIsUtility>cleanse</style> charges.");
				RegisterFragment("IMMUNITY_RECHARGE", "\nGain a charge {0}.");
				RegisterFragment("IMMUNITY_RECHARGE_REDUCE", "\nGain a charge {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterFragment("IMMUNITY_PROTECTION", "\nEach charge prevents <style=cIsDamage>debuffs</style> {0}.");
				RegisterFragment("IMMUNITY_PROTECTION_BARRIER", "\nEach charge prevents <style=cIsDamage>debuffs</style> {0} and generates {1} <style=cIsHealing>barrier</style>.");
				RegisterFragment("SAFE_REGENERATION", "\nIncreases <style=cIsHealing>health regeneration</style> by {0} while out of danger.");
				RegisterFragment("BEN_PICKUP_CHEATBARRIER", "Prevent debuffs, instead gaining a temporary barrier.");
				RegisterFragment("BEN_PICKUP_CHEAT", "Become immune to debuffs.");
				RegisterFragment("BEN_PICKUP_CHARGEBARRIER", "Prevent debuffs, instead gaining a temporary barrier. Recharges over time.");
				RegisterFragment("BEN_PICKUP_CHARGE", "Prevent debuffs. Recharges over time.");
				RegisterToken("ITEM_IMMUNETODEBUFF_DESC", DescriptionText());
				RegisterToken("ITEM_IMMUNETODEBUFF_PICKUP", PickupText());

				targetLanguage = "pt-BR";

				RegisterFragment("IMMUNITY_CHEAT", "Se torne imune a <style=cIsDamage>penalidades</style>.");
				RegisterFragment("IMMUNITY_CHEAT_BARRIER", "Previne <style=cIsDamage>penalidades</style> e, em troca, concede {0} de <style=cIsHealing>barreira</style>.");
				RegisterFragment("IMMUNITY_CHARGES", "Máximo de {0} cargas de <style=cIsUtility>purificação</style>.");
				RegisterFragment("IMMUNITY_RECHARGE", "\nGanhe uma carga de {0}.");
				RegisterFragment("IMMUNITY_RECHARGE_REDUCE", "\nGanhe uma carga de {0} <style=cStack>(-{1} por acúmulo)</style>.");
				RegisterFragment("IMMUNITY_PROTECTION", "\nCada carga previne <style=cIsDamage>penalidades</style> {0}.");
				RegisterFragment("IMMUNITY_PROTECTION_BARRIER", "\nCada carga previne <style=cIsDamage>penalidades</style> {0} e gera uma {1} <style=cIsHealing>barreira</style>.");
				RegisterFragment("SAFE_REGENERATION", "\nAumenta a <style=cIsHealing>regeneração de saúde</style> em {0} enquanto fora de perigo.");
				RegisterFragment("BEN_PICKUP_CHEATBARRIER", "Previne penalidades, e, em troca, concede uma barreira temporária.");
				RegisterFragment("BEN_PICKUP_CHEAT", "Se torna imune a penalidades.");
				RegisterFragment("BEN_PICKUP_CHARGEBARRIER", "Previne penalidades, e, em troca, concede uma barreira temporária. Recharrega ao longo do temp.");
				RegisterFragment("BEN_PICKUP_CHARGE", "Previne penalidades. Recharrega ao longo do tempo.");
				RegisterToken("ITEM_IMMUNETODEBUFF_DESC", DescriptionText());
				RegisterToken("ITEM_IMMUNETODEBUFF_PICKUP", PickupText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";
			if (BaseHealth.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_HEALTH", true),
					ScalingText(BaseHealth.Value, StackHealth.Value, "flat", "cIsHealing")
				);
			}
			if (BaseHealthPercent.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_HEALTH", true),
					ScalingText(BaseHealthPercent.Value, StackHealthPercent.Value, "percent", "cIsHealing")
				);
			}
			if (ImmunityCheat.Value)
			{
				if (output != "") output += "\n";

				if (BaseBarrierRecovery.Value > 0f)
				{
					output += String.Format(
						TextFragment("IMMUNITY_CHEAT_BARRIER"),
						ScalingText(BaseBarrierRecovery.Value, StackBarrierRecovery.Value, "percent", "cIsHealing")
					);
				}
				else
				{
					output += TextFragment("IMMUNITY_CHEAT");
				}
			}
			else
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("IMMUNITY_CHARGES"),
					ScalingText(BaseImmunity.Value, StackImmunity.Value, "flat", "cIsUtility")
				);

				if (StackReduction.Value == 0f)
				{
					output += String.Format(
						TextFragment("IMMUNITY_RECHARGE"),
						SecondText(BaseRecharge.Value, "every", "cIsUtility")
					);
				}
				else
				{
					output += String.Format(
						TextFragment("IMMUNITY_RECHARGE_REDUCE"),
						SecondText(BaseRecharge.Value, "every", "cIsUtility"),
						ScalingText(StackReduction.Value, "percent")
					);
				}

				if (BaseBarrierRecovery.Value > 0f)
				{
					output += String.Format(
						TextFragment("IMMUNITY_PROTECTION_BARRIER"),
						SecondText(ImmunityDuration.Value, "for"),
						ScalingText(BaseBarrierRecovery.Value, StackBarrierRecovery.Value, "percent", "cIsHealing")
					);
				}
				else
				{
					output += String.Format(
						TextFragment("IMMUNITY_PROTECTION"),
						SecondText(ImmunityDuration.Value, "for")
					);
				}
			}
			if (BaseSafeRegenFraction.Value > 0f)
			{
				output += String.Format(
					TextFragment("SAFE_REGENERATION"),
					ScalingText(BaseSafeRegenFraction.Value, StackSafeRegenFraction.Value, "percentregen", "cIsHealing")
				);
			}

			return output;
		}

		private static string PickupText()
		{
			if (ImmunityCheat.Value)
			{
				if (BaseBarrierRecovery.Value > 0f)
				{
					return TextFragment("BEN_PICKUP_CHEATBARRIER");
				}
				else
				{
					return TextFragment("BEN_PICKUP_CHEAT");
				}
			}
			else
			{
				if (BaseBarrierRecovery.Value > 0f)
				{
					return TextFragment("BEN_PICKUP_CHARGEBARRIER");
				}
				else
				{
					return TextFragment("BEN_PICKUP_CHARGE");
				}
			}
		}



		private static void AttachTrackerHook()
		{
			On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
			{
				orig(self);

				if (NetworkServer.active && self)
				{
					UpdateImmunityTracker(self);
				}
			};
		}

		private static void UpdateImmunityTracker(CharacterBody self)
		{
			Inventory inventory = self.inventory;
			if (inventory)
			{
				int count = inventory.GetItemCount(DLC1Content.Items.ImmuneToDebuff);

				ImmunityTracker immunityTracker = self.GetComponent<ImmunityTracker>();
				if (!immunityTracker && count > 0)
				{
					immunityTracker = self.gameObject.AddComponent<ImmunityTracker>();
					immunityTracker.body = self;
				}

				if (immunityTracker)
				{
					immunityTracker.UpdateStacks(count);
				}
			}
		}



		private static void DisableDefaultHook()
		{
			On.RoR2.Items.ImmuneToDebuffBehavior.OnEnable += (orig, self) => { };
			On.RoR2.Items.ImmuneToDebuffBehavior.OnDisable += (orig, self) => { };
			On.RoR2.Items.ImmuneToDebuffBehavior.FixedUpdate += (orig, self) => { };
		}

		private static void TryApplyOverrideHook()
		{
			On.RoR2.Items.ImmuneToDebuffBehavior.TryApplyOverride += (orig, body) =>
			{
				ImmunityTracker immunityTracker = body.GetComponent<ImmunityTracker>();
				if (immunityTracker)
				{
					if (immunityTracker.isProtected) return true;

					if (immunityTracker.canProtect)
					{
						immunityTracker.TriggerProtection();

						return true;
					}
				}

				return false;
			};
		}



		private static void HandleCleanseHook()
		{
			On.RoR2.Util.CleanseBody += (orig, body, debuff, buff, cooldown, dot, stun, proj) =>
			{
				if (body && cooldown)
				{
					ImmunityTracker immunityTracker = body.GetComponent<ImmunityTracker>();
					if (immunityTracker)
					{
						immunityTracker.CleanseCooldown();
					}
				}

				orig(body, debuff, buff, cooldown, dot, stun, proj);
			};
		}
	}



	// should only ever exist on server
	public class ImmunityTracker : MonoBehaviour
	{
		public CharacterBody body;

		public bool isProtected = false;
		public bool canProtect = false;

		private int ItemCount = 0;
		private float RecoveryFraction = 0f;

		private bool ProtectionCheat = false;
		private int ProtectionLimit = 0;
		private int ProtectionCount = 0;

		private float ProtectionDuration = 0.25f;
		private float ProtectionTimer = 0f;

		private float RechargeInterval = 5f;
		private float RechargeTimer = 0f;



		public void FixedUpdate()
		{
			float fixedDeltaTime = Time.fixedDeltaTime;

			isProtected = ProtectionTimer > 0f;

			ProtectionTimer = Mathf.Max(0f, ProtectionTimer - fixedDeltaTime);

			int cooldownDisplay = 0;
			int protectionDisplay = 0;

			if (ProtectionCheat)
			{
				canProtect = ProtectionLimit > 0;

				ProtectionCount = 0;
				RechargeTimer = RechargeInterval;
			}
			else
			{
				if (ProtectionCount < ProtectionLimit)
				{
					if (!isProtected)
					{
						RechargeTimer -= fixedDeltaTime;
					}

					if (RechargeTimer <= 0f)
					{
						ProtectionCount++;

						RechargeTimer += RechargeInterval;
					}
				}
				else
				{
					RechargeTimer = RechargeInterval;
				}

				if (ProtectionLimit > 0)
				{
					if (ProtectionCount > 0)
					{
						canProtect = true;

						protectionDisplay = ProtectionCount;
					}
					else
					{
						canProtect = false;

						cooldownDisplay = Mathf.CeilToInt(RechargeTimer);
					}
				}
				else
				{
					canProtect = false;
				}
			}

			int buffCount = body.GetBuffCount(DLC1Content.Buffs.ImmuneToDebuffCooldown);
			if (buffCount != cooldownDisplay)
			{
				body.SetBuffCount(DLC1Content.Buffs.ImmuneToDebuffCooldown.buffIndex, cooldownDisplay);
			}

			buffCount = body.GetBuffCount(DLC1Content.Buffs.ImmuneToDebuffReady);
			if (buffCount != protectionDisplay)
			{
				body.SetBuffCount(DLC1Content.Buffs.ImmuneToDebuffReady.buffIndex, protectionDisplay);
			}
		}


		public void UpdateStacks(int count)
		{
			if (ItemCount != count)
			{
				if (count > 0)
				{
					ProtectionLimit = BensRainCoat.BaseImmunity.Value + BensRainCoat.StackImmunity.Value * (count - 1);
					RechargeInterval = BensRainCoat.BaseRecharge.Value * Mathf.Pow(1f - BensRainCoat.StackReduction.Value, count - 1);
					ProtectionCheat = BensRainCoat.ImmunityCheat.Value;

					if (BensRainCoat.BaseBarrierRecovery.Value > 0f)
					{
						RecoveryFraction = BensRainCoat.BaseBarrierRecovery.Value + BensRainCoat.StackBarrierRecovery.Value * (count - 1);
					}
					else
					{
						RecoveryFraction = 0f;
					}
				}
				else
				{
					ProtectionLimit = 0;
					RechargeInterval = BensRainCoat.BaseRecharge.Value;
					ProtectionCheat = false;

					RecoveryFraction = 0f;
				}

				if (ProtectionCheat)
				{
					ProtectionDuration = BensRainCoat.ImmunityCheatDuration.Value;
				}
				else
				{
					ProtectionDuration = BensRainCoat.ImmunityDuration.Value;
				}

				if (ProtectionCount > ProtectionLimit)
				{
					ProtectionCount = ProtectionLimit;
					RechargeTimer = RechargeInterval;
				}

				// OnEnable Equivalent
				if (ItemCount == 0 && count > 0)
				{
					ProtectionCount = 1;
					RechargeTimer = RechargeInterval;
				}

				ItemCount = count;
			}
		}

		public void TriggerProtection()
		{
			isProtected = true;

			ProtectionCount--;
			ProtectionTimer = ProtectionDuration;

			if (BensRainCoat.ImmunityEffectPrefab)
			{
				EffectManager.SimpleImpactEffect(BensRainCoat.ImmunityEffectPrefab, body.corePosition, Vector3.up, true);
			}

			if (RecoveryFraction > 0f)
			{
				HealthComponent healthComponent = body.healthComponent;
				if (healthComponent)
				{
					healthComponent.AddBarrier(RecoveryFraction * healthComponent.fullCombinedHealth);
				}
			}
		}

		public void CleanseCooldown()
		{
			if (!ProtectionCheat)
			{
				if (ProtectionCount <= 0 && ProtectionLimit > 0)
				{
					canProtect = true;

					ProtectionCount = 1;
					RechargeTimer = RechargeInterval;

					body.SetBuffCount(DLC1Content.Buffs.ImmuneToDebuffCooldown.buffIndex, 0);
					body.SetBuffCount(DLC1Content.Buffs.ImmuneToDebuffReady.buffIndex, 1);
				}
			}
		}
	}
}
