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

                // 最初、KeyTyped と MouseClicked で実装したが、以下のように変更
                // 詳細を taskKiller のログに

                mGlobalHook = new TaskPoolGlobalHook ();
                mGlobalHook.KeyPressed += mGlobalHook_KeyPressed;
                mGlobalHook.MousePressed += mGlobalHook_MousePressed;
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

                mResultsLabel.Visibility = Visibility.Visible;
                mResults.Visibility = Visibility.Visible;

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

                mResultsLabel.Visibility = Visibility.Collapsed;
                mResults.Visibility = Visibility.Collapsed;

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
                    // ここで lock (iCounter.Stopwatch.Locker) を行うと、
                    //     mWindow_Closed で iCounter.Stopwatch.TotalElapsedTime_lock がデッドロックになり、
                    //     ウィンドウが閉じてからもスレッドが残り、プロセスが終わらない
                    // デバッグモードにより mWindow_Closed で止めて iCounter.Stopwatch.TotalElapsedTime_lock を見ると、
                    //     デッドロック状態なのでタイムアウトになり、その旨がエラーメッセージとして表示される

                    // 警戒するべきは、「if 文の時点ではそうだったのに、次の瞬間にはそうでなくなっていた」のよくあるケース

                    // iCounter.Stopwatch.TotalElapsedTime_lock は、iCounter.Stopwatch が Dispose されてからも落ちることはない
                    // しかし、その仕様は今後変更される可能性があるため、この while ループが終わってからの Dispose になるように mWindow_Closed で待つ

                    // ウィンドウが完全に破棄されてからの mWindow.Dispatcher.Invoke は、落ちる可能性がある
                    // UI スレッドがなくなっていれば Invoke は単に何もしないと思うが、100％でないため try/catch に入れる

                    try
                    {
                        // 同じ値を何度も設定しないように、さらに iCounter.IsPausedManually や iCounter.Stopwatch.IsRunning を見たり、
                        //     前回と同じ文字列なら Invoke を呼ばなかったりの選択肢もあるが、
                        //     どのようなステートであっても表示が乱れないように条件分岐を考えると意外とややこしい
                        // 計測中なら経過時間を表示し、そうでないなら空にするという単純な実装でも、100ミリ秒に1回なのでコストは軽微

                        if (iCounter.AreTasksStarted)
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

                    catch
                    {
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
                        iCounter.Stopwatch.AutoPauses_lock = true; // デフォルトで true だが明示的に
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

                    mResults.Text = iShared.Session.GetStringOrDefault ("Results", string.Empty);
                }

                else
                {
                    // 前回のデータがない場合、デフォルトの値がコントロールの初期状態と異なるものだけ変更

                    mAutoPauses.IsChecked = true;
                    iCounter.Stopwatch.AutoPauses_lock = true;
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

                mContinuesUpdatingElapsedTime = true;
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
            // 経過時間を表示するスレッドの iCounter.Stopwatch.TotalElapsedTime_lock と
            //     こちらの iCounter.Stopwatch.Reset_lock のことを考えて lock を検討したが、やめておく
            // 計測終了時に100ミリ秒だけ経過時間が（空でなく）0になるかもしれないが、
            //     絶妙のタイミングを要することで発生確率が極めて低いし、ユーザーへの影響もない

            List <string> xResults = Shared.ParseTasksString (mResults.Text);

            iPreviousLogs.AddLog (new LogInfo (iCounter.GetStartUtc (), Shared.ParseTasksString (mCurrentTasks.Text),
                mAreCurrentTasksValuable.IsChecked!.Value, mIsDisoriented.IsChecked!.Value, iCounter.Stopwatch.TotalElapsedTime_lock,
                xResults.Count > 0 ? xResults : null));

            iCounter.PreviousStartUtc = null;
            iCounter.PreviousElapsedTime = null;

            iCounter.AreTasksStarted = false;
            iCounter.IsPausedManually = false;
            iCounter.Stopwatch.Reset_lock ();

            mCurrentTasks.Clear ();
            mAutoPauses.IsChecked = true;
            iCounter.Stopwatch.AutoPauses_lock = true;
            mAreCurrentTasksValuable.IsChecked = false;
            mIsDisoriented.IsChecked = false;
            mResults.Clear ();

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
                mResults.Clear ();

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

        private void mGlobalHook_KeyPressed (object? sender, KeyboardHookEventArgs e)
        {
            try
            {
                // 元データ感のより強い mAutoPauses.IsChecked!.Value を見ると、
                //     「System.InvalidOperationException: このオブジェクトは別のスレッドに所有されているため、呼び出しスレッドはこのオブジェクトにアクセスできません」になる
                // キーボードなどのフックが別スレッドによる処理であることを忘れていた

                if (iCounter.AreTasksStarted && iCounter.Stopwatch.AutoPauses_lock && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock_lock (true);

                // iUpdateControls ();
            }

            catch (Exception xException)
            {
                // this を渡して try 側で例外を投げてみると、プログラムが落ち、
                //     （おそらく Windows の UI を更新するプロセスが影響を受けるほどのリソース消費がどこかで起こって）マウスカーソルがカクつく
                // MessageBox.Show が Windows の API への処理の丸投げだからだろう
                iShared.HandleException (null, xException);
            }
        }

        private void mGlobalHook_MousePressed (object? sender, MouseHookEventArgs e)
        {
            try
            {
                if (iCounter.AreTasksStarted && iCounter.Stopwatch.AutoPauses_lock && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock_lock (true);

                // iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (null, xException);
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

        private void mResults_TextChanged (object sender, TextChangedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("Results", mResults.Text);
                iShared.Session.Save ();

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iAdjustElapsedTimeFontSize ()
        {
            Typeface xTypeface = new Typeface (FontFamily, FontStyle, FontWeight, FontStretch);
            double xPixelsPerDip = VisualTreeHelper.GetDpi (this).PixelsPerDip;

            // 1時間を過ぎても「秒」をなくさないように変更したことで、「999時間59分59秒」でのフォントサイズの調整が非現実的になった
            // そのため、「分」や「秒」に（必要に応じて）ゼロ詰めし、経過時間の文字列の長さに基づく調整を SizeChanged だけでなく TextChanged でも行うように
            // ゼロ詰めしたのは、「1分59秒」のあと「2分0秒」になると同時にフォントが大きくなり、「2分10秒」でまた小さくなるなどがせわしなかったため

            string xText = "0000000000時間00分00秒";
            xText = xText.Substring (xText.Length - mElapsedTime.Text.Length);

            // 左右にぴったり合わさる長さにすると TextBox の左側パディング（？）で右側が少しはみ出るし、そもそも見た目が悪い
            // いくつかの値を試したうち、0.8 では経過時間の文字列が長いときに小さくなりすぎたので 0.9 で様子見

            mElapsedTime.FontSize = iShared.GetProperFontSize (mElapsedTime.ActualWidth * 0.9, mElapsedTime.ActualHeight, xText, xTypeface, xPixelsPerDip);
        }

        private void mElapsedTime_SizeChanged (object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (mElapsedTime.Visibility == Visibility.Visible)
                {
                    if (mElapsedTime.Text.Length > 0)
                        iAdjustElapsedTimeFontSize ();

                    // 不要だが一応
                    iUpdateControls ();
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private double mElapsedTimePreviousTextLength;

        private void mElapsedTime_TextChanged (object sender, TextChangedEventArgs e)
        {
            try
            {
                if (mElapsedTime.Visibility == Visibility.Visible)
                {
                    if (mElapsedTime.Text.Length > 0 && mElapsedTime.Text.Length != mElapsedTimePreviousTextLength)
                    {
                        mElapsedTimePreviousTextLength = mElapsedTime.Text.Length;

                        iAdjustElapsedTimeFontSize ();
                    }

                    // 不要だが一応
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

                    iUpdateControls ();
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

                iUpdateControls ();
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

        private void mWindow_Closing (object sender, CancelEventArgs e)
        {
            try
            {
                // 自動中断中かどうかは考慮されない
                // 閉じる操作が検出されて直前に再開される可能性が高いということもある
                // プログラム再起動時に「再開」が可能な状態になるため、中断中の終了だと確認されない

                if (iCounter.AreTasksStarted && iCounter.IsPausedManually == false)
                {
                    if (MessageBox.Show (this, "計測中ですが、終了しますか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.None, MessageBoxResult.No) == MessageBoxResult.No)
                        e.Cancel = true;
                }
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

                // Just before a window actually closes, Closed is raised とのこと
                // コントロールの情報をここで集めるのは安全である可能性が高い
                // https://docs.microsoft.com/en-us/dotnet/desktop/wpf/windows/

                // アプリでなく（Windows の）セッションの終了時、Closing が MessageBox で引っ掛かると Closed が起こらないようなので、
                //     セッション情報の保存の処理を App.iSavePreviousInfo に移した

                // ウィンドウが完全に破棄されてからの別スレッドによる mWindow.Dispatcher.Invoke の回避のため
                // タイムアウトを指定して mWindow_Closed を抜けたところで別スレッド内で万が一にもデッドロックが発生していればプロセスは終わらない
                // timeLog 側で lock をなくし、デッドロックの可能性がゼロになったはずなので、しばらく様子見

                mContinuesUpdatingElapsedTime = false;
                mElapsedTimeUpdatingTask!.Wait (300);

                // こちらでは待つ必要がない
                // バックグラウンドスレッドだし、保存の必要なステート情報もない

                // nStopwatch は Dispose されてからもデータの読み出しが可能
                // セッション情報の保存の処理を App.iSavePreviousInfo に移したが、
                //     Dispose をなしにする必要はない

                iCounter.Stopwatch.Dispose ();

                iShared.IsWindowClosed = true;
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }
    }
}
