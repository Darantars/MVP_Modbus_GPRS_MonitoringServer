﻿using System.Net.Sockets;

namespace Read_Write_GPRS_Server.TcpDevice
{
    public class UsrGPRS232_730
    {
        public string heartbeatMessageTextASCII {  get; set; }

        public int heartbeatMessageRateSec {  get; set; }

        public TcpClient tcpClient { get; set; }

        public string tcpConnectionStatus {  get; set; }

        public int tcp5HeartBeatTimingMessageCounter;
        
        public object connectinLocker;

        public UsrGPRS232_730(string deviceHeartbeatMessageTextASCII, int deviceHeartbeatMessageRateSec) 
        {
            heartbeatMessageRateSec = deviceHeartbeatMessageRateSec;

            heartbeatMessageTextASCII = deviceHeartbeatMessageTextASCII;
            
            tcpClient = new TcpClient();

            tcpConnectionStatus = "Offline";

            tcp5HeartBeatTimingMessageCounter = 0;

            connectinLocker = new();
        }

    }
}
