using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace FtpClientLibrary
{
    public class FtpClientApi
    {
        #region enum
        public enum SIDE
        {
            SERVER,
            LOCAL
        }
        #endregion

        #region fields & properties

        private string m_host;
        private string m_user;
        private string m_password;

        private int m_bufferSize = 2048;

        /// <summary>
        /// Le nombre de pourcentage de l'uplaod/download en cours
        /// </summary>
        
        /// <summary>
        /// Les transferts en cours.
        /// </summary>
        public List<FtpTransfert> CurrentTransferts { get; private set; }

        /// <summary>
        /// La racine du serveur FTP.
        /// </summary>
        public FtpDirectory Root { get; private set; }

        #endregion

        #region constructor

        public FtpClientApi(string p_host, string p_user, string p_password)
        {
            m_host = p_host;
            m_user = p_user;
            m_password = p_password;

            Root = new FtpDirectory("/", "/");

            CurrentTransferts = new List<FtpTransfert>();
        }

        #endregion

        #region private methods

        /// <summary>
        /// Retourne la taille du fichier présent au chemin passé en paramètre
        /// </summary>
        /// <param name="p_path">le chemin du fichier</param>
        /// <param name="p_side">le côté où est présent le fichier (client ou serveur)</param>
        /// <returns></returns>
        private long? GetFileSize(string p_path, SIDE p_side)
        {
            try
            {
                if (p_side == SIDE.SERVER)
                {
                    FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(p_path);
                    ftpRequest.Credentials = new NetworkCredential(m_user, m_password);
                    ftpRequest.Method = WebRequestMethods.Ftp.GetFileSize;

                    using (WebResponse resp = ftpRequest.GetResponse())
                        return resp.ContentLength;
                }
                else
                {
                    FileInfo info = new FileInfo(p_path);
                    return info.Length;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        #endregion

        #region public methods
        /// <summary>
        /// Enclanche le téléchargement d'un fichier du serveur ftp
        /// </summary>
        /// <param name="pathServer">le chemin du fichier à télécharger sur le serveur (chemin relatif à la racine)</param>
        /// <param name="pathClient">le chemin où enregistrer le fichier chez le client en local (chemin absolu)</param>
        /// <param name="p_transfert">l'objet FtpTransfert associé</param>
        public void Download(FtpTransfert p_transfert)
        {
            try
            {
                string pathServer = p_transfert.Location;
                string pathClient = p_transfert.DestinationPath;
                string address = "ftp://" + m_host + "/" + pathServer;

                long? fileSize = p_transfert.Size;

                if (fileSize == null) throw new Exception("Un problème est intervenu lors de la lecture de la taille du fichier.");

                FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(address);
                ftpRequest.Credentials = new NetworkCredential(m_user, m_password);
                ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;

                string fileName = pathServer.Replace('\\', '/').Split('/').Last();

                if (fileName.Contains("pdf"))
                    ftpRequest.UseBinary = true;

                using (FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
                using (Stream ftpStream = ftpResponse.GetResponseStream())
                using (FileStream writeStream = new FileStream(pathClient, FileMode.Create))
                {
                    Byte[] buffer = new Byte[m_bufferSize];
                    int bytesRead = ftpStream.Read(buffer, 0, m_bufferSize);
                    int bytes = 0;

                    p_transfert.Name = fileName;
                    p_transfert.Size = (long)fileSize;

                    CurrentTransferts.Add(p_transfert);

                    while (bytesRead > 0)
                    {
                        writeStream.Write(buffer, 0, bytesRead);
                        bytesRead = ftpStream.Read(buffer, 0, m_bufferSize);
                        bytes += bytesRead;

                        p_transfert.PercentDone = Convert.ToInt32(((bytes / 1000) * 100) / ((int)fileSize / 1000));
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        /// Enclanche l'envoi d'un fichier vers le serveur ftp
        /// </summary>
        /// <param name="pathFile">le chemin où enregistrer le fichier chez le client en local (chemin absolu)</param>
        /// <param name="pathServer">le chemin du fichier à télécharger sur le serveur (chemin relatif à la racine)</param>
        /// <param name="p_transfert">l'objet FtpTransfert associé</param>
        public void Upload(FtpTransfert p_transfert)
        {
            try
            {
                string pathFile = p_transfert.Location;
                string pathServer = p_transfert.DestinationPath;

                string fileName = pathFile.Replace('\\', '/').Split('/').Last();

                string address = string.Format("ftp://{0}{1}", m_host, pathServer);

                long? fileSize = GetFileSize(pathFile, SIDE.LOCAL);

                if (fileSize == null) throw new Exception("Un problème est intervenu lors de la lecture de la taille du fichier.");

                FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(address);
                ftpRequest.Credentials = new NetworkCredential(m_user, m_password);
                ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;

                if (fileName.Contains("pdf"))
                    ftpRequest.UseBinary = true;

                using (Stream ftpStream = ftpRequest.GetRequestStream())
                using (FileStream fileStream = File.OpenRead(pathFile))
                {
                    Byte[] buffer = new Byte[m_bufferSize];
                    int bytesToUpload = (int)fileSize;
                    int bytes = 0;
                    int bytesRead;

                    p_transfert.Name = fileName;
                    p_transfert.Size = (long)fileSize;

                    CurrentTransferts.Add(p_transfert);

                    while ( bytes < bytesToUpload)
                    {
                        if (p_transfert.TokenSource.Token.IsCancellationRequested)
                            throw new OperationCanceledException();

                        bytesRead = fileStream.Read(buffer, 0, m_bufferSize);
                        ftpStream.Write(buffer, 0, bytesRead);
                        bytes += bytesRead;

                        p_transfert.PercentDone = Convert.ToInt32(((bytes / 1000) * 100) / ((int)fileSize / 1000));
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        /// Supprime un fichier du serveur ftp ou du poste client.
        /// </summary>
        /// <param name="p_path">le chemin vers le fichier (absolu pour le client, relatif à la racine pour le serveur).</param>
        /// <param name="p_side">L'emplacement du fichier (Client ou Serveur)</param>
        public void Delete(string p_path, SIDE p_side)
        {
            if (p_side == SIDE.SERVER)
            {
                try
                {
                    string address = string.Format("ftp://{0}{1}", m_host, p_path);

                    FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(address);
                    ftpRequest.Credentials = new NetworkCredential(m_user, m_password);
                    ftpRequest.Method = WebRequestMethods.Ftp.DeleteFile;

                    FtpWebResponse response = (FtpWebResponse)ftpRequest.GetResponse();
                    response.Close();
                }
                catch (Exception e)
                {
                    throw;
                }
            }
            else
            {
                File.Delete(p_path);
            }
        }

        /// <summary>
        /// Charge l'arborescence du serveur ftp.
        /// </summary>
        /// <param name="p_dir">Le dossier à partir duquel charger l'arborescence.</param>
        public void LoadArborescence(FtpDirectory p_dir)
        {
            if (p_dir.Children.Count > 0)
                p_dir.Children.Clear();

            try
            {
                string address = "ftp://" + m_host + p_dir.FullName;
                FtpWebRequest ftpRequest = (FtpWebRequest)FtpWebRequest.Create(address);
                ftpRequest.Credentials = new NetworkCredential(m_user, m_password);

                ftpRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                using (FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
                using (Stream ftpStream = ftpResponse.GetResponseStream())
                using (StreamReader reader = new StreamReader(ftpStream))
                {
                    while (reader.Peek() != -1)
                    {
                        string line = reader.ReadLine();

                        string[] lineArray = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lineArray[5] == "fevr.")
                            lineArray[5] = "févr.";
                        if (lineArray[5] == "dec.")
                            lineArray[5] = "déc.";

                        if (lineArray[0][0] == 'd' && ! new string[] {".", ".."}.Contains(lineArray.Last()))
                        {
                            FtpDirectory directory = new FtpDirectory(lineArray.Last(),
                                string.Format("{0}{1}/",p_dir.FullName, lineArray.Last()));
                            p_dir.Children.Add(directory);
                        }
                        else
                        {
                            DateTime modified;
                            int monthInDigit = (int)DateTime.ParseExact(lineArray[5], "MMM", new CultureInfo("fr-FR")).Month;

                            int year = DateTime.Now.Year;
                            if (lineArray[7].Contains(':'))
                            {
                                if (DateTime.Now.Month < monthInDigit)
                                    year = DateTime.Now.Year - 1;
                                int hour = Convert.ToInt32(lineArray[7].Split(':')[0]);
                                int min = Convert.ToInt32(lineArray[7].Split(':')[1]);

                                modified = new DateTime(year, monthInDigit, Convert.ToInt32(lineArray[6]), hour, min, 0);
                            }
                            else
                                modified = new DateTime(year, monthInDigit, Convert.ToInt32(lineArray[6]));
                            
                            FtpFile file = new FtpFile(lineArray.Last(), p_dir.FullName + lineArray.Last(),
                                modified, Convert.ToInt64(lineArray[4]));
                            p_dir.Children.Add(file);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
        #endregion
    }
}
