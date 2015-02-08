using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FtpClientLibrary;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace FtpClient
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region fields & properties
        private string m_host;
        private string m_user;
        private string m_password;

        private FtpClientApi m_ftpClient;
        private ThreadManager m_manager;

        private object m_dummyNode = null;
        #endregion

        #region constructor
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Window Event
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLocalTree();
            m_manager = new ThreadManager(3);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ClearLog();
        }
        #endregion

        #region ConnectionBar Event
        private void btnConnexion_Click(object sender, RoutedEventArgs e)
        {
            m_host = btnHost.Text + ":" + btnPort.Text;
            m_user = btnUserName.Text;
            m_password = btnPassword.Password;

            try
            {
                LoadServerTree();
            }
            catch (Exception except)
            {
                listBoxInfos.Items.Add("Une erreur est survenue lors de la connection au serveur.");
            }
            LoadInfos();
        }

        private void btnPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!Char.IsDigit(c))
                    e.Handled = true;
            }
        }
        #endregion

        #region Logs Methods
        private void LoadInfos()
        {
            listBoxInfos.Items.Clear();

            using (FileStream stream = File.Open("network.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                StreamReader reader = new StreamReader(stream);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("-"))
                    {
                        string[] lineArray = line.Split(' ').Skip(7).ToArray<string>();
                        line = string.Join(" ", lineArray);
                        listBoxInfos.Items.Add(line);
                    }
                }
                listBoxInfos.SelectedIndex = listBoxInfos.Items.Count - 1;
                listBoxInfos.ScrollIntoView(listBoxInfos.SelectedIndex);
                listBoxInfos.UnselectAll();
            }
        }

        private void ClearLog()
        {
            TraceSource source = new TraceSource("System.Net");
            source.Listeners["System.Net"].Close();

            using (FileStream stream = File.Open("network.log", FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                stream.SetLength(0);
                stream.Flush();
            }
        }
        #endregion

        #region Local Methods
        #region TreeeViewLocal Methods
        private void LoadLocalTree()
        {
            foreach (string s in Directory.GetLogicalDrives())
            {
                TreeViewItem item = new TreeViewItem();
                item.Header = s;
                item.Tag = s;
                item.FontWeight = FontWeights.Normal;
                item.Items.Add(m_dummyNode);
                item.Expanded += new RoutedEventHandler(LocalFolder_Expanded);
                LocalTree.Items.Add(item);
            }
        }

        private void LocalFolder_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == m_dummyNode)
            {
                item.Items.Clear();
                try
                {
                    foreach (string s in Directory.GetDirectories(item.Tag.ToString()))
                    {
                        TreeViewItem subitem = new TreeViewItem();
                        subitem.Header = s.Substring(s.LastIndexOf("\\") + 1);
                        subitem.Tag = s;
                        subitem.FontWeight = FontWeights.Normal;
                        subitem.Items.Add(m_dummyNode);
                        subitem.Expanded += new RoutedEventHandler(LocalFolder_Expanded);
                        subitem.Selected += new RoutedEventHandler(TreeViewItemLocal_Selected);
                        item.Items.Add(subitem);
                    }
                }
                catch (Exception) { }
            }
        }

        private void TreeViewItemLocal_Selected(object sender, RoutedEventArgs e)
        {
            TreeViewItem selectedItem = e.OriginalSource as TreeViewItem;

            LstViewLocalDetailed.Items.Clear();

            try
            {
                foreach (string s in Directory.GetDirectories(selectedItem.Tag.ToString()))
                {

                    DirectoryInfo info = new DirectoryInfo(s);

                    DateTime time = info.LastWriteTime;
                    LstViewLocalDetailed.Items.Add(new LocalFile
                    {
                        FileName = s.Substring(s.LastIndexOf("\\") + 1),
                        Size = "",
                        Type = "Dossier",
                        LastModified = time.ToString("dd/MM/yyyy hh:mm")
                    });
                }

                foreach (string s in Directory.GetFiles(selectedItem.Tag.ToString()))
                {
                    FileInfo info = new FileInfo(s);

                    long size = info.Length;
                    DateTime time = info.LastWriteTime;
                    LstViewLocalDetailed.Items.Add(new LocalFile
                    {
                        FileName = s.Substring(s.LastIndexOf("\\") + 1),
                        Size = "" + size,
                        Type = "Fichier",
                        LastModified = time.ToString("dd/MM/yyyy hh:mm:ss"),
                        FullPath = s
                    });
                }
            }
            catch (UnauthorizedAccessException except)
            {
                MessageBox.Show("Accès non autorisé à ce dossier !", "Erreur d'autorisation",
                    MessageBoxButton.OK, MessageBoxImage.Stop, MessageBoxResult.OK);
            }
        }

        #endregion

        #region ListViewLocal Methods
        private void LstViewLocalDetailedItem_Click(object sender, RoutedEventArgs e)
        {
            LocalFile item = ((FrameworkElement)e.OriginalSource).DataContext as LocalFile;

            try
            {
                if (item.FullPath != null)
                    StartUpload(item);
            }
            catch (NullReferenceException except) { }
        }
        #endregion
        #endregion

        #region Server Method
        #region TreeViewServer Methods
        private void LoadServerTree()
        {
            ServerTree.Items.Clear();

            m_ftpClient = new FtpClientApi(m_host, m_user, m_password);
            m_ftpClient.LoadArborescence(m_ftpClient.Root);
            LoadInfos();

            foreach (FtpDirectory dir in m_ftpClient.Root.Children.OfType<FtpDirectory>())
            {
                TreeViewItem item = new TreeViewItem();
                item.Header = dir.Name;
                item.Tag = dir;
                item.FontWeight = FontWeights.Normal;
                item.Items.Add(m_dummyNode);
                item.Expanded += new RoutedEventHandler(ServerFolder_Expanded);
                item.Selected += new RoutedEventHandler(TreeViewItemServer_Selected);
                ServerTree.Items.Add(item);
            }
        }

        private void ServerFolder_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            FtpDirectory dirToExpand = item.Tag as FtpDirectory;
            m_ftpClient.LoadArborescence(dirToExpand);
            LoadInfos();

            if (item.Items.Count == 1 && item.Items[0] == m_dummyNode)
            {
                item.Items.Clear();
                try
                {
                    foreach (FtpDirectory dir in dirToExpand.Children.OfType<FtpDirectory>())
                    {
                        TreeViewItem subitem = new TreeViewItem();
                        subitem.Header = dir.Name;
                        subitem.Tag = dir;
                        subitem.FontWeight = FontWeights.Normal;
                        subitem.Items.Add(m_dummyNode);
                        subitem.Expanded += new RoutedEventHandler(ServerFolder_Expanded);
                        subitem.Selected += new RoutedEventHandler(TreeViewItemServer_Selected);
                        item.Items.Add(subitem);
                    }
                }
                catch (Exception) { }
            }
        }

        private void TreeViewItemServer_Selected(object sender, RoutedEventArgs e)
        {
            TreeViewItem selectedItem = e.OriginalSource as TreeViewItem;
            FtpDirectory selectedDir = selectedItem.Tag as FtpDirectory;

            m_ftpClient.LoadArborescence(selectedDir);
            LoadInfos();

            LstViewServerDetailed.Items.Clear();

            foreach (FtpDirectory dir in selectedDir.Children.OfType<FtpDirectory>())
            {
                dir.Type = "Dossier";
                dir.StringSize = "";
                dir.LastModified = "";
                LstViewServerDetailed.Items.Add(dir);
            }

            foreach (FtpFile file in selectedDir.Children.OfType<FtpFile>())
            {
                file.Type = "Fichier";
                file.StringSize = "" + file.Size;
                file.LastModified = file.Modified.ToString("dd/MM/yyyy hh:mm");
                LstViewServerDetailed.Items.Add(file);
            }
        }
        #endregion

        #region ListViewServer Methods
        private void LstViewServerDetailedItem_Click(object sender, RoutedEventArgs e)
        {
            FtpItem file = ((FrameworkElement)e.OriginalSource).DataContext as FtpItem;

            try
            {
                if (file is FtpFile)
                    StartDownload(file as FtpFile);
            }
            catch (NullReferenceException except) { }
        }
        #endregion
        #endregion

        #region Transfert Methods
        private void StartDownload(FtpFile p_file)
        {
            string pathClient = currentLocalFolder.Text + "/";

            if (pathClient == "/")
                MessageBox.Show("Aucun emplacement d'arrivé sélectionné", "Erreur", MessageBoxButton.OK,
                    MessageBoxImage.Warning, MessageBoxResult.OK);
            else
            {
                FtpTransfert transfert = new FtpTransfert(new CancellationTokenSource());
                transfert.PropertyChanged += FtpTransfert_PropertyChange;

                transfert.Direction = "Client";
                transfert.Location = p_file.FullName;
                transfert.Size = p_file.Size;
                transfert.Status = "WAITING";

                transfert.DestinationPath = pathClient + p_file.Name;

                m_manager.Add(() => m_ftpClient.Download(transfert), transfert.TokenSource.Token);

                LoadInfos();
            }
        }

        private void StartUpload(LocalFile p_file)
        {
            string pathServer = currentServerFolder.Text;

            if (pathServer == "")
                MessageBox.Show("Aucun emplacement d'arrivé sélectionné", "Erreur", MessageBoxButton.OK,
                    MessageBoxImage.Warning, MessageBoxResult.OK);
            else
            {
                FtpTransfert transfert = new FtpTransfert(new CancellationTokenSource());
                transfert.PropertyChanged += FtpTransfert_PropertyChange;

                transfert.Direction = "Serveur";
                transfert.Location = p_file.FullPath;
                transfert.Size = Convert.ToInt64(p_file.Size);
                transfert.Status = "WAITING";
                transfert.DestinationPath = pathServer + p_file.FileName;

                m_manager.Add(() => m_ftpClient.Upload(transfert), transfert.TokenSource.Token);

                LoadInfos();
            }
        }

        private void FtpTransfert_PropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsStopped")
            {
                FtpTransfert aFtpTransfert = sender as FtpTransfert;
                aFtpTransfert.Status = "STOPPED";
                aFtpTransfert.TokenSource.Cancel();

                if (aFtpTransfert.Direction == "Serveur")
                    m_ftpClient.Delete(aFtpTransfert.DestinationPath, FtpClientApi.SIDE.SERVER);
                else
                    m_ftpClient.Delete(aFtpTransfert.DestinationPath, FtpClientApi.SIDE.LOCAL);
            }

            if (e.PropertyName == "PercentDone")
            {
                FtpTransfert aFtpTransfert = sender as FtpTransfert;

                if (aFtpTransfert.PercentDone > 0 && aFtpTransfert.PercentDone < 100)
                    aFtpTransfert.Status = "RUNING";
                if (aFtpTransfert.PercentDone == 100)
                    aFtpTransfert.Status = "DONE";
            }
        }
        #endregion
    }
}
