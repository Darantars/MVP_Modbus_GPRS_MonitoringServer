namespace Read_Write_GPRS_Server.Device
{
    public class UsrGPRS232_730
    {
        public string heartbeatMessageTextASCII {  get; set; }

        public int heartbeatMessageRateSec {  get; set; }

        public UsrGPRS232_730(string heartbeatMessageTextASCII, int heartbeatMessageRateSec) 
        {
            heartbeatMessageRateSec = heartbeatMessageRateSec;

            heartbeatMessageTextASCII = heartbeatMessageTextASCII;
        }

    }
}
