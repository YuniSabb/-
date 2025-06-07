using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Data.SqlClient;

namespace WpfApp1
{
    public static class MetricCollector
    {
        //Делегат для вывода уведомления
        public static Action<string> ShowAlertMessage = null;

        //Флаг, чтобы показать MessageBox только один раз
        private static bool wasIncidentShown = false;

        public static float GetCpuUsage()
        {
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            {
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000);
                return cpuCounter.NextValue();
            }
        }

        public static float GetRamUsage()
        {
            using (var ramCounter = new PerformanceCounter("Memory", "Available MBytes"))
            {
                float available = ramCounter.NextValue();
                float total = GetTotalRamInMB();
                return 100 - (available / total * 100);
            }
        }

        private static float GetTotalRamInMB()
        {
            var wql = new ObjectQuery("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            using (var searcher = new ManagementObjectSearcher(wql))
            {
                foreach (var result in searcher.Get())
                {
                    return Convert.ToSingle(result["TotalVisibleMemorySize"]) / 1024;
                }
            }
            return 0;
        }

        public static float GetFreeDiskSpace(string driveLetter = "C")
        {
            var drive = new DriveInfo(driveLetter);
            return drive.IsReady ? drive.AvailableFreeSpace / (1024f * 1024f * 1024f) : -1;
        }

        public static string GetNetworkStatus(string address = "8.8.8.8")
        {
            using (var ping = new Ping())
            {
                try
                {
                    var reply = ping.Send(address, 1000);
                    return reply.Status == IPStatus.Success ? "Up" : "Down";
                }
                catch
                {
                    return "Down";
                }
            }
        }

        public static void SaveMetricToDatabase(int serverId, string connectionString)
        {
            float cpu = GetCpuUsage();
            float ram = GetRamUsage();
            float disk = GetFreeDiskSpace();
            string network = GetNetworkStatus();

            if (float.IsNaN(cpu) || float.IsNaN(ram) || disk < 0 || string.IsNullOrEmpty(network))
                throw new Exception("Некорректные значения метрик: cpu/ram/disk/network");

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO Metric (ServerId, Timestamp, CpuLoad, RamUsage, DiskFree, NetworkStatus)
                    VALUES (@ServerId, @Timestamp, @CpuLoad, @RamUsage, @DiskFree, @NetworkStatus)", conn);

                cmd.Parameters.AddWithValue("@ServerId", serverId);
                cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                cmd.Parameters.AddWithValue("@CpuLoad", cpu);
                cmd.Parameters.AddWithValue("@RamUsage", ram);
                cmd.Parameters.AddWithValue("@DiskFree", disk);
                cmd.Parameters.AddWithValue("@NetworkStatus", network);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                    throw new Exception("Ошибка: строка не была добавлена в таблицу Metric.");

                //Проверка превышения порогов
                CheckAndLogIncident(conn, "CPU", cpu);
                CheckAndLogIncident(conn, "RAM", ram);
            }
        }

        private static void CheckAndLogIncident(SqlConnection conn, string type, float value)
        {
            const float threshold = 90f; //можно изменить порог вручную

            if (value < threshold) return;

            var checkCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Incident
                WHERE AlertType = @Type AND TriggeredAt > DATEADD(MINUTE, -5, GETDATE())", conn);
            checkCmd.Parameters.AddWithValue("@Type", type);

            int existing = (int)checkCmd.ExecuteScalar();
            if (existing > 0) return;

            var insertCmd = new SqlCommand(@"
                INSERT INTO Incident (AlertType, ThresholdValue, TriggeredAt)
                VALUES (@Type, @Threshold, @Now)", conn);
            insertCmd.Parameters.AddWithValue("@Type", type);
            insertCmd.Parameters.AddWithValue("@Threshold", threshold);
            insertCmd.Parameters.AddWithValue("@Now", DateTime.Now);
            insertCmd.ExecuteNonQuery();

            Console.WriteLine($"[ALERT] {type} превышен: {value:F1}% > {threshold}% — инцидент записан.");

            //Показать окно 1 раз
            if (!wasIncidentShown && ShowAlertMessage != null)
            {
                wasIncidentShown = true;
                ShowAlertMessage.Invoke($"{type} превышен: {value:F1}% (> {threshold}%) — инцидент записан.");
            }
        }
    }
}
