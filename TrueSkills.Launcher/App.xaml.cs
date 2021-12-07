using System.Net.NetworkInformation;
using System.Windows;
using TrueSkills.Launcher;

namespace TrueSkills
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsNetwork { get; set; }

        public App()
        {
            IsNetwork = NetworkInterface.GetIsNetworkAvailable();
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            IsNetwork = e.IsAvailable;
        }
    }
}
