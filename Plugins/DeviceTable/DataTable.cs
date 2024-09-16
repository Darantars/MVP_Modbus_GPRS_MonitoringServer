using Microsoft.AspNetCore.SignalR;
using System.Drawing;

namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class DataTable
    {
        private int rowSize {  get; set; }
        private int columnSize { get; set; }

        private string[] headers {  get; set; }

        private string[] defHeaders = new string[] {"Имя", "Значение", "Ед. измер", "Адрес(dec)", "Формат", "Вид", "Размер", "Запись", "Минимум", "Максимум", "Заводские"};

        private string[] tableDataValues {  get; set; } 

        private int[] adreses { get; set; }

        private Controllers.TcpConnectionController.TcpDeviceTableServer TableServer {  get; set; }

        public string answerMb3 { get; set; }    // Вот в нее нужно положить последний ответ

        public bool readyToGetTableData { get; set; }

        private int badRequestMb3Counter {  get; set; }

        public DataTable( int tableRowSize, int tableColumnSize, int[] tableAdreses, Controllers.TcpConnectionController.TcpDeviceTableServer tableTableServer) 
        {
            if (tableRowSize < 0 )
            {
                throw new ArgumentException("invalid DataSet for Table");
            }

            TableServer = tableTableServer;
            rowSize = tableRowSize;
            columnSize = tableColumnSize;
            headers = new string[tableRowSize];
            adreses = tableAdreses;
            tableDataValues = new string[tableColumnSize];
            badRequestMb3Counter = 0;
            readyToGetTableData = true;

        }

        public async Task GetTableDataAsync(string mode, int modbusID)     //Версия для Uint16 только value
        {
            if (readyToGetTableData)
            {
                readyToGetTableData = false;
                for (int i = 0; i < columnSize; i++)
                {
                    tableDataValues[i] = await GetValueByAdressMb(mode, modbusID, this.adreses[i]);
                }
                readyToGetTableData = true;
            }

        }

        private async Task<string> GetValueByAdressMb(string mode, int modbusID, int adress)
        {

            if (mode == "default")
            {
                await this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, 1);
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
                        answerMb3 = null;
                        badRequestMb3Counter = 0; // Обнуляем счетчик неудачных запросов
                        return currentResponse;
                    }
                }

                badRequestMb3Counter++;
                //if (badRequestMb3Counter >= 3)
                //{
                //    TableServer.Stop().Wait();
                //    badRequestMb3Counter = 0;
                //    return "Сервер остановлен из-за превышения лимита неудачных запросов";
                //}

                return "Не получены данные от устройства";
            });

            return response;
        }

        private string CheckResponseBuffer()
        {
            if (answerMb3 != null)
                return answerMb3;
            else
                return null;
        }

        public string[] GetTableDataValues()
        {
            return tableDataValues;
        }

    }
}
