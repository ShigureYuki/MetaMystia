using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using System.IO;

namespace MetaMystia;

public static class Utils
{
    private static ManualLogSource Log => Plugin.Instance.Log;
    


    // 获取全部 allCharacters, 找到所有不是 Kyouko 的设置 collider 为 false
    public static void InitAllCharacterColliders()
    {
        var allcharacters = DayScene.DaySceneMap.allCharacters; // Dictionary<string, CharacterConditionComponent> 
        foreach (var character in allcharacters)
        {
            if (character.Key != "Kyouko")
            {
                character.Value.Character.UpdateColliderStatus(false);
                Log.LogInfo($"[Utils] Disabled collider for character: {character.Key}");
            }
        }
    }
};