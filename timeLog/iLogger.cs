using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeLog
{
    internal static class iLogger
    {
        public static readonly FileInfo LogFile = new FileInfo (Shared.MapPath ("timeLog.Errors.txt"));

        public static void WriteSafe (string value)
        {
            try
            {
                StringBuilder xBuilder = new StringBuilder ();

                if (LogFile.Exists && LogFile.Length > 0)
                    xBuilder.AppendLine ();

                xBuilder.AppendLine ($"[{DateTime.UtcNow.ToString ("O")}]");
                xBuilder.AppendLine (value);

                File.AppendAllText (LogFile.FullName, xBuilder.ToString (), Encoding.UTF8);
            }

            catch
            {
                // *Safe メソッドなので何もしない
            }
        }
    }
}
