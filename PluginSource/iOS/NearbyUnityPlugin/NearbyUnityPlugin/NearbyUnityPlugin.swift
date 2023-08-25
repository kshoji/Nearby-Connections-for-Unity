//
//  NearbyUnityPlugin.swift
//  NearbyUnityPlugin
//
//  Created by Kaoru Shoji on 2023/07/17.
//
import Foundation
import NearbyConnections

@objc protocol AdvertisingEventDelegate : AnyObject {
    func onAdvertisingStarted()
    func onAdvertisingFailed()
}

@objc protocol ConnectionEventDelegate : AnyObject {
    func onConnectionInitiated(endpointId: String, endpointName: String , isIncomingConnection: Bool)
    func onConnectionFailed(endpointId: String)
    func onEndpointConnected(endpointId: String)
    func onEndpointDisconnected(endpointId: String)
}

@objc protocol DiscoveryEventDelegate : AnyObject {
    func onDiscoveryStarted()
    func onDiscoveryFailed()
    func onEndpointDiscovered(endpointId: String)
    func onEndpointLost(endpointId: String)
}

@objc protocol TransmissionEventDelegate : AnyObject {
    // Bytes
    func onReceive(endpointId: String, id: Int64, payload: [UInt8]);

    // File
    func onFileTransferComplete(endpointId: String, id: Int64, localUrl: String?, fileName: String?);
    func onFileTransferUpdate(endpointId: String, id: Int64, bytesTransferred: Int64, totalSize: Int64);
    func onFileTransferFailed(endpointId: String, id: Int64);
    func onFileTransferCancelled(endpointId: String, id: Int64);

    // Stream
    func onReceiveStream(endpointId: String, id: Int64, streamData: [UInt8]);
    func onStreamTransferComplete(endpointId: String, id: Int64);
    func onStreamTransferFailed(endpointId: String, id: Int64);
    func onStreamTransferCancelled(endpointId: String, id: Int64);
}

struct Payload: Identifiable {
    let id: PayloadID
    var type: PayloadType
    var status: Status
    let isIncoming: Bool
    let cancellationToken: CancellationToken?
    // File
    let localURL: URL?
    let fileName: String?
    // Stream
    let outputStream: OutputStream?
    var inputStream: InputStream?

    enum PayloadType {
        case bytes, stream, file
    }
    enum Status {
        case inProgress(Progress), success, failure, canceled
    }
}

struct ConnectedEndpoint: Identifiable {
    let id: UUID
    let endpointID: EndpointID
    let endpointName: String
    var payloads: [Payload] = []
}

struct ConnectionRequest: Identifiable {
    let id: UUID
    let endpointID: EndpointID
    let endpointName: String
    let pin: String
    let shouldAccept: ((Bool) -> Void)
}

struct DiscoveredEndpoint: Identifiable {
    let id: UUID
    let endpointID: EndpointID
    let endpointName: String
}

@objc public class NearbyUnityPlugin : NSObject {
    @objc var advertisingEventDelegate:AdvertisingEventDelegate?
    @objc var connectionEventDelegate:ConnectionEventDelegate?
    @objc var discoveryEventDelegate:DiscoveryEventDelegate?
    @objc var transmissionEventDelegate:TransmissionEventDelegate?
    
    var connectionManager: ConnectionManager!
    var advertiser: Advertiser?
    var discoverer: Discoverer?
    
    var advertisingServiceId: String
    var advertisingStrategy: Strategy
    @objc var isAdvertising: Bool
    
    var discoveringServiceId: String
    var discoveringStrategy: Strategy
    @objc var isDiscovering: Bool
    
    @Published private(set) var requests: [ConnectionRequest] = []
    @Published private(set) var connections: [ConnectedEndpoint] = []
    @Published private(set) var endpoints: [DiscoveredEndpoint] = []
    
    @objc public static let shared = NearbyUnityPlugin()
    
    override init() {
        advertisingServiceId = ""
        advertisingStrategy = Strategy.star
        isAdvertising = false
        
        discoveringServiceId = ""
        discoveringStrategy = Strategy.star
        isDiscovering = false
    }
    
    @objc func startAdvertising(localEndpointName: String, serviceId: String, strategyInt: Int) {
#if DEBUG
        print("startAdvertising localEndpointName: \(localEndpointName), serviceId: \(serviceId), strategy: \(strategyInt)")
#endif
        advertisingServiceId = serviceId
        advertisingStrategy = {
            switch (strategyInt) {
            case 0: return Strategy.pointToPoint
            case 1: return Strategy.star
            case 2: return Strategy.cluster
            default: return Strategy.star
            }
        }()
        
        connectionManager = ConnectionManager(serviceID: advertisingServiceId, strategy: advertisingStrategy)
        connectionManager.delegate = self

        if (isAdvertising) {
            advertiser?.stopAdvertising()
            advertiser = nil
        }
        if (advertiser == nil) {
            advertiser = Advertiser(connectionManager: connectionManager)
        }
        advertiser?.delegate = self
        advertiser?.startAdvertising(using: localEndpointName.data(using: .utf8)!)
        isAdvertising = true
        advertisingEventDelegate?.onAdvertisingStarted()
    }
    
    @objc func stopAdvertising() {
#if DEBUG
        print("stopAdvertising")
#endif
        if (advertiser != nil) {
            advertiser?.stopAdvertising()
        }
        isAdvertising = false
    }
    
    @objc func startDiscovering(serviceId: String, strategyInt: Int) {
#if DEBUG
        print("startDiscovering serviceId: \(serviceId), strategy: \(strategyInt)")
#endif
        discoveringServiceId = serviceId
        discoveringStrategy = {
            switch (strategyInt) {
            case 0: return Strategy.pointToPoint
            case 1: return Strategy.star
            case 2: return Strategy.cluster
            default: return Strategy.star
            }
        }()
        
        connectionManager = ConnectionManager(serviceID: discoveringServiceId, strategy: discoveringStrategy)
        connectionManager.delegate = self
        
        if (isDiscovering) {
            discoverer?.stopDiscovery()
            discoverer = nil
        }
        if (discoverer == nil) {
            discoverer = Discoverer(connectionManager: connectionManager)
        }
        discoverer?.delegate = self
        discoverer?.startDiscovery()
        discoveryEventDelegate?.onDiscoveryStarted()
        isDiscovering = true
    }
    
    @objc func stopDiscovering() {
#if DEBUG
        print("stopDiscovering")
#endif
        if (discoverer != nil) {
            discoverer?.stopDiscovery()
        }
        isDiscovering = false;
    }
    
    @objc func requestConnection(to endpointID: EndpointID, localEndpointName: String) {
#if DEBUG
        print("requestConnection endpointID: \(endpointID), localEndpointName: \(localEndpointName)")
#endif
        discoverer?.requestConnection(to: endpointID, using: localEndpointName.data(using: .utf8)!)
    }
    
    @objc func acceptConnection(to endpointID: EndpointID) {
#if DEBUG
        print("acceptConnection endpointID: \(endpointID)")
#endif
        guard let index = requests.firstIndex(where: { $0.endpointID == endpointID }) else {
#if DEBUG
            print("acceptConnection endpointID: \(endpointID) not found")
            requests.forEach {
                print("acceptConnection requests' endpointID: \($0.endpointID)")
            }
#endif
            return
        }

#if DEBUG
        print("acceptConnection endpointID: \(endpointID) index: \(index), request: \(requests[index])")
#endif
        requests[index].shouldAccept(true)
    }

    @objc func rejectConnection(to endpointID: EndpointID) {
#if DEBUG
        print("rejectConnection endpointID: \(endpointID)")
#endif
        guard let index = requests.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }

        requests[index].shouldAccept(false)
    }

    @objc func disconnect(from endpointID: EndpointID) {
#if DEBUG
        print("disconnect endpointID: \(endpointID)")
#endif
        connectionManager?.disconnect(from: endpointID)
    }
    
    @objc func disconnectFromAllEndpoints() {
#if DEBUG
        print("disconnectFromAllEndpoints")
#endif
        connections.forEach {
            connectionManager?.disconnect(from: $0.endpointID)
        }
    }
    
    @objc func stopAllEndpoints() {
#if DEBUG
        print("stopAllEndpoints")
#endif
        disconnectFromAllEndpoints()
        stopAdvertising()
        stopDiscovering()
        requests = []
        connections = []
        endpoints = []
    }

    @objc func send(url: URL, fileName: String) -> Int64 {
#if DEBUG
        print("send payload: \(fileName)")
#endif
        let payloadID = PayloadID.unique()
        let endpointIDs = connections.map{ $0.endpointID }

        if (connections.count < 1) {
            // no endpoints connected
            return 0
        }

        let token = connectionManager?.sendResource(at: url, withName: fileName, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .file,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: url,
            fileName: fileName,
            outputStream: nil,
            inputStream: nil
        )

#if DEBUG
        print("send payload: \(payload), payloadId: \(payloadID)")
#endif

        for index in connections.indices {
            connections[index].payloads.insert(payload, at: 0)
        }

        return payload.id
    }

    @objc func send(url: URL, fileName: String, endpointID: String) -> Int64 {
        guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return 0
        }

        let payloadID = PayloadID.unique()
        let endpointIDs = [endpointID]

        let token = connectionManager?.sendResource(at: url, withName: fileName, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .file,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: url,
            fileName: fileName,
            outputStream: nil,
            inputStream: nil
        )

        connections[index].payloads.insert(payload, at: 0)

        return payload.id
    }

    @objc func send(data: UnsafePointer<UInt8>, count: Int64) -> Int64 {
        let payloadID = PayloadID.unique()
        let endpointIDs = connections.map{ $0.endpointID }

        if (connections.count < 1) {
            // no endpoints connected
            return 0
        }

        var inputStream: InputStream? = nil
        var outputStream: OutputStream? = nil
        Stream.getBoundStreams(withBufferSize: 1048576, inputStream: &inputStream, outputStream: &outputStream)
        if inputStream == nil || outputStream == nil {
            return 0
        }
        inputStream!.open()
        outputStream!.open()
        
        let token = connectionManager?.startStream(inputStream!, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .stream,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: nil,
            fileName: nil,
            outputStream: outputStream,
            inputStream: inputStream
        )

        outputStream!.write(data, maxLength: Int(count))

#if DEBUG
        print("send payload: \(payload), payloadID: \(payloadID), inputStream: \(String(describing: inputStream)), outputStream: \(String(describing: outputStream))")
#endif

        for index in connections.indices {
            connections[index].payloads.insert(payload, at: 0)
        }

        return payload.id
    }
    
    @objc func send(data: UnsafePointer<UInt8>, count: Int64, payloadID: PayloadID) {
#if DEBUG
        print("send payload payloadID: \(payloadID) count: \(count) data: \(data)")
#endif
        for endpoint in connections {
            guard let payloadIndex = endpoint.payloads.firstIndex(where: { $0.id == payloadID }) else {
                continue
            }
            if endpoint.payloads[payloadIndex].outputStream == nil {
                continue
            }

#if DEBUG
            print("send payload update payloadID: \(payloadID) endpoint: \(endpoint)")
#endif

            if (count == 0) {
#if DEBUG
                print("send payload update payloadID: \(payloadID) endpoint: \(endpoint) count == 0, last payload")
#endif
                // last payload
                if (endpoint.payloads[payloadIndex].outputStream!.streamStatus == .open) {
                    endpoint.payloads[payloadIndex].outputStream!.close()
                }
                if (endpoint.payloads[payloadIndex].inputStream!.streamStatus == .open) {
                    endpoint.payloads[payloadIndex].inputStream!.close()
                }
                continue
            }

            endpoint.payloads[payloadIndex].outputStream!.write(data, maxLength: Int(count))
        }
    }

    @objc func send(data: UnsafePointer<UInt8>, count: Int64, endpointID: String) -> Int64 {
        guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return 0
        }

        let payloadID = PayloadID.unique()
        let endpointIDs = [endpointID]

        var inputStream: InputStream? = nil
        var outputStream: OutputStream? = nil
        Stream.getBoundStreams(withBufferSize: 1048576, inputStream: &inputStream, outputStream: &outputStream)
        if inputStream == nil || outputStream == nil {
            return 0
        }
        inputStream!.open()
        outputStream!.open()

        outputStream!.write(data, maxLength: Int(count))

        let token = connectionManager?.startStream(inputStream!, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .stream,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: nil,
            fileName: nil,
            outputStream: outputStream,
            inputStream: inputStream
        )

#if DEBUG
        print("send payload: \(payload), payloadId: \(payloadID)")
#endif

        connections[index].payloads.insert(payload, at: 0)

        return payload.id
    }

    @objc func cancel(endpointID: EndpointID, payloadID: PayloadID) {
        guard let connectionIndex = connections.firstIndex(where: { $0.endpointID == endpointID }),
              let payloadIndex = connections[connectionIndex].payloads.firstIndex(where: { $0.id == payloadID }) else {
            return
        }
        
        let payload = connections[connectionIndex].payloads[payloadIndex]
        payload.cancellationToken?.cancel()
    }

    @objc func cancel(payloadID: PayloadID) {
        connections.forEach { connection in
            guard let payloadIndex = connection.payloads.firstIndex(where: { $0.id == payloadID }) else {
                return
            }

            connection.payloads[payloadIndex].cancellationToken?.cancel()
        }
    }

    @objc func send(payload: Data) {
#if DEBUG
        print("send payload: \(payload)")
#endif
        let payloadID = PayloadID.unique()
        let endpointIDs = connections.map{ $0.endpointID }
#if DEBUG
        connections.forEach {
            print("send payload to endpoint: \($0.endpointID)")
        }
#endif

        if (connections.count < 1) {
            // no endpoints connected
#if DEBUG
            print("send payload: no endpoints connected.")
#endif
            return
        }

        let token = connectionManager?.send(payload, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .bytes,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: nil,
            fileName: nil,
            outputStream: nil,
            inputStream: nil
        )
        
        for index in connections.indices {
            connections[index].payloads.insert(payload, at: 0)
        }
    }

    @objc func send(endpointID: String, payload: Data) {
#if DEBUG
        print("send endpointID: \(endpointID), payload: \(payload)")
#endif
        guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }

        let payloadID = PayloadID.unique()
        let endpointIDs = [endpointID]
        let token = connectionManager?.send(payload, to: endpointIDs, id: payloadID)
        let payload = Payload(
            id: payloadID,
            type: .bytes,
            status: .inProgress(Progress()),
            isIncoming: false,
            cancellationToken: token,
            localURL: nil,
            fileName: nil,
            outputStream: nil,
            inputStream: nil
        )

        connections[index].payloads.insert(payload, at: 0)
    }
}

extension NearbyUnityPlugin: DiscovererDelegate {
    public func discoverer(_ discoverer: Discoverer, didFind endpointID: EndpointID, with context: Data) {
#if DEBUG
        print("discoverer didFind endpointID: \(endpointID), context: \(context)")
#endif
        let endpoint = DiscoveredEndpoint(
            id: UUID(),
            endpointID: endpointID,
            endpointName: String(data: context, encoding: .utf8)!
        )
        endpoints.insert(endpoint, at: 0)

        // Notify to Unity
        discoveryEventDelegate?.onEndpointDiscovered(endpointId: endpointID)
    }

    public func discoverer(_ discoverer: Discoverer, didLose endpointID: EndpointID) {
#if DEBUG
        print("discoverer didLose endpointID: \(endpointID)")
#endif
        guard let index = endpoints.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }
        endpoints.remove(at: index)
        
        // Notify to Unity
        discoveryEventDelegate?.onEndpointLost(endpointId: endpointID)
    }
}

extension NearbyUnityPlugin: AdvertiserDelegate {
    public func advertiser(_ advertiser: NearbyConnections.Advertiser, didReceiveConnectionRequestFrom endpointID: NearbyConnections.EndpointID, with context: Data, connectionRequestHandler: @escaping (Bool) -> Void) {
#if DEBUG
        print("advertiser didReceiveConnectionRequestFrom endpointID: \(endpointID), context: \(context)")
#endif
        let endpoint = DiscoveredEndpoint(
            id: UUID(),
            endpointID: endpointID,
            endpointName: String(data: context, encoding: .utf8)!
        )
        endpoints.insert(endpoint, at: 0)
        connectionRequestHandler(true)

        // Notify to Unity
        connectionEventDelegate?.onConnectionInitiated(endpointId: endpointID, endpointName: endpoint.endpointName, isIncomingConnection: true)
    }
}

extension NearbyUnityPlugin: ConnectionManagerDelegate {
    public func connectionManager(_ connectionManager: ConnectionManager, didReceive verificationCode: String, from endpointID: EndpointID, verificationHandler: @escaping (Bool) -> Void) {
#if DEBUG
        print("connectionManager didReceive verificationCode: \(verificationCode), endpointID: \(endpointID)")
#endif
        guard let index = endpoints.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }
        let endpoint = endpoints.remove(at: index)
        let request = ConnectionRequest(
            id: endpoint.id,
            endpointID: endpointID,
            endpointName: endpoint.endpointName,
            pin: verificationCode,
            shouldAccept: { accept in
                verificationHandler(accept)
            }
        )
        requests.insert(request, at: 0)

        // Notify to Unity
        connectionEventDelegate?.onConnectionInitiated(endpointId: endpointID, endpointName: endpoint.endpointName, isIncomingConnection: true)
    }

    public func connectionManager(_ connectionManager: ConnectionManager, didReceive data: Data, withID payloadID: PayloadID, from endpointID: EndpointID) {
#if DEBUG
        print("connectionManager didReceive data: \(data), payloadID: \(payloadID), endpointID: \(endpointID)")
#endif
        let payload = Payload(
            id: payloadID,
            type: .bytes,
            status: .success,
            isIncoming: true,
            cancellationToken: nil,
            localURL: nil,
            fileName: nil,
            outputStream: nil,
            inputStream: nil
        )
        guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }
        connections[index].payloads.insert(payload, at: 0)

        // Notify to Unity
        transmissionEventDelegate?.onReceive(endpointId: endpointID, id: payloadID, payload: [UInt8](data))
    }

    public func connectionManager(_ connectionManager: ConnectionManager, didReceive stream: InputStream, withID payloadID: PayloadID, from endpointID: EndpointID, cancellationToken token: CancellationToken) {
#if DEBUG
        print("connectionManager didReceive stream, payloadID: \(payloadID), endpointID: \(endpointID), cancellationToken: \(token)")
#endif
        guard let connectionIndex = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }

        guard let payloadIndex = connections[connectionIndex].payloads.firstIndex(where: { $0.id == payloadID }) else {
            // new payload
            let payload = Payload(
                id: payloadID,
                type: .stream,
                status: .success,
                isIncoming: true,
                cancellationToken: token,
                localURL: nil,
                fileName: nil,
                outputStream: nil,
                inputStream: stream
            )
            connections[connectionIndex].payloads.insert(payload, at: 0)

 #if DEBUG
            print("payload.inputStream: \(String(describing: payload.inputStream)), stream: \(stream)")
 #endif
            return
        }

        // existing payload
        var payload = connections[connectionIndex].payloads[payloadIndex]
        payload.inputStream = stream
    }

    func readStreamAndCallback(endpointID: EndpointID, payload: Payload) {
        if payload.isIncoming == false {
            return
        }

        let bufferSize = 1024
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer {
            buffer.deallocate()
        }

        if (payload.inputStream!.streamStatus != .open) {
            payload.inputStream!.open()
        }

        // read once
#if DEBUG
        print("readStreamAndCallback reading.. \(String(describing: payload.inputStream))")
#endif
        let read = payload.inputStream!.read(buffer, maxLength: bufferSize)
        if read < 0 {
            // stream error
#if DEBUG
            print("connectionManager didReceive stream, error: \(String(describing: payload.inputStream!.streamError))")
#endif
            return
        } else if read == 0 {
#if DEBUG
            print("readStreamAndCallback read == 0 EOF")
#endif
            // EOF
            return
        }
#if DEBUG
        print("readStreamAndCallback read: \(read) bytes")
#endif

        // Notify to Unity (stream received)
        let bts = withUnsafePointer(to: &buffer[0]) {
            $0.withMemoryRebound(to: UInt8.self, capacity: read) { $0 }
        }
        let bytesArray = UnsafeBufferPointer(start: bts, count: read).map { $0 }
        transmissionEventDelegate?.onReceiveStream(endpointId: endpointID, id: payload.id, streamData: bytesArray)
    }
    
    public func connectionManager(_ connectionManager: ConnectionManager, didStartReceivingResourceWithID payloadID: PayloadID, from endpointID: EndpointID, at localURL: URL, withName name: String, cancellationToken token: CancellationToken) {
#if DEBUG
        print("connectionManager didStartReceivingResourceWithID payloadID: \(payloadID), endpointID: \(endpointID), localURL: \(localURL), name: \(name), cancellationToken: \(token)")
#endif
        let payload = Payload(
            id: payloadID,
            type: .file,
            status: .inProgress(Progress()),
            isIncoming: true,
            cancellationToken: token,
            localURL: localURL,
            fileName: name,
            outputStream: nil,
            inputStream: nil
        )
        guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
            return
        }
        connections[index].payloads.insert(payload, at: 0)
    }

    public func connectionManager(_ connectionManager: ConnectionManager, didReceiveTransferUpdate update: TransferUpdate, from endpointID: EndpointID, forPayload payloadID: PayloadID) {
        guard let connectionIndex = connections.firstIndex(where: { $0.endpointID == endpointID }),
              let payloadIndex = connections[connectionIndex].payloads.firstIndex(where: { $0.id == payloadID }) else {
            return
        }
        var payload = connections[connectionIndex].payloads[payloadIndex]
#if DEBUG
        print("connectionManager didReceiveTransferUpdate payloadID: \(payloadID), endpointID: \(endpointID), type: \(payload.type) update: \(update)")
#endif
        switch update {
        case .success:
            payload.status = .success
            if (payload.type == .file) {
#if DEBUG
                print("connectionManager didReceiveTransferUpdate payloadID: \(payloadID), endpointID: \(endpointID), localURL: \(payload.localURL!.absoluteString), fileName: \(payload.fileName!)")
#endif
                transmissionEventDelegate?.onFileTransferComplete(endpointId: endpointID, id: payloadID, localUrl: payload.localURL!.absoluteString, fileName: payload.fileName!)
            } else if (payload.type == .stream) {
                transmissionEventDelegate?.onStreamTransferComplete(endpointId: endpointID, id: payloadID)
            }
        case .canceled:
            payload.status = .canceled
            if (payload.type == .file) {
                transmissionEventDelegate?.onFileTransferCancelled(endpointId: endpointID, id: payloadID)
            } else if (payload.type == .stream) {
                transmissionEventDelegate?.onStreamTransferCancelled(endpointId: endpointID, id: payloadID)
            }
        case .failure:
            payload.status = .failure
            if (payload.type == .file) {
                transmissionEventDelegate?.onFileTransferFailed(endpointId: endpointID, id: payloadID)
            } else if (payload.type == .stream) {
                transmissionEventDelegate?.onStreamTransferFailed(endpointId: endpointID, id: payloadID)
            }
        case let .progress(progress):
            payload.status = .inProgress(progress)
            if (payload.type == .file) {
                if (progress.totalUnitCount == 0) {
                    // finished on this update
                    payload.status = .success
                    transmissionEventDelegate?.onFileTransferComplete(endpointId: endpointID, id: payloadID, localUrl: payload.localURL!.absoluteString, fileName: payload.fileName!)
                } else {
                    transmissionEventDelegate?.onFileTransferUpdate(endpointId: endpointID, id: payloadID, bytesTransferred: progress.completedUnitCount, totalSize: progress.totalUnitCount)
                }
            } else if (payload.type == .stream) {
                if (payload.isIncoming) {
                    readStreamAndCallback(endpointID: endpointID, payload: payload)
                }
            }
        }
    }

    public func connectionManager(_ connectionManager: ConnectionManager, didChangeTo state: ConnectionState, for endpointID: EndpointID) {
#if DEBUG
        print("connectionManager didChangeTo state: \(state), endpointID: \(endpointID)")
#endif
        switch (state) {
        case .connecting:
            break
        case .connected:
            guard let index = requests.firstIndex(where: { $0.endpointID == endpointID }) else {
                return
            }
            let request = requests.remove(at: index)
            let connection = ConnectedEndpoint(
                id: request.id,
                endpointID: endpointID,
                endpointName: request.endpointName
            )
            connections.insert(connection, at: 0)
            
            // Notify to Unity
            connectionEventDelegate?.onEndpointConnected(endpointId: endpointID)
        case .disconnected:
            guard let index = connections.firstIndex(where: { $0.endpointID == endpointID }) else {
#if DEBUG
                print("connectionManager .disconnected endpointID: \(endpointID) not found")
                connections.forEach {
                    print("connectionManager .disconnected connections' endpointID: \($0.endpointID)")
                }
#endif
                return
            }
#if DEBUG
            // connectionManager .disconnected endpointID: PWQ2 found. index: 0
            print("connectionManager .disconnected endpointID: \(endpointID) found. index: \(index)")
#endif
            connections.remove(at: index)
            
            // Notify to Unity
            connectionEventDelegate?.onEndpointDisconnected(endpointId: endpointID)
        case .rejected:
            guard let index = requests.firstIndex(where: { $0.endpointID == endpointID }) else {
                return
            }
            requests.remove(at: index)
            
            // Notify to Unity
            connectionEventDelegate?.onConnectionFailed(endpointId: endpointID)
        }
    }
}
