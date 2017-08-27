using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIT1
{
    class Sensor
    {
        protected int _id;
        protected DateTime _lastSeen;
        protected DateTime _lastSeenSave;
        protected string _location;
        public ConnectionData _connection;
        protected string _devEUI;
        //protected string _rxInfo;
        protected List<double> _rssi = new List<double>();
        protected List<string> _time = new List<string>();
        protected List<double> _snr = new List<double>();
        protected List<string> _mac = new List<string>();
        protected int _bandwidth;
        protected int _spreadFactor;
        protected bool _adr;
        protected string _codeRate;
        protected string _frequency;

        public Sensor(int id, string location, DateTime lastSeen)
        {
            _id = id;
            _location = location;
            _lastSeen = lastSeen;
        }

        public Sensor(AppRxPacket packet, int id, string location, DateTime time)
        {
            _id = id;
            _location = location;
            _lastSeen = time;
            if (_rssi.Count == _snr.Count)
                for (int i = 0; i < _rssi.Count; i++)
                    if (_connection == null)
                        _connection = new ConnectionData((int)_rssi[i], (int)_snr[i]);
                    else
                        _connection.newOccurence((int)_rssi[i], (int)_snr[i]);
        }

        protected void update(DateTime time, AppRxPacket packet)
        {
            _lastSeenSave = _lastSeen;
            _lastSeen = time;
            _devEUI = packet.devEui;
            _spreadFactor = packet.spreadFactor;
            _bandwidth = packet.bandwidth;
            _codeRate = packet.codeRate;
            _adr = packet.adr;
            _frequency = packet.frequency;
            _rssi.Clear();
            _snr.Clear();
            _mac.Clear();
            _time.Clear();
            for(int i = 0; i < packet.rssi.Count; i++)
            {
                _rssi.Add(packet.rssi[i]);
                _snr.Add(packet.snr[i]);
                _mac.Add(packet.mac[i]);
                _time.Add(packet.time[i]);
            }
            if (_rssi.Count == _snr.Count)
                for(int i = 0; i < _rssi.Count; i++)
                    if (_connection == null)
                        _connection = new ConnectionData((int)_rssi[i], (int)_snr[i]);
                    else
                        _connection.newOccurence((int)_rssi[i], (int)_snr[i]);
        }

        protected string convertion(string extractedData)
        {
            byte[] convertedByte = Convert.FromBase64String(((extractedData)));
            string hex = BitConverter.ToString(convertedByte).Replace("-", string.Empty);
            return hex;
        }

        protected int bin16dec(int bin)
        {
            int num = bin & 0xFFFF;
            if ((0x8000 & num) != 0)
                num = -(0x010000 - num);
            return num;
        }

        protected int bin8dec(int bin)
        {
            var num = bin & 0xFF;
            if ((0x80 & num) != 0)
                num = -(0x0100 - num);
            return num;
        }

        protected string macAssociation(string mac)
        {
            string str = "";
            switch (mac)
            {
                case "b827ebfffe82bbc9":
                    str = "CIT block B building(imst lite gw)";
                    break;
                case "024b08fffe0e0bd0":
                    str = "CIT admin building (kerlink gw)";
                    break;
                case "84eb18fffee6c342":
                    str = "CIT nimbus building (lorrier gw)";
                    break;
                case "b827ebfffe416a34":
                    str = "CIT nimbus building indoor (imst lite gw)";
                    break;
            }
            return str;
        }

        protected byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        protected string toString(string sensor)
        {
            string result = "";
            result += sensor + "\n";
            result += "Id : " + _id + "\n";
            result += "Nodename : sen_" + sensor.ToLower() + "_" + _id + "\n";
            result += "Last seen : " + _lastSeen + " , " + (_lastSeenSave - _lastSeen) + " ago\n";
            result += "Location : " + _location + "\n";
            result += "DevEUI : " + _devEUI + " \n";

            result += "-----\n"+"TxInfo : \nFrequency : " + _frequency + "\nBandwitdth : "
                + _bandwidth + "\nSpreadFactor : " + _spreadFactor + "\nAdr : "
                + _adr + "\nCodeRate : " + _codeRate + "\n-----\n";
            try
            {
                for(int i = 0; i < _snr.Count; i++)
                {
                    result += "Mac " + (i + 1)+" : " + _mac[i] +" (" + macAssociation(_mac[i]) + ")\n";
                    result += "Time : " + _time[i] + "\n";
                    result += "Rssi : " + _rssi[i] + "\n";
                    result += "Snr : " + _snr[i] +"\n";
                    result += "-----\n";
                }
            }catch(Exception e)
            {
                System.Console.WriteLine(e);
            }
            result += "Average Snr connection : " + _connection.Snr + " Average Rssi connection : " + _connection.Rssi +"\n";
            result += "-----\n";

            return result + "\n";
        }

        public DateTime lastseen(){return _lastSeen;}

        public int id() {return _id;}
    }
}
