using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiagnosticWPF.Models
{
    public class UIGrid
    {
        public UIGrid()
        {
            Columns = new List<UIGridColumn>();
        }

        public static UIGrid Create<T>(string detailsProperty, params UIGridColumn[] columns)
        {
            var instance = new UIGrid();
            foreach (var column in columns)
                instance.Columns.Add(column);

            instance.MasterType = typeof(T);
            if (detailsProperty != null)
            {
                instance.DetailsProperty = instance.MasterType.GetProperty(detailsProperty);
                instance.DetailsType = instance.GetFirstGenericType(instance.DetailsProperty);
            }

            return instance;
        }

        public Type MasterType { get; set; }

        public IList<UIGridColumn> Columns { get; private set; }

        public PropertyInfo DetailsProperty { get; set; }

        public Type DetailsType { get; set; }

        private Type GetFirstGenericType(PropertyInfo property)
        {
            if (!property.PropertyType.IsGenericType) throw new Exception("Invalid details property");
            var firstArg = property.PropertyType.GetGenericArguments().FirstOrDefault();
            if (firstArg == null) new Exception("Invalid details property");
            return firstArg;
        }
    }
}
