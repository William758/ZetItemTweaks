using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class WeepingFungus
	{
		internal static BuffDef ActivityBuff;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod" };

		public static string itemIdentifier = "WeepingFungus";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<bool> ActivityIndicator { get; set; }
		public static ConfigEntry<float> SprintThreshold { get; set; }
		public static ConfigEntry<float> HealInterval { get; set; }
		public static ConfigEntry<float> BaseFlatHeal { get; set; }
		public static ConfigEntry<float> StackFlatHeal { get; set; }
		public static ConfigEntry<float> BaseFractionHeal { get; set; }
		public static ConfigEntry<float> StackFractionHeal { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				BuffDef buffDef = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/MushroomVoidActive");
				if (buffDef)
				{
					if (buffDef.isHidden == ActivityIndicator.Value)
					{
						buffDef.isHidden = !ActivityIndicator.Value;

						ModifiedBuffDefCount++;
					}
				}

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
			ActivityIndicator = ConfigEntry(
				itemIdentifier, "ActivityIndicator", false,
				"Display item activity buff."
			);
			SprintThreshold = ConfigEntry(
				itemIdentifier, "SprintThreshold", 0.2f,
				"Time after not sprinting to stop healing."
			);
			HealInterval = ConfigEntry(
				itemIdentifier, "HealInterval", 0.5f,
				"Time between each heal from sprinting."
			);
			BaseFlatHeal = ConfigEntry(
				itemIdentifier, "BaseFlatHeal", 0f,
				"Healing per second while sprinting."
			);
			StackFlatHeal = ConfigEntry(
				itemIdentifier, "StackFlatHeal", 0f,
				"Healing per second while sprinting per stack."
			);
			BaseFractionHeal = ConfigEntry(
				itemIdentifier, "BaseFractionHeal", 0.02f,
				"Healing percent per second while sprinting. 0.02 = 2% health."
			);
			StackFractionHeal = ConfigEntry(
				itemIdentifier, "StackFractionHeal", 0.01f,
				"Healing percent per second while sprinting per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			ActivityBuff = DLC1Content.Buffs.MushroomVoidActive;

			DisableDefaultHook();
			CharacterBody.onBodyInventoryChangedGlobal += HandleItemBehavior;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("SPRINTHEAL_ANY", "<style=cIsHealing>Heals</style> for {0} health every second while sprinting.");
				RegisterFragment("SPRINTHEAL_BOTH", "<style=cIsHealing>Heals</style> for {0} plus an additional {1} health every second while sprinting.");
				RegisterFragment("SPRINTHEAL_CORRUPTION", "\n<style=cIsVoid>Corrupts all Bustling Fungi</style>.");
				RegisterToken("ITEM_MUSHROOMVOID_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("SPRINTHEAL_ANY", "<style=cIsHealing>Cura</style> em {0} da saúde por segundo ao correr.");
				RegisterFragment("SPRINTHEAL_BOTH", "<style=cIsHealing>Cura</style> em {0} mais um adicional de {1} da saúde por segundo ao correr.");
				RegisterFragment("SPRINTHEAL_CORRUPTION", "\n<style=cIsVoid>Corrompe todos os Fungos Vibrantes</style>.");
				RegisterToken("ITEM_MUSHROOMVOID_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseFlatHeal.Value > 0f && BaseFractionHeal.Value > 0f)
			{
				output += String.Format(
					TextFragment("SPRINTHEAL_BOTH", true),
					ScalingText(BaseFlatHeal.Value, StackFlatHeal.Value, "flat", "cIsHealing"),
					ScalingText(BaseFractionHeal.Value, StackFractionHeal.Value, "percent", "cIsHealing")
				);
			}
			else if (BaseFractionHeal.Value > 0f)
			{
				output += String.Format(
					TextFragment("SPRINTHEAL_ANY", true),
					ScalingText(BaseFractionHeal.Value, StackFractionHeal.Value, "percent", "cIsHealing")
				);
			}
			else if (BaseFlatHeal.Value > 0f)
			{
				output += String.Format(
					TextFragment("SPRINTHEAL_ANY", true),
					ScalingText(BaseFlatHeal.Value, StackFlatHeal.Value, "flat", "cIsHealing")
				);
			}

			if (output == "") output += TextFragment("CFG_NO_EFFECT");

			output += TextFragment("SPRINTHEAL_CORRUPTION");

			return output;
		}



		private static void DisableDefaultHook()
		{
			On.RoR2.MushroomVoidBehavior.OnEnable += (orig, self) => { };
			On.RoR2.MushroomVoidBehavior.OnDisable += (orig, self) => { };
			On.RoR2.MushroomVoidBehavior.FixedUpdate += (orig, self) => { };
		}

		private static void HandleItemBehavior(CharacterBody body)
		{
			if (NetworkServer.active)
			{
				body.AddItemBehavior<WungusBehavior>(body.inventory.GetItemCount(DLC1Content.Items.MushroomVoid));
			}
		}
	}



	public class WungusBehavior : CharacterBody.ItemBehavior
	{
		public float healTimer = 0f;
		public float healInterval = 0.5f;

		public float healFlat = 0f;
		public float healFraction = 0f;
		private int ItemCount = 0;

		public float timeSinceLastSprint = 0f;
		public float sprintThreshold = 0.25f;

		private HealthComponent healthComponent;

		public void Awake()
		{
			enabled = false;
		}

		public void OnEnable()
		{
			healInterval = Mathf.Max(0.2f, WeepingFungus.HealInterval.Value);
			sprintThreshold = Mathf.Max(0f, WeepingFungus.SprintThreshold.Value);

			if (body)
			{
				healthComponent = body.GetComponent<HealthComponent>();
				body.SetBuffCount(WeepingFungus.ActivityBuff.buffIndex, 0);
			}

			timeSinceLastSprint = sprintThreshold + 1f;
		}

		public void OnDisable()
		{
			if (body)
			{
				body.SetBuffCount(WeepingFungus.ActivityBuff.buffIndex, 0);
			}

			healthComponent = null;
		}

		public void FixedUpdate()
		{
			if (body)
			{
				int active = 0;
				float deltaTime = Time.fixedDeltaTime;

				if (body.isSprinting) timeSinceLastSprint = 0f;
				else timeSinceLastSprint += deltaTime;

				if (timeSinceLastSprint <= sprintThreshold)
				{
					active = 1;
					healTimer += deltaTime;
				}

				int buffCount = body.GetBuffCount(WeepingFungus.ActivityBuff);
				if (buffCount != active)
				{
					body.SetBuffCount(WeepingFungus.ActivityBuff.buffIndex, active);
				}
			}

			if (ItemCount != stack)
			{
				ItemCount = stack;

				RecalcHealing();
			}

			if (healthComponent)
			{
				while (healTimer > healInterval)
				{
					healTimer -= healInterval;

					healthComponent.Heal(healFlat + (healFraction * healthComponent.fullHealth), default);
				}
			}
		}

		private void RecalcHealing()
		{
			float flatHealing = 0f;
			if (WeepingFungus.BaseFlatHeal.Value > 0f)
			{
				flatHealing = (WeepingFungus.BaseFlatHeal.Value + WeepingFungus.StackFlatHeal.Value * (ItemCount - 1)) * healInterval;
			}
			healFlat = flatHealing;

			float fractionHealing = 0f;
			if (WeepingFungus.BaseFractionHeal.Value > 0f)
			{
				fractionHealing = (WeepingFungus.BaseFractionHeal.Value + WeepingFungus.StackFractionHeal.Value * (ItemCount - 1)) * healInterval;
			}
			healFraction = fractionHealing;
		}
	}
}
