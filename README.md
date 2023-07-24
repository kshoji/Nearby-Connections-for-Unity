# Nearby Connections for Unity
[Nearby Connections](https://developers.google.com/nearby/connections/overview) Unity Plugin implementation

This package enables to find nearby devices, connect them, and communicate with each others without the mediating server.

### Supporting platforms:
- iOS
- Android
- macOS Standalone
- Unity Editor on macOS

(The Nearby seems to be also worked on Windows, but I couldn't build the binary.)

# Install the package on the Unity Package Manager
Push the `+` button on the Unity's Package Manager view, and select `Add package from git URL…` menu.  
Then specify the URL below:

```text
ssh://git@github.com/kshoji/Nearby-Connections-for-Unity.git
```

### Note about errors under Windows environment:
On the Windows environment, Unity's package manager may show errors like these.
```bat
Filename too long
```
This repository contains plugin's source code on [PluginSource~](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/main/PluginSource~) directory, this directory has too long pathname.  
To fix this issue, execute this command on the terminal.
```bat
git config --system core.longpaths true
```

# The main class of this package:
The `NearbyConnectionsManager` class is a wrapper class for Nearby API.  
To look the implementation of the feature, please see the [Sample Project](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/main/Samples~/SampleProject) of this package.

# License
[Apache License 2.0](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/main/LICENSE)

# Changelog
- The link to [Changelog](https://github.com/kshoji/Nearby-Connections-for-Unity/tree/main/CHANGELOG.md).
- See also repository's [Releases](https://github.com/kshoji/Nearby-Connections-for-Unity/releases) page.
