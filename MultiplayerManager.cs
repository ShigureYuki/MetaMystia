using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using Il2CppMono;
using UnityEngine;

namespace MetaMystia;

public class MultiplayerManager
{
    private static MultiplayerManager _instance;
    private static readonly object _lock = new object();
    private static ManualLogSource Log => Plugin.Instance.Log;

    private const int TCP_PORT = 40815;
    private string playerId = System.Environment.MachineName;
    private string peerId = "<Unknown>";
    private TcpListener _tcpListener;
    private Thread _tcpListenerThread;
    private bool _isRunning = false;
    private bool _isConnected = false;
    private TcpClient _peerConnection;
    private Thread _peerHandlerThread;
    private string _peerAddress;

    private static readonly string LOG_TAG = "[MultiplayerManager.cs]";

    public static MultiplayerManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new MultiplayerManager();
                    }
                }
            }
            return _instance;
        }
    }

    private MultiplayerManager()
    {
    }

    public bool IsRunning()
    {
        return _isRunning;
    }

    public bool IsConnected()
    {
        return _isConnected;
    }

    public string GetPeerAddress()
    {
        return _peerAddress;
    }

    public void Start()
    {
        if (_isRunning)
        {
            Log.LogWarning($"{LOG_TAG} MultiplayerManager is already running");
            return;
        }

        _isRunning = true;
        Log.LogInfo($"{LOG_TAG} Starting MultiplayerManager");
        peerId = "<Unknown>";

        StartTcpListener();
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        Log.LogInfo($"{LOG_TAG} Stopping MultiplayerManager");
        _isRunning = false;

        DisconnectPeer();

        try
        {
            _tcpListener?.Stop();
        }
        catch (Exception e)
        {
            Log.LogError($"{LOG_TAG} Error stopping TCP listener: {e.Message}");
        }

        peerId = "<Unknown>";
        Log.LogInfo($"{LOG_TAG} MultiplayerManager has stopped");
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void ToggleRunning()
    {
        if (_isRunning)
        {
            Log.LogInfo($"{LOG_TAG} Stopping MultiplayerManager...");
            Stop();
        }
        else
        {
            Log.LogInfo($"{LOG_TAG} Attempting to start MultiplayerManager...");
            Start();
        }
    }

    public string GetPlayerId()
    {
        return playerId;
    }

    public void SetPlayerId(string newId)
    {
        playerId = newId;
        Log.LogInfo($"{LOG_TAG} Player ID set to: {playerId}");
    }

    private void StartTcpListener()
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
            _tcpListener.Start();
            Log.LogInfo($"{LOG_TAG} TCP listener started on port {TCP_PORT}");

            _tcpListenerThread = new Thread(TcpListenerLoop);
            _tcpListenerThread.IsBackground = true;
            _tcpListenerThread.Start();
        }
        catch (Exception e)
        {
            Log.LogError($"{LOG_TAG} Error starting TCP listener: {e.Message}");
        }
    }

    private void TcpListenerLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (_tcpListener.Pending())
                {
                    if (_isConnected)
                    {
                        TcpClient rejectedClient = _tcpListener.AcceptTcpClient();
                        Log.LogInfo($"{LOG_TAG} Connection from {rejectedClient.Client.RemoteEndPoint} rejected (already connected)");
                        rejectedClient.Close();
                    }
                    else
                    {
                        TcpClient client = _tcpListener.AcceptTcpClient();
                        Log.LogInfo($"{LOG_TAG} Connection from {client.Client.RemoteEndPoint} accepted");
                        AcceptPeerConnection(client);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                if (_isRunning)
                {
                    Log.LogError($"{LOG_TAG} Error in TCP listener loop: {e.Message}");
                }
            }
        }
    }

    public bool ConnectToPeer(string peerIp)
    {
        if (_isConnected)
        {
            Log.LogWarning($"{LOG_TAG} Already connected to a peer. Please disconnect first.");
            return false;
        }

        try
        {
            Log.LogInfo($"{LOG_TAG} Connecting to {peerIp}:{TCP_PORT}...");
            TcpClient client = new TcpClient();
            client.Connect(peerIp, TCP_PORT);
            Log.LogInfo($"{LOG_TAG} Successfully connected to peer {peerIp}:{TCP_PORT}");

            _peerAddress = peerIp;
            _isConnected = true;
            _peerConnection = client;

            _peerHandlerThread = new Thread(() => HandlePeerConnection(client));
            _peerHandlerThread.IsBackground = true;
            _peerHandlerThread.Start();

            SendHello();
            SendSync();

            return true;
        }
        catch (Exception e)
        {
            Log.LogError($"{LOG_TAG} Error connecting to peer: {e.Message}");
            return false;
        }
    }

    private void AcceptPeerConnection(TcpClient client)
    {
        if (_isConnected)
        {
            Log.LogWarning($"{LOG_TAG} Already connected to a peer. Please disconnect first.");
            client.Close();
            return;
        }

        _isConnected = true;
        _peerConnection = client;
        _peerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

        Log.LogInfo($"{LOG_TAG} Connection from {_peerAddress} accepted");

        _peerHandlerThread = new Thread(() => HandlePeerConnection(client));
        _peerHandlerThread.IsBackground = true;
        _peerHandlerThread.Start();

        SendHello();
        SendSync();
    }

    private void HandlePeerConnection(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        StringBuilder messageBuilder = new StringBuilder();

        try
        {
            while (_isRunning && _isConnected && client.Connected)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(data);

                        string accumulated = messageBuilder.ToString();
                        int newlineIndex;
                        while ((newlineIndex = accumulated.IndexOf('\n')) >= 0)
                        {
                            string line = accumulated.Substring(0, newlineIndex).Trim('\r', '\n');
                            accumulated = accumulated.Substring(newlineIndex + 1);

                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                ProcessPeerMessage(line, client);
                            }
                        }
                        messageBuilder.Clear();
                        messageBuilder.Append(accumulated);
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception e)
        {
            Log.LogError($"{LOG_TAG} Error handling peer connection: {e.Message}");
        }
        finally
        {
            Log.LogInfo($"{LOG_TAG} Peer connection disconnected");
            DisconnectPeer();
        }
    }

    private void ProcessPeerMessage(string message, TcpClient client)
    {
        Log.LogInfo($"{LOG_TAG} Received peer message: {message}");

        string[] parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string command = parts[0].ToLower();

        switch (command)
        {
            case "ping":
                SendToPeer("pong\n");
                Log.LogInfo($"{LOG_TAG} Responding to ping with pong");
                break;

            case "pong":
                Log.LogInfo($"{LOG_TAG} Received pong response from peer");
                break;

            case "hello":
                if (parts.Length >= 2)
                {
                    string peerId = parts[1];
                    this.peerId = peerId;
                    Log.LogInfo($"{LOG_TAG} Received hello from peer: {peerId}");
                }
                break;
            case "sync":
                if (parts.Length < 7)
                {
                    Log.LogWarning($"{LOG_TAG} Invalid sync message format");
                    break;
                }
                string mapLabel = parts[1];
                bool isSprinting = parts[2].ToLower() == "true";
                float vx = float.Parse(parts[3]);
                float vy = float.Parse(parts[4]);
                float px = float.Parse(parts[5]);
                float py = float.Parse(parts[6]);
                PluginManager.Instance.RunOnMainThread(() =>
                {
                    KyoukoManager.Instance.SyncFromPeer(mapLabel, isSprinting, new Vector2(vx, vy), new Vector2(px, py));
                });
                break;
            case "ready":
                KyoukoManager.isReady = true;
                if (MystiaManager.isReady) // 10->11: 需要先显示「准备完成」对话框再在其回调中执行 OnDayOver
                {
                    PluginManager.Instance.RunOnMainThread(() =>
                    {
                        Utils.ShowReadyDialog(true, () => 
                        {
                            DayScene.SceneManager.Instance.OnDayOver();
                        });
                    });
                }
                // else: 00->01: Nope
                Log.LogInfo($"{LOG_TAG} Kyouko is ready");
                break;
            default:
                Log.LogWarning($"{LOG_TAG} Unknown peer command: {command}");
                break;
        }
    }

    private void SendToPeer(string message)
    {
        if (!_isConnected || _peerConnection == null)
        {
            Log.LogWarning($"{LOG_TAG} Cannot send message: not connected to a peer");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            NetworkStream stream = _peerConnection.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception e)
        {
            Log.LogError($"{LOG_TAG} Error sending to peer: {e.Message}");
            DisconnectPeer();
        }
    }
    
    public void DisconnectPeer()
    {
        if (_peerConnection != null)
        {
            try
            {
                _peerConnection.Close();
            }
            catch { }
            _peerConnection = null;
        }

        _isConnected = false;
        _peerAddress = null;

        Log.LogInfo($"{LOG_TAG} Peer connection disconnected");
    }

    public void SendPing()
    {
        if (_isConnected)
        {
            Log.LogInfo($"{LOG_TAG} Sending ping to peer");
            SendToPeer("ping\n");
        }
        else
        {
            Log.LogWarning($"{LOG_TAG} Cannot send ping: not connected");
        }
    }

    public void SendHello()
    {
        if (_isConnected)
        {
            Log.LogInfo($"{LOG_TAG} Sending hello to peer");
            SendToPeer($"hello {GetPlayerId()}\n");
        }
        else
        {
            Log.LogWarning($"{LOG_TAG} Cannot send hello: not connected");
        }
    }

    public void SendMoveData(UnityEngine.Vector2 inputDirection)
    {
        if (!_isConnected)
        {
            return;
        }
        // format: move <vx> <vy> <px> <py>
        var position = MystiaManager.Instance.GetPosition();
        string message = $"move {inputDirection.x} {inputDirection.y} {position.x} {position.y}\n";
        SendToPeer(message);
    }

    public void SendSprintData(bool isSprinting)
    {
        if (!_isConnected)
        {
            return;
        }
        // format: sprint <false|true> <px> <py>
        var position = MystiaManager.Instance.GetPosition();
        string message = $"sprint {isSprinting} {position.x} {position.y}\n";
        SendToPeer(message);
    }


    public string GetStatus()
    {
        StringBuilder status = new StringBuilder();
        status.AppendLine($"Mystia ID: {GetPlayerId()}");
        status.AppendLine($"Local Port: {TCP_PORT}");
        status.AppendLine($"Running: {(_isRunning ? "Yes" : "No")}");
        status.AppendLine($"Connected: {(_isConnected ? "Yes" : "No")}");
        if (_isConnected)
        {
            status.AppendLine($"Kyouko ID: {peerId}");
        }
        
        if (_isConnected)
        {
            status.AppendLine($"Kyouko Address: {_peerAddress ?? "<Unknown>"}");
        }

        return status.ToString();
    }

    public string GetBriefStatus()
    {
        if (!_isRunning)
        {
            return "Multiplayer: Off";
        }
        if (_isConnected)
        {
            return $"Multiplayer: Connected to {peerId} ({_peerAddress})";
        }
        else
        {
            return "Multiplayer: On (Not connected)";
        }
    }

    public void SendSync()
    {
        if (!_isConnected)
        {
            return;
        }

        var mapLabel = MystiaManager.MapLabel;
        var isSprinting = MystiaManager.IsSprinting;
        var inputDirection = MystiaManager.InputDirection;
        var position = MystiaManager.Instance.GetPosition();

        SendToPeer($"sync {mapLabel} {isSprinting} {inputDirection.x} {inputDirection.y} {position.x} {position.y}\n");
    }

    public void SendReady()
    {
        if (!_isConnected)
        {
            return;
        }

        Log.LogInfo($"{LOG_TAG} Sending ready status to peer");
        SendToPeer("ready\n");
    }
}
