using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace timeLog
{
    internal static class iShared
    {
        public static void HandleException (Window? owner, Exception exception)
        {
            iLogger.WriteSafe ("エラーが発生しました:" + Environment.NewLine + Shared.IndentLines (exception.ToString ().TrimEnd (), 1));

            const string xMessage = "エラーが発生しました。Errors.log を確認してください。";

            if (owner != null)
                MessageBox.Show (owner, xMessage);

            else MessageBox.Show (xMessage);
        }

        public static DateTime? CurrentTasksStartUtc;
    }
}
