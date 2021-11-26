using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace SEdgeToPV
{
    internal class TranslateInfo
    {

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FormatName { get; set; }
        public string ConversionInputDir { get; set; }
        public string ConversionOutputDir { get; set; }
        public HashSet<string> AdditionalFileFormats { get; set; }

        private TranslateInfo()
        {
            AdditionalFileFormats = new HashSet<string>();
        }

        public static TranslateInfo CreateFromInputFile(string InFile)
        {
            TranslateInfo info = new TranslateInfo();

            if (!File.Exists(InFile))
            {
                throw new FileNotFoundException(string.Format("File not found {0}", InFile));
            }

            string InFileContent = File.ReadAllText(InFile);
            if (string.IsNullOrEmpty(InFileContent))
            {
                throw new FormatException(string.Format("Wrong input file " + InFile + " the file is empty"));
            }

            log.DebugFormat("Readed raw row {0} from in file ", InFileContent);

            string[] InFileTokens = InFileContent.Split(' ');
            if (InFileContent.Length < 6)
            {
                throw new FormatException(string.Format("Wrong input file " + InFile + " expected 6 token, found {0}", InFileTokens.Length));
            }

            info.FileName = DecodeName(InFileTokens[0]);
            info.FileType = InFileTokens[1];
            info.FormatName = InFileTokens[2];
            info.ConversionInputDir = InFileTokens[4];
            info.ConversionOutputDir = InFileTokens[5];

            string WVSJobFilePath = string.Format("{0}\\{1}", info.ConversionInputDir, Program.WVSJOBFileName);
            if (!File.Exists(WVSJobFilePath))
            {
                throw new FileNotFoundException(string.Format("WVJ JOB File not found {0}", WVSJobFilePath));
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(WVSJobFilePath);

            XmlNodeList FileNodes = doc.SelectNodes("//publish/output[@typename='ALTFILE']/file");
            foreach (XmlNode FileNode in FileNodes)
            {
                string NodeFileType = FileNode.Attributes["type"].Value;
                info.AdditionalFileFormats.Add(NormalizeFileType(NodeFileType));
            }

            return info;
        }

        private static string NormalizeFileType(string FileType)
        {
            FileType = FileType.ToLower();
            if (string.Equals(FileType, "step", StringComparison.CurrentCultureIgnoreCase))
            {
                FileType = "stp";
            }
            else if (string.Equals(FileType, "iges", StringComparison.CurrentCultureIgnoreCase))
            {
                FileType = "igs";
            }
            else if (string.Equals(FileType, "parasolid", StringComparison.CurrentCultureIgnoreCase))
            {
                FileType = "x_t";
            }

            return FileType;
        }

        private static string DecodeName(string Value)
        {
            string NewValue = (string) Value.Clone();

            Dictionary<string, string> dictionary = new Dictionary<string, string>
            {
                { "@_", " " },
                { "@HQ", "\t" },
                { "@HR", "\n" },
                { "@HU", "\r" },
                { "@KT", "<" },
                { "@KV", ">" }
            };

            foreach (string key in dictionary.Keys)
            {
                NewValue = NewValue.Replace(key, dictionary[key]);
            }

            StringBuilder sb = new StringBuilder();
            NewValue = (NewValue.IndexOf("?~~?") > -1 ? NewValue.Replace("@@", "?~~?") : NewValue.Replace("@@", "|~~|"));

            char[] NewValueCharArray = NewValue.ToCharArray();
            for (var i = 0; i < NewValueCharArray.Length; i++)
            {
                char c = NewValueCharArray[i];
                if (c == '@')
                {
                    char FirstCharAfterAt = NewValueCharArray[i + 1];
                    if (FirstCharAfterAt >= 72 && FirstCharAfterAt <= 87)
                    {
                        int ConvertedFirstCharAfterAt = FirstCharAfterAt - 72;
                        int ConvertedSecondfCharAfterAt = NewValueCharArray[i + 2] - (int) 'H';
                        sb.Append((char) (ConvertedFirstCharAfterAt * 16 + ConvertedSecondfCharAfterAt));
                        i += 2;
                    }
                    else
                    {
                        string HexSubString = NewValue.Substring(i + 1, i + 5);
                        char ParsedCharFromHex = (char) int.Parse(HexSubString, System.Globalization.NumberStyles.HexNumber);
                        sb.Append(ParsedCharFromHex);
                        i += 4;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            NewValue = sb.ToString();
            NewValue = (NewValue.IndexOf("?~~?") > -1 ? NewValue.Replace("?~~?", "@") : NewValue.Replace("|~~|", "@"));
            return NewValue;
        }


        public override string ToString()
        {
            return string.Format("FileName: {0}, FileType: {1}, FormatName: {2}, ConversionInputDir: {3}, ConversionOutputDir: {4}, AdditionalFileFormats [{5}] ", FileName, FileType, FormatName, ConversionInputDir,
                ConversionOutputDir, string.Join(", ", System.Linq.Enumerable.ToArray(AdditionalFileFormats)));
        }

    }
}
