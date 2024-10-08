using Microsoft.AspNetCore.SignalR;
using System.Drawing;


namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class DataTable
    {
        public string id {  get; set; }
        private int rowSize {  get; set; }
        private int columnSize { get; set; }

        private string[] headers {  get; set; }

        private string[] defHeaders = new string[] {"Имя", "Значение", "Ед. измер", "Адрес(dec)", "Формат", "Вид", "Размер", "Запись", "Минимум", "Максимум", "Заводские"};

        public List<string> paramNames { get; set; }

        public string[] tableDataValues {  get; set; }

        public List<int> paramAdreses { get; set; }

        public List<int> paramcoiffients { get; set; }

        public List<int> paramSizes { get; set; }

        public List<string> paramTypes { get; set; }

        public List<string> paramUnitTypes { get; set; }

        public List<string> paramFormats { get; set; }

        private Controllers.TcpConnectionController.TcpDeviceTableServer TableServer {  get; set; }

        private int badRequestMb3Counter {  get; set; }

        private DeviceLogger logger { get; set; }
        public DataTable(string tableId, int tableRowSize, int tableColumnSize, List<string> tableParamNames, List<int> tableParamAdreses, List<int> tableParamSizes, List<string> tableParamTypes, List<string> tableParamUnitTypes, List<string> tableParamFormats, List<int> tableParamcoiffients , Controllers.TcpConnectionController.TcpDeviceTableServer tableTableServer) 
        {
            if (tableRowSize < 0 )
            {
                throw new ArgumentException("invalid DataSet for Table");
            }

            id = tableId;
            TableServer = tableTableServer;
            rowSize = tableRowSize;
            columnSize = tableColumnSize;
            headers = new string[tableRowSize];
            paramNames = tableParamNames;
            paramAdreses = tableParamAdreses;
            paramSizes = tableParamSizes;
            paramTypes = tableParamTypes;
            paramUnitTypes = tableParamUnitTypes;
            paramFormats = tableParamFormats;
            paramcoiffients = tableParamcoiffients;
            tableDataValues = new string[tableColumnSize];
            badRequestMb3Counter = 0;
            logger = new DeviceLogger(tableId);
        }

        public async Task GetTableDataAsync(string mode, int modbusID)   
        {
            if (TableServer.readyToGetTableData)
            {
                TableServer.readyToGetTableData = false;
                for (int i = 0; i < columnSize; i++)
                {
                    tableDataValues[i] = await GetValueByAdressMb(mode, modbusID, this.paramAdreses[i], this.paramSizes[i], this.paramFormats[i]);
                    await logger.LogParamChangeAsync(DateTime.Now, paramNames[i], tableDataValues[i]);
                }
                TableServer.readyToGetTableData = true;
            }

        }

        private async Task<string> GetValueByAdressMb(string mode, int modbusID, int adress, int size, string format)
        {
            if(size == 0)
            {
                return "null";
            }

            if (mode == "default")
            {
                this.TableServer.answerFormat = format;
                await this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, size / 2);
                return await WaitingResponseMb3Async();
            }
            else
            {
                return "Функционал еще не разработан";
            }
        }

        public async Task<string> WaitingResponseMb3Async()
        {
            DateTime startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            // Запускаем цикл while в фоновом потоке
            string response = await Task.Run(() =>
            {
                while (DateTime.Now - startTime < timeout)
                {
                    // Проверяем буфер сообщений на наличие ответа
                    string currentResponse = CheckResponseBuffer();
                    if (!string.IsNullOrEmpty(currentResponse))
                    {
                        TableServer.answerMb3 = null;
                        badRequestMb3Counter = 0; // Обнуляем счетчик неудачных запросов
                        return currentResponse;
                    }
                }

                badRequestMb3Counter++;

                return "Не получены данные от устройства";
            });

            return response;
        }

        private string CheckResponseBuffer()
        {
            if (TableServer.answerMb3 != null)
                return TableServer.answerMb3;
            else
                return null;
        }

        public string[] GetTableDataValues()
        {
            return tableDataValues;
        }

    }
}
