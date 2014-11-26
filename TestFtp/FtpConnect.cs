using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.FtpClient;

namespace TestFtp
{
    public class FtpConnect
    {
        private FtpClient m_client;

        public string DestinationDirectory { get; set; }

        public FtpConnect(string p_host, int p_port, System.Net.NetworkCredential p_credential)
        {
            m_client = new FtpClient();
            m_client.Host = p_host;
            m_client.Port = p_port;
            m_client.Credentials = p_credential;

            m_client.Connect();
        }

        public List<string> getArborescence(string p_path)
        {
            return new List<string>();
        }

        public void Close()
        {
            m_client.Disconnect();
        }
    }
}
