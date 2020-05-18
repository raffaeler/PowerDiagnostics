using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StaticMemoryLeaks
{
    public class LanguageEnglish : IEnglishLanguage
    {
        public static readonly string Filename = "EnglishWords.txt";

        private string Load(string filename) => File.ReadAllText(filename);

        public LanguageEnglish()
        {
            var content = Load(Filename);
            WordsList = TextUtilities.ExtractWords(content);
        }

        public IList<string> WordsList { get; private set; }

        public string LanguageName => "English";
    }


    public interface IEnglishLanguage { }
}
