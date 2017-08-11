using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIT1
{
    class Ascoel : Sensor
    {
        protected double _batteryVolt;
        protected string _batteryType;
        protected double _batteryPercent;
        protected double _temperature;
        protected float _humidity;
        protected string _alarm;
        protected int _state;
        protected int _counter;
        protected int _port;
        protected string[] _stateString = { "(Close)", "(Open)" };

        public Ascoel(int id, string location, DateTime lastSeen) : base(id, location, lastSeen)
        {
        }

        public Ascoel(AppRxPacket packet, int id, string location, DateTime time) : base(packet, id, location, time)
        {
            doorsDecode(packet);
        }

        private void doorsDecode(AppRxPacket packet)
        {
            _port = packet.fPort;
            switch (_port)
            {
                case 30:
                    byte[] data = StringToByteArray(convertion(packet.data));

                    switch (data[0])
                    {
                        case 0:
                            _state = 0;// " (Close)\n";
                            break;
                        case 1:
                            _state = 1; //" (Open)\n";
                            break;
                    }
                    _counter = bin16dec((data[1] << 8) | (data[2]));

                    float buff = BitConverter.ToSingle(data, 3);
                    _temperature = buff;
                    buff = BitConverter.ToSingle(data, 7);
                    _humidity = buff;
                    break;
                case 9:
                    byte[] data2 = StringToByteArray(convertion(packet.data));

                    if ((data2[0] & 0x80) == 0)
                    {
                        _batteryType = "(3.6V Lithium-thionyl)";
                        _batteryPercent = bin8dec(data2[0] & 0x7F);
                        _batteryVolt = (2.1 + (3.6 - 2.1) * bin8dec(data2[0] & 0x7F) / 100.0);
                    }
                    else
                    {
                        _batteryType = "(3.0V Alkaline Battery)";
                        _batteryPercent = bin8dec(data2[0] & 0x7F);
                        _batteryVolt = (2.1 + (3.0 - 2.1) * bin8dec(data2[0] & 0x7F) / 100.0);
                    }
                    int temp = bin8dec(data2[1]);
                    string status = "";
                    if ((temp & 1 << 4) == 1)
                    {
                        if ((temp & 1 << 3) == 1)
                        {
                            status += "Line Open, ";
                        }
                        else
                        {
                            status += "Short circuit, ";
                        }
                    }
                    else
                    {
                        if ((temp & 1 << 3) == 1)
                        {
                            status += "Alarm, ";
                        }
                        else
                        {
                            status += "OK, ";
                        }
                    }
                    if ((temp & 1 << 2) == 1)
                    {
                        status += "LOW BATTERY EVENT, ";
                    }
                    else
                    {
                        status += "Battery OK, ";
                    }
                    if ((temp & 1 << 1) == 1)
                    {
                        status += "Tamper Alarm, ";
                    }
                    else
                    {
                        status += "Tamper no Alarm, ";
                    }
                    if ((temp & 1 << 0) == 1)
                    {
                        status += "Intrusion Alarm ";
                    }
                    else
                    {
                        status += "Intrusion no Alarm ";
                    }
                    _alarm = status + "\n";
                    float buff2 = BitConverter.ToSingle(data2, 2);
                    _temperature = buff2;
                    buff2 = BitConverter.ToSingle(data2, 6);
                    _humidity = buff2;
                    break;
                default:
                    break;
            }
        }

        public new void update(DateTime time, AppRxPacket packet)
        {
            base.update(time, packet);
            doorsDecode(packet);
        }

        public string toString()
        {
            string str = "*************************************\n";
            str += base.toString("Ascoel");

            switch (_port)
            {
                case 9:
                    {
                        str += "Alive\n";
                        str += "Battery : " + _batteryType + ", " + _batteryVolt + " : " + _batteryPercent + "\n";
                        str += _alarm;
                        str += "Temperature : " + _temperature + "\n";
                        str += "Humidity : " + _humidity + "\n";
                    }
                    break;
                case 30:
                    {
                        str += "Mesurments :\n";
                        str += "Event : " + _state + _stateString[_state] + "\n";
                        str += "Counter Value : " + _counter + "\n";
                        str += "Temperature : " + _temperature + "\n";
                        str += "Humidity : " + _humidity + "\n";
                    }
                    break;
                default:
                    str += "Not yet implemented, port must be 9 or 30" + " port received : " + _port;
                    break;
            }
            return str + "*************************************\n";
        }
    }
}
