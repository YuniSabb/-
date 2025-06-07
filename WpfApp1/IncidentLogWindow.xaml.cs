using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace WpfApp1
{
    public partial class IncidentLogWindow : Window
    {
        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=""C:\Users\778\OneDrive\Рабочий стол\Project-YP-Graphic-Error\WpfApp1\Database1.mdf"";Integrated Security=True";

        public IncidentLogWindow()
        {
            InitializeComponent();
            LoadIncidentLog();
        }

        private void LoadIncidentLog()
        {
            DataTable table = new DataTable();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT 
                        IncidentId,
                        AlertType,
                        ThresholdValue,
                        TriggeredAt
                    FROM Incident
                    ORDER BY TriggeredAt DESC", conn);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(table);
            }

            IncidentGrid.ItemsSource = table.DefaultView;
        }
    }
}