using HarmonyLib;
using UnityEngine;
using Common.CharacterUtility;
using BepInEx.Logging;
using DayScene.Input;
using GameData.RunTime.Common;
using DayScene;
using System.Collections.Generic;

namespace MetaMystia;

[HarmonyPatch(typeof(CharacterControllerInputGeneratorComponent))]
public class CharacterInputPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    [HarmonyPatch(nameof(CharacterControllerInputGeneratorComponent.UpdateInputDirection))]
    [HarmonyPrefix]
    public static void UpdateInputDirection_Prefix(CharacterControllerInputGeneratorComponent __instance, Vector2 inputDirection)
    {
        try
        {
            var playerInputGenerator = MystiaManager.Instance.GetInputGenerator();
            if (playerInputGenerator != null && __instance == playerInputGenerator)
            {
                MultiplayerManager.Instance.SendMoveData(inputDirection);
            }
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in UpdateInputDirection_Prefix: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(DayScenePlayerInputGenerator))]
public class DayScenePlayerInputPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintPerformed))]
    [HarmonyPrefix]
    public static void OnSprintPerformed_Prefix()
    {
        MultiplayerManager.Instance.SendSprintData(true);
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintCanceled))]
    [HarmonyPrefix]
    public static void OnSprintCanceled_Prefix()
    {
        MultiplayerManager.Instance.SendSprintData(false);
    }
}

[HarmonyPatch(typeof(RunTimeScheduler))]
public class RunTimeSchedulerPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    [HarmonyPatch(nameof(RunTimeScheduler.OnEnterDaySceneMap))]
    [HarmonyPostfix]
    public static void OnEnterDaySceneMap_Postfix()
    {
        MystiaManager.Instance.UpdateMapLabel();
    }
}

[HarmonyPatch(typeof(DaySceneMap))]
public class DaySceneMapPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    // Hook SolveAndUpdateCharacterPositionInternal 来强制某些NPC可见
    [HarmonyPatch(nameof(DaySceneMap.SolveAndUpdateCharacterPositionInternal))]
    [HarmonyPrefix]
    public static void SolveAndUpdateCharacterPositionInternal_Prefix(DaySceneMap __instance, Dictionary<string, GameData.RunTime.DaySceneUtility.Collection.TrackedNPC> npcs, GameData.RunTime.DaySceneUtility.Collection.TrackedNPC npc, DayScene.Interactables.Collections.ConditionComponents.CharacterConditionComponent character, out bool isNPCOnMap, bool changeRotation)
    {
        try
        {
            if (npc.key == "Kyouko")
            {
                isNPCOnMap = true;
                Log.LogMessage($"强制设置 Kyouko 可见: {npc.key}");
                return;
            }

            isNPCOnMap = npcs.ContainsKey(npc.key);
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in SolveAndUpdateCharacterPositionInternal_Prefix: {e.Message}");
            isNPCOnMap = false;
        }
    }
}
