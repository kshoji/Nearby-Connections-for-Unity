package jp.kshoji.unity.nearby;

public interface ConnectionEventListener {
    void onConnectionInitiated(String endpointId, String endpointName, boolean isIncomingConnection);
    void onConnectionFailed(String endpointId);
    void onEndpointConnected(String endpointId);
    void onEndpointDisconnected(String endpointId);
}
