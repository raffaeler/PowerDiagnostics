using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StaticMemoryLeaks
{
    public class LanguageLatin
    {
        public static readonly string Filename = "LatinWordsList.txt";

        private string Load(string filename) => File.ReadAllText(filename);

        public LanguageLatin()
        {
            var content = Load(Filename);
            (_, content) = RemoveLines(content);
            Words = TextUtilities.ExtractWords(content);
        }

        public IList<string> Words { get; private set; }

        public string Language => "Latin";


        public static (int processed, string result) RemoveLines(string text)
        {
            // will dispose at the end!
            using var reader = new StringReader(text);

            var sb = new StringBuilder();
            int processed = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                int index = line.IndexOf(':');
                string word;
                if (index != -1)
                {
                    word = line.Substring(0, index);
                }
                else
                {
                    word = line;
                }

                sb.AppendLine(word);

                processed++;
            }

            return (processed, sb.ToString());
        }

    }

}
