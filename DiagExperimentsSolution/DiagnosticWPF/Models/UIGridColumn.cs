using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DiagnosticWPF.Models
{
    public class UIGridColumn
    {
        public UIGridColumn(string header, string path, string stringFormat,
            string tooltip, string tooltipPath, DataGridLength length, bool isRightAligned = false)
        {
            this.Header = header;
            this.Path = path;
            this.StringFormat = stringFormat;
            this.Tooltip = tooltip;
            this.TooltipPath = tooltipPath;
            this.Length = length;
            this.IsRightAligned = isRightAligned;
        }

        public string Header { get; set; }
        public string Path { get; set; }
        public string StringFormat { get; set; }
        public string Tooltip { get; set; }
        public string TooltipPath { get; set; }
        public DataGridLength Length { get; set; }
        public bool IsRightAligned { get; set; }
    }
}
