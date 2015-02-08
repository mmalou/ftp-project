using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtpClientLibrary
{
    public abstract class FtpItem
    {
        public string Name { get; protected set; }
        public string FullName { get; protected set; }
        public string Type { get; set; }
        public string StringSize { get; set; }
        public string LastModified { get; set; }

        public FtpItem(string p_name,string p_fullName)
        {
            Name = p_name;
            FullName = p_fullName;
        }

        public override string ToString()
        {
            return FullName;
        }
    }
}
