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

        // CurrentTasksStartUtc は iShared に入っているが、
        //     ウィンドウと関連するメンバー変数はここに

        private Counter? mCounter = null;

        private TaskPoolGlobalHook? mGlobalHook = null;

        // mGlobalHook の Dispose により終了となる
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

            if (iShared.CurrentTasksStartUtc != null)
            {
                if (xAreNextTasksOK && xAreCurrentTasksOK)
                    mStartNextTasks.IsEnabled = true;

                else mStartNextTasks.IsEnabled = false;

                mCurrentTasks.IsEnabled = true;
                mAreCurrentTasksValuable.IsEnabled = true;
                mIsDisoriented.IsEnabled = true;
                mPauseOrResumeCounting.IsEnabled = true;

                if (mCounter!.IsRunning)
                    mPauseOrResumeCounting.Content = "中断";

                else mPauseOrResumeCounting.Content = "再開";

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
                mAreCurrentTasksValuable.IsEnabled = false;
                mIsDisoriented.IsEnabled = false;
                mPauseOrResumeCounting.IsEnabled = false;
                mPauseOrResumeCounting.Content = "中断";
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
            Task.Factory.StartNew (() =>
            {
                // 前回 mWindow.Title などを更新したときの経過秒数
                double xPreviousElapsedSeconds = -1;

                while (true)
                {
                    if (iShared.CurrentTasksStartUtc == null)
                        break;

                    TimeSpan xElapsedTime = mCounter!.ElapsedTime;

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

                if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreNextTasksValuable", string.Empty), out bool xResultAlt1))
                    mAreNextTasksValuable.IsChecked = xResultAlt1;

                if (DateTime.TryParseExact (iShared.Session.GetStringOrDefault ("CurrentTasksStartUtc", string.Empty), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime xResult))
                {
                    iShared.CurrentTasksStartUtc = xResult;

                    mCurrentTasks.Text = iShared.Session.GetStringOrDefault ("CurrentTasks", string.Empty);

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreCurrentTasksValuable", string.Empty), out bool xResultAlt2))
                        mAreCurrentTasksValuable.IsChecked = xResultAlt2;

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("IsDisoriented", string.Empty), out bool xResultAlt))
                        mIsDisoriented.IsChecked = xResultAlt;

                    // セッションデータに破損がないなら必ず読めるべきだが、そういう実装にすると起動しなくなる可能性がある

                    bool? xIsRunning = null;
                    TimeSpan? xElapsedTime = null;

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("IsCounterRunning", string.Empty), out bool xResultAlt3))
                        xIsRunning = xResultAlt3;

                    if (TimeSpan.TryParseExact (iShared.Session.GetStringOrDefault ("CounterElapsedTime", string.Empty), "c", CultureInfo.InvariantCulture, out TimeSpan xResultAlt4))
                        xElapsedTime = xResultAlt4;

                    mCounter = new Counter (xIsRunning ?? true, xElapsedTime ?? TimeSpan.Zero);
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

                // 経過時間の表示が必要かスマートに判定できるので手抜き
                // 別スレッドを作るので、UI 系の処理が終わってから

                if (mEndCurrentTasks.IsEnabled)
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
                iUpdateControls ();

                iShared.Session.SetString ("NextTasks", mNextTasks.Text);
                iShared.Session.Save ();
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
                iUpdateControls ();

                iShared.Session.SetString ("AreNextTasksValuable", mAreNextTasksValuable.IsChecked!.Value.ToString ());
                iShared.Session.Save ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iAddLog ()
        {
            iPreviousLogs.AddLog (new LogInfo (iShared.CurrentTasksStartUtc!.Value, Shared.ParseTasksString (mCurrentTasks.Text), mAreCurrentTasksValuable.IsChecked!.Value, mIsDisoriented.IsChecked!.Value, mCounter!.ElapsedTime));
            iShared.CurrentTasksStartUtc = null;
            mCounter = null;
            mCurrentTasks.Clear ();
            mAreCurrentTasksValuable.IsChecked = false;
            mIsDisoriented.IsChecked = false;

            mPreviousTasks.ScrollIntoView (mPreviousTasks.Items [0]);
        }

        private void mStartNextTasks_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iShared.CurrentTasksStartUtc != null)
                {
                    iAddLog ();
                    iUpdateStatistics ();
                }

                iShared.CurrentTasksStartUtc = DateTime.UtcNow;
                mCounter = new Counter ();
                mCounter.Start ();
                mCurrentTasks.Text = string.Join (Environment.NewLine, Shared.ParseTasksString (mNextTasks.Text));
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
                iUpdateControls ();

                iShared.Session.SetString ("CurrentTasks", mCurrentTasks.Text);
                iShared.Session.Save ();
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
                iUpdateControls ();

                iShared.Session.SetString ("AreCurrentTasksValuable", mAreCurrentTasksValuable.IsChecked!.Value.ToString ());
                iShared.Session.Save ();
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
                iUpdateControls ();

                iShared.Session.SetString ("IsDisoriented", mIsDisoriented.IsChecked!.Value.ToString ());
                iShared.Session.Save ();
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

                iShared.CurrentTasksStartUtc = null;
                iShared.IsWindowClosed = true;
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }
    }
}
