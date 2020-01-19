using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipArchiver
{
    internal static class Logger
    {
        private static volatile bool _errorFixed;
        private static volatile bool _disableOutput;

        public static bool ErrorFixed
        {
            get
            {
                return _errorFixed;
            }
        }

        public static void TraceError(string msg)
        {
            _errorFixed = true;
            if (_disableOutput)
                return;

            Console.WriteLine();
            Console.WriteLine(msg);
            Console.WriteLine();
        }

        public static void DisableOutput()
        {
            _disableOutput = true;
        }
    }
}
