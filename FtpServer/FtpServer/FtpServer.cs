using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using log4net;

namespace FtpServer
{
    public class FtpServer : IDisposable
    {
        #region fields & properties
        ILog m_log = LogManager.GetLogger(typeof(FtpServer));

        private bool m_disposed = false;
        private bool m_listening = false;

        private TcpListener m_listener;
        public List<ClientConnection> ActiveConnections { get; private set; }

        private IPEndPoint m_localEndPoint;
        #endregion

        #region constructors
        public FtpServer() : this(IPAddress.Any, 21) { }

        public FtpServer(IPAddress ipAddress, int port)
        {
            m_localEndPoint = new IPEndPoint(ipAddress, port);
        }
        #endregion

        public void Start()
        {
            m_listener = new TcpListener(m_localEndPoint);

            m_log.Info("#Version: 1.0");
            m_log.Info("FtpServer started");

            m_listening = true;
            m_listener.Start();

            ActiveConnections = new List<ClientConnection>();

            m_listener.BeginAcceptTcpClient(HandleAcceptTcpClient, m_listener);
        }

        public void Stop()
        {
            m_log.Info("Stopping FtpServer");

            m_listening = false;
            m_listener.Stop();

            m_listener = null;
        }

        #region private methods
        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            if (m_listening)
            {
                m_listener.BeginAcceptTcpClient(HandleAcceptTcpClient, m_listener);

                TcpClient client = m_listener.EndAcceptTcpClient(result);

                ClientConnection connection = new ClientConnection(client);

                ActiveConnections.Add(connection);

                ThreadPool.QueueUserWorkItem(connection.HandleClient, client);
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    Stop();

                    foreach (ClientConnection conn in ActiveConnections)
                        conn.Dispose();
                }
            }
            m_disposed = true;
        }
        #endregion
    }
}
