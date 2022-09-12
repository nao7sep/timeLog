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

        // null チェックの回避のため、LoadPreviousInfo において値がなければ TimeSpan.Zero に
        public static TimeSpan PreviousElapsedTime;

        public static void LoadPreviousInfo ()
        {
            if (DateTime.TryParseExact (iShared.Session.GetStringOrDefault ("PreviousStartUtc", string.Empty), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime xResult))
                PreviousStartUtc = xResult;

            // 明示的に
            else PreviousStartUtc = null;

            if (TimeSpan.TryParseExact (iShared.Session.GetStringOrDefault ("PreviousElapsedTime", string.Empty), "c", CultureInfo.InvariantCulture, out TimeSpan xResultAlt))
                PreviousElapsedTime = xResultAlt;

            else PreviousElapsedTime = TimeSpan.Zero;
        }

        // 保存までは行わないので Apply* に

        public static void ApplyPreviousInfo ()
        {
            if (PreviousStartUtc != null)
            {
                iShared.Session.SetString ("PreviousStartUtc", PreviousStartUtc.Value.ToString ("O"));
                iShared.Session.SetString ("PreviousElapsedTime", PreviousElapsedTime.ToString ("c"));
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
        public static bool IsRunning;

        // カウンターの表示を更新するループで繰り返し見られるのでコントロールの値をコピー
        // バインディングでやるべきことだろうが、古い設計のプログラムなので既存のコードと同様に

        public static bool AutoPauses
        {
            get
            {
                return Stopwatch.AutoPauses;
            }

            set
            {
                Stopwatch.AutoPauses = value;
            }
        }

        // 自動中断機能でなく、ボタンにより中断された場合に false に
        // IsRunning == true なら「開始」が押されていて、IsPausedManually == false なら「中断」は押されていない
        // その状況でカウントが止まっているなら、自動中断中を疑い、Stopwatch.IsRunning == false をチェック
        public static bool IsPausedManually;

        // null チェックを省くため、単一のインスタンスを Reset で使い回す
        public static readonly nStopwatch Stopwatch = new nStopwatch
        {
#if DEBUG
            AutoPausingInterval = TimeSpan.FromSeconds (3)
#endif
        };
    }
}
