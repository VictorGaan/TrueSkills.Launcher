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
        #region Consts
        const string SUPPORT_SITE = "https://help.trueskills.ru";
        string DOWNLOAD_URL = "http://api.trueskills.devit.pw/api-v1/app?v=" + Settings.Default.Version;
        string APP_DIRECTORY = Path.GetTempPath() + "TrueSkillsApp";
        string APP_FOLDER = Path.GetTempPath() + "TrueSkillsApp\\" + "netcoreapp3.1";
        string ZIP_FOLDER = Path.GetTempPath() + "TrueSkillsApp\\" + "Build.zip";
        string VERSION_FILE = Path.GetTempPath() + "TrueSkillsApp\\" + "Build\\" + "Version.txt";
        #endregion

        #region Variables
        private ResourceDictionary _currentResource;
        private string _content;
        private string _version;
        private ObservableCollection<CultureInfo> _languages;
        private Status _status;
        private bool _isEnabledButton;
        private string _downloadingProcess;
        private Visibility _progressBarVisible;
        #endregion

        #region Properties
        public event EventHandler LanguageChanged;
        public ReactiveCommand<Unit, Unit> SupportCommand { get; }
        public ReactiveCommand<Unit, Unit> MakeEventCommand { get; }


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
        #endregion

        public void GetVersion()
        {
            if (!Directory.Exists(APP_DIRECTORY))
            {
                Version = _currentResource["lm_VersionNoExists"].ToString();
                SetVersion();
            }
            if (Directory.Exists(APP_DIRECTORY) && !Directory.Exists(APP_FOLDER))
            {
                Version = _currentResource["lm_VersionNoExists"].ToString();
                SetVersion();
            }
            if (Directory.Exists(APP_DIRECTORY) && Directory.Exists(APP_FOLDER) && !Directory.Exists(VERSION_FILE))
            {
                Version = _currentResource["lm_VersionNoExists"].ToString();
                SetVersion();
            }
            if (Directory.Exists(APP_DIRECTORY) && Directory.Exists(APP_FOLDER) && Directory.Exists(VERSION_FILE))
            {
                Version = File.ReadAllText(VERSION_FILE);
                SetVersion(false, Version);
            }
        }


        private void SetVersion(bool isNewApp = true, string version = null)
        {
            if (isNewApp)
            {
                Settings.Default.Version = "1.0.0.0";
            }
            else
            {
                if (version != null)
                {
                    Settings.Default.Version = version;
                }
                else
                {
                    Settings.Default.Version = "1.0.0.0";
                }
            }
            Settings.Default.Save();
        }

        public MainWindowVM()
        {
            _languages = new ObservableCollection<CultureInfo>();
            GetLanguages();
            ProgressBarVisible = Visibility.Collapsed;
            IsEnabledButton = true;
            if (Application.Current.Resources.MergedDictionaries[2] is ResourceDictionary resourceDictionary)
            {
                _currentResource = resourceDictionary;
            }
            LanguageChanged += App_LanguageChanged;
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
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = APP_FOLDER,
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
                    InstallAppFiles();
                    break;
            }
        }

        private int _counter = 0;
        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            try
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
            catch (Exception)
            {
                new MessageBoxWindow(_currentResource["lm_UnexpectedError"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                ProgressBarVisible = Visibility.Collapsed;
                IsEnabledButton = true;
                return;
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

        private bool IsTroubleDownloadZip()
        {
            long length = new FileInfo(ZIP_FOLDER).Length;
            if (length > 0)
                return false;
            return true;
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
                if (App.IsNetwork)
                {
                    if (IsValidZip(path))
                    {
                        ProgressBarVisible = Visibility.Collapsed;
                        ZipFile.ExtractToDirectory(path, APP_DIRECTORY, true);
                        File.Delete(path);
                        IsEnabledButton = true;
                        GetVersion();
                        Status = Status.Ready;
                    }
                    else
                    {
                        if (!IsTroubleDownloadZip())
                        {
                            new MessageBoxWindow(_currentResource["lm_ErrorDownload"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                            Status = Status.DownloadingApp;
                            ProgressBarVisible = Visibility.Collapsed;
                            IsEnabledButton = true;
                        }
                        else
                        {
                            new MessageBoxWindow(_currentResource["lm_ErrorZip"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                            Status = Status.DownloadingApp;
                            ProgressBarVisible = Visibility.Collapsed;
                            IsEnabledButton = true;
                        }
                    }
                }
                else
                {
                    new MessageBoxWindow(_currentResource["lm_UnexpectedError"].ToString(), _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                    ProgressBarVisible = Visibility.Collapsed;
                    Status = Status.DownloadingApp;
                    IsEnabledButton = true;
                }
            }
            catch (Exception ex)
            {
                new MessageBoxWindow(ex.Message, _currentResource["a_Error"].ToString(), MessageBoxWindow.MessageBoxButton.Ok, GetProperties());
                Status = Status.DownloadingApp;
                ProgressBarVisible = Visibility.Collapsed;
                IsEnabledButton = true;
            }
        }


        private void InstallAppFiles()
        {
            Directory.CreateDirectory(APP_DIRECTORY);
            try
            {
                if (App.IsNetwork)
                {
                    using (WebClient webClient = new WebClient())
                    {
                        ProgressBarVisible = Visibility.Visible;
                        webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                        webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                        webClient.DownloadFileAsync(new Uri(DOWNLOAD_URL), ZIP_FOLDER);
                    }
                }
            }
            catch (Exception)
            {
                ProgressBarVisible = Visibility.Collapsed;
                CheckUpdates();
                return;
            }
        }

        private void CheckUpdates()
        {
            VersionText version = null;
            bool completed = ExecuteWithTimeLimit(TimeSpan.FromMilliseconds(1000), () =>
            {
                version = CheckVersion(DOWNLOAD_URL);
            });

            GetVersion();
            if (version != null)
            {
                if (Directory.Exists(APP_DIRECTORY))
                {
                    var zip = PathToZip();
                    if (IsEmpty())
                    {
                        if (zip == null)
                        {
                            if (Directory.Exists(APP_FOLDER))
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
                }
            }
            else
            {
                Status = Status.DownloadingApp;
                ClearDirectory();
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
                if (item.Contains(".zip"))
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


        private VersionText CheckVersion(string url)
        {
            if (App.IsNetwork)
            {
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        var response = client.DownloadString(DOWNLOAD_URL);
                        if (response.Contains("text"))
                        {
                            return JsonConvert.DeserializeObject<VersionText>(response);
                        }
                        else
                        {
                            client.Dispose();
                            return null;
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        public bool ExecuteWithTimeLimit(TimeSpan timeSpan, Action codeBlock)
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
