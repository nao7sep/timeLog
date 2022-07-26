using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace timeLog
{
    public static class Shared
    {
        public static List <string> ParseTasksString (string value)
        {
            List <string> xTasks = new List <string> ();

            using (StringReader xReader = new StringReader (value))
            {
                string? xLine;

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    // 2回の掃除により、最小限の文字列に
                    // コストの低そうな Trim から行う

                    if ((xLine = xLine.Trim ()).Length > 0)
                    {
                        if ((xLine = Regex.Replace (xLine, @"\s+", "\x20", RegexOptions.Compiled | RegexOptions.CultureInvariant)).Length > 0)
                            xTasks.Add (xLine);
                    }
                }
            }

            return xTasks;
        }

        public static void AddLogToFile (string filePath, LogInfo log)
        {
            if (Path.IsPathFullyQualified (filePath) == false)
                throw new InvalidOperationException ();

            FileInfo xFile = new FileInfo (filePath);

            if (xFile.Directory != null && xFile.Directory.Exists == false)
                xFile.Directory.Create ();

            File.AppendAllText (filePath, (xFile.Exists && xFile.Length > 0 ? Environment.NewLine : string.Empty) + log.ToChunk (), Encoding.UTF8);
        }

        public static List <string> StringToParagraphs (string value)
        {
            List <string> xLines = new List <string> ();

            using (StringReader xReader = new StringReader (value))
            {
                string? xLine;

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    // 文字列の最適化を行うことなく、
                    //     トリミングすればなくなる行を無視

                    if (string.IsNullOrWhiteSpace (xLine) == false)
                        xLines.Add (xLine);

                    else xLines.Add (string.Empty);
                }
            }

            List <string> xParagraphs = new List <string> ();

            StringBuilder xParagraph = new StringBuilder ();

            void iAddParagraph ()
            {
                if (xParagraph.Length > 0)
                {
                    xParagraphs.Add (xParagraph.ToString ());
                    xParagraph.Clear ();
                }
            }

            foreach (string xLine in xLines)
            {
                if (xLine.Length > 0)
                {
                    if (xParagraph.Length > 0)
                        xParagraph.AppendLine ();

                    xParagraph.Append (xLine);
                }

                else iAddParagraph ();
            }

            // xParagraph に何か残っている可能性
            iAddParagraph ();

            return xParagraphs;
        }

        /// <summary>
        /// パーズできなかった chunk の List を返す
        /// </summary>
        public static List <string> LoadLogFile (string filePath, out SortedList <DateTime, LogInfo> result)
        {
            if (Path.IsPathFullyQualified (filePath) == false)
                throw new InvalidOperationException ();

            var xLogs = new SortedList <DateTime, LogInfo> ();
            List <string> xBadChunks = new List <string> ();

            foreach (string xChunk in StringToParagraphs (File.ReadAllText (filePath, Encoding.UTF8)))
            {
                try
                {
                    LogInfo xLog = LogInfo.ParseChunk (xChunk);
                    xLogs.Add (xLog.StartUtc, xLog);
                }

                catch
                {
                    xBadChunks.Add (xChunk);
                }
            }

            result = xLogs;
            return xBadChunks;
        }

        private static string? mAppDirectoryPath = null;

        public static string AppDirectoryPath
        {
            get
            {
                if (mAppDirectoryPath == null)
                    mAppDirectoryPath = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);

                return mAppDirectoryPath!;
            }
        }

        public static string MapPath (string relativePath)
        {
            if (Path.IsPathFullyQualified (relativePath))
                throw new InvalidOperationException ();

            return Path.Join (AppDirectoryPath, relativePath);
        }

        // 決め打ち
        // 日付を - で区切る国がたくさんあるが、日本やアメリカでは /
        // ローカライズする可能性は低い
        // 「○年○月○日」のようなのは長ったらしい

        public readonly static string DateTimeFriendlyFormatString = "yyyy'/'M'/'d' 'H':'mm";

        public static string UtcToLocalTimeFriendlyString (DateTime value)
        {
            return value.ToLocalTime ().ToString (DateTimeFriendlyFormatString, CultureInfo.InvariantCulture);
        }

        public static string TimeSpanToString (TimeSpan value)
        {
            long xSeconds = (long) value.TotalSeconds;

            // 最初は「時」と「分」だけだったが、2～3分、早ければ1分未満で終わるタスクもある
            // そのとき、59秒が「0分」となったり、1分59分が「1分」となったりするのでは誤差が大きい

            if (xSeconds >= 3600)
                return FormattableString.Invariant ($"{xSeconds / 3600}時間{xSeconds % 3600 / 60}分");

            else if (xSeconds >= 600)
                return FormattableString.Invariant ($"{xSeconds / 60}分");

            else if (xSeconds >= 60)
                return FormattableString.Invariant ($"{xSeconds / 60}分{xSeconds % 60}秒");

            else return FormattableString.Invariant ($"{xSeconds}秒");
        }

        public static string IndentLines (string value, int depth)
        {
            List <string> xLines = new List <string> ();

            using (StringReader xReader = new StringReader (value))
            {
                string? xLine;

                while ((xLine = xReader.ReadLine ()) != null)
                    xLines.Add (xLine.TrimEnd ());
            }

            string xIndents = string.Concat (Enumerable.Repeat ("\x20\x20\x20\x20", depth));

            return string.Join (Environment.NewLine, xLines.Select (x =>
            {
                if (x.Length > 0)
                    return xIndents + x;

                else return x;
            }));
        }
    }
}
