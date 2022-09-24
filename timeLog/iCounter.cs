using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace timeLog
{
    internal static class iCounter
    {
        // セッション情報のファイルにキーがなければ null になり、
        //     前回、カウント中でも中断中でもない状態で終わったことが示される
        // その場合、関連項目である mIsDisoriented などの値は復元されない

        // キーがあり、値を読めれば、mIsDisoriented などが復元され、
        //     カウントが中断されている状態から操作可能に

        public static DateTime? PreviousStartUtc;

        public static TimeSpan? PreviousElapsedTime;

        public static void LoadPreviousInfo ()
        {
            // プログラムの起動時に一度だけ呼ばれるメソッド
            // 以下の二つは、いずれも値がなければ null のまま

            if (DateTime.TryParseExact (iShared.Session.GetStringOrDefault ("PreviousStartUtc", string.Empty), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime xResult))
                PreviousStartUtc = xResult;

            if (TimeSpan.TryParseExact (iShared.Session.GetStringOrDefault ("PreviousElapsedTime", string.Empty), "c", CultureInfo.InvariantCulture, out TimeSpan xResultAlt))
                PreviousElapsedTime = xResultAlt;
        }

        // 保存までは行わないので Apply* に

        public static void ApplyPreviousInfo ()
        {
            if (PreviousStartUtc != null)
            {
                iShared.Session.SetString ("PreviousStartUtc", PreviousStartUtc.Value.ToString ("O"));
                iShared.Session.SetString ("PreviousElapsedTime", (PreviousElapsedTime ?? TimeSpan.Zero).ToString ("c"));
            }

            else
            {
                iShared.Session.Delete ("PreviousStartUtc");
                iShared.Session.Delete ("PreviousElapsedTime");
            }
        }

        // mStartNextTasks によりタスクが開始され、mEndCurrentTasks により終了されるまで true に
        // これは、nStopwatch による自動中断、mPauseOrResumeCounting、プログラムの終了などに影響されない
        // 起動時のみ、PreviousStartUtc に値があれば true になるという、引き継ぎのための特例がある
        // それはつまり、前回が計測中のプログラム終了なら、今回は計測中かつ中断中のものを「再開」できるところから始まるということ

        // IsRunning では nStopwatch の IsRunning と紛らわしかったので名前を変更

        public static bool AreTasksStarted;

        // 自動中断機能でなく、ボタンにより中断された場合に false に
        // AreTasksStarted == true なら「開始」が押されていて、IsPausedManually == false なら「中断」は押されていない
        // その状況でカウントが止まっているなら、自動中断中を疑い、Stopwatch.IsRunning == false をチェック
        public static bool IsPausedManually;

        // null チェックを省くため、単一のインスタンスを Reset で使い回す
        // Task や、それを内部的に生成するものをいくつも作る実装は、とても分かりにくい

        public static readonly nStopwatch Stopwatch = new nStopwatch
        {
#if DEBUG
            AutoPausingInterval = TimeSpan.FromSeconds (3)
#endif
        };

        public static DateTime GetStartUtc ()
        {
            // 前回のデータがあれば、前回の続きなのでその値を使う
            // なくて、nStopwatch 内に古いデータがあるなら、初回の Pause/Stop の値を使う
            // いずれもないなら、新規カウントにおいて一度も Pause/Stop されていない状態なので現行の値を

            if (PreviousStartUtc != null)
                return PreviousStartUtc.Value;

            else if (Stopwatch.PreviousEntries.Count > 0)
                return Stopwatch.PreviousEntries [0].StartUtc;

            else return Stopwatch.CurrentEntryStartUtc!.Value;
        }
    }
}
