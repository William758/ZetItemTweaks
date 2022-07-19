using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class RoseBuckler
	{
		internal static BuffDef MomentumBuff;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.Ben.BenBalanceMod" };

		public static string itemIdentifier = "RoseBuckler";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<int> MaxMomentum { get; set; }
		public static ConfigEntry<float> SprintThreshold { get; set; }
		public static ConfigEntry<float> ChargeInterval { get; set; }
		public static ConfigEntry<float> DecayInterval { get; set; }
		public static ConfigEntry<float> BaseMove { get; set; }
		public static ConfigEntry<float> StackMove { get; set; }
		public static ConfigEntry<float> BaseMomentumMove { get; set; }
		public static ConfigEntry<float> StackMomentumMove { get; set; }
		public static ConfigEntry<float> BaseArmor { get; set; }
		public static ConfigEntry<float> StackArmor { get; set; }
		public static ConfigEntry<float> BaseMomentumArmor { get; set; }
		public static ConfigEntry<float> StackMomentumArmor { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnBuffCatalogPreInit += ModifyBuff;
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
			MaxMomentum = ConfigEntry(
				itemIdentifier, "MaxMomentum", 5,
				"Maximum momentum stacks. Full momentum effect at max stacks."
			);
			SprintThreshold = ConfigEntry(
				itemIdentifier, "SprintThreshold", 0.1f,
				"Time after not sprinting to start momentum decay."
			);
			ChargeInterval = ConfigEntry(
				itemIdentifier, "ChargeInterval", 0.4f,
				"Momentum stack charge time while sprinting. 0 = instant."
			);
			DecayInterval = ConfigEntry(
				itemIdentifier, "DecayInterval", 0.2f,
				"Momentum stack decay time while not sprinting. 0 = instant."
			);
			BaseMove = ConfigEntry(
				itemIdentifier, "BaseMove", 0f,
				"Movement speed gained from item."
			);
			StackMove = ConfigEntry(
				itemIdentifier, "StackMove", 0f,
				"Movement speed gained from item per stack."
			);
			BaseMomentumMove = ConfigEntry(
				itemIdentifier, "BaseMomentumMove", 0.2f,
				"Movement speed gained from maximum momentum."
			);
			StackMomentumMove = ConfigEntry(
				itemIdentifier, "StackMomentumMove", 0.1f,
				"Movement speed gained from maximum momentum per stack."
			);
			BaseArmor = ConfigEntry(
				itemIdentifier, "BaseArmor", 0f,
				"Armor gained from item."
			);
			StackArmor = ConfigEntry(
				itemIdentifier, "StackArmor", 0f,
				"Armor gained from item per stack."
			);
			BaseMomentumArmor = ConfigEntry(
				itemIdentifier, "BaseMomentumArmor", 30f,
				"Armor gained from maximum momentum."
			);
			StackMomentumArmor = ConfigEntry(
				itemIdentifier, "StackMomentumArmor", 15f,
				"Armor gained from maximum momentum per stack."
			);
		}

		private static void ModifyBuff()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList, Feedback.None)) return;

			if (!UsingMomentum()) return;

			if (!PluginLoaded("com.Wolfo.WolfoQualityOfLife")) return;

			BuffDef buffDef = FindBuffDefPreCatalogInit("visual_SprintArmor");
			if (buffDef)
			{
				if (!buffDef.isHidden)
				{
					buffDef.isHidden = true;

					LogInfo(itemIdentifier + " :: Hiding buff : visual_SprintArmor");

					ModifiedBuffDefCount++;
				}
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			CharacterBody.onBodyInventoryChangedGlobal += HandleItemBehavior;

			OverrideVisualHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("SPRINT_MOMENTUM", "\n<style=cIsUtility>Sprinting</style> generates <style=cIsUtility>momentum</style>, granting up to {0}.");
				RegisterFragment("MOMENTUM_ARMOR", "{0} <style=cIsHealing>armor</style>");
				RegisterFragment("MOMENTUM_MOVESPEED", "{0} <style=cIsUtility>movement speed</style>");
				RegisterFragment("MOMENTUM_BOTH", "{0} <style=cIsUtility>movement speed</style> and {1} <style=cIsHealing>armor</style>");
				RegisterToken("ITEM_SPRINTARMOR_DESC", DescriptionText());
				RegisterToken("ITEM_SPRINTARMOR_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (MaxMomentum.Value > 0 && (BaseMomentumMove.Value > 0f || BaseMomentumArmor.Value > 0f))
			{
				string momentumEffect = "???";
				if (BaseMomentumMove.Value > 0f && BaseMomentumArmor.Value > 0f)
				{
					momentumEffect = String.Format(
						TextFragment("MOMENTUM_BOTH", true),
						ScalingText(BaseMomentumMove.Value, StackMomentumMove.Value, "percent", "cIsUtility"),
						ScalingText(BaseMomentumArmor.Value, StackMomentumArmor.Value, "flat", "cIsHealing")
					);
				}
				else if (BaseMomentumMove.Value > 0f)
				{
					momentumEffect = String.Format(
						TextFragment("MOMENTUM_MOVESPEED", true),
						ScalingText(BaseMomentumMove.Value, StackMomentumMove.Value, "percent", "cIsUtility")
					);
				}
				else if (BaseMomentumArmor.Value > 0f)
				{
					momentumEffect = String.Format(
						TextFragment("MOMENTUM_MOVESPEED", true),
						ScalingText(BaseMomentumArmor.Value, StackMomentumArmor.Value, "flat", "cIsHealing")
					);
				}

				output += String.Format(
					TextFragment("SPRINT_MOMENTUM", true),
					momentumEffect
				);
			}

			if (BaseMove.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_MOVESPEED", true),
					ScalingText(BaseMove.Value, StackMove.Value, "percent", "cIsUtility")
				);
			}
			if (BaseArmor.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_ARMOR", true),
					ScalingText(BaseArmor.Value, StackArmor.Value, "flat", "cIsHealing")
				);
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}

		private static string PickupText()
		{
			if (MaxMomentum.Value > 0 && (BaseMomentumMove.Value > 0f || BaseMomentumArmor.Value > 0f))
			{
				return "Sprinting builds momentum.";
			}

			if (BaseArmor.Value > 0f)
			{
				return "Reduce incoming damage.";
			}
			if (BaseMove.Value > 0f)
			{
				return "Increase movement speed.";
			}

			return "No effect.";
		}



		internal static bool UsingMomentum()
		{
			if (BaseMomentumMove.Value > 0f || BaseMomentumArmor.Value > 0f)
			{
				return true;
			}

			return false;
		}

		private static void HandleItemBehavior(CharacterBody body)
		{
			if (NetworkServer.active)
			{
				body.AddItemBehavior<SprintArmorBehavior>(body.inventory.GetItemCount(RoR2Content.Items.SprintArmor));
			}
		}

		private static void OverrideVisualHook()
		{
			IL.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdflda(typeof(CharacterBody).GetField("bucklerShieldTempEffectInstance", BindingFlags.Instance | BindingFlags.NonPublic))
				);

				if (found)
				{
					found = c.TryGotoNext(
						x => x.MatchCallOrCallvirt<CharacterBody>("UpdateSingleTemporaryVisualEffect")
					);

					if (found)
					{
						c.Emit(OpCodes.Pop);
						c.Emit(OpCodes.Pop);
						c.Emit(OpCodes.Ldarg, 0);
						c.EmitDelegate<Func<CharacterBody, bool>>((body) =>
						{
							return body.HasBuff(MomentumBuff);
						});
						c.Emit(OpCodes.Ldstr, "");
					}
					else
					{
						LogWarn(itemIdentifier + " :: OverrideVisualHook:USTVE Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: OverrideVisualHook:TVEI Failed!");
				}
			};
		}
	}



	public class SprintArmorBehavior : CharacterBody.ItemBehavior
	{
		public int maxMomentum = 0;
		public int currentMomentum = 0;

		public float tracker = 0f;
		public float chargeRate = 2.5f;
		public float decayRate = 5f;

		public float timeSinceLastSprint = 0f;
		public float sprintThreshold = 0.1f;

		public void Awake()
		{
			enabled = false;
		}

		public void OnEnable()
		{
			maxMomentum = RoseBuckler.UsingMomentum() ? RoseBuckler.MaxMomentum.Value : 0;

			sprintThreshold = Mathf.Max(0f, RoseBuckler.SprintThreshold.Value);

			chargeRate = RoseBuckler.ChargeInterval.Value;
			chargeRate = (chargeRate >= 0.01f) ? (1f / chargeRate) : 0f;

			decayRate = RoseBuckler.DecayInterval.Value;
			decayRate = (decayRate >= 0.01f) ? (1f / decayRate) : 0f;

			if (body)
			{
				body.SetBuffCount(RoseBuckler.MomentumBuff.buffIndex, 0);
			}

			timeSinceLastSprint = sprintThreshold + 1f;
		}

		public void OnDisable()
		{
			if (body)
			{
				body.SetBuffCount(RoseBuckler.MomentumBuff.buffIndex, 0);
			}
		}

		public void FixedUpdate()
		{
			if (body)
			{
				if (maxMomentum > 0)
				{
					float deltaTime = Time.fixedDeltaTime;

					if (body.isSprinting) timeSinceLastSprint = 0f;
					else timeSinceLastSprint += deltaTime;

					if (timeSinceLastSprint <= sprintThreshold)
					{
						if (chargeRate == 0f)
						{
							tracker = 0f;
							currentMomentum = maxMomentum;
						}
						else
						{
							if (timeSinceLastSprint == 0f)
							{
								tracker += deltaTime * chargeRate;
							}

							if (currentMomentum >= maxMomentum)
							{
								tracker = Mathf.Min(tracker, 0f);
							}
							else if (tracker >= 1f)
							{
								tracker -= 1f;
								currentMomentum++;
							}
						}
					}
					else
					{
						if (decayRate == 0f)
						{
							tracker = 0f;
							currentMomentum = 0;
						}
						else
						{
							tracker -= deltaTime * decayRate;

							if (currentMomentum <= 0)
							{
								tracker = Mathf.Max(tracker, 0f);
							}
							else if (tracker <= -1f)
							{
								tracker += 1f;
								currentMomentum--;
							}
						}
					}
				}
				else
				{
					currentMomentum = 0;
				}

				int buffCount = body.GetBuffCount(RoseBuckler.MomentumBuff);
				if (buffCount != currentMomentum)
				{
					body.SetBuffCount(RoseBuckler.MomentumBuff.buffIndex, currentMomentum);
				}
			}
		}
	}
}
