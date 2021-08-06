using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace SEdgeToPV
{
    internal class TranslateInfo
    {
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

        public static TranslateInfo createFromInputFile(string InFile)
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

            return FileType;
        }

        private static string DecodeName(string Value)
        {
            string NewValue = (string)Value.Clone();
            Dictionary<string, string> dictionary = new Dictionary<string, string>
            {
                { "@_", " " },
                { "@VL", "ä" },
                { "@VI", "á" },
                { "@VQ", "é" },
                { "@WT", "ü" },
                { "@WN", "ö" },
                { "@UT", "Ü" },
                { "@TL", "Ä" },
                { "@UN", "Ö" },
                { "@TI", "Á" },
                { "@TQ", "É" },
                { "@UW", "ß" },
                { "@@", "@" },
                { "@SH", "°" },
                { "@RO", "§" },
                { "@SM", "µ" },
                { "@UP", "Ø" },
                { "@WP", "Ø" }
            };
            foreach (string key in dictionary.Keys)
            {
                NewValue = NewValue.Replace(key, dictionary[key]);
            }

            return NewValue;
        }


        public override string ToString()
        {
            return string.Format("FileName: {0}, FileType: {1}, FormatName: {2}, ConversionInputDir: {3}, ConversionOutputDir: {4}, AdditionalFileFormats [{5}] ", FileName, FileType, FormatName, ConversionInputDir,
                ConversionOutputDir, string.Join(", ", System.Linq.Enumerable.ToArray(AdditionalFileFormats)));
        }

    }
}
