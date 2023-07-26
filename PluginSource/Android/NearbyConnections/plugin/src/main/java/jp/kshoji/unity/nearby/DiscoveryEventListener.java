package jp.kshoji.unity.nearby;

public interface DiscoveryEventListener {
    void onDiscoveryStarted();
    void onDiscoveryFailed();
    void onEndpointDiscovered(String endpointId);
    void onEndpointLost(String endpointId);
}
