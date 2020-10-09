using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

using DiagnosticWPF.Helpers;
using DiagnosticWPF.Models;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Win32;

using WpfHexaEditor;

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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeQueries()
        {
            _queries = new List<KnownQuery>()
            {
                new KnownQuery(typeof(UIDumpHeapStat), "DumpHeapStat", a =>
                    a.DumpHeapStat(0)
                        .Select(t => new UIDumpHeapStat()
                        {
                            Type = t.type,
                            Objects = t.objects,
                            GraphSize = t.size,
                        })
                        .ToList()),

                new KnownQuery(typeof(UIStaticFields), "GetStaticFieldsWithGraphAndSize", a =>
                    a.GetStaticFieldsWithGraphAndSize()
                        .Select(t => new UIStaticFields()
                        {
                            Field = t.field,
                            Obj = t.obj,
                            Size = (long)t.size,
                        })
                        .ToList()),

                new KnownQuery(typeof(UIDupStrings), "GetDuplicateStrings", a =>
                    a.GetDuplicateStrings()
                        .Select(t => new UIDupStrings()
                        {
                            Text = t.Key,
                            Count = t.Value,
                        })
                        .ToList()),

                new KnownQuery(typeof(UIStringsBySize), "GetStringsBySize", a =>
                    a.GetStringsBySize(0)
                        .Select(t => new UIStringsBySize()
                        {
                            Obj = t.obj,
                            Text = t.text,
                        })
                        .ToList()),

                new KnownQuery(typeof(ClrModule), "Modules", a => a.Modules.ToList()),

                new KnownQuery(typeof(UIStackFrame), "Threads stacks", a =>
                    a.Stacks()
                    .Select(s => new UIStackFrame()
                    {
                        Thread = s.thread,
                        StackFrames = s.stackFrames.ToList(),
                    })
                    .ToList()),

                new KnownQuery(typeof(IClrRoot), "Roots", a => a.Roots.ToList()),

                new KnownQuery(typeof(ClrObject), "ObjectsBySize", a => a.GetObjectsBySize(1).ToList()),
                new KnownQuery(typeof(ClrObject), "NonSystemObjectsBySize", a =>
                    a.GetObjectsBySize(1)
                    .Where(o => ((o.Type.Name != null &&
                                !o.Type.Name.StartsWith("System") &&
                                !o.Type.Name.StartsWith("Microsoft") &&
                                !o.Type.Name.StartsWith("Interop") &&
                                !o.Type.Name.StartsWith("Internal")) &&
                                !o.Type.IsFree)
                                || o.Type.Name == null)
                    .ToList()),

                new KnownQuery(typeof(UIAllocatorGroup), "Experimental GetObjectsGroupedByAllocator", a =>
                    a.GetObjectsGroupedByAllocator(a.Objects)
                    .Select(g => new UIAllocatorGroup()
                    {
                        Allocator = g.allocator,
                        Objects = g.objects,
                        Name = a.GetAllocatorName(g.allocator), // experimental!!
                    })
                    .ToList()),
            };

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeQueries();

            ComboQueries.ItemsSource = _queries;
            ComboQueries.IsEnabled = false;
        }

        private void OpenDump(object sender, RoutedEventArgs e)
        {
            Close(null, null);
            var filename = FileHelper.OpenDialog(this, "Select a dump file");
            if (File.Exists(filename))
            {
                try
                {
                    _analyzer = DiagnosticAnalyzer.FromDump(filename, true);
                    ComboQueries.IsEnabled = true;
                }
                catch (Exception err)
                {
                    _analyzer = null;
                    ComboQueries.IsEnabled = false;
                    status.Text = err.Message;
                }
            }

        }

        private void Snapshot(object sender, RoutedEventArgs e)
        {
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
            Dispatcher.Invoke(() =>
            {
                tb.Text = data;
            });
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
            Master.ItemsSource = item.Populate(_analyzer);
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
                _currentDetailsProperty = null;
                return;
            }
            _currentDetailsProperty = uIGrid.DetailsProperty;
            detailsColumn.Width = new GridLength(1, GridUnitType.Star);

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
            if (data == null || _currentDetailsProperty == null) return;
            var dataDetails = _currentDetailsProperty.GetValue(data) as System.Collections.IEnumerable;
            if (dataDetails == null) return;
            Details.ItemsSource = dataDetails;
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
    }
}
