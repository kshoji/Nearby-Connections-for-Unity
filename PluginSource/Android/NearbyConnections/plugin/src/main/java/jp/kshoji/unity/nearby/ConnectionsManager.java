package jp.kshoji.unity.nearby;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.util.Log;

import androidx.annotation.NonNull;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.google.android.gms.nearby.Nearby;
import com.google.android.gms.nearby.connection.AdvertisingOptions;
import com.google.android.gms.nearby.connection.ConnectionInfo;
import com.google.android.gms.nearby.connection.ConnectionLifecycleCallback;
import com.google.android.gms.nearby.connection.ConnectionResolution;
import com.google.android.gms.nearby.connection.ConnectionsClient;
import com.google.android.gms.nearby.connection.ConnectionsStatusCodes;
import com.google.android.gms.nearby.connection.DiscoveredEndpointInfo;
import com.google.android.gms.nearby.connection.DiscoveryOptions;
import com.google.android.gms.nearby.connection.EndpointDiscoveryCallback;
import com.google.android.gms.nearby.connection.Payload;
import com.google.android.gms.nearby.connection.PayloadCallback;
import com.google.android.gms.nearby.connection.PayloadTransferUpdate;
import com.google.android.gms.nearby.connection.Strategy;
import com.google.android.gms.tasks.OnFailureListener;
import com.google.android.gms.tasks.OnSuccessListener;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

public class ConnectionsManager {
    private static final String TAG = "ConnectionsManager";
    private static final boolean USE_LOGS = false;

    /**
     * These permissions are required before connecting to Nearby Connections.
     */
    private static final String[] REQUIRED_PERMISSIONS;
    static {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            REQUIRED_PERMISSIONS =
                    new String[] {
                            android.Manifest.permission.BLUETOOTH_SCAN,
                            android.Manifest.permission.BLUETOOTH_ADVERTISE,
                            android.Manifest.permission.BLUETOOTH_CONNECT,
                            android.Manifest.permission.ACCESS_WIFI_STATE,
                            android.Manifest.permission.CHANGE_WIFI_STATE,
                            android.Manifest.permission.ACCESS_COARSE_LOCATION,
                            android.Manifest.permission.ACCESS_FINE_LOCATION,
                            android.Manifest.permission.NEARBY_WIFI_DEVICES,
                    };
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            REQUIRED_PERMISSIONS =
                    new String[] {
                            android.Manifest.permission.BLUETOOTH_SCAN,
                            android.Manifest.permission.BLUETOOTH_ADVERTISE,
                            android.Manifest.permission.BLUETOOTH_CONNECT,
                            android.Manifest.permission.ACCESS_WIFI_STATE,
                            android.Manifest.permission.CHANGE_WIFI_STATE,
                            android.Manifest.permission.ACCESS_COARSE_LOCATION,
                            android.Manifest.permission.ACCESS_FINE_LOCATION,
                    };
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            REQUIRED_PERMISSIONS =
                    new String[] {
                            android.Manifest.permission.BLUETOOTH,
                            android.Manifest.permission.BLUETOOTH_ADMIN,
                            android.Manifest.permission.ACCESS_WIFI_STATE,
                            android.Manifest.permission.CHANGE_WIFI_STATE,
                            android.Manifest.permission.ACCESS_COARSE_LOCATION,
                            android.Manifest.permission.ACCESS_FINE_LOCATION,
                    };
        } else {
            REQUIRED_PERMISSIONS =
                    new String[] {
                            android.Manifest.permission.BLUETOOTH,
                            android.Manifest.permission.BLUETOOTH_ADMIN,
                            android.Manifest.permission.ACCESS_WIFI_STATE,
                            android.Manifest.permission.CHANGE_WIFI_STATE,
                            Manifest.permission.ACCESS_COARSE_LOCATION,
                    };
        }
    }

    private static final int REQUEST_CODE_REQUIRED_PERMISSIONS = 1;

    private Activity context;

    /** Our handler to Nearby Connections. */
    private ConnectionsClient connectionsClient;

    /** The devices we've discovered near us. */
    private final Map<String, Endpoint> discoveredEndpoints = new HashMap<>();

    /**
     * The devices we have pending connections to. They will stay pending until we call {@link
     * #acceptConnection(String)} or {@link #rejectConnection(String)}.
     */
    private final Map<String, Endpoint> pendingConnections = new HashMap<>();

    /**
     * The devices we are currently connected to. For advertisers, this may be large. For discoverers,
     * there will only be one entry in this map.
     */
    private final Map<String, Endpoint> establishedConnections = new HashMap<>();

    /**
     * True if we are asking a discovered device to connect to us. While we ask, we cannot ask another
     * device.
     */
    private boolean isConnecting = false;

    /** True if we are discovering. */
    private boolean isDiscovering = false;

    /** True if we are advertising. */
    private boolean isAdvertising = false;

    // callbacks set from Unity
    private AdvertisingEventListener advertisingEventListener;
    private DiscoveryEventListener discoveryEventListener;
    private ConnectionEventListener connectionEventListener;
    private TransmissionEventListener transmissionEventListener;

    private final Map<Long, FileTransfer> fileTransferDictionary = new HashMap<>();
    private static final class FileTransfer {
        private final InputStream inputStream;
        private final FileOutputStream fileOutputStream;
        private final String path;
        FileTransfer(InputStream inputStream, FileOutputStream fileOutputStream, String path) {
            this.inputStream = inputStream;
            this.fileOutputStream = fileOutputStream;
            this.path = path;
        }
    }

    public void initialize(Activity context,
                           AdvertisingEventListener advertisingEventListener,
                           DiscoveryEventListener discoveryEventListener,
                           ConnectionEventListener connectionEventListener,
                           TransmissionEventListener transmissionEventListener) {
        connectionsClient = Nearby.getConnectionsClient(context);
        this.context = context;
        this.advertisingEventListener = advertisingEventListener;
        this.discoveryEventListener = discoveryEventListener;
        this.connectionEventListener = connectionEventListener;
        this.transmissionEventListener = transmissionEventListener;
    }

    /**
     * Returns {@code true} if the app was granted all the permissions. Otherwise, returns {@code false}.
     */
    public boolean hasPermissions() {
        for (String permission : REQUIRED_PERMISSIONS) {
            if (ContextCompat.checkSelfPermission(context, permission) != PackageManager.PERMISSION_GRANTED) {
                return false;
            }
        }
        return true;
    }

    public void requestPermissions() {
        if (!hasPermissions()) {
            if (Build.VERSION.SDK_INT < 23) {
                ActivityCompat.requestPermissions(
                        context, REQUIRED_PERMISSIONS, REQUEST_CODE_REQUIRED_PERMISSIONS);
            } else {
                context.requestPermissions(REQUIRED_PERMISSIONS, REQUEST_CODE_REQUIRED_PERMISSIONS);
            }
        }
    }

    /** Callbacks for connections to other devices. */
    private final ConnectionLifecycleCallback mConnectionLifecycleCallback =
            new ConnectionLifecycleCallback() {
                @Override
                public void onConnectionInitiated(@NonNull String endpointId, ConnectionInfo connectionInfo) {
                    if (USE_LOGS) {
                        Log.d(TAG,
                                String.format(
                                        "onConnectionInitiated(endpointId=%s, endpointName=%s)",
                                        endpointId, connectionInfo.getEndpointName()));
                    }
                    Endpoint endpoint = new Endpoint(endpointId, connectionInfo.getEndpointName());
                    synchronized (pendingConnections) {
                        pendingConnections.put(endpointId, endpoint);
                    }
                    ConnectionsManager.this.onConnectionInitiated(endpoint.getId(), connectionInfo);
                }

                @Override
                public void onConnectionResult(@NonNull String endpointId, @NonNull ConnectionResolution result) {
                    if (USE_LOGS) {
                        Log.d(TAG, String.format("onConnectionResponse(endpointId=%s, result=%s)", endpointId, result));
                    }

                    // We're no longer connecting
                    isConnecting = false;

                    Endpoint pendingConnection;
                    synchronized (pendingConnections) {
                        pendingConnection = pendingConnections.remove(endpointId);
                    }
                    if (!result.getStatus().isSuccess()) {
                        if (USE_LOGS) {
                            Log.w(TAG,
                                    String.format(
                                            "Connection failed. Received status [%d]%s.",
                                            result.getStatus().getStatusCode(),
                                            result.getStatus().getStatusMessage() != null
                                                    ? result.getStatus().getStatusMessage()
                                                    : ConnectionsStatusCodes.getStatusCodeString(result.getStatus().getStatusCode())
                                    )
                            );
                        }
                        if (pendingConnection != null) {
                            onConnectionFailed(pendingConnection);
                        }
                        return;
                    }

                    if (pendingConnection != null) {
                        connectedToEndpoint(pendingConnection);
                    }
                }

                @Override
                public void onDisconnected(@NonNull String endpointId) {
                    if (USE_LOGS) {
                        Log.d(TAG, "onDisconnected from endpoint " + endpointId);
                    }
                    synchronized (establishedConnections) {
                        if (!establishedConnections.containsKey(endpointId)) {
                            if (USE_LOGS) {
                                Log.w(TAG, "Unexpected disconnection from endpoint " + endpointId);
                            }
                            return;
                        }
                        disconnectedFromEndpoint(establishedConnections.get(endpointId));
                    }
                }
            };

    // network strategy
    private Strategy getStrategy(final int strategy) {
        switch (strategy) {
            case 0:
                return Strategy.P2P_POINT_TO_POINT;
            case 1:
                return Strategy.P2P_STAR;
            case 2:
                return Strategy.P2P_CLUSTER;
            default:
                return Strategy.P2P_STAR;
        }
    }

    // advertising
    /**
     * Sets the device to advertising mode. It will broadcast to other devices in discovery mode.
     * Either {@link #onAdvertisingStarted()} or {@link #onAdvertisingFailed()} will be called once
     * we've found out if we successfully entered this mode.
     */
    protected void startAdvertising(final String localEndpointName, final String serviceId, final int strategy) {
        if (isAdvertising) {
            connectionsClient.stopAdvertising();
        }
        isAdvertising = true;

        AdvertisingOptions.Builder advertisingOptions = new AdvertisingOptions.Builder();
        advertisingOptions.setStrategy(getStrategy(strategy));

        connectionsClient
                .startAdvertising(
                        localEndpointName,
                        serviceId,
                        mConnectionLifecycleCallback,
                        advertisingOptions.build())
                .addOnSuccessListener(
                        new OnSuccessListener<Void>() {
                            @Override
                            public void onSuccess(Void unusedResult) {
                                if (USE_LOGS) {
                                    Log.v(TAG, "Now advertising endpoint " + localEndpointName);
                                }
                                onAdvertisingStarted();
                            }
                        })
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                isAdvertising = false;
                                if (USE_LOGS) {
                                    Log.w(TAG, "startAdvertising() failed.", e);
                                }
                                onAdvertisingFailed();
                            }
                        });
    }

    /** Stops advertising. */
    protected void stopAdvertising() {
        isAdvertising = false;
        connectionsClient.stopAdvertising();
    }

    protected boolean isAdvertising() {
        return isAdvertising;
    }

    /** Called when advertising successfully starts. Override this method to act on the event. */
    protected void onAdvertisingStarted() {
        advertisingEventListener.onAdvertisingStarted();
    }

    /** Called when advertising fails to start. Override this method to act on the event. */
    protected void onAdvertisingFailed() {
        advertisingEventListener.onAdvertisingFailed();
    }

    // discovering
    /**
     * Sets the device to discovery mode. It will now listen for devices in advertising mode. Either
     * {@link #onDiscoveryStarted()} or {@link #onDiscoveryFailed()} will be called once we've found
     * out if we successfully entered this mode.
     */
    protected void startDiscovering(final String serviceId, final int strategy) {
        if (isDiscovering) {
            connectionsClient.stopDiscovery();
        }
        isDiscovering = true;
        synchronized (discoveredEndpoints) {
            discoveredEndpoints.clear();
        }
        DiscoveryOptions.Builder discoveryOptions = new DiscoveryOptions.Builder();
        discoveryOptions.setStrategy(getStrategy(strategy));

        connectionsClient
                .startDiscovery(
                        serviceId,
                        new EndpointDiscoveryCallback() {
                            @Override
                            public void onEndpointFound(@NonNull String endpointId, @NonNull DiscoveredEndpointInfo info) {
                                if (USE_LOGS) {
                                    Log.d(TAG,
                                            String.format(
                                                    "onEndpointFound(endpointId=%s, serviceId=%s, endpointName=%s)",
                                                    endpointId, info.getServiceId(), info.getEndpointName()));
                                }

                                if (serviceId.equals(info.getServiceId())) {
                                    Endpoint endpoint = new Endpoint(endpointId, info.getEndpointName());
                                    synchronized (discoveredEndpoints) {
                                        discoveredEndpoints.put(endpointId, endpoint);
                                    }
                                    onEndpointDiscovered(endpoint);
                                }
                            }

                            @Override
                            public void onEndpointLost(@NonNull String endpointId) {
                                if (USE_LOGS) {
                                    Log.d(TAG, String.format("onEndpointLost(endpointId=%s)", endpointId));
                                }
                                synchronized (discoveredEndpoints) {
                                    discoveredEndpoints.remove(endpointId);
                                }
                                ConnectionsManager.this.onEndpointLost(endpointId);
                            }
                        },
                        discoveryOptions.build())
                .addOnSuccessListener(
                        new OnSuccessListener<Void>() {
                            @Override
                            public void onSuccess(Void unusedResult) {
                                onDiscoveryStarted();
                            }
                        })
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                isDiscovering = false;
                                if (USE_LOGS) {
                                    Log.w(TAG, "startDiscovering() failed.", e);
                                }
                                onDiscoveryFailed();
                            }
                        });
    }

    /** Stops discovery. */
    protected void stopDiscovering() {
        isDiscovering = false;
        connectionsClient.stopDiscovery();
    }

    /** Returns {@code true} if currently discovering. */
    protected boolean isDiscovering() {
        return isDiscovering;
    }

    /** Called when discovery successfully starts. Override this method to act on the event. */
    protected void onDiscoveryStarted() {
        discoveryEventListener.onDiscoveryStarted();
    }

    /** Called when discovery fails to start. Override this method to act on the event. */
    protected void onDiscoveryFailed() {
        discoveryEventListener.onDiscoveryFailed();
    }

    /**
     * Called when a remote endpoint is discovered. To connect to the device, call {@link
     * #connectToEndpoint(String, String)}.
     */
    protected void onEndpointDiscovered(Endpoint endpoint) {
        discoveryEventListener.onEndpointDiscovered(endpoint.getId());
    }

    /**
     * Called when a remote endpoint is lost.
     */
    protected void onEndpointLost(String endpointId) {
        discoveryEventListener.onEndpointLost(endpointId);
    }

    // connections

    /** Disconnects from the given endpoint. */
    protected void disconnect(String endpointId) {
        Log.d(TAG, "disconnect " + endpointId);
        connectionsClient.disconnectFromEndpoint(endpointId);
        connectionEventListener.onEndpointDisconnected(endpointId);
        synchronized (establishedConnections) {
            establishedConnections.remove(endpointId);
        }
    }

    /** Disconnects from all currently connected endpoints. */
    protected void disconnectFromAllEndpoints() {
        synchronized (establishedConnections) {
            for (Endpoint endpoint : establishedConnections.values()) {
                connectionsClient.disconnectFromEndpoint(endpoint.getId());
                connectionEventListener.onEndpointDisconnected(endpoint.getId());
            }
            establishedConnections.clear();
        }
    }

    /** Resets and clears all state in Nearby Connections. */
    protected void stopAllEndpoints() {
        connectionsClient.stopAllEndpoints();
        isAdvertising = false;
        isDiscovering = false;
        isConnecting = false;
        synchronized (discoveredEndpoints) {
            discoveredEndpoints.clear();
        }
        synchronized (pendingConnections) {
            pendingConnections.clear();
        }
        synchronized (establishedConnections) {
            establishedConnections.clear();
        }
    }

    /**
     * Sends a connection request to the endpoint. Either {@link #onConnectionInitiated(String,
     * ConnectionInfo)} or {@link #onConnectionFailed(Endpoint)} will be called once we've found out
     * if we successfully reached the device.
     */
    protected void connectToEndpoint(final String localEndpointName, final String endpointId) {
        Endpoint endpoint;
        synchronized (discoveredEndpoints) {
            endpoint = discoveredEndpoints.get(endpointId);
        }
        if (endpoint == null)
        {
            if (USE_LOGS) {
                Log.v(TAG, "The endpoint ID" + endpointId + " not found");
            }
            return;
        }

        if (USE_LOGS) {
            Log.v(TAG, "Sending a connection request to endpoint " + endpoint);
        }
        // Mark ourselves as connecting so we don't connect multiple times
        isConnecting = true;

        // Ask to connect
        connectionsClient
                .requestConnection(localEndpointName, endpoint.getId(), mConnectionLifecycleCallback)
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                if (USE_LOGS) {
                                    Log.w(TAG, "requestConnection() failed.", e);
                                }
                                isConnecting = false;
                                onConnectionFailed(endpoint);
                            }
                        });
    }

    /** Returns {@code true} if we're currently attempting to connect to another device. */
    protected final boolean isConnecting() {
        return isConnecting;
    }

    private void connectedToEndpoint(Endpoint endpoint) {
        if (USE_LOGS) {
            Log.d(TAG, String.format("connectedToEndpoint(endpoint=%s)", endpoint));
        }
        synchronized (establishedConnections) {
            establishedConnections.put(endpoint.getId(), endpoint);
        }
        onEndpointConnected(endpoint);
    }

    private void disconnectedFromEndpoint(Endpoint endpoint) {
        if (USE_LOGS) {
            Log.d(TAG, String.format("disconnectedFromEndpoint(endpoint=%s)", endpoint));
        }
        synchronized (establishedConnections) {
            establishedConnections.remove(endpoint.getId());
        }
        onEndpointDisconnected(endpoint);
    }

    /**
     * Called when a connection with this endpoint has failed. Override this method to act on the
     * event.
     */
    protected void onConnectionFailed(Endpoint endpoint) {
        connectionEventListener.onConnectionFailed(endpoint.getId());
    }

    /** Called when someone has connected to us. Override this method to act on the event. */
    protected void onEndpointConnected(Endpoint endpoint) {
        connectionEventListener.onEndpointConnected(endpoint.getId());
    }

    /** Called when someone has disconnected. Override this method to act on the event. */
    protected void onEndpointDisconnected(Endpoint endpoint) {
        connectionEventListener.onEndpointDisconnected(endpoint.getId());
    }

    /**
     * Called when a pending connection with a remote endpoint is created. Use {@link ConnectionInfo}
     * for metadata about the connection (like incoming vs outgoing, or the authentication token). If
     * we want to continue with the connection, call {@link #acceptConnection(String)}. Otherwise,
     * call {@link #rejectConnection(String)}.
     */
    protected void onConnectionInitiated(String endpointId, ConnectionInfo connectionInfo) {
        connectionEventListener.onConnectionInitiated(endpointId, connectionInfo.getEndpointName(), connectionInfo.isIncomingConnection());
    }

    /** Accepts a connection request. */
    protected void acceptConnection(final String endpointId) {
        connectionsClient
                .acceptConnection(endpointId, mPayloadCallback)
                .addOnSuccessListener(new OnSuccessListener<Void>() {
                    @Override
                    public void onSuccess(@NonNull Void unused) {
                        if (USE_LOGS) {
                            Log.w(TAG, "acceptConnection() succeeded.");
                        }
                    }
                })
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                if (USE_LOGS) {
                                    Log.w(TAG, "acceptConnection() failed.", e);
                                }
                            }
                        });
    }

    /** Rejects a connection request. */
    protected void rejectConnection(final String endpointId) {
        connectionsClient
                .rejectConnection(endpointId)
                .addOnSuccessListener(new OnSuccessListener<Void>() {
                    @Override
                    public void onSuccess(@NonNull Void unused) {
                        if (USE_LOGS) {
                            Log.w(TAG, "rejectConnection() succeeded.");
                        }
                    }
                })
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                if (USE_LOGS) {
                                    Log.w(TAG, "rejectConnection() failed.", e);
                                }
                            }
                        });
    }

    // data transmission

    /**
     * Sends a file {@link Payload} to all currently connected endpoints.
     *
     * @param filePath The file you want to send.
     */
    protected long sendFile(String filePath) {
        try {
            Payload payload = Payload.fromFile(new File(filePath));
            synchronized (establishedConnections) {
                send(payload, establishedConnections.keySet());
            }
            return payload.getId();
        } catch (FileNotFoundException e) {
            throw new RuntimeException(e);
        }
    }

    /**
     * Sends a file {@link Payload} to the specified endpoint.
     *
     * @param filePath The file you want to send.
     * @param endpointId The endpoint ID
     */
    protected long sendFile(String filePath, String endpointId) {
        try {
            Payload payload = Payload.fromFile(new File(filePath));
            Set<String> endpoints = new HashSet<>();
            endpoints.add(endpointId);
            send(payload, endpoints);
            return payload.getId();
        } catch (FileNotFoundException e) {
            throw new RuntimeException(e);
        }
    }

    /**
     * Sends a bytes {@link Payload} to all currently connected endpoints.
     *
     * @param bytes The data you want to send.
     */
    protected void send(byte[] bytes) {
        Payload payload = Payload.fromBytes(bytes);
        synchronized (establishedConnections) {
            send(payload, establishedConnections.keySet());
        }
    }

    /**
     * Sends a bytes {@link Payload} to the specified endpoint.
     *
     * @param bytes The data you want to send.
     * @param endpointId The endpoint ID
     */
    protected void send(byte[] bytes, String endpointId) {
        Payload payload = Payload.fromBytes(bytes);
        Set<String> endpoints = new HashSet<>();
        endpoints.add(endpointId);
        send(payload, endpoints);
    }

    private void send(Payload payload, Set<String> endpoints) {
        connectionsClient
                .sendPayload(new ArrayList<>(endpoints), payload)
                .addOnFailureListener(
                        new OnFailureListener() {
                            @Override
                            public void onFailure(@NonNull Exception e) {
                                if (USE_LOGS) {
                                    Log.w(TAG, "sendPayload() failed.", e);
                                }
                            }
                        });
    }

    /**
     * Cancels the specified payloadId
     * @param payloadId the payload ID
     */
    protected void cancel(long payloadId) {
        connectionsClient.cancelPayload(payloadId);
    }

    /**
     * Callbacks for payloads (bytes of data) sent from another device to us.
     */
    private final PayloadCallback mPayloadCallback =
            new PayloadCallback() {
                @Override
                public void onPayloadReceived(@NonNull String endpointId, @NonNull Payload payload) {
                    if (USE_LOGS) {
                        Log.d(TAG, String.format("onPayloadReceived(endpointId=%s, payload=%s)", endpointId, payload));
                    }
                    Endpoint endpoint;
                    synchronized (establishedConnections) {
                        endpoint = establishedConnections.get(endpointId);
                    }
                    if (endpoint != null) {
                        onReceive(endpoint, payload);
                    }
                }

                @Override
                public void onPayloadTransferUpdate(@NonNull String endpointId, @NonNull PayloadTransferUpdate update) {
                    if (USE_LOGS) {
                        Log.d(TAG, String.format("onPayloadTransferUpdate(endpointId=%s, update=%s)", endpointId, update));
                    }

                    // Notify transfer update
                    FileTransfer fileTransfer = fileTransferDictionary.get(update.getPayloadId());
                    if (fileTransfer != null) {
                        // Receiving
                        try {
                            byte[] buffer = new byte[1024];
                            int len;
                            while ((len = fileTransfer.inputStream.read(buffer)) != -1) {
                                fileTransfer.fileOutputStream.write(buffer, 0, len);
                            }
                        } catch (IOException e) {
                            throw new RuntimeException(e);
                        }
                        if (update.getStatus() == PayloadTransferUpdate.Status.IN_PROGRESS) {
                            transmissionEventListener.onFileTransferUpdate(endpointId, update.getPayloadId(), update.getBytesTransferred(), update.getTotalBytes());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.FAILURE) {
                            // Close streams
                            try {
                                fileTransfer.inputStream.close();
                            } catch (IOException ignored) {
                            }
                            try {
                                fileTransfer.fileOutputStream.close();
                            } catch (IOException ignored) {
                            }
                            fileTransferDictionary.remove(update.getPayloadId());

                            transmissionEventListener.onFileTransferFailed(endpointId, update.getPayloadId());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.CANCELED) {
                            // Close streams
                            try {
                                fileTransfer.inputStream.close();
                            } catch (IOException ignored) {
                            }
                            try {
                                fileTransfer.fileOutputStream.close();
                            } catch (IOException ignored) {
                            }
                            fileTransferDictionary.remove(update.getPayloadId());

                            transmissionEventListener.onFileTransferCancelled(endpointId, update.getPayloadId());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.SUCCESS) {
                            // Close streams
                            try {
                                fileTransfer.inputStream.close();
                            } catch (IOException ignored) {
                            }
                            try {
                                fileTransfer.fileOutputStream.close();
                            } catch (IOException ignored) {
                            }
                            fileTransferDictionary.remove(update.getPayloadId());

                            // Notify transfer finished
                            transmissionEventListener.onFileTransferComplete(endpointId, update.getPayloadId(), fileTransfer.path);
                        }
                    } else {
                        // Sending
                        if (update.getStatus() == PayloadTransferUpdate.Status.IN_PROGRESS) {
                            transmissionEventListener.onFileTransferUpdate(endpointId, update.getPayloadId(), update.getBytesTransferred(), update.getTotalBytes());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.FAILURE) {
                            transmissionEventListener.onFileTransferFailed(endpointId, update.getPayloadId());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.CANCELED) {
                            transmissionEventListener.onFileTransferCancelled(endpointId, update.getPayloadId());
                        } else if (update.getStatus() == PayloadTransferUpdate.Status.SUCCESS) {
                            // Notify transfer finished
                            transmissionEventListener.onFileTransferComplete(endpointId, update.getPayloadId(), null);
                        }
                    }
                }
            };

    /**
     * Someone connected to us has sent us data. Override this method to act on the event.
     *
     * @param endpoint The sender.
     * @param payload The data.
     */
    protected void onReceive(Endpoint endpoint, Payload payload) {
        switch (payload.getType()) {
            case Payload.Type.BYTES:
                transmissionEventListener.onReceive(endpoint.getId(), payload.getId(), payload.asBytes());
                break;
            case Payload.Type.FILE:
                try {
                    Uri uri = payload.asFile().asUri();
                    InputStream in = context.getContentResolver().openInputStream(uri);
                    File tempFile = File.createTempFile("temp", ".bin", context.getCacheDir());
                    FileOutputStream out = new FileOutputStream(tempFile);

                    byte[] buffer = new byte[1024];
                    int len;
                    while ((len = in.read(buffer)) != -1) {
                        out.write(buffer, 0, len);
                    }
                    fileTransferDictionary.put(payload.getId(), new FileTransfer(in, out, tempFile.getAbsolutePath()));
                } catch (NullPointerException e) {
                    throw new RuntimeException(e);
                } catch (IOException e) {
                    throw new RuntimeException(e);
                }
                break;
            case Payload.Type.STREAM:
                break;
        }
    }

    /** Represents a device we can talk to. */
    protected static class Endpoint {
        @NonNull
        private final String id;
        @NonNull private final String name;

        private Endpoint(@NonNull String id, @NonNull String name) {
            this.id = id;
            this.name = name;
        }

        @NonNull
        public String getId() {
            return id;
        }

        @NonNull
        public String getName() {
            return name;
        }

        @Override
        public boolean equals(Object obj) {
            if (obj instanceof Endpoint) {
                Endpoint other = (Endpoint) obj;
                return id.equals(other.id);
            }
            return false;
        }

        @Override
        public int hashCode() {
            return id.hashCode();
        }

        @NonNull
        @Override
        public String toString() {
            return String.format("Endpoint{id=%s, name=%s}", id, name);
        }
    }
}
