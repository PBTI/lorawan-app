using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace CIT1
{
    class LoraWanApp
    {
        static int port = 12124;
        static string ip = "157.190.53.104";
        static string topic = "application/#";
        static private List<int> elsys = new List<int>();
        static private List<int> doors = new List<int>();
        static Stopwatch stopwatch1 = new Stopwatch();
        static Stopwatch stopwatch2 = new Stopwatch();
        static Stopwatch savetimer = new Stopwatch();
        private static readonly Mutex m = new Mutex();
        private static int elsyssensorNumber = 20;
        private static int doorsensorNumber = 50;
        private static SortedDictionary<int, string> elsysLocation = new SortedDictionary<int, string>() ;
        private static SortedDictionary<int, string> doorLocation = new SortedDictionary<int, string>();
        private static SortedDictionary<int, DateTime> elsystime = new SortedDictionary<int, DateTime>();
        private static SortedDictionary<int, DateTime> doortime = new SortedDictionary<int, DateTime>();
        private static SortedDictionary<int, ConnectionData> elsysCo = new SortedDictionary<int, ConnectionData>();
        private static SortedDictionary<int, ConnectionData> doorCo = new SortedDictionary<int, ConnectionData>();
        private static StreamWriter file = new StreamWriter("log.txt", true);
        public static readonly AutoResetEvent ResetEvent = new AutoResetEvent(false);


        static void Main(string[] args)
        {
            file.AutoFlush = true;
            if (!File.Exists("config.txt"))
            {
                File.Create("config.txt");
            }
            Console.WriteLine("------------------------------------------");
            DecodeConfig();
            var sortedDict = from entry in elsysLocation orderby entry.Value ascending select entry;
            Console.WriteLine("------------------------------------------");
            foreach (var element in sortedDict)
            {
                try
                {
                    Console.WriteLine("elsys_{0} is in: {1} , last seen: {2}",element.Key, element.Value,elsystime[element.Key]);
                }catch(KeyNotFoundException)
                {
                    Console.WriteLine("elsys_{0} is in: {1} , Never seen Before", element.Key, element.Value);
                }
            }
            sortedDict = from entry in doorLocation orderby entry.Value ascending select entry;
            foreach (var element in sortedDict)
            {
                try
                {
                    Console.WriteLine("ascoel_{0} is in: {1} , last seen: {2}", element.Key, element.Value, doortime[element.Key]);
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine("ascoel_{0} is in: {1} , Never seen Before", element.Key, element.Value);
                }
            }
            Console.WriteLine("------------------------------------------");
            //Connection:
            MqttClient client = new MqttClient(IPAddress.Parse(ip), port, false, null, null, MqttSslProtocols.None);
            // register to message received
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
            Task childTask = Task.Factory.StartNew(() =>
            {
                savetime();
                ResetEvent.Set();
            });
            // subscribe to the topic "/home/temperature" with QoS 2
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            stopwatch1.Start();
            stopwatch2.Start();
            savetimer.Start();

            ResetEvent.WaitOne();
        }

        private static void savetime()
        {
            while (true)
            {
                if (savetimer.Elapsed.Minutes > 8)
                {
                    if (File.Exists("config.txt"))
                    {
                        var input = File.ReadAllLines("config.txt");
                        int i = 0;
                        List<string> line = new List<string>();
                        bool elsyscheck = false;
                        bool ascoelcheck = false;
                        bool type = false;
                        m.WaitOne(); //TODO: reduce file flow to decrease critical code segment length
                        try
                        {
                            while (i < input.Length)
                            {
                                switch (input[i])
                                {
                                    case "ElapsedTime:elsys":
                                        elsyscheck = true;
                                        type = true;
                                        line.Add("\n" + input[i]);
                                        foreach (var element in elsystime)
                                        {
                                            line.Add(element.Key + "!" + element.Value);
                                        }
                                        break;
                                    case "ElapsedTime:ascoel":
                                        ascoelcheck = true;
                                        type = true;
                                        line.Add("\n" + input[i]);
                                        foreach (var element in doortime)
                                        {
                                            line.Add(element.Key + "!" + element.Value);
                                        }
                                        break;
                                    default:
                                        if (type == false)
                                            line.Add(input[i]);
                                        try
                                        {
                                            Int32.Parse(input[i].Split('!')[0]);
                                        }
                                        catch (FormatException)
                                        {
                                            type = false;
                                        }
                                        break;
                                    }
                                    i++;
                                }
                                if (!ascoelcheck)
                                {
                                    line.Add("ElapsedTime:ascoel");
                                    foreach (var element in doortime)
                                    {
                                        line.Add(element.Key + "!" + element.Value);
                                    }
                                }
                                if (!elsyscheck)
                                {
                                    line.Add("ElapsedTime:elsys");
                                    foreach (var element in elsystime)
                                    {   
                                        line.Add(element.Key + "!" + element.Value);
                                    }
                                }
                            File.WriteAllLines("output.txt", line);
                            File.Replace("output.txt", "config.txt", null);
                        }
                        finally
                        {
                            m.ReleaseMutex();
                        }
                    }
                    else
                    {
                        using (var output = new StreamWriter("config.txt"))
                        {
                            output.WriteLine("sensor:elsys:20\nsensor:ascoel:50");
                            Console.WriteLine("++++++++++++++++++++++++NO CONFIG FILE!! GENEATING ONE++++++++++++++++++++++++++++++++++++++++");
                        }
                    }
                    savetimer.Restart();
                }
            }
        }

        private static void DecodeConfig()
        {
            if(new FileInfo("config.txt").Length == 0)
            {
                Console.WriteLine("No Config file - Using default config");
                return;
            }
            string[] lines = File.ReadAllLines("config.txt");
            string[] buff;
            for( int i = 0; i< lines.Length; i++)
            {
                buff = lines[i].Split(':');
                switch(buff[0])
                {
                    case "sensor":
                        switch(buff[1])
                        {
                            case "elsys":
                                elsyssensorNumber = Int32.Parse(buff[2]);
                                Console.WriteLine("Setting elsys for {0} sensors", elsyssensorNumber);
                                break;
                            case "ascoel":
                                doorsensorNumber = Int32.Parse(buff[2]);
                                Console.WriteLine("Setting ascoel for {0} sensors", doorsensorNumber);
                                break;
                            default:
                                Console.WriteLine("ERROR: Did not recognise sensor name");
                                break;
                        }
                        break;
                    case "Location":
                        bool flag = true;
                        switch (buff[1])
                        {
                            case "elsys":
                                flag = true;
                                while (flag && i < lines.Length-1)
                                {
                                    i++;
                                    buff = lines[i].Split(':');
                                    try
                                    {
                                        int number = Int32.Parse(buff[0]);
                                        if(elsysLocation.ContainsKey(number))
                                        {
                                            Console.WriteLine("ERROR: A sensor is placed a two places at the same time, please check config file for elsys_" + number);
                                        }else
                                        {
                                            elsysLocation.Add(number, buff[1]);
                                        }
                                    }
                                    catch (FormatException)
                                    {
                                        i--;
                                        flag = false;   
                                    }
                                }
                                break;
                            case "ascoel":
                                flag = true;
                                while (flag && i < lines.Length - 1)
                                {
                                    i++;
                                    buff = lines[i].Split(':');
                                    try
                                    {
                                        int number = Int32.Parse(buff[0]);
                                        if (doorLocation.ContainsKey(number))
                                        {
                                            Console.WriteLine("ERROR: A sensor is placed a two places at the same time, please check config file for ascoel_" + number);
                                        }
                                        else
                                        {
                                            doorLocation.Add(number, buff[1]);
                                        }
                                    }
                                    catch (FormatException)
                                    {
                                        i--;
                                        flag = false;
                                    }
                                }
                                break;
                            default:
                                Console.WriteLine("ERROR: Did not recognise sensor name");
                                break;
                        }
                        break;
                    case "":
                        break;
                    case "ElapsedTime":
                        switch(buff[1])
                        {
                            case "elsys":
                                flag = true;
                                while (flag && i < lines.Length - 1)
                                {
                                    i++;
                                    buff = lines[i].Split('!');
                                    try
                                    {
                                        int number = Int32.Parse(buff[0]);
                                        if (elsystime.ContainsKey(number))
                                        {
                                            Console.WriteLine("Time error with sensor {0}", number);
                                        }
                                        else
                                        {
                                            elsystime.Add(number, Convert.ToDateTime(buff[1]));
                                        }
                                    }
                                    catch (FormatException)
                                    {
                                        i--;
                                        flag = false;
                                    }
                                }
                                break;
                            case "ascoel":
                                flag = true;
                                while (flag && i < lines.Length - 1)
                                {
                                    i++;
                                    buff = lines[i].Split('!');
                                    try
                                    {
                                        int number = Int32.Parse(buff[0]);
                                        if (doortime.ContainsKey(number))
                                        {
                                            Console.WriteLine("Time error with sensor {0}", number);
                                        }
                                        else
                                        {
                                            doortime.Add(number, Convert.ToDateTime(buff[1]));
                                        }
                                    }
                                    catch (FormatException)
                                    {
                                        i--;
                                        flag = false;
                                    }
                                }
                                break;
                            default:
                                Console.WriteLine("ERROR: Did not recognise sensor name");
                                break;
                        }
                        break;
                    default:
                        Console.WriteLine("ERROR: Did not recognise keyword:{0}", buff[0]);
                        break;
                }
            }
        }

        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            byte[] byteArray = e.Message;
            Encoding encoder = Encoding.GetEncoding("iso-8859-1");
            var packet = encoder.GetString(byteArray);
            string[] parts = e.Topic.Split('/');
            string[] extractedData = packet.Split(',');
            Task childTask;
            if (parts.Length > 1)
            {
                childTask = Task.Factory.StartNew(() =>
                 {
                     switch (parts[1])
                     {
                         case "7":
                             elsysDecode(extractedData);
                             break;
                         case "8":
                             doorsDecode(extractedData);
                             break;
                         default:
                             m.WaitOne();
                             try
                             {
                                Console.WriteLine("Sensor nor recognised : " + parts[1]);
                                file.WriteLine("Sensor nor recognised : " + parts[1]);
                                file.WriteLine("Message received:" + extractedData );
                             }
                             finally
                             {
                                 m.ReleaseMutex();
                             }
                             break;
                     }
                 });
            }
        }

        private static string convertion( string[] extractedData)
        {
            byte[] convertedByte = Convert.FromBase64String(((extractedData[extractedData.Length - 1].Split('}'))[0].Split(':')[1]).Split('"')[1]);
            string hex = BitConverter.ToString(convertedByte).Replace("-", string.Empty);
            return hex;
        }

        private static void doorsDecode(string[] input)
        {
            for (int i = 0; i < input.Length; i++)
                if((input[i].Split(':'))[0] == "\"fPort\"")
                    switch(input[i].Split(':')[1])
                    {
                        case "30":
                            string[] bf = setInformations(input);
                            byte[] data = StringToByteArray(convertion(input));
                            string result = bf[0] + "\nMesurements : \n";

                            result += "Event : " + bin8dec(data[0]); //+ "\n";
                            switch (data[0])
                            {
                                case 0:
                                    result += " (Closing)\n";
                                    break;
                                case 1:
                                    result += " (Opening)\n";
                                    break;
                            }
                            result += "Counter value : " + bin16dec((data[1] << 8) | (data[2])) + "\n";

                            float buff = BitConverter.ToSingle(data, 3);
                            result += "Temperature : " + buff + "\n";
                            buff = BitConverter.ToSingle(data, 7);
                            result += "Humidity : " + buff + "\n";
                            m.WaitOne();
                            try
                            {
                                int number = Int32.Parse(bf[2]);
                                if (doortime.ContainsKey(number))
                                {
                                    TimeSpan duration = doortime[number] - DateTime.Now;
                                    result += "\nLast time seen : " + doortime[number] + " | elapsed time : " + duration;
                                    doortime[number] = DateTime.Now;
                                }
                                else
                                {
                                    result += "\n First time Seen !";
                                    doortime.Add(number, DateTime.Now);
                                }
                                Console.WriteLine(result + "\n***********************************");
                                file.WriteLine(result + "\n***********************************");
                                if (bf[1] != "")
                                    using (StreamWriter sw = File.AppendText("healthdoor.txt"))
                                    {
                                        sw.WriteLine(bf[1]);
                                    }
                            }
                            finally
                            {
                                m.ReleaseMutex();
                            }
                            break;
                        case "9":
                            bf = setInformations(input);
                            byte[] data2 = StringToByteArray(convertion(input));
                            string result2 =  bf[0]+  "\nAlive : \n";

                            if((data2[0] & 0x80) == 0)
                            {
                                result2 += "Battery :  (3.6V Lithium-thionyl)" + bin8dec(data2[0] & 0x7F) + " %"+ "=> " + (2.1+(3.6-2.1)* bin8dec(data2[0] & 0x7F)/100.0) + "V" + "\n";

                            }
                            else
                            {
                                result2 += "Battery :  (3.0V Alkaline Battery) : " + bin8dec(data2[0] & 0x7F) + "% "+ "=> " + (2.1 + (3.0 - 2.1) * bin8dec(data2[0] & 0x7F) / 100.0) + "V" + "\n";
                            }
                            int temp = bin8dec(data2[1]);
                            result2 += "Event : "; //+ "\n";
                            if ((temp & 1 << 4) == 1)
                            {
                                if ((temp & 1 << 3) == 1)
                                {
                                    result2 += "Line Open,"; //+ "\n";
                                }
                                else
                                {
                                    result2 += "Short circuit,"; //+ "\n";
                                }
                            }
                            else
                            {
                                if ((temp & 1 << 3) == 1)
                                {
                                    result2 += "Alarm,"; //+ "\n";
                                }
                                else
                                {
                                    result2 += "OK,"; //+ "\n";
                                }
                            }
                            if((temp & 1 << 2) == 1)
                            {
                                result2 += "LOW BATTERY EVENT,"; //+ "\n";
                            }
                            else
                            {
                                result2 += "Battery OK,"; //+ "\n";
                            }
                            if ((temp & 1 << 1) == 1)
                            {
                                result2 += "Tamper Alarm,"; //+ "\n";
                            }else
                            {
                                result2 += "Tamper no Alarm,"; //+ "\n";
                            }
                            if ((temp & 1 << 0) == 1)
                            {
                                result2 += "Intrusion Alarm"; //+ "\n";
                            }
                            else
                            {
                                result2 += "Intrusion no Alarm"; //+ "\n";
                            }
                            float buff2 = BitConverter.ToSingle(data2, 2);
                            result2 += "\nTemperature : " + buff2 + "\n";
                            buff2 = BitConverter.ToSingle(data2, 6);
                            result2 += "Humidity : " + buff2 + "\n";
                            m.WaitOne();
                            try
                            {
                                int number = Int32.Parse(bf[2]);
                                if (doortime.ContainsKey(number))
                                {
                                    TimeSpan duration = doortime[number] - DateTime.Now;
                                    result2 += "\nLast time seen : " + doortime[number] + " | elapsed time : " + duration;
                                    doortime[number] = DateTime.Now;
                                }
                                else
                                {
                                    result2 += "\n First time Seen !";
                                    doortime.Add(number, DateTime.Now);
                                }
                                if (bf[1] != "")
                                    using (StreamWriter sw = File.AppendText("healthdoor.txt"))
                                    {
                                        sw.WriteLine(bf[1]);
                                    }
                                file.WriteLine(result2 + "\n***********************************");
                                Console.WriteLine(result2 + "\n***********************************");
                            }
                            finally
                            {
                                m.ReleaseMutex();
                            }
                            break;
                        default:
                            m.WaitOne();
                            try
                            {
                                file.WriteLine(setInformations(input) + "Not yet implemented, port must be 9 or 30" + " port received : " + input[i].Split(':')[1]);
                                Console.WriteLine("Not yet implemented, port must be 9 or 30" + " port received : " + input[i].Split(':')[1]);
                            }
                            finally
                            {
                                m.ReleaseMutex();
                            }
                            return;
                    }
        }

        private static void elsysDecode(string[] input)
        {
            
            byte[] data = StringToByteArray(convertion(input));
            string[] bf = setInformations(input);
            string result = bf[0] + "\nMesurements :\n";
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
                        result += "temperature = " + temp/10.0 + "\n";
                        i += 2;
                        break;
                    case TYPE_RH: //Humidity
                        var rh = (data[i + 1]);
                        result += "Humidity = " + rh+ "%" + "\n";
                        i += 1;
                        break;
                    case TYPE_ACC: //Acceleration
                        result += "x = " + bin8dec(data[i + 1]) + "\n";
                        result += "y = " + bin8dec(data[i + 2]) + "\n";
                        result += "z = " + bin8dec(data[i + 3]) + "\n";
                        i += 3;
                        break;
                    case TYPE_LIGHT: //Light
                        var light = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Light = " + light + "\n";
                        i += 2;
                        break;
                    case TYPE_MOTION: //Motion sensor(PIR)
                        var motion = (data[i + 1]);
                        result += "Motion = " + motion + "\n";
                        i += 1;
                        break;
                    case TYPE_CO2: //CO2
                        var co2 = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Co2 = " + co2 + "\n";
                        i += 2;
                        break;
                    case TYPE_VDD: //Battery level
                        var vdd = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Battery = " + vdd/1000.0 + "\n";
                        i += 2;
                        break;
                    case TYPE_ANALOG1: //Analog input 1
                        var analog1 = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Analog1 = " + analog1 + "\n";
                        i += 2;
                        break;
                    case TYPE_GPS: //gps
                        result += "Lat = " + ((data[i + 1] << 16) | (data[i + 2] << 8) | (data[i + 3])) + "\n";
                        result += "Long = " + ((data[i + 4] << 16) | (data[i + 5] << 8) | (data[i + 6])) + "\n";
                        i += 6;
                        break;
                    case TYPE_PULSE1: //Pulse input 1
                        var pulse1 = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Pulse = " + pulse1 + "\n";
                        i += 2;
                        break;
                    case TYPE_PULSE1_ABS: //Pulse input 1 absolute value
                        var pulseAbs = (data[i + 1] << 24) | (data[i + 2] << 16) | (data[i + 3] << 8) | (data[i + 4]);
                        result += "PulseAbs = " + pulseAbs + "\n";
                        i += 4;
                        break;
                    case TYPE_EXT_TEMP1: //External temp
                        var tamp = (data[i + 1] << 8) | (data[i + 2]);
                        temp = bin16dec(tamp);
                        result += "ExtTemp = " + tamp + "\n";
                        i += 2;
                        break;
                    case TYPE_EXT_DIGITAL: //Digital input
                        var digital = (data[i + 1]);
                        result += "Digital = " + digital + "\n";
                        i += 1;
                        break;
                    case TYPE_EXT_DISTANCE: //Distance sensor input 
                        var distance = (data[i + 1] << 8) | (data[i + 2]);
                        result += "Dist = " + distance + "\n";
                        i += 2;
                        break;
                    case TYPE_ACC_MOTION: //Acc motion
                        var motion1 = (data[i + 1]);
                        result += "Acc motion = " + motion1 + "\n";
                        i += 1;
                        break;
                    case TYPE_IR_TEMP: //IR temperature
                        var iTemp = (data[i + 1] << 8) | (data[i + 2]);
                        iTemp = bin16dec(iTemp);
                        var eTemp = (data[i + 3] << 8) | (data[i + 4]);
                        eTemp = bin16dec(eTemp);
                        result += "irInternalTemp = " + iTemp + "\n";
                        result += "irExternalTemp = " + eTemp + "\n";
                        i += 4;
                        break;
                    case TYPE_OCCUPANCY: //Body occupancy
                        var occupancy = (data[i + 1]);
                        result += "Occupency = " + occupancy + "\n";
                        i += 1;
                        break;
                    case TYPE_WATERLEAK: //Water leak
                        var waterleak = (data[i + 1]);
                        result += "Waterleak = " + waterleak + "\n";
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
            m.WaitOne();
            try
            {
                int number = Int32.Parse(bf[2]);
                if (elsystime.ContainsKey(number))
                {
                    TimeSpan duration = elsystime[number] - DateTime.Now;
                    result += "\nLast time seen : " + elsystime[number] + " | elapsed time : " + duration;
                    elsystime[number] = DateTime.Now;
                }
                else
                {
                    result += "\n First time Seen !";
                    elsystime.Add(number, DateTime.Now);
                }
                file.WriteLine(result + "\n***********************************");
                Console.WriteLine(result + "\n***********************************");
                if (bf[1] != "")
                    using (StreamWriter sw = File.AppendText("healthelsys.txt"))
                    {
                        sw.WriteLine(bf[1]);
                    }
            }
            finally
            {
                m.ReleaseMutex();
            }
        }

        private static string[] setInformations(string[] input)
        {
            int counter = 1;
            string info =""; //"Time left until log writing : " +(5.0-savetimer.Elapsed.Minutes) + "m";
            string tamp = "";
            int type = 0;
            int rssi = 1000;
            float snr = 1000;
            string[] result = new string[3];
            for(int i = 0; i < input.Length;i++)
            {
                string[] buff = input[i].Split(':');
                switch (buff[0])
                {
                    case "\"nodeName\"":
                        tamp = nodeNameCount(buff[1]);
                        if(buff[1].Contains("elsys"))
                            type = 1;
                        if (buff[1].Contains("ascoel"))
                            type = 2;
                        result[2] = Regex.Replace(buff[1], "[^0-9]+", string.Empty);
                        info += tamp;
                        info += "\n***********************************\n" + "Node : " + buff[1] + setLocation(buff[1]) +"\n";
                        break;
                    case "\"devEUI\"":
                        info += "EUI : " + buff[1] + "\n";
                        break;
                    case "\"rxInfo\"":
                        info += "-----\nmac 1 : " + buff[2] + " (" + macAssociation(buff[2]) + ")" + "\n";
                        break;
                    case "\"time\"":
                        info += "time : " + buff[1] + ":" + buff[2] + ":" + buff[3] + "\n";
                        break;
                    case "\"rssi\"":
                        rssi = Int32.Parse(buff[1]);
                        info += "rssi" + " : " + buff[1] + "\n";
                        break;
                    case "\"loRaSNR\"":
                        //var r = new Regex(@"[0-9]+\.[0-9]+");
                        //var mc = r.Matches(buff[1].TrimEnd(']', '}'));
                        //var matches = new Match[mc.Count];
                        //mc.CopyTo(matches, 0);
                        snr = float.Parse(buff[1].Replace('.',',').TrimEnd(']', '}'));  
                        info += "SNR : " + buff[1].TrimEnd(']', '}') + "\n-----" + "\n";
                        break;
                    case "{\"mac\"":
                        info += "mac " + (++counter) + " : " + buff[1] + " (" + macAssociation(buff[1]) + ")" + "\n";
                        break;
                    case "\"txInfo\"":
                        info += "TxInfo" + " : " + Regex.Replace(buff[1], "{", string.Empty) +" : "+ buff[2] + "\n";
                        break;
                    case "\"dataRate\"":
                        info += "DataRate" + " : " + Regex.Replace(buff[1], "{", string.Empty) +" : "+ buff[2] + ",";
                        break;
                    case "\"bandwidth\"":
                        info += "bandwitdth" + " : " + Regex.Replace(buff[1], "{", string.Empty)+ ", ";
                        break;
                    case "\"spreadFactor\"":
                        info += "SpreadFactor" + " : " + Regex.Replace(buff[1], "}", string.Empty) + "\n";
                        break;
                    case "\"adr\"":
                        info += "adr" + " : "+ buff[1] + ", ";
                        break;
                    case "\"codeRate\"":
                        info += "CodeRate" + " : " + Regex.Replace(buff[1], "}", string.Empty) + "\n-----\n";
                        break;
                }
                if(snr != 1000 && rssi != 1000)
                {
                    switch (type)
                    {
                        case 1:
                            if (elsysCo.ContainsKey(Int32.Parse(result[2])))
                                elsysCo[Int32.Parse(result[2])].newOccurence(rssi, snr);
                            else
                                elsysCo.Add(Int32.Parse(result[2]), new ConnectionData(rssi, snr));
                            break;
                        case 2:
                            if (doorCo.ContainsKey(Int32.Parse(result[2])))
                                doorCo[Int32.Parse(result[2])].newOccurence(rssi, snr);
                            else
                                doorCo.Add(Int32.Parse(result[2]), new ConnectionData(rssi, snr));
                            break;
                    }
                    snr = 1000;
                    rssi = 1000;
                }
            }
            result[0] = info;
            result[1] = tamp;
            return result;
        }

        private static string setLocation(string v)
        {
            string[] buff = v.Split('_');
            string tamp = "";
            switch (buff[1])
            {
                case "ascoel":
                    if (doorLocation.TryGetValue(Int32.Parse(Regex.Replace(buff[buff.Length - 1], "\"", string.Empty)), out tamp))
                        return " (" + tamp + ") ";
                    return " (?) ";
                case "elsys":
                    if(elsysLocation.TryGetValue(Int32.Parse(Regex.Replace(buff[buff.Length - 1], "\"", string.Empty)), out tamp))
                        return " (" + tamp + ") ";
                    return " (?) ";
                default:
                    Console.WriteLine("????????????????????????????????????????????????????????????????????????????????");
                    break;
            }
            return " (?) ";
        }

        private static string nodeNameCount(string v)
        {
            string result = "!!!!!!!!!!!!!!!!!!\nMissing : " + DateTime.Now +"\n";
            if ((v.Split('_')[1] ) == "ascoel")
            {
                result += "ascoel_report:\n";
                if (stopwatch2.Elapsed.Minutes >= 55 /*|| (doors.FindIndex(item => item == Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty))) < 0)*/)
                {
                    stopwatch2.Restart();
                    doors.Sort();
                    int count = 1;
                    int i = 0;
                    for (i = 0; i < doors.Count; i++)
                    {
                        while (doors[i] > count)
                        {
                            result += "ascoel_" + count + setLocation("sen_ascoel_lrth_" +count) +" Last seen : " + (doortime.ContainsKey(count) ? (doortime[count].ToString() + " => " + (DateTime.Now - doortime[count]) + " ago") : "Never") +  "\n";
                            if (doorCo.ContainsKey(count))
                                result += "Average RSSI : " + doorCo[count].Rrsi + "  | Average SNR : " + doorCo[count].Snr + "\n";
                            count++;
                        }
                        count++;
                    }
                    while (count < doorsensorNumber + 1)
                    {
                        result += "ascoel" + count + setLocation("sen_ascoel_lrth_" + count) + " Last seen : " + (doortime.ContainsKey(count) ? (doortime[count].ToString() + " => " + (DateTime.Now - doortime[count]) + " ago") : "Never") + "\n";
                        if (doorCo.ContainsKey(count))
                            result += "Average RSSI : " + doorCo[count].Rrsi + "  | Average SNR : " + doorCo[count].Snr + "\n";
                        count++;
                    }
                    doors.Clear();
                    return result + "\n!!!!!!!!!!!!!!!!!!\n";
                }
                if ((doors.FindIndex(item => item == Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty))) < 0))
                    doors.Add(Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty)));
            }
            else
            {
                result += "elsys_report:\n";
                if (stopwatch1.Elapsed.Minutes >= 6 /*|| (elsys.FindIndex(item => item == Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty))) > -1)*/)
                {
                    stopwatch1.Restart();
                    elsys.Sort();
                    int count = 1;
                    int i = 0;
                    for (i = 0; i < elsys.Count; i++)
                    {
                        while (elsys[i] > count)
                        {
                            result += "elsys_" + count + setLocation("sen_elsys_"+count) + " Last seen : " +(elsystime.ContainsKey(count)?(elsystime[count].ToString()+" => " + ( DateTime.Now - elsystime[count]) +" ago"):"Never") + "\n";
                            if(elsysCo.ContainsKey(count))
                                result += "Average RSSI : " + elsysCo[count].Rrsi + "  | Average SNR : " + elsysCo[count].Snr + "\n";
                            count++;
                        }
                        count++;
                    }
                    while (count < elsyssensorNumber + 1)
                    {
                        result += "elsys" + count + setLocation("sen_elsys_" + count) + " Last seen : " + (elsystime.ContainsKey(count) ? (elsystime[count].ToString() + " => " + (DateTime.Now - elsystime[count]) + " ago") : "Never") + "\n";
                        if (elsysCo.ContainsKey(count))
                            result += "Average RSSI : " + elsysCo[count].Rrsi + "  | Average SNR : " + elsysCo[count].Snr + "\n";
                        count++;
                    }
                    elsys.Clear();
                    return result + "\n!!!!!!!!!!!!!!!!!!\n";
                }
                if((elsys.FindIndex(item => item == Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty))) < 0))
                    elsys.Add(Int32.Parse(Regex.Replace(v, "[^0-9]+", string.Empty)));

            }
            return "";
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private static int bin16dec( int bin)
        {
            int num = bin & 0xFFFF;
            if ((0x8000 & num) != 0)
                num = -(0x010000 - num);
            return num;
        }

        private static int  bin8dec(int bin)
        {
            var num = bin & 0xFF;
            if ((0x80 & num) != 0)
                num = -(0x0100 - num);
            return num;
        }

        private static string macAssociation(string mac)
        {
            string str = "";
            switch(mac)
            {
                case "\"b827ebfffe82bbc9\"":
                    str = "CIT block B building(imst lite gw)";
                    break;
                case "\"024b08fffe0e0bd0\"":
                    str = "CIT admin building (kerlink gw)";
                    break;
                case "\"84eb18fffee6c342\"":
                    str = "CIT nimbus building (lorrier gw)";
                    break;
                case "\"b827ebfffe416a34\"":
                    str = "CIT nimbus building indoor (imst lite gw)";
                    break;
            }
            return str;
        }
    }
}
