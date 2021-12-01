using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TrueSkills.Launcher;
using TrueSkills.Launcher.Properties;

namespace TrueSkills
{
    public class MainWindowVM : ReactiveObject
    {
        const string SUPPORT_SITE = "https://help.trueskills.ru";
        string DOWNLOAD_APP = "http://api.trueskills.devit.pw/api-v1/app?v=" + Settings.Default.Version;

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
                    case Status.DownloadingApp:
                        Content = $"{_currentResource["lm_DownloadingApp"]}";
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

        private void MakeApp()
        {
            switch (Status)
            {
                case Status.Ready:
                    var path = Directory.GetDirectories(APP_DIRECTORY)[0];
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = path,
                            UseShellExecute = false,
                            FileName = "dotnet",
                            Arguments = $"TrueSkills.dll {Language.Name}",
                            CreateNoWindow = true
                        };
                        Process.Start(startInfo);
                    }
                    catch
                    {
                        return;
                    }
                    break;
                case Status.DownloadingApp:
                    Text version = null;
                    bool completed = ExecuteWithTimeLimit(TimeSpan.FromMilliseconds(2000), () =>
                    {
                        version = CheckVersion(DOWNLOAD_APP);
                    });
                    if (version == null)
                    {
                        InstallAppFiles();
                    }
                    break;
                default:
                    break;
            }
        }

        private int _counter = 0;
        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _counter++;
            if (IsEnabledButton)
            {
                IsEnabledButton = false;
            }
            if (_counter % 25 == 0)
            {
                DownloadingProcess = ((e.BytesReceived / 1024f) / 1024f).ToString("#0.##") + "/" + ((e.TotalBytesToReceive / 1024f) / 1024f).ToString("#0.##") + $"\nMb";
            }
        }

        private bool IsValidZip(string path)
        {
            try
            {
                using (var zipFile = ZipFile.OpenRead(path))
                {
                    var entries = zipFile.Entries;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private string[] GetProperties()
        {
            return new string[] { _currentResource["mb_Yes"].ToString(), _currentResource["mb_No"].ToString(), _currentResource["mb_Ok"].ToString() };
        }

        private void WebClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                string path = PathToZip();
                string pathVersion = string.Empty;

                if (IsValidZip(path))
                {
                    ProgressBarVisible = Visibility.Collapsed;
                    ZipFile.ExtractToDirectory(path, APP_DIRECTORY, true);
                    File.Delete(path);
                    pathVersion = $"{Directory.GetDirectories(APP_DIRECTORY)[0]}\\Version.txt";
                    if (!File.Exists(pathVersion))
                    {
                        SetNewApp("1.0.0.0");
                    }
                    else
                    {
                        SetNewApp(File.ReadAllText(pathVersion));
                    }
                    SaveVersion();
                    IsEnabledButton = true;
                    Status = Status.Ready;
                }
                else
                {
                    new MessageBoxWindow(_currentResource["lm_ErrorZip"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                    Status = Status.DownloadingApp;
                }
            }
            catch (Exception ex)
            {
                new MessageBoxWindow(ex.Message, _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                Status = Status.DownloadingApp;
            }
        }


        private void InstallAppFiles()
        {
            ProgressBarVisible = Visibility.Visible;
            Directory.CreateDirectory(APP_DIRECTORY);
            using (WebClient webClient = new WebClient())
            {
                webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                webClient.DownloadFileAsync(new Uri(DOWNLOAD_APP), APP_DIRECTORY + "\\Build.zip");
            }
        }

        private void CheckUpdates()
        {
            Text version = null;
            bool completed = ExecuteWithTimeLimit(TimeSpan.FromMilliseconds(2000), () =>
            {
                version = CheckVersion(DOWNLOAD_APP);
            });

            if (version != null)
            {
                if (Directory.Exists(APP_DIRECTORY))
                {
                    var zip = PathToZip();
                    if (IsEmpty())
                    {
                        if (zip == null)
                        {
                            Status = Status.Ready;
                        }
                        else
                        {
                            Status = Status.DownloadingApp;
                        }
                    }
                    else
                    {
                        Status = Status.DownloadingApp;
                    }
                }
                else
                {
                    Status = Status.DownloadingApp;
                }
            }
            else
            {
                Status = Status.DownloadingApp;
                ClearDirectory();
            }

        }

        private void SetNewApp(string version)
        {
            var anotherVersion = Settings.Default.Version;
            if (version != anotherVersion)
            {
                Version = version;
            }
        }

        private void GetLanguages()
        {
            _languages.Add(new CultureInfo("ru-RU"));
            _languages.Add(new CultureInfo("en-US"));
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

        private void ClearDirectory()
        {
            if (Directory.Exists(APP_DIRECTORY))
            {
                Directory.Delete(APP_DIRECTORY, true);
            }
        }

        private string PathToZip()
        {
            string result = null;
            foreach (var item in Directory.GetFiles(APP_DIRECTORY))
            {
                if (item.Contains(".zip") || item.Contains(".7z") || item.Contains(".rar"))
                {
                    result = item;
                }
                else
                {
                    result = null;
                }
            }
            return result;
        }

        private void SaveVersion()
        {
            Settings.Default.Version = Version;
            Settings.Default.Save();
        }

        private Text CheckVersion(string url)
        {
            using (WebClient client = new WebClient())
            {
                var response = client.DownloadString(DOWNLOAD_APP);
                if (response.Contains("status"))
                {
                    return JsonConvert.DeserializeObject<Text>(response);
                }
                else
                {
                    client.Dispose();
                    return null;
                }
            }
        }
        public class Text
        {
            [JsonProperty("status")]
            public string Content { get; set; }
        }

        public static bool ExecuteWithTimeLimit(TimeSpan timeSpan, Action codeBlock)
        {
            try
            {
                Task task = Task.Factory.StartNew(() => codeBlock());
                task.Wait(timeSpan);
                return task.IsCompleted;
            }
            catch (AggregateException ae)
            {
                throw ae.InnerExceptions[0];
            }
        }
    }
}
