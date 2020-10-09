using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DiagnosticWPF
{
    /// <summary>
    /// Interaction logic for HexViewer.xaml
    /// </summary>
    public partial class HexViewer : Window
    {
        private bool _isLoaded;

        public HexViewer()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            SetSource();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            //this.DialogResult = false;
            this.Close();
            hexEditor.Stream?.Dispose();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            //this.DialogResult = true;
            this.Close();
            hexEditor.Stream?.Dispose();
        }


        private byte[] _data;
        public byte[] Data
        {
            get => _data;
            set
            {
                _data = value;
                if (_isLoaded) SetSource();
            }
        }

        private void SetSource()
        {
            hexEditor.Stream = new MemoryStream(_data);
        }
    }
}
