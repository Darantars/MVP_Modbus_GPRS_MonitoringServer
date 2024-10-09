using System;
using System.IO;
using System.Threading.Tasks;

namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class DeviceLogger
    {
        public string DeviceId { get; set; }

        private string deviceLog { get; set; }

        private DateTime lastFileCreateTime { get; set; }

        private string logFilePath { get; set; }

        private string logDirectoryPath { get; set; }

        public DeviceLogger(string newDeviceId)
        {
            DeviceId = newDeviceId;
            deviceLog = "";
            logDirectoryPath = Path.GetFullPath(@"UserData\Logs");
            lastFileCreateTime = DateTime.MinValue;

            Console.WriteLine($"Log directory path: {logDirectoryPath}");
        }

        public async Task LogParamChangeAsync(DateTime changeTime, string parametrName, string value)
        {
            if (changeTime > lastFileCreateTime + TimeSpan.FromHours(3))
            {
                await CreateNewLogFileAsync();
            }
            deviceLog += $"{changeTime.ToString("dd.MM.yyyy HH:mm:ss")} - {parametrName}: {value}\r\n";
            await WriteLogToFileAsync();
        }

        private async Task CreateNewLogFileAsync()
        {
            string deviceDirectoryPath = Path.Combine(logDirectoryPath, DeviceId);
            if (!Directory.Exists(deviceDirectoryPath))
            {
                Directory.CreateDirectory(deviceDirectoryPath);
                Console.WriteLine($"Created device directory: {deviceDirectoryPath}");
            }

            string fileName = $"{DateTime.Now:yyyy.MM.dd.HH.mm.ss}.log";
            logFilePath = Path.Combine(deviceDirectoryPath, fileName);
            lastFileCreateTime = DateTime.Now;

            Console.WriteLine($"Created new log file: {logFilePath}");
        }

        private async Task WriteLogToFileAsync()
        {
            try
            {
                await File.WriteAllTextAsync(logFilePath, deviceLog, System.Text.Encoding.UTF8);
                Console.WriteLine($"Log written to file: {logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
                throw new Exception("Ошибка записи в файл лога: " + ex.Message);
            }
        }

        public async Task <List<(DateTime date, string value)>> GetParameterValuesLast3Hours(string parameterName)
        {
            var values = new List<(DateTime date, string value)>();
            var cutoffTime = DateTime.Now - TimeSpan.FromHours(3);

            // Разделяем deviceLog на строки
            var lines = deviceLog.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Разделяем строку на части
                var parts = line.Split(new[] { " - " }, StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                var timePart = parts[0];
                var valuePart = parts[1];

                // Разделяем valuePart на имя параметра и значение
                var valueParts = valuePart.Split(new[] { ": " }, StringSplitOptions.None);
                if (valueParts.Length != 2)
                    continue;

                var paramName = valueParts[0];
                var paramValue = valueParts[1];

                DateTime.TryParse(timePart, out DateTime time);

                if (paramName.Replace(" ", "").Equals(parameterName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) && paramValue != "null" && paramValue != "Не получены данные от устройства")
                {
                    values.Add((date: time, value: paramValue));
                }
            }

            Console.WriteLine($"Parameter values for {parameterName}: {values.Count} entries"); // Отладочное сообщение
            return values;
        }
    }
}
