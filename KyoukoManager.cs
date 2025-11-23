using System;
using BepInEx.Logging;
using UnityEngine;
using Common.CharacterUtility;
using DayScene.Interactables.Collections.ConditionComponents;

namespace MetaMystia;

public class KyoukoManager
{
    private static KyoukoManager _instance;
    private static readonly object _lock = new object();
    
    private const string KYOUKO_ID = "Kyouko";
    private static ManualLogSource Log => Plugin.Instance.Log;

    public static string MapLabel { get; private set; }
    public static bool IsKyoukoVisible { get; private set; } = true;
    private static bool isForceSync = true;

    private static Vector2 actualVelocity;
    private static Vector2 positionOffset;
    private static Vector2 currectVelocity;
    public static KyoukoManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new KyoukoManager();
                    }
                }
            }
            return _instance;
        }
    }

    private KyoukoManager()
    {
    }

    public void OnFixedUpdate()
    {
        if (!IsKyoukoVisible)
        {
            return;
        }

        if (!MultiplayerManager.Instance.IsConnected())
        {
            return;
        }

        // 在每个 FixedUpdate 执行位置修正
        
        // if (positionOffset.magnitude > 0.001f)
        // {
        //     return;
        // }

        // 推测经验公式，数据可能有误：位移(px) = 速度(px/s) * 时间 * 速度倍率(目测5) * 奔跑倍率(1 or 1.5)
        // 实际速度由对端 Kyouko 同步而来
        // 修正速度要保证在「一定时间内」完成位置修正
        // 这里使用较为取巧的方法，使用比较好实现的指数衰减模型（线性还要保存历史数据）
        currectVelocity = positionOffset / 0.5f / 5f;
        positionOffset -= currectVelocity * Time.fixedDeltaTime * 5f * GetCharacterUnit().sprintMultiplier;

        // 这样假设位置偏差为 3px, dt = 0.02s
        // currectVelocity = 3 / 0.5 / 5 = 1.2 px/s
        // 位置偏差每帧减少 0.02s * 1.2 px/s * 5 = 0.12 px
        // 每帧修正 0.12px / 3px = 4%
        // 25 帧修正为 96% ^ 25 = 36%
        // 50 帧修正为 96% ^ 50 = 13%
        // 100 帧修正为 96% ^ 100 = 1.7%

        // 由于数据可能有误差，实际计算结果会有不同，但是都是指数衰减的趋势，实测观感还好
        // https://www.bilibili.com/video/av115571395922890?p=3

        // 每 Fixed Frame 都要根据 actualVelocity + currectVelocity 设置实际速度
        var velocity = actualVelocity + currectVelocity;
        if (velocity.magnitude < 0.01f)
        {
            SetMoving(false);
            SetMoveSpeed(1.48f); // 原版 Kyouko 默认速度为 1, Mysita 默认为 1.48
            return;
        }
        SetMoving(true);
        var characterUnit = GetCharacterUnit();
        if (characterUnit == null)
        {
            Log.LogWarning("Failed to get CharacterControllerUnit for Kyouko in OnFixedUpdate");
            return;
        }
        characterUnit.UpdateInputVelocity(velocity);
    }

    public CharacterConditionComponent GetCharacterComponent()
    {
        return DayScene.DaySceneMap.TryGetCharacter("Kyouko");
    }

    public CharacterControllerUnit GetCharacterUnit()
    {
        var component = GetCharacterComponent();
        if (component == null)
        {
            return null;
        }

        var characterUnit = component.Character;
        if (characterUnit == null)
        {
            Log.LogWarning($"CharacterControllerUnit of '{KYOUKO_ID}' is null");
            return null;
        }

        return characterUnit;
    }

    public Rigidbody2D GetRigidbody2D()
    {
        var characterUnit = GetCharacterUnit();
        if (characterUnit == null)
        {
            return null;
        }

        var rb = characterUnit.rb2d;
        if (rb == null)
        {
            Log.LogWarning($"Rigidbody2D of '{KYOUKO_ID}' is null");
            return null;
        }

        return rb;
    }

    public Vector2 GetPosition()
    {
        var rb = GetRigidbody2D();
        return rb?.position ?? Vector2.zero;
    }

    public bool SetPosition(float x, float y)
    {
        var rb = GetRigidbody2D();
        if (rb == null)
        {
            return false;
        }
        rb.position = new Vector2(x, y);
        Log.LogInfo($"Kyouko position set to ({x}, {y})");
        return true;
    }

    public bool GetMoving()
    {
        var characterUnit = GetCharacterUnit();
        return characterUnit.IsMoving;
    }

    public bool SetMoving(bool isMoving)
    {
        var characterUnit = GetCharacterUnit();
        if (characterUnit == null)
        {
            return false;
        }
        if (characterUnit.IsMoving != isMoving)
        {
            characterUnit.IsMoving = isMoving;
            Log.LogInfo($"Kyouko moving state set to {isMoving}");
        }
        return true;
    }

    public void UpdateInputDirection(Vector2 inputDirection, Vector2 syncPosition)
    {
        if (!IsKyoukoVisible)
        {
            Log.LogWarning("Cannot set input direction: Kyouko is not visible");
            return;
        }
        if (isForceSync)
        {
            var rb = GetRigidbody2D();
            if (rb != null)
            {
                rb.position = syncPosition;
                Log.LogMessage($"Force position sync for Kyouko to ({syncPosition.x}, {syncPosition.y})");
                actualVelocity = inputDirection;
            }
            isForceSync = false;
        }

        var characterUnit = GetCharacterUnit();
        
        // characterUnit.UpdateInputVelocity(inputDirection);
        // 速度设置已由 OnFixedUpdate 负责
        actualVelocity = inputDirection;
        characterUnit.IsMoving = inputDirection.magnitude > 0;
        Log.LogInfo($"Update input direction: ({inputDirection.x}, {inputDirection.y})");

        positionOffset = syncPosition - characterUnit.rb2d.position;
    }

    public void UpdateSprintState(bool isSprinting, Vector2 syncPosition)
    {
        if (!IsKyoukoVisible)
        {
            Log.LogWarning("Cannot set input direction: Kyouko is not visible");
            return;
        }

        var characterUnit = GetCharacterUnit();
        characterUnit.sprintMultiplier = isSprinting ? 1.5f : 1.0f;
        Log.LogMessage($"Update sprint state: {isSprinting}");

        positionOffset = syncPosition - characterUnit.rb2d.position;
    }

    public float GetMoveSpeed()
    {
        var characterUnit = GetCharacterUnit();
        return characterUnit.MoveSpeedMultiplier;
    }

    public bool SetMoveSpeed(float speed)
    {
        var characterUnit = GetCharacterUnit();

        if (characterUnit.MoveSpeedMultiplier != speed)
        {
            characterUnit.MoveSpeedMultiplier = speed;
            Log.LogInfo($"Kyouko move speed set to {speed}");    
        }
        return true;
    }

    public bool SetInputDirection(float x, float y, float z = 0)
    {
        if (!IsKyoukoVisible)
        {
            Log.LogWarning("Cannot set input direction: Kyouko is not visible");
            return false;
        }

        var characterUnit = GetCharacterUnit();
        if (characterUnit == null)
        {
            Log.LogWarning("Failed to get CharacterControllerUnit for Kyouko");
            return false;
        }

        characterUnit.inputDirection = new Vector3(x, y, z);
        Log.LogInfo($"Kyouko input direction set to ({x}, {y}, {z})");
        return true;
    }

    public void UpdateMapLabel(string mapLabel)
    {
        MapLabel = mapLabel;
        Log.LogInfo($"Updated Kyouko map label to '{mapLabel}'");
    }

    public void UpdateVisibility()
    {
        var newVisibility = MapLabel == MystiaManager.MapLabel;
        if (newVisibility == IsKyoukoVisible)
        {
            return;
        }
        IsKyoukoVisible = newVisibility;
        Log.LogInfo($"Kyouko visibility updated to {IsKyoukoVisible} (Kyouko map: '{MapLabel}', Mystia map: '{MystiaManager.MapLabel}')");

        if (IsKyoukoVisible)
        {
            isForceSync = true;
            return;
        }
        var rb = GetRigidbody2D();
        if (rb == null)
        {
            return;
        }

        rb.position = new Vector2(114514f, 114514f);
    }

    public void EnterMap(string mapLabel, Vector2 position)
    {
        Log.LogInfo($"[KyoukoManager.cs] EnterMap");
        GameData.RunTime.DaySceneUtility.RunTimeDayScene.MoveCharacter("Kyouko", mapLabel, position, 0, out var oldNPCData);
        UpdateMapLabel(mapLabel);
        UpdateVisibility();
        Log.LogInfo($"Kyouko entered map '{mapLabel}' at position ({position.x}, {position.y}) rotation 0");
        if (mapLabel != MystiaManager.MapLabel)
        {
            return;
        }
    }
}
