using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TrueSkills.Launcher
{
    /// <summary>
    /// Логика взаимодействия для MessageBoxWindow.xaml
    /// </summary>
    public partial class MessageBoxWindow : Window
    {
        public string Header { get; set; }
        public string Text { get; set; }
        public string FContent { get; set; }
        public string SContent { get; set; }
        public Visibility FButtonVisibility { get; set; }
        public Visibility SButtonVisibility { get; set; }
        public bool IsYes { get; set; }
        public MessageBoxWindow(string text, string title, MessageBoxButton button, string[] properties)
        {
            InitializeComponent();
            IsYes = false;
            Header = title;
            Text = text;
            switch (button)
            {
                case MessageBoxButton.YesNo:
                    SContent = properties[0];
                    FContent = properties[1];
                    break;
                case MessageBoxButton.Ok:
                    FContent = properties[2];

                    SButtonVisibility = Visibility.Collapsed;
                    break;
            }
            SButton.Click += SButton_Click;
            FButton.Click += FButton_Click;
            DataContext = this;
            this.ShowDialog();
        }

        private void FButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SButton_Click(object sender, RoutedEventArgs e)
        {
            IsYes = true;
            Close();
        }

        public enum MessageBoxButton
        {
            YesNo,
            Ok
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            }
            catch
            {
                return;
            }
        }
    }
}
