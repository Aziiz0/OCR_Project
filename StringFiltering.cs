using System.Text.RegularExpressions;

namespace StringFiltering
{
    public class StringFilter
    {
        public string ReduceWhitespace(string input)
        {
            string noNewLines = input.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            string normalized = Regex.Replace(noNewLines, @"\s+", " ");
            return normalized.Trim();
        }

        public string RemoveWords(string input, List<string> wordsToRemove)
        {
            foreach (string word in wordsToRemove)
            {
                input = input.Replace(word, "");
            }
            return input;
        }

        public string RemoveRepeatingNewLines(string input)
        {
            string noRepeatingNewLines = Regex.Replace(input, @"(\r\n){2,}", "\r\n").Replace("\n{2,}", "\n").Replace("\r{2,}", "\r");
            return noRepeatingNewLines;
        }
    }
}
