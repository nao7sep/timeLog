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
        public static void AddLogToFile (string filePath, LogInfo log)
        {
            if (Path.IsPathFullyQualified (filePath) == false)
                throw new ArgumentException ();

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
                throw new ArgumentException ();

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

        // 追加が削除より圧倒的に多いプログラムなので追記型の実装を行った
        // つまり、1000件のデータに1件を追加するだけで1001件の（再）出力にしない
        // その上での削除の実装なので、こちらは追加ほど低コストでない

        public static void DeleteLogFromFile (string filePath, LogInfo log)
        {
            if (Path.IsPathFullyQualified (filePath) == false)
                throw new ArgumentException ();

            string xChunk = log.ToChunk ();

            // ファイルが存在しなければ落とす
            // 完全に一致するものだけを除外

            var xChunks = StringToParagraphs (File.ReadAllText (filePath, Encoding.UTF8)).
                Select (x => x + Environment.NewLine).
                Where (x => x.Equals (xChunk, StringComparison.Ordinal) == false);

            if (xChunks.Any ())
                File.WriteAllText (filePath, string.Join (Environment.NewLine, xChunks), Encoding.UTF8);

            // 空でも BOM 分で空行の処理がおかしくなるので消す
            else File.Delete (filePath);
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
                throw new ArgumentException ();

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

        public static string SecondsToString (double value)
        {
            long xSeconds = (long) value;

            // 「秒」がなくなると計測中かどうか分からなくなるので、常に表示されるように変更
            // また、「分」や「秒」の桁数によりフォントサイズが何度も変更されないようにゼロ詰め

            if (xSeconds >= 3600)
                return FormattableString.Invariant ($"{xSeconds / 3600}時間{xSeconds % 3600 / 60 :D2}分{xSeconds % 60 :D2}秒");

            else if (xSeconds >= 60)
                return FormattableString.Invariant ($"{xSeconds / 60}分{xSeconds % 60 :D2}秒");

            else return FormattableString.Invariant ($"{xSeconds}秒");
        }

        public static string TimeSpanToString (TimeSpan value)
        {
            return SecondsToString (value.TotalSeconds);
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
                // 空白系文字の扱いに関する仕様変更を反映

                if (string.IsNullOrWhiteSpace (x) == false)
                    return xIndents + x;

                else return string.Empty;
            }));
        }

        public static string IsValuableToFriendlyString (bool value)
        {
            return value ? "付加価値あり" : "どうでもよいこと";
        }

        public static string IsDisorientedToFriendlyString (bool value)
        {
            return value ? "集中した" : "ダラダラした";
        }

        public readonly static string DateFriendlyFormatString = "yyyy'/'M'/'d";

        public static string DateToFriendlyString (DateTime value)
        {
            return value.ToString (DateFriendlyFormatString, CultureInfo.InvariantCulture);
        }

        public static (string ShortString, string LongString) GetStatistics (LogFile file)
        {
            var xData1 = file.Logs.Select (x => (x.Key.ToLocalTime ().AddHours (-4).Date, x.Value));

            var xData2 = xData1.DistinctBy (x => x.Date).Select (x =>
            {
                var xData3 = xData1.Where (y => y.Date == x.Date).Select (y => y.Value);

                double
                    xValuableAndNotDisorientedSeconds = xData3.Where (y => y.IsValuable && y.IsDisoriented == false).Sum (y => y.ElapsedTime.TotalSeconds),
                    xValuableAndDisorientedSeconds = xData3.Where (y => y.IsValuable && y.IsDisoriented).Sum (y => y.ElapsedTime.TotalSeconds),
                    xValuableSeconds = xValuableAndNotDisorientedSeconds + xValuableAndDisorientedSeconds,
                    xNotValuableAndNotDisorientedSeconds = xData3.Where (y => y.IsValuable == false && y.IsDisoriented == false).Sum (y => y.ElapsedTime.TotalSeconds),
                    xNotValuableAndDisorientedSeconds = xData3.Where (y => y.IsValuable == false && y.IsDisoriented).Sum (y => y.ElapsedTime.TotalSeconds),
                    xNotValuableSeconds = xNotValuableAndNotDisorientedSeconds + xNotValuableAndDisorientedSeconds,
                    xTotalSeconds = xValuableSeconds + xNotValuableSeconds,
                    xNotDisorientedSeconds = xValuableAndNotDisorientedSeconds + xNotValuableAndNotDisorientedSeconds,
                    xDisorientedSeconds = xValuableAndDisorientedSeconds + xNotValuableAndDisorientedSeconds;

                int xValuableSecondsPercentage = (int) Math.Round (xValuableSeconds * 100 / xTotalSeconds),
                    xValuableAndNotDisorientedSecondsPercentage = (int) Math.Round (xValuableAndNotDisorientedSeconds * 100 / xTotalSeconds),
                    xValuableAndDisorientedSecondsPercentage = xValuableSecondsPercentage - xValuableAndNotDisorientedSecondsPercentage,
                    xNotValuableSecondsPercentage = 100 - xValuableSecondsPercentage,
                    xNotValuableAndNotDisorientedSecondsPercentage = (int) Math.Round (xNotValuableAndNotDisorientedSeconds * 100 / xTotalSeconds),
                    xNotValuableAndDisorientedSecondsPercentage = xNotValuableSecondsPercentage - xNotValuableAndNotDisorientedSecondsPercentage,
                    xNotDisorientedSecondsPercentage = (int) Math.Round (xNotDisorientedSeconds * 100 / xTotalSeconds),
                    xDisorientedSecondsPercentage = 100 - xNotDisorientedSecondsPercentage;

                string xFriendlyString = FormattableString.Invariant (
$@"{IsValuableToFriendlyString (true)}: {SecondsToString (xValuableSeconds)}（{xValuableSecondsPercentage}％）
    {IsDisorientedToFriendlyString (true)}: {SecondsToString (xValuableAndDisorientedSeconds)}（{xValuableAndDisorientedSecondsPercentage}％）
    {IsDisorientedToFriendlyString (false)}: {SecondsToString (xValuableAndNotDisorientedSeconds)}（{xValuableAndNotDisorientedSecondsPercentage}％）
{IsValuableToFriendlyString (false)}: {SecondsToString (xNotValuableSeconds)}（{xNotValuableSecondsPercentage}％）
    {IsDisorientedToFriendlyString (true)}: {SecondsToString (xNotValuableAndDisorientedSeconds)}（{xNotValuableAndDisorientedSecondsPercentage}％）
    {IsDisorientedToFriendlyString (false)}: {SecondsToString (xNotValuableAndNotDisorientedSeconds)}（{xNotValuableAndNotDisorientedSecondsPercentage}％）
-------------------------
{IsDisorientedToFriendlyString (true)}: {SecondsToString (xDisorientedSeconds)}（{xDisorientedSecondsPercentage}％）
{IsDisorientedToFriendlyString (false)}: {SecondsToString (xNotDisorientedSeconds)}（{xNotDisorientedSecondsPercentage}％）");

                return (x.Date, FriendlyString: xFriendlyString);
            }).
            OrderByDescending (x => x);

            string iBuild (bool isShortVersion)
            {
                var xData4 = (isShortVersion ? xData2!.Take (7) : xData2).Select (x =>
                {
                    return $"{DateToFriendlyString (x.Date)}:{Environment.NewLine}{IndentLines (x.FriendlyString, 1)}";
                });

                string xString = string.Join (Environment.NewLine + Environment.NewLine, xData4);

                if (xString.Length > 0)
                    return xString + Environment.NewLine;

                else return xString;
            }

            return (iBuild (true), iBuild (false));
        }

        public static void SaveStatistics (string value)
        {
            string xFilePath = MapPath ("timeLog.Statistics.txt");

            if (File.Exists (xFilePath) == false || File.ReadAllText (xFilePath, Encoding.UTF8) != value)
                File.WriteAllText (xFilePath, value, Encoding.UTF8);
        }
    }
}
