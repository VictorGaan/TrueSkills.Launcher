using Microsoft.Web.WebView2.Core;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Threading;
using System.Windows;
using TrueSkills.Launcher.Properties;

namespace TrueSkills
{
    public class MainWindowVM : ReactiveObject
    {
        const string SUPPORT_SITE = "https://help.trueskills.ru";
        const string DOWNLOAD_APP = "https://codeload.github.com/VictorGaan/Build/zip/refs/heads/master";
        const string DOWNLOAD_APP_VERSION = "https://raw.githubusercontent.com/VictorGaan/Build/master/Version.txt";
        const string WEBVIEW2_RUNTIME = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        string APP_DIRECTORY = Path.GetTempPath() + "TrueSkillsApp";
        public event EventHandler LanguageChanged;
        public ReactiveCommand<Unit, Unit> SupportCommand { get; }
        public ReactiveCommand<Unit, Unit> MakeEventCommand { get; }
        private ResourceDictionary _currentResource;
        private string _content;
        private string _version;
        private ObservableCollection<CultureInfo> _languages;
        private Status _status;
        private bool _isEnabledButton;
        private string _downloadingProcess;
        private Visibility _progressBarVisible;

        public string DownloadingProcess
        {
            get => _downloadingProcess;
            set => this.RaiseAndSetIfChanged(ref _downloadingProcess, value);
        }

        public Visibility ProgressBarVisible
        {
            get => _progressBarVisible;
            set => this.RaiseAndSetIfChanged(ref _progressBarVisible, value);
        }

        public bool IsEnabledButton
        {
            get => _isEnabledButton;
            set => this.RaiseAndSetIfChanged(ref _isEnabledButton, value);
        }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public string Version
        {
            get => _version;
            set => this.RaiseAndSetIfChanged(ref _version, value);
        }

        public Status Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case Status.Ready:
                        Content = $"{_currentResource["lm_Ready"]}";
                        break;
                    case Status.Failed:
                        Content = $"{_currentResource["lm_Failed"]}";
                        break;
                    case Status.DownloadingApp:
                        Content = $"{_currentResource["lm_DownloadingApp"]}";
                        break;
                    case Status.DownloadingUpdate:
                        Content = $"{_currentResource["lm_DownloadingUpdate"]}";
                        break;
                }
            }
        }

        public ObservableCollection<CultureInfo> Languages
        {
            get
            {
                return _languages;
            }
        }
        public MainWindowVM()
        {
            _languages = new ObservableCollection<CultureInfo>();
            ProgressBarVisible = Visibility.Collapsed;
            IsEnabledButton = true;
            _version = Settings.Default.Version;
            if (Application.Current.Resources.MergedDictionaries[2] is ResourceDictionary resourceDictionary)
            {
                _currentResource = resourceDictionary;
            }


            LanguageChanged += App_LanguageChanged;
            GetLanguages();
            Language = Settings.Default.DefaultLanguage;
            SupportCommand = ReactiveCommand.Create(Support);
            MakeEventCommand = ReactiveCommand.Create(MakeApp);
            CheckUpdates();
        }

        private string GetVersionRuntime()
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                return null;
            }
        }

        private void MakeApp()
        {
            switch (Status)
            {
                case Status.Ready:
                    var path = Directory.GetDirectories(APP_DIRECTORY)[0];
                    var pathArgs = path + "\\Args.txt";
                    File.WriteAllText(pathArgs, $"{Language.Name}&{Environment.CurrentDirectory}");
                    var pathRuntime = APP_DIRECTORY + "\\MicrosoftEdgeWebview2Setup.exe";
                    Process process = null;
                    if (GetVersionRuntime() == null)
                    {
                        if (File.Exists(pathRuntime))
                        {
                            ProcessStartInfo startInfoRuntime = new ProcessStartInfo()
                            {
                                WorkingDirectory = APP_DIRECTORY,
                                FileName = "MicrosoftEdgeWebview2Setup.exe",
                                UseShellExecute = true
                            };
                            process = Process.Start(startInfoRuntime);
                        }
                    }
                    if (process != null)
                    {
                        process.WaitForExit();
                    }
                    ProcessStartInfo startInfo = new ProcessStartInfo()
                    {
                        WorkingDirectory = path,
                        FileName = "TrueSkills.exe",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    break;
                case Status.Failed:
                case Status.DownloadingApp:
                case Status.DownloadingUpdate:
                    InstallAppFiles();
                    break;
                default:
                    break;
            }
        }

        private int _counter = 0;
        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _counter++;
            if (_counter % 25 == 0)
            {
                IsEnabledButton = false;
                DownloadingProcess = ((e.BytesReceived / 1024f) / 1024f).ToString("#0.##") + $" Mb/s"; ;
            }
        }

        private void WebClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                var path = PathToZip();
                if (path != null)
                {
                    ProgressBarVisible = Visibility.Collapsed;
                    ZipFile.ExtractToDirectory(path, APP_DIRECTORY, true);
                    File.Delete(path);
                    SaveVersion();
                    IsEnabledButton = true;
                    Status = Status.Ready;
                }
                else
                {
                    if (!File.Exists(path) && File.Exists(APP_DIRECTORY + "\\MicrosoftEdgeWebview2Setup.exe"))
                    {
                        Status = Status.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, _currentResource["a_Error"].ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                Status = Status.Failed;
            }
        }


        private void InstallAppFiles()
        {
            ProgressBarVisible = Visibility.Visible;
            Directory.CreateDirectory(APP_DIRECTORY);
            if (GetVersionRuntime() == null && !File.Exists(APP_DIRECTORY + "\\MicrosoftEdgeWebview2Setup.exe"))
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                    webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                    webClient.DownloadFileAsync(new Uri(WEBVIEW2_RUNTIME), APP_DIRECTORY + "\\MicrosoftEdgeWebview2Setup.exe");
                }
            }
            using (WebClient webClient = new WebClient())
            {
                webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                webClient.DownloadFileAsync(new Uri(DOWNLOAD_APP), APP_DIRECTORY + "\\Build.zip");
            }

        }

        private string _onlineVersion;
        private void CheckUpdates()
        {
            if (_onlineVersion == null)
            {
                WebClient webClient = new WebClient();
                _onlineVersion = webClient.DownloadString(DOWNLOAD_APP_VERSION);
            }

            if (!Directory.Exists(APP_DIRECTORY))
            {
                Directory.CreateDirectory(APP_DIRECTORY);
                Status = Status.DownloadingApp;
            }


            //Если новая версия
            if (IsNewApp(_onlineVersion))
            {
                var zip = PathToZip();
                if (Directory.Exists(APP_DIRECTORY))
                {
                    //Если есть файлы
                    if (IsEmpty())
                    {
                        if (zip == null)
                        {
                            Status = Status.DownloadingUpdate;
                        }
                        else
                        {
                            Status = Status.Failed;
                        }
                    }
                    else
                    {
                        if (zip!=null)
                        {
                            Status = Status.Failed;
                        }
                        else
                        {
                            Status = Status.DownloadingUpdate;
                        }
                    }
                }
            }
            //Если совпадают
            if (!IsNewApp(_onlineVersion))
            {
                var zip = PathToZip();
                if (Directory.Exists(APP_DIRECTORY))
                {
                    if (IsEmpty())
                    {
                        if (zip == null)
                        {
                            Status = Status.Ready;
                        }
                        else
                        {
                            Status = Status.Failed;
                        }
                    }
                    else
                    {
                        if (zip != null)
                        {
                            Status = Status.Failed;
                        }
                        else
                        {
                            Status = Status.DownloadingApp;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="version"></param>
        /// <returns>True, если версии не совпадают</returns>
        private bool IsNewApp(string version)
        {
            var anotherVersion = Settings.Default.Version;
            if (version != anotherVersion)
            {
                Version = version;
                return true;
            }
            return false;
        }

        private void GetLanguages()
        {
            var directory = Environment.CurrentDirectory + "\\Languages";
            if (!Directory.Exists(directory))
            {
                MessageBox.Show(_currentResource["a_Language1"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            foreach (var item in Directory.GetFiles(directory))
            {
                try
                {
                    var language = item.Replace(directory + "\\", "").Replace("Language.", "").Replace(".xaml", "");
                    _languages.Add(new CultureInfo(language));
                }
                catch
                {

                    MessageBox.Show(_currentResource["a_Language2"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public CultureInfo Language
        {
            get
            {
                return Thread.CurrentThread.CurrentUICulture;
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (value == Thread.CurrentThread.CurrentUICulture) return;

                Thread.CurrentThread.CurrentUICulture = value;

                ResourceDictionary dict = new ResourceDictionary();
                dict.Source = new Uri(string.Format("/Languages/Language.{0}.xaml", value.Name), UriKind.RelativeOrAbsolute);
                _currentResource = dict;
                ResourceDictionary oldDict = (from d in Application.Current.Resources.MergedDictionaries
                                              where d.Source != null && d.Source.OriginalString == $"/Languages/Language.{Settings.Default.DefaultLanguage}.xaml"
                                              select d).FirstOrDefault();
                if (oldDict != null)
                {
                    int ind = Application.Current.Resources.MergedDictionaries.IndexOf(oldDict);
                    Application.Current.Resources.MergedDictionaries.Remove(oldDict);
                    Application.Current.Resources.MergedDictionaries.Insert(ind, dict);
                }
                else
                {
                    Application.Current.Resources.MergedDictionaries.Add(dict);
                }
                LanguageChanged(Application.Current, new EventArgs());
            }
        }


        private void App_LanguageChanged(object sender, EventArgs e)
        {
            CheckUpdates();
            Settings.Default.DefaultLanguage = Language;
            Settings.Default.Save();
        }

        private void Support()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = SUPPORT_SITE,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private bool IsEmpty()
        {
            return PathToZip() != null || Directory.GetFiles(APP_DIRECTORY).Any() || Directory.GetDirectories(APP_DIRECTORY).Any();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Возращает строку, когда файл, есть и его можно считать</returns>
        private string PathToZip()
        {
            foreach (var item in Directory.GetFiles(APP_DIRECTORY))
            {
                try
                {
                    using (var zipFile = ZipFile.OpenRead(item))
                    {
                        var entries = zipFile.Entries;
                        return item;
                    }
                }
                catch
                {
                    return item;
                }
            }
            return null;
        }
        private void SaveVersion()
        {
            Settings.Default.Version = Version;
            Settings.Default.Save();
        }
    }
}
