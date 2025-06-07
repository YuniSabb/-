using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer metricTimer;
        private TimeSpan currentRange = TimeSpan.FromHours(1);
        private PlotModel model;
        private string currentMetric = "CpuLoad";
        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=""C:\Users\778\OneDrive\Рабочий стол\Project-YP-Graphic-Error\WpfApp1\Database1.mdf"";Integrated Security=True";

        public MainWindow()
        {
            InitializeComponent();
            StartMetricTimer();
            LoadPlot(currentRange);
            MetricCollector.ShowAlertMessage = (msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(msg, "Оповещение об инциденте", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            };
        }

        private void StartMetricTimer()
        {
            metricTimer = new DispatcherTimer();
            metricTimer.Interval = TimeSpan.FromSeconds(10);
            metricTimer.Tick += (s, e) =>
            {
                try
                {
                    MetricCollector.SaveMetricToDatabase(1, connectionString);
                    LoadPlot(currentRange); // обновление графика сразу после добавления
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении метрики: {ex.Message}");
                }
            };
            metricTimer.Start();

            try
            {
                MetricCollector.SaveMetricToDatabase(1, connectionString);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сбора метрик:\n" + ex.Message);
            }

            // Получим диск и сеть, отобразим
            float disk = MetricCollector.GetFreeDiskSpace();
            string net = MetricCollector.GetNetworkStatus();

            Dispatcher.Invoke(() =>
            {
                DiskFreeText.Text = $"Диск C: {disk:F1} ГБ свободно";
                NetworkStatusText.Text = $"Сеть: {(net == "Up" ? "Доступна" : "Недоступна")}";
            });

            LoadPlot(currentRange);
        }

        private void LoadPlot(TimeSpan range)
        {
            var lineSeries = new LineSeries
            {
                Title = currentMetric,
                MarkerType = MarkerType.Circle
            };

            DateTime fromTime = DateTime.Now - range;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand($@"
                    SELECT Timestamp, {currentMetric}
                    FROM Metric
                    WHERE ServerId = 1 AND Timestamp >= @FromTime
                    ORDER BY Timestamp", conn);

                cmd.Parameters.AddWithValue("@FromTime", fromTime);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime time = reader.GetDateTime(0);
                        double value = Convert.ToDouble(reader[1]);
                        lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(time), value));
                    }
                }
            }

            model = new PlotModel { Title = $"{currentMetric} за {range.TotalHours} ч." };
            model.Series.Add(lineSeries);

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm",
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            PlotView.Model = model;
        }

        private void Cpu_Click(object sender, RoutedEventArgs e)
        {
            currentMetric = "CpuLoad";
            LoadPlot(currentRange);
        }

        private void Ram_Click(object sender, RoutedEventArgs e)
        {
            currentMetric = "RamUsage";
            LoadPlot(currentRange);
        }


        private void OneHour_Click(object sender, RoutedEventArgs e)
        {
            currentRange = TimeSpan.FromHours(1);
            LoadPlot(currentRange);
        }

        private void TwentyFourHours_Click(object sender, RoutedEventArgs e)
        {
            currentRange = TimeSpan.FromHours(24);
            LoadPlot(currentRange);
        }

        private void SevenDays_Click(object sender, RoutedEventArgs e)
        {
            currentRange = TimeSpan.FromHours(168);
            LoadPlot(currentRange);
        }

        private void IncidentLog_Click(object sender, RoutedEventArgs e)
        {
            var window = new IncidentLogWindow();
            window.Show();
        }
    }
}
