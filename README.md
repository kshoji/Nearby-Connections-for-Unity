# Nearby Connections for Unity's native plugin source

### How to build plugin:
- iOS/macOS Standalone
    - Open [PluginSource/iOS/NearbyUnityPlugin/NearbyUnityPlugin.xcodeproj](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/plugin-source/PluginSource/iOS/NearbyUnityPlugin/NearbyUnityPlugin.xcodeproj) with Xcode, and build the `NearbyUnityPlugin` or `NearbyUnityPlugin-osx` target with `Release` configuration.
- Android
    - Open [PluginSoucce/Android/NearbyConnections](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/plugin-source/PluginSource/Android/NearbyConnections) directory with Android Studio, and select `Make Project` menu with `release` variant.

### Note about errors under Windows environment:
On the Windows environment, git may show errors like these.
```bat
Filename too long
```
The plugin's directory has too long pathname in the nearby submodule.  
To fix this issue, execute this command on the terminal.
```bat
git config --system core.longpaths true
```
