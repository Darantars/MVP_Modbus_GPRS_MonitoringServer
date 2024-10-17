namespace Read_Write_GPRS_Server.Plugins.DeviceTable
{
    public class Parametr
    {
        public string name { get; set; }

        public string value { get; set; }

        public int adress { get; set; }

        public int coiffient { get; set; }

        public int size { get; set; }

        public string type { get; set; }

        public string unitType { get; set; }

        public string format { get; set; }

        public int coificients {  get; set; }

        public Parametr(string paramName, string paramValue, int paramAdress, int paramSize, string paramType, string paramUnitType, string paramFormat, int paramCoificients)
        {
            name = paramName;
            value = paramValue;
            adress = paramAdress;
            coiffient = paramSize;
            size = paramSize;
            type = paramType;
            unitType = paramUnitType;
            format = paramFormat;
            coificients = paramCoificients;
        }
    }
}
