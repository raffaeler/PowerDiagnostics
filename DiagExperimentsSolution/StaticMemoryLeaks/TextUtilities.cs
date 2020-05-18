using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaticMemoryLeaks
{
    public static class TextUtilities
    {
        private static char[] _separators = new char[]
        {
            '\r', '\n', ',', '.', ',', '^', '%', '!', '~', '+',
            '-', '_', '­', '\'', '\"', '*', '/', '>', '<', '=',
            '&', '|', ')', '(', '0', '1', '2', '3', '4', '5',
            '6', '7', '8', '9', ' ', '[', ']', '?', ',', ';',
            ':',
        };

        // Using the local function 'commit' to encapsulate all the code of this method
        public static IList<string> ExtractWords(string text)
        {
            var list = new List<string>();
            var sb = new StringBuilder();

            foreach (var ch in text)
            {
                if (_separators.Contains(ch))
                {
                    commit();
                    continue;
                }

                sb.Append(ch);
            }

            commit();
            return list;

            void commit()
            {
                // 'sb' and 'list' are implicitly captured
                if (sb.Length == 0) return;
                var word = sb.ToString().ToLower();
                list.Add(word);
                sb.Clear();
            }
        }

        public static IList<string> LoadVocabulary(object languageObject)
        {
            switch (languageObject)
            {
                case LanguageEnglish english:
                    return english.WordsList;

                case LanguageLatin latin:
                    return latin.Words;

                //case object obj when typeof(IEnglishLanguage).IsAssignableFrom(obj.GetType()):
                //    return ...;

                default:
                    throw new Exception($"Unknow language: {languageObject}");
            }
        }

        public static IList<string> LoadVocabulary2(object languageObject)
        {
            // C$ 8.0
            return languageObject switch
            {
                LanguageEnglish english => english.WordsList,
                LanguageLatin latin => latin.Words,
                _ => throw new Exception($"Unknow language: {languageObject}"),
            };
        }


    }
}
