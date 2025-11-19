using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using System;
using Il2CppInterop.Runtime;
using System.Linq;

namespace MetaMystia;
public class PluginManager : MonoBehaviour
{
    public static PluginManager Instance { get; private set; }
    public static ManualLogSource Log => Plugin.Instance.Log;

    private bool isTextVisible = true;
    private string label = "MetaMystia loaded";
    private NetConsole netConsole;

    public PluginManager(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    internal static GameObject Create(string name)
    {
        var gameObject = new GameObject(name);
        DontDestroyOnLoad(gameObject);

        var component = new PluginManager(gameObject.AddComponent(Il2CppType.Of<PluginManager>()).Pointer);

        return gameObject;
    }

    private void Awake()
    {
        netConsole = new NetConsole();
        netConsole.Start();

        // MultiplayerManager.Instance.Start();
    }

    private void OnGUI()
    {
        if (isTextVisible)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine(label);
            info.AppendLine(MultiplayerManager.Instance.GetBriefStatus());
            GUI.Label(new Rect(10, Screen.height - 50, 600, 50), info.ToString());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backslash)) {
            isTextVisible = !isTextVisible;
            Log.LogMessage("Toggled text visibility: " + isTextVisible);
        }
        if (Input.GetKeyDown(KeyCode.Slash) && Input.GetKey(KeyCode.LeftControl)) {
            MultiplayerManager.Instance.ToggleRunning();
        }
    }

    private void FixedUpdate()
    {
        KyoukoManager.Instance.OnFixedUpdate();
    }

    private void OnDestroy()
    {
        if (netConsole != null)
        {
            netConsole.Stop();
        }
    }
}
