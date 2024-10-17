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

            Console.WriteLine("Поймано: " + BitConverter.ToString(byteData));

            while (i < byteData.Length - 6)
            {
                byte functionCodeRequest = byteData[i + 1];

                if (functionCodeRequest == 3)
                {
                    // Проверка на наличие двух запросов и ответов
                    if (i + 28 <= byteData.Length)
                    {
                        if (Parser.TryParseMb3TwoRequestsAndResponses(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }

                    // Проверка на наличие одной пары запрос-ответ
                    if (i + 14 <= byteData.Length)
                    {
                        if (Parser.TryParseMb3RequestAndResponse(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }

                    // Проверка на наличие ответа
                    if (i + 6 <= byteData.Length)
                    {
                        if (Parser.TryParseMb3Response(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }

                    // Проверка на наличие запроса
                    if (i + 8 <= byteData.Length)
                    {
                        if (Parser.TryParseMb3Request(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }


                }
                else if(functionCodeRequest == 16)
                {
                    // Проверка на наличие запроса
                    if (i + 10 <= byteData.Length)
                    {
                        if (Parser.TryParseMb10Request(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }

                    // Проверка на наличие запроса
                    if (i + 8 <= byteData.Length)
                    {

                        if (Parser.TryParseMb10Response(byteData, ref i, commands))
                        {
                            continue;
                        }
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

        public static List<byte[]> CutToModbusRtuMessageListTableFastMb(byte[] byteData)
        {
            List<byte[]> commands = new List<byte[]>();
            int i = 0;

            Console.WriteLine("Поймано: " + BitConverter.ToString(byteData));

            while (i < byteData.Length - 6)
            {
                byte functionCodeRequest = byteData[i + 1];

                if (functionCodeRequest == 3)
                {
                  
                    // Проверка на наличие ответа
                    if (i + 6 <= byteData.Length)
                    {
                        if (Parser.TryParseMb3Response(byteData, ref i, commands))
                        {
                            continue;
                        }
                    }
                }
                else if (functionCodeRequest == 16)
                {
                    // Проверка на наличие ответа
                    if (i + 10 <= byteData.Length)
                    {
                        if (Parser.TryParseMb10Request(byteData, ref i, commands))
                        {
                            continue;
                        }
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

        public static byte[] GenerateWriteMultipleRegistersCommand(int modbusId, int startAddress, int quantity, int byteCount, byte[] byteData)
        {
            // Проверка входных данных
            if (modbusId < 0 || modbusId > 255)
                throw new ArgumentOutOfRangeException(nameof(modbusId), "Modbus ID must be between 0 and 255.");
            if (startAddress < 0 || startAddress > 65535)
                throw new ArgumentOutOfRangeException(nameof(startAddress), "Start address must be between 0 and 65535.");
            if (quantity < 1 || quantity > 123)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be between 1 and 123.");
            if (byteCount != quantity * 2)
                throw new ArgumentException("Byte count must be equal to quantity * 2.");
            if (byteData.Length != byteCount)
                throw new ArgumentException("Byte data length must be equal to byte count.");

            // Создаем массив для команды
            byte[] command = new byte[9 + byteCount];

            // Заполняем команду
            command[0] = (byte)modbusId;                      // Адрес устройства
            command[1] = 0x10;                                // Функция (Write Multiple Registers)
            command[2] = (byte)(startAddress >> 8);           // Старший байт адреса начального регистра
            command[3] = (byte)(startAddress & 0xFF);         // Младший байт адреса начального регистра
            command[4] = (byte)(quantity >> 8);               // Старший байт количества регистров
            command[5] = (byte)(quantity & 0xFF);             // Младший байт количества регистров
            command[6] = (byte)byteCount;                      // Количество байт данных

            // Копируем данные
            Array.Copy(byteData, 0, command, 7, byteCount);

            // Вычисляем CRC
            ushort crc = CalculateCRC(command, 0, command.Length - 2);
            command[command.Length - 2] = (byte)(crc & 0xFF);  // Младший байт CRC
            command[command.Length - 1] = (byte)(crc >> 8);   // Старший байт CRC

            return command;
        }

        public static ushort CalculateCRC(byte[] data, int start, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = start; i < start + length; i++)
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

        public static string DecodeModbusMessage(byte[] buffer, string mode)
        {
            if (buffer.Length < 2)
            {
                return "Недостаточная длина команды";
            }
            mode = mode.ToLower();
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
                        StringBuilder data = new StringBuilder();
                        // Извлечение данных регистров для функции 3
                        switch (mode)
                        {
                            case "int16":
                                data = new StringBuilder();
                                for (int i = 0; i < byteCount / 2; i++)
                                {
                                    short value = (short)((buffer[3 + i * 2] << 8) | buffer[3 + i * 2 + 1]);
                                    data.Append($"{value} ");
                                }
                                registers = $"{data.ToString().Trim()}";
                                break;
                            case "uint16":
                                data = new StringBuilder();
                                for (int i = 0; i < byteCount / 2; i++)
                                {
                                    ushort value = (ushort)((buffer[3 + i * 2] << 8) | buffer[3 + i * 2 + 1]);
                                    data.Append($"{value} ");
                                }
                                registers = $"{data.ToString().Trim()}";
                                break;


                            case "uint32":
                                for (int i = 0; i < byteCount / 4; i++)
                                {
                                    uint value = (uint)((buffer[3 + i * 4] << 24) | (buffer[3 + i * 4 + 1] << 16) |
                                                        (buffer[3 + i * 4 + 2] << 8) | buffer[3 + i * 4 + 3]);
                                    data.Append($"{value} ");
                                }
                                registers = $"{data.ToString().Trim()}";
                                break;

                            case "int32":
                                for (int i = 0; i < byteCount / 4; i++)
                                {
                                    int value = (buffer[3 + i * 4] << 24) | (buffer[3 + i * 4 + 1] << 16) |
                                                (buffer[3 + i * 4 + 2] << 8) | buffer[3 + i * 4 + 3];
                                    data.Append($"{value} ");
                                }
                                registers = $"{data.ToString().Trim()}";
                                break;

                            case "float":
                                for (int i = 0; i < byteCount / 4; i++)
                                {
                                    byte[] floatBytes = new byte[4];
                                    floatBytes[2] = buffer[3 + i * 4 + 3];
                                    floatBytes[3] = buffer[3 + i * 4 + 2];
                                    floatBytes[0] = buffer[3 + i * 4 + 1];
                                    floatBytes[1] = buffer[3 + i * 4];
                                    float value = BitConverter.ToSingle(floatBytes, 0);
                                    data.Append($"{value} ");
                                }
                                registers = $"{data.ToString().Trim()}";
                                break;
                        }
                    }
                    break;

                case 16: // Write Multiple Holding Registers
                    if (bytesRead < 7)
                    {
                        return "Недостаточная длина команды для записи нескольких регистров";
                    }
                    if (bytesRead == 8)
                    {
                        messageType = "Запрос";
                    }
                    else
                    {
                        messageType = "Ответ";
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
                        registers += $"{data.ToString().Trim()} ";
                    }
                    break;

                default:
                    return "Неподдерживаемая функция Modbus";
            }

            switch (mode)
            {
                case "gettypeandadress":
                    return $"{messageType}: команда {functionCode} для устройства ID {modbusId}";
                case "int16":
                    return registers;
                case "uint16":
                    return registers;
                case "int32":
                    return registers;
                case "uint32":
                    return registers;
                case "float":
                    return registers;
                default:
                    return $"Формат {mode} еще не введен";
            }
        }

        private class Parser()
        {
            public static bool TryParseMb3TwoRequestsAndResponses(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte addressRequest1 = byteData[i];
                byte functionCodeRequest1 = byteData[i + 1];
                byte byteCountRequest1 = 8;
                byte addressRequest2 = byteData[i + 8];
                byte functionCodeRequest2 = byteData[i + 9];
                byte byteCountRequest2 = 8;

                if (i + byteCountRequest1 + byteCountRequest2 <= byteData.Length)
                {
                    //start index of resposponses 16
                    byte addressResponce1 = byteData[i + 16];
                    byte functionCodeResponce1 = byteData[i + 17];
                    byte dataNumResponce1 = byteData[i + 18];
                    int byteCountResponce1 = 3 + dataNumResponce1 + 2;
                    
                    if (i + byteCountRequest1 + byteCountRequest2 + byteCountResponce1 <= byteData.Length)
                    {
                        byte addressResponce2 = byteData[i + 16 + byteCountResponce1];
                        byte functionCodeResponce2 = byteData[i + 17 + byteCountResponce1];
                        byte dataNumResponce2 = byteData[i + 18 + byteCountResponce1];
                        int byteCountResponce2 = 3 + dataNumResponce2 + 2;

                        if (i + byteCountRequest1 + byteCountRequest2 + byteCountResponce1 + byteCountResponce2 <= byteData.Length + 1)
                        {
                            if ((functionCodeRequest1 == functionCodeRequest2) && (functionCodeResponce1 == functionCodeResponce2) 
                                && (functionCodeResponce2 == 3) && (functionCodeRequest1 == 3))
                            {
                                byte[] request1 = new byte[8];
                                Array.Copy(byteData, i, request1, 0, 8);
                                commands.Add(request1);

                                byte[] request2 = new byte[8];
                                Array.Copy(byteData, i + 8, request2, 0, 8);
                                commands.Add(request2);

                                byte[] responce1 = new byte[byteCountResponce1];
                                Array.Copy(byteData, i + 16, responce1, 0, byteCountResponce1);
                                commands.Add(responce1);

                                byte[] responce2 = new byte[byteCountResponce2];
                                Array.Copy(byteData, i + 16 + byteCountResponce2, responce2, 0, byteCountResponce2);
                                commands.Add(responce2);

                                i += byteCountRequest1 + byteCountRequest2 + byteCountResponce1 + byteCountResponce2;
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            public static bool TryParseMb3RequestAndResponse(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte addressRequest = byteData[i];
                byte functionCodeRequest = byteData[i + 1];
                int requestLenght = 8;

                byte addressResponse = byteData[i + 8];
                byte functionCodeResponse = byteData[i + 9];
                byte byteCountResponse = byteData[i + 10];
                int responseLength = byteCountResponse + 3 + 2;

                if (functionCodeRequest == 3 && functionCodeResponse == 3)
                {
                    if (addressRequest == addressResponse && functionCodeRequest == functionCodeResponse)
                    {
                        
                        if (i + requestLenght + responseLength <= byteData.Length)
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

            public static bool TryParseMb3Request(byte[] byteData, ref int i, List<byte[]> commands)
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

            public static bool TryParseMb10Request(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte functionCodeRequest = byteData[i + 1];
                if (functionCodeRequest == 16)
                {
                    byte byteCount = byteData[i + 6];

                    int requestLength = 1 + 1 + 2 + 2 + 1 + byteCount + 2;

                    if (i + requestLength <= byteData.Length)
                    {
                        byte[] request = new byte[requestLength];
                        Array.Copy(byteData, i, request, 0, requestLength);
                        commands.Add(request);
                        i += requestLength;
                        return true;
                    }
                }
                return false;
            }

            public static bool TryParseMb3Response(byte[] byteData, ref int i, List<byte[]> commands)
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

            public static bool TryParseMb10Response(byte[] byteData, ref int i, List<byte[]> commands)
            {
                byte functionCodeRequest = byteData[i + 1];
                if (functionCodeRequest == 16)
                {
                    byte byteCountResponse = byteData[i + 2];
                    int responseLength = 8;
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