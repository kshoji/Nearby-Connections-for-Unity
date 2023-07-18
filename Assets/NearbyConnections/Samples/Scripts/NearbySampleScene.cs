using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace jp.kshoji.unity.nearby.sample
{
    public class NearbySampleScene : MonoBehaviour
    {
        private const string ServiceID = "a7b90efd-f739-4a0a-842e-fba4f42ffb2e";
        private static string LocalEndpointName = Guid.NewGuid().ToString();
        private HashSet<string> connectedEndpoints = new HashSet<string>();

        private void Awake()
        {
            guiScale = (Screen.width > Screen.height) ? Screen.width / 1024f : Screen.height / 1024f;

            NearbyConnectionsManager.Instance.OnAdvertisingStarted += () =>
            {
                receivedMessages.Add($"OnAdvertisingStarted");
            };

            NearbyConnectionsManager.Instance.OnAdvertisingFailed += () =>
            {
                receivedMessages.Add($"OnAdvertisingFailed");
            };

            NearbyConnectionsManager.Instance.OnDiscoveryStarted += () =>
            {
                receivedMessages.Add($"OnDiscoveryStarted");
            };
            NearbyConnectionsManager.Instance.OnDiscoveryFailed += () =>
            {
                receivedMessages.Add($"OnDiscoveryFailed");
            };
            NearbyConnectionsManager.Instance.OnEndpointDiscovered += id =>
            {
                receivedMessages.Add($"OnEndpointDiscovered id: {id}");
                if (autoAcceptConnection)
                {
                    NearbyConnectionsManager.Instance.Connect(LocalEndpointName, id);
                }
            };

            NearbyConnectionsManager.Instance.OnConnectionInitiated += (id, endpointName, connection) =>
            {
                receivedMessages.Add($"OnConnectionInitiated id: {id}, endpointName: {endpointName}, connection: {connection}");
                if (autoAcceptConnection)
                {
                    NearbyConnectionsManager.Instance.AcceptConnection(id);
                }
            };
            NearbyConnectionsManager.Instance.OnConnectionFailed += id =>
            {
                receivedMessages.Add($"OnConnectionInitiated id: {id}");
            };
            NearbyConnectionsManager.Instance.OnEndpointConnected += id =>
            {
                lock (connectedEndpoints)
                {
                    connectedEndpoints.Add(id);
                }
                receivedMessages.Add($"OnEndpointConnected id: {id}");
            };
            NearbyConnectionsManager.Instance.OnEndpointDisconnected += id =>
            {
                lock (connectedEndpoints)
                {
                    connectedEndpoints.Remove(id);
                }
                receivedMessages.Add($"OnEndpointDisconnected id: {id}");
            };

            NearbyConnectionsManager.Instance.OnReceive += (id, l, payload) =>
            {
                Debug.Log($"OnReceive id: {id}, l: {l}, payload: {string.Join(", ", payload)}");
                receivedMessages.Add($"OnReceive [{id}]({l}): {Encoding.UTF8.GetString(payload)}");
            };

            NearbyConnectionsManager.Instance.Initialize(() =>
            {
                NearbyConnectionsManager.Instance.StartDiscovering(ServiceID, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
            });
        }

        private const int MaxNumberOfReceivedMessages = 50;
        private readonly List<string> receivedMessages = new List<string>();

        private const int ReceivedMessageWindow = 1;
        private const int ConnectionWindow = 2;

        private Rect connectionWindowRect = new Rect(0, 0, 400, 400);
        private Rect receivedMessageWindowRect = new Rect(50, 50, 400, 400);

        private Vector2 receiveMidiWindowScrollPosition;
        private float guiScale;
        private bool autoAcceptConnection;
        private string sendText;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Layout)
            {
                return;
            }

            GUIUtility.ScaleAroundPivot(new Vector2(guiScale, guiScale), Vector2.zero);

            receivedMessageWindowRect = GUILayout.Window(ReceivedMessageWindow, receivedMessageWindowRect, OnGUIWindow, "Received Messages");
            connectionWindowRect = GUILayout.Window(ConnectionWindow, connectionWindowRect, OnGUIWindow, "Nearby Connections");
       }

        private void OnGUIWindow(int id)
        {
            switch (id)
            {
                case ReceivedMessageWindow:
                    receiveMidiWindowScrollPosition = GUILayout.BeginScrollView(receiveMidiWindowScrollPosition);
                    GUILayout.Label("Received messages: ");
                    if (receivedMessages.Count > MaxNumberOfReceivedMessages)
                    {
                        receivedMessages.RemoveRange(0, receivedMessages.Count - MaxNumberOfReceivedMessages);
                    }
                    foreach (var message in receivedMessages.AsReadOnly().Reverse())
                    {
                        GUILayout.Label(message);
                    }
                    GUILayout.EndScrollView();
                    break;

                case ConnectionWindow:
                    GUILayout.Label($"LocalEndpointName: {LocalEndpointName}");

                    autoAcceptConnection = GUILayout.Toggle(autoAcceptConnection, "Auto accept incoming connections");

                    if (GUILayout.Button("StartDiscovering"))
                    {
                        if (NearbyConnectionsManager.Instance.IsDiscovering())
                        {
                            NearbyConnectionsManager.Instance.StopDiscovering();
                        }
                        NearbyConnectionsManager.Instance.StartDiscovering(ServiceID, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
                    }
                    if (GUILayout.Button("StopDiscovering"))
                    {
                        NearbyConnectionsManager.Instance.StopDiscovering();
                    }

                    if (GUILayout.Button("StartAdvertising"))
                    {
                        if (NearbyConnectionsManager.Instance.IsAdvertising())
                        {
                            NearbyConnectionsManager.Instance.StopAdvertising();
                        }
                        NearbyConnectionsManager.Instance.StartAdvertising(LocalEndpointName, ServiceID, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
                    }
                    if (GUILayout.Button("StopAdvertising"))
                    {
                        NearbyConnectionsManager.Instance.StopAdvertising();
                    }

                    sendText = GUILayout.TextField(sendText);
                    if (GUILayout.Button("Send"))
                    {
                        NearbyConnectionsManager.Instance.Send(Encoding.UTF8.GetBytes(sendText));
                    }

                    GUILayout.Label($"Discovered endpoint:");
                    // accept / reject endpoint
                    var discoveredEndpoints = NearbyConnectionsManager.Instance.GetDiscoveredEndpoints();
                    foreach (var discoveredEndpoint in discoveredEndpoints)
                    {
                        GUILayout.Label($"endpoint: {discoveredEndpoint}");
                        if (GUILayout.Button("Accept"))
                        {
                            NearbyConnectionsManager.Instance.AcceptConnection(discoveredEndpoint);
                        }
                        if (GUILayout.Button("Reject"))
                        {
                            NearbyConnectionsManager.Instance.RejectConnection(discoveredEndpoint);
                        }
                    }

                    GUILayout.Label($"Connected endpoint:");
                    // disconnect endpoint
                    lock (connectedEndpoints)
                    {
                        foreach (var connectedEndpoint in connectedEndpoints)
                        {
                            GUILayout.Label($"endpoint: {connectedEndpoint}");
                            if (GUILayout.Button("Disconnect"))
                            {
                                NearbyConnectionsManager.Instance.Disconnect(connectedEndpoint);
                            }
                        }
                    }

                    break;
            }
            GUI.DragWindow();
        }

        private void OnApplicationQuit()
        {
            NearbyConnectionsManager.Instance.Terminate();
        }

        /// <summary>
        /// Get <see cref="Stream"/> from url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="onResult"></param>
        /// <returns></returns>
        IEnumerator GetStreamFromUrl(string url, Action<Stream> onResult)
        {
            using (var www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();
                onResult(new MemoryStream(www.downloadHandler.data));
            }
        }

        /// <summary>
        /// Get <see cref="Stream"/> for a Streaming Asset
        /// </summary>
        /// <param name="filename">asset file name</param>
        /// <param name="onResult">Action for getting <see cref="Stream"/></param>
        /// <returns></returns>
        IEnumerator GetStreamingAssetFilePath(string filename, Action<Stream> onResult)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            var path = Path.Combine(Application.streamingAssetsPath, filename);
            if (path.Contains("://"))
            {
                var www = UnityWebRequest.Get(path);
                yield return www.SendWebRequest();
                onResult(new MemoryStream(www.downloadHandler.data));
            }
            else
            {
                onResult(new FileStream(path, FileMode.Open, FileAccess.Read));
            }
#else
            onResult(new FileStream(Path.Combine(Application.streamingAssetsPath, filename), FileMode.Open, FileAccess.Read));
            yield break;
#endif
        }
    }
}