using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using OxyPlot.Axes;

namespace WpfApp1
{
    public partial class MetricPlot : Window
    {
        public MetricPlot(string metricColumn, string metricName, int serverId, TimeSpan range)
        {
            InitializeComponent();

            var model = new PlotModel { Title = metricName };
            var lineSeries = new LineSeries { Title = metricName, MarkerType = MarkerType.Circle };

            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=""S:\ИСП 331 Математическое моделирование\Басьюни Мартынюк 331\PROJECT YP\Project-YP-Graphic-Error\WpfApp1\Database1.mdf"";Integrated Security=True";

            DateTime fromTime = DateTime.Now - range;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand($@"
                    SELECT Timestamp, {metricColumn}
                    FROM Metric
                    WHERE ServerId = @ServerId AND Timestamp >= @FromTime
                    ORDER BY Timestamp", conn);

                cmd.Parameters.AddWithValue("@ServerId", serverId);
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

            model.Series.Add(lineSeries);
            model.Axes.Add(new OxyPlot.Axes.DateTimeAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, StringFormat = "HH:mm" });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left });

            PlotView.Model = model;
        }
    }
}