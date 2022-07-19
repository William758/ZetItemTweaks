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
			LeptonDaisy.RegenBuff = Buffs.LeptonRegen;
			BerzerkersPauldron.MultiKillBuff = Buffs.MultiKill;
			Aegis.BarrierBuff = Buffs.BarrierArmor;
			FrostRelic.IndicatorBuff = Buffs.IcicleIndicator;
			PixieTube.PixieBuff = Buffs.PixiePower;

			AlienHead.PickupIcon = Sprites.GreenAlien;
			BlackMonolith.PickupIcon = Sprites.RedMonolith;
			ScratchTicket.PickupIcon = Sprites.GreenTicket;

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
			public static BuffDef LeptonRegen;
			public static BuffDef MultiKill;
			public static BuffDef BarrierArmor;
			public static BuffDef IcicleIndicator;
			public static BuffDef PixiePower;

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
				SprintMomentum.iconSprite = Sprites.SprintArmor;

				buffDefs.Add(SprintMomentum);

				LeptonRegen = ScriptableObject.CreateInstance<BuffDef>();
				LeptonRegen.name = "ZetLeptonRegen";
				LeptonRegen.buffColor = new Color(0.65f, 1f, 0.25f);
				LeptonRegen.canStack = true;
				LeptonRegen.isDebuff = false;
				LeptonRegen.iconSprite = Sprites.LeptonAura;

				buffDefs.Add(LeptonRegen);

				MultiKill = ScriptableObject.CreateInstance<BuffDef>();
				MultiKill.name = "ZetMultiKill";
				MultiKill.buffColor = new Color(1f, 0.65f, 0.35f);
				MultiKill.canStack = true;
				MultiKill.isDebuff = false;
				MultiKill.iconSprite = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/WarCryBuff").iconSprite;

				buffDefs.Add(MultiKill);

				BarrierArmor = ScriptableObject.CreateInstance<BuffDef>();
				BarrierArmor.name = "ZetBarrierArmor";
				BarrierArmor.buffColor = new Color(0.75f, 0.75f, 0.75f);
				BarrierArmor.canStack = false;
				BarrierArmor.isDebuff = false;
				BarrierArmor.isHidden = true;
				BarrierArmor.iconSprite = LegacyResourcesAPI.Load<BuffDef>("BuffDefs/SmallArmorBoost").iconSprite;

				buffDefs.Add(BarrierArmor);

				IcicleIndicator = ScriptableObject.CreateInstance<BuffDef>();
				IcicleIndicator.name = "ZetIcicleIndicator";
				IcicleIndicator.buffColor = new Color(0.75f, 0.85f, 1f);
				IcicleIndicator.canStack = true;
				IcicleIndicator.isDebuff = false;
				IcicleIndicator.iconSprite = Sprites.SnowFlake;

				buffDefs.Add(IcicleIndicator);

				PixiePower = ScriptableObject.CreateInstance<BuffDef>();
				PixiePower.name = "ZetPixiePower";
				PixiePower.buffColor = new Color(1f, 0.5f, 0.85f);
				PixiePower.canStack = true;
				PixiePower.isDebuff = false;
				PixiePower.iconSprite = Sprites.PixiePower;

				buffDefs.Add(PixiePower);
			}
		}

		public static class Sprites
		{
			public static Sprite GreenAlien;
			public static Sprite RedMonolith;
			public static Sprite GreenTicket;

			public static Sprite SprintArmor;
			public static Sprite LeptonAura;
			public static Sprite SnowFlake;
			public static Sprite PixiePower;

			public static void Create()
			{
				GreenAlien = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texAlienHeadGreen.png");
				RedMonolith = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBlackMonolithRed.png");
				GreenTicket = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texScratchTicketGreen.png");

				SprintArmor = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBuffSprintArmor.png");
				LeptonAura = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBuffLepton.png");
				SnowFlake = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBuffIcicleShaded.png");
				PixiePower = ZetItemTweaksPlugin.Assets.LoadAsset<Sprite>("Assets/Icons/texBuffPixie.png");
			}
		}
	}
}