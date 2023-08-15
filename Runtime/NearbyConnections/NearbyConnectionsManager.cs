using System;
using System.Collections.Generic;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.ComponentModel;
using System.IO;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System.Runtime.InteropServices;
#endif
using AsyncOperation = System.ComponentModel.AsyncOperation;

namespace jp.kshoji.unity.nearby
{
    /// <summary>
    /// Nearby Connections Manager, will be registered as `DontDestroyOnLoad` GameObject
    /// </summary>
    public class NearbyConnectionsManager : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static Thread mainThread;
        private AndroidJavaObject connectionsManager;
        private bool permissionRequested;
#endif
        private AsyncOperation asyncOperation;

        private Action onInitializeCompleted;

        private readonly HashSet<string> discoveredEndpoints = new HashSet<string>();
        private readonly HashSet<string> pendingConnections = new HashSet<string>();
        private readonly HashSet<string> establishedConnections = new HashSet<string>();

        /// <summary>
        /// Get an instance<br />
        /// SHOULD be called by Unity's main thread.
        /// </summary>
        public static NearbyConnectionsManager Instance => lazyInstance.Value;

        private static readonly Lazy<NearbyConnectionsManager> lazyInstance = new Lazy<NearbyConnectionsManager>(() =>
        {
            var instance = new GameObject("NearbyConnectionsManager").AddComponent<NearbyConnectionsManager>();
            instance.asyncOperation = AsyncOperationManager.CreateOperation(null);

#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                DontDestroyOnLoad(instance);
            }
            else
            {
                Debug.Log("Don't initialize NearbyConnectionsManager while Unity Editor is not playing!");
            }
#else
            DontDestroyOnLoad(instance);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            mainThread = Thread.CurrentThread;
#endif

            return instance;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        private NearbyConnectionsManager()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            connectionsManager = new AndroidJavaObject("jp.kshoji.unity.nearby.ConnectionsManager");
#endif
        }

        ~NearbyConnectionsManager()
        {
            Terminate();
        }

#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string DllName = "NearbyUnityPlugin-osx";
#else
        private const string DllName = "__Internal";
#endif
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Advertising event listener
        /// </summary>
        private class AdvertisingEventListener : AndroidJavaProxy
        {
            public AdvertisingEventListener() : base("jp.kshoji.unity.nearby.AdvertisingEventListener")
            {
            }
            public void onAdvertisingStarted()
                => Instance.asyncOperation.Post(o => Instance.OnAdvertisingStarted?.Invoke(), null);
            public void onAdvertisingFailed()
                => Instance.asyncOperation.Post(o => Instance.OnAdvertisingFailed?.Invoke(), null);
        }
#endif

        public delegate void OnAdvertisingStartedDelegate();
        public event OnAdvertisingStartedDelegate OnAdvertisingStarted;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnAdvertisingStartedDelegate))]
        private static void IosOnAdvertisingStarted() =>
            Instance.asyncOperation.Post(_ => Instance.OnAdvertisingStarted?.Invoke(), null);
        [DllImport(DllName)]
        private static extern void SetAdvertisingStartedDelegate(OnAdvertisingStartedDelegate callback);
#endif

        public delegate void OnAdvertisingFailedDelegate();
        public event OnAdvertisingFailedDelegate OnAdvertisingFailed;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnAdvertisingFailedDelegate))]
        private static void IosOnAdvertisingFailed() =>
            Instance.asyncOperation.Post(_ => Instance.OnAdvertisingFailed?.Invoke(), null);
        [DllImport(DllName)]
        private static extern void SetAdvertisingFailedDelegate(OnAdvertisingFailedDelegate callback);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Discovery event listener
        /// </summary>
        private class DiscoveryEventListener : AndroidJavaProxy
        {
            public DiscoveryEventListener() : base("jp.kshoji.unity.nearby.DiscoveryEventListener")
            {
            }
            void onDiscoveryStarted()
                => Instance.asyncOperation.Post(o => Instance.OnDiscoveryStarted?.Invoke(), null);
            void onDiscoveryFailed()
                => Instance.asyncOperation.Post(o => Instance.OnDiscoveryFailed(), null);
            void onEndpointDiscovered(string endpointId)
                => Instance.asyncOperation.Post(o =>
                {
                    var discoveredEndpointId = (string)o;
                    lock (Instance.discoveredEndpoints)
                    {
                        Instance.discoveredEndpoints.Add(discoveredEndpointId);
                    }
                    Instance.OnEndpointDiscovered?.Invoke(discoveredEndpointId);
                }, endpointId);
            void onEndpointLost(string endpointId)
                => Instance.asyncOperation.Post(o =>
                {
                    var lostEndpointId = (string)o;
                    lock (Instance.discoveredEndpoints)
                    {
                        Instance.discoveredEndpoints.Remove(lostEndpointId);
                    }
                    Instance.OnEndpointLost?.Invoke(lostEndpointId);
                }, endpointId);
        }
#endif

        public delegate void OnDiscoveryStartedDelegate();
        public event OnDiscoveryStartedDelegate OnDiscoveryStarted;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnDiscoveryStartedDelegate))]
        private static void IosOnDiscoveryStarted() =>
            Instance.asyncOperation.Post(_ => Instance.OnDiscoveryStarted?.Invoke(), null);
        [DllImport(DllName)]
        private static extern void SetDiscoveryStartedDelegate(OnDiscoveryStartedDelegate callback);
#endif

        public delegate void OnDiscoveryFailedDelegate();
        public event OnDiscoveryFailedDelegate OnDiscoveryFailed;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnDiscoveryFailedDelegate))]
        private static void IosOnDiscoveryFailed() =>
            Instance.asyncOperation.Post(_ => Instance.OnDiscoveryFailed?.Invoke(), null);
        [DllImport(DllName)]
        private static extern void SetDiscoveryFailedDelegate(OnDiscoveryFailedDelegate callback);
#endif

        public delegate void OnEndpointDiscoveredDelegate(string endpointId);
        public event OnEndpointDiscoveredDelegate OnEndpointDiscovered;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnEndpointDiscoveredDelegate))]
        private static void IosOnEndpointDiscovered(string endpointId) =>
            Instance.asyncOperation.Post(o =>
            {
                var discoveredEndpointId = (string)o;
                lock (Instance.discoveredEndpoints)
                {
                    Instance.discoveredEndpoints.Add(discoveredEndpointId);
                }
                Instance.OnEndpointDiscovered?.Invoke(discoveredEndpointId);
            }, endpointId);
        [DllImport(DllName)]
        private static extern void SetEndpointDiscoveredDelegate(OnEndpointDiscoveredDelegate callback);
#endif

        public delegate void OnEndpointLostDelegate(string endpointId);
        public event OnEndpointLostDelegate OnEndpointLost;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnEndpointDiscoveredDelegate))]
        private static void IosOnEndpointLost(string endpointId) =>
            Instance.asyncOperation.Post(o =>
            {
                var lostEndpointId = (string)o;
                lock (Instance.discoveredEndpoints)
                {
                    Instance.discoveredEndpoints.Remove(lostEndpointId);
                }
                Instance.OnEndpointLost?.Invoke(lostEndpointId);
            }, endpointId);
        [DllImport(DllName)]
        private static extern void SetEndpointLostDelegate(OnEndpointLostDelegate callback);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Connection event listener
        /// </summary>
        private class ConnectionEventListener : AndroidJavaProxy
        {
            public ConnectionEventListener() : base("jp.kshoji.unity.nearby.ConnectionEventListener")
            {
            }
            void onConnectionInitiated(string endpointId, string endpointName, bool isIncomingConnection)
                => Instance.asyncOperation.Post(o =>
                {
                    lock (Instance.pendingConnections)
                    {
                        Instance.pendingConnections.Add(endpointId);
                    }
                    Instance.OnConnectionInitiated?.Invoke((string)((object[])o)[0], (string)((object[])o)[1], (bool)((object[])o)[2]);
                }, new object[] {endpointId, endpointName, isIncomingConnection});

            void onConnectionFailed(string endpointId)
                => Instance.asyncOperation.Post(o =>
                {
                    lock (Instance.pendingConnections)
                    {
                        Instance.pendingConnections.Remove(endpointId);
                    }
                    Instance.OnConnectionFailed?.Invoke((string)o);
                }, endpointId);

            void onEndpointConnected(string endpointId)
                => Instance.asyncOperation.Post(o =>
                {
                    var connectedEndpointId = (string)o;
                    lock (Instance.pendingConnections)
                    {
                        Instance.pendingConnections.Remove(endpointId);
                    }
                    lock (Instance.establishedConnections)
                    {
                        Instance.establishedConnections.Add(endpointId);
                    }
                    Instance.OnEndpointConnected?.Invoke(connectedEndpointId);
                }, endpointId);
            void onEndpointDisconnected(string endpointId)
                => Instance.asyncOperation.Post(o =>
                {
                    lock (Instance.establishedConnections)
                    {
                        Instance.establishedConnections.Remove(endpointId);
                    }
                    Instance.OnEndpointDisconnected?.Invoke((string)o);
                }, endpointId);
        }
#endif

        public delegate void OnConnectionInitiatedDelegate(string endpointId, string endpointName, bool isIncomingConnection);
        public event OnConnectionInitiatedDelegate OnConnectionInitiated;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnConnectionInitiatedDelegate))]
        private static void IosOnConnectionInitiated(string endpointId, string endpointName, bool isIncomingConnection) =>
            Instance.asyncOperation.Post(o =>
            {
                lock (Instance.pendingConnections)
                {
                    Instance.pendingConnections.Add(endpointId);
                }
                Instance.OnConnectionInitiated?.Invoke((string)((object[])o)[0], (string)((object[])o)[1], (bool)((object[])o)[2]);
            }, new object[] { endpointId, endpointName, isIncomingConnection });
        [DllImport(DllName)]
        private static extern void SetConnectionInitiatedDelegate(OnConnectionInitiatedDelegate callback);
#endif

        public delegate void OnConnectionFailedDelegate(string endpointId);
        public event OnConnectionFailedDelegate OnConnectionFailed;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnConnectionFailedDelegate))]
        private static void IosOnConnectionFailed(string endpointId) =>
            Instance.asyncOperation.Post(o =>
            {
                lock (Instance.pendingConnections)
                {
                    Instance.pendingConnections.Remove(endpointId);
                }
                Instance.OnConnectionFailed?.Invoke((string)o);
            }, endpointId);
        [DllImport(DllName)]
        private static extern void SetConnectionFailedDelegate(OnConnectionFailedDelegate callback);
#endif

        public delegate void OnEndpointConnectedDelegate(string endpointId);
        public event OnEndpointConnectedDelegate OnEndpointConnected;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnEndpointConnectedDelegate))]
        private static void IosOnEndpointConnected(string endpointId) =>
            Instance.asyncOperation.Post(o =>
            {
                var connectedEndpointId = (string)o;
                lock (Instance.pendingConnections)
                {
                    Instance.pendingConnections.Remove(endpointId);
                }
                lock (Instance.establishedConnections)
                {
                    Instance.establishedConnections.Add(endpointId);
                }
                Instance.OnEndpointConnected?.Invoke(connectedEndpointId);
            }, endpointId);
        [DllImport(DllName)]
        private static extern void SetEndpointConnectedDelegate(OnEndpointConnectedDelegate callback);
#endif

        public delegate void OnEndpointDisconnectedDelegate(string endpointId);
        public event OnEndpointDisconnectedDelegate OnEndpointDisconnected;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [AOT.MonoPInvokeCallback(typeof(OnEndpointDisconnectedDelegate))]
        private static void IosOnEndpointDisconnected(string endpointId) =>
            Instance.asyncOperation.Post(o =>
            {
                lock (Instance.establishedConnections)
                {
                    Instance.establishedConnections.Remove(endpointId);
                }
                Instance.OnEndpointDisconnected?.Invoke((string)o);
            }, endpointId);
        [DllImport(DllName)]
        private static extern void SetEndpointDisconnectedDelegate(OnEndpointDisconnectedDelegate callback);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Transmission event listener
        /// </summary>
        private class TransmissionEventListener : AndroidJavaProxy
        {
            public TransmissionEventListener() : base("jp.kshoji.unity.nearby.TransmissionEventListener")
            {
            }
            void onReceive(string endpointId, long id, byte[] payload)
                => Instance.asyncOperation.Post(o => Instance.OnReceive?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (byte[])((object[])o)[2]), new object[] {endpointId, id, payload});

            void onFileTransferComplete(string endpointId, long id, string filePath)
                => Instance.asyncOperation.Post(o => Instance.OnFileTransferComplete?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (string)((object[])o)[2]), new object[] {endpointId, id, filePath});
            void onFileTransferUpdate(string endpointId, long id, long bytesTransferred, long totalSize)
				=> Instance.asyncOperation.Post(o => Instance.OnFileTransferUpdate?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (long)((object[])o)[2], (long)((object[])o)[3]), new object[] {endpointId, id, bytesTransferred, totalSize});
            void onFileTransferFailed(string endpointId, long id)
				=> Instance.asyncOperation.Post(o => Instance.OnFileTransferFailed?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] {endpointId, id});
            void onFileTransferCancelled(string endpointId, long id)
				=> Instance.asyncOperation.Post(o => Instance.OnFileTransferCancelled?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] {endpointId, id});

            void onReceiveStream(string endpointId, long id, byte[] payload)
                => Instance.asyncOperation.Post(o => Instance.OnReceiveStream?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (byte[])((object[])o)[2]), new object[] {endpointId, id, payload});
            void onStreamTransferComplete(string endpointId, long id)
                => Instance.asyncOperation.Post(o => Instance.OnStreamTransferComplete?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] {endpointId, id});
            void onStreamTransferFailed(string endpointId, long id)
				=> Instance.asyncOperation.Post(o => Instance.OnStreamTransferFailed?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] {endpointId, id});
            void onStreamTransferCancelled(string endpointId, long id)
				=> Instance.asyncOperation.Post(o => Instance.OnStreamTransferCancelled?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] {endpointId, id});
        }
#endif

        public delegate void OnReceiveDelegate(string endpointId, long payloadId, byte[] payload);
        public event OnReceiveDelegate OnReceive;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnReceiveDelegate(string endpointId, long payloadId, int payloadLength, IntPtr payload);
        [AOT.MonoPInvokeCallback(typeof(IosOnReceiveDelegate))]
        private static void IosOnReceive(string endpointId, long payloadId, int payloadLength, IntPtr payload)
        {
            var mangedData = new byte[payloadLength];
            Marshal.Copy(payload, mangedData, 0, payloadLength);
            Instance.asyncOperation.Post(o => Instance.OnReceive?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (byte[])((object[])o)[2]), new object[] { endpointId, payloadId, mangedData });
            Marshal.FreeHGlobal(payload);
        }
        [DllImport(DllName)]
        private static extern void SetReceiveDelegate(IosOnReceiveDelegate callback);
#endif

        public delegate void OnFileTransferCompleteDelegate(string endpointId, long payloadId, string fileName);
        public event OnFileTransferCompleteDelegate OnFileTransferComplete;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnFileTransferCompleteDelegate(string endpointId, long payloadId, string fileName);
        [AOT.MonoPInvokeCallback(typeof(IosOnFileTransferCompleteDelegate))]
        private static void IosOnFileTransferComplete(string endpointId, long payloadId, string fileName)
        {
            Instance.asyncOperation.Post(o => Instance.OnFileTransferComplete?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (string)((object[])o)[2]), new object[] { endpointId, payloadId, fileName });
        }
        [DllImport(DllName)]
        private static extern void SetFileTransferCompleteDelegate(IosOnFileTransferCompleteDelegate callback);
#endif

        public delegate void OnFileTransferUpdateDelegate(string endpointId, long payloadId, long bytesTransferred, long totalSize);
        public event OnFileTransferUpdateDelegate OnFileTransferUpdate;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnFileTransferUpdateDelegate(string endpointId, long payloadId, long bytesTransferred, long totalSize);
        [AOT.MonoPInvokeCallback(typeof(IosOnFileTransferUpdateDelegate))]
        private static void IosOnFileTransferUpdate(string endpointId, long payloadId, long bytesTransferred, long totalSize)
        {
            Instance.asyncOperation.Post(o => Instance.OnFileTransferUpdate?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (long)((object[])o)[2], (long)((object[])o)[3]), new object[] { endpointId, payloadId, bytesTransferred, totalSize });
        }
        [DllImport(DllName)]
        private static extern void SetFileTransferUpdateDelegate(OnFileTransferUpdateDelegate callback);
#endif

        public delegate void OnFileTransferFailedDelegate(string endpointId, long payloadId);
        public event OnFileTransferFailedDelegate OnFileTransferFailed;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnFileTransferFailedDelegate(string endpointId, long payloadId);
        [AOT.MonoPInvokeCallback(typeof(IosOnFileTransferFailedDelegate))]
        private static void IosOnFileTransferFailed(string endpointId, long payloadId)
        {
            Instance.asyncOperation.Post(o => Instance.OnFileTransferFailed?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] { endpointId, payloadId });
        }
        [DllImport(DllName)]
        private static extern void SetFileTransferFailedDelegate(IosOnFileTransferFailedDelegate callback);
#endif

        public delegate void OnFileTransferCancelledDelegate(string endpointId, long payloadId);
        public event OnFileTransferCancelledDelegate OnFileTransferCancelled;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnFileTransferCancelledDelegate(string endpointId, long payloadId);
        [AOT.MonoPInvokeCallback(typeof(IosOnFileTransferCancelledDelegate))]
        private static void IosOnFileTransferCancelled(string endpointId, long payloadId)
        {
            Instance.asyncOperation.Post(o => Instance.OnFileTransferCancelled?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] { endpointId, payloadId });
        }
        [DllImport(DllName)]
        private static extern void SetFileTransferCancelledDelegate(IosOnFileTransferCancelledDelegate callback);
#endif

        public delegate void OnReceiveStreamDelegate(string endpointId, long payloadId, byte[] payload);
        public event OnReceiveStreamDelegate OnReceiveStream;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnReceiveStreamDelegate(string endpointId, long payloadId, int payloadLength, IntPtr payload);
        [AOT.MonoPInvokeCallback(typeof(IosOnReceiveStreamDelegate))]
        private static void IosOnReceiveStream(string endpointId, long payloadId, int payloadLength, IntPtr payload)
        {
            var mangedData = new byte[payloadLength];
            Marshal.Copy(payload, mangedData, 0, payloadLength);
            Instance.asyncOperation.Post(o => Instance.OnReceiveStream?.Invoke((string)((object[])o)[0], (long)((object[])o)[1], (byte[])((object[])o)[2]), new object[] { endpointId, payloadId, mangedData });
            Marshal.FreeHGlobal(payload);
        }
        [DllImport(DllName)]
        private static extern void SetReceiveStreamDelegate(IosOnReceiveStreamDelegate callback);
#endif

        public delegate void OnStreamTransferCompleteDelegate(string endpointId, long payloadId);
        public event OnStreamTransferCompleteDelegate OnStreamTransferComplete;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnStreamTransferCompleteDelegate(string endpointId, long payloadId);
        [AOT.MonoPInvokeCallback(typeof(IosOnStreamTransferCompleteDelegate))]
        private static void IosOnStreamTransferComplete(string endpointId, long payloadId)
        {
            Instance.asyncOperation.Post(o => Instance.OnStreamTransferComplete?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] { endpointId, payloadId });
        }
        [DllImport(DllName)]
        private static extern void SetStreamTransferCompleteDelegate(IosOnStreamTransferCompleteDelegate callback);
#endif

        public delegate void OnStreamTransferFailedDelegate(string endpointId, long payloadId);
        public event OnStreamTransferFailedDelegate OnStreamTransferFailed;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnStreamTransferFailedDelegate(string endpointId, long payloadId);
        [AOT.MonoPInvokeCallback(typeof(IosOnStreamTransferFailedDelegate))]
        private static void IosOnStreamTransferFailed(string endpointId, long payloadId)
        {
            Instance.asyncOperation.Post(o => Instance.OnStreamTransferFailed?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] { endpointId, payloadId });
        }
        [DllImport(DllName)]
        private static extern void SetStreamTransferFailedDelegate(IosOnStreamTransferFailedDelegate callback);
#endif

        public delegate void OnStreamTransferCancelledDelegate(string endpointId, long payloadId);
        public event OnStreamTransferCancelledDelegate OnStreamTransferCancelled;
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private delegate void IosOnStreamTransferCancelledDelegate(string endpointId, long payloadId);
        [AOT.MonoPInvokeCallback(typeof(IosOnStreamTransferCancelledDelegate))]
        private static void IosOnStreamTransferCancelled(string endpointId, long payloadId)
        {
            Instance.asyncOperation.Post(o => Instance.OnStreamTransferCancelled?.Invoke((string)((object[])o)[0], (long)((object[])o)[1]), new object[] { endpointId, payloadId });
        }
        [DllImport(DllName)]
        private static extern void SetStreamTransferCancelledDelegate(IosOnStreamTransferCancelledDelegate callback);
#endif

#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport(DllName)]
        private static extern void IosInitialize();

        [DllImport(DllName)]
        private static extern void IosStartAdvertising(string localEndpointName, string serviceId, int strategy);

        [DllImport(DllName)]
        private static extern void IosStopAdvertising();

        [DllImport(DllName)]
        private static extern bool IosIsAdvertising();

        [DllImport(DllName)]
        private static extern void IosStartDiscovering(string serviceId, int strategy);

        [DllImport(DllName)]
        private static extern void IosStopDiscovering();

        [DllImport(DllName)]
        private static extern bool IosIsDiscovering();

        [DllImport(DllName)]
        private static extern void IosConnectToEndpoint(string localEndpointName, string endpointId);

        [DllImport(DllName)]
        private static extern void IosAcceptConnection(string endpointId);

        [DllImport(DllName)]
        private static extern void IosRejectConnection(string endpointId);

        [DllImport(DllName)]
        private static extern void IosDisconnect(string endpointId);

        [DllImport(DllName)]
        private static extern void IosDisconnectFromAllEndpoints();

        [DllImport(DllName)]
        private static extern void IosStopAllEndpoints();

        [DllImport(DllName)]
        private static extern void IosSend(byte[] bytes, int length);

        [DllImport(DllName)]
        private static extern void IosSendToEndpoint(byte[] bytes, int length, string endpointId);

        [DllImport(DllName)]
        private static extern long IosSendFile(string path, string fileName);

        [DllImport(DllName)]
        private static extern long IosSendFileToEndpoint(string path, string fileName, string endpointId);

        [DllImport(DllName)]
        private static extern long IosSendStream(byte[] data, int length);

        [DllImport(DllName)]
        private static extern long IosSendStreamToEndpoint(byte[] data, int length, string endpointId);

        [DllImport(DllName)]
        private static extern void IosSendStreamUpdate(byte[] data, int length, long payloadId);

        [DllImport(DllName)]
        private static extern long IosCancelPayload(long payloadId);
#endif
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private void RequestPermissions()
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            var hasPermissions = connectionsManager.Call<bool>("hasPermissions");
            if (!hasPermissions)
            {
                permissionRequested = true;
                connectionsManager.Call("requestPermissions");
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && permissionRequested)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var hasPermissions = connectionsManager.Call<bool>("hasPermissions");
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }

                if (hasPermissions)
                {
                    permissionRequested = false;
                    // all permissions granted
                    Initialize(() =>
                    {
                        onInitializeCompleted?.Invoke();
                        onInitializeCompleted = null;
                    });
                }
                else
                {
                    Debug.Log($"Permissions are not granted.");
                }
            }
        }
#endif

        /// <summary>
        /// Initializes Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void Initialize(Action initializeCompletedAction)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
           if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            connectionsManager.Call("initialize", activity, new AdvertisingEventListener(),
                new DiscoveryEventListener(), new ConnectionEventListener(), new TransmissionEventListener());

            var hasPermissions = connectionsManager.Call<bool>("hasPermissions");
            if (hasPermissions)
            {
                initializeCompletedAction?.Invoke();
            }
            else
            {
                RequestPermissions();
                onInitializeCompleted = initializeCompletedAction;
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            SetAdvertisingStartedDelegate(IosOnAdvertisingStarted);
            SetAdvertisingFailedDelegate(IosOnAdvertisingFailed);
            SetDiscoveryStartedDelegate(IosOnDiscoveryStarted);
            SetDiscoveryFailedDelegate(IosOnDiscoveryFailed);
            SetEndpointDiscoveredDelegate(IosOnEndpointDiscovered);
            SetConnectionInitiatedDelegate(IosOnConnectionInitiated);
            SetEndpointConnectedDelegate(IosOnEndpointConnected);
            SetConnectionFailedDelegate(IosOnConnectionFailed);
            SetEndpointDisconnectedDelegate(IosOnEndpointDisconnected);
            SetReceiveDelegate(IosOnReceive);
            SetFileTransferCompleteDelegate(IosOnFileTransferComplete);
            SetFileTransferUpdateDelegate(IosOnFileTransferUpdate);
            SetFileTransferFailedDelegate(IosOnFileTransferFailed);
            SetFileTransferCancelledDelegate(IosOnFileTransferCancelled);
            SetReceiveStreamDelegate(IosOnReceiveStream);
            SetStreamTransferCompleteDelegate(IosOnStreamTransferComplete);
            SetStreamTransferFailedDelegate(IosOnStreamTransferFailed);
            SetStreamTransferCancelledDelegate(IosOnStreamTransferCancelled);
            IosInitialize();
            initializeCompletedAction?.Invoke();
#else
            initializeCompletedAction?.Invoke();
#endif
        }

        /// <summary>
        /// Network topology strategy
        /// </summary>
        public enum Strategy
        {
            P2P_POINT_TO_POINT,
            P2P_STAR,
            P2P_CLUSTER,
        }

        /// <summary>
        /// Convert Strategy into int
        /// </summary>
        /// <param name="strategy">the strategy enum</param>
        /// <returns>int value</returns>
        private int GetStrategy(Strategy strategy)
        {
            switch (strategy)
            {
                case Strategy.P2P_POINT_TO_POINT:
                    return 0;
                case Strategy.P2P_STAR:
                    return 1;
                case Strategy.P2P_CLUSTER:
                    return 2;
            }

            // default: P2P_STAR
            return 1;
        }

        /// <summary>
        /// Sets the device to advertising mode. It will broadcast to other devices in discovery mode.
        /// Either {@link #onAdvertisingStarted()} or {@link #onAdvertisingFailed()} will be called once
        /// we've found out if we successfully entered this mode.
        /// </summary>
        public void StartAdvertising(string localEndpointName, string serviceId, Strategy strategy)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("startAdvertising", localEndpointName, serviceId, GetStrategy(strategy));
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosStartAdvertising(localEndpointName, serviceId, GetStrategy(strategy));
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Stops advertising.
        /// </summary>
        public void StopAdvertising()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("stopAdvertising");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosStopAdvertising();
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Checks if currently advertising
        /// </summary>
        /// <returns>true: advertising</returns>
        public bool IsAdvertising()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            var result = connectionsManager.Call<bool>("isAdvertising");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }

            return result;
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return IosIsAdvertising();
#else
            // platform not supported: do nothing
            return false;
#endif
        }

        /// <summary>
        /// Sets the device to discovery mode. It will now listen for devices in advertising mode. Either
        /// {@link #onDiscoveryStarted()} or {@link #onDiscoveryFailed()} will be called once we've found
        /// out if we successfully entered this mode.
        /// </summary>
        public void StartDiscovering(string serviceId, Strategy strategy)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            lock (discoveredEndpoints)
            {
                discoveredEndpoints.Clear();
            }

            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("startDiscovering", serviceId, GetStrategy(strategy));
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            lock (discoveredEndpoints)
            {
                discoveredEndpoints.Clear();
            }

            IosStartDiscovering(serviceId, GetStrategy(strategy));
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }
        
        /// <summary>
        /// Stops discovery.
        /// </summary>
        public void StopDiscovering()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("stopDiscovering");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosStopDiscovering();
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Checks if currently discovering
        /// </summary>
        /// <returns>true: discovering</returns>
        public bool IsDiscovering()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            var result = connectionsManager.Call<bool>("isDiscovering");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }

            return result;
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return IosIsDiscovering();
#else
            // platform not supported: do nothing
            return false;
#endif
        }

        /// <summary>
        /// Obtains all discovered endpoint IDs 
        /// </summary>
        /// <returns>set of endpoint IDs</returns>
        public HashSet<string> GetDiscoveredEndpoints()
        {
            lock (discoveredEndpoints)
            {
                return new HashSet<string>(discoveredEndpoints);
            }
        }

        /// <summary>
        /// Obtains all pending endpoint IDs 
        /// </summary>
        /// <returns>set of endpoint IDs</returns>
        public HashSet<string> GetPendingConnections()
        {
            lock (pendingConnections)
            {
                return new HashSet<string>(pendingConnections);
            }
        }

        /// <summary>
        /// Obtains all established endpoint IDs 
        /// </summary>
        /// <returns>set of endpoint IDs</returns>
        public HashSet<string> GetEstablishedConnections()
        {
            lock (establishedConnections)
            {
                return new HashSet<string>(establishedConnections);
            }
        }
        /// <summary>
        /// Connect to the endpoint
        /// </summary>
        /// <param name="localEndpointName">endpoint name of this device</param>
        /// <param name="endpointId">the endpoint ID to begin connecting</param>
        public void Connect(string localEndpointName, string endpointId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("connectToEndpoint", localEndpointName, endpointId);
            lock (Instance.discoveredEndpoints)
            {
                Instance.discoveredEndpoints.Remove(endpointId);
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosConnectToEndpoint(localEndpointName, endpointId);
            lock (Instance.discoveredEndpoints)
            {
                Instance.discoveredEndpoints.Remove(endpointId);
            }
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Accept the incoming connection
        /// </summary>
        /// <param name="endpointId">the endpoint ID</param>
        public void AcceptConnection(string endpointId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("acceptConnection", endpointId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosAcceptConnection(endpointId);
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Reject the incoming connection
        /// </summary>
        /// <param name="endpointId">the endpoint ID</param>
        public void RejectConnection(string endpointId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("rejectConnection", endpointId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosRejectConnection(endpointId);
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Disconnect the connection
        /// </summary>
        /// <param name="endpointId">the endpoint ID</param>
        public void Disconnect(string endpointId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("disconnect", endpointId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosDisconnect(endpointId);
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Disconnect the all connections
        /// </summary>
        public void DisconnectFromAllEndpoints()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("disconnectFromAllEndpoints");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosDisconnectFromAllEndpoints();
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Send data to the specified endpoint
        /// </summary>
        /// <param name="bytes">the data</param>
        /// <param name="endpointId">the endpoint ID, send to the all endpoints if null specified</param>
        public void Send(byte[] bytes, string endpointId = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            if (endpointId == null)
            {
                connectionsManager.Call("send", bytes);
            }
            else
            {
                connectionsManager.Call("send", bytes, endpointId);
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (endpointId == null)
            {
                IosSend(bytes, bytes.Length);
            }
            else
            {
                IosSendToEndpoint(bytes, bytes.Length, endpointId);
            }
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Send file to the specified endpoint
        /// </summary>
        /// <param name="filePath">the file path</param>
        /// <param name="endpointId">the endpoint ID, send to the all endpoints if null specified</param>
        /// <returns></returns>
        public long Send(string filePath, string endpointId = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var path = Path.GetFullPath(filePath);
            long result;
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            if (endpointId == null)
            {
                result = connectionsManager.Call<long>("sendFile", path);
            }
            else
            {
                result = connectionsManager.Call<long>("sendFile", path, endpointId);
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
            return result;
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            var path = Path.GetFullPath(filePath);
            var fileName = Path.GetFileName(filePath);
            if (endpointId == null)
            {
                return IosSendFile($"file://{path}", fileName);
            }
            else
            {
                return IosSendFileToEndpoint($"file://{path}", fileName, endpointId);
            }
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
            return 0;
#endif
        }

        public long SendStream(byte[] payloadBytes, string endpointId = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            long result;
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            if (endpointId == null)
            {
                result = connectionsManager.Call<long>("sendStream", payloadBytes);
            }
            else
            {
                result = connectionsManager.Call<long>("sendStream", payloadBytes, endpointId);
            }
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
            return result;
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            long result;
            if (endpointId == null)
            {
                result = IosSendStream(payloadBytes, payloadBytes.Length);
            }
            else
            {
                result = IosSendStreamToEndpoint(payloadBytes, payloadBytes.Length, endpointId);
            }
            return result;
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
            return 0;
#endif
        }

        public void SendStream(long payloadId, byte[] payloadBytes)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            long result;
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("sendStream", payloadId, payloadBytes);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosSendStreamUpdate(payloadBytes, payloadBytes.Length, payloadId);
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        /// <summary>
        /// Cancel transfer with specified payload ID
        /// </summary>
        /// <param name="payloadId">the payload ID</param>
        public void CancelTransfer(long payloadId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("cancel", payloadId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosCancelPayload(payloadId);
#else
            // platform not supported: do nothing
            Debug.Log($"Platform {Application.platform} is not supported.");
#endif
        }

        private void OnApplicationQuit()
        {
            // terminates system if not terminated
            Terminate();
        }

        /// <summary>
        /// Terminates Plugin system
        /// </summary>
        public void Terminate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            connectionsManager.Call("stopAllEndpoints");
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            IosStopAllEndpoints();
#else
#endif
        }
    }
}