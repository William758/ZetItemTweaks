using RoR2;
using RoR2.ContentManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TPDespair.ZetItemTweaks
{
	public class ZetItemTweaksContent : IContentPackProvider
	{
		public ContentPack contentPack = new ContentPack();

		public string identifier
		{
			get { return "ZetItemTweaksContent"; }
		}

		public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
		{
			ZetItemTweaksPlugin.LoadAssets();

			Sprites.Create();

			Buffs.Create();

			OldWarStealthKit.EvadeBuff = Buffs.StealthEvade;
			RoseBuckler.MomentumBuff = Buffs.SprintMomentum;
			BerzerkersPauldron.MultiKillBuff = Buffs.MultiKill;
			FrostRelic.IndicatorBuff = Buffs.IcicleIndicator;

			AlienHead.PickupIcon = Sprites.GreenAlien;

			contentPack.buffDefs.Add(Buffs.buffDefs.ToArray());

			args.ReportProgress(1f);
			yield break;
		}

		public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
		{
			ContentPack.Copy(contentPack, args.output);
			args.ReportProgress(1f);
			yield break;
		}

		public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
		{
			args.ReportProgress(1f);
			yield break;
		}



		public static class Buffs
		{
			public static BuffDef StealthEvade;
			public static BuffDef SprintMomentum;
			public static BuffDef MultiKill;
			public static BuffDef IcicleIndicator;

			public static List<BuffDef> buffDefs = new List<BuffDef>();


			public static void Create()
			{
				StealthEvade = ScriptableObject.CreateInstance<BuffDef>();
				StealthEvade.name = "ZetStealthEvade";
				StealthEvade.buffColor = new Color(0.35f, 1f, 0.65f);
				StealthEvade.canStack = false;
				StealthEvade.isDebuff = false;
				StealthEvade.isHidden = true;
				StealthEvade.iconSprite = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/MedkitHeal").iconSprite;

				buffDefs.Add(StealthEvade);

				SprintMomentum = ScriptableObject.CreateInstance<BuffDef>();
				SprintMomentum.name = "ZetSprintMomentum";
				SprintMomentum.buffColor = new Color(1f, 0.75f, 0.25f);
				SprintMomentum.canStack = true;
				SprintMomentum.isDebuff = false;
				SprintMomentum.iconSprite = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/SmallArmorBoost").iconSprite;

				buffDefs.Add(SprintMomentum);

				MultiKill = ScriptableObject.CreateInstance<BuffDef>();
				MultiKill.name = "ZetMultiKill";
				MultiKill.buffColor = new Color(1f, 0.65f, 0.35f);
				MultiKill.canStack = true;
				MultiKill.isDebuff = false;
				MultiKill.iconSprite = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/WarCryBuff").iconSprite;

				buffDefs.Add(MultiKill);

				IcicleIndicator = ScriptableObject.CreateInstance<BuffDef>();
				IcicleIndicator.name = "ZetIcicleIndicator";
				IcicleIndicator.buffColor = new Color(0.75f, 0.85f, 1f);
				IcicleIndicator.canStack = true;
				IcicleIndicator.isDebuff = false;
				IcicleIndicator.iconSprite = Sprites.SnowFlake;

				buffDefs.Add(IcicleIndicator);
			}
		}

		public static class Sprites
		{
			public static Sprite GreenAlien;
			public static Sprite SnowFlake;

			public static void Create()
			{
				GreenAlien = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texAlienHeadGreen.png");
				SnowFlake = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBuffIcicleShaded.png");
			}
		}
	}
}