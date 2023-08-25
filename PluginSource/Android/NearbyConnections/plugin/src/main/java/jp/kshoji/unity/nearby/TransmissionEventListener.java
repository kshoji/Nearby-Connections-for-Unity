package jp.kshoji.unity.nearby;

public interface TransmissionEventListener {
    void onReceive(String endpointId, long id, byte[] payload);
    void onFileTransferComplete(String endpointId, long id, String localUrl, String filePath);
    void onFileTransferUpdate(String endpointId, long payloadId, long bytesTransferred, long totalSize);
    void onFileTransferFailed(String endpointId, long payloadId);
    void onFileTransferCancelled(String endpointId, long payloadId);
    void onReceiveStream(String endpointId, long id, byte[] payload);
    void onStreamTransferComplete(String endpointId, long id);
    void onStreamTransferFailed(String endpointId, long payloadId);
    void onStreamTransferCancelled(String endpointId, long payloadId);
}
