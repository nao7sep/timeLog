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

            if (iCounter.IsRunning)
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
            Shared.SaveStatistics (xStatistics.Item2);
            mStatistics.Text = xStatistics.Item1;
        }

        private void iStartUpdatingElapsedTime ()
        {
            Task.Run (() =>
            {
                // 前回 mWindow.Title などを更新したときの経過秒数
                // 同じ値を何度も設定することを回避
                double xPreviousElapsedSeconds = -1;

                while (true)
                {
                    // 自動中断や「中断」ボタンによる中断でもループを抜けようとしたが、
                    //     100ミリ秒に一度のごく少量の計算なので Task を回しておくのがシンプル

                    if (iCounter.IsRunning == false)
                        break;

                    TimeSpan xElapsedTime = iCounter.Stopwatch.TotalElapsedTime;

                    // (double) _ticks / TicksPerSecond という割り算なので結果をキャッシュ
                    // https://source.dot.net/#System.Private.CoreLib/TimeSpan.cs
                    double xElapsedSeconds = xElapsedTime.TotalSeconds;

                    if (xElapsedSeconds > xPreviousElapsedSeconds)
                    {
                        xPreviousElapsedSeconds = xElapsedSeconds;

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

                    // 少々カクつくかもしれないが、このくらいで十分

                    if (iShared.IsWindowClosed == false)
                        Thread.Sleep (100);
                }

                if (iShared.IsWindowClosed == false)
                {
                    mWindow.Dispatcher.Invoke (() =>
                    {
                        mWindow.Title = "timeLog";
                        mElapsedTime.Clear ();
                    });
                }
            });
        }

        private void mWindow_Loaded (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.IsWindowClosed = false;

                mNextTasks.Text = iShared.Session.GetStringOrDefault ("NextTasks", string.Empty);

                if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreNextTasksValuable", string.Empty), out bool xResult))
                    mAreNextTasksValuable.IsChecked = xResult;

                // 自動中断機能はデフォルトでオン
                // セッション情報でオフの場合のみオフに

                // iCounter.AutoPauses = true;
                mAutoPauses.IsChecked = true;

                // 「中断」ボタンにより中断されていないのが初期状態
                iCounter.IsPausedManually = false;

                iCounter.LoadPreviousInfo ();

                // 起動時に前回の情報が残っていれば、カウント中あるいは中断中（自動、マニュアルの両方）にプログラムが終了したということ
                // カウント中で、各部のデータが戻り、マニュアルで中断されていて、「再開」ボタンから再スタートできる状態に

                if (iCounter.PreviousStartUtc != null)
                {
                    iCounter.IsRunning = true;

                    mCurrentTasks.Text = iShared.Session.GetStringOrDefault ("CurrentTasks", string.Empty);

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AutoPauses", string.Empty), out bool xResultAlt))
                    {
                        // iCounter.AutoPauses = xResultAlt;
                        mAutoPauses.IsChecked = xResultAlt;
                    }

                    iCounter.IsPausedManually = true;

                    iCounter.Stopwatch.PreviousEntries.Add (new nStopwatchEntry <nEmptyClass, nEmptyStruct>
                    {
                        StartUtc = iCounter.PreviousStartUtc.Value,
                        EndUtc = iCounter.PreviousStartUtc.Value + iCounter.PreviousElapsedTime
                    });

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreCurrentTasksValuable", string.Empty), out bool xResultAlt1))
                        mAreCurrentTasksValuable.IsChecked = xResultAlt1;

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("IsDisoriented", string.Empty), out bool xResultAlt2))
                        mIsDisoriented.IsChecked = xResultAlt2;
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

                InputMethod.Current.ImeState = InputMethodState.On;

                // 別スレッドを作るので、UI 系の処理が終わってから

                if (iCounter.IsRunning)
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
            iPreviousLogs.AddLog (new LogInfo (iCounter.PreviousStartUtc ?? iCounter.Stopwatch.CurrentEntryStartUtc!.Value,
                Shared.ParseTasksString (mCurrentTasks.Text), mAreCurrentTasksValuable.IsChecked!.Value, mIsDisoriented.IsChecked!.Value,
                iCounter.Stopwatch.TotalElapsedTime));

            iCounter.PreviousStartUtc = null;
            // iCounter.PreviousElapsedTime = TimeSpan.Zero;

            iCounter.IsRunning = false;
            // iCounter.AutoPauses = true;
            iCounter.IsPausedManually = false;
            iCounter.Stopwatch.Reset ();

            mCurrentTasks.Clear ();
            mAutoPauses.IsChecked = true;
            mAreCurrentTasksValuable.IsChecked = false;
            mIsDisoriented.IsChecked = false;

            mPreviousTasks.ScrollIntoView (mPreviousTasks.Items [0]);
        }

        private void mStartNextTasks_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iCounter.IsRunning)
                {
                    iAddLog ();
                    iUpdateStatistics ();
                }

                iCounter.IsRunning = true;
                iCounter.Stopwatch.Start ();

                mCurrentTasks.Text = string.Join (Environment.NewLine, Shared.ParseTasksString (mNextTasks.Text));
                mAutoPauses.IsChecked = true;
                mAreCurrentTasksValuable.IsChecked = mAreNextTasksValuable.IsChecked;
                mIsDisoriented.IsChecked = false;

                mNextTasks.Clear ();
                mAreNextTasksValuable.IsChecked = false;

                iUpdateControls ();

                iStartUpdatingElapsedTime ();
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
                iCounter.AutoPauses = mAutoPauses.IsChecked!.Value;

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
                    iCounter.Stopwatch.Pause ();
                    mPauseOrResumeCounting.Content = "再開";
                }

                else
                {
                    iCounter.IsPausedManually = false;
                    iCounter.Stopwatch.Resume ();
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
                iDeleteLog ();
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
                // null のチェックと設定はなくてよいが、作法として
                // IsRunning や IsDisposed を見るまでは不要

                if (mGlobalHook != null)
                {
                    mGlobalHook.Dispose ();
                    mGlobalHook = null;
                }

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
