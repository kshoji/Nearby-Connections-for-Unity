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

                // TODO: apply the all using serviceIds. This serviceId is used at NearbySampleScene class.
                const string serviceId = "a7b90efd-f739-4a0a-842e-fba4f42ffb2e";
                bonjourServices.AddString(ComputeBonjourService(serviceId));

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
            return $"_${sb}._tcp";
        }
    }
#endif

#if UNITY_ANDROID
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