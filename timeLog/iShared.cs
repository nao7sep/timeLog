using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Notification.Wpf;

namespace timeLog
{
    internal static class iShared
    {
        // 通知画面の配色は決め打ち
        // いじれるようにすることを考えたが、ほかも色はいじれないので、慣れた方が早い

        // 状態は三つ
        // タスクが開始されていない → NotStarted
        // 開始されていて、一時的に中断されている → Paused
        // 開始されている（計測中） → Counting

        // それぞれについて、赤、黄色、青またはダイアログの色、というのを最初は考えたが、やって見ると自分には赤が地味で、黄色の方が目立った
        // 赤は、おそらく、黒を基調とする UI においては警告色となるが、白が基調なら、ちょっと目立つアクセント程度なのだろう
        // 実際、プレゼンなどを作成するにおいて「ここだけ特に目立ってほしい」に赤を使う人がいて、警告されているような印象は抱かない

        // そのため、区別がつき、それぞれが同等に主張し、一時的な中断は休憩なのだからリラックスできる色にするとの考えにより、現行の配色にした
        // NotStarted を濃いピンクにするのは、赤と同系色でありながら、ポップで、しつこくなく、慣れたら気にもならなくなる色だろうから

        public static readonly Brush NotStartedNotificationBackgroundColor = new SolidColorBrush (Colors.DeepPink);

        public static readonly Brush PausedNotificationBackgroundColor = new SolidColorBrush (Colors.DodgerBlue);

        public static readonly Brush CountingNotificationBackgroundColor = new SolidColorBrush (Color.FromRgb (0xF0, 0xF0, 0xF0));

        public static readonly Brush NotStartedNotificationForegroundColor = new SolidColorBrush (Colors.White);

        public static readonly Brush PausedNotificationForegroundColor = new SolidColorBrush (Colors.White);

        public static readonly Brush CountingNotificationForegroundColor = new SolidColorBrush (Colors.Black);

        public static readonly NotificationManager NotificationManager = new NotificationManager ();

        public static readonly NotificationContent NotificationContent = new NotificationContent
        {
            Title = "timeLog"
        };

        public static void HandleException (Window? owner, Exception exception)
        {
            iLogger.Write_catch ("エラーが発生しました:" + Environment.NewLine + Shared.IndentLines (exception.ToString ().TrimEnd (), 1));

            const string xMessage = "エラーが発生しました。timeLog.Errors.txt を確認してください。";

            if (owner != null)
                MessageBox.Show (owner, xMessage);

            else MessageBox.Show (xMessage);
        }

        // Window の Closed イベントのあと、いつまでコントロールにアクセスできるのか不詳
        // Closed 内で true に設定し、その後はコントロールに一切アクセスしない

        public static bool IsWindowClosed;

        public static readonly KvsFile Session = new KvsFile (Shared.MapPath ("timeLog.Session.txt"));

        // 低い可能性だが、他のコントロールでも使うかもしれないので iShared に

        public static Size GetFormattedTextSize (string value, Typeface typeface, double fontSize, double pixelsPerDip)
        {
            FormattedText xText = new FormattedText (value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black, pixelsPerDip);
            return new Size (xText.Width, xText.Height);
        }

        public static double GetProperFontSize (double maxWidth, double maxHeight, string value, Typeface typeface, double pixelsPerDip)
        {
            double xFontSize = 1;

            while (true)
            {
                Size xSize = GetFormattedTextSize (value, typeface, xFontSize, pixelsPerDip);

                if (xSize.Width < maxWidth && xSize.Height < maxHeight)
                    xFontSize ++;

                else break;
            }

            return xFontSize;
        }

        // 以前はプログラムの終了時のみ保存されたデータ
        // いつプログラムがクラッシュしてもいいように、定期的な保存に変更

        public static void SavePreviousInfo ()
        {
            if (iCounter.AreTasksStarted)
            {
                iCounter.PreviousStartUtc = iCounter.GetStartUtc ();
                iCounter.PreviousElapsedTime = iCounter.Stopwatch.TotalElapsedTime;
            }

            else
            {
                iCounter.PreviousStartUtc = null;
                iCounter.PreviousElapsedTime = null;
            }

            iCounter.ApplyPreviousInfo ();
            Session.Save ();
        }
    }
}
