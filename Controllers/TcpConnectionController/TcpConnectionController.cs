using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;

namespace Read_Write_GPRS_Server.Controllers
{
    public class TcpConnectionController : ControllerBase
    {
        public class ModbusInput
        {
            public string ModbusReadID { get; set; }
            public string ModbusReadColumnNumber { get; set; }

        }
        public class TcpServer
        {
            private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
            private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            public string messageLog {  get; set; }
            private TcpClient client { get; set; }

            private TcpListener server { get; set; }

            public TcpServer()
                {
                    Task.Run(ProcessQueueAsync);
                }

                public async Task Start(string ipAddress, int port)
                {
                    
                    messageLog = "Loading...";

                    try
                    {
                        server = new TcpListener(IPAddress.Any, port);

                        // Начинаем прослушивание входящих соединений
                        server.Start();
                        messageLog = $"TCP-сервер запущен на {ipAddress}:{port}";

                        while (true)
                        {
                            client = await server.AcceptTcpClientAsync();
                            await AddMessageToQueue("Подключено новое соединение.");

                            // Обрабатываем клиента в отдельной задаче
                            _ = Task.Run(() => HandleClientAsync(client));
                        }
                    }
                    catch (Exception ex)
                    {
                        await AddMessageToQueue($"Ошибка: {ex.Message}");
                    }
                }

            public async Task Stop()
            {
                _cancellationTokenSource.Cancel();

                if (server != null)
                {
                    server.Stop();
                    messageLog = "TCP-сервер остановлен.";
                }

                if (client != null)
                {
                    client.Close();
                    messageLog = "Клиентское соединение закрыто.";
                }

                await AddMessageToQueue("Сервер и клиентские соединения закрыты.");
            }

            public async Task SendMessgeToDeviceASCII(string message)
                {
                    if (client == null || !client.Connected)
                    {
                        await AddMessageToQueue("No client. Pleasr, await client");
                        return;
                    }

                    try
                    {
                        NetworkStream stream = client.GetStream();
                        string response = message;
                        byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        await AddMessageToQueue($"Отправлено сообщение: {response}");
                    }
                    catch (Exception ex)
                    {
                        await AddMessageToQueue($"Sending error: {ex.Message}");
                    }
                }

                public async Task SendMB3CommandToDevice(int deviceId, int address, int quantity)
                {
                    if (client == null || !client.Connected)
                    {
                        await AddMessageToQueue("No client. Pleasr, await client");
                        return;
                    }

                    try
                    {
                        NetworkStream stream = client.GetStream();
                        byte[] responseBytes = GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
                    string command = BitConverter.ToString(responseBytes); 
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        await AddMessageToQueue($"Отправлено сообщение: Отправлена команда MB4  для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
                    }
                    catch (Exception ex)
                    {
                        await AddMessageToQueue($"Sending error: {ex.Message}");
                    }
                }


                private async Task HandleClientAsync(TcpClient client)
                {
                    try
                    {
                        using (NetworkStream stream = client.GetStream())
                        {
                            byte[] buffer = new byte[1024];
                            int bytesRead;

                            // Читаем данные от клиента
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                                string decodedMbCommand = DecodeModbusMessage(buffer);
                            
                                string hexedNessage = BitConverter.ToString(buffer, 0, bytesRead);

                                await AddMessageToQueue($"Получено сообщение: <br> ASCII: {message}<br>MB: {decodedMbCommand} <br>hex:{hexedNessage}");

                                string response = "Message accepted.";
                                byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                                await AddMessageToQueue($"Отправлено сообщение: {response}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await AddMessageToQueue($"Client error: {ex.Message}");
                    }
                    
                }



            private async Task AddMessageToQueue(string message)
                {
                    _messageQueue.Enqueue(message);
                    _semaphore.Release();
                }

                private async Task ProcessQueueAsync()
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await _semaphore.WaitAsync(_cancellationTokenSource.Token);

                        if (_messageQueue.TryDequeue(out string message))
                        {
                            Console.WriteLine(message);

                            if(messageLog == "Loading...")
                            {
                                messageLog =  "<br>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + "<br>";
                            }
                            messageLog =  "<br>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + "<br>" + messageLog;
                        }
                    }
                }


                public static string DecodeModbusMessage(byte[] buffer)
                {
                    if (buffer.Length < 2)
                    {
                        return "Недостаточная длина команды";
                    }

                    byte modbusId = buffer[0];
                    byte functionCode = buffer[1];
                    int bytesRead = buffer.Length;

                    string messageType = "";
                    string registers = "";
                    ushort startAddress;
                    ushort quantity;
                    byte byteCount;


                    switch (functionCode)
                    {
                        case 3: // Read Holding Registers
                            if (bytesRead < 5)
                            {
                                return "Недостаточная длина команды для чтения регистров";
                            }

                            // Определение типа сообщения
                            if (bytesRead == 8)
                            {
                                messageType = "Запрос";
                                startAddress = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToUInt16(buffer, 2));
                                quantity = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToUInt16(buffer, 4));
                                registers = $"{startAddress}-{startAddress + quantity - 1}";
                            }
                            else
                            {
                                messageType = "Ответ";
                                byteCount = buffer[2]; // Количество байт данных
                                if (bytesRead < 3 + byteCount)
                                {
                                    return "Недостаточная длина команды для чтения регистров";
                                }

                                // Извлечение данных регистров для функции 3
                                StringBuilder data = new StringBuilder();
                                for (int i = 0; i < byteCount / 2; i++)
                                {
                                    ushort value = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 3 + i * 2));
                                data.Append($"{value} ");
                                }
                                registers = $"Данные: {data.ToString().Trim()}";
                            }
                            break;

                        case 10: // Write Multiple Holding Registers
                            if (bytesRead < 7)
                            {
                                return "Недостаточная длина команды для записи нескольких регистров";
                            }
                            startAddress = BitConverter.ToUInt16(buffer, 2);
                            quantity = BitConverter.ToUInt16(buffer, 4);
                            byteCount = buffer[6];
                            registers = $"{startAddress}-{startAddress + quantity - 1}";

                            // Извлечение данных регистров для функции 10
                            if (bytesRead >= 7 + byteCount)
                            {
                                StringBuilder data = new StringBuilder();
                                for (int i = 0; i < byteCount / 2; i++)
                                {
                                    ushort value = BitConverter.ToUInt16(buffer, 7 + i * 2);
                                    data.Append($"{value} ");
                                }
                                registers += $" (Данные: {data.ToString().Trim()})";
                            }
                            break;

                        default:
                            return "Неподдерживаемая функция Modbus";
                    }

                    return $"{messageType}: команда {functionCode} для устройства ID {modbusId} {registers}";
                }
        }


        public class TcpDeviceTable
        {
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly ConcurrentQueue<TaskCompletionSource<string>> _requestQueue = new ConcurrentQueue<TaskCompletionSource<string>>();
            private readonly ConcurrentQueue<string> _responseQueue = new ConcurrentQueue<string>();
            private readonly SemaphoreSlim getParamMb3Semaphore = new SemaphoreSlim(1, 1);

            TcpClient client { get; set; }
            bool serverRuning { get; set; }
            TcpListener server { get; set; }

            public string mbValue { get; set; }

            public string connectionStatus {  get; set; }

            public TcpDeviceTable() { 
                connectionStatus = "Disconnected";
                serverRuning = false;
            }

            public async Task Start(string ipAddress, int port)
            {
                Console.WriteLine("TryingStartTable");
                try
                {
                    server = new TcpListener(IPAddress.Any, port);

                    // Начинаем прослушивание входящих соединений
                    server.Start();
                    Console.WriteLine($"TCP-сервер связи с устройством запущен на {ipAddress}:{port}");
                    connectionStatus = $"Started on {ipAddress}:{port}, waiting for clients...";
                    while (true)
                    {
                        client = await server.AcceptTcpClientAsync();
                        Console.WriteLine("Подключено новое соединение.");
                        connectionStatus = "Connected";
                        serverRuning = true;
                        //Обрабатываем клиента в отдельной задаче
                        _ = Task.Run(() => HandleClientAnswerMb3Async(client));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
                Console.WriteLine("StartTable");
            }

            public async Task<string> GetMb3ParamValueAsync(int deviceId, int address, int quantity)
            {
                await getParamMb3Semaphore.WaitAsync();
                try
                {
                    if (serverRuning)
                    {
                        var tcs = new TaskCompletionSource<string>();
                        _requestQueue.Enqueue(tcs);
                        await SendMB3CommandToDeviceAsync(deviceId, address, quantity);
                        return await tcs.Task;
                    }
                    return "Await client";
                }
                finally
                {
                    getParamMb3Semaphore.Release();
                }
            }

            public async Task<string> SendMB3CommandToDeviceAsync(int deviceId, int address, int quantity)
            {
                if (client == null || !client.Connected)
                {
                    mbValue = "Loading...";
                    return mbValue;
                }

                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] responseBytes = GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"Отправлено сообщение: Отправлена команда MB4 для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
                    return "Request sent";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sending error: {ex.Message}");
                    return "no data";
                }
            }

            private async Task HandleClientAnswerMb3Async(TcpClient client)
            {
                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        // Читаем данные от клиента
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            string response = DecodeModbusMessageValueMb3(buffer);
                            if (response != null && response != "no data")
                            {
                                _responseQueue.Enqueue(response);
                                Console.WriteLine($"Поймано {response}");

                                if (_requestQueue.TryDequeue(out var tcs))
                                {
                                    tcs.SetResult(response);
                                }
                            }
                            //if (responseTimeoutHasElapsed) 
                            //{
                                //response = "Timeout has ellapsed";
                                //_responseQueue.Enqueue(response);
                                //Console.WriteLine($"Timeout has ellapsed");

                                //if (_requestQueue.TryDequeue(out var tcs))
                                //{
                                //    tcs.SetResult(response);
                                //}
                            //}
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Client error: {ex.Message}");
                }
            }

            public async Task Stop()
            {
                Console.WriteLine("TryingStopTable");
                _cancellationTokenSource.Cancel();

                if (server != null)
                {
                    server.Stop();
                    Console.WriteLine("TCP-сервер остановлен.");
                }

                if (client != null)
                {
                    client.Close();
                    Console.WriteLine("Клиентское соединение закрыто.");
                }
                serverRuning = false;
                connectionStatus = "Disconnected";
                Console.WriteLine("Сервер и клиентские соединения закрыты.");
            }

            public static string DecodeModbusMessageValueMb3(byte[] buffer)
            {
                if (buffer.Length < 2)
                {
                    return "Недостаточная длина команды";
                }

                byte modbusId = buffer[0];
                byte functionCode = buffer[1];
                int bytesRead = buffer.Length;

                string registers = "";
                ushort startAddress;
                ushort quantity;
                byte byteCount;

                if (functionCode == 3)
                {
                    if (bytesRead < 5)
                    {
                        return "Недостаточная длина команды для чтения регистров";
                    }

                    // Определение типа сообщения
                    if (bytesRead == 8)
                    {
                        startAddress = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 2));
                        quantity = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 4));
                        registers = $"{startAddress}-{startAddress + quantity - 1}";
                    }
                    else
                    {
                        byteCount = buffer[2]; // Количество байт данных
                        if (bytesRead < 3 + byteCount)
                        {
                            return "Недостаточная длина команды для чтения регистров";
                        }

                        // Извлечение данных регистров для функции 3
                        StringBuilder data = new StringBuilder();
                        for (int i = 0; i < byteCount / 2; i++)
                        {
                            ushort value = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 3 + i * 2));
                            data.Append($"{value} ");
                        }
                        registers = $"Данные: {data.ToString().Trim()}";
                    }
                    return registers;
                }
                else return "no data";
            }
        }

        public static byte[] GenerateReadHoldingRegistersCommand(int modbusId, int startAddress, int quantity)
        {
            // Создаем буфер для команды
            byte[] command = new byte[8];

            // Заполняем буфер данными
            command[0] = (byte)modbusId; // ID устройства
            command[1] = 0x03; // Функция 3 (чтение нескольких регистров)
            command[2] = (byte)(startAddress >> 8); // Старший байт начального адреса регистра
            command[3] = (byte)(startAddress & 0xFF); // Младший байт начального адреса регистра
            command[4] = (byte)(quantity >> 8); // Старший байт количества регистров
            command[5] = (byte)(quantity & 0xFF); // Младший байт количества регистров

            // Вычисляем контрольную сумму (CRC)
            ushort crc = CalculateCRC(command, 6);
            command[6] = (byte)(crc & 0xFF); // Младший байт CRC
            command[7] = (byte)(crc >> 8); // Старший байт CRC

            return command;
        }

        private static ushort CalculateCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }
}
    
