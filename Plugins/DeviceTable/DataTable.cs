using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection.Metadata;


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
                    Dictionary<int, Parametr> mappedParametrs = await GetTableDataByBuffer(modbusID);
                    foreach (var item in mappedParametrs)
                    {
                        this.Parametrs.Where(parametr => parametr.adress == item.Key).Last().value = item.Value.value;
                        //еще нужен логгер
                    }

                }
                TableServer.readyToGetTableData = true;
            }

        }

        private async Task<Dictionary<int, Parametr>> GetTableDataByBuffer(int modbusID)
        {
            Dictionary<int, Parametr> mappedParametrs = new Dictionary<int, Parametr>();

            foreach (Parametr param in this.Parametrs)
            {
                if (param.size != 0)
                {
                    mappedParametrs.Add(param.adress, param);
                }
            }
            SortedDictionary<int, Parametr> sortedMappedParametrs = new SortedDictionary<int, Parametr>(mappedParametrs);

            List<int> adresses = new List<int>();
            Dictionary<int, string> rawValues = new Dictionary<int, string>();

            while (sortedMappedParametrs.Count > 0)
            {
                int startQueryAdress = sortedMappedParametrs.First().Key;
                int endQueryAdress = startQueryAdress;
                int lastParamSize = 0;

                foreach (var param in sortedMappedParametrs)
                {
                    if (param.Key + param.Value.size - startQueryAdress <= 246)
                    {
                        endQueryAdress = param.Key + param.Value.size;
                        lastParamSize = param.Value.size;
                    }
                    else
                    {
                        break;
                    }
                }

                string response = await GetValueByBufferAdressesMb(modbusID, startQueryAdress, endQueryAdress - startQueryAdress + lastParamSize);
                if (response != "Не получены данные от устройства")
                {
                    string[] ansValues = response.Split(' ');

                    for (int i = 0; i < ansValues.Length; i++)
                    {
                        int currentAdress = startQueryAdress + i;
                        if (sortedMappedParametrs.ContainsKey(currentAdress))
                        {
                            rawValues.Add(currentAdress, ansValues[i]);
                            sortedMappedParametrs.Remove(currentAdress);
                        }
                    }
                }
            }

            foreach (var item in rawValues)
            {
                mappedParametrs[item.Key].value = item.Value;
            }

            return mappedParametrs;
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
            string response = await Task.Run(async () =>
            {
                while (DateTime.Now - startTime < timeout)
                {
                    // Проверяем буфер сообщений на наличие ответа
                    string currentResponse = await CheckResponseBuffer();
                    if (!string.IsNullOrEmpty(currentResponse))
                    {
                        Console.WriteLine("TableMove^ " + currentResponse);
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

        private async Task<string> CheckResponseBuffer()
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
