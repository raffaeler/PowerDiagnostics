using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StaticMemoryLeaks
{
    public class TextProcessor
    {
        private static List<string> words = new List<string>();

        private string _content;
        public TextProcessor(string filename)
        {
            _content = File.ReadAllText(filename);
        }

        public async Task Process(CancellationToken cancellationToken, Action<int> onNext)
        {
            words.Clear();
            int count = 0;

            while(true)
            {
                foreach(var word in TextUtilities.ExtractWords(_content))
                {
                    words.Add(word);
                    //await Task.Delay(20);
                    Thread.Sleep(20);
                    onNext(++count);
                    if (cancellationToken.IsCancellationRequested) return;
                }
            }
        }

    }
}
