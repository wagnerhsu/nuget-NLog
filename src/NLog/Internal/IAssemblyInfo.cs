using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLog.Internal
{
    internal class AssemblyInfo : IAssemblyInfo
    {

        internal static IAssemblyInfo Instance = new AssemblyInfo();

        private AssemblyInfo()
        {

        }

#if !SILVERLIGHT && !NETSTANDARD1_3
        public string GetEntryAssemblyFileName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        public string GetEntryAssemblyLocation()
        {
            return AssemblyHelpers.GetAssemblyFileLocation(System.Reflection.Assembly.GetEntryAssembly());
        }
#endif
    }

    internal interface IAssemblyInfo

    {
#if !SILVERLIGHT && !NETSTANDARD1_3
        string GetEntryAssemblyFileName();
        string GetEntryAssemblyLocation();
#endif
    }
}
