using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIT1
{
    class ConnectionData
    {
        private int _rssi;
        private float _snr;
        private int _counter;


        public ConnectionData(int rrsi, float snr)
        {
            _rssi   = rrsi;
            _snr    = snr;
            _counter = 1;
        }

        public void newOccurence(int rssi, float snr)
        {
            _rssi += rssi;
            _snr += snr;
            _counter++;
        }
        public float Snr
        {
            get
            {
                return _snr/_counter;
            }

            set
            {
                _snr = value;
            }
        }

        public float Rssi
        {
            get
            {
                return (float)_rssi/_counter;
            }

            set
            {
                _rssi = (int)value;
            }
        }
    }
}
