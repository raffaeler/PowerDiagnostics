using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TestWebAddonContract;

namespace TestWebAddon
{
    /// <summary>
    /// An addon that is loaded in a different Load Context
    /// It is used to show how to list all the custom contexts in an app
    /// </summary>
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

        public byte[] AllocateSomeMemory(int size)
        {
            var blob = new byte[size];
            for (int i = 0; i < blob.Length; i++)
                blob[i] = (byte)i;

            return blob;
        }
    }
}
