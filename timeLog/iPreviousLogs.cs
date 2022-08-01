using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace timeLog
{
    // このプログラム専用のクラス
    // API 的に他から使われる可能性が低いので Shared に入らない
    // 初期の手抜きにより設計が良くないが、そのことは taskKiller のログに

    internal static class iPreviousLogs
    {
        public readonly static LogFile LogFile = new LogFile (Shared.MapPath ("timeLogs.txt"));

        // LogFile.Logs とかぶるが、そちらは SortedList という違いがある
        // Shared 内のものを API 的に考えるなら、ログデータの順序は保証されなければならない
        // 一方、WPF でコントロールとバインディングするものは、この形式でないといけない

        private static ObservableCollection <LogInfo>? mLogs = null;

        public static ObservableCollection <LogInfo> Logs
        {
            get
            {
                if (mLogs == null)
                {
                    mLogs = new ObservableCollection <LogInfo> ();

                    foreach (var xPair in LogFile.Logs)
                        mLogs.Add (xPair.Value);

                    CollectionViewSource.GetDefaultView (mLogs).SortDescriptions.Add (
                        new SortDescription ("StartUtc", ListSortDirection.Descending));
                }

                return mLogs;
            }
        }

        public static void AddLog (LogInfo log)
        {
            LogFile.AddLog (log);
            Logs.Add (log);
        }

        public static void DeleteLog (LogInfo log)
        {
            LogFile.DeleteLog (log);
            Logs.Remove (log);
        }
    }
}
