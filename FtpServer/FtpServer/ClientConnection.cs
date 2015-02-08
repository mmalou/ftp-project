using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using log4net;

namespace FtpServer
{
    public class ClientConnection : IDisposable
    {
        private class DataConnectionOperation
        {
            public Func<NetworkStream, string, string> Operation { get; set; }
            public string Arguments { get; set; }
        }
     
        #region Enums
        private enum TransferType
        {
            Ascii,
            Ebcdic,
            Image,
            Local,
        }

        private enum FormatControlType
        {
            NonPrint,
            Telnet,
            CarriageControl,
        }

        private enum DataConnectionType
        {
            Passive,
            Active,
        }

        private enum FileStructureType
        {
            File,
            Record,
            Page,
        }

        #endregion

        #region fields & properties
        ILog m_log = LogManager.GetLogger(typeof(ClientConnection));

        private bool m_disposed = false;

        private TcpListener m_passiveListener;

        private TcpClient m_controlClient;
        private TcpClient m_dataClient;

        private NetworkStream m_controlStream;
        private StreamReader m_controlReader;
        private StreamWriter m_controlWriter;

        private TransferType m_connectionType = TransferType.Ascii;
        private FormatControlType m_formatControlType = FormatControlType.NonPrint;
        private DataConnectionType m_dataConnectionType = DataConnectionType.Active;
        private FileStructureType m_fileStructureType = FileStructureType.File;

        public string Username { get; private set; }
        private string m_root;
        private string m_currentDirectory;
        private IPEndPoint m_dataEndpoint;
        private IPEndPoint m_remoteEndPoint;

        //FTP over TSL
        private X509Certificate m_cert = null;
        private SslStream m_sslStream;

        private string m_clientIP;

        private User m_currentUser;

        private List<string> m_validCommands;
        #endregion

        #region constructor
        public ClientConnection(TcpClient client)
        {
            m_controlClient = client;

            m_validCommands = new List<string>();
        }
        #endregion

        public void HandleClient(object obj)
        {
            m_remoteEndPoint = (IPEndPoint)m_controlClient.Client.RemoteEndPoint;

            m_clientIP = m_remoteEndPoint.Address.ToString();

            m_controlStream = m_controlClient.GetStream();

            m_controlReader = new StreamReader(m_controlStream);
            m_controlWriter = new StreamWriter(m_controlStream);

            m_controlWriter.WriteLine("220 Service Ready.");
            m_controlWriter.Flush();

            m_validCommands.AddRange(new string[] { "AUTH", "USER", "PASS", "QUIT", "HELP", "NOOP" });

            string line;

            m_dataClient = new TcpClient();

            string renameFrom = null;

            try
            {
                while ((line = m_controlReader.ReadLine()) != null)
                {
                    string response = null;

                    string[] command = line.Split(' ');

                    string cmd = command[0].ToUpperInvariant();
                    string arguments = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                    if (arguments != null && arguments.Trim().Length == 0)
                    {
                        arguments = null;
                    }

                    LogEntry logEntry = new LogEntry
                    {
                        Date = DateTime.Now,
                        CIP = m_clientIP,
                        CSUriStem = arguments
                    };

                    if (!m_validCommands.Contains(cmd))
                    {
                        response = CheckUser();
                    }

                    if (cmd != "RNTO")
                    {
                        renameFrom = null;
                    }

                    if (response == null)
                    {
                        switch (cmd)
                        {
                            case "USER":
                                response = User(arguments);
                                break;
                            case "PASS":
                                response = Password(arguments);
                                logEntry.CSUriStem = "******";
                                break;
                            case "CWD":
                                response = ChangeWorkingDirectory(arguments);
                                break;
                            case "CDUP":
                                response = ChangeWorkingDirectory("..");
                                break;
                            case "QUIT":
                                response = "221 Service closing control connection";
                                break;
                            case "REIN":
                                m_currentUser = null;
                                Username = null;
                                m_passiveListener = null;
                                m_dataClient = null;

                                response = "220 Service ready for new user";
                                break;
                            case "PORT":
                                response = Port(arguments);
                                logEntry.CPort = m_dataEndpoint.Port.ToString();
                                break;
                            case "PASV":
                                response = Passive();
                                logEntry.SPort = ((IPEndPoint)m_passiveListener.LocalEndpoint).Port.ToString();
                                break;
                            case "TYPE":
                                response = Type(command[1], command.Length == 3 ? command[2] : null);
                                logEntry.CSUriStem = command[1];
                                break;
                            case "STRU":
                                response = Structure(arguments);
                                break;
                            case "MODE":
                                response = Mode(arguments);
                                break;
                            case "RNFR":
                                renameFrom = arguments;
                                response = "350 Requested file action pending further information";
                                break;
                            case "RNTO":
                                response = Rename(renameFrom, arguments);
                                break;
                            case "DELE":
                                response = Delete(arguments);
                                break;
                            case "RMD":
                                response = RemoveDir(arguments);
                                break;
                            case "MKD":
                                response = CreateDir(arguments);
                                break;
                            case "PWD":
                                response = PrintWorkingDirectory();
                                break;
                            case "RETR":
                                response = Retrieve(arguments);
                                logEntry.Date = DateTime.Now;
                                break;
                            case "STOR":
                                response = Store(arguments);
                                logEntry.Date = DateTime.Now;
                                break;
                            case "STOU":
                                response = StoreUnique();
                                logEntry.Date = DateTime.Now;
                                break;
                            case "APPE":
                                response = Append(arguments);
                                logEntry.Date = DateTime.Now;
                                break;
                            case "LIST":
                                response = List(arguments ?? m_currentDirectory);
                                logEntry.Date = DateTime.Now;
                                break;
                            case "SYST":
                                response = "215 UNIX Type: L8";
                                break;
                            case "NOOP":
                                response = "200 OK";
                                break;
                            case "ACCT":
                                response = "200 OK";
                                break;
                            case "ALLO":
                                response = "200 OK";
                                break;
                            case "NLST":
                                response = "502 Command not implemented";
                                break;
                            case "SITE":
                                response = "502 Command not implemented";
                                break;
                            case "STAT":
                                response = "502 Command not implemented";
                                break;
                            case "HELP":
                                response = "502 Command not implemented";
                                break;
                            case "SMNT":
                                response = "502 Command not implemented";
                                break;
                            case "REST":
                                response = "502 Command not implemented";
                                break;
                            case "ABOR":
                                response = "502 Command not implemented";
                                break;

                            // Extensions defined by rfc 2228 -> FTP over SSL
                            // TODO : not working...
                            // cert generator : makecert -r -pe -n "CN=localhost" -ss my -sr localmachine -sky exchange -sp "Microsoft RSA SChannel Cryptographic Provider" -sy 12 server.cer
                            /*case "AUTH":
                                response = Auth(arguments);
                                break;*/

                            // Extensions defined by rfc 2389
                            case "FEAT":
                                response = FeatureList();
                                break;
                            case "OPTS":
                                response = Options(arguments);
                                break;

                            // Extensions defined by rfc 3659
                            case "MDTM":
                                response = FileModificationTime(arguments);
                                break;
                            case "SIZE":
                                response = FileSize(arguments);
                                break;

                            // Extensions defined by rfc 2428
                            case "EPRT":
                                response = EPort(arguments);
                                logEntry.CPort = m_dataEndpoint.Port.ToString();
                                break;
                            case "EPSV":
                                response = EPassive();
                                logEntry.SPort = ((IPEndPoint)m_passiveListener.LocalEndpoint).Port.ToString();
                                break;

                            default:
                                response = "502 Command not implemented";
                                break;
                        }
                    }

                    logEntry.CSMethod = cmd;
                    logEntry.CSUsername = Username;
                    logEntry.SCStatus = response.Substring(0, response.IndexOf(' '));

                    m_log.Info(logEntry);

                    if (m_controlClient == null || !m_controlClient.Connected)
                    {
                        break;
                    }
                    else
                    {
                        m_controlWriter.WriteLine(response);
                        m_controlWriter.Flush();

                        if (response.StartsWith("221"))
                        {
                            break;
                        }

                        /*if (cmd == "AUTH")
                        {
                            _cert = new X509Certificate("server.cer");

                            _sslStream = new SslStream(_controlStream);

                            _sslStream.AuthenticateAsServer(_cert);

                            _controlReader = new StreamReader(_sslStream);
                            _controlWriter = new StreamWriter(_sslStream);
                        }*/
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error(ex.Message);
            }

            Dispose();
        }

        #region Utilities
        private string CheckUser()
        {
            if (m_currentUser == null)
            {
                return "530 Not logged in";
            }

            return null;
        }

        private bool IsPathValid(string path)
        {
            return path.StartsWith(m_root);
        }

        private string NormalizeFilename(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }

            if (path == "/")
            {
                return m_root;
            }
            else if (path.StartsWith("/"))
            {
                path = new FileInfo(Path.Combine(m_root, path.Substring(1))).FullName;
            }
            else
            {
                path = new FileInfo(Path.Combine(m_currentDirectory, path)).FullName;
            }

            return IsPathValid(path) ? path : null;
        }

        private static long CopyStream(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                total += count;
            }

            return total;
        }

        private static long CopyStreamAscii(Stream input, Stream output, int bufferSize)
        {
            char[] buffer = new char[bufferSize];
            int count = 0;
            long total = 0;

            using (StreamReader rdr = new StreamReader(input, Encoding.ASCII))
            {
                using (StreamWriter wtr = new StreamWriter(output, Encoding.ASCII))
                {
                    while ((count = rdr.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        wtr.Write(buffer, 0, count);
                        total += count;
                    }
                }
            }

            return total;
        }

        private long CopyStream(Stream input, Stream output)
        {
            Stream limitedStream = output;

            if (m_connectionType == TransferType.Image)
            {
                return CopyStream(input, limitedStream, 4096);
            }
            else
            {
                return CopyStreamAscii(input, limitedStream, 4096);
            }
        }
        #endregion 

        #region FTP Commands

        private string FeatureList()
        {
            m_controlWriter.WriteLine("211- Extensions supported:");
            m_controlWriter.WriteLine(" MDTM");
            m_controlWriter.WriteLine(" SIZE");
            return "211 End";
        }

        private string Options(string arguments)
        {
            return "200 Looks good to me...";
        }

        private string Auth(string authMode)
        {
            if (authMode == "TLS")
            {
                return "234 Enabling TLS Connection";
            }
            else
            {
                return "504 Unrecognized AUTH mode";
            }
        }

        private string User(string username)
        {
            Username = username;

            return "331 Username ok, need password";
        }

        private string Password(string password)
        {
            m_currentUser = UserManager.Validate(Username, password);

            if (m_currentUser != null)
            {
                m_root = m_currentUser.HomeDir;
                m_currentDirectory = m_root;

                return "230 User logged in";
            }
            else
            {
                return "530 Not logged in";
            }
        }

        private string ChangeWorkingDirectory(string pathname)
        {
            if (pathname == "/")
            {
                m_currentDirectory = m_root;
            }
            else
            {
                string newDir;

                if (pathname.StartsWith("/"))
                {
                    pathname = pathname.Substring(1).Replace('/', '\\');
                    newDir = Path.Combine(m_root, pathname);
                }
                else
                {
                    pathname = pathname.Replace('/', '\\');
                    newDir = Path.Combine(m_currentDirectory, pathname);
                }

                if (Directory.Exists(newDir))
                {
                    m_currentDirectory = new DirectoryInfo(newDir).FullName;

                    if (!IsPathValid(m_currentDirectory))
                    {
                        m_currentDirectory = m_root;
                    }
                }
                else
                {
                    m_currentDirectory = m_root;
                }
            }

            return "250 Changed to new directory";
        }

        private string Port(string hostPort)
        {
            m_dataConnectionType = DataConnectionType.Active;

            string[] ipAndPort = hostPort.Split(',');

            byte[] ipAddress = new byte[4];
            byte[] port = new byte[2];

            for (int i = 0; i < 4; i++)
            {
                ipAddress[i] = Convert.ToByte(ipAndPort[i]);
            }

            for (int i = 4; i < 6; i++)
            {
                port[i - 4] = Convert.ToByte(ipAndPort[i]);
            }

            if (BitConverter.IsLittleEndian)
                Array.Reverse(port);

            m_dataEndpoint = new IPEndPoint(new IPAddress(ipAddress), BitConverter.ToInt16(port, 0));

            return "200 Data Connection Established";
        }

        private string EPort(string hostPort)
        {
            m_dataConnectionType = DataConnectionType.Active;

            char delimiter = hostPort[0];

            string[] rawSplit = hostPort.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

            char ipType = rawSplit[0][0];

            string ipAddress = rawSplit[1];
            string port = rawSplit[2];

            m_dataEndpoint = new IPEndPoint(IPAddress.Parse(ipAddress), int.Parse(port));

            return "200 Data Connection Established";
        }

        private string Passive()
        {
            m_dataConnectionType = DataConnectionType.Passive;

            IPAddress localIp = ((IPEndPoint)m_controlClient.Client.LocalEndPoint).Address;

            m_passiveListener = new TcpListener(localIp, 0);
            m_passiveListener.Start();

            IPEndPoint passiveListenerEndpoint = (IPEndPoint)m_passiveListener.LocalEndpoint;

            byte[] address = passiveListenerEndpoint.Address.GetAddressBytes();
            short port = (short)passiveListenerEndpoint.Port;

            byte[] portArray = BitConverter.GetBytes(port);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(portArray);

            return string.Format("227 Entering Passive Mode ({0},{1},{2},{3},{4},{5})", address[0], address[1], address[2], address[3], portArray[0], portArray[1]);
        }

        private string EPassive()
        {
            m_dataConnectionType = DataConnectionType.Passive;

            IPAddress localIp = ((IPEndPoint)m_controlClient.Client.LocalEndPoint).Address;

            m_passiveListener = new TcpListener(localIp, 0);
            m_passiveListener.Start();

            IPEndPoint passiveListenerEndpoint = (IPEndPoint)m_passiveListener.LocalEndpoint;

            return string.Format("229 Entering Extended Passive Mode (|||{0}|)", passiveListenerEndpoint.Port);
        }

        private string Type(string typeCode, string formatControl)
        {
            switch (typeCode.ToUpperInvariant())
            {
                case "A":
                    m_connectionType = TransferType.Ascii;
                    break;
                case "I":
                    m_connectionType = TransferType.Image;
                    break;
                default:
                    return "504 Command not implemented for that parameter";
            }

            if (!string.IsNullOrWhiteSpace(formatControl))
            {
                switch (formatControl.ToUpperInvariant())
                {
                    case "N":
                        m_formatControlType = FormatControlType.NonPrint;
                        break;
                    default:
                        return "504 Command not implemented for that parameter";
                }
            }

            return string.Format("200 Type set to {0}", m_connectionType);
        }

        private string Delete(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    File.Delete(pathname);
                }
                else
                {
                    return "550 File Not Found";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 File Not Found";
        }

        private string RemoveDir(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (Directory.Exists(pathname))
                {
                    Directory.Delete(pathname);
                }
                else
                {
                    return "550 Directory Not Found";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 Directory Not Found";
        }

        private string CreateDir(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (!Directory.Exists(pathname))
                {
                    Directory.CreateDirectory(pathname);
                }
                else
                {
                    return "550 Directory already exists";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 Directory Not Found";
        }

        private string FileModificationTime(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    return string.Format("213 {0}", File.GetLastWriteTime(pathname).ToString("yyyyMMddHHmmss.fff"));
                }
            }

            return "550 File Not Found";
        }

        private string FileSize(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    long length = 0;

                    using (FileStream fs = File.Open(pathname, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        length = fs.Length;
                    }

                    return string.Format("213 {0}", length);
                }
            }

            return "550 File Not Found";
        }

        private string Retrieve(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    var state = new DataConnectionOperation { Arguments = pathname, Operation = RetrieveOperation };

                    SetupDataConnectionOperation(state);

                    return string.Format("150 Opening {0} mode data transfer for RETR", m_dataConnectionType);
                }
            }

            return "550 File Not Found";
        }

        private string Store(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                var state = new DataConnectionOperation { Arguments = pathname, Operation = StoreOperation };

                SetupDataConnectionOperation(state);

                return string.Format("150 Opening {0} mode data transfer for STOR", m_dataConnectionType);
            }

            return "450 Requested file action not taken";
        }

        private string Append(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                var state = new DataConnectionOperation { Arguments = pathname, Operation = AppendOperation };

                SetupDataConnectionOperation(state);

                return string.Format("150 Opening {0} mode data transfer for APPE", m_dataConnectionType);
            }

            return "450 Requested file action not taken";
        }

        private string StoreUnique()
        {
            string pathname = NormalizeFilename(new Guid().ToString());

            var state = new DataConnectionOperation { Arguments = pathname, Operation = StoreOperation };

            SetupDataConnectionOperation(state);

            return string.Format("150 Opening {0} mode data transfer for STOU", m_dataConnectionType);
        }

        private string PrintWorkingDirectory()
        {
            string current = m_currentDirectory.Replace(m_root, string.Empty).Replace('\\', '/');

            if (current.Length == 0)
            {
                current = "/";
            }

            return string.Format("257 \"{0}\" is current directory.", current); ;
        }

        private string List(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                var state = new DataConnectionOperation { Arguments = pathname, Operation = ListOperation };

                SetupDataConnectionOperation(state);

                return string.Format("150 Opening {0} mode data transfer for LIST", m_dataConnectionType);
            }

            return "450 Requested file action not taken";
        }

        private string Structure(string structure)
        {
            switch (structure)
            {
                case "F":
                    m_fileStructureType = FileStructureType.File;
                    break;
                case "R":
                case "P":
                    return string.Format("504 STRU not implemented for \"{0}\"", structure);
                default:
                    return string.Format("501 Parameter {0} not recognized", structure);
            }

            return "200 Command OK";
        }

        private string Mode(string mode)
        {
            if (mode.ToUpperInvariant() == "S")
            {
                return "200 OK";
            }
            else
            {
                return "504 Command not implemented for that parameter";
            }
        }

        private string Rename(string renameFrom, string renameTo)
        {
            if (string.IsNullOrWhiteSpace(renameFrom) || string.IsNullOrWhiteSpace(renameTo))
            {
                return "450 Requested file action not taken";
            }

            renameFrom = NormalizeFilename(renameFrom);
            renameTo = NormalizeFilename(renameTo);

            if (renameFrom != null && renameTo != null)
            {
                if (File.Exists(renameFrom))
                {
                    File.Move(renameFrom, renameTo);
                }
                else if (Directory.Exists(renameFrom))
                {
                    Directory.Move(renameFrom, renameTo);
                }
                else
                {
                    return "450 Requested file action not taken";
                }

                return "250 Requested file action okay, completed";
            }

            return "450 Requested file action not taken";
        }

        #endregion

        #region DataConnection Operations

        private void HandleAsyncResult(IAsyncResult result)
        {
            if (m_dataConnectionType == DataConnectionType.Active)
            {
                m_dataClient.EndConnect(result);
            }
            else
            {
                m_dataClient = m_passiveListener.EndAcceptTcpClient(result);
            }
        }

        private void SetupDataConnectionOperation(DataConnectionOperation state)
        {
            if (m_dataConnectionType == DataConnectionType.Active)
            {
                m_dataClient = new TcpClient(m_dataEndpoint.AddressFamily);
                m_dataClient.BeginConnect(m_dataEndpoint.Address, m_dataEndpoint.Port, DoDataConnectionOperation, state);
            }
            else
            {
                m_passiveListener.BeginAcceptTcpClient(DoDataConnectionOperation, state);
            }
        }

        private void DoDataConnectionOperation(IAsyncResult result)
        {
            HandleAsyncResult(result);

            DataConnectionOperation op = result.AsyncState as DataConnectionOperation;

            string response;

            using (NetworkStream dataStream = m_dataClient.GetStream())
            {
                response = op.Operation(dataStream, op.Arguments);
            }

            m_dataClient.Close();
            m_dataClient = null;

            m_controlWriter.WriteLine(response);
            m_controlWriter.Flush();
        }

        private string RetrieveOperation(NetworkStream dataStream, string pathname)
        {
            long bytes = 0;

            using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
            {
                bytes = CopyStream(fs, dataStream);
            }

            return "226 Closing data connection, file transfer successful";
        }

        private string StoreOperation(NetworkStream dataStream, string pathname)
        {
            long bytes = 0;

            using (FileStream fs = new FileStream(pathname, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                bytes = CopyStream(dataStream, fs);
            }

            LogEntry logEntry = new LogEntry
            {
                Date = DateTime.Now,
                CIP = m_clientIP,
                CSMethod = "STOR",
                CSUsername = Username,
                SCStatus = "226",
                CSBytes = bytes.ToString()
            };

            m_log.Info(logEntry);

            return "226 Closing data connection, file transfer successful";
        }

        private string AppendOperation(NetworkStream dataStream, string pathname)
        {
            long bytes = 0;

            using (FileStream fs = new FileStream(pathname, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                bytes = CopyStream(dataStream, fs);
            }

            LogEntry logEntry = new LogEntry
            {
                Date = DateTime.Now,
                CIP = m_clientIP,
                CSMethod = "APPE",
                CSUsername = Username,
                SCStatus = "226",
                CSBytes = bytes.ToString()
            };

            m_log.Info(logEntry);

            return "226 Closing data connection, file transfer successful";
        }

        private string ListOperation(NetworkStream dataStream, string pathname)
        {
            StreamWriter dataWriter = new StreamWriter(dataStream, Encoding.ASCII);

            IEnumerable<string> directories = Directory.EnumerateDirectories(pathname);

            foreach (string dir in directories)
            {
                DirectoryInfo d = new DirectoryInfo(dir);

                string date = d.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                    d.LastWriteTime.ToString("MMM dd  yyyy") :
                    d.LastWriteTime.ToString("MMM dd HH:mm");

                string line = string.Format("drwxr-xr-x    2 2003     2003     {0,8} {1} {2}", "4096", date, d.Name);

                byte[] bytesLine = Encoding.GetEncoding("ISO-8859-8").GetBytes(line);
                line = Encoding.UTF8.GetString(bytesLine);

                dataWriter.WriteLine(line);
                dataWriter.Flush();
            }

            IEnumerable<string> files = Directory.EnumerateFiles(pathname);

            foreach (string file in files)
            {
                FileInfo f = new FileInfo(file);

                string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                    f.LastWriteTime.ToString("MMM dd  yyyy") :
                    f.LastWriteTime.ToString("MMM dd HH:mm");

                string line = string.Format("-rw-r--r--    2 2003     2003     {0,8} {1} {2}", f.Length, date, f.Name);

                byte[] bytesLine = Encoding.GetEncoding("ISO-8859-8").GetBytes(line);
                line = Encoding.UTF8.GetString(bytesLine);

                dataWriter.WriteLine(line);
                dataWriter.Flush();
            }

            LogEntry logEntry = new LogEntry
            {
                Date = DateTime.Now,
                CIP = m_clientIP,
                CSMethod = "LIST",
                CSUsername = Username,
                SCStatus = "226"
            };

            m_log.Info(logEntry);

            return "226 Transfer complete";
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
                    if (m_controlClient != null)
                    {
                        m_controlClient.Close();
                    }

                    if (m_dataClient != null)
                    {
                        m_dataClient.Close();
                    }

                    if (m_controlStream != null)
                    {
                        m_controlStream.Close();
                    }

                    if (m_controlReader != null)
                    {
                        m_controlReader.Close();
                    }

                    if (m_controlWriter != null)
                    {
                        m_controlWriter.Close();
                    }
                }
            }

            m_disposed = true;
        }
        
        #endregion

        public override string ToString()
        {
            return m_clientIP + " : " + Username;
        }
    }
}