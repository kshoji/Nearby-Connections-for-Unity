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
                receivedMessages.Add($"OnEndpointConnected id: {id}");
            };
            NearbyConnectionsManager.Instance.OnEndpointDisconnected += id =>
            {
                receivedMessages.Add($"OnEndpointDisconnected id: {id}");
            };

            NearbyConnectionsManager.Instance.OnReceive += (id, l, payload) =>
            {
                Debug.Log($"OnReceive id: {id}, l: {l}, payload: {string.Join(", ", payload)}");
                receivedMessages.Add($"OnReceive [{id}]({l}): {Encoding.UTF8.GetString(payload)}");
            };

            NearbyConnectionsManager.Instance.OnReceiveFile += (id, l, fileName) =>
            {
                Debug.Log($"OnReceiveFile id: {id}, l: {l}, fileName: {fileName}");
                receivedMessages.Add($"OnReceiveFile [{id}]({l}): {fileName}");
            };

            NearbyConnectionsManager.Instance.Initialize(() =>
            {
                receivedMessages.Add($"NearbyConnectionsManager initialized.");
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

                    if (NearbyConnectionsManager.Instance.IsDiscovering())
                    {
                        if (GUILayout.Button("StopDiscovering"))
                        {
                            NearbyConnectionsManager.Instance.StopDiscovering();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("StartDiscovering"))
                        {
                            NearbyConnectionsManager.Instance.StartDiscovering(ServiceID, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
                        }
                    }

                    if (NearbyConnectionsManager.Instance.IsAdvertising())
                    {
                        if (GUILayout.Button("StopAdvertising"))
                        {
                            NearbyConnectionsManager.Instance.StopAdvertising();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("StartAdvertising"))
                        {
                            NearbyConnectionsManager.Instance.StartAdvertising(LocalEndpointName, ServiceID, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
                        }
                    }

                    sendText = GUILayout.TextField(sendText);
                    if (GUILayout.Button("Send") && !string.IsNullOrEmpty(sendText))
                    {
                        NearbyConnectionsManager.Instance.Send(Encoding.UTF8.GetBytes(sendText));
                    }

                    if (GUILayout.Button("Send File"))
                    {
                        IEnumerator GetFileContentsAndSend(string filePath)
                        {
                            var request = UnityWebRequest.Get(filePath);
                            yield return request.SendWebRequest();
                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                var tempFileName = Path.GetTempFileName();
                                using var fileStream = new FileStream(tempFileName, FileMode.OpenOrCreate);
                                fileStream.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
 
                                var payloadId = NearbyConnectionsManager.Instance.Send(tempFileName);
                                receivedMessages.Add($"Send File payloadId: {payloadId}");
                            }
                        }

                        // TODO: place a file to Assets/StreamingAssets/testFile.zip
                        var filePath = Path.Combine(Application.streamingAssetsPath, "testFile.zip");
                        if (Uri.IsWellFormedUriString(filePath, UriKind.Absolute))
                        {
                            // for Android
                            StartCoroutine(GetFileContentsAndSend(filePath));
                        }
                        else
                        {
                            var payloadId = NearbyConnectionsManager.Instance.Send(filePath);
                            receivedMessages.Add($"Send File payloadId: {payloadId}");
                        }
                    }
                    
                    GUILayout.Label($"Discovered endpoints:");
                    var discoveredEndpoints = NearbyConnectionsManager.Instance.GetDiscoveredEndpoints();
                    foreach (var discoveredEndpoint in discoveredEndpoints)
                    {
                        GUILayout.Label($"endpoint: {discoveredEndpoint}");

                        // connect to endpoint
                        if (GUILayout.Button("Connect"))
                        {
                            NearbyConnectionsManager.Instance.Connect(LocalEndpointName, discoveredEndpoint);
                        }
                    }

                    GUILayout.Label($"Pending connections:");
                    var pendingConnections = NearbyConnectionsManager.Instance.GetPendingConnections();
                    foreach (var pendingConnection in pendingConnections)
                    {
                        GUILayout.Label($"endpoint: {pendingConnection}");

                        // accept / reject endpoint
                        if (GUILayout.Button("Accept"))
                        {
                            NearbyConnectionsManager.Instance.AcceptConnection(pendingConnection);
                        }

                        if (GUILayout.Button("Reject"))
                        {
                            NearbyConnectionsManager.Instance.RejectConnection(pendingConnection);
                        }
                    }

                    GUILayout.Label($"Connected endpoints:");
                    var establishedConnections = NearbyConnectionsManager.Instance.GetEstablishedConnections();
                    foreach (var connectedEndpoint in establishedConnections)
                    {
                        GUILayout.Label($"endpoint: {connectedEndpoint}");

                        // disconnect endpoint
                        if (GUILayout.Button("Disconnect"))
                        {
                            NearbyConnectionsManager.Instance.Disconnect(connectedEndpoint);
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
    }
}