using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Threading;

namespace FtpClientLibrary
{
    public class FtpTransfert : INotifyPropertyChanged, ICloneable
    {
        #region fields & properties
        public string DestinationPath { get; set; }
        public string Location { get; set; }
        public string Direction { get; set; }
        
        public string Name { get; set; }
        public long Size { get; set; }

        public string Status { get; set; }

        private int m_percentDone;
        public int PercentDone
        {
            get { return m_percentDone; }
            set
            {
                m_percentDone = value;
                OnPropertyChanged("PercentDone");
            }
        }

        private bool m_isStopped;
        public bool IsStopped
        {
            get { return m_isStopped; }
            set
            {
                m_isStopped = value;
                OnPropertyChanged("IsStopped");
            }
        }

        public CancellationTokenSource TokenSource { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region constructors
        public FtpTransfert(CancellationTokenSource p_tokenSource)
        {
            TokenSource = p_tokenSource;
            m_isStopped = false;
        }
        #endregion

        #region PropertyChanged methods
        /// <summary>
        /// Invoque un évènement lors de la mise à jour d'une propriété
        /// </summary>
        /// <param name="p_propertyName">le nom de la propriété mise à jour</param>
        private void OnPropertyChanged(string p_propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(p_propertyName));
        }
        #endregion

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
