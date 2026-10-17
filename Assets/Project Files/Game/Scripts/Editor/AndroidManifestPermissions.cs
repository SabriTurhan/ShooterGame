#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Watermelon.SquadShooter
{
    // The haptic module vibrates through the Android Vibrator API, which needs the VIBRATE permission;
    // Unity only adds it on its own when Handheld.Vibrate is used, so it is added to the generated manifest here.
    public class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
    {
        private const string ANDROID_NS = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            XmlDocument manifest = new XmlDocument();
            manifest.Load(manifestPath);
            XmlElement root = manifest.DocumentElement;

            foreach (XmlNode node in root.SelectNodes("uses-permission"))
                if (node.Attributes?["android:name"]?.Value == "android.permission.VIBRATE") return;

            XmlElement permission = manifest.CreateElement("uses-permission");
            permission.SetAttribute("name", ANDROID_NS, "android.permission.VIBRATE");
            root.PrependChild(permission);
            manifest.Save(manifestPath);
        }
    }
}
#endif
