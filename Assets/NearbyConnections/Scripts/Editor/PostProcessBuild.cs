#if UNITY_IOS
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

namespace jp.kshoji.unity.midi.Editor
{
#if UNITY_IOS
    public class PostProcessBuild
    {
        [PostProcessBuild(99)]
        private static void OnPostProcess(BuildTarget target, string pathToBuildProject)
        {
            if (target == BuildTarget.iOS)
            {
                var project = new PBXProject();
                var pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
                project.ReadFromFile(pbxProjectPath);
#if UNITY_2019_4_OR_NEWER
                project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreMIDI.framework", true);
                project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreAudioKit.framework", true);
#else
                project.AddFrameworkToProject(project.TargetGuidByName("Unity-iPhone"), "CoreMIDI.framework", true);
                project.AddFrameworkToProject(project.TargetGuidByName("Unity-iPhone"), "CoreAudioKit.framework", true);
#endif
                project.WriteToFile(pbxProjectPath);

                var infoPlist = new PlistDocument();
                var infoPlistPath = pathToBuildProject + "/Info.plist";
                infoPlist.ReadFromFile(infoPlistPath);
                if (infoPlist.root["NSBluetoothAlwaysUsageDescription"] == null)
                {
                    infoPlist.root.SetString("NSBluetoothAlwaysUsageDescription", "Uses for connecting BLE MIDI devices");
                    infoPlist.WriteToFile(infoPlistPath);
                }
            }
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

#if FEATURE_ANDROID_COMPANION_DEVICE
            // change main activity
            androidManifest.ChangeMainActivity("jp.kshoji.unity.midi.BleMidiUnityPlayerActivity");
#endif

            androidManifest.SetPermission("android.permission.BLUETOOTH");
            androidManifest.SetPermission("android.permission.BLUETOOTH_ADMIN");
#if FEATURE_ANDROID_COMPANION_DEVICE
            androidManifest.SetFeature("android.software.companion_device_setup", false);
#else
            androidManifest.SetPermission("android.permission.ACCESS_FINE_LOCATION");
#endif
            androidManifest.SetFeature("android.hardware.bluetooth_le", true);

            androidManifest.SetPermission("android.permission.BLUETOOTH_SCAN");
            androidManifest.SetPermission("android.permission.BLUETOOTH_CONNECT");
            androidManifest.SetPermission("android.permission.BLUETOOTH_ADVERTISE");

            androidManifest.SetFeature("android.hardware.usb.host", false);

            // NOTE: If you want to use the USB MIDI feature on Oculus(Meta) Quest 2, please UNCOMMENT below to detect USB MIDI device connections.
            // androidManifest.AddUsbIntentFilterForOculusDevices();

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

        internal void SetPermission(string permission)
        {
            if (HasPermission(permission))
            {
                return;
            }

            var child = CreateElement("uses-permission");
            child.Attributes.Append(CreateAndroidAttribute("name", permission));

            var manifest = SelectSingleNode("/manifest");
            manifest?.AppendChild(child);
        }

        private bool HasPermission(string permission)
        {
            return HasElement("uses-permission", permission);
        }

        internal void SetFeature(string feature, bool required)
        {
            if (HasFeature(feature))
            {
                return;
            }

            var child = CreateElement("uses-feature");
            child.Attributes.Append(CreateAndroidAttribute("name", feature));
            if (required)
            {
                child.Attributes.Append(CreateAndroidAttribute("required", "true"));
            }

            var manifest = SelectSingleNode("/manifest");
            manifest?.AppendChild(child);
        }

        private bool HasFeature(string feature)
        {
            return HasElement("uses-feature", feature);
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

        internal void AddUsbIntentFilterForOculusDevices()
        {
            var application = SelectSingleNode("/manifest/application");
            if (application == null)
            {
                return;
            }

            foreach (var childNode in application.ChildNodes)
            {
                if (((XmlNode)childNode).Name != "activity")
                {
                    continue;
                }

                var activity = (XmlNode)childNode;
                var isUnityActivity = false;
                foreach (XmlNode metaData in activity.ChildNodes)
                {
                    if (metaData == null || metaData.Name != "meta-data" || metaData.Attributes == null)
                    {
                        continue;
                    }

                    var name = metaData.Attributes["android:name"];
                    var value = metaData.Attributes["android:value"];
                    if (name != null && name.Value == "unityplayer.UnityActivity" &&
                        value != null && value.Value == "true")
                    {
                        isUnityActivity = true;
                        break;
                    }
                }

                if (isUnityActivity)
                {
                    var metaData = CreateElement("meta-data");
                    metaData.Attributes.Append(CreateAndroidAttribute("name", "android.hardware.usb.action.USB_DEVICE_ATTACHED"));
                    metaData.Attributes.Append(CreateAndroidAttribute("resource", "@xml/device_filter"));
                    if (!HasUsbDeviceAttachedMetaData(activity))
                    {
                        activity.AppendChild(metaData);
                    }

                    foreach (XmlNode intentFilter in activity.ChildNodes)
                    {
                        if (intentFilter == null || intentFilter.Name != "intent-filter")
                        {
                            continue;
                        }

                        var action = CreateElement("action");
                        action.Attributes.Append(CreateAndroidAttribute("name", "android.hardware.usb.action.USB_DEVICE_ATTACHED"));
                        if (!HasUsbDeviceAttachedIntentFilter(activity))
                        {
                            intentFilter.AppendChild(action);
                        }
                    }

                    break;
                }
            }
        }

        private bool HasUsbDeviceAttachedMetaData(XmlNode activity)
        {
            foreach (XmlNode metaData in activity.ChildNodes)
            {
                if (metaData == null || metaData.Name != "meta-data" || metaData.Attributes == null)
                {
                    continue;
                }
                
                var nameAttribute = metaData.Attributes["android:name"];
                var valueAttribute = metaData.Attributes["android:resource"];
                if (nameAttribute != null && nameAttribute.Value == "android.hardware.usb.action.USB_DEVICE_ATTACHED" &&
                    valueAttribute != null && valueAttribute.Value == "@xml/device_filter")
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUsbDeviceAttachedIntentFilter(XmlNode activity)
        {
            foreach (XmlNode intentFilter in activity.ChildNodes)
            {
                if (intentFilter == null || intentFilter.Name != "intent-filter" || intentFilter.Attributes == null)
                {
                    continue;
                }

                foreach (XmlNode action in intentFilter.ChildNodes)
                {
                    if (action == null || action.Name != "action" || action.Attributes == null)
                    {
                        continue;
                    }

                    var nameAttribute = action.Attributes["android:name"];
                    if (nameAttribute != null && nameAttribute.Value == "android.hardware.usb.action.USB_DEVICE_ATTACHED")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal void ChangeMainActivity(string activityClass)
        {
            var applications = DocumentElement.GetElementsByTagName("application");
            foreach (XmlElement application in applications)
            {
                var activities = application.GetElementsByTagName("activity");
                foreach (XmlElement activity in activities)
                {
                    var isUnityActivity = false;
                    var metas = application.GetElementsByTagName("meta-data");
                    foreach (XmlElement meta in metas)
                    {
                        if (meta.Attributes["android:name"]?.Value == "unityplayer.UnityActivity" &&
                            meta.Attributes["android:value"]?.Value.ToLower() == "true")
                        {
                            isUnityActivity = true;
                            break;
                        }
                    }

                    if (isUnityActivity)
                    {
                        activity.Attributes.Append(CreateAndroidAttribute("name", activityClass));
                        break;
                    }
                }
            }
        }
    }
#endif
}