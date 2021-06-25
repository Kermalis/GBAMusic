using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Properties;
using Kermalis.VGMusicStudio.UI;
using System;
using System.Windows.Forms;

namespace Kermalis.VGMusicStudio
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
#if DEBUG
            //Debug.GBAGameCodeScan(@"C:\Users\Kermalis\Documents\Emulation\GBA\Games");
#endif
            try
            {
                GlobalConfig.Init();
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex, Strings.ErrorGlobalConfig);
                return;
            }
            Application.EnableVisualStyles();
            MainForm.Instance.SetLaunchArgs(args);
            Application.Run(MainForm.Instance);
        }
    }
}
