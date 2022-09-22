using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Nekote;
using SharpHook;

namespace timeLog
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        public MainWindow ()
        {
            InitializeComponent ();
        }

        private TaskPoolGlobalHook? mGlobalHook = null;

        // mGlobalHook の Dispose により終了する
        // 今のところ不要だが、一応
        private Task? mHookingTask = null;

        private void mWindow_Initialized (object sender, EventArgs e)
        {
            try
            {
                if (double.TryParse (ConfigurationManager.AppSettings ["InitialWidth"], out double xResult))
                    mWindow.Width = xResult;

                if (double.TryParse (ConfigurationManager.AppSettings ["InitialHeight"], out double xResultAlt))
                    mWindow.Height = xResultAlt;

                if (bool.TryParse (ConfigurationManager.AppSettings ["IsMaximized"], out bool xResultAlt1))
                {
                    if (xResultAlt1)
                        mWindow.WindowState = WindowState.Maximized;
                }

                string? xFontFamily = ConfigurationManager.AppSettings ["FontFamily"];

                if (string.IsNullOrEmpty (xFontFamily) == false)
                    mWindow.FontFamily = new FontFamily (xFontFamily);

                // 不要
                // iUpdateControls ();

                mGlobalHook = new TaskPoolGlobalHook ();
                mGlobalHook.KeyTyped += mGlobalHook_KeyTyped;
                mGlobalHook.MouseClicked += mGlobalHook_MouseClicked;
                mHookingTask = mGlobalHook.RunAsync ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iUpdateControls ()
        {
            bool xAreNextTasksOK = string.IsNullOrEmpty (mNextTasks.Text) == false && Shared.ParseTasksString (mNextTasks.Text).Count > 0,
                xAreCurrentTasksOK = string.IsNullOrEmpty (mCurrentTasks.Text) == false && Shared.ParseTasksString (mCurrentTasks.Text).Count > 0;

            if (iCounter.AreTasksStarted)
            {
                // 実行中のタスクを「終了」にして押し出すため、
                //     そちらもデータが揃っているのを確認

                if (xAreNextTasksOK && xAreCurrentTasksOK)
                    mStartNextTasks.IsEnabled = true;

                else mStartNextTasks.IsEnabled = false;

                mCurrentTasks.IsEnabled = true;
                mAutoPauses.IsEnabled = true;

                mPauseOrResumeCounting.IsEnabled = true;

                if (iCounter.IsPausedManually == false)
                    mPauseOrResumeCounting.Content = "中断";

                else mPauseOrResumeCounting.Content = "再開";

                mAreCurrentTasksValuable.IsEnabled = true;
                mIsDisoriented.IsEnabled = true;

                if (xAreCurrentTasksOK)
                    mEndCurrentTasks.IsEnabled = true;

                else mEndCurrentTasks.IsEnabled = false;

                mElapsedTimeLabel.Visibility = Visibility.Visible;
                mElapsedTime.Visibility = Visibility.Visible;
            }

            else
            {
                if (xAreNextTasksOK)
                    mStartNextTasks.IsEnabled = true;

                else mStartNextTasks.IsEnabled = false;

                mCurrentTasks.IsEnabled = false;
                mAutoPauses.IsEnabled = false;
                mPauseOrResumeCounting.IsEnabled = false;
                mPauseOrResumeCounting.Content = "中断";
                mAreCurrentTasksValuable.IsEnabled = false;
                mIsDisoriented.IsEnabled = false;
                mEndCurrentTasks.IsEnabled = false;

                mElapsedTimeLabel.Visibility = Visibility.Collapsed;
                mElapsedTime.Visibility = Visibility.Collapsed;
            }

            if (mPreviousTasks.SelectedItem != null)
                mDeleteSelectedLog.IsEnabled = true;

            else mDeleteSelectedLog.IsEnabled = false;
        }

        private void iUpdateStatistics ()
        {
            var xStatistics = Shared.GetStatistics (iPreviousLogs.LogFile);
            Shared.SaveStatistics (xStatistics.LongString);
            mStatistics.Text = xStatistics.ShortString;
        }

        private bool mContinuesUpdatingElapsedTime;

        // 不要だが一応
        private Task? mElapsedTimeUpdatingTask;

        private void iStartUpdatingElapsedTime ()
        {
            mElapsedTimeUpdatingTask = Task.Run (() =>
            {
                while (mContinuesUpdatingElapsedTime)
                {
                    lock (iCounter.Stopwatch.Locker)
                    {
                        if (iCounter.AreTasksStarted && iCounter.IsPausedManually == false && iCounter.Stopwatch.IsRunning)
                        {
                            TimeSpan xElapsedTime = iCounter.Stopwatch.TotalElapsedTime_lock;

                            if (iShared.IsWindowClosed == false)
                            {
                                string xElapsedTimeString = Shared.TimeSpanToString (xElapsedTime);

                                mWindow.Dispatcher.Invoke (() =>
                                {
                                    // Visual Studio や Google Chrome にならう
                                    mWindow.Title = xElapsedTimeString + " - timeLog";
                                    mElapsedTime.Text = xElapsedTimeString;
                                });
                            }
                        }

                        else
                        {
                            if (iShared.IsWindowClosed == false)
                            {
                                mWindow.Dispatcher.Invoke (() =>
                                {
                                    mWindow.Title = "timeLog";
                                    mElapsedTime.Clear ();
                                });
                            }
                        }
                    }

                    // 少々カクつくかもしれないが、このくらいで十分
                    Thread.Sleep (100);
                }
            });
        }

        private void mWindow_Loaded (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.IsWindowClosed = false;

                // 以下、セッション情報のファイルから読まれたばかりの値が書き込まれるのは、軽微なコストなので無視
                // イベントのメソッドですぐにセッション情報を書き込む設計の問題だが、これも 0.1 のときの手抜き

                mNextTasks.Text = iShared.Session.GetStringOrDefault ("NextTasks", string.Empty);

                if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreNextTasksValuable", string.Empty), out bool xResult))
                    mAreNextTasksValuable.IsChecked = xResult;

                iCounter.LoadPreviousInfo ();

                // 起動時に前回の情報が残っていれば、カウント中あるいは中断中（自動、マニュアルの両方）にプログラムが終了したということ
                // カウント中で、各部のデータが戻り、マニュアルで中断されていて、「再開」ボタンにより再スタートできる状態に

                if (iCounter.PreviousStartUtc != null)
                {
                    iCounter.AreTasksStarted = true;

                    mCurrentTasks.Text = iShared.Session.GetStringOrDefault ("CurrentTasks", string.Empty);

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AutoPauses", string.Empty), out bool xResultAlt))
                    {
                        mAutoPauses.IsChecked = xResultAlt;
                        iCounter.Stopwatch.AutoPauses_lock = xResultAlt;
                    }

                    else
                    {
                        // 自動中断はデフォルトでオン

                        mAutoPauses.IsChecked = true;
                        iCounter.Stopwatch.AutoPauses_lock = true;
                    }

                    // 前回のデータが残っていれば、「中断」ボタンによりマニュアルで中断されてからのプログラムの終了とみなされる
                    iCounter.IsPausedManually = true;

                    iCounter.Stopwatch.PreviousEntries.Add (new nStopwatchEntry <nEmptyClass, nEmptyStruct>
                    {
                        StartUtc = iCounter.PreviousStartUtc.Value,
                        ElapsedTime = iCounter.PreviousElapsedTime ?? TimeSpan.Zero // 起動できないと復旧できない
                    });

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreCurrentTasksValuable", string.Empty), out bool xResultAlt1))
                        mAreCurrentTasksValuable.IsChecked = xResultAlt1;

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("IsDisoriented", string.Empty), out bool xResultAlt2))
                        mIsDisoriented.IsChecked = xResultAlt2;
                }

                else
                {
                    // 前回のデータがない場合、デフォルトの値がコントロールの初期状態と異なるものだけ変更

                    mAutoPauses.IsChecked = true;
                    iCounter.Stopwatch.AutoPauses_lock = true; // デフォルトで true だが明示的に
                }

                // 「～の一部に」とか「読み込みにおいてエラーが～」とかも考えたが、
                //     まず起こらないことなので最小限のメッセージに

                if (iPreviousLogs.LogFile.BadChunks.Count > 0)
                    MessageBox.Show (this, "timeLogs.txt に問題があります。");

                // 紆余曲折については taskKiller のログに
                mPreviousTasks.ItemsSource = iPreviousLogs.Logs;

                iUpdateControls ();
                iUpdateStatistics ();

                mNextTasks.Focus ();

#if DEBUG
                // テスト時には IME がオフの方が良い
#else
                InputMethod.Current.ImeState = InputMethodState.On;
#endif

                // 別スレッドを作るので、UI 系の処理が終わってから
                iStartUpdatingElapsedTime ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mNextTasks_TextChanged (object sender, TextChangedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("NextTasks", mNextTasks.Text);
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mAreNextTasksValuable_IsCheckedChanged (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("AreNextTasksValuable", mAreNextTasksValuable.IsChecked!.Value.ToString ());
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iAddLog ()
        {
            lock (iCounter.Stopwatch.Locker)
            {
                DateTime xStartUtc;

                // 前回のデータがあれば、前回の続きなのでその値を使う
                // なくて、nStopwatch 内に古いデータがあるなら、初回の Pause/Stop の値を使う
                // いずれもないなら、新規カウントにおいて一度も Pause/Stop されていない状態なので現行の値を

                if (iCounter.PreviousStartUtc != null)
                    xStartUtc = iCounter.PreviousStartUtc.Value;

                else if (iCounter.Stopwatch.PreviousEntries.Count > 0)
                    xStartUtc = iCounter.Stopwatch.PreviousEntries [0].StartUtc;

                else xStartUtc = iCounter.Stopwatch.CurrentEntryStartUtc!.Value;

                iPreviousLogs.AddLog (new LogInfo (xStartUtc, Shared.ParseTasksString (mCurrentTasks.Text),
                    mAreCurrentTasksValuable.IsChecked!.Value, mIsDisoriented.IsChecked!.Value, iCounter.Stopwatch.TotalElapsedTime_lock));

                iCounter.PreviousStartUtc = null;
                iCounter.PreviousElapsedTime = null;

                iCounter.AreTasksStarted = false;
                iCounter.IsPausedManually = false;
                iCounter.Stopwatch.Reset_lock ();
            }

            mCurrentTasks.Clear ();
            mAutoPauses.IsChecked = true;
            iCounter.Stopwatch.AutoPauses_lock = true;
            mAreCurrentTasksValuable.IsChecked = false;
            mIsDisoriented.IsChecked = false;

            mPreviousTasks.ScrollIntoView (mPreviousTasks.Items [0]);
        }

        private void mStartNextTasks_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iCounter.AreTasksStarted)
                {
                    iAddLog ();
                    iUpdateStatistics ();
                }

                iCounter.AreTasksStarted = true;
                iCounter.Stopwatch.Start_lock ();

                mCurrentTasks.Text = string.Join (Environment.NewLine, Shared.ParseTasksString (mNextTasks.Text));
                mAutoPauses.IsChecked = true;
                iCounter.Stopwatch.AutoPauses_lock = true;
                mAreCurrentTasksValuable.IsChecked = mAreNextTasksValuable.IsChecked;
                mIsDisoriented.IsChecked = false;

                mNextTasks.Clear ();
                mAreNextTasksValuable.IsChecked = false;

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mCurrentTasks_TextChanged (object sender, TextChangedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("CurrentTasks", mCurrentTasks.Text);
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mAutoPauses_IsCheckedChanged (object sender, RoutedEventArgs e)
        {
            try
            {
                iCounter.Stopwatch.AutoPauses_lock = mAutoPauses.IsChecked!.Value;

                iShared.Session.SetString ("AutoPauses", mAutoPauses.IsChecked!.Value.ToString ());
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mPauseOrResumeCounting_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iCounter.IsPausedManually == false)
                {
                    iCounter.IsPausedManually = true;
                    iCounter.Stopwatch.Pause_lock ();
                    mPauseOrResumeCounting.Content = "再開";
                }

                else
                {
                    iCounter.IsPausedManually = false;
                    iCounter.Stopwatch.Resume_lock ();
                    mPauseOrResumeCounting.Content = "中断";
                }

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mAreCurrentTasksValuable_IsCheckedChanged (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("AreCurrentTasksValuable", mAreCurrentTasksValuable.IsChecked!.Value.ToString ());
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mIsDisoriented_IsCheckedChanged (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("IsDisoriented", mIsDisoriented.IsChecked!.Value.ToString ());
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mGlobalHook_KeyTyped (object? sender, KeyboardHookEventArgs e)
        {
            try
            {
                if (iCounter.AreTasksStarted && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock_lock (true);
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mGlobalHook_MouseClicked (object? sender, MouseHookEventArgs e)
        {
            try
            {
                if (iCounter.AreTasksStarted && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock_lock (true);
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mEndCurrentTasks_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                iAddLog ();
                iUpdateStatistics ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mElapsedTime_SizeChanged (object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (mElapsedTime.Visibility == Visibility.Visible)
                {
                    Typeface xTypeface = new Typeface (FontFamily, FontStyle, FontWeight, FontStretch);
                    double xPixelsPerDip = VisualTreeHelper.GetDpi (this).PixelsPerDip;

                    mElapsedTime.FontSize = iShared.GetProperFontSize (mElapsedTime.ActualWidth, mElapsedTime.ActualHeight, "999時間59分", xTypeface, xPixelsPerDip);

                    // 不要だが一応
                    // いずれ必要になるかもしれない
                    iUpdateControls ();
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mPreviousTasks_SelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            try
            {
                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private bool iDeleteLog ()
        {
            if (MessageBox.Show (this, "選択中のログを削除しますか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.None, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                int xSelectedIndex = mPreviousTasks.SelectedIndex;

                iPreviousLogs.DeleteLog ((LogInfo) mPreviousTasks.SelectedItem);

                if (mPreviousTasks.Items.Count > 0)
                {
                    void iSelectItem (int index)
                    {
                        // なくてもよさそうだが一応
                        mPreviousTasks.SelectedIndex = index;

                        // UpdateLayout と ScrollIntoView により、ContainerFromIndex の returns null if the item is not realized を回避
                        // https://stackoverflow.com/questions/6713365/itemcontainergenerator-containerfromitem-returns-null
                        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.itemcontainergenerator.containerfromindex
                        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.virtualizingstackpanel

                        mPreviousTasks.UpdateLayout ();
                        mPreviousTasks.ScrollIntoView (mPreviousTasks.Items [index]);

                        ListBoxItem xItem = (ListBoxItem) mPreviousTasks.ItemContainerGenerator.ContainerFromIndex (index);
                        xItem.IsSelected = true;
                        xItem.Focus ();
                    }

                    if (xSelectedIndex < mPreviousTasks.Items.Count)
                        iSelectItem (xSelectedIndex);

                    else iSelectItem (xSelectedIndex - 1);
                }

                return true;
            }

            else return false;
        }

        private void mPreviousTasks_KeyDown (object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete)
                {
                    e.Handled = true;

                    if (iDeleteLog ())
                        iUpdateStatistics ();
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mDeleteSelectedLog_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iDeleteLog ())
                    iUpdateStatistics ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mClose_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                Close ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mWindow_Closed (object sender, EventArgs e)
        {
            try
            {
                mGlobalHook!.Dispose ();
                iCounter.Stopwatch.Dispose (); // ついでに

                // Just before a window actually closes, Closed is raised とのこと
                // コントロールの情報をここで集めるのは安全である可能性が高い
                // https://docs.microsoft.com/en-us/dotnet/desktop/wpf/windows/

                iCounter.ApplyPreviousInfo ();
                iShared.Session.Save ();

                iShared.IsWindowClosed = true;
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }
    }
}
