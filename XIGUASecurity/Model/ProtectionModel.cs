using Compatibility.Windows.Storage;
namespace XIGUASecurity.Model
{
    public class ProtectionModel
    {
        public bool IsProtected => ProtectionManager.IsOpen();
        public string LastScanTime
        {
            get => ApplicationData.Current.LocalSettings.Values["LastScanTime"] as string ?? "";
            set => ApplicationData.Current.LocalSettings.Values["LastScanTime"] = value;
        }
        public int ThreatCount
        {
            get => (int)(ApplicationData.Current.LocalSettings.Values["ThreatCount"] ?? 0);
            set => ApplicationData.Current.LocalSettings.Values["ThreatCount"] = value;
        }
    }
}