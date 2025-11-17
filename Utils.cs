using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace MetaMystia;

public static class Utils
{
    private static ManualLogSource Log => Plugin.Instance.Log;

    private static UnityEngine.Rigidbody2D GetPlayerRigidbody2D()
    {
        return MystiaManager.Instance.GetRigidbody2D();
    }

    public static Vector2? GetPlayerPosition()
    {
        var rb = MystiaManager.Instance.GetRigidbody2D();
        return rb?.position;
    }

    public static bool SetPlayerPosition(float x, float y)
    {
        var rb = MystiaManager.Instance.GetRigidbody2D();
        if (rb == null)
        {
            return false;
        }
        rb.position = new Vector2(x, y);
        Log.LogMessage($"已设置 Mystia 位置到 ({x}, {y})");
        return true;
    }

    public static string GetCurrentActiveMapLabel()
    {
        var sceneManager = DayScene.SceneManager.Instance;
        if (sceneManager == null)
        {
            Log.LogMessage("未找到 DayScene.SceneManager 实例");
            return null;
        }

        return sceneManager.CurrentActiveMapLabel;
    }
};