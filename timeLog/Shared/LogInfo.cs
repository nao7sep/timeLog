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

        // プロパティーでないと Binding できないようだ

        public bool IsValuable { get; private set; }

        public string IsValuableFriendlyString
        {
            get
            {
                return Shared.IsValuableToFriendlyString (IsValuable);
            }
        }

        public bool IsDisoriented { get; private set; }

        public string IsDisorientedFriendlyString
        {
            get
            {
                return Shared.IsDisorientedToFriendlyString (IsDisoriented);
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

        public LogInfo (DateTime startUtc, List <string> tasks, bool isValuable, bool isDisoriented, TimeSpan elapsedTime)
        {
            StartUtc = startUtc;
            Tasks = tasks;
            IsValuable = isValuable;
            IsDisoriented = isDisoriented;
            ElapsedTime = elapsedTime;
        }

        public string ToChunk ()
        {
            // こういうまとまりを空行で区切ってファイルに出力する

            // ヘッダーとタスクリストの明示的な区別のために // を行頭に置く
            // 行数でも処理できるが、将来的にヘッダーを拡張する可能性はゼロでない

            StringBuilder xBuilder = new StringBuilder ();
            xBuilder.AppendLine ("StartUtc:" + StartUtc.ToString ("O"));
            xBuilder.AppendLine ("IsValuable:" + IsValuable.ToString ());
            xBuilder.AppendLine ("IsDisoriented:" + IsDisoriented.ToString ());
            xBuilder.AppendLine ("ElapsedTime:" + ElapsedTime.ToString ("c"));
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

                string? xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("StartUtc:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                DateTime xStartUtc = DateTime.ParseExact (xLine.Substring ("StartUtc:".Length), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("IsValuable:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                bool xIsValuable = bool.Parse (xLine.Substring ("IsValuable:".Length));

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("IsDisoriented:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                bool xIsDisoriented = bool.Parse (xLine.Substring ("IsDisoriented:".Length));

                // -----------------------------------------------------------------------------

                xLine = xReader.ReadLine ();

                if (xLine!.StartsWith ("ElapsedTime:", StringComparison.OrdinalIgnoreCase) == false)
                    throw new FormatException ();

                TimeSpan xElapsedTime = TimeSpan.ParseExact (xLine.Substring ("ElapsedTime:".Length), "c", CultureInfo.InvariantCulture);

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

                return new LogInfo (xStartUtc, xTasks, xIsValuable, xIsDisoriented, xElapsedTime);
            }
        }
    }
}
