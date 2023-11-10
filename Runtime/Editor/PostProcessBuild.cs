#if UNITY_IOS
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif
#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;
#endif

namespace jp.kshoji.unity.nearby.Editor
{
#if UNITY_IOS
    public class PostProcessBuild
    {
        [PostProcessBuild(99)]
        private static void OnPostProcess(BuildTarget target, string pathToBuildProject)
        {
            if (target == BuildTarget.iOS)
            {
                var infoPlist = new PlistDocument();
                var infoPlistPath = pathToBuildProject + "/Info.plist";
                infoPlist.ReadFromFile(infoPlistPath);
                var infoPlistModified = false;
                if (infoPlist.root["NSBluetoothAlwaysUsageDescription"] == null)
                {
                    infoPlist.root.SetString("NSBluetoothAlwaysUsageDescription", "Uses Bluetooth for finding and connecting nearby devices");
                    infoPlistModified = true;
                }
                if (infoPlist.root["NSLocalNetworkUsageDescription"] == null)
                {
                    infoPlist.root.SetString("NSLocalNetworkUsageDescription", "Uses networks for finding and connecting nearby devices");
                    infoPlistModified = true;
                }

                PlistElementArray bonjourServices;
                if (infoPlist.root["NSBonjourServices"] == null)
                {
                    bonjourServices = infoPlist.root.CreateArray("NSBonjourServices");
                }
                else
                {
                    bonjourServices = infoPlist.root["NSBonjourServices"].AsArray();
                }

                // Apply the all using serviceIds. This serviceId is used at NearbySampleScene class.
                // Read `Resources/NearbyConnections-ios-serviceIds.json` file.
                var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("NearbyConnections-ios-serviceIds");
                if (asset != null)
                {
                    // Read JSON file
                    var serviceIdJson = UnityEngine.JsonUtility.FromJson<ServiceId>(asset.text);
                    foreach (var serviceId in serviceIdJson.serviceIds)
                    {
                        UnityEngine.Debug.Log($"serviceId: {serviceId}");
                        bonjourServices.AddString(ComputeBonjourService(serviceId));
                        infoPlistModified = true;
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("`NearbyConnections-ios-serviceIds.json` file not found! Please copy it from `Samples/SampleProject/Resources/NearbyConnections-ios-serviceIds.json` to `Assets/Resources` directory.");
                }

                if (infoPlistModified)
                {
                    infoPlist.WriteToFile(infoPlistPath);
                }
            }
        }

        private static string ComputeBonjourService(string nearbyServiceId)
        {
            var hash = new SHA256CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(nearbyServiceId));
            var sb = new StringBuilder();
            // uses first 12 chars
            for (var i = 0; i < 6; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return $"_{sb}._tcp";
        }
    }

    [System.Serializable]
    public class ServiceId {
        public string[] serviceIds;
    }
#endif

#if UNITY_ANDROID
    public class ModifyGradleConfigurations : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 98;
        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            ModifyBuildGradle(basePath);
            ModifyGradleProperties(basePath);
        }

        private static void ModifyBuildGradle(string path)
        {
            var unityLibraryBuildGradlePath = Path.Combine(path, "build.gradle");
            var unityLibraryBuildGradleLines = File.ReadLines(unityLibraryBuildGradlePath);
            var updatedUnityLibraryBuildGradleText = new StringBuilder();
            var nextLineIsDependenciesFirstLine = false;
            foreach (var gradle in unityLibraryBuildGradleLines)
            {
                if (nextLineIsDependenciesFirstLine)
                {
                    // Add the original line
                    updatedUnityLibraryBuildGradleText.Append(gradle);
                    updatedUnityLibraryBuildGradleText.AppendLine();
                    // Add the dependency
                    updatedUnityLibraryBuildGradleText.Append("    implementation 'com.google.android.gms:play-services-nearby:18.7.0'");
                    updatedUnityLibraryBuildGradleText.AppendLine();
                    // Add the dependency
                    updatedUnityLibraryBuildGradleText.Append("    implementation 'androidx.appcompat:appcompat:1.6.1'");
                    updatedUnityLibraryBuildGradleText.AppendLine();

                    nextLineIsDependenciesFirstLine = false;
                }
                else if (gradle.Contains("dependencies"))
                {
                    // Add the original line
                    updatedUnityLibraryBuildGradleText.Append(gradle);
                    updatedUnityLibraryBuildGradleText.AppendLine();
                    nextLineIsDependenciesFirstLine = true;
                }
                else
                {
                    if (gradle.Contains("com.google.android.gms:play-services-nearby"))
                    {
                        // NOTE: ignore the original configuration, use version 18.7.0
                        continue;
                    }

                    if (gradle.Contains("androidx.appcompat:appcompat"))
                    {
                        // NOTE: ignore the original configuration, use version 1.6.1
                        continue;
                    }

                    // Add the original line
                    updatedUnityLibraryBuildGradleText.Append(gradle);
                    updatedUnityLibraryBuildGradleText.AppendLine();
                }
            }

            File.WriteAllText(unityLibraryBuildGradlePath, updatedUnityLibraryBuildGradleText.ToString());
        }

        private static void ModifyGradleProperties(string path)
        {
            var rootProjectPath = path.TrimEnd('/').Replace("unityLibrary", "");
            var gradlePropertiesPath = Path.Combine(rootProjectPath, "gradle.properties");
            var gradleProperties = File.ReadLines(gradlePropertiesPath);
            var updatedGradlePropertiesText = new StringBuilder();
            var useAndroidXAppended = false;
            foreach (var property in gradleProperties)
            {
                if (property.Contains("android.useAndroidX"))
                {
                    // Replace property
                    updatedGradlePropertiesText.Append("android.useAndroidX=true");
                    updatedGradlePropertiesText.AppendLine();
                    useAndroidXAppended = true;
                }
                else
                {
                    // Add the original line
                    updatedGradlePropertiesText.Append(property);
                    updatedGradlePropertiesText.AppendLine();
                }
            }

            if (!useAndroidXAppended)
            {
                // Add the property
                updatedGradlePropertiesText.Append("android.useAndroidX=true");
                updatedGradlePropertiesText.AppendLine();
            }

            File.WriteAllText(gradlePropertiesPath, updatedGradlePropertiesText.ToString());
        }
    }

    public class ModifyAndroidManifest : IPostGenerateGradleAndroidProject
    {
        private string _manifestFilePath;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            var androidManifest = new AndroidManifest(GetManifestPath(basePath));

            androidManifest.SetPermission("android.permission.BLUETOOTH", 30);
            androidManifest.SetPermission("android.permission.BLUETOOTH_ADMIN", 30);
            androidManifest.SetPermission("android.permission.ACCESS_COARSE_LOCATION");
            androidManifest.SetPermission("android.permission.ACCESS_FINE_LOCATION");
            androidManifest.SetPermission("android.permission.BLUETOOTH_SCAN", null, "neverForLocation");
            androidManifest.SetPermission("android.permission.BLUETOOTH_CONNECT");
            androidManifest.SetPermission("android.permission.BLUETOOTH_ADVERTISE");

            androidManifest.SetPermission("android.permission.ACCESS_WIFI_STATE");
            androidManifest.SetPermission("android.permission.CHANGE_WIFI_STATE");
            androidManifest.SetPermission("android.permission.NEARBY_WIFI_DEVICES");

            androidManifest.SetPermission("android.permission.NFC");

            androidManifest.Save();
        }

        public int callbackOrder => 99;

        private string GetManifestPath(string basePath)
        {
            if (string.IsNullOrEmpty(_manifestFilePath))
            {
                var pathBuilder = new StringBuilder(basePath);
                pathBuilder.Append(Path.DirectorySeparatorChar).Append("src");
                pathBuilder.Append(Path.DirectorySeparatorChar).Append("main");
                pathBuilder.Append(Path.DirectorySeparatorChar).Append("AndroidManifest.xml");
                _manifestFilePath = pathBuilder.ToString();
            }

            return _manifestFilePath;
        }
    }

    internal class AndroidManifest : XmlDocument
    {
        private readonly string _androidXmlNamespace = "http://schemas.android.com/apk/res/android";
        private readonly string _path;

        protected internal AndroidManifest(string path)
        {
            _path = path;
            using (var reader = new XmlTextReader(_path))
            {
                Load(reader);
            }

            var namespaceManager = new XmlNamespaceManager(NameTable);
            namespaceManager.AddNamespace("android", _androidXmlNamespace);
        }

        public void Save()
        {
            using (var writer = new XmlTextWriter(_path, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                Save(writer);
            }
        }

        private XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            var attr = CreateAttribute("android", key, _androidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        internal void SetPermission(string permission, int? maxSdkVersion = null, string usesPermissionFlags = null)
        {
            if (HasPermission(permission))
            {
                return;
            }

            var child = CreateElement("uses-permission");
            child.Attributes.Append(CreateAndroidAttribute("name", permission));
            if (maxSdkVersion.HasValue)
            {
                child.Attributes.Append(CreateAndroidAttribute("maxSdkVersion", maxSdkVersion.Value.ToString()));
            }
            if (usesPermissionFlags != null)
            {
                child.Attributes.Append(CreateAndroidAttribute("usesPermissionFlags", usesPermissionFlags));
            }

            var manifest = SelectSingleNode("/manifest");
            manifest?.AppendChild(child);
        }

        private bool HasPermission(string permission)
        {
            return HasElement("uses-permission", permission);
        }

        private bool HasElement(string elementName, string nameValue)
        {
            var manifest = SelectSingleNode("/manifest");
            if (manifest == null)
            {
                return false;
            }

            var nameAttribute = CreateAttribute("android", "name", _androidXmlNamespace);
            foreach (var childNode in manifest.ChildNodes)
            {
                if (((XmlNode)childNode).Name != elementName)
                {
                    continue;
                }

                var attributes = ((XmlNode)childNode).Attributes;
                if (attributes?[nameAttribute.Name] != null && attributes[nameAttribute.Name].Value == nameValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
#endif
}