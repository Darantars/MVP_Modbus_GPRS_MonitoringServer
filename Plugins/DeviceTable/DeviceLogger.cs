using System.Data;

namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class DeviceLogger
    {
        public string deviceId { get; set; }

        public string deviceLog { get; set; }

        private DateTime lastFileCreateTime { get; set; }

        private string logFilePath { get; set; }

        private string lofDirectoryPath { get; set; }



        public DeviceLogger(string newDeviceId)
        {
            deviceId = newDeviceId;

            deviceLog = "";

            lofDirectoryPath =  @"..\..\UserData\Logs";
        }

        public void LogParamChange(DateTime changeTime, string parametrName, string value)
        {
            if (changeTime > lastFileCreateTime + TimeSpan.FromHours(3))
                CreateNewLogFile();
            deviceLog += $"{changeTime.ToString("dd.MM.yyyy HH:mm:ss")} - {parametrName}: {value}\r\n";
        }

        private void CreateNewLogFile() 
        {
            if (!Directory.Exists(lofDirectoryPath + $"\\{deviceId}"))
            {
                Directory.CreateDirectory(lofDirectoryPath + $"\\{deviceId}");
            }
            try
            {
                File.Create(lofDirectoryPath  + $"\\{deviceId}" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            catch 
            {
                throw new Exception("Ошибка создания файла лога");
            }
            logFilePath = lofDirectoryPath + $"\\{deviceId}" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

    }
}
