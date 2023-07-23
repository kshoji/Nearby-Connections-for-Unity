//
//  NearbyUnityPlugin.m
//  NearbyUnityPlugin
//
//  Created by Kaoru Shoji on 2023/07/17.
//

#import <Foundation/Foundation.h>
#if TARGET_OS_IOS
#import "NearbyUnityPlugin-Swift.h"
#else
#import "NearbyUnityPlugin_osx-Swift.h"
#endif
#import "NearbyUnityPlugin-Bridging-Header.h"

@interface NearbyConnectionsPlugin : NSObject<AdvertisingEventDelegate, ConnectionEventDelegate, DiscoveryEventDelegate, TransmissionEventDelegate>

@end

@implementation NearbyConnectionsPlugin

static NearbyConnectionsPlugin* instance;

// delegate methods
typedef void ( __cdecl *OnAdvertisingFailedDelegate )();
OnAdvertisingFailedDelegate advertisingFailedCallback;
- (void)onAdvertisingFailed {
#ifdef DEBUG
    NSLog(@"onAdvertisingFailed called");
#endif
    if (advertisingFailedCallback) {
        advertisingFailedCallback();
    }
}

typedef void ( __cdecl *OnAdvertisingStartedDelegate )();
OnAdvertisingStartedDelegate advertisingStartedCallback;
- (void)onAdvertisingStarted {
#ifdef DEBUG
    NSLog(@"onAdvertisingStarted called");
#endif
    if (advertisingStartedCallback) {
        advertisingStartedCallback();
    }
}

typedef void ( __cdecl *OnConnectionFailedDelegate )( const char* );
OnConnectionFailedDelegate connectionFailedCallback;
- (void)onConnectionFailedWithEndpointId:(NSString * _Nonnull)endpointId {
#ifdef DEBUG
    NSLog(@"onConnectionFailedWithEndpointId called");
#endif
    if (connectionFailedCallback) {
        connectionFailedCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding]);
    }
}

typedef void ( __cdecl *OnConnectionInitiatedDelegate )( const char*, const char*, bool );
OnConnectionInitiatedDelegate connectionInitiatedCallback;
- (void)onConnectionInitiatedWithEndpointId:(NSString * _Nonnull)endpointId endpointName:(NSString * _Nonnull)endpointName isIncomingConnection:(BOOL)isIncomingConnection {
#ifdef DEBUG
    NSLog(@"onConnectionInitiatedWithEndpointId called");
#endif
    if (connectionInitiatedCallback) {
        connectionInitiatedCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding], [endpointName cStringUsingEncoding:NSUTF8StringEncoding], isIncomingConnection == YES);
    }
}

typedef void ( __cdecl *OnEndpointConnectedDelegate )( const char* );
OnEndpointConnectedDelegate endpointConnectedCallback;
- (void)onEndpointConnectedWithEndpointId:(NSString * _Nonnull)endpointId {
#ifdef DEBUG
    NSLog(@"onEndpointConnectedWithEndpointId called");
#endif
    if (endpointConnectedCallback) {
        endpointConnectedCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding]);
    }
}

typedef void ( __cdecl *OnEndpointDisconnectedDelegate )( const char* );
OnEndpointDisconnectedDelegate endpointDisconnectedCallback;
- (void)onEndpointDisconnectedWithEndpointId:(NSString * _Nonnull)endpointId {
#ifdef DEBUG
    NSLog(@"onEndpointDisconnectedWithEndpointId called");
#endif
    if (endpointDisconnectedCallback) {
        endpointDisconnectedCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding]);
    }
}

typedef void ( __cdecl *OnDiscoveryFailedDelegate )();
OnDiscoveryFailedDelegate discoveryFailedCallback;
- (void)onDiscoveryFailed {
#ifdef DEBUG
    NSLog(@"onDiscoveryFailed called");
#endif
    if (discoveryFailedCallback) {
        discoveryFailedCallback();
    }
}

typedef void ( __cdecl *OnDiscoveryStartedDelegate )();
OnDiscoveryStartedDelegate discoveryStartedCallback;
- (void)onDiscoveryStarted {
#ifdef DEBUG
    NSLog(@"onDiscoveryStarted called");
#endif
    if (discoveryStartedCallback) {
        discoveryStartedCallback();
    }
}

typedef void ( __cdecl *OnEndpointDiscoveredDelegate )( const char* );
OnEndpointDiscoveredDelegate endpointDisoveredCallback;
- (void)onEndpointDiscoveredWithEndpointId:(NSString * _Nonnull)endpointId {
#ifdef DEBUG
    NSLog(@"onEndpointDiscoveredWithEndpointId called. endpointId: %@, %s", endpointId, [endpointId cStringUsingEncoding:NSUTF8StringEncoding]);
#endif
    if (endpointDisoveredCallback) {
        endpointDisoveredCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding]);
    }
}

typedef void ( __cdecl *OnReceiveDelegate )( const char*, long, int, unsigned char* );
OnReceiveDelegate receiveCallback;
- (void)onReceiveWithEndpointId:(NSString * _Nonnull)endpointId id:(int64_t)payloadId payload:(NSArray<NSNumber *> * _Nonnull)payload {
    if (receiveCallback) {
        unsigned char* bytes = (unsigned char*)calloc(payload.count, sizeof(unsigned char));
        [payload enumerateObjectsUsingBlock:^(NSNumber* number, NSUInteger index, BOOL* stop){
            bytes[index] = number.unsignedCharValue;
        }];
#ifdef DEBUG
    NSLog(@"onReceiveWithEndpointId called, payload.count: %lu, payload: %s", payload.count, bytes);
#endif
        receiveCallback([endpointId cStringUsingEncoding:NSUTF8StringEncoding], payloadId, (int)payload.count, bytes);
    }
}

@end

#ifdef __cplusplus
extern "C" {
#endif
    void IosInitialize() {
        if (!instance) {
            instance = [[NearbyConnectionsPlugin alloc] init];
        }

        [NearbyUnityPlugin shared].advertisingEventDelegate = instance;
        [NearbyUnityPlugin shared].connectionEventDelegate = instance;
        [NearbyUnityPlugin shared].discoveryEventDelegate = instance;
        [NearbyUnityPlugin shared].transmissionEventDelegate = instance;
    }

    void IosStartAdvertising(const char *localEndpointName, const char *serviceId, int strategy) {
        [[NearbyUnityPlugin shared] startAdvertisingWithLocalEndpointName:[NSString stringWithUTF8String: localEndpointName] serviceId:[NSString stringWithUTF8String: serviceId] strategyInt:strategy];
    }
    
    void IosStopAdvertising() {
        [[NearbyUnityPlugin shared] stopAdvertising];
    }
    
    bool IosIsAdvertising() {
        return [[NearbyUnityPlugin shared] isAdvertising];
    }

    void IosStartDiscovering(const char *serviceId, int strategy) {
        [[NearbyUnityPlugin shared] startDiscoveringWithServiceId:[NSString stringWithUTF8String: serviceId] strategyInt:strategy];
    }

    void IosStopDiscovering() {
        [[NearbyUnityPlugin shared] stopDiscovering];
    }
    
    bool IosIsDiscovering() {
        return [[NearbyUnityPlugin shared] isDiscovering];
    }

    void IosConnectToEndpoint(const char *localEndpointName, const char *endpointId) {
        [[NearbyUnityPlugin shared] requestConnectionTo:[NSString stringWithUTF8String: endpointId] localEndpointName:[NSString stringWithUTF8String: localEndpointName]];
    }

    void IosAcceptConnection(const char *endpointId) {
        [[NearbyUnityPlugin shared] acceptConnectionTo:[NSString stringWithUTF8String: endpointId]];
    }

    void IosRejectConnection(const char *endpointId) {
        [[NearbyUnityPlugin shared] rejectConnectionTo:[NSString stringWithUTF8String: endpointId]];
    }

    void IosDisconnect(const char *endpointId) {
        [[NearbyUnityPlugin shared] disconnectFrom:[NSString stringWithUTF8String: endpointId]];
    }
    
    void IosDisconnectFromAllEndpoints() {
        [[NearbyUnityPlugin shared] disconnectFromAllEndpoints];
    }

    void IosStopAllEndpoints() {
        [[NearbyUnityPlugin shared] stopAllEndpoints];
    }
    
    void IosSend(const unsigned char* data, int length) {
        [[NearbyUnityPlugin shared] sendWithPayload:[NSData dataWithBytes:data length:length]];
    }

    void IosSendToEndpoint(const unsigned char* data, int length, const char *endpointId) {
        [[NearbyUnityPlugin shared] sendWithEndpointID:[NSString stringWithUTF8String: endpointId] payload:[NSData dataWithBytes:data length:length]];
    }

    // delegate methods
    void SetAdvertisingFailedDelegate(OnAdvertisingFailedDelegate callback) {
        advertisingFailedCallback = callback;
    }

    void SetAdvertisingStartedDelegate(OnAdvertisingStartedDelegate callback) {
        advertisingStartedCallback = callback;
    }

    void SetDiscoveryFailedDelegate(OnDiscoveryFailedDelegate callback) {
        discoveryFailedCallback = callback;
    }

    void SetDiscoveryStartedDelegate(OnDiscoveryStartedDelegate callback) {
        discoveryStartedCallback = callback;
    }

    void SetConnectionInitiatedDelegate(OnConnectionInitiatedDelegate callback) {
        connectionInitiatedCallback = callback;
    }

    void SetConnectionFailedDelegate(OnConnectionFailedDelegate callback) {
        connectionFailedCallback = callback;
    }

    void SetEndpointDiscoveredDelegate(OnEndpointDiscoveredDelegate callback) {
        endpointDisoveredCallback = callback;
    }

    void SetEndpointConnectedDelegate(OnEndpointConnectedDelegate callback) {
        endpointConnectedCallback = callback;
    }

    void SetEndpointDisconnectedDelegate(OnEndpointDisconnectedDelegate callback) {
        endpointDisconnectedCallback = callback;
    }

    void SetReceiveDelegate(OnReceiveDelegate callback) {
        receiveCallback = callback;
    }

#ifdef __cplusplus
}
#endif
