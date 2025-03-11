using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Notification.Wpf.Constants;

namespace timeLog
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: Application
    {
        private void mApp_Startup (object sender, StartupEventArgs e)
        {
            NotificationConstants.MaxWidth = double.MaxValue;
        }

        private bool mIsPreviousInfoSaved;

        private void iSavePreviousInfo ()
        {
            if (mIsPreviousInfoSaved == false)
            {
                iShared.SavePreviousInfo (immediately: true);
                iShared.EndSavingTask ();

                mIsPreviousInfoSaved = true;
            }
        }

        private void mApp_SessionEnding (object sender, SessionEndingCancelEventArgs e)
        {
            iSavePreviousInfo ();
        }

        private void mApp_Exit (object sender, ExitEventArgs e)
        {
            iSavePreviousInfo ();
        }
    }
}
