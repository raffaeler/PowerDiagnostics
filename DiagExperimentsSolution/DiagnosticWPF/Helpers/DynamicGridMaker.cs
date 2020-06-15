using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using DiagnosticWPF.Models;

namespace DiagnosticWPF.Helpers
{
    public static class DynamicGridMaker
    {
        public static DataGridTemplateColumn CreateGridColumn(UIGridColumn uIGridColumn)
        {
            var binding = new Binding(uIGridColumn.Path);
            if (!string.IsNullOrEmpty(uIGridColumn.StringFormat)) binding.StringFormat = "{" + uIGridColumn.StringFormat + "}";

            var tc = new DataGridTemplateColumn()
            {
                Header = uIGridColumn.Header,
                Width = uIGridColumn.Length,
            };

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.MarginProperty, new Thickness(7, 5, 7, 5));
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, binding);
            stackPanelFactory.AppendChild(textFactory);
            var dt = new DataTemplate();
            dt.VisualTree = stackPanelFactory;

            tc.CellTemplate = dt;
            tc.CellStyle = CreateCellStyle(uIGridColumn.Tooltip, uIGridColumn.TooltipPath, uIGridColumn.IsRightAligned);
            return tc;
        }

        private static Style CreateCellStyle(string tooltip, string tooltipPath, bool isRightAligned)
        {
            var style = new Style(typeof(DataGridCell));
            if (!string.IsNullOrEmpty(tooltip))
            {
                style.Setters.Add(new Setter(DataGridCell.ToolTipProperty, tooltip));
            }
            else
            {
                var binding = new Binding(tooltipPath);
                style.Setters.Add(new Setter(DataGridCell.ToolTipProperty, binding));
            }

            style.Setters.Add(new Setter(DataGridCell.FontFamilyProperty, new FontFamily("Courier New")));
            style.Setters.Add(new Setter(DataGridCell.FontSizeProperty, 16.0));
            if(isRightAligned)
                style.Setters.Add(new Setter(DataGridCell.HorizontalAlignmentProperty, HorizontalAlignment.Right));

            return style;
        }

    }
}
