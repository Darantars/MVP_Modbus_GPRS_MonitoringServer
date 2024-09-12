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

        }

        public async Task GetTableDataAsync(string mode, int modbusID)     //Версия для Uint16 только value
        {
            for (int i = 0; i < columnSize; i++)
            {
                tableDataValues[i] = await GetValueByAdressMb(mode, modbusID, this.adreses[i]);
            }
        }

        private async Task<string> GetValueByAdressMb(string mode, int modbusID, int adress)
        {
            if (mode == "default")
            {
                this.TableServer.SendMB3CommandToDevice(TableServer.device, modbusID, adress, 1);
                return await WaitingResponseMb3Async();

            }
            else
            {
                return "Функционал еще не разработан";
            }
        }

        public  async Task<string> WaitingResponseMb3Async() //Здесь мы ждем ответа от сервера подходящего под усл в течении 2 сек 
        {
                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromSeconds(10);

                while (DateTime.Now - startTime < timeout)
                {
                    // Проверяем буфер сообщений на наличие ответа
                    string response = CheckResponseBuffer();
                    if (!string.IsNullOrEmpty(response))
                    {
                        badRequestMb3Counter = 0; // Обнуляем счетчик неудачных запросов
                        return response;
                    }

                    await Task.Delay(100); // Ждем 100 мс перед следующей проверкой
                }

                badRequestMb3Counter++;
                if (badRequestMb3Counter >= 3)
                {
                    await TableServer.Stop();
                    badRequestMb3Counter = 0;
                    return "Сервер остановлен из-за превышения лимита неудачных запросов";
                }

                return "Не получены данные от устройства";  
        }

        private string CheckResponseBuffer()
        {

                     return null;  //  ******      Нужно изменить       *******
        }

        public string[] GetTableDataValues()
        {
            return tableDataValues;
        }

    }
}
