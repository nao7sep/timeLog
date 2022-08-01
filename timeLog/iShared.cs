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
            iLogger.WriteSafely ("エラーが発生しました:" + Environment.NewLine + Shared.IndentLines (exception.ToString ().TrimEnd (), 1));

            const string xMessage = "エラーが発生しました。timeLog.Errors.txt を確認してください。";

            if (owner != null)
                MessageBox.Show (owner, xMessage);

            else MessageBox.Show (xMessage);
        }

        // ややイレギュラーだが、すぐにセッション情報を書き込むためにプロパティーとして

        private static DateTime? mCurrentTasksStartUtc;

        public static DateTime? CurrentTasksStartUtc
        {
            get
            {
                return mCurrentTasksStartUtc;
            }

            set
            {
                mCurrentTasksStartUtc = value;

                if (value != null)
                    Session.SetString ("CurrentTasksStartUtc", value.Value.ToString ("O"));

                else Session.SetString ("CurrentTasksStartUtc", string.Empty);
            }
        }

        // Window の Closed イベントのあと、いつまでコントロールにアクセスできるのか不詳
        // Closed 内で true に設定し、その後はコントロールに一切アクセスしない

        public static bool IsWindowClosed;

        public static readonly KvsFile Session = new KvsFile (Shared.MapPath ("timeLog.Session.txt"));
    }
}
