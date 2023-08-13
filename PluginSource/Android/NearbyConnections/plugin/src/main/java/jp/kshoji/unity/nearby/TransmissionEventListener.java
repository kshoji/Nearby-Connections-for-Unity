package jp.kshoji.unity.nearby;

public interface TransmissionEventListener {
    void onReceive(String endpointId, long id, byte[] payload);
    void onFileTransferComplete(String endpointId, long id, String filePath);
    void onFileTransferUpdate(String endpointId, long payloadId, long bytesTransferred, long totalSize);
}
