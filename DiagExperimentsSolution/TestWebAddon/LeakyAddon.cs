using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TestWebAddonContract;

namespace TestWebAddon
{
    public class LeakyAddon : ILeakyAddon
    {
        private static Dictionary<string, string> _leakyMap = new();

        public void LeakSomeMemory(int num)
        {
            for (int i = 0; i < num; i++)
            {
                _leakyMap[i.ToString()] = "This is a leaked string " + i.ToString();
            }
        }
    }
}
