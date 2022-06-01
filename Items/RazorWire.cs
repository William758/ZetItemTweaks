using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class RazorWire
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "RazorWire";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> MinBaseDamage { get; set; }
		public static ConfigEntry<float> MinStackDamage { get; set; }
		public static ConfigEntry<float> MaxBaseDamage { get; set; }
		public static ConfigEntry<float> MaxStackDamage { get; set; }
		public static ConfigEntry<float> MinDuration { get; set; }
		public static ConfigEntry<float> MaxDuration { get; set; }
		public static ConfigEntry<int> BaseRazors { get; set; }
		public static ConfigEntry<int> StackRazors { get; set; }
		public static ConfigEntry<float> BaseRange { get; set; }
		public static ConfigEntry<float> StackRange { get; set; }



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
			MinBaseDamage = ConfigEntry(
				itemIdentifier, "MinBaseDamage", 1.2f,
				"Minimum damage dealt by razors."
			);
			MinStackDamage = ConfigEntry(
				itemIdentifier, "MinStackDamage", 0f,
				"Minimum damage dealt by razors per stack."
			);
			MaxBaseDamage = ConfigEntry(
				itemIdentifier, "MaxBaseDamage", 1.8f,
				"Maximum damage dealt by razors."
			);
			MaxStackDamage = ConfigEntry(
				itemIdentifier, "MaxStackDamage", 0f,
				"Maximum damage dealt by razors per stack."
			);
			MaxBaseDamage.Value = Mathf.Max(MinBaseDamage.Value, Mathf.Abs(MaxBaseDamage.Value));
			MaxStackDamage.Value = Mathf.Max(MinStackDamage.Value, Mathf.Abs(MaxStackDamage.Value));
			MinDuration = ConfigEntry(
				itemIdentifier, "MinDuration", 0.5f,
				"Delay after being hit to start increasing damage."
			);
			MaxDuration = ConfigEntry(
				itemIdentifier, "MaxDuration", 1.5f,
				"Delay after being hit to reach maximum damage."
			);
			MaxDuration.Value = Mathf.Max(MinDuration.Value, Mathf.Abs(MaxDuration.Value));
			BaseRazors = ConfigEntry(
				itemIdentifier, "BaseRazors", 5,
				"Razor count gained from item."
			);
			StackRazors = ConfigEntry(
				itemIdentifier, "StackRazors", 2,
				"Razor count gained from item per stack."
			);
			BaseRange = ConfigEntry(
				itemIdentifier, "BaseRange", 25f,
				"Razor range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 5f,
				"Razor range gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DamageHook();
			CountHook();
			RangeHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("RAZOR_BURST", "Getting hit causes you to explode in a burst of razors ");
				RegisterFragment("RAZOR_DAMAGE_BASIC", "that deal {0} <style=cIsDamage>damage</style>.");
				RegisterFragment("RAZOR_DAMAGE_COMPLEX", "that deal between {0} and {1} <style=cIsDamage>damage</style>.");
				RegisterFragment("RAZOR_TARGETING", "\nHits up to {0} targets within {1}.");
				RegisterToken("ITEM_THORNS_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = TextFragment("RAZOR_BURST");
			if (MaxBaseDamage.Value > MinBaseDamage.Value)
			{
				output += String.Format(
					TextFragment("RAZOR_DAMAGE_COMPLEX"),
					ScalingText(MinBaseDamage.Value, MinStackDamage.Value, "percent", "cIsDamage"),
					ScalingText(MaxBaseDamage.Value, MaxStackDamage.Value, "percent", "cIsDamage")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("RAZOR_DAMAGE_BASIC"),
					ScalingText(MinBaseDamage.Value, MinStackDamage.Value, "percent", "cIsDamage")
				);
			}
			output += String.Format(
				TextFragment("RAZOR_TARGETING"),
				ScalingText(BaseRazors.Value, StackRazors.Value, "flat", "cIsDamage"),
				ScalingText(BaseRange.Value, StackRange.Value, "meter", "cIsDamage")
			);

			return output;
		}



		private static void DamageHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				VariableDefinition hitTimeVar = new VariableDefinition(il.Body.Method.Module.TypeSystem.Single);
				il.Body.Variables.Add(hitTimeVar);

				//LogInfo("RazorWire :: DamageHook - Added LocalVariable : " + hitTimeVar);

				bool found = c.TryGotoNext(
					x => x.MatchCall<HealthComponent>("UpdateLastHitTime")
				);

				if (found)
				{
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, float>>((healthComponent) =>
					{
						return healthComponent.timeSinceLastHit;
					});
					c.Emit(OpCodes.Stloc, hitTimeVar);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook:GetHitTime Failed!");
					LogWarn("!!! - Aborting RazorWire :: DamageHook");
					return;
				}

				found = c.TryGotoNext(
					x => x.MatchStloc(68)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldarg, 1);
					c.Emit(OpCodes.Ldloc, hitTimeVar);
					c.EmitDelegate<Func<HealthComponent, DamageInfo, float, float>>((healthComponent, damageInfo, hitTime) =>
					{
						return healthComponent.body.damage * GetDamageCoefficient(damageInfo, hitTime, healthComponent.itemCounts.thorns);
					});
					c.Emit(OpCodes.Stloc, 68);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook:SetDamageMult Failed!");
				}
			};
		}

		private static float GetDamageCoefficient(DamageInfo damageInfo, float hitTime, int count)
		{
			float minDamage = MinBaseDamage.Value + MinStackDamage.Value * (count - 1);

			if ((damageInfo.damageType & DamageType.Silent) > 0) return minDamage;

			float maxDamage = MaxBaseDamage.Value + MaxStackDamage.Value * (count - 1);

			if (minDamage >= maxDamage) return minDamage;

			float minDuration = MinDuration.Value;
			float maxDuration = MaxDuration.Value;

			if (hitTime >= maxDuration) return maxDamage;
			else if (hitTime <= minDuration) return minDamage;

			return Mathf.Lerp(minDamage, maxDamage, Mathf.InverseLerp(minDuration, maxDuration, hitTime));
		}

		private static void CountHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(64)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, int>>((healthComponent) =>
					{
						int count = healthComponent.itemCounts.thorns;
						if (count > 0)
						{
							return BaseRazors.Value + StackRazors.Value * (count - 1);
						}

						return 1;
					});
					c.Emit(OpCodes.Stloc, 64);
				}
				else
				{
					LogWarn(itemIdentifier + " :: CountHook Failed!");
				}
			};
		}

		private static void RangeHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(66)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, float>>((healthComponent) =>
					{
						int count = healthComponent.itemCounts.thorns;
						if (count > 0)
						{
							return BaseRange.Value + StackRange.Value * (count - 1);
						}

						return 5f;
					});
					c.Emit(OpCodes.Stloc, 66);
				}
				else
				{
					LogWarn(itemIdentifier + " :: RangeHook Failed!");
				}
			};
		}
	}
}
