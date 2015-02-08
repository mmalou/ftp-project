using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace FtpClientLibrary
{
    public class ThreadManager : INotifyPropertyChanged
    {
        #region fields & properties

        /// <summary>
        /// Obtient la liste des transferts en cours
        /// </summary>
        public List<Task> CurrentTasks { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Obtient la file des transferts en attente
        /// </summary>
        private Queue<KeyValuePair<Action, CancellationToken>> m_waitingTranferts;

        public Queue<KeyValuePair<Action, CancellationToken>> WaitingTransferts
        {
            get { return m_waitingTranferts; }
            private set
            {
                m_waitingTranferts = value;
            }
        }

        private int m_nbCurrentTransferts;
        /// <summary>
        /// Le nombre de transfert en cours.
        /// </summary>
        public int NbCurrentTransferts
        {
            get { return m_nbCurrentTransferts; }
            private set
            {
                m_nbCurrentTransferts = value;
                OnPropertyChanged("NbCurrentTransferts");
            }
        }

        private int m_maxTransfert;
        /// <summary>
        /// Le nombre maximum de transferts simultanés autorisés.
        /// </summary>
        public int MaxTransfert
        {
            get { return m_maxTransfert; }
            set
            {
                if (value > 10)
                    throw new Exception("Nombre maximum de transferts simultanés : 10.");
                else
                    m_maxTransfert = value;
            }
        }

        #endregion

        #region constructor

        public ThreadManager(int p_maxTransfert)
        {
            m_waitingTranferts = new Queue<KeyValuePair<Action, CancellationToken>>();
            m_maxTransfert = p_maxTransfert;
            m_nbCurrentTransferts = 0;
            CurrentTasks = new List<Task>();

            this.PropertyChanged += PropertyChange;
        }

        #endregion

        #region PropertyChange methods
        private void OnPropertyChanged(string p_propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(p_propertyName));
        }

        private void PropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "NbCurrentTransferts")
            {
                if (m_nbCurrentTransferts < m_maxTransfert && m_waitingTranferts.Count > 0)
                {
                    KeyValuePair<Action, CancellationToken> dequeue = m_waitingTranferts.Dequeue();
                    Add(dequeue.Key, dequeue.Value);
                }
            }
        }
        #endregion

        #region public methods

        /// <summary>
        /// Ajoute un transfert aux transferts courants, ou à la file d'attente si le nombres de transfert en cours est atteint.
        /// </summary>
        /// <param name="p_action">Le transfert à ajouter.</param>
        public void Add(Action p_action, CancellationToken p_token)
        {
            if (m_nbCurrentTransferts < m_maxTransfert)
            {
                m_nbCurrentTransferts++;
                Task transfert = Task.Factory.StartNew(p_action, p_token);

                CurrentTasks.Add(transfert);

                transfert.ContinueWith((c) =>
                {
                    if (NbCurrentTransferts > 0) NbCurrentTransferts--;
                    CurrentTasks.Remove(transfert);
                });
            }
            else
            {
                m_waitingTranferts.Enqueue(new KeyValuePair<Action, CancellationToken>(p_action, p_token));
            }
        }
        #endregion
    }
}
