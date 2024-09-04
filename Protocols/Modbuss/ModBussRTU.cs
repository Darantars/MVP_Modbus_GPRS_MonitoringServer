using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Read_Write_GPRS_Server.Protocols.Modbuss
{
    public class ModBussRTU
    {

        public static List<byte[]> CutToModbusRtuMessageListFastMb(byte[] byteData)
        {
            List<byte[]> commands = new List<byte[]>();
            int i = 0;

            while (i < byteData.Length - 6)
            {
                // Проверка на наличие двух запросов и ответов
                if (i + 30 <= byteData.Length)
                {
                    if (Parser.TryParseTwoRequestsAndResponses(byteData, ref i, commands))
                    {
                        continue;
                    }
                }

                // Проверка на наличие одной пары запрос-ответ
                if (i + 16 <= byteData.Length)
                {
                    if (Parser.TryParseRequestAndResponse(byteData, ref i, commands))
                    {
                        continue;
                    }
                }

                // Проверка на наличие запроса
                if (i + 8 <= byteData.Length)
                {
                    if (Parser.TryParseRequest(byteData, ref i, commands))
                    {
                        continue;
                    }
                }

                // Проверка на наличие ответа
                if (i + 7 <= byteData.Length)
                {
                    if (Parser.TryParseResponse(byteData, ref i, commands))
                    {
                        continue;
                    }
                }

                // Если ничего не подошло, пробуем начать снова со второго байта
                i++;
            }

            // Вывод в консоль полученного буфера команд в HEX формате
            foreach (var cmd in commands)
            {
                Console.WriteLine(BitConverter.ToString(cmd));
            }

            return commands;
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
                    registers = $"{data.ToString().Trim()}";
                }
                return registers;
            }
            else return "no data";
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
                        registers += $" (Данные: {data.ToString().Trim()}) ";
                    }
                    break;

                default:
                    return "Неподдерживаемая функция Modbus";
            }

            return $"{messageType}: команда {functionCode} для устройства ID {modbusId} {registers}";
        }

        private class Parser()
        {

            public static bool TryParseTwoRequestsAndResponses(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte addressRequest1 = byteData[i];
                byte functionCodeRequest1 = byteData[i + 1];
                byte addressResponse1 = byteData[i + 8];
                byte functionCodeResponse1 = byteData[i + 9];
                byte byteCountResponse1 = byteData[i + 10];

                int nextRequireIndex = i + 8 + 3 + 10 + byteCountResponse1;
                if (nextRequireIndex <= byteData.Length)
                {
                    byte addressRequest2 = byteData[i + 8 + 3 + byteCountResponse1];
                    byte functionCodeRequest2 = byteData[i + 8 + 3 + byteCountResponse1 + 1];
                    byte addressResponse2 = byteData[i + 8 + 3 + byteCountResponse1 + 8];
                    byte functionCodeResponse2 = byteData[i + 8 + 3 + byteCountResponse1 + 9];
                    byte byteCountResponse2 = byteData[i + 8 + 3 + byteCountResponse1 + 10];

                    nextRequireIndex = nextRequireIndex + 8 + 3 + 10 + byteCountResponse2;
                    if (nextRequireIndex <= byteData.Length)
                    {
                        if (addressRequest1 == addressResponse1 && functionCodeRequest1 == functionCodeResponse1 &&
                            addressRequest2 == addressResponse2 && functionCodeRequest2 == functionCodeResponse2)
                        {
                            if (functionCodeRequest1 == 3 && functionCodeRequest2 == 3)
                            {
                                int responseLength1 = byteCountResponse1 + 3 + 2;
                                int responseLength2 = byteCountResponse2 + 3 + 2;
                                if (i + 8 + responseLength1 + 8 + responseLength2 <= byteData.Length)
                                {
                                    byte[] request1 = new byte[8];
                                    Array.Copy(byteData, i, request1, 0, 8);
                                    commands.Add(request1);

                                    byte[] response1 = new byte[responseLength1];
                                    Array.Copy(byteData, i + 8, response1, 0, responseLength1);
                                    commands.Add(response1);

                                    byte[] request2 = new byte[8];
                                    Array.Copy(byteData, i + 8 + responseLength1, request2, 0, 8);
                                    commands.Add(request2);

                                    byte[] response2 = new byte[responseLength2];
                                    Array.Copy(byteData, i + 8 + responseLength1 + 8, response2, 0, responseLength2);
                                    commands.Add(response2);

                                    i += 8 + responseLength1 + 8 + responseLength2;
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }

            public static bool TryParseRequestAndResponse(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte addressRequest = byteData[i];
                byte functionCodeRequest = byteData[i + 1];
                byte addressResponse = byteData[i + 8];
                byte functionCodeResponse = byteData[i + 9];
                byte byteCountResponse = byteData[i + 10];

                if (functionCodeRequest == 3 && functionCodeResponse == 3)
                {
                    if (addressRequest == addressResponse && functionCodeRequest == functionCodeResponse)
                    {
                        int responseLength = byteCountResponse + 3 + 2;
                        if (i + 8 + responseLength <= byteData.Length)
                        {
                            byte[] request = new byte[8];
                            Array.Copy(byteData, i, request, 0, 8);
                            commands.Add(request);

                            byte[] response = new byte[responseLength];
                            Array.Copy(byteData, i + 8, response, 0, responseLength);
                            commands.Add(response);

                            i += 8 + responseLength;
                            return true;
                        }
                    }
                }
                return false;
            }

            public static bool TryParseRequest(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte functionCodeRequest = byteData[i + 1];
                if (functionCodeRequest == 3)
                {
                    byte[] request = new byte[8];
                    Array.Copy(byteData, i, request, 0, 8);
                    commands.Add(request);
                    i += 8;
                    return true;
                }
                return false;
            }

            public static bool TryParseResponse(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte functionCodeRequest = byteData[i + 1];
                if (functionCodeRequest == 3)
                {
                    byte byteCountResponse = byteData[i + 2];
                    int responseLength = byteCountResponse + 3 + 2;
                    if (i + responseLength <= byteData.Length)
                    {
                        byte[] response = new byte[responseLength];
                        Array.Copy(byteData, i, response, 0, responseLength);
                        commands.Add(response);
                        i += responseLength;
                        return true;
                    }
                }
                return false;
            }
        }
    }
}