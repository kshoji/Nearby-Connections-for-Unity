package jp.kshoji.unity.nearby;

public interface TransmissionEventListener {
    void onReceive(String endpointId, long id, byte[] payload);
}
