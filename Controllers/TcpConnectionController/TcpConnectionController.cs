using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.AspNetCore.Http;
using Read_Write_GPRS_Server.TcpDevice;
using Microsoft.JSInterop;
using System.Data;

namespace Read_Write_GPRS_Server.Controllers
{
    public class TcpConnectionController : ControllerBase
    {
        public class ModbusInput
        {
            public string modbusID { get; set; }
            public string modbusStartAdress { get; set; }
            public string modbussQuanity { get; set; }
            public string modbussData { get; set; }
        }


        public class TcpServer
        {
            private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>();

            private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            public string messageLog { get; set; }

            private int messageCounter { get; set; }

            public int messageBufferSize { get; set; }

            public UsrGPRS232_730 device;

            private TcpListener server { get; set; }

            private bool isRunning = false;

            private string lastIpAdress { get; set; }
            private int lastPort { get; set; }

            public TcpServer(int messageBufferSize)
            {
                this.messageBufferSize = messageBufferSize;
                Task.Run(ProcessQueueAsync);
            }

            public async Task Start(string ipAddress, int port)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                if (isRunning)
                {
                    await AddMessageToQueue("Сервер уже запущен.");
                    return;
                }

                try
                {
                    server = new TcpListener(IPAddress.Any, port);

                    // Начинаем прослушивание входящих соединений
                    server.Start();
                    await AddMessageToQueue($"TCP-сервер запущен на {ipAddress}:{port}");

                    lastIpAdress = ipAddress;
                    lastPort = port;

                    device = new TcpDevice.UsrGPRS232_730("GPRS Online", 1); //потом заменить на задание со View
                    device.tcpConnectionStatus = "Connecting...";
                    isRunning = true;
                    await AddMessageToQueue("Сервер развернут. Ожидание подключения устройства...");

                    while (true)
                    {
                        device.tcpClient = await server.AcceptTcpClientAsync();
                        await AddMessageToQueue("Подключено новое соединение.");
                        lock (device.connectinLocker)
                        {
                            _ = Task.Run(() => StartCheckConnectionToDeviceLoop(device));
                        }

                        // Обрабатываем клиента в отдельной задаче
                        _ = Task.Run(() => HandleClientAsync(device));
                    }
                }
                catch (Exception ex)
                {
                    await AddMessageToQueue($"Ошибка: {ex.Message}");
                }
            }

            public async Task Stop()
            {
                if (!isRunning)
                {
                    await AddMessageToQueue("Сервер уже остановлен.");
                    return;
                }

                if (server != null)
                {
                    server.Stop();
                    await AddMessageToQueue("TCP-сервер остановлен.");
                }

                if (device.tcpClient != null)
                {
                    device.tcpClient.Close();
                    await AddMessageToQueue("Клиентское соединение закрыто.");
                }

                isRunning = false;
                await AddMessageToQueue("Сервер и клиентские соединения закрыты.");
            }

            private async Task StartCheckConnectionToDeviceLoop(UsrGPRS232_730 device)              ////T0 DO: Таска была открыта, таску нужно закрыть
            {
                double delayFiveHeartBeatReal = device.heartbeatMessageRateSec * 2;
                int loopCounter = 1;
                int[] heartBeatAtLoop = new int[5];
                int lostConnectionCounter = 0;

                while (isRunning)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    heartBeatAtLoop[loopCounter - 1] = device.tcp5HeartBeatTimingMessageCounter;
                    int missedPockets = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (heartBeatAtLoop[i] < i + 1)
                            missedPockets++;
                    }
                    switch (missedPockets)
                    {
                        case 5:
                            if (device.tcpClient != null)
                                device.tcpConnectionStatus = "Bad connection 0% package recived";
                            await AddMessageToQueue("Bad connection 0% package recived");
                            lostConnectionCounter++;
                            break;
                        case 4:
                            device.tcpConnectionStatus = "Bad connection <= 20% package recived";
                            await AddMessageToQueue("Bad connection <= 20% package recived");
                            lostConnectionCounter++;
                            break;
                        case 3:
                            device.tcpConnectionStatus = "Bad connection <= 40% package recived";
                            await AddMessageToQueue("Bad connection <= 40% package recived");
                            break;
                        case 2:
                            device.tcpConnectionStatus = "Unstable connection <= 60% package recived";
                            await AddMessageToQueue("Unstable connection <= 60% package recived");
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        case 1:
                            device.tcpConnectionStatus = "Connected ~ 80% package recived";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        case 0:
                            device.tcpConnectionStatus = "Fast connection 100% package recived";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        default:
                            device.tcpConnectionStatus = "Connection supergood!";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                    }

                    if (lostConnectionCounter >= 5)
                    {
                        await Stop();
                        await AddMessageToQueue("В связи с нестабильным соединением осуществляется переподключение");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        await Start(lastIpAdress, lastPort);
                        lostConnectionCounter = 0;
                    }


                    if (loopCounter == 5)
                    {
                        loopCounter = 1;
                        device.tcp5HeartBeatTimingMessageCounter = 0;
                    }
                    else
                        loopCounter = loopCounter + 1;

                }
            }

            public string GetDeviceConnectionStatus()
            {
                if (!isRunning)
                    return "Server is not running";
                if (device != null)
                    return device.tcpConnectionStatus;
                else
                    return "Not started yet";
            }

            public async Task SendMessgeToDeviceASCII(string message)
            {
                if (device.tcpClient == null || !device.tcpClient.Connected)
                {
                    await AddMessageToQueue("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = device.tcpClient.GetStream();
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

            public async Task SendMB3CommandToDevice(UsrGPRS232_730 device, int deviceId, int address, int quantity)
            {
                if (device == null) return;
                if (device.tcpClient == null || !device.tcpClient.Connected)
                {
                    await AddMessageToQueue("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = device.tcpClient.GetStream();
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await AddMessageToQueue($"Отправлено сообщение: Отправлена команда MB3  для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
                }
                catch (Exception ex)
                {
                    await AddMessageToQueue($"Sending error: {ex.Message}");
                }
            }

            public async Task SendMB10CommandToDevice(UsrGPRS232_730 device, int deviceId, int address, int quantity, byte[] byteData)
            {
                if (device == null) return;
                if (device.tcpClient == null || !device.tcpClient.Connected)
                {
                    await AddMessageToQueue("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = device.tcpClient.GetStream();
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateWriteMultipleRegistersCommand(deviceId, address, quantity, quantity * 2, byteData);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await AddMessageToQueue($"Отправлено сообщение: Отправлена команда MB10  для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
                }
                catch (Exception ex)
                {
                    await AddMessageToQueue($"Sending error: {ex.Message}");
                }
            }

            private async Task HandleClientAsync(UsrGPRS232_730 device)
            {
                TcpClient client = device.tcpClient;
                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        // Читаем данные от клиента
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            device.tcp5HeartBeatTimingMessageCounter = device.tcp5HeartBeatTimingMessageCounter + 1;
                            byte[] newBuffer = new byte[bytesRead];
                            Array.Copy(buffer, newBuffer, bytesRead);
                            await Task.Run(async () =>
                            {
                                List<byte[]> responseList = await Task.Run(() => Protocols.Modbuss.ModBussRTU.CutToModbusRtuMessageListFastMb(newBuffer));

                                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                                string decodedMbCommand = "";

                                for (int i = 0; i < responseList.Count; i++)
                                {
                                    decodedMbCommand = decodedMbCommand + "<br>"+ Protocols.Modbuss.ModBussRTU.DecodeModbusMessage(responseList[i], "getTypeAndAdress");
                                }

                                string hexedNessage = BitConverter.ToString(buffer, 0, bytesRead);

                                string cuttedMessageMB = "";
                                for (int i = 0; i < responseList.Count; i++)
                                {
                                    cuttedMessageMB = cuttedMessageMB + "<br>"+ BitConverter.ToString(responseList[i]);
                                }

                                await AddMessageToQueue($"<br> Получено сообщение: <br> ASCII: {message} <br> MB: {decodedMbCommand} <br> hex: {hexedNessage} <br> hex-commands(probably): {cuttedMessageMB}");
                            });
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
                _messageQueue.Add(message);
                Console.WriteLine(message);
                messageCounter++;
            }

            private async Task ProcessQueueAsync()
            {
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        string message = _messageQueue.Take(_cancellationTokenSource.Token);

                        Console.WriteLine(message);

                        if (messageLog == "Loading...")
                        {
                            messageLog = "<br>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + "<br>";
                        }
                        messageLog = "<br>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + "<br>" + messageLog;

                        // Проверка счетчика сообщений
                        if (messageCounter >= messageBufferSize)
                        {
                            await SaveMessageLogToFile();
                            messageLog = $"Лог предыдущих сообщений сохранен в файл messageLog_{DateTime.Now:yyyyMMddHHmm}.txt";
                            messageCounter = 0;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Обработка отмены задачи
                    Console.WriteLine($"Сообщение отменено");
                }
                catch (Exception ex)
                {
                    // Обработка других исключений
                    Console.WriteLine($"Error in ProcessQueueAsync: {ex.Message}");
                }
            }

            private async Task SaveMessageLogToFile()
            {
                try
                {
                    string _fileText = messageLog.Replace("<br>", "\n");
                    string _fileName = $"messageLog_{DateTime.Now:yyyyMMddHHmm}.txt";
                    await System.IO.File.WriteAllTextAsync(_fileName, _fileText);
                    Console.WriteLine($"Сообщения сохранены в файл: {_fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при сохранении сообщений в файл: {ex.Message}");
                }
            }
        }


        public class TcpDeviceTableServer
        {
            public bool isRunning { get; set; }
            private TcpListener server;

            public UsrGPRS232_730 device;

            public string connectionStatus { get; set; }

            public List<Read_Write_GPRS_Server.Plugins.DeviceTable.DataTable> dataTablesList { get; set; }

            private CancellationTokenSource _cts = new CancellationTokenSource();
            private string lastIpAdress { get; set; }
            private int lastPort { get; set; }
            public bool readyToGetTableData { get; set; }

            private object tableAdiingLock { get; set; }
            public string answerFormat { get; set; }
            public string answerMb3 { get; set; }

            public string readingMode { get; set; }

            public TcpDeviceTableServer()
            {
                connectionStatus = "Disconnected";
                isRunning = false;
                dataTablesList = new List<Read_Write_GPRS_Server.Plugins.DeviceTable.DataTable>();
                readyToGetTableData = true;
                tableAdiingLock = new CancellationToken();
                answerMb3 = null;
                answerFormat = "int16";
                readingMode = "buffer";
            }

            public async Task AddNewTable(string id, int tableRowSize, int tableColumnSize, List<string> tableNames, List<int> tableAdreses, List<int> tableSizes, List<string> tableTypes, List<string> tableParamUnitTypes, List<string> TableFormats, List<int> tableCoificent)
            {
                lock (tableAdiingLock)
                {
                    Read_Write_GPRS_Server.Plugins.DeviceTable.DataTable dataTable = new Plugins.DeviceTable.DataTable(id, tableRowSize, tableColumnSize, tableNames, tableAdreses, tableSizes, tableTypes, tableParamUnitTypes, TableFormats, tableCoificent, this);
                    dataTablesList.Add(dataTable);
                }
            }

            public async Task Start(string ipAddress, int port)
            {
                if (isRunning)
                {
                    Console.WriteLine("Сервер уже запущен.");
                    return;
                }

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    _cts = new CancellationTokenSource();
                    // Начинаем прослушивание входящих соединений
                    server.Start();
                    Console.WriteLine($"TCP-сервер связи с устройством запущен на {ipAddress}:{port}");
                    connectionStatus = $"Started on {ipAddress}:{port}, waiting for clients...";

                    lastIpAdress = ipAddress;
                    lastPort = port;

                    device = new UsrGPRS232_730("GPRS Online", 1); //потом заменить на задание со View
                    device.tcpConnectionStatus = "Connecting...";
                    isRunning = true;
                    connectionStatus = "Сервер развернут. Ожидание подключения устройства...";

                    while (isRunning)
                    {
                        device.tcpClient = await server.AcceptTcpClientAsync();
                        connectionStatus = "Connected";

                        lock (device.connectinLocker)
                        {
                            _ = Task.Run(() => StartCheckConnectionToDeviceLoop(device, _cts.Token));
                        }

                        // Обрабатываем клиента в отдельной задаче
                        _ = Task.Run(() => HandleClientAsync(device, _cts.Token));
                    }
                }
                catch (Exception ex)
                {
                    connectionStatus = $"Ошибка: {ex.Message}";
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    isRunning = false;
                }
            }

            private async Task HandleClientAsync(UsrGPRS232_730 device, CancellationToken cancellationToken)
            {
                TcpClient client = device.tcpClient;
                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        // Читаем данные от клиента
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            device.tcp5HeartBeatTimingMessageCounter = device.tcp5HeartBeatTimingMessageCounter + 1;
                            byte[] newBuffer = new byte[bytesRead];
                            Array.Copy(buffer, newBuffer, bytesRead);

                            await Task.Run(async () =>
                            {
                                List<byte[]> responseList = await Task.Run(() => Protocols.Modbuss.ModBussRTU.CutToModbusRtuMessageListTableFastMb(newBuffer));

                                string cuttedMessageMB = "";
                                for (int i = 0; i < responseList.Count; i++)
                                {
                                    cuttedMessageMB = cuttedMessageMB + "<br>" + BitConverter.ToString(responseList[i]);
                                    if (responseList[i][1] == 3)
                                    {
                                        if(readingMode == "default")
                                        {
                                            answerMb3 = Protocols.Modbuss.ModBussRTU.DecodeModbusMessage(responseList[i], answerFormat);
                                        }
                                        else if(readingMode == "buffer") //О госпади, какая срань...
                                        {
                                            answerMb3 = Protocols.Modbuss.ModBussRTU.DecodeModbusMessage(responseList[i], "float"); //Buffer: Нужно наисать код учитывающий не только float
                                        }
                                    }
                                }
                            });
                        }
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Socket error: {ex.Message}");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"IO error: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Operation canceled.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Client error: {ex.Message}");
                }
                finally
                {
                    client.Close();
                }
            }

            private async Task StartCheckConnectionToDeviceLoop(UsrGPRS232_730 device, CancellationToken cancellationToken)
            {
                double delayFiveHeartBeatReal = device.heartbeatMessageRateSec * 2;
                int loopCounter = 1;
                int[] heartBeatAtLoop = new int[5];
                int lostConnectionCounter = 0;

                while (isRunning && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    heartBeatAtLoop[loopCounter - 1] = device.tcp5HeartBeatTimingMessageCounter;
                    int missedPockets = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (heartBeatAtLoop[i] < i + 1)
                            missedPockets++;
                    }

                    switch (missedPockets)
                    {
                        case 5:
                            if (device.tcpClient != null)
                                device.tcpConnectionStatus = "Bad connection 0% package recived";
                            Console.WriteLine("Bad connection 0% package recived");
                            lostConnectionCounter++;
                            break;
                        case 4:
                            device.tcpConnectionStatus = "Bad connection <= 20% package recived";
                            Console.WriteLine("Bad connection <= 20% package recived");
                            lostConnectionCounter++;
                            break;
                        case 3:
                            device.tcpConnectionStatus = "Bad connection <= 40% package recived";
                            Console.WriteLine("Bad connection <= 40% package recived");
                            break;
                        case 2:
                            device.tcpConnectionStatus = "Unstable connection <= 60% package recived";
                            Console.WriteLine("Unstable connection <= 60% package recived");
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        case 1:
                            device.tcpConnectionStatus = "Connected ~ 80% package recived";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        case 0:
                            device.tcpConnectionStatus = "Fast connection 100% package recived";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                        default:
                            device.tcpConnectionStatus = "Connection supergood!";
                            if (lostConnectionCounter > 0)
                                lostConnectionCounter--;
                            break;
                    }
                    connectionStatus = device.tcpConnectionStatus;

                    if (lostConnectionCounter >= 5)
                    {
                        lostConnectionCounter = 0;
                        device.tcpConnectionStatus = "В связи с нестабильным соединением осуществляется переподключение";
                        await StopTalkAndReconnect();
                    }

                    if (loopCounter == 5)
                    {
                        loopCounter = 1;
                        device.tcp5HeartBeatTimingMessageCounter = 0;
                    }
                    else
                        loopCounter = loopCounter + 1;
                }
            }


            public async Task SendMB3CommandToDevice(UsrGPRS232_730 device, int deviceId, int address, int quantity)
            {
                if (device == null) return;
                if (device.tcpClient == null || !device.tcpClient.Connected)
                {
                    return;
                }

                try
                {
                    NetworkStream stream = device.tcpClient.GetStream();
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine("Sent command to device: " + command);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sending error: {ex.Message}");
                }
            }

            public async Task SendMB10CommandToDevice(UsrGPRS232_730 device, int deviceId, int address, int quantity, byte[] byteData)
            {
                if (device == null) return;
                if (device.tcpClient == null || !device.tcpClient.Connected)
                {
                    Console.WriteLine("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = device.tcpClient.GetStream();
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateWriteMultipleRegistersCommand(deviceId, address, quantity, quantity * 2, byteData);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"Отправлено сообщение: Отправлена команда MB10  для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sending error: {ex.Message}");
                }
            }
            public async Task Stop()
            {
                if (!isRunning)
                {
                    Console.WriteLine("Сервер уже остановлен.");
                    return;
                }

                try
                {
                    _cts.Cancel();

                    if (server != null)
                    {
                        server.Stop();
                        Console.WriteLine("TCP-сервер остановлен.");
                    }

                    if (device.tcpClient != null)
                    {
                        device.tcpClient.Close();
                        Console.WriteLine("Клиентское соединение закрыто.");
                    }

                    isRunning = false;
                    connectionStatus = "Disconnecting...";
                    await Task.Delay(1000); // Даем время на закрытие потока вывода
                    connectionStatus = "Disconnected";
                    Console.WriteLine("Сервер и клиентские соединения закрыты.");
                }
                catch (Exception ex)
                {
                    connectionStatus = $"Ошибка при остановке сервера: {ex.Message}";
                    Console.WriteLine($"Ошибка при остановке сервера: {ex.Message}");
                }
            }


            private async Task StopTalkAndReconnect()
            {
                await Stop();
                Task.Delay(10000);
                Start(lastIpAdress, lastPort);
            }
        }

    }
}

