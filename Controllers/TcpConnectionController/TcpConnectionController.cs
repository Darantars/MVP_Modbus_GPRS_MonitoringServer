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
            private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>();
            private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            public string messageLog { get; set; }

            private int messageCounter { get; set; }

            public int messageBufferSize { get; set; }

            private TcpDevice.UsrGPRS232_730 gprsOne;

            private TcpListener server { get; set; }

            private bool isRunning = false;

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
                await AddMessageToQueue("Await press start to run server");

                try
                {
                    server = new TcpListener(IPAddress.Any, port);

                    // Начинаем прослушивание входящих соединений
                    server.Start();
                    await AddMessageToQueue($"TCP-сервер запущен на {ipAddress}:{port}");

                    gprsOne = new TcpDevice.UsrGPRS232_730("GPRS Online", 1); //потом заменить на задание со View
                    gprsOne.tcpConnectionStatus = "Connecting...";
                    isRunning = true;
                    await AddMessageToQueue("Сервер развернут. Ожидание подключения устройства...");
                    
                    while (true)
                    {
                        gprsOne.tcpClient = await server.AcceptTcpClientAsync();
                        await AddMessageToQueue("Подключено новое соединение.");
                        _ = Task.Run(() => StartCheckConnectionToDeviceLoop(gprsOne));
                        // Обрабатываем клиента в отдельной задаче
                        _ = Task.Run(() => HandleClientAsync(gprsOne));
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

                if (gprsOne.tcpClient != null)
                {
                    gprsOne.tcpClient.Close();
                    await AddMessageToQueue("Клиентское соединение закрыто.");
                }

                isRunning = false;
                await AddMessageToQueue("Сервер и клиентские соединения закрыты.");
            }

            private async Task StartCheckConnectionToDeviceLoop(UsrGPRS232_730 device)              ////T0 DO: Таска была открыта, таску нужно закрыть
            {
                double delayFiveHeartBeatReal = device.heartbeatMessageRateSec * 2;
                int loopCounter = 1;
                int[] heartBeatAtLoop = new int[4];
                

                while (isRunning )
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    heartBeatAtLoop[loopCounter - 1] = device.tcp5HeartBeatTimingMessageCounter;
                    int missedPockets = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (heartBeatAtLoop[i] < i) 
                            missedPockets++;
                    }
                        switch (missedPockets)
                        {
                            case 5:
                                if (device.tcpClient != null)
                                    device.tcpConnectionStatus = "Bad connection 0% package recived";
                                break;
                            case 4:
                                device.tcpConnectionStatus = "Bad connection 20% package recived";
                                break;
                            case 3:
                                device.tcpConnectionStatus = "Bad connection 40% package recived";
                                break;
                            case 2:
                                device.tcpConnectionStatus = "Connected 60% package recived";
                                break;
                            case 1:
                                device.tcpConnectionStatus = "Connected 80% package recived";
                                break;
                            case 0:
                                device.tcpConnectionStatus = "Fast connection 100% package recived";
                                break;
                            default:
                                device.tcpConnectionStatus = "Connection supergood!";
                                break;
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
                if (gprsOne != null)
                    return gprsOne.tcpConnectionStatus;
                else
                    return "Not started yet";
            }

            public async Task SendMessgeToDeviceASCII(string message)
            {
                if (gprsOne.tcpClient == null || !gprsOne.tcpClient.Connected)
                {
                    await AddMessageToQueue("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = gprsOne.tcpClient.GetStream();
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
                if (gprsOne.tcpClient == null || !gprsOne.tcpClient.Connected)
                {
                    await AddMessageToQueue("No client. Pleasr, await client");
                    return;
                }

                try
                {
                    NetworkStream stream = gprsOne.tcpClient.GetStream();
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
                    string command = BitConverter.ToString(responseBytes);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await AddMessageToQueue($"Отправлено сообщение: Отправлена команда MB4  для {deviceId} ID {address} регистр 2 байта<br> hex command: {command}");
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
                            List<byte[]> responseList = await Task.Run(() => Protocols.Modbuss.ModBussRTU.CutToModbusRtuMessageListFastMb(newBuffer));

                                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                                string decodedMbCommand = "";

                                for (int i = 0; i < responseList.Count; i++)
                                {
                                    decodedMbCommand = decodedMbCommand + "<br>"+ Protocols.Modbuss.ModBussRTU.DecodeModbusMessage(responseList[i]);
                                }

                                string hexedNessage=  BitConverter.ToString(buffer, 0, bytesRead);

                                string cuttedMessageMB = "";
                                for (int i = 0; i < responseList.Count; i++)
                                {
                                    cuttedMessageMB = cuttedMessageMB + "<br>"+ BitConverter.ToString(responseList[i]);
                                }

                                await AddMessageToQueue($"<br> Получено сообщение: <br> ASCII: {message} <br> MB: {decodedMbCommand} <br> hex: {hexedNessage} <br> hex-commands(probably): {cuttedMessageMB}");
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
                    byte[] responseBytes = Protocols.Modbuss.ModBussRTU.GenerateReadHoldingRegistersCommand(deviceId, address, quantity);
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
                            string response = Protocols.Modbuss.ModBussRTU.DecodeModbusMessageValueMb3(buffer);
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
                _cancellationTokenSource.Cancel();
            }

            
        }
    }
}
    
