using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Read_Write_GPRS_Server.Protocols.Modbuss
{
    public class ModBussRTU
    {
public static List<byte[]> CutToModbusRtuMessageList(byte[] byteData)
    {
        List<byte[]> commands = new List<byte[]>();
        int i = 0;
        while (i < byteData.Length)
        {

            if (i + 7 > byteData.Length)
                break;

            byte address = byteData[i];
            byte functionCode = byteData[i + 1];
            int totalLength = 0;
            bool isLastMessageRequest = false;

            if (functionCode == 0x03)   // 3 (Read Holding Registers)
            {
                if(i + 8 <= byteData.Length &&  isLastMessageRequest == false)
                {
                        isLastMessageRequest = true;
                        totalLength = 8;
                }

                else if (i + 5 <= byteData.Length)
                {
                    isLastMessageRequest = false;
                    int dataLength = byteData[i + 2];
                    totalLength = 5 + dataLength; // Ответ
                }
            }
            else if (functionCode == 0x10)  // 10 (Write Multiple Registers)
            {
                if (i + 9 <= byteData.Length && isLastMessageRequest == false) 
                {
                    isLastMessageRequest = true;
                    int byteCount = byteData[i + 6];
                    totalLength = 9 + byteCount; // Запрос
                }
                else if (i + 8 <= byteData.Length) 
                {
                    isLastMessageRequest = false;
                    totalLength = 8; // Ответ
                }
            }
            else
            {
                break;
            }

            // Проверяем, что длина команды не превышает длину оставшихся данных
            if (i + totalLength > byteData.Length)
                break;

            // Извлекаем команду и ответ
            byte[] command = new byte[totalLength];
            Array.Copy(byteData, i, command, 0, totalLength);
            commands.Add(command);

            // Переходим к следующей команде
            i += totalLength;
        }

        // Вывод в консоль полученного буфера команд в HEX формате
        foreach (var cmd in commands)
        {
            Console.WriteLine(BitConverter.ToString(cmd));
        }

        return commands;
    }

        public static List<byte[]> CutToModbusRtuMessageListFastMb(byte[] byteData)
        {
            List<byte[]> commands = new List<byte[]>();
            int i = 0;

            while (i < byteData.Length)
            {
                // 1. Проверить достаточно ли байт в посылке чтобы содержать хотя бы одну пару запрос-ответ
                if (i + 11 <= byteData.Length)
                {
                    // 2. Считать первую пару следующим образом:
                    byte addressRequest = byteData[i];
                    byte functionCodeRequest = byteData[i + 1];
                    byte addressResponse = byteData[i + 8];
                    byte functionCodeResponse = byteData[i + 9];
                    byte byteCountResponse = byteData[i + 10];

                    if (functionCodeRequest == 3)
                    {
                        // Проверяем, что адрес и код функции совпадают
                        if (addressRequest == addressResponse && functionCodeRequest == functionCodeResponse)
                        {
                            int responseLength = byteCountResponse + 3; // 3 байта заголовка ответа
                            if (i + 8 + responseLength <= byteData.Length)
                            {
                                // 3. Записать запрос и ответ в результирующие команды
                                byte[] request = new byte[8];
                                Array.Copy(byteData, i, request, 0, 8);
                                commands.Add(request);

                                byte[] response = new byte[responseLength];
                                Array.Copy(byteData, i + 8, response, 0, responseLength);
                                commands.Add(response);

                                // Переходим к следующей команде
                                i += 8 + responseLength;
                                continue;
                            }
                        }
                    }
                }

                // 4. Проверить запрос ли:
                if (i + 8 <= byteData.Length)
                {
                    byte[] request = new byte[8];
                    byte functionCodeRequest = byteData[i + 1];
                    if (functionCodeRequest == 3)
                    {
                        Array.Copy(byteData, i, request, 0, 8);
                        commands.Add(request);
                        i += 8;
                        continue;
                    }
                }

                // 5. Проверить ответ ли это
                if (i + 3 <= byteData.Length)
                {
                    byte functionCodeRequest = byteData[i + 1];
                    if (functionCodeRequest == 3)
                    {
                        byte byteCountResponse = byteData[i + 2];
                        int responseLength = byteCountResponse + 3; // 3 байта заголовка ответа
                        if (i + responseLength <= byteData.Length)
                        {
                            byte[] response = new byte[responseLength];
                            Array.Copy(byteData, i, response, 0, responseLength);
                            commands.Add(response);
                            i += responseLength;
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
                        registers += $" (Данные: {data.ToString().Trim()})";
                    }
                    break;

                default:
                    return "Неподдерживаемая функция Modbus";
            }

            return $"{messageType}: команда {functionCode} для устройства ID {modbusId} {registers}";
        }
    }
}