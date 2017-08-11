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
using System.Xml;
using System.Xml.Linq;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace CIT1
{
    class LoraWanApp
    {
        //Default config
        static int port = 12124;
        static string ip = "157.190.53.104";
        static string topic = "application/#";
        private static int elsyssensorNumber = 20;
        private static int doorsensorNumber = 50;
        //Sensors data
        static private SortedDictionary<int, Elsys> elsys = new SortedDictionary<int, Elsys>();
        static private SortedDictionary<int, Ascoel> doors = new SortedDictionary<int, Ascoel>();
        //timers
        static Stopwatch stopwatch1 = new Stopwatch();
        static Stopwatch stopwatch2 = new Stopwatch();
        static Stopwatch savetimer = new Stopwatch();
        //mutex
        private static readonly Mutex m = new Mutex();
        //Config file data
        private static SortedDictionary<int, string> elsysLocation = new SortedDictionary<int, string>();
        private static SortedDictionary<int, string> doorLocation = new SortedDictionary<int, string>();
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
                    Console.WriteLine("elsys_{0} is in: {1} , last seen: {2}", element.Key, element.Value, elsys[element.Key].lastseen());
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine("elsys_{0} is in: {1} , Never seen Before", element.Key, element.Value);
                }
            }
            sortedDict = from entry in doorLocation orderby entry.Value ascending select entry;
            foreach (var element in sortedDict)
            {
                try
                {
                    Console.WriteLine("ascoel_{0} is in: {1} , last seen: {2}", element.Key, element.Value, doors[element.Key].lastseen());
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
                                        line.Add(input[i]);
                                        foreach (var element in elsys)
                                        {
                                            line.Add(element.Key + "!" + element.Value.lastseen());
                                        }
                                        break;
                                    case "ElapsedTime:ascoel":
                                        ascoelcheck = true;
                                        type = true;
                                        line.Add(input[i]);
                                        foreach (var element in doors)
                                        {
                                            line.Add(element.Key + "!" + element.Value.lastseen());
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
                                foreach (var element in doors)
                                {
                                    line.Add(element.Key + "!" + element.Value.lastseen());
                                }
                            }
                            if (!elsyscheck)
                            {
                                line.Add("ElapsedTime:elsys");
                                foreach (var element in elsys)
                                {
                                    line.Add(element.Key + "!" + element.Value.lastseen());
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
            if (new FileInfo("config.txt").Length == 0)
            {
                Console.WriteLine("No Config file - Using default config");
                return;
            }
            string[] lines = File.ReadAllLines("config.txt");
            string[] buff;
            for (int i = 0; i < lines.Length; i++)
            {
                buff = lines[i].Split(':');
                switch (buff[0])
                {
                    case "sensor":
                        switch (buff[1])
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
                                while (flag && i < lines.Length - 1)
                                {
                                    i++;
                                    buff = lines[i].Split(':');
                                    try
                                    {
                                        int number = Int32.Parse(buff[0]);
                                        if (elsysLocation.ContainsKey(number))
                                        {
                                            Console.WriteLine("ERROR: A sensor is placed a two places at the same time, please check config file for elsys_" + number);
                                        }
                                        else
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
                        switch (buff[1])
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
                                        if (elsys.ContainsKey(number))
                                        {
                                            Console.WriteLine("Time error with sensor {0}", number);
                                        }
                                        else
                                        {
                                            string temp = "";
                                            elsysLocation.TryGetValue(number, out temp);
                                            elsys.Add(number, new Elsys(number, temp == null ? "(?)" : temp, Convert.ToDateTime(buff[1])));
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
                                        if (doors.ContainsKey(number))
                                        {
                                            Console.WriteLine("Time error with sensor {0}", number);
                                        }
                                        else
                                        {
                                            string temp = "";
                                            doorLocation.TryGetValue(number, out temp);
                                            doors.Add(number, new Ascoel(number, temp == null ? "(?)" : temp, Convert.ToDateTime(buff[1])));
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
            string type = "app";
            LoraPacket packetLora = ProcessPacket(type, packet);
            if (parts.Length > 1)
            {
                childTask = Task.Factory.StartNew(() =>
                 {
                     if (packetLora is AppRxPacket)
                     {
                         int temp = (packetLora as AppRxPacket).nodeNameId;
                         switch (parts[1])
                         {
                             case "7":
                                 if (elsys.ContainsKey(temp))
                                     elsys[temp].update(DateTime.Now, (packetLora as AppRxPacket));
                                 else
                                 {
                                     string str = "";
                                     elsysLocation.TryGetValue(temp, out str);
                                     elsys.Add(temp, new Elsys((packetLora as AppRxPacket), temp, (str == null ? "(?)" : str), DateTime.Now));
                                 }
                                 m.WaitOne();
                                 try
                                 {
                                     Console.WriteLine(elsys[temp].toString());
                                     file.WriteLine(elsys[temp].toString());
                                 }
                                 finally
                                 {
                                     m.ReleaseMutex();
                                 }
                                 break;
                             case "8":
                                 if (doors.ContainsKey(temp))
                                     doors[temp].update(DateTime.Now, (packetLora as AppRxPacket));
                                 else
                                 {
                                     string str = "";
                                     doorLocation.TryGetValue(temp, out str);
                                     doors.Add(temp, new Ascoel((packetLora as AppRxPacket), temp, (str == null ? "(?)" : str), DateTime.Now));
                                 }
                                 m.WaitOne();
                                 try
                                 {
                                     Console.WriteLine(doors[temp].toString());
                                     file.WriteLine(doors[temp].toString());
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
                                     Console.WriteLine("Sensor nor recognised : " + parts[1]);
                                     file.WriteLine("Sensor nor recognised : " + parts[1]);
                                     file.WriteLine("Message received:" + extractedData);
                                 }
                                 finally
                                 {
                                     m.ReleaseMutex();
                                 }
                                 break;
                         }
                     }
                     health();
                 });
            }
        }

        private static void health()
        {
            DateTime now = DateTime.Now;
            TimeSpan diff;
            int i = 0;
            int j = 0;
            string result = "!!!!!!!!!!!!\n";
            if (stopwatch2.Elapsed.Minutes >= 55)
            {
                stopwatch2.Restart();
                result += "Report of : " + now + "\n";
                
                for(i = 0; i < doors.Count; i++)
                {
                    while(doors.ElementAt(i).Value.id() > j)
                    {
                        result += "ascoel_" + j + setLocation("sen_ascoel_lrth_" + j) + " Last seen : " + "Never" + "\n";
                    }
                    diff = now - doors[i].lastseen();
                    if ( diff.Minutes > 55)
                        result += "ascoel_" + j + setLocation("sen_ascoel_lrth_" + j) 
                            + " Last seen : " + doors[i].lastseen() + " => " 
                            + (now - doors[i].lastseen()) 
                            + " Average RSSI : " + doors[i]._connection.Rssi 
                            + " Average SNR : " + doors[i]._connection.Snr
                            + "\n";
                    j++;
                }
                while (j < doorsensorNumber + 1)
                {
                    result += "ascoel_" + j + setLocation("sen_ascoel_lrth_" + j) + " Last seen : " + "Never" + "\n";
                    j++;
                }
                if (result != "")
                    using (StreamWriter sw = File.AppendText("healthdoor.txt"))
                    {
                        sw.WriteLine(result + "!!!!!!!!!!!!\n");
                    }
            }
            else if(stopwatch1.Elapsed.Minutes >= 8)
            {
                stopwatch1.Restart();
                result += "Report of : " + now + "\n";
                for (i = 0; i < elsys.Count; i++)
                {
                    while (elsys.ElementAt(i).Value.id() > j)
                    {
                        result += "elsys_" + j + setLocation("sen_elsys_lrth_" + j) + " Last seen : " + "Never" + "\n";
                    }
                    diff = now - doors[i].lastseen();
                    if (diff.Minutes > 8)
                        result += "elsys_" + j + setLocation("sen_elsys_lrth_" + j)
                            + " Last seen : " + doors[i].lastseen() + " => "
                            + (now - doors[i].lastseen())
                            + " Average RSSI : " + doors[i]._connection.Rssi
                            + " Average SNR : " + doors[i]._connection.Snr
                            + "\n";
                    j++;
                }
                while (j < elsyssensorNumber + 1)
                {
                    result += "elsys_" + j + setLocation("sen_elsys_lrth_" + j) + " Last seen : " + "Never" + "\n";
                    j++;
                }
                if (result != "")
                    using (StreamWriter sw = File.AppendText("healthelsys.txt"))
                    {
                        sw.WriteLine(result + "!!!!!!!!!!!!\n");
                    }
            }
        }

        private static LoraPacket ProcessPacket(string type, string packet)
{
    LoraPacket packetLora = null;

    if (string.IsNullOrEmpty(packet) == true)
        return packetLora;

    XmlDocument xmlDoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode("{packet:" + packet + "}", "packet");
    string xmlString = xmlDoc.DocumentElement.InnerXml;
    XElement Xpacket = XElement.Parse(xmlString);

    if (type == "app")
    {
        packetLora = new AppRxPacket(Xpacket);
    }

    return packetLora;
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
                    if (elsysLocation.TryGetValue(Int32.Parse(Regex.Replace(buff[buff.Length - 1], "\"", string.Empty)), out tamp))
                        return " (" + tamp + ") ";
                    return " (?) ";
                default:
                    Console.WriteLine("????????????????????????????????????????????????????????????????????????????????");
                    break;
            }
            return " (?) ";
        }

    }
}
