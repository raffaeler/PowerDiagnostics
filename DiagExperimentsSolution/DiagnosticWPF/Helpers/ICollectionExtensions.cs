using System;
using System.Collections.Generic;
using System.Text;

namespace DiagnosticWPF.Helpers
{
    public static class ICollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> items)
        {
            foreach (var item in list)
            {
                list.Add(item);
            }
        }
    }
}
