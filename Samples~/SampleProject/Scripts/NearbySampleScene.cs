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
        private static HashSet<long> sendFilePayloads = new HashSet<long>();
        private static Stream sendStreamPayload;
        private static Dictionary<long, Stream> readStreamPayloads = new Dictionary<long, Stream>();

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
            NearbyConnectionsManager.Instance.OnEndpointDiscovered += endpointId =>
            {
                receivedMessages.Add($"OnEndpointDiscovered id: {endpointId}");
            };

            NearbyConnectionsManager.Instance.OnConnectionInitiated += (endpointId, endpointName, connection) =>
            {
                receivedMessages.Add($"OnConnectionInitiated id: {endpointId}, endpointName: {endpointName}, connection: {connection}");
                if (autoAcceptConnection)
                {
                    NearbyConnectionsManager.Instance.AcceptConnection(endpointId);
                }
            };
            NearbyConnectionsManager.Instance.OnConnectionFailed += endpointId =>
            {
                receivedMessages.Add($"OnConnectionInitiated id: {endpointId}");
            };
            NearbyConnectionsManager.Instance.OnEndpointConnected += endpointId =>
            {
                receivedMessages.Add($"OnEndpointConnected id: {endpointId}");
            };
            NearbyConnectionsManager.Instance.OnEndpointDisconnected += endpointId =>
            {
                receivedMessages.Add($"OnEndpointDisconnected id: {endpointId}");
            };

            NearbyConnectionsManager.Instance.OnReceive += (endpointId, payloadId, payload) =>
            {
                Debug.Log($"OnReceive id: {endpointId}, l: {payloadId}, payload: {string.Join(", ", payload)}");
                receivedMessages.Add($"OnReceive id: {endpointId}, l: {payloadId}, payload: {Encoding.UTF8.GetString(payload)}");
            };

            NearbyConnectionsManager.Instance.OnFileTransferComplete += (endpointId, payloadId, localUrl, fileName) =>
            {
                Debug.Log($"OnFileTransferComplete id: {endpointId}, l: {payloadId}, localUrl: {localUrl}, fileName: {fileName}");
                receivedMessages.Add($"OnFileTransferComplete id: {endpointId}, l: {payloadId}, localUrl: {localUrl}, fileName: {fileName}");
            };

            NearbyConnectionsManager.Instance.OnFileTransferUpdate += (endpointId, payloadId, bytesTransferred, totalSize) =>
            {
                // too much calling on transferring large file, so output logs only
                Debug.Log($"OnFileTransferUpdate id: {endpointId}, l: {payloadId}, progress: {bytesTransferred} / {totalSize})");
            };

            NearbyConnectionsManager.Instance.OnFileTransferCancelled += (endpointId, payloadId) =>
            {
                receivedMessages.Add($"OnFileTransferCancelled [{endpointId}]({payloadId})");
            };

            NearbyConnectionsManager.Instance.OnFileTransferFailed += (endpointId, payloadId) =>
            {
                receivedMessages.Add($"OnFileTransferFailed [{endpointId}]({payloadId})");
            };

            NearbyConnectionsManager.Instance.OnReceiveStream += (endpointId, payloadId, payload) =>
            {
                receivedMessages.Add($"OnReceiveStream [{endpointId}]({payloadId})");
                readStreamPayloads.Add(payloadId, payload);
            };

            NearbyConnectionsManager.Instance.OnStreamTransferComplete += (endpointId, payloadId) =>
            {
                receivedMessages.Add($"OnStreamTransferComplete [{endpointId}]({payloadId})");
                if (readStreamPayloads.ContainsKey(payloadId))
                {
                    readStreamPayloads[payloadId].Close();
                    readStreamPayloads[payloadId].Dispose();
                    readStreamPayloads.Remove(payloadId);
                }
            };

            NearbyConnectionsManager.Instance.OnStreamTransferCancelled += (endpointId, payloadId) =>
            {
                receivedMessages.Add($"OnStreamTransferCancelled [{endpointId}]({payloadId})");
                if (readStreamPayloads.ContainsKey(payloadId))
                {
                    readStreamPayloads[payloadId].Close();
                    readStreamPayloads[payloadId].Dispose();
                    readStreamPayloads.Remove(payloadId);
                }
            };

            NearbyConnectionsManager.Instance.OnStreamTransferFailed += (endpointId, payloadId) =>
            {
                receivedMessages.Add($"OnStreamTransferFailed [{endpointId}]({payloadId})");
                if (readStreamPayloads.ContainsKey(payloadId))
                {
                    readStreamPayloads[payloadId].Close();
                    readStreamPayloads[payloadId].Dispose();
                    readStreamPayloads.Remove(payloadId);
                }
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

        private Vector2 receivedMessageWindowScrollPosition;
        private float guiScale;
        private bool autoAcceptConnection;
        private string sendText;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Layout)
            {
                return;
            }

            CheckReadStreams();

            GUIUtility.ScaleAroundPivot(new Vector2(guiScale, guiScale), Vector2.zero);

            receivedMessageWindowRect = GUILayout.Window(ReceivedMessageWindow, receivedMessageWindowRect, OnGUIWindow, "Received Messages");
            connectionWindowRect = GUILayout.Window(ConnectionWindow, connectionWindowRect, OnGUIWindow, "Nearby Connections");
        }

        private void CheckReadStreams()
        {
            var buffer = new byte[1024];
            var closedStreams = new HashSet<long>();
            foreach (var stream in readStreamPayloads)
            {
                var read = stream.Value.Read(buffer);
                if (read > 0)
                {
                    var readBytes = new byte[read];
                    Array.Copy(buffer, 0, readBytes, 0, read);
                    receivedMessages.Add($"Stream received payloadId: {stream.Key}, data: {Encoding.UTF8.GetString(readBytes)} ({string.Join(", ", readBytes)})");
                }
                else if (read == -1)
                {
                    receivedMessages.Add($"Stream closed payloadId: {stream.Key}");
                    stream.Value.Close();
                    closedStreams.Add(stream.Key);
                }
            }

            foreach (var closedStream in closedStreams)
            {
                readStreamPayloads.Remove(closedStream);
            }
        }

        private void OnGUIWindow(int id)
        {
            switch (id)
            {
                case ReceivedMessageWindow:
                    receivedMessageWindowScrollPosition = GUILayout.BeginScrollView(receivedMessageWindowScrollPosition);
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
                    if (GUILayout.Button("Send with Bytes") && !string.IsNullOrEmpty(sendText))
                    {
                        NearbyConnectionsManager.Instance.Send(Encoding.UTF8.GetBytes(sendText));
                    }

                    if (GUILayout.Button("Send with Stream"))
                    {
                        var sendData = string.IsNullOrEmpty(sendText) ? null : Encoding.UTF8.GetBytes(sendText);
                        if (sendStreamPayload == null)
                        {
                            // new connection
                            if (sendData == null)
                            {
                                return;
                            }
                            sendStreamPayload = NearbyConnectionsManager.Instance.StartSendStream();
                            sendStreamPayload.Write(sendData);
                            receivedMessages.Add($"StartStream payload: {sendStreamPayload}");
                        }
                        else
                        {
                            // already connected
                            if (sendData == null)
                            {
                                sendStreamPayload.Close();
                                sendStreamPayload.Dispose();
                                sendStreamPayload = null;
                                return;
                            }

                            sendStreamPayload.Write(sendData);
                        }
                    }

                    if (GUILayout.Button("Close Stream"))
                    {
                        if (sendStreamPayload != null)
                        {
                            sendStreamPayload.Close();
                            sendStreamPayload.Dispose();
                            sendStreamPayload = null;
                        }
                    }

                    if (GUILayout.Button("Send a File"))
                    {
                        IEnumerator GetFileContentsAndSend(string filePath)
                        {
                            var request = UnityWebRequest.Get(filePath);
                            yield return request.SendWebRequest();
                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
                                using var fileStream = new FileStream(tempFileName, FileMode.OpenOrCreate);
                                fileStream.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
 
                                var payloadId = NearbyConnectionsManager.Instance.Send(tempFileName);
                                receivedMessages.Add($"Send File payloadId: {payloadId}");
                                lock (sendFilePayloads)
                                {
                                    sendFilePayloads.Add(payloadId);
                                }
                            }
                            else
                            {
                                Debug.LogError($"File {filePath} not found.");
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
                            lock (sendFilePayloads)
                            {
                                sendFilePayloads.Add(payloadId);
                            }
                        }
                    }

                    if (GUILayout.Button("Cancel Send File"))
                    {
                        lock (sendFilePayloads)
                        {
                            foreach (var payloadId in sendFilePayloads)
                            {
                                NearbyConnectionsManager.Instance.CancelTransfer(payloadId);
                            }
                            sendFilePayloads.Clear();
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