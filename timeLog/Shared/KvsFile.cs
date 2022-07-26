using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeLog
{
    public class KvsFile
    {
        public readonly string FilePath;

        private Dictionary <string, string>? mPairs = null;

        public Dictionary <string, string> Pairs
        {
            get
            {
                if (mPairs == null)
                    Load ();

                return mPairs!;
            }
        }

        public KvsFile (string filePath)
        {
            FilePath = filePath;
        }

        private static string iUnescape (string value)
        {
            StringBuilder xBuilder = new StringBuilder ();

            for (int temp = 0; temp < value.Length; temp ++)
            {
                if (value [temp] == '\\')
                {
                    // なければ落ちる
                    char xNextChar = value [temp + 1];

                    if (xNextChar == '\\')
                    {
                        xBuilder.Append ('\\');
                        temp ++;
                    }

                    else if (xNextChar == 'r')
                    {
                        xBuilder.Append ('\r');
                        temp ++;
                    }

                    else if (xNextChar == 'n')
                    {
                        xBuilder.Append ('\n');
                        temp ++;
                    }

                    else throw new FormatException ();
                }

                else xBuilder.Append (value [temp]);
            }

            return xBuilder.ToString ();
        }

        public void Load ()
        {
            Dictionary <string, string> xPairs = new Dictionary <string, string> (StringComparer.OrdinalIgnoreCase);

            if (File.Exists (FilePath))
            {
                using (StringReader xReader = new StringReader (File.ReadAllText (FilePath, Encoding.UTF8)))
                {
                    string? xLine;

                    while ((xLine = xReader.ReadLine ()) != null)
                    {
                        int xIndex = xLine.IndexOf (':');

                        if (xIndex >= 0)
                            xPairs [xLine.Substring (0, xIndex)] = iUnescape (xLine.Substring (xIndex + 1));
                    }
                }
            }

            mPairs = xPairs;
        }

        private static string iEscape (string value)
        {
            StringBuilder xBuilder = new StringBuilder ();

            foreach (char xChar in value)
            {
                if (xChar == '\\')
                    xBuilder.Append (@"\\");

                else if (xChar == '\r')
                    xBuilder.Append (@"\r");

                else if (xChar == '\n')
                    xBuilder.Append (@"\n");

                else xBuilder.Append (xChar);
            }

            return xBuilder.ToString ();
        }

        public void Save ()
        {
            string? xDirectoryPath = Path.GetDirectoryName (FilePath);

            if (xDirectoryPath != null && Directory.Exists (xDirectoryPath) == false)
                Directory.CreateDirectory (xDirectoryPath);

            File.WriteAllLines (FilePath, Pairs.Select (x => x.Key + ':' + iEscape (x.Value)), Encoding.UTF8);
        }

        public string GetStringOrDefault (string key, string value)
        {
            if (Pairs.ContainsKey (key))
                return Pairs [key];

            else return value;
        }

        public void SetString (string key, string value)
        {
            Pairs [key] = value;
        }
    }
}
