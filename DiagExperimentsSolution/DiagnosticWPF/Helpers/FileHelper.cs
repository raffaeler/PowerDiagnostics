using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DiagnosticWPF.Helpers
{
    public static class FileHelper
    {
        public static string OpenDialog(this Window window, string title)
        {
            OpenFileDialog dlg = new OpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dmp",
                Filter = "Dump files (*.dmp)|*.dmp|All files (*.*)|*.*",
                FilterIndex = 0,
                Multiselect = false,
                Title = title,
            };

            var result = dlg.ShowDialog(window);
            if (result.HasValue && result.Value)
            {
                return dlg.FileName;
            }

            return null;
        }
    }
}
