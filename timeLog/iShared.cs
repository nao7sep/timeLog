using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace timeLog
{
    internal static class iShared
    {
        public static void HandleException (Window? owner, Exception exception)
        {
            iLogger.WriteSafely ("エラーが発生しました:" + Environment.NewLine + Shared.IndentLines (exception.ToString ().TrimEnd (), 1));

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
    }
}
