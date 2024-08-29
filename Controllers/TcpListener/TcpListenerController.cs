using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Read_Write_GPRS_Server.Controllers
{



    public class TcpListenerController: ControllerBase
    {

        



        public static async Task Start(string ipAddress, int port)
            {

                // Создаем TcpListener
                TcpListener listener = new TcpListener(IPAddress.Any, port);

                try
                {
                    // Начинаем прослушивание входящих соединений
                    listener.Start();
                    Console.WriteLine($"TCP-сервер запущен на {ipAddress}:{port}");

                    // Ожидаем входящие соединения
                    while (true)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        Console.WriteLine("Подключено новое соединение.");

                        // Обрабатываем клиента в отдельной задаче
                        _ = Task.Run(() => HandleClientAsync(client));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
                //finally
                //{
                //    // Останавливаем прослушивание
                //    listener.Stop();
                //}
            }

            static async Task HandleClientAsync(TcpClient client)
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
                            Console.WriteLine($"Получено сообщение: {message}");

                            string response = "Message accepted.";
                            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке клиента: {ex.Message}");
                }
                //finally
                //{
                //    // Закрываем соединение с клиентом
                //    client.Close();
                //}
            }

    }
    }

