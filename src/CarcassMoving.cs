using AK;
using Harmony;
using ModComponentMapper;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarcassMoving
{

	public class CarcassMoving : MonoBehaviour
	{
		[HarmonyPatch(typeof(Panel_BodyHarvest), "Enable", null)]
		internal class Panel_BodyHarvest_Start_Pos
		{
			private static void Postfix(Panel_BodyHarvest __instance, BodyHarvest bh, bool enable)
			{
				if (enable && __instance.CanEnable(bh))
				{
					currentCarryObj = ((Component)(object)bh).gameObject;
					currentBodyHarvest = bh;
					currentHarvestPanel = __instance;
					MaybeAddCarcassMoveButton(__instance, bh);
				}
			}
		}

		[HarmonyPatch(typeof(LoadScene), "Activate", null)]
		internal class LoadScene_Activate
		{
			private static void Postfix(LoadScene __instance)
			{
				if (PlayerIsCarryingCarcass)
				{
					UnityEngine.Object.DontDestroyOnLoad(currentCarryObj);
					((Behaviour)(object)currentBodyHarvest).enabled = false;
				}
			}
		}

		[HarmonyPatch(typeof(GameManager), "TriggerSurvivalSaveAndDisplayHUDMessage", null)]
		internal class GameManager_TriggerSurvivalSaveAndDisplayHUDMessage
		{
			private static void Prefix()
			{
				if (PlayerIsCarryingCarcass)
				{
					((Behaviour)(object)currentBodyHarvest).enabled = true;
					MoveCarcassToPlayerPosition();
					AddCarcassToSceneSaveData(currentBodyHarvest);
				}
			}
		}

		[HarmonyPatch(typeof(MissionServicesManager), "SceneLoadCompleted", null)]
		internal class MissionServicesManager_SceneLoadCompleted
		{
			private static void Postfix()
			{
				if (PlayerIsCarryingCarcass)
				{
					((Behaviour)(object)currentBodyHarvest).enabled = true;
				}
			}
		}
		///*
		[HarmonyPatch(typeof(PlayerManager), "ShouldSaveGameAfterTeleport", null)]
		internal class PlayerManager_ShouldSaveGameAfterTeleport
		{
			private static bool Prefix(ref bool __result)
			{
				if (!PlayerIsCarryingCarcass)
				{
					return true;
				}
				__result = (!GameManager.m_SceneTransitionData.m_TeleportPlayerSaveGamePosition && GameManager.m_SceneTransitionData.m_SpawnPointName != null);
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerManager), "PlayerCanSprint", null)]
		public static class MaybeChangeWhetherPlayerCanSprint
		{
			private static void Postfix(ref bool __result)
			{
				if (PlayerIsCarryingCarcass)
				{
					__result = false;
				}
			}
		}
		
		[HarmonyPatch(typeof(Encumber), "GetEncumbranceSlowdownMultiplier", null)]
		internal class MaybeAdjustEncumbranceSlowDown
		{
			private static void Postfix(ref float __result)
			{
				if (PlayerIsCarryingCarcass)
				{
					__result *= Mathf.Clamp(1f - carcassWeight / 20f, 0.1f, 0.8f);
				}
			}
		}
		
		[HarmonyPatch(typeof(Fatigue), "CalculateFatigueIncrease", null)]
		internal class Fatigue_CalculateFatigueIncrease_Pos
		{
			private static void Postfix(ref float __result)
			{
				if (PlayerIsCarryingCarcass)
				{
					__result *= Mathf.Clamp(1f + carcassWeight / 20f, 1.2f, 2f);
				}
			}
		}
		
		[HarmonyPatch(typeof(EquipItemPopup), "ShouldHideEquipPopup", null)]
		internal class EquipItemPopup_ShouldHideEquipAndAmmoPopups
		{
			private static void Postfix(ref bool __result)
			{
				if (PlayerIsCarryingCarcass)
				{
					__result = false;
				}
			}
		}
		[HarmonyPatch(typeof(EquipItemPopup), "ShouldHideAmmoPopup", null)]
		internal class EquipItemPopup_ShouldHideAmmoPopup
		{
			private static void Postfix(ref bool __result)
			{
				if (PlayerIsCarryingCarcass)
				{
					__result = false;
				}
			}
		}

		[HarmonyPatch(typeof(Panel_BodyHarvest), "CanEnable", null)]
		internal class Panel_BodyHarvest_CarcassTooFrozenToHarvestBareHands
		{
			private static void Postfix(BodyHarvest bodyHarvest, ref bool __result)
			{
				if (IsMovableCarcass(bodyHarvest) && !(bodyHarvest.GetCondition() < 0.5f))
				{
					__result = true;
				}
			}
		}

		[HarmonyPatch(typeof(PlayerManager), "EquipItem", null)]
		internal class PlayerManager_EquipItem
		{
			private static bool Prefix()
			{

				if (PlayerIsCarryingCarcass)
				{
					HUDMessage.AddMessage("CANNOT EQUIP ITEM WHILE CARRYING CARCASS", false);
					GameAudioManager.PlayGUIError();
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(Panel_HUD), "Update", null)]
		internal class Panel_HUD_Update
		{
			private static void Postfix(Panel_HUD __instance)
			{

				MaybeChangeSprintSpriteColors(__instance);

			}
		}

		[HarmonyPatch(typeof(Panel_BodyHarvest), "DisplayCarcassToFrozenMessage", null)]
		internal class Panel_BodyHarvest_DisplayCarcassToFrozenMessage
		{
			private static bool Prefix(Panel_BodyHarvest __instance)
			{

				if (!HarvestAmmountsAreSelected(__instance))
				{
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(GameAudioManager), "PlayGUIError", null)]
		internal class GameAudioManager_PlayGUIError
		{
			private static bool Prefix()
			{

				Panel_BodyHarvest panel_BodyHarvest = InterfaceManager.m_Panel_BodyHarvest;
				if ((UnityEngine.Object)(object)panel_BodyHarvest != null && !HarvestAmmountsAreSelected(panel_BodyHarvest))
				{
					return false;
				}
				return true;
			}
		}

		public static GameObject currentCarryObj;

		public static BodyHarvest currentBodyHarvest;

		public static EquipItemPopup equipItemPopup;

		public static GameObject moveCarcassBtnObj;

		public static UIButton moveCarcassUIBtn;

		public static Panel_BodyHarvest currentHarvestPanel;

		public static bool PlayerIsCarryingCarcass;

		public static string carcassOriginalScene;

		public static float carcassWeight;

		private void Update()
		{
			if (PlayerIsCarryingCarcass)
			{
				if (HasInjuryPreventingCarry() || GameManager.GetPlayerStruggleComponent().InStruggle())
				{
					DropCarcass();
					
				}
				else if (InputManager.GetAltFirePressed(this))
				{
					DropCarcass();
				}
			}
		}

		internal static void MaybeAddCarcassMoveButton(Panel_BodyHarvest panelInstance, BodyHarvest bodyHarvest)
		{
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Expected O, but got Unknown
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Expected O, but got Unknown
			if (IsMovableCarcass(bodyHarvest))
			{
				if (moveCarcassBtnObj == null)
				{
					moveCarcassBtnObj = UnityEngine.Object.Instantiate(panelInstance.m_Mouse_Button_Harvest, panelInstance.m_Mouse_Button_Harvest.transform);
					moveCarcassBtnObj.GetComponentInChildren<UILocalize>().key = "MOVE CARCASS";
					panelInstance.m_Mouse_Button_Harvest.transform.localPosition += new Vector3(-100f, 0f, 0f);
					moveCarcassBtnObj.transform.localPosition = new Vector3(200f, 0f, 0f);
					moveCarcassUIBtn = moveCarcassBtnObj.GetComponentInChildren<UIButton>();
					moveCarcassUIBtn.onClick.Clear();
					moveCarcassUIBtn.onClick.Add((EventDelegate)(object)new EventDelegate((EventDelegate.Callback)(object)new EventDelegate.Callback(OnMoveCarcass)));
				}
			}
			else if (moveCarcassBtnObj != null)
			{
				UnityEngine.Object.DestroyImmediate(moveCarcassBtnObj);
				panelInstance.m_Mouse_Button_Harvest.transform.localPosition += new Vector3(100f, 0f, 0f);
			}
		}

		internal static bool IsMovableCarcass(BodyHarvest bodyHarvest)
		{
			return ((UnityEngine.Object)(object)bodyHarvest).name.Contains("Stag") || ((UnityEngine.Object)(object)bodyHarvest).name.Contains("Deer") || ((UnityEngine.Object)(object)bodyHarvest).name.Contains("Wolf");
		}

		internal static void OnMoveCarcass()
		{
			if (HasInjuryPreventingCarry())
			{
				GameAudioManager.PlayGUIError();
				AccessTools.Method(typeof(Panel_BodyHarvest), "DisplayErrorMessage", (Type[])null, (Type[])null).Invoke(currentHarvestPanel, new object[1]
				{
					"CANNOT MOVE CARCASS WHILE INJURED"
				});
			}
			else
			{
				PickUpCarcass();
			}
		}

		internal static void PickUpCarcass()
		{
			PlayerIsCarryingCarcass = true;
			carcassWeight = currentBodyHarvest.m_MeatAvailableKG + currentBodyHarvest.GetGutsAvailableWeightKg() + currentBodyHarvest.GetHideAvailableWeightKg();
			currentHarvestPanel.OnBack();
			carcassOriginalScene = GameManager.m_ActiveScene;
			CarcassMoving component = currentCarryObj.GetComponent<CarcassMoving>();
			if (component == null)
			{
				component = currentCarryObj.AddComponent<CarcassMoving>();
			}
			GameManager.GetPlayerManagerComponent().UnequipItemInHands();
			DisplayDropCarcassPopUp();
			HideCarcassFromView();
			PlayCarcassPickUpAudio();
		}

		internal static void DropCarcass()
		{
			PlayerIsCarryingCarcass = false;
			MoveCarcassToPlayerPosition();
			BringCarcassBackIntoView();
			if (GameManager.m_ActiveScene != carcassOriginalScene)
			{
				AddCarcassToSceneSaveData(currentBodyHarvest);
			}
			PlayCarcassDropAudio();
		}

		internal static void HideCarcassFromView()
		{
			currentCarryObj.transform.localScale = new Vector3(0f, 0f, 0f);
		}

		internal static void BringCarcassBackIntoView()
		{
			currentCarryObj.transform.localScale = new Vector3(1f, 1f, 1f);
		}

		internal static void DisplayDropCarcassPopUp()
		{
			EquipItemPopupUtils.ShowItemPopups(string.Empty, Localization.Get("DROP CARCASS"), false, false, false);
		}

		internal static bool HasInjuryPreventingCarry()
		{
			return GameManager.GetSprainedAnkleComponent().HasSprainedAnkle() || GameManager.GetSprainedWristComponent().HasSprainedWrist() || GameManager.GetSprainedWristComponent().GetAfflictionsCount()>0 || GameManager.GetBrokenRibComponent().HasBrokenRib();
		}

		internal static void MoveCarcassToPlayerPosition()
		{
			currentCarryObj.transform.position = GameManager.GetPlayerTransform().position;
			currentCarryObj.transform.rotation = GameManager.GetPlayerTransform().rotation * Quaternion.Euler(0f, 90f, 0f);
		}

		internal static void AddCarcassToSceneSaveData(BodyHarvest bodyHarvest)
		{
			BodyHarvestManager.AddBodyHarvest(currentBodyHarvest);
			UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(currentCarryObj, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
		}

		internal static void PlayCarcassPickUpAudio()
		{
			GameAudioManager.PlaySound("Play_RopeGetOn", InterfaceManager.GetSoundEmitter());
			GameAudioManager.PlaySound(EVENTS.PLAY_EXERTIONLOW, InterfaceManager.GetSoundEmitter());
		}

		internal static void PlayCarcassDropAudio()
		{
			GameAudioManager.PlaySound(EVENTS.PLAY_BODYFALLLARGE, InterfaceManager.GetSoundEmitter());
		}

		internal static bool HarvestAmmountsAreSelected(Panel_BodyHarvest __instance)
		{
			return __instance.m_MenuItem_Meat.m_HarvestAmount > 0f || __instance.m_MenuItem_Hide.m_HarvestAmount > 0f || __instance.m_MenuItem_Gut.m_HarvestAmount > 0f;
		}

		internal static void MaybeChangeSprintSpriteColors(Panel_HUD __instance)
		{
			if (PlayerIsCarryingCarcass)
			{
				((UIWidget)__instance.m_Sprite_SprintCenter).color=__instance.m_SprintBarNoSprintColor;
				
				((UIWidget)__instance.m_Sprite_SprintBar).color=__instance.m_SprintBarNoSprintColor;
			}
		}
	}
}
