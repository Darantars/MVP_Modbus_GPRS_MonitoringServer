using Microsoft.AspNetCore.SignalR;
using Read_Write_GPRS_Server.Protocols.Modbuss;
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
            // Сортировка параметров по адресам
            var sortedParametrs = this.Parametrs
                .Where(p => p.size != 0)
                .OrderBy(p => p.adress)
                .ToList();

            Dictionary<int, Parametr> mappedParametrs = sortedParametrs.ToDictionary(p => p.adress, p => p);
            Dictionary<int, string> rawValues = new Dictionary<int, string>();

            while (sortedParametrs.Count > 0)
            {
                int startQueryAdress = sortedParametrs.First().adress;
                int endQueryAdress = startQueryAdress;
                int querySize = 0;

                // Определение диапазона адресов для запроса
                foreach (var param in sortedParametrs)
                {
                    if (param.adress + param.size/2 - startQueryAdress <= 248)
                    {
                        endQueryAdress = param.adress + param.size/2;
                        querySize = endQueryAdress - startQueryAdress;
                    }
                    else
                    {
                        break;
                    }
                }

                string response = await GetValueByBufferAdressesMb(modbusID, startQueryAdress, querySize);
                if (response != "Не получены данные от устройства")
                {
                    string[] ansValues = response.Split(' ');

                    // Обработка ответа
                    for (int i = 0; i < ansValues.Length; i++)
                    {
                        int currentAdress = startQueryAdress + i;  
                        if (mappedParametrs.ContainsKey(currentAdress))
                        {
                            string value = "";
                            for (int j = 0; j < mappedParametrs[currentAdress].size; j++)
                            {
                                value += ansValues[(currentAdress - startQueryAdress) * 2 + j];
                            }
                            
                            rawValues.Add(currentAdress, value);
                        }
                    }


                    // Удаление обработанных параметров
                    sortedParametrs.RemoveAll(p => p.adress >= startQueryAdress && p.adress < endQueryAdress);
                }
            }

            // Обновление значений параметров
            foreach (var item in rawValues)
            {
                ModBussRTU modBussRTU = new ModBussRTU();
                string value = await modBussRTU.DecodeModbusPayload(ConvertHexStringToByteArray(item.Value.Split(' ')), mappedParametrs[item.Key].format);

                mappedParametrs[item.Key].value = value;
            }

            return mappedParametrs;
        }


        private byte[] ConvertHexStringToByteArray(string[] hexStrings)
        {
            List<byte> byteList = new List<byte>();
            foreach (var hexString in hexStrings)
            {
                byteList.AddRange(StringToByteArray(hexString));
            }
            return byteList.ToArray();
        }

        private byte[] StringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
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
            await this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, size);  //Нужно добавить режим answera в TcpConnection
            Console.WriteLine($"Отправлена команда 3: {modbusID} {adress} {size}");
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
