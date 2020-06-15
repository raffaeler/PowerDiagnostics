using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Diagnostics.NETCore.Client;

namespace DiagnosticWPF
{
    /// <summary>
    /// Interaction logic for ProcessPicker.xaml
    /// </summary>
    public partial class ProcessPicker : Window
    {
        public ProcessPicker()
        {
            InitializeComponent();
        }

        public Process SelectedProcess { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            var processes = DiagnosticsClient.GetPublishedProcesses()
                .Select(p => Process.GetProcessById(p))
                .ToList();

            lvProcesses.ItemsSource = processes;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            SelectedProcess = lvProcesses.SelectedItem as Process;
            this.DialogResult = true;
            this.Close();
        }

    }
}
