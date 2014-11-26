using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.FtpClient;
using System.IO;

namespace TestFtp
{
    class Program
    {
        static void Main(string[] args)
        {
            //C:\Users\Maxime\Projets\FTP\Download
            using (var client = new FtpClient())
            {
                client.Host = "ftp.dryke.tv";
                client.Port = 21;
                client.Credentials = new System.Net.NetworkCredential("dryke", "UixWzB4J");
                var destinationDirectory = @"C:\Users\Maxime\Projets\FTP\Download";

                client.Connect();

                var file = client.GetListing("/www");//.Where(f => string.Equals(Path.GetFileNameWithoutExtension(f.Name), "1mb")).First();
                //var destinationPath = string.Format(@"{0}\{1}", destinationDirectory, file.Name);

                foreach (var ftpListitem in client.GetListing())
                {
                    if (ftpListitem.Type == FtpFileSystemObjectType.Directory)
                        Console.WriteLine("dossier : {0}", ftpListitem.FullName);
                    else if (ftpListitem.Type == FtpFileSystemObjectType.File)
                    {
                        
                        Console.WriteLine("fichier : {0}", ftpListitem.FullName);
                    }
                }

               /* using(var ftpStream = client.OpenRead(file.FullName))
                using(var fileStream = File.Create(destinationPath, (int)ftpStream.Length))
                {
                    Console.WriteLine("fichier = {0}", file.FullName);
                    var buffer = new byte[8 * 1024];
                    int count;

                    while((count = ftpStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, count);
                    }
                }*/
            }
            Console.WriteLine("\n\nTransfert complet.");
        }
    }
}
