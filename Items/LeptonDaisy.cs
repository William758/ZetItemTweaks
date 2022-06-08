using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using EntityStates;
using EntityStates.TeleporterHealNovaController;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class LeptonDaisy
	{
		internal static BuffDef RegenBuff;

		public static GameObject NovaGenerator;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "LeptonDaisy";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<int> PulseMode { get; set; }
		public static ConfigEntry<float> MinPulsePeriod { get; set; }
		public static ConfigEntry<bool> Continuous { get; set; }
		public static ConfigEntry<int> BaseCount { get; set; }
		public static ConfigEntry<int> StackCount { get; set; }
		public static ConfigEntry<bool> CountNormalization { get; set; }
		public static ConfigEntry<float> BaseInterval { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }
		public static ConfigEntry<float> BasePulseHeal { get; set; }
		public static ConfigEntry<float> StackPulseHeal { get; set; }
		public static ConfigEntry<float> BaseHoldoutRegen { get; set; }
		public static ConfigEntry<float> StackHoldoutRegen { get; set; }
		public static ConfigEntry<float> BaseHoldoutRegenFraction { get; set; }
		public static ConfigEntry<float> StackHoldoutRegenFraction { get; set; }



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
			PulseMode = ConfigEntry(
				itemIdentifier, "PulseMode", 2,
				"Healing pulse mode. 0 = Disabled, 1 = Count, 2 = Interval."
			);
			MinPulsePeriod = ConfigEntry(
				itemIdentifier, "MinPulsePeriod", 5f,
				"Must wait at least this many seconds between healing pulses."
			);
			Continuous = ConfigEntry(
				itemIdentifier, "Continuous", true,
				"Continue to fire healing pulses after the charging phase if the holdout zone is still active. Will still fire healing pulses while in continuous phase even if nobody is within holdout zone."
			);
			BaseCount = ConfigEntry(
				itemIdentifier, "BaseCount", 4,
				"Healing pulses during holdout from item. Will evenly distribute healing pulses during the charging phase. Used in Count mode."
			);
			StackCount = ConfigEntry(
				itemIdentifier, "StackCount", 0,
				"Healing pulses during holdout from item per stack. Used in Count mode."
			);
			CountNormalization = ConfigEntry(
				itemIdentifier, "CountNormalization", true,
				"Scale pulse count by holdout zone base duration. Assumes a baseline duration of 60 seconds. So a holdout zone with a base duration of 30 seconds will have its pulse count cut in half. Used in Count mode."
			);
			BaseInterval = ConfigEntry(
				itemIdentifier, "BaseInterval", 15f,
				"Healing pulse interval. Will evenly distribute healing pulses during the charging phase. Used in Interval mode."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0f,
				"Healing pulse interval reduction per stack. Used in Interval mode."
			);
			BasePulseHeal = ConfigEntry(
				itemIdentifier, "BasePulseHeal", 0.1f,
				"Pulse heal fraction from item."
			);
			StackPulseHeal = ConfigEntry(
				itemIdentifier, "StackPulseHeal", 0.025f,
				"Pulse heal fraction from item per stack."
			);
			BaseHoldoutRegenFraction = ConfigEntry(
				itemIdentifier, "BaseHoldoutRegenFraction", 0f,
				"Health percent regeneration while in holdout zone gained from item. 0.01 = 1% regeneration."
			);
			StackHoldoutRegenFraction = ConfigEntry(
				itemIdentifier, "StackHoldoutRegenFraction", 0f,
				"Health percent regeneration while in holdout zone gained from item per stack."
			);
			BaseHoldoutRegen = ConfigEntry(
				itemIdentifier, "BaseHoldoutRegen", 4.8f,
				"Health regeneration while in holdout zone gained from item."
			);
			StackHoldoutRegen = ConfigEntry(
				itemIdentifier, "StackHoldoutRegen", 2.4f,
				"Health regeneration while in holdout zone gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			NovaGenerator = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/TeleporterHealNovaGenerator");

			HealHook();
			StopDeletingNovaGenHook();
			UpdateGeneratorHook();
			HoldoutAuraHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("HEALNOVA_AMOUNT", "\nRelease <style=cIsHealing>novas</style> while inside a holdout zone, healing for {0} maximum health.");
				RegisterFragment("HEALNOVA_COUNT", "\nOccurs {0} times.");
				RegisterFragment("HEALNOVA_COUNT_NORMALIZED", "\nOccurs {0} times every 60 seconds.");
				RegisterFragment("HEALNOVA_INTERVAL", "\nOccurs {0}.");
				RegisterFragment("HEALNOVA_INTERVAL_REDUCE", "\nOccurs {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterFragment("HEALNOVA_CONTINUOUS", "\n<style=cStack>(Continues to pulse after charging phase)</style>");
				RegisterFragment("HOLDOUT_REGENERATION", "\nIncreases <style=cIsHealing>health regeneration</style> by {0} while inside a holdout zone.");
				RegisterToken("ITEM_TPHEALINGNOVA_DESC", DescriptionText());
				RegisterToken("ITEM_TPHEALINGNOVA_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (PulseMode.Value >= 0)
			{
				output += String.Format(
					TextFragment("HEALNOVA_AMOUNT", true),
					ScalingText(BasePulseHeal.Value, StackPulseHeal.Value, "percent", "cIsHealing")
				);

				if (PulseMode.Value >= 2)
				{
					if (StackReduction.Value == 0f)
					{
						output += String.Format(
							TextFragment("HEALNOVA_INTERVAL"),
							SecondText(BaseInterval.Value, "every", "cIsHealing")
						);
					}
					else
					{
						output += String.Format(
							TextFragment("HEALNOVA_INTERVAL_REDUCE"),
							SecondText(BaseInterval.Value, "every", "cIsHealing"),
							ScalingText(StackReduction.Value, "percent")
						);
					}
				}
				else if (PulseMode.Value == 1)
				{
					if (CountNormalization.Value)
					{
						output += String.Format(
							TextFragment("HEALNOVA_COUNT_NORMALIZED"),
							ScalingText(BaseCount.Value, StackCount.Value, "flat", "cIsHealing")
						);
					}
					else
					{
						output += String.Format(
							TextFragment("HEALNOVA_COUNT"),
							ScalingText(BaseCount.Value, StackCount.Value, "flat", "cIsHealing")
						);
					}
				}

				if (Continuous.Value) output += TextFragment("HEALNOVA_CONTINUOUS");
			}

			if (BaseHoldoutRegenFraction.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("HOLDOUT_REGENERATION", true),
					ScalingText(BaseHoldoutRegenFraction.Value, StackHoldoutRegenFraction.Value, "percentregen", "cIsHealing")
				);
			}
			if (BaseHoldoutRegen.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("HOLDOUT_REGENERATION", true),
					ScalingText(BaseHoldoutRegen.Value, StackHoldoutRegen.Value, "flatregen", "cIsHealing")
				);
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}

		private static string PickupText()
		{
			if (PulseMode.Value > 0)
			{
				return "Holdout zones periodically release a healing nova.";
			}
			if (BaseHoldoutRegen.Value > 0f || BaseHoldoutRegenFraction.Value > 0f)
			{
				return "Regenerate health while inside a holdout zone.";
			}

			return "No effect.";
		}



		private static int GetTeamItemCount(TeamIndex teamIndex)
		{
			return Util.GetItemCountForTeam(teamIndex, RoR2Content.Items.TPHealingNova.itemIndex, false, true);
		}

		private static void HealHook()
		{
			IL.EntityStates.TeleporterHealNovaController.TeleporterHealNovaPulse.OnEnter += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(0.5f)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 1);
					c.EmitDelegate<Func<TeamIndex, float>>((teamIndex) =>
					{
						int count = GetTeamItemCount(teamIndex);

						return BasePulseHeal.Value + StackPulseHeal.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: HealHook Failed!");
				}
			};
		}

		private static void StopDeletingNovaGenHook()
		{
			On.RoR2.HoldoutZoneController.UpdateHealingNovas += (orig, self, charging) =>
			{
				if (self.applyHealingNova)
				{
					bool isActive = false;

					bool teamHasItem = GetTeamItemCount(TeamIndex.Player) > 0;
					if (teamHasItem && charging)
					{
						isActive = true;

						if (NetworkServer.active)
						{
							ref GameObject novaGen = ref self.healingNovaGeneratorsByTeam[(int)TeamIndex.Player];
							if (teamHasItem && !novaGen)
							{
								if (Continuous.Value || self.charge < 1f)
								{
									novaGen = UnityEngine.Object.Instantiate(NovaGenerator, self.healingNovaRoot ?? self.transform);
									novaGen.GetComponent<TeamFilter>().teamIndex = TeamIndex.Player;
									NetworkServer.Spawn(novaGen);
								}
							}
						}
					}

					if (self.healingNovaItemEffect)
					{
						self.healingNovaItemEffect.SetActive(isActive);
					}
				}
			};
		}

		private static void UpdateGeneratorHook()
		{
			On.EntityStates.TeleporterHealNovaController.TeleporterHealNovaGeneratorMain.FixedUpdate += (orig, self) =>
			{
				if (NetworkServer.active && Time.fixedDeltaTime > 0f)
				{
					bool fullyCharged = self.holdoutZone.charge >= 1f;

					if (!self.holdoutZone || (fullyCharged && !Continuous.Value))
					{
						EntityState.Destroy(self.outer.gameObject);
						return;
					}

					if (PulseMode.Value <= 0) return;

					//LogInfo(self.secondsUntilPulseAvailable);

					if (self.holdoutZone.isActiveAndEnabled)
					{
						if (self.secondsUntilPulseAvailable > 0f)
						{
							self.secondsUntilPulseAvailable -= Time.fixedDeltaTime;
						}
						else
						{
							int count = GetTeamItemCount(self.teamIndex);
							if (count <= 0) return;

							float baseDuration = self.holdoutZone.baseChargeDuration;

							if (PulseMode.Value >= 2)
							{
								if (!fullyCharged)
								{
									float interval = BaseInterval.Value * Mathf.Pow(1f - StackReduction.Value, count - 1);
									int pulseCount = Mathf.Max(1, Mathf.RoundToInt((baseDuration / interval) + 0.05f));

									float nextFraction = TeleporterHealNovaGeneratorMain.CalculateNextPulseFraction(pulseCount, self.previousPulseFraction);
									if (nextFraction < self.holdoutZone.charge)
									{
										self.Pulse();
										self.pulseCount++;

										self.previousPulseFraction = nextFraction;
										self.secondsUntilPulseAvailable = MinPulsePeriod.Value;
									}
								}
								else
								{
									float interval = BaseInterval.Value * Mathf.Pow(1f - StackReduction.Value, count - 1);
									self.secondsUntilPulseAvailable = Mathf.Max(interval, MinPulsePeriod.Value);

									if (self.pulseCount <= 0)
									{
										self.pulseCount = 1;
										return;
									}

									self.Pulse();
									self.pulseCount++;
								}
							}
							else
							{
								int pulseCount = BaseCount.Value + StackCount.Value * (count - 1);
								if (CountNormalization.Value) pulseCount = Mathf.RoundToInt((pulseCount * (baseDuration / 60f)) + 0.05f);
								pulseCount = Mathf.Max(1, pulseCount);

								if (!fullyCharged)
								{
									float nextFraction = TeleporterHealNovaGeneratorMain.CalculateNextPulseFraction(pulseCount, self.previousPulseFraction);
									if (nextFraction < self.holdoutZone.charge)
									{
										self.Pulse();
										self.pulseCount++;

										self.previousPulseFraction = nextFraction;
										self.secondsUntilPulseAvailable = MinPulsePeriod.Value;
									}
								}
								else
								{
									float interval = baseDuration / pulseCount;
									self.secondsUntilPulseAvailable = Mathf.Max(interval, MinPulsePeriod.Value);

									if (self.pulseCount <= 0)
									{
										self.pulseCount = 1;
										return;
									}

									self.Pulse();
									self.pulseCount++;
								}
							}
						}
					}
				}
			};
		}

		private static void HoldoutAuraHook()
		{
			On.RoR2.HoldoutZoneController.Awake += (orig, self) =>
			{
				orig(self);

				if (NetworkServer.active && self)
				{
					HoldoutZoneAura holdoutAura = self.gameObject.AddComponent<HoldoutZoneAura>();
					holdoutAura.holdoutZone = self;
					holdoutAura.buffDef = RegenBuff;
					holdoutAura.trigger = (HoldoutZoneAura aura) => TriggerLeptonHoldoutAura(aura);
				}
			};
		}

		private static bool TriggerLeptonHoldoutAura(HoldoutZoneAura holdoutAura)
		{
			int itemCount = GetTeamItemCount(holdoutAura.teamIndex);
			if (itemCount <= 0) return false;
			holdoutAura.buffCount = itemCount;

			if (BaseHoldoutRegen.Value <= 0f && BaseHoldoutRegenFraction.Value <= 0f) return false;

			return true;
		}
	}



	public class HoldoutZoneAura : MonoBehaviour
	{
		public HoldoutZoneController holdoutZone;
		public BuffDef buffDef;
		public int buffCount = 1;
		public TeamIndex teamIndex = TeamIndex.Player;
		public Func<HoldoutZoneAura, bool> trigger = (HoldoutZoneAura holdoutAura) => false;

		private float timer = 0f;

		public void FixedUpdate()
		{
			timer -= Time.fixedDeltaTime;

			if (timer <= 0f)
			{
				timer = 0.3f;

				if (holdoutZone && buffDef)
				{
					if (trigger(this) && buffCount > 0) ApplyBuffAura();
				}
			}
		}

		public void ApplyBuffAura()
		{
			bool buffCanStack = buffDef.canStack;
			CharacterBody[] zoneHolders = CollectZoneHolders();

			for (int i = 0; i < zoneHolders.Length; i++)
			{
				CharacterBody body = zoneHolders[i];

				if (buffCanStack)
				{
					SetTimedBuffStacks(body, buffDef.buffIndex, 0.85f, buffCount);
				}
				else
				{
					body.AddTimedBuff(buffDef, 0.85f);
				}
			}
		}

		public CharacterBody[] CollectZoneHolders()
		{
			ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(teamIndex);
			List<CharacterBody> bodyList = new List<CharacterBody>();

			for (int i = 0; i < teamMembers.Count; i++)
			{
				TeamComponent teamComponent = teamMembers[i];
				CharacterBody body = teamComponent.body;

				if (body && holdoutZone.IsBodyInChargingRadius(body))
				{
					bodyList.Add(body);
				}
			}

			return bodyList.ToArray();
		}
	}
}
