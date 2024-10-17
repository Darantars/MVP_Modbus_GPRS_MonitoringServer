using Microsoft.AspNetCore.SignalR;
using System.Drawing;


namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class DataTable
    {
        public string id { get; set; }
        private int rowSize { get; set; }
        private int columnSize { get; set; }

        private string[] headers { get; set; }

        private string[] defHeaders = new string[] { "Имя", "Значение", "Ед. измер", "Адрес(dec)", "Формат", "Вид", "Размер", "Запись", "Минимум", "Максимум", "Заводские" };

        public List<Parametr> Parametrs { get; set; }

        private Controllers.TcpConnectionController.TcpDeviceTableServer TableServer { get; set; }

        private int badRequestMb3Counter { get; set; }

        private DeviceLogger logger { get; set; }
        public DataTable(string tableId, int tableRowSize, int tableColumnSize, List<string> tableParamNames, List<int> tableParamAdreses, List<int> tableParamSizes, List<string> tableParamTypes, List<string> tableParamUnitTypes, List<string> tableParamFormats, List<int> tableParamcoiffients, Controllers.TcpConnectionController.TcpDeviceTableServer tableTableServer)
        {
            if (tableRowSize < 0)
            {
                throw new ArgumentException("invalid DataSet for Table");
            }

            id = tableId;
            TableServer = tableTableServer;
            rowSize = tableRowSize;
            columnSize = tableColumnSize;
            headers = new string[tableRowSize];
            Parametrs = new List<Parametr>();
            for (int i = 0; i < tableColumnSize; i++)
            {
                Parametrs.Add(new Parametr(
                tableParamNames[i],
                "Не определено",
                tableParamAdreses[i],
                tableParamSizes[i],
                tableParamTypes[i],
                tableParamUnitTypes[i],
                tableParamFormats[i],
                tableParamcoiffients[i]
                ));
            }

            badRequestMb3Counter = 0;
            logger = new DeviceLogger(tableId);
        }

        public async Task GetTableDataAsync(string mode, int modbusID)
        {
            if (TableServer.readyToGetTableData)
            {
                TableServer.readyToGetTableData = false;
                if (mode == "default")
                {
                    for (int i = 0; i < columnSize; i++)
                    {
                        Parametrs[i].value = await GetValueByAdressMb(modbusID, this.Parametrs[i].adress, this.Parametrs[i].size, this.Parametrs[i].format);
                        await logger.LogParamChangeAsync(DateTime.Now, Parametrs[i].name, Parametrs[i].value);
                    }
                }
                else if (mode == "buffer")
                {
                    try
                    {
                        List<int> sortedAdresess = (Parametrs.Select(param => param.adress).ToList()).OrderBy(a => a).ToList();
                        if (sortedAdresess[sortedAdresess.Count - 1] - sortedAdresess[2] < 50)
                        {
                            int maxAdress = sortedAdresess[sortedAdresess.Count - 1];
                            int minAdress = sortedAdresess[2];
                            int quanity = (maxAdress - minAdress) / 2;
                            // Здесь нужен метод запроса плюс дешифровки и return
                            string[] values = (await GetValueByBufferAdressesMb(modbusID, minAdress, quanity)).Split(' ');
                            foreach (string value in values)
                            {
                                System.Console.WriteLine(value);
                            }
                        }

                        for (int i = 0; i < columnSize; i++)
                        {
                            while(sortedAdresess[i] == 0 && sortedAdresess[i + 1] == 0)
                            {
                                i++;
                                i++;
                            }
                            int startAdresses = sortedAdresess[i];

                            while (sortedAdresess[i + 1] - sortedAdresess[0] <= 2 && i < columnSize)
                            {
                                i++;
                            }
                            int maxAdress = sortedAdresess[i];
                            int quanity = (maxAdress - startAdresses) / 2;

                            string[] values = (await GetValueByBufferAdressesMb(modbusID, startAdresses, quanity)).Split(' ');
                            // Здесь нужен метод дешифровки и return
                            foreach (string value in values)
                            {
                                System.Console.WriteLine(value);
                            }

                        }
                    }
                    catch
                    {

                    }
                   
                }
                TableServer.readyToGetTableData = true;
            }

        }

        private async Task<string> GetValueByAdressMb(int modbusID, int adress, int size, string format)
        {
            if (size == 0)
            {
                return "null";
            }
            this.TableServer.answerFormat = format;
            await this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, size / 2);
            return await WaitingResponseMb3Async();
        }

        private async Task<string> GetValueByBufferAdressesMb(int modbusID, int adress, int size)
        {
            await this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, size / 2);  //Нужно добавить режим answera в TcpConnection
            Console.WriteLine($"Отправлена команда 3: {modbusID} {adress} {size / 2}");
            return await WaitingResponseMb3Async(); //Нужно добавить режим answera в TcpConnection
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
            return Parametrs.Select(param => param.value).ToArray();
        }

        public async Task<List<(DateTime date, string value)>> GetParameterValuesLast3Hours(string parameterName)
        {
            return await logger.GetParameterValuesLast3Hours(parameterName);
        }

    }
}
