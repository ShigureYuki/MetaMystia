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

    // TODO: UpdateInputDirection 并非实际移动方法，需要额外考虑带有物理的 CharacterControllerUnit.GetTargetMovePosition 或考虑为 Kyouko 增加物理
    // 问题复现: Mystia 顶着墙移动，对方眼中，Kyouko 穿墙飞走
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
        MultiplayerManager.Instance.SendMapLabel();
        KyoukoManager.Instance.UpdateVisibility();
    }
}

[HarmonyPatch(typeof(DaySceneMap))]
public class DaySceneMapPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    [HarmonyPatch(nameof(DaySceneMap.SolveAndUpdateCharacterPositionInternal))]
    [HarmonyPostfix]
    public static void SolveAndUpdateCharacterPositionInternal_Postfix(DaySceneMap __instance, GameData.RunTime.DaySceneUtility.Collection.TrackedNPC npc, DayScene.Interactables.Collections.ConditionComponents.CharacterConditionComponent character, ref bool isNPCOnMap, bool changeRotation)
    {
        try
        {
            if (npc == null || character == null)
            {
                return;
            }

            string npcKey = npc.key;
            if (string.IsNullOrEmpty(npcKey))
            {
                return;
            }

            var persistentNPCKeys = new HashSet<string> { "Kyouko" };

            if (persistentNPCKeys.Contains(npcKey) && MultiplayerManager.Instance.IsConnected())
            {
                isNPCOnMap = true;
                Log.LogMessage($"Force visible: {npcKey}");
            }
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in SolveAndUpdateCharacterPositionInternal_Postfix: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(Common.CharacterUtility.CharacterControllerUnit))]
public class CharacterControllerUnitPatch
{
    private static ManualLogSource Log => Plugin.Instance.Log;
    
    // 供后续调试用
    [HarmonyPatch("GetTargetMovePosition")]
    [HarmonyPrefix]
    public static void GetTargetMovePosition_Prefix(Vector3 inputDirection)
    {
        try
        {
            Log.LogDebug($"GetTargetMovePosition called with inputDirection: {inputDirection}");
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in GetTargetMovePosition_Prefix: {e.Message}");
        }
    }

    [HarmonyPatch("GetTargetMovePosition")]
    [HarmonyPostfix]
    public static void GetTargetMovePosition_Postfix(Vector3 inputDirection, ref Vector2 __result)
    {
        try
        {
            Log.LogDebug($"GetTargetMovePosition returned: {__result}");
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in GetTargetMovePosition_Postfix: {e.Message}");
        }
    }
}
