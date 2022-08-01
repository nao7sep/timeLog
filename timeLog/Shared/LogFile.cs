using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeLog
{
    public class LogFile
    {
        public readonly string FilePath;

        public readonly List <string> BadChunks = new List <string> ();

        private SortedList <DateTime, LogInfo>? mLogs = null;

        public SortedList <DateTime, LogInfo> Logs
        {
            get
            {
                if (mLogs == null)
                {
                    var xLogs = new SortedList <DateTime, LogInfo> ();

                    if (File.Exists (FilePath))
                        BadChunks.AddRange (Shared.LoadLogFile (FilePath, out xLogs));

                    mLogs = xLogs;
                }

                return mLogs;
            }
        }

        public LogFile (string filePath)
        {
            if (Path.IsPathFullyQualified (filePath) == false)
                throw new ArgumentException ();

            FilePath = filePath;
        }

        public void AddLog (LogInfo log)
        {
            Shared.AddLogToFile (FilePath, log);
            Logs.Add (log.StartUtc, log);
        }

        public void DeleteLog (LogInfo log)
        {
            Shared.DeleteLogFromFile (FilePath, log);
            Logs.Remove (log.StartUtc); // 甘いが大丈夫
        }
    }
}
