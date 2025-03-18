using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nekote;

namespace timeLog
{
    public class LogInfo
    {
        // 毎回 get* メソッドというのが好きになれず、できるだけ readonly 変数を使うが、
        //     ICollectionView でソートするにはプロパティーでないといけないようだ

        public DateTime StartUtc { get; }

        private string? mStartUtcFriendlyString = null;

        public string StartUtcFriendlyString
        {
            get
            {
                if (mStartUtcFriendlyString == null)
                    mStartUtcFriendlyString = Shared.UtcToLocalTimeFriendlyString (StartUtc);

                return mStartUtcFriendlyString;
            }
        }

        public string TasksString { get; init; }

        // プロパティーでないと Binding できないようだ

        public bool IsValuable { get; private set; }

        public string IsValuableFriendlyString
        {
            get
            {
                return Shared.IsValuableToFriendlyString (IsValuable);
            }
        }

        public bool IsFocused { get; private set; }

        public string IsFocusedFriendlyString
        {
            get
            {
                return Shared.IsFocusedToFriendlyString (IsFocused);
            }
        }

        // 今のところ困っていないので readonly 変数のまま

        public readonly TimeSpan ElapsedTime;

        private string? mElapsedTimeString = null;

        public string ElapsedTimeString
        {
            get
            {
                if (mElapsedTimeString == null)
                    mElapsedTimeString = Shared.TimeSpanToString (ElapsedTime);

                return mElapsedTimeString;
            }
        }

        public string? ResultsString { get; init; }

        public bool HasResults
        {
            get
            {
                return string.IsNullOrWhiteSpace (ResultsString) == false;
            }
        }

        public LogInfo (DateTime startUtc, string tasksString, bool isValuable, bool isFocused, TimeSpan elapsedTime, string? resultsString)
        {
            StartUtc = startUtc;
            TasksString = tasksString;
            IsValuable = isValuable;
            IsFocused = isFocused;
            ElapsedTime = elapsedTime;
            ResultsString = resultsString;
        }

        public string ToChunk ()
        {
            // こういうまとまりを空行で区切ってファイルに出力する

            // ヘッダーとタスクリストの明示的な区別のために // を行頭に置く
            // 行数でも処理できるが、将来的にヘッダーを拡張する可能性はゼロでない

            StringBuilder xBuilder = new StringBuilder ();
            xBuilder.AppendLine ("StartUtc:" + StartUtc.ToString ("O"));
            xBuilder.AppendLine ("IsValuable:" + IsValuable.ToString ());
            xBuilder.AppendLine ("IsFocused:" + IsFocused.ToString ());
            xBuilder.AppendLine ("ElapsedTime:" + ElapsedTime.ToString ("c"));

            string _StringToLines (string str, string prefix)
            {
                // EnumerateLines, by default, removes redundant empty lines.
                return string.Join (Environment.NewLine, str.EnumerateLines ().Select (x =>
                {
                    if (string.IsNullOrEmpty (x) == false)
                        return $"{prefix}\x20{x}";

                    else return prefix;
                }));
            }

            xBuilder.AppendLine (_StringToLines (TasksString, "//"));

            if (HasResults)
                xBuilder.AppendLine (_StringToLines (ResultsString!, "=>"));

            return xBuilder.ToString ();
        }

        public static LogInfo ParseChunk (string value)
        {
            using (StringReader xReader = new StringReader (value))
            {
                // 何か問題があればどこかで落ちる実装に
                // 何もしなくても勝手に落ちるところはノーチェックで処理し、
                //     自分で投げないといけないところではそうしている

                string? xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("StartUtc:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                DateTime xStartUtc = DateTime.ParseExact (xLine.AsSpan ("StartUtc:".Length), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("IsValuable:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                bool xIsValuable = bool.Parse (xLine.AsSpan ("IsValuable:".Length));

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("IsFocused:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                bool xIsFocused = bool.Parse (xLine.AsSpan ("IsFocused:".Length));

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("ElapsedTime:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                TimeSpan xElapsedTime = TimeSpan.ParseExact (xLine.AsSpan ("ElapsedTime:".Length), "c", CultureInfo.InvariantCulture);

                // -----------------------------------------------------------------------------

                List <string> xTasks = new List <string> (),
                    xResults = new List <string> ();

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    if (xLine.StartsWith ("//\x20"))
                        xTasks.Add (xLine.Substring ("//\x20".Length));

                    else if (xLine.StartsWith ("//"))
                        xTasks.Add (string.Empty);

                    else if (xLine.StartsWith ("=>\x20"))
                        xResults.Add (xLine.Substring ("=>\x20".Length));

                    else if (xLine.StartsWith ("=>"))
                        xResults.Add (string.Empty);

                    else throw new FormatException ();
                }

                if (xTasks.Count == 0)
                    throw new FormatException ();

                // Whenever data is acquired, text must be optimized to reduce redundant empty lines.

                string? xTasksString = string.Join (Environment.NewLine, xTasks).Optimize (),
                       xResultsString = xResults.Count > 0 ? string.Join (Environment.NewLine, xResults).Optimize () : null;

                // HasResults を通るので空の List でもよいが、一応、null に整えておく
                return new LogInfo (xStartUtc, xTasksString!, xIsValuable, xIsFocused, xElapsedTime, xResultsString);
            }
        }
    }
}
