using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CIT1
{
    #region packet data defintions
    public class LoraPacket
    {
        public string type;
        public string raw_packet;
        public string value;
        public DateTime my_timestamp;

        public LoraPacket(XElement Xpacket)
        {
            this.type = this.GetType().Name;
            this.raw_packet = Xpacket.ToString();
            this.value = Xpacket.Value;
            this.my_timestamp = DateTime.UtcNow;
        }
    }

    /*
    mqtt topic: application/+/node/+/rx 

    {"applicationID":"7","applicationName":"cit_deployment_pn_elsys","nodeName":"sen_elsys_19","devEUI":"a81758fffe03080d",
    
    "rxInfo":[
    {"mac":"024b08fffe0e0bd0","time":"2017-06-23T13:12:16.573183Z","rssi":-101,"loRaSNR":0.5},
    {"mac":"84eb18fffee6c342","time":"2017-06-23T13:49:50.186247Z","rssi":-112,"loRaSNR":-1},
    {"mac":"b827ebfffe82bbc9","time":"2017-06-23T13:13:33.942369Z","rssi":-118,"loRaSNR":-3}
    ],
    
    "txInfo":
    {"frequency":868300000,"dataRate":{"modulation":"LORA","bandwidth":125,"spreadFactor":7},"adr":true,"codeRate":"4/5"},

    "fCnt":2308,"fPort":5,"data":"AQD2AjAEAMAFAAYAAAcNvg=="}
    */

    public class AppRxPacket : LoraPacket
    {
        // main
        public int appId;
        public string appName;
        public string nodeName;
        public int nodeNameId;
        public string devEui;
        public int fCnt;
        public int fPort;
        public string data;
        // rxinfo
        public List<string> mac = new List<string>();
        public List<string> time = new List<string>();
        public List<double> rssi = new List<double>();
        public List<double> snr = new List<double>();
        // txinfo
        public string frequency;
        public string modulation;
        public int bandwidth;
        public int spreadFactor;
        public bool adr;
        public string codeRate;

        public AppRxPacket(XElement Xpacket)
            : base(Xpacket)
        {
            // main 
            this.appId = Xpacket.Element("applicationID") != null ? Convert.ToInt32(Xpacket.Element("applicationID").Value) : -1;
            this.appName = Xpacket.Element("applicationName") != null ? Xpacket.Element("applicationName").Value : "";
            this.nodeName = Xpacket.Element("nodeName") != null ? Xpacket.Element("nodeName").Value : "";
            try
            {
                this.nodeNameId = Int32.Parse(Regex.Replace(this.nodeName, "[^0-9]+", string.Empty));
            }catch(Exception e)
            {
                Console.WriteLine("!!!!!!!!!!!!!!! ===> " + e);
                Console.WriteLine("!!!!!!!!!!!!!!! ===> " + this.nodeName);
            }
            this.devEui = Xpacket.Element("devEUI") != null ? Xpacket.Element("devEUI").Value : "";
            this.fCnt = Xpacket.Element("fCnt") != null ? Convert.ToInt32(Xpacket.Element("fCnt").Value) : -1;
            this.fPort = Xpacket.Element("fPort") != null ? Convert.ToInt32(Xpacket.Element("fPort").Value) : -1;
            this.data = Xpacket.Element("data") != null ? Xpacket.Element("data").Value : "";

            // rxinfo
            var XrxInfo = Xpacket.Elements("rxInfo");
            if (XrxInfo != null)
            {
                foreach (var xe in XrxInfo)
                {
                    mac.Add(xe.Element("mac").Value);
                    time.Add(xe.Element("time").Value);
                    rssi.Add(Convert.ToDouble(xe.Element("rssi").Value));
                    snr.Add(Convert.ToDouble(xe.Element("loRaSNR").Value.Replace('.',',')));
                }
            }

            // txinfo
            XElement XtxInfo = Xpacket.Element("txInfo");
            if (XtxInfo != null)
            {
                this.frequency = XtxInfo.Element("frequency") != null ? XtxInfo.Element("frequency").Value : "";

                XElement XdataRate = XtxInfo.Element("dataRate");
                if (XdataRate != null)
                {
                    this.modulation = XdataRate.Element("modulation") != null ? XdataRate.Element("modulation").Value : "";
                    this.bandwidth = XdataRate.Element("bandwidth") != null ? Convert.ToInt32(XdataRate.Element("bandwidth").Value) : -1;
                    this.spreadFactor = XdataRate.Element("spreadFactor") != null ? Convert.ToInt32(XdataRate.Element("spreadFactor").Value) : -1;
                }

                this.adr = XtxInfo.Element("adr") != null ? Convert.ToBoolean(XtxInfo.Element("adr").Value) : false;
                this.codeRate = XtxInfo.Element("codeRate") != null ? XtxInfo.Element("codeRate").Value : "";
            }
        }
    }

    /*
    mqtt topic: gateway/+/rx 

    {"rxInfo":
    {"mac":"b827ebfffe82bbc9",
     "time":"2017-06-23T13:13:33.942369Z",
     "timestamp":2477723811,
     "frequency":868300000,
     "channel":1,
     "rfChain":1,
     "crcStatus":1,
     "codeRate":"4/5",
     "rssi":-118,
     "loRaSNR":-3,
     "size":29,
     "dataRate":{"modulation":"LORA","spreadFactor":7,"bandwidth":125}},

     "phyPayload":"QHsg5TOABAkF7Frh/rgWnHH3EYw0nCq4y0HA7bg="}
    */

    public class PhyRxPacket : LoraPacket
    {
        public string mac;
        public string time;
        public long timestamp;
        public double frequency;
        public int channel;
        public int rfChain;
        public int crcStatus;
        public string codeRate;
        public double rssi;
        public double loRaSNR;
        public int size;
        public string modulation;
        public int spreadFactor;
        public int bandwidth;
        public string phyPayload;

        public PhyRxPacket(XElement Xpacket)
            : base(Xpacket)
        {
            XElement XrxInfo = Xpacket.Element("rxInfo");
            if (XrxInfo != null)
            {
                this.mac = XrxInfo.Element("mac") != null ? XrxInfo.Element("mac").Value : "";
                this.time = XrxInfo.Element("time") != null ? XrxInfo.Element("time").Value : "";

                DateTime result = new DateTime(0);
                if (DateTime.TryParse(this.time, out result) == false)
                { }

                this.timestamp = XrxInfo.Element("timestamp") != null ? Convert.ToInt64(XrxInfo.Element("timestamp").Value) : 0;
                this.frequency = XrxInfo.Element("frequency") != null ? Convert.ToDouble(XrxInfo.Element("frequency").Value) : 0;
                this.channel = XrxInfo.Element("channel") != null ? Convert.ToInt32(XrxInfo.Element("channel").Value) : -1;
                this.rfChain = XrxInfo.Element("rfChain") != null ? Convert.ToInt32(XrxInfo.Element("rfChain").Value) : -1;
                this.crcStatus = XrxInfo.Element("crcStatus") != null ? Convert.ToInt32(XrxInfo.Element("crcStatus").Value) : -1;
                this.codeRate = XrxInfo.Element("codeRate") != null ? XrxInfo.Element("codeRate").Value : "";
                this.rssi = XrxInfo.Element("rssi") != null ? Convert.ToDouble(XrxInfo.Element("rssi").Value) : 0;
                this.loRaSNR = XrxInfo.Element("loRaSNR") != null ? Convert.ToDouble(XrxInfo.Element("loRaSNR").Value) : 0;
                this.size = XrxInfo.Element("size") != null ? Convert.ToInt32(XrxInfo.Element("size").Value) : 0;

                XElement XdataRate = XrxInfo.Element("dataRate");
                if (XdataRate != null)
                {
                    this.modulation = XdataRate.Element("modulation") != null ? XdataRate.Element("modulation").Value : "";
                    this.spreadFactor = XdataRate.Element("spreadFactor") != null ? Convert.ToInt32(XdataRate.Element("spreadFactor").Value) : -1;
                    this.bandwidth = XdataRate.Element("bandwidth") != null ? Convert.ToInt32(XdataRate.Element("bandwidth").Value) : -1;
                }
            }
            this.phyPayload = Xpacket.Element("phyPayload") != null ? Xpacket.Element("phyPayload").Value : "";
        }
    }

    /*
    mqtt topic: gateway/+/stats 

    {"mac":"84eb18fffee6c342",
     "time":"2017-06-23T14:49:34+01:00",
     "latitude":51.8869,
     "longitude":-8.53538,
     "altitude":10,
     "rxPacketsReceived":3,
     "rxPacketsReceivedOK":2,
     "txPacketsReceived":0,
     "txPacketsEmitted":0,
     "customData":null}
    */

    public class GwStatsPacket : LoraPacket
    {
        public string mac;
        public string time;
        public double latitude;
        public double longitude;
        public double altitude;
        public int rxPacketsReceived;
        public int rxPacketsReceivedOK;
        public int txPacketsReceived;
        public int txPacketsEmitted;
        public string customData;

        public GwStatsPacket(XElement Xpacket)
            : base(Xpacket)
        {
            this.mac = Xpacket.Element("mac") != null ? Xpacket.Element("mac").Value : "";
            this.time = Xpacket.Element("time") != null ? Xpacket.Element("time").Value : "";

            DateTime result = new DateTime(0);
            if (DateTime.TryParse(this.time, out result) == false)
            { }

            this.latitude = Xpacket.Element("latitude") != null ? Convert.ToDouble(Xpacket.Element("latitude").Value) : 0;
            this.longitude = Xpacket.Element("longitude") != null ? Convert.ToDouble(Xpacket.Element("longitude").Value) : 0;
            this.altitude = Xpacket.Element("altitude") != null ? Convert.ToDouble(Xpacket.Element("altitude").Value) : 0;
            this.rxPacketsReceived = Xpacket.Element("rxPacketsReceived") != null ? Convert.ToInt32(Xpacket.Element("rxPacketsReceived").Value) : -1;
            this.rxPacketsReceivedOK = Xpacket.Element("rxPacketsReceivedOK") != null ? Convert.ToInt32(Xpacket.Element("rxPacketsReceivedOK").Value) : -1;
            this.txPacketsReceived = Xpacket.Element("txPacketsReceived") != null ? Convert.ToInt32(Xpacket.Element("txPacketsReceived").Value) : -1;
            this.txPacketsReceived = Xpacket.Element("txPacketsEmitted") != null ? Convert.ToInt32(Xpacket.Element("txPacketsEmitted").Value) : -1;
            this.customData = Xpacket.Element("customData") != null ? Xpacket.Element("customData").Value : "";
        }
    }
    #endregion
}
