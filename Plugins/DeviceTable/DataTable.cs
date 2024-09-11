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

        }

        public async Task GetTableDataAsync(string mode, int modbusID)     //Версия для Uint16 только value
        {
            for (int i = 0; i < columnSize - 1; i++)
            {
                tableDataValues[i] = await GetValueByAdressMbAsync(mode, modbusID, this.adreses[i]);
            }
        }

        private async Task<string> GetValueByAdressMbAsync(string mode, int modbusID, int adress)
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

        public async Task<string> WaitingResponseMb3Async() //Здесь мы ждем ответа от сервера подходящего под усл в течении 2 сек 
        {
            while (true) //заменить на tirElapsed
            {
                return "Функционал еще не разработан";
                //Нужно организовать буффер для сообщений примерно на 2 сек и искать по нему подходящие - это нужно делать в HandleAsinc
            }
            return "Нет данных";
        }

        public string[] GetTableDataValues()
        {
            return tableDataValues;
        }

    }
}
