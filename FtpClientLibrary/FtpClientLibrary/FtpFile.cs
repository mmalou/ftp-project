using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtpClientLibrary
{
    public class FtpFile : FtpItem
    {
        public long Size { get; set; }
        public DateTime Modified { get; private set; }

        public FtpFile(string p_name, string p_fullName, DateTime p_modified, long p_size)
            :base(p_name, p_fullName)
        {
            Size = p_size;
            Modified = p_modified;
        }
    }
}
