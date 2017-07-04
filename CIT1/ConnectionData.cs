using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIT1
{
    class ConnectionData
    {
        private int _rrsi;
        private float _snr;
        private int _counter;


        public ConnectionData(int rrsi, float snr)
        {
            _rrsi   = rrsi;
            _snr    = snr;
            _counter = 1;
        }

        public void newOccurence(int rrsi, float snr)
        {
            _rrsi += rrsi;
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

        public float Rrsi
        {
            get
            {
                return (float)_rrsi/_counter;
            }

            set
            {
                _rrsi = (int)value;
            }
        }
    }
}
