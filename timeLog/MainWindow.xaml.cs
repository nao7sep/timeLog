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

#pragma warning disable IDE0052
        private Task? mHookingTask = null;
#pragma warning restore IDE0052

        private readonly double _NotificationTitleFontSize = 12 * 3;
        private readonly double _NotificationNormalMessageFontSize = 12 * 3;
        private readonly double _NotificationLargeMessageFontSize = 12 * 7;

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

                TextOptions.SetTextFormattingMode (this, iShared.TextFormattingMode);
                TextOptions.SetTextHintingMode (this, iShared.TextHintingMode);
                TextOptions.SetTextRenderingMode (this, iShared.TextRenderingMode);

                string? xFontFamily = ConfigurationManager.AppSettings ["FontFamily"];

                if (string.IsNullOrEmpty (xFontFamily) == false)
                    mWindow.FontFamily = new FontFamily (xFontFamily);

                // We dont update notification font settings here because the notification content (and its corresponding loop) may have been initialized already.
                // Considering cases where the app starts in the paused state, I prefer not to complicate the code by delaying the notification loop.

                if (bool.TryParse (ConfigurationManager.AppSettings ["IsImeEnabled"], out bool xResultAlt2))
                    InputMethod.Current.ImeState = InputMethodState.On;

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
            bool xAreNextTasksOK = string.IsNullOrEmpty (mNextTasks.Text) == false && (mNextTasks.Text.Optimize () ?? "").Length > 0,
                xAreCurrentTasksOK = string.IsNullOrEmpty (mCurrentTasks.Text) == false && (mCurrentTasks.Text.Optimize () ?? "").Length > 0;

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
                mIsFocused.IsEnabled = true;

                if (xAreCurrentTasksOK)
                {
                    mStartWithoutTasks.IsEnabled = true;
                    mEndCurrentTasks.IsEnabled = true;
                }

                else
                {
                    mStartWithoutTasks.IsEnabled = false;
                    mEndCurrentTasks.IsEnabled = false;
                }

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

                mStartWithoutTasks.IsEnabled = true;
                mCurrentTasks.IsEnabled = false;
                mAutoPauses.IsEnabled = false;
                mPauseOrResumeCounting.IsEnabled = false;
                mPauseOrResumeCounting.Content = "中断";
                mAreCurrentTasksValuable.IsEnabled = false;
                mIsFocused.IsEnabled = false;
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

        // 不要だが一応 → 早すぎるフックの回避にハック的に
        private Task? mElapsedTimeUpdatingTask;

        private void iStartUpdatingElapsedTime ()
        {
            mElapsedTimeUpdatingTask = Task.Run (() =>
            {
                // 3分ごとの通知
                // 初回はプログラムの起動から5秒後に
                DateTime xLastNotificationUtc = DateTime.UtcNow.AddSeconds (-175);

                while (mContinuesUpdatingElapsedTime)
                {
                    // ここで lock (iCounter.Stopwatch.Locker) を行うと、
                    //     mWindow_Closed で iCounter.Stopwatch.TotalElapsedTime がデッドロックになり、
                    //     ウィンドウが閉じてからもスレッドが残り、プロセスが終わらない
                    // デバッグモードにより mWindow_Closed で止めて iCounter.Stopwatch.TotalElapsedTime を見ると、
                    //     デッドロック状態なのでタイムアウトになり、その旨がエラーメッセージとして表示される

                    // 警戒するべきは、「if 文の時点ではそうだったのに、次の瞬間にはそうでなくなっていた」のよくあるケース

                    // iCounter.Stopwatch.TotalElapsedTime は、iCounter.Stopwatch が Dispose されてからも落ちることはない
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
                            TimeSpan xElapsedTime = iCounter.Stopwatch.TotalElapsedTime;

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

                        // プログラムの起動時からまわり続けるスレッドがあるので、通知画面もそこから表示

                        DateTime xUtcNow = DateTime.UtcNow;

                        if ((xUtcNow - xLastNotificationUtc).TotalSeconds >= 180)
                        {
                            xLastNotificationUtc = xUtcNow;

                            // 通知画面の表示の前に、これまたついでに3分ごとのセッション情報の保存も行う
                            // 内容が変更されたか見ないため1時間あたり必ず20回の書き込みになるが、SSD へのダメージは微々たるもの
                            // Windows 全体のクラッシュは今も数ヶ月に1回はある
                            // timeLog は24時間走らせるプログラムになりつつあるため、記録が歯抜けになるのをできるだけ防ぐ

                            // 遅延書き込みを実装した
                            // このスレッドによる書き込みとコントロールを操作しての書き込みの衝突を考え、こちらも遅延書き込みに

                            iShared.SavePreviousInfo (immediately: false);

                            if (iShared.IsWindowClosed == false)
                            {
                                bool xIsLargeMessage = false;

                                if (iCounter.AreTasksStarted == false)
                                {
                                    iShared.NotificationContent.Background = iShared.NotStartedNotificationBackgroundColor;
                                    iShared.NotificationContent.Foreground = iShared.NotStartedNotificationForegroundColor;

                                    // 開始されていないのは「計測」なのか「タスク」なのか
                                    // 最初は「計測」を考えたが、「はよ働け！」を言いたいので「タスク」に
                                    iShared.NotificationContent.Message = "はよ働け！";
                                    xIsLargeMessage = true;
                                }

                                else if (iCounter.IsPausedManually)
                                {
                                    iShared.NotificationContent.Background = iShared.PausedNotificationBackgroundColor;
                                    iShared.NotificationContent.Foreground = iShared.PausedNotificationForegroundColor;

                                    // 厳密には「計測」が中断されているが、上と同じ理由で「タスク」に
                                    iShared.NotificationContent.Message = "止まってるで！";
                                    xIsLargeMessage = true;
                                }

                                else
                                {
                                    // こういうことができると、今まで知らなかった
                                    // Invoke は同期処理なので、すぐに結果が得られる

                                    // c# - How do I wait for the result from Dispatcher Invoke? - Stack Overflow
                                    // https://stackoverflow.com/questions/39438441/how-do-i-wait-for-the-result-from-dispatcher-invoke

                                    // Dispatcher.Invoke Method (System.Windows.Threading) | Microsoft Learn
                                    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcher.invoke

                                    string xText = mWindow.Dispatcher.Invoke (() => mCurrentTasks.Text);
                                    var xParagraphs = nString.EnumerateParagraphs (xText);
                                    int xParagraphCount = xParagraphs.Count ();

                                    iShared.NotificationContent.Background = iShared.CountingNotificationBackgroundColor;
                                    iShared.NotificationContent.Foreground = iShared.CountingNotificationForegroundColor;

                                    // 入力があり、段落が一つだけなら、それが済んでいるかまだかを考慮せず、それを表示
                                    // 段落が複数なら、自分の使い方では空行で区切って二つ目以降の段落にまだのことを書くため、まず二つ目の段落のみ表示
                                    // しなければならないことが多いときにまだのことがゴソッと表示されるとうるさい

                                    if (xParagraphCount == 0)
                                        iShared.NotificationContent.Message = "何やってるか書いて！";

                                    else if (xParagraphCount == 1)
                                        iShared.NotificationContent.Message = string.Join (Environment.NewLine, xParagraphs.First ());

                                    else iShared.NotificationContent.Message = string.Join (Environment.NewLine, xParagraphs.ElementAt (1));
                                }

                                // Makes sure the fonts and sizes are up-to-date.
                                // mWindow_Initialized may be called AFTER the notification loop has started.

                                mWindow.Dispatcher.Invoke (() =>
                                {
                                    iShared.NotificationContent.TitleTextSettings.FontFamily = mWindow.FontFamily;
                                    iShared.NotificationContent.MessageTextSettings.FontFamily = mWindow.FontFamily;
                                });

                                iShared.NotificationContent.TitleTextSettings.FontSize = _NotificationTitleFontSize;

                                if (xIsLargeMessage)
                                    iShared.NotificationContent.MessageTextSettings.FontSize = _NotificationLargeMessageFontSize;
                                else iShared.NotificationContent.MessageTextSettings.FontSize = _NotificationNormalMessageFontSize;

                                // By default, the notification stays visible for about 3 seconds (I think).
                                // 3 seconds out of 180 seconds is often not enough and I often work for some time with the counter paused.
                                TimeSpan? xExpirationTime = xIsLargeMessage ? TimeSpan.FromSeconds (10) : null;

                                iShared.NotificationManager.Show (iShared.NotificationContent, expirationTime: xExpirationTime);
                            }
                        }
                    }

                    catch (Exception xException)
                    {
                        // For debugging purposes.
                        MessageBox.Show (xException.ToString ());
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
                        iCounter.Stopwatch.AutoPauses = xResultAlt;
                    }

                    else
                    {
                        // 自動中断はデフォルトでオン

                        mAutoPauses.IsChecked = true;
                        iCounter.Stopwatch.AutoPauses = true; // デフォルトで true だが明示的に
                    }

                    // 前回のデータが残っていれば、「中断」ボタンによりマニュアルで中断されてからのプログラムの終了とみなされる
                    iCounter.IsPausedManually = true;

                    iCounter.Stopwatch.PreviousEntries.Add (new nStopwatchEntry <object>
                    {
                        StartUtc = iCounter.PreviousStartUtc.Value,
                        ElapsedTime = iCounter.PreviousElapsedTime ?? TimeSpan.Zero // 起動できないと復旧できない
                    });

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("AreCurrentTasksValuable", string.Empty), out bool xResultAlt1))
                        mAreCurrentTasksValuable.IsChecked = xResultAlt1;

                    if (bool.TryParse (iShared.Session.GetStringOrDefault ("IsFocused", string.Empty), out bool xResultAlt2))
                        mIsFocused.IsChecked = !xResultAlt2;

                    mResults.Text = iShared.Session.GetStringOrDefault ("Results", string.Empty);
                }

                else
                {
                    // 前回のデータがない場合、デフォルトの値がコントロールの初期状態と異なるものだけ変更

                    mAutoPauses.IsChecked = true;
                    iCounter.Stopwatch.AutoPauses = true;
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
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

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
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iAddLog ()
        {
            // 経過時間を表示するスレッドの iCounter.Stopwatch.TotalElapsedTime と
            //     こちらの iCounter.Stopwatch.Reset のことを考えて lock を検討したが、やめておく
            // 計測終了時に100ミリ秒だけ経過時間が（空でなく）0になるかもしれないが、
            //     絶妙のタイミングを要することで発生確率が極めて低いし、ユーザーへの影響もない

            string? xResultsString = mResults.Text.Optimize ();

            LogInfo xLog = new LogInfo (iCounter.GetStartUtc (), mCurrentTasks.Text.Optimize ()!,
                mAreCurrentTasksValuable.IsChecked!.Value, !mIsFocused.IsChecked!.Value, iCounter.Stopwatch.TotalElapsedTime,
                string.IsNullOrEmpty (xResultsString) == false ? xResultsString : null);

            // 過去ログのところに計測データが入ったあと、プログラムのクラッシュやオンラインストレージ系のアプリの挙動などにより
            //     「プログラムの再起動時に timeLog.Session.txt の内容が古く、それが読み込まれる」ということが少なくとも以前の実装ではあった
            // その状態で「今のタスクを終了」をクリックすると、SortedList における DateTime 型のキーの衝突により例外が発生する

            // そもそもそういう状態にならないのが理想だが、timeLog.Session.txt を意図的に戻すなどにより再現できることなので、対応しないわけにはいかない
            // キーの偶然の衝突は考えにくいので、新しいデータの方が、さらに計測しての経過時間の増加の可能性があることにより、古い方を消してから追加する

            // 実装としては、キーの衝突が SortedList で起こっているので、そこでキーの存在を見て、古いデータもそこから参照として抜く
            // DeleteLog の内部では、ファイルからは、その参照のデータが文字列化されたものと完全に一致する部分が消され、
            //     SortedList からは StartUtc をキーとする削除が行われ、ObservableCollection からは参照による削除が行われる

            if (iPreviousLogs.LogFile.Logs.ContainsKey (xLog.StartUtc))
            {
                LogInfo xOldLog = iPreviousLogs.LogFile.Logs [xLog.StartUtc];
                iPreviousLogs.DeleteLog (xOldLog);
            }

            iPreviousLogs.AddLog (xLog);

            iCounter.PreviousStartUtc = null;
            iCounter.PreviousElapsedTime = null;

            iCounter.AreTasksStarted = false;
            iCounter.IsPausedManually = false;
            iCounter.Stopwatch.Reset ();

            mCurrentTasks.Clear ();
            mAutoPauses.IsChecked = true;
            iCounter.Stopwatch.AutoPauses = true;
            mAreCurrentTasksValuable.IsChecked = false;
            mIsFocused.IsChecked = false;
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
                iCounter.Stopwatch.Start ();

                mCurrentTasks.Text = mNextTasks.Text;
                mAutoPauses.IsChecked = true;
                iCounter.Stopwatch.AutoPauses = true;
                mAreCurrentTasksValuable.IsChecked = mAreNextTasksValuable.IsChecked;
                mIsFocused.IsChecked = false;
                mResults.Clear ();

                mNextTasks.Clear ();
                mAreNextTasksValuable.IsChecked = false;

                iShared.SavePreviousInfo (immediately: false);

                iUpdateControls ();

                mCurrentTasks.Focus ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mStartWithoutTasks_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                if (iCounter.AreTasksStarted)
                {
                    iAddLog ();
                    iUpdateStatistics ();
                }

                iCounter.AreTasksStarted = true;
                iCounter.Stopwatch.Start ();

                // 以下、iAddLog と同じ

                mCurrentTasks.Clear ();
                mAutoPauses.IsChecked = true;
                iCounter.Stopwatch.AutoPauses = true;
                mAreCurrentTasksValuable.IsChecked = false;
                mIsFocused.IsChecked = false;
                mResults.Clear ();

                iShared.SavePreviousInfo (immediately: false);

                iUpdateControls ();

                mCurrentTasks.Focus ();
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
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

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
                iCounter.Stopwatch.AutoPauses = mAutoPauses.IsChecked!.Value;

                iShared.Session.SetString ("AutoPauses", mAutoPauses.IsChecked!.Value.ToString ());
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

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

                iShared.SavePreviousInfo (immediately: false);

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
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

                iUpdateControls ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mIsFocused_IsCheckedChanged (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.Session.SetString ("IsFocused", mIsFocused.IsChecked!.Value.ToString ());
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

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
                // まだ mWindow_Loaded の途中で、IsPausedManually などが設定される前に Knock が呼ばれると、計測を始めずのノックになって Nekote が例外を投げる
                // mWindow_Loaded の最後で mElapsedTimeUpdatingTask にインスタンスが設定されるため、それにより、初期化後のフックしか対応されないように

                if (mElapsedTimeUpdatingTask == null)
                    return;

                // 元データ感のより強い mAutoPauses.IsChecked!.Value を見ると、
                //     「System.InvalidOperationException: このオブジェクトは別のスレッドに所有されているため、呼び出しスレッドはこのオブジェクトにアクセスできません」になる
                // キーボードなどのフックが別スレッドによる処理であることを忘れていた

                if (iCounter.AreTasksStarted && iCounter.Stopwatch.AutoPauses && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock (true);

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
                if (mElapsedTimeUpdatingTask == null)
                    return;

                if (iCounter.AreTasksStarted && iCounter.Stopwatch.AutoPauses && iCounter.IsPausedManually == false)
                    iCounter.Stopwatch.Knock (true);

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

                iShared.SavePreviousInfo (immediately: false);

                iUpdateControls ();

                mNextTasks.Focus ();
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
                // iShared.Session.Save ();
                iShared.SavePreviousInfo (immediately: false);

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
            if (MessageBox.Show (this, "選択中のログを消しますか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.None, MessageBoxResult.No) == MessageBoxResult.Yes)
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

                // ここではセッション情報の保存は不要
                // 直後に App クラスの方で行われる
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }
    }
}
