﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using ClrDiagnostics;
using ClrDiagnostics.Triggers;

using CustomEventSource;

using DiagnosticInvestigations;

using DiagnosticWPF.Helpers;
using DiagnosticWPF.Models;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Win32;

using WpfHexaEditor;

using static System.Net.Mime.MediaTypeNames;

namespace DiagnosticWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DiagnosticAnalyzer _analyzer;
        private PropertyInfo _currentDetailsProperty;
        private IList<KnownQuery> _queries;
        private Process _process;
        private TriggerAll _triggerAll;
        private ListCollectionView _masterView;
        private KnownQuery _currentQuery;
        private CancellationTokenSource _updateDetailsTextCts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeQueries()
        {
            var queryService = new QueriesService();
            _queries = queryService.Queries.Values.ToList();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeQueries();

            ComboQueries.ItemsSource = _queries;
            ComboQueries.IsEnabled = false;
        }

        private void OpenDump(object sender, RoutedEventArgs e)
        {
            status.Text = string.Empty;
            Close(null, null);
            var filename = FileHelper.OpenDialog(this, "Select a dump file");
            if (string.IsNullOrEmpty(filename)) return;
            var fi = new FileInfo(filename);
            if (fi.Exists)
            {
                try
                {
                    _analyzer = DiagnosticAnalyzer.FromDump(fi.FullName, true);
                    ComboQueries.IsEnabled = true;
                }
                catch (Exception err)
                {
                    _analyzer = null;
                    ComboQueries.IsEnabled = false;
                    status.Text = err.Message;
                    return;
                }
            }

            UpdateStatus($"File {fi.Name} loaded");
        }

        private void Snapshot(object sender, RoutedEventArgs e)
        {
            status.Text = string.Empty;
            if (_process == null)
            {
                MessageBox.Show(this, "Monitor a process before snapshotting it", "Snapshot", MessageBoxButton.OK);
                return;
            }

            try
            {
                ResetUI();
                var sw = new Stopwatch();
                sw.Start();
                var processPath = new FileInfo(_process.MainModule.FileName).DirectoryName;
                _analyzer = DiagnosticAnalyzer.FromSnapshot(_process.Id);
                //_analyzer = DiagnosticAnalyzer.FromProcess(_process.Id);
                var elapsed = sw.Elapsed;
                status.Text = $"Process {_process.Id} snapshot took {sw.ElapsedMilliseconds}ms";
                ComboQueries.IsEnabled = true;
            }
            catch (Exception err)
            {
                _analyzer = null;
                ComboQueries.IsEnabled = false;
                status.Text = err.Message;
            }
        }

        private void MonitorProcess(object sender, RoutedEventArgs e)
        {
            status.Text = string.Empty;
            Close(null, null);
            ProcessPicker picker = new ProcessPicker();
            var result = picker.ShowDialog();
            if (result.HasValue && result.Value)
            {
                _process = picker.SelectedProcess;
                SubscribeTriggers();
            }
        }

        private void SubscribeTriggers()
        {
            UnsubscribeTriggers();
            _triggerAll = new TriggerAll(_process.Id, Constants.CustomHeaderEventSourceName,
                Constants.TriggerHeaderCounterName);

            _triggerAll.OnCpu = d => UpdateTextBlock(trCpu, d.ToString() + "%");
            _triggerAll.OnEventCounterCount = d => UpdateTextBlock(trCustomHeader, d.ToString());
            _triggerAll.OnException = d => UpdateTextBlock(trException, d);
            _triggerAll.OnGcAllocation = d => UpdateTextBlock(trGcAlloc, $"{d}");
            _triggerAll.OnHttpRequests = d => UpdateTextBlock(trHttpReq, $"{d}/sec");
            _triggerAll.OnWorkingSet = d => UpdateTextBlock(trWorkingSet, $"{d} MB");

            _triggerAll.Start();
        }

        private void UnsubscribeTriggers()
        {
            if (_triggerAll != null)
            {
                _triggerAll.Dispose();
                _triggerAll = null;
            }
        }

        private void UpdateTextBlock(TextBlock tb, string data)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    tb.Text = data;
                });
            }
            catch(Exception)
            {
                // Swallow exceptions coming from the dispatcher
                // This happens when the application is closing
                // and the UI has gone before the async operation
                // has completed
            }
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            if (_analyzer != null)
            {
                ResetUI();

                _analyzer.Dispose();
                _analyzer = null;
                ComboQueries.IsEnabled = false;
                status.Text = string.Empty;
            }
        }

        private void ResetUI()
        {
            status.Text = string.Empty;
            _masterView = null;
            _currentQuery = null;
            Master.ItemsSource = null;
            Details.ItemsSource = null;
            Master.Columns.Clear();
            Details.Columns.Clear();
            ComboQueries.SelectedItem = null;
        }

        private void ComboQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ComboQueries.SelectedItem as KnownQuery;
            if (item == null) return;

            ComboCue.Visibility = Visibility.Collapsed;

            if (!KnownGrids.TryGetUIGridByType(item.Type, out UIGrid uIGrid)) return;

            UIGrid detailsUiGrid = null;
            if (uIGrid.DetailsType != null)
                KnownGrids.TryGetUIGridByType(uIGrid.DetailsType, out detailsUiGrid);

            MakeGrid(uIGrid, detailsUiGrid);
            _currentQuery = item;
            _masterView = (ListCollectionView)CollectionViewSource.GetDefaultView(item.Populate(_analyzer));
            _masterView.Filter = MasterFilter;
            Master.ItemsSource = _masterView;
        }

        public void MakeGrid(UIGrid uIGrid, UIGrid detailsUiGrid)
        {
            Master.ItemsSource = null;
            Details.ItemsSource = null;
            Master.Columns.Clear();
            Details.Columns.Clear();

            foreach (var c in uIGrid.Columns)
            {
                var column = DynamicGridMaker.CreateGridColumn(c);
                Master.Columns.Add(column);
            }

            if (uIGrid.DetailsType == null)
            {
                detailsColumn.Width = new GridLength(0);
                detailsColumn.MinWidth = 0;
                _currentDetailsProperty = null;
                return;
            }
            _currentDetailsProperty = uIGrid.DetailsProperty;
            detailsColumn.Width = new GridLength(1, GridUnitType.Star);
            detailsColumn.MinWidth = 100;

            if (detailsUiGrid == null) return;

            foreach (var c in detailsUiGrid.Columns)
            {
                var column = DynamicGridMaker.CreateGridColumn(c);
                Details.Columns.Add(column);
            }
        }

        private void Master_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var data = Master.SelectedItem;
            if (data is ClrObject clrObject)
            {
                UpdateDetailsText(clrObject);
                return;
            }

            UpdateDetailsText(null);

            if (data == null || _currentDetailsProperty == null) return;
            var dataDetails = _currentDetailsProperty.GetValue(data) as System.Collections.IEnumerable;
            if (dataDetails == null)
            {
                return;
            }

            Details.ItemsSource = dataDetails;
        }

        private void Details_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var data = Details.SelectedItem;
            if (data is ClrObject clrObject)
            {
                UpdateDetailsText(clrObject);
                return;
            }

            UpdateDetailsText(null);
        }

        private void ClearLastException(object sender, RoutedEventArgs e)
        {
            trException.Text = string.Empty;
        }

        private void GridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;

            var blob = row.Item switch
            {
                ClrObject clrObject => GetBlob(clrObject),
                IClrRoot clrRoot => GetBlob(clrRoot.Object),
                _ => null,
            };

            if (blob == null) return;

            var hex = new HexViewer();
            hex.Data = blob;
            hex.Show();

            byte[] GetBlob(ClrObject @object) => _analyzer.ReadRawContent(@object);
        }

        private void ClearFilter(object sender, RoutedEventArgs e)
        {
            FilterTextBlock.Text = string.Empty;
            FilterChanged(null, null);
        }

        private void ClearHeader(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            var textBlock = button.Tag as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = string.Empty;
                return;
            }
        }

        private void FilterChanged(object sender, KeyEventArgs e)
        {
            if (_analyzer == null || _masterView == null) return;

            _masterView.Filter = MasterFilter;
        }

        private bool MasterFilter(object obj)
        {
            try
            {
                var result = _currentQuery.Filter(obj, FilterTextBlock.Text);
                if (!result.HasValue) return false;
                return result.Value;
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.ToString());
                return false;
            }
        }

        private async void UpdateStatus(string text, int milliseconds = 3500)
        {
            if (text == null) text = string.Empty;
            status.Text = text;
            await Task.Delay(milliseconds);
            status.Text = string.Empty;
        }

        private async void UpdateDetailsText(ClrObject? clrObject)
        {
            if (_updateDetailsTextCts != null)
                _updateDetailsTextCts.Cancel();

            _updateDetailsTextCts = new CancellationTokenSource();
            textDetails.Text = string.Empty;
            if (clrObject == null)
            {
                detailsRow.Height = new GridLength(0);
                detailsRow.MinHeight = 0;
                return;
            }

            int count = _analyzer.GetGraphPathsCount(clrObject.Value);
            if (count > 75)
            {
                if (MessageBox.Show($"There are {count} references. Do you want to continue?",
                    "Time expensive operation",
                    MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    return;
                }
            }

            try
            {
                var previous = 0;
                string rootText = await _analyzer.PrintRootsAsync(clrObject.Value, num =>
                {
                    var newProgress = (int)((double)num * 100.0 / (double)count);
                    if (newProgress != previous)
                    {
                        Dispatcher.Invoke(() => status.Text = $"{newProgress}%");
                        previous = newProgress;
                    }
                }, _updateDetailsTextCts.Token);

                textDetails.Text = rootText;
                detailsRow.Height = new GridLength(3, GridUnitType.Star);
                detailsRow.MinHeight = 100;

                UpdateStatus($"Graph built successfully ({count} references)");
                _updateDetailsTextCts = null;
            }
            catch (OperationCanceledException err)
            {
                Debug.WriteLine($"Roots computation canceled: {err.Message}");
                UpdateStatus(string.Empty);
            }
            catch (Exception err)
            {
                UpdateStatus(err.Message);
            }
        }
    }
}
