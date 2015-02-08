using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FtpClientLibrary;
using System.Threading;
using System.ComponentModel;
using System.IO;

namespace MainFtpClient
{
    class Program
    {
        static FtpClientApi myFtpClient = new FtpClientApi("127.0.0.1:21", "manu", "manu");

        static void Main(string[] args)
        {
            /*ThreadManager manager = new ThreadManager(2);

            FtpTransfert firstTransfert = new FtpTransfert(new CancellationTokenSource());
            firstTransfert.PropertyChanged += FtpTransfert_PropertyChange;
            firstTransfert.DestinationPath = @"C:\testFtp\ninjaSodomite.gif";
            firstTransfert.Location = "/www/ninjaSodomite.gif";
            firstTransfert.Status = "WAITING";

            manager.Add(() => myFtpClient.Download(firstTransfert), firstTransfert.TokenSource.Token);
            firstTransfert.IsStopped = true;
            Task.WaitAll(manager.CurrentTasks.ToArray<Task>());*/
        }

        static void FtpTransfert_PropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsStopped")
            {
                FtpTransfert aFtpTransfert = sender as FtpTransfert;
                aFtpTransfert.TokenSource.Cancel();
                Console.WriteLine("cancel !");
                myFtpClient.Delete(aFtpTransfert.DestinationPath, FtpClientApi.SIDE.LOCAL);
            }

            if (e.PropertyName == "PercentDone")
            {
                FtpTransfert t = sender as FtpTransfert;
                if (t.PercentDone % 3 == 0)
                    Console.WriteLine("{0}%", t.PercentDone);
            }
        }
    }
}
