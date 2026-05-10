using LiveCharts;
using LiveCharts.Wpf;
using MaterialDesignThemes.Wpf;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace U_MOCO_FreeD_Repeater
{
    public class FreeDRecord
    {
        public string Time { get; set; } = "";
        public string X { get; set; } = "";
        public string Y { get; set; } = "";
        public string Z { get; set; } = "";
        public string Pan { get; set; } = "";
        public string Tilt { get; set; } = "";
        public string Roll { get; set; } = "";
        public string Focus { get; set; } = "";
        public string Zoom { get; set; } = "";

        public double XVal { get; set; }
        public double YVal { get; set; }
        public double ZVal { get; set; }
        public double PanVal { get; set; }
        public double TiltVal { get; set; }
        public double RollVal { get; set; }
        public double FocusVal { get; set; }
        public double ZoomVal { get; set; }
    }
    public class ForwardTarget : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private string _ip = "";
        public string IP
        {
            get => _ip;
            set { _ip = value; OnPropertyChanged(); }
        }

        private string _port = "6301";
        public string Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); }
        }

        // 显示文字，如 "3 ms" 或 "timeout"
        private string _pingDisplay = "-";
        public string PingDisplay
        {
            get => _pingDisplay;
            set { _pingDisplay = value; OnPropertyChanged(); }
        }

        // 颜色 Brush
        private Brush _pingColor = Brushes.Gray;
        public Brush PingColor
        {
            get => _pingColor;
            set { _pingColor = value; OnPropertyChanged(); }
        }

        /// <summary>更新 Ping 显示与颜色（在 UI 线程调用）</summary>
        public void UpdatePing(long? ms)
        {
            if (ms == null)
            {
                PingDisplay = "999ms";
                PingColor = Brushes.Red;
            }
            else if (ms <= 3)
            {
                PingDisplay = $"{ms}ms";
                PingColor = Brushes.LimeGreen;
            }
            else if (ms <= 15)
            {
                PingDisplay = $"{ms}ms";
                PingColor = Brushes.Yellow;
            }
            else
            {
                PingDisplay = $"{ms}ms";
                PingColor = Brushes.Red;
            }
        }

        /// <summary>尝试解析 Port 为 int，失败返回 -1</summary>
        public int GetPort() => int.TryParse(Port, out int p) ? p : -1;
    }
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private const int MaxTableRecords = 2000;
        private const int ChartPoints = 300;
        private record ActiveChannel(LineSeries Series, ChartValues<double> Values, Func<FreeDMsg.FreeDData, double> Get);
        private List<ActiveChannel> _activeChannels = new();

        public Func<double, string> Formatter { get; set; } = value => value.ToString("F2");

        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private int _packetCount = 0;
        private int _totalCount = 0;
        private bool _isListening = false;
        private bool _isForwardGridReadOnly = false;
        public bool IsNotListening => !_isForwardGridReadOnly;
        public bool IsForwardGridReadOnly
        {
            get => _isForwardGridReadOnly;
            set { _isForwardGridReadOnly = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotListening)); }
        }
        public ObservableCollection<FreeDRecord> Records { get; } = new();
        public ObservableCollection<ForwardTarget> ForwardTargets { get; } = new();

        private SeriesCollection _chartSeries = new();
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }

        private readonly ConcurrentQueue<(FreeDMsg.FreeDData data, byte[] raw)> _dataQueue = new();

        private record ChannelDef(string Name, Brush Stroke, Func<FreeDMsg.FreeDData, double> Get);

        private readonly ChannelDef[] _channelDefs =
        [
            new("X",     Brushes.OrangeRed,  d => d.XPos),
            new("Y",     Brushes.LimeGreen,  d => d.YPos),
            new("Z",     Brushes.DodgerBlue, d => d.ZPos),
            new("Pan",   Brushes.Yellow,     d => d.Pan),
            new("Tilt",  Brushes.Violet,     d => d.Tilt),
            new("Roll",  Brushes.Cyan,       d => d.Roll),
            new("Focus", Brushes.HotPink,    d => d.Focus),
            new("Zoom",  Brushes.Gold,       d => d.Zoom),
        ];  

        // ComboBox 数据源
        public string[] ChannelNames => _channelDefs.Select(c => c.Name).ToArray();

        private System.Windows.Threading.DispatcherTimer? _chartTimer;
        // Ping 定时器（1 秒一次）
        private Timer? _pingTimer;
        // UDP 转发客户端（共用，无需绑定端口）
        private readonly UdpClient _forwardClient = new UdpClient();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            dataGrid.ItemsSource = Records;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (_isListening) StopListening();
            else StartListening();
        }

        private void InitChart()
        {
            ChartSeries.Clear();
            _activeChannels.Clear();

            // 默认选中 X（index 0）
            AddActiveChannel(0);

            _chartTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(32)
            };
            _chartTimer.Tick += ChartTimer_Tick;
            _chartTimer.Start();
        }
        private void AddActiveChannel(int index)
        {
            var ch = _channelDefs[index];
            var vals = new ChartValues<double>();
            var series = new LineSeries
            {
                Title = ch.Name,
                Values = vals,
                Stroke = ch.Stroke,
                Fill = null,
                PointGeometrySize = 0,
                StrokeThickness = 1.5,
            };
            ChartSeries.Add(series);
            _activeChannels.Add(new ActiveChannel(series, vals, ch.Get));
        }
        private void SetActiveChannels(params int[] indices)
        {
            ChartSeries.Clear();
            _activeChannels.Clear();
            foreach (var i in indices)
                AddActiveChannel(i);
        }
        private void ChartTimer_Tick(object? sender, EventArgs e)
        {
            if (_dataQueue.IsEmpty) return;

            var batch = new List<FreeDMsg.FreeDData>(32);
            while (_dataQueue.TryDequeue(out var item))
                batch.Add(item.data);

            // ── 更新表格 ──
            var latest = batch[^1];
            var now = DateTime.Now;
            Records.Insert(0, new FreeDRecord
            {
                Time = $"{now:HH}:{now:mm}:{now:ss}.{now.Millisecond:D3}",
                X = $"{latest.XPos:F2}",
                Y = $"{latest.YPos:F2}",
                Z = $"{latest.ZPos:F2}",
                Pan = $"{latest.Pan:F2}",
                Tilt = $"{latest.Tilt:F2}",
                Roll = $"{latest.Roll:F2}",
                Focus = latest.Focus.ToString(),
                Zoom = latest.Zoom.ToString(),
            });
            if (Records.Count > MaxTableRecords)
                Records.RemoveAt(Records.Count - 1);

            lblCount.Content = _totalCount.ToString();

            // ── 更新所有激活通道 ──
            foreach (var ac in _activeChannels)
            {
                var newVals = new double[batch.Count];
                for (int j = 0; j < batch.Count; j++)
                    newVals[j] = ac.Get(batch[j]);

                ac.Values.AddRange(newVals);

                int excess = ac.Values.Count - ChartPoints;
                if (excess > 0)
                {
                    var kept = new double[ChartPoints];
                    for (int j = 0; j < ChartPoints; j++)
                        kept[j] = ac.Values[excess + j];
                    ac.Values.Clear();
                    ac.Values.AddRange(kept);
                }
            }
        }

        private void RbChannel_Checked(object sender, RoutedEventArgs e)
        {
            if (_activeChannels.Count == 0) return;

            if (sender is RadioButton rb)
            {
                if (rb == rbXYZ)
                    SetActiveChannels(0, 1, 2);          // X Y Z
                else if (rb == rbPTR)
                    SetActiveChannels(3, 4, 5);          // Pan Tilt Roll
                else if (rb == rbFZ)
                    SetActiveChannels(6, 7);             // Focus Zoom
                else
                {
                    int idx = rb switch
                    {
                        _ when rb == rbX => 0,
                        _ when rb == rbY => 1,
                        _ when rb == rbZ => 2,
                        _ when rb == rbPan => 3,
                        _ when rb == rbTilt => 4,
                        _ when rb == rbRoll => 5,
                        _ when rb == rbFocus => 6,
                        _ when rb == rbZoom => 7,
                        _ => 0
                    };
                    SetActiveChannels(idx);
                }
            }
        }
        private void StartListening()
        {
            if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("请输入有效的端口号（1-65535）", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { _udpClient = new UdpClient(port); }
            catch (Exception ex)
            {
                MessageBox.Show($"无法绑定端口 {port}：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _isListening = true;
            IsForwardGridReadOnly = true;
            btnOK.Background = new SolidColorBrush(Colors.LimeGreen);
            btnOK.Content = new PackIcon { Kind = PackIconKind.Close, Width = 15, Height = 15 };
            _totalCount = 0;
            while (_dataQueue.TryDequeue(out _)) { }

            _chartTimer?.Start();

            // ── 接收 & 转发线程 ────────────────────────────────────
            new Thread(() =>
            {
                IPEndPoint remoteEP = new(IPAddress.Any, 0);
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        byte[] raw = _udpClient.Receive(ref remoteEP);
                        var freed = FreeDMsg.ParseFreeD(raw);
                        if (freed.IsValid)
                        {
                            Interlocked.Increment(ref _packetCount);
                            Interlocked.Increment(ref _totalCount);
                            _dataQueue.Enqueue((freed, raw));

                            // 转发到所有目标（在接收线程直接发，避免延迟）
                            ForwardData(raw);
                        }
                    }
                    catch { break; }
                }
            })
            { IsBackground = true }.Start();

            // ── 频率统计线程 ───────────────────────────────────────
            new Thread(() =>
            {
                const double interval = 2.0;
                var sw = Stopwatch.StartNew();
                while (!token.IsCancellationRequested)
                {
                    while (sw.Elapsed.TotalSeconds < interval)
                    {
                        if (token.IsCancellationRequested) return;
                        Thread.SpinWait(100);
                    }
                    double elapsed = sw.Elapsed.TotalSeconds;
                    sw.Restart();
                    double hz = Interlocked.Exchange(ref _packetCount, 0) / elapsed;
                    Dispatcher.InvokeAsync(() => lblFrequency.Content = $"{hz:F2} Hz");
                }
            })
            { IsBackground = true }.Start();

            // ── Ping 定时器（1 秒一次）────────────────────────────
            _pingTimer = new System.Threading.Timer(_ => PingAllTargets(),
                null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void StopListening()
        {
            _pingTimer?.Dispose();
            _pingTimer = null;

            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient = null;
            _isListening = false;
            _chartTimer?.Stop();

            IsForwardGridReadOnly = false;
            btnOK.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FF9E9E9E"));
            btnOK.Content = new PackIcon { Kind = PackIconKind.Check, Width = 15, Height = 15 };
            lblFrequency.Content = "0.00 Hz";

            // 清空 Ping 显示
            Dispatcher.InvokeAsync(() =>
            {
                foreach (var t in ForwardTargets)
                {
                    t.PingDisplay = "-";
                    t.PingColor = Brushes.Gray;
                }
            });
        }
        private void ForwardData(byte[] raw)
        {
            // 快照，避免枚举时集合变更
            ForwardTarget[] targets;
            Dispatcher.Invoke(() => targets = ForwardTargets.ToArray());
            // 注意：此处在非 UI 线程，需安全访问
            // 实际用本地快照：
            lock (_forwardLock)
            {
                foreach (var t in _targetSnapshot)
                {
                    int p = t.GetPort();
                    if (string.IsNullOrWhiteSpace(t.IP) || p < 1) continue;
                    try
                    {
                        _forwardClient.Send(raw, raw.Length, t.IP.Trim(), p);
                    }
                    catch { /* 忽略单个目标发送失败 */ }
                }
            }
        }
        // 快照列表，在 UI 线程更新，转发线程读取
        private readonly object _forwardLock = new();
        private ForwardTarget[] _targetSnapshot = [];

        private void RefreshTargetSnapshot()
        {
            lock (_forwardLock)
                _targetSnapshot = ForwardTargets.ToArray();
        }

        // ── Ping 所有目标 ──────────────────────────────────────────
        private void PingAllTargets()
        {
            ForwardTarget[] targets;
            lock (_forwardLock) targets = _targetSnapshot;

            foreach (var t in targets)
            {
                if (string.IsNullOrWhiteSpace(t.IP)) continue;
                long? ms = DoPing(t.IP.Trim());
                Dispatcher.InvokeAsync(() => t.UpdatePing(ms));
            }
        }

        private static long? DoPing(string host)
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send(host, 900);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : (long?)null;
            }
            catch { return null; }
        }
        private void btnAddTarget_Click(object sender, RoutedEventArgs e)
        {
            ForwardTargets.Add(new ForwardTarget { IP = "", Port = "" });
            RefreshTargetSnapshot();
        }

        private void btnRemoveTarget_Click(object sender, RoutedEventArgs e)
        {
            var selected = forwardGrid.SelectedItem as ForwardTarget;
            if (selected != null)
            {
                ForwardTargets.Remove(selected);
                RefreshTargetSnapshot();
            }
        }
        private void btnClear_Click(object sender, RoutedEventArgs e) => Records.Clear();

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            InitChart();
            txtPort.Focus();
            txtPort.SelectAll();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            // 导出逻辑保持不变
        }
    }
}