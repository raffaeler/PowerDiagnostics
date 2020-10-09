using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StressTestWebApp
{
    public class MenuItem
    {
        public MenuItem(char menuKey, HttpMethod verb,
            string relativeAddress,
            int concurrency,
            bool useCustomHeader = false)
        {
            this.MenuKey = menuKey;
            this.Verb = verb;
            this.RelativeAddress = relativeAddress;
            this.Concurrency = concurrency;
            this.UseCustomHeader = useCustomHeader;
        }

        public char MenuKey { get; set; }
        public HttpMethod Verb { get; set; }
        public string RelativeAddress { get; set; }
        public int Concurrency { get; set; }
        public bool UseCustomHeader { get; set; }

        public override string ToString()
        {
            var header = UseCustomHeader ? "CustomHeader" : "";
            return $"{MenuKey}. {Verb} {RelativeAddress} ({Concurrency}) - {header}";
        }

        public string ToStringTabular(params int[] columns)
        {
            if (columns.Length < 5) throw new ArgumentException(nameof(columns));
            var header = UseCustomHeader ? "CustomHeader" : "";

            return string.Join(" ",
                Pad(MenuKey, columns[0]),
                Pad(Verb, columns[1]),
                Pad(RelativeAddress, columns[2]),
                Pad(Concurrency, columns[3]),
                Pad(header, columns[4])
                );
        
            static string Pad<T>(T data, int pad) => data.ToString().PadRight(pad);
        }

    }
}
