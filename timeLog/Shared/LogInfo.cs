using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        public readonly List <string> Tasks;

        private string? mTasksString = null;

        public string TasksString
        {
            get
            {
                if (mTasksString == null)
                    mTasksString = string.Join (Environment.NewLine, Tasks);

                return mTasksString;
            }
        }

        public readonly bool IsDisoriented;

        public string IsDisorientedFriendlyString
        {
            get
            {
                return IsDisoriented ? "グダグダ" : string.Empty;
            }
        }

        // 今のところ困っていないので readonly 変数のまま

        public readonly DateTime EndUtc;

        private string? mEndUtcFriendlyString = null;

        public string EndUtcFriendlyString
        {
            get
            {
                if (mEndUtcFriendlyString == null)
                    mEndUtcFriendlyString = Shared.UtcToLocalTimeFriendlyString (EndUtc);

                return mEndUtcFriendlyString;
            }
        }

        private TimeSpan? mElapsedTime = null;

        public TimeSpan ElapsedTime
        {
            get
            {
                if (mElapsedTime == null)
                    mElapsedTime = EndUtc - StartUtc;

                return mElapsedTime.Value;
            }
        }

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

        public LogInfo (DateTime startUtc, List <string> tasks, bool isDisoriented, DateTime endUtc)
        {
            StartUtc = startUtc;
            Tasks = tasks;
            IsDisoriented = isDisoriented;
            EndUtc = endUtc;
        }

        public string ToChunk ()
        {
            // こういうまとまりを空行で区切ってファイルに出力する

            // ヘッダーとタスクリストの明示的な区別のために // を行頭に置く
            // 行数でも処理できるが、将来的にヘッダーを拡張する可能性はゼロでない

            StringBuilder xBuilder = new StringBuilder ();
            xBuilder.AppendLine ("StartUtc:" + StartUtc.ToString ("O"));
            xBuilder.AppendLine ("IsDisoriented:" + IsDisoriented.ToString ());
            xBuilder.AppendLine ("EndUtc:" + EndUtc.ToString ("O"));
            xBuilder.AppendLine (string.Join (Environment.NewLine, Tasks.Select (x => "//\x20" + x)));
            return xBuilder.ToString ();
        }

        public static LogInfo ParseChunk (string value)
        {
            using (StringReader xReader = new StringReader (value))
            {
                // 何か問題があればどこかで落ちる実装に
                // 何もしなくても勝手に落ちるところはノーチェックで処理し、
                //     自分で投げないといけないところではそうしている

                // ジェネリックのローカル関数を作りコードの共通化を試みたが、DateTime や bool を T に変換できないというエラーが出た
                // キャストなどで解決しなかったので、何らかの制限またはバグがあるか、寝不足の自分が何か変なことをしていたか

                string? xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("StartUtc:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                DateTime xStartUtc = DateTime.ParseExact (xLine.Substring ("StartUtc:".Length), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("IsDisoriented:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                bool xIsDisoriented = bool.Parse (xLine.Substring ("IsDisoriented:".Length));

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("EndUtc:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                DateTime xEndUtc = DateTime.ParseExact (xLine.Substring ("EndUtc:".Length), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                // -----------------------------------------------------------------------------

                List <string> xTasks = new List <string> ();

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    if (xLine.StartsWith ("//\x20") == false)
                        throw new FormatException ();

                    xTasks.Add (xLine.Substring ("//\x20".Length));
                }

                if (xTasks.Count == 0)
                    throw new FormatException ();

                return new LogInfo (xStartUtc, xTasks, xIsDisoriented, xEndUtc);
            }
        }
    }
}
