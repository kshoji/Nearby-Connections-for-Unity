package jp.kshoji.unity.nearby;

public interface TransmissionEventListener {
    void onReceive(String endpointId, long id, byte[] payload);
    void onReceiveFile(String endpointId, long id, String filePath);
}
