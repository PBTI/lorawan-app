using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIT1
{
    class Elsys : Sensor
    {
        protected double _temperature;
        protected int _humidity;
        protected int _light;
        protected int _motion;
        protected double _co2;
        protected double _batteryLevel;
        protected int _analog1;

        public Elsys(int id, string location, DateTime lastSeen) : base(id, location, lastSeen)
        {

        }

        public Elsys(AppRxPacket packet,int id, string location, DateTime time) : base( packet, id, location, time)
        {
            elsysDecode(packet);
        }

        public new void update(DateTime time, AppRxPacket packet)
        {
            base.update(time, packet);
            elsysDecode(packet);
        }

        private void elsysDecode(AppRxPacket packet)
        {

            byte[] data = StringToByteArray(convertion(packet.data));
            //FROM ELSYS.SE:
            const int TYPE_TEMP = 0x01; //temp 2 bytes -3276.8°C -->3276.7°C
            const int TYPE_RH = 0x02; //Humidity 1 byte  0-100%
            const int TYPE_ACC = 0x03; //acceleration 3 bytes X,Y,Z -128 --> 127 +/-63=1G
            const int TYPE_LIGHT = 0x04; //Light 2 bytes 0-->65535 Lux
            const int TYPE_MOTION = 0x05; //No of motion 1 byte  0-255
            const int TYPE_CO2 = 0x06; //Co2 2 bytes 0-65535 ppm 
            const int TYPE_VDD = 0x07; //VDD 2byte 0-65535mV
            const int TYPE_ANALOG1 = 0x08; //VDD 2byte 0-65535mV
            const int TYPE_GPS = 0x09; //3bytes lat 3bytes long binary
            const int TYPE_PULSE1 = 0x0A; //2bytes relative pulse count
            const int TYPE_PULSE1_ABS = 0x0B;  //4bytes no 0->0xFFFFFFFF
            const int TYPE_EXT_TEMP1 = 0x0C;  //2bytes -3276.5C-->3276.5C
            const int TYPE_EXT_DIGITAL = 0x0D;  //1bytes value 1 or 0
            const int TYPE_EXT_DISTANCE = 0x0E;  //2bytes distance in mm
            const int TYPE_ACC_MOTION = 0x0F;  //1byte number of vibration/motion
            const int TYPE_IR_TEMP = 0x10;  //2bytes internal temp 2bytes external temp -3276.5C-->3276.5C
            const int TYPE_OCCUPANCY = 0x11;  //1byte data
            const int TYPE_WATERLEAK = 0x12;  //1byte data 0-255 
            const int TYPE_GRIDEYE = 0x13;  //65byte temperature data 1byte ref+64byte external temp

            for (int i = 0; i < data.Length; i++)
            {
                switch (data[i])
                {
                    case TYPE_TEMP: //Temperature
                        var temp = (data[i + 1] << 8) | (data[i + 2]);
                        temp = bin16dec(temp);
                       _temperature =  temp / 10.0;
                        i += 2;
                        break;
                    case TYPE_RH: //Humidity
                        var rh = (data[i + 1]);
                        _humidity =  rh;
                        i += 1;
                        break;
                    case TYPE_ACC: //Acceleration
                        //result += "x = " + bin8dec(data[i + 1]) + "\n";
                        //result += "y = " + bin8dec(data[i + 2]) + "\n";
                        //result += "z = " + bin8dec(data[i + 3]) + "\n";
                        i += 3;
                        break;
                    case TYPE_LIGHT: //Light
                        var light = (data[i + 1] << 8) | (data[i + 2]);
                        _light = light;
                        i += 2;
                        break;
                    case TYPE_MOTION: //Motion sensor(PIR)
                        var motion = (data[i + 1]);
                        _motion = motion;
                        i += 1;
                        break;
                    case TYPE_CO2: //CO2
                        var co2 = (data[i + 1] << 8) | (data[i + 2]);
                        _co2 = co2;
                        i += 2;
                        break;
                    case TYPE_VDD: //Battery level
                        var vdd = (data[i + 1] << 8) | (data[i + 2]);
                        _batteryLevel=  vdd / 1000.0;
                        i += 2;
                        break;
                    case TYPE_ANALOG1: //Analog input 1
                        var analog1 = (data[i + 1] << 8) | (data[i + 2]);
                        _analog1 = analog1;
                        i += 2;
                        break;
                    case TYPE_GPS: //gps
                        //result += "Lat = " + ((data[i + 1] << 16) | (data[i + 2] << 8) | (data[i + 3])) + "\n";
                        //result += "Long = " + ((data[i + 4] << 16) | (data[i + 5] << 8) | (data[i + 6])) + "\n";
                        i += 6;
                        break;
                    case TYPE_PULSE1: //Pulse input 1
                        var pulse1 = (data[i + 1] << 8) | (data[i + 2]);
                        //result += "Pulse = " + pulse1 + "\n";
                        i += 2;
                        break;
                    case TYPE_PULSE1_ABS: //Pulse input 1 absolute value
                        var pulseAbs = (data[i + 1] << 24) | (data[i + 2] << 16) | (data[i + 3] << 8) | (data[i + 4]);
                        //result += "PulseAbs = " + pulseAbs + "\n";
                        i += 4;
                        break;
                    case TYPE_EXT_TEMP1: //External temp
                        var tamp = (data[i + 1] << 8) | (data[i + 2]);
                        temp = bin16dec(tamp);
                        //result += "ExtTemp = " + tamp + "\n";
                        i += 2;
                        break;
                    case TYPE_EXT_DIGITAL: //Digital input
                        var digital = (data[i + 1]);
                        //result += "Digital = " + digital + "\n";
                        i += 1;
                        break;
                    case TYPE_EXT_DISTANCE: //Distance sensor input 
                        var distance = (data[i + 1] << 8) | (data[i + 2]);
                        //result += "Dist = " + distance + "\n";
                        i += 2;
                        break;
                    case TYPE_ACC_MOTION: //Acc motion
                        var motion1 = (data[i + 1]);
                        //result += "Acc motion = " + motion1 + "\n";
                        i += 1;
                        break;
                    case TYPE_IR_TEMP: //IR temperature
                        var iTemp = (data[i + 1] << 8) | (data[i + 2]);
                        iTemp = bin16dec(iTemp);
                        var eTemp = (data[i + 3] << 8) | (data[i + 4]);
                        eTemp = bin16dec(eTemp);
                        //result += "irInternalTemp = " + iTemp + "\n";
                        //result += "irExternalTemp = " + eTemp + "\n";
                        i += 4;
                        break;
                    case TYPE_OCCUPANCY: //Body occupancy
                        var occupancy = (data[i + 1]);
                        //result += "Occupency = " + occupancy + "\n";
                        i += 1;
                        break;
                    case TYPE_WATERLEAK: //Water leak
                        var waterleak = (data[i + 1]);
                        //result += "Waterleak = " + waterleak + "\n";
                        i += 1;
                        break;
                    case TYPE_GRIDEYE: //Grideye data
                        i += 65;
                        break;
                    default://somthing is wrong with data
                        i = data.Length;
                        break;
                }
            }
        }

        public string toString()
        {
            string str = "*************************************\n";
            str += base.toString("Elsys");
            str += "Mesurments :\n";
            str += "Temperature : " + _temperature + "\n";
            str += "Humidity : " + _humidity + "\n";
            str += "Light : " + _light + "\n";
            str += "Motion : " + _motion + "\n";
            str += "Co2 : " + _co2 + "\n";
            str += "Battery : " + _batteryLevel + "\n";
            return str + "*************************************\n";
        }
    }
}
