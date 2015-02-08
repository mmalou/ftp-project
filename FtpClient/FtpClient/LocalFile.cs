using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtpClient
{
    class LocalFile
    {
        public string FileName { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string LastModified { get; set; }

        public string FullPath { get; set; }
    }
}
