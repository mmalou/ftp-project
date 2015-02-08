using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtpClientLibrary
{
    public class FtpDirectory : FtpItem
    {
        public List<FtpItem> Children { get; private set; }

        public FtpDirectory(string p_name,string p_fullName)
            : base(p_name, p_fullName)
        {
            Children = new List<FtpItem>();
        }
    }
}
