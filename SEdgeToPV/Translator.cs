using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;

namespace SEdgeToPV
{
    internal class Translator
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Dictionary<IntermediateFormat, string> FormatDictiornary = new Dictionary<IntermediateFormat, string>();


        private readonly string InFile;
        private readonly string OutFile;

        static Translator()
        {
            FormatDictiornary.Add(IntermediateFormat.STP, "stp");
            FormatDictiornary.Add(IntermediateFormat.JT, "jt");
            FormatDictiornary.Add(IntermediateFormat.PDF, "pdf");
            FormatDictiornary.Add(IntermediateFormat.IGS, "igs");
            FormatDictiornary.Add(IntermediateFormat.DWG, "dwg");
            FormatDictiornary.Add(IntermediateFormat.DXF, "dxf");
        }

        public Translator(string _InFile, string _OutFile)
        {
            InFile = _InFile;
            OutFile = _OutFile;
        }

        public void Execute()
        {
            TranslateInfo TranslateInfo = TranslateInfo.CreateFromInputFile(InFile);
            log.DebugFormat("Created translation info {0}", TranslateInfo.ToString());

            //Ensure output directory exists
            if (!Directory.Exists(TranslateInfo.ConversionOutputDir))
            {
                Directory.CreateDirectory(TranslateInfo.ConversionOutputDir);
            }

            bool PrimaryFileConversionResult = false;
            if (string.Equals(TranslateInfo.FileType, "dft", StringComparison.CurrentCultureIgnoreCase))
            {
                PrimaryFileConversionResult = Convert2DModel(TranslateInfo);
            }
            else
            {
                PrimaryFileConversionResult = Convert3DModel(TranslateInfo);
            }


            if (PrimaryFileConversionResult && TranslateInfo.AdditionalFileFormats.Count > 0)
            {
                IList<string> CreatedAdditionalFiles = CreateAdditionalFileFormats(TranslateInfo);
                log.Debug("List of generated additional files:");
                foreach (string CreatedAdditionalFile in CreatedAdditionalFiles)
                {
                    log.DebugFormat("{0}", CreatedAdditionalFile);
                }

                PackFilesToPvoa(TranslateInfo, TranslateInfo.FileName, CreatedAdditionalFiles);
            }

            bool IsGenerateThumbnail = string.Compare("true", ConfigurationManager.AppSettings["GenerateThumbnail"], true) == 0;
            if (IsGenerateThumbnail)
            {
                File.WriteAllText(string.Format("{0}\\loaderoptions.txt", TranslateInfo.ConversionOutputDir), "thumbnailcreate=true\n");
            }

        }

        private bool Convert3DModel(TranslateInfo TranslateInfo)
        {
            try
            {
                string IntermediateFileFormat = string.Format("{0}", ConfigurationManager.AppSettings["IntermediateFormat"]);

                IntermediateFormat IntermediateFormat = IntermediateFormat.Unknown;
                if (string.Equals(IntermediateFileFormat, "STEP", StringComparison.CurrentCultureIgnoreCase)
                    || string.Equals(IntermediateFileFormat, "STP", StringComparison.CurrentCultureIgnoreCase))
                {
                    IntermediateFormat = IntermediateFormat.STP;
                }
                else if (string.Equals(IntermediateFileFormat, "JT", StringComparison.CurrentCultureIgnoreCase))
                {
                    IntermediateFormat = IntermediateFormat.JT;
                }
                else
                {
                    throw new Exception(string.Format("Intermediate configured type {0} is not supported, check worker configuration", IntermediateFileFormat));
                }

                ExternalExecutableResult IntermediateResult = PerformSolidEdgeTranslation(TranslateInfo, IntermediateFormat);

                log.Debug("Intermediate File Generation Result");
                log.DebugFormat("Executable return code: {0}", IntermediateResult.ExecutableExitCode);
                log.DebugFormat("Executable STDOUT: {0}", IntermediateResult.ExecutableStdOut);
                log.DebugFormat("Executable STDERR: {0}", IntermediateResult.ExecutableStdErr);
                log.DebugFormat("Executable Additional Job Log: {0}", IntermediateResult.JobLog);

                if (!IntermediateResult.Result || !File.Exists(IntermediateResult.ResultFile))
                {
                    string ReturnMessage = string.Format("Intermediate file generation failed, failed to generete {0}. Process returned ExitCoce {1}: {2}{3}", IntermediateFileFormat, IntermediateResult.ExecutableExitCode,
                        IntermediateResult.ExecutableStdErr, IntermediateResult.JobLog);
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    return false;
                }

                CleanUpLogs(TranslateInfo);

                ExternalExecutableResult PVSResult = PerformPVSConversion(IntermediateResult.ResultFile, TranslateInfo, IntermediateFormat);
                log.Debug("PVS File Generation Result");
                log.DebugFormat("Executable return code: {0}", PVSResult.ExecutableExitCode);
                log.DebugFormat("Executable STDOUT: {0}", PVSResult.ExecutableStdOut);
                log.DebugFormat("Executable STDERR: {0}", PVSResult.ExecutableStdErr);

                if (!PVSResult.Result || !File.Exists(PVSResult.ResultFile))
                {
                    string ReturnMessage = string.Format("PVS file generation failed, failed to generete pvs from {0}. Process returned ExitCoce {1}: {2}{3}", IntermediateFileFormat, PVSResult.ExecutableExitCode,
                        PVSResult.ExecutableStdOut, PVSResult.ExecutableStdErr);
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    return false;
                }

                File.WriteAllText(OutFile, string.Format("0 {0}", PVSResult.ResultFile));
            }
            catch (Exception e)
            {
                log.Error(e);
                File.WriteAllText(OutFile, "1 " + e.Message.Replace(System.Environment.NewLine, "<br />"));
                return false;
            }
            return true;
        }

        private bool Convert2DModel(TranslateInfo TranslateInfo)
        {
            try
            {
                ExternalExecutableResult ConversionResult = PerformSolidEdgeTranslation(TranslateInfo, IntermediateFormat.PDF);

                log.Debug("Intermediate File Generation Result");
                log.DebugFormat("Executable return code: {0}", ConversionResult.ExecutableExitCode);
                log.DebugFormat("Executable STDOUT: {0}", ConversionResult.ExecutableStdOut);
                log.DebugFormat("Executable STDERR: {0}", ConversionResult.ExecutableStdErr);
                log.DebugFormat("Executable Additional Job Log: {0}", ConversionResult.JobLog);

                if (!ConversionResult.Result || !File.Exists(ConversionResult.ResultFile))
                {
                    string ReturnMessage = string.Format("Intermediate file generation failed, failed to generete pdf. Process returned ExitCoce {0}: {1}{2}", ConversionResult.ExecutableExitCode,
                        ConversionResult.ExecutableStdErr, ConversionResult.JobLog);
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    return false;
                }

                CleanUpLogs(TranslateInfo);

                string ASCIIPVSPath = CrateASCIIPVSFile(TranslateInfo, ConversionResult);
                if (!ConversionResult.Result || !File.Exists(ConversionResult.ResultFile))
                {
                    string ReturnMessage = string.Format("Failed to generate ascii pvs file");
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    return false;
                }

                ExternalExecutableResult PVSResult = PerfomASCIIPVSToBinaryConversion(ASCIIPVSPath, TranslateInfo);
                log.Debug("PVS File Generation Result");
                log.DebugFormat("Executable return code: {0}", PVSResult.ExecutableExitCode);
                log.DebugFormat("Executable STDOUT: {0}", PVSResult.ExecutableStdOut);
                log.DebugFormat("Executable STDERR: {0}", PVSResult.ExecutableStdErr);

                if (!PVSResult.Result || !File.Exists(PVSResult.ResultFile))
                {
                    string ReturnMessage = string.Format("PVS file generation failed, failed to convert pvs to binary. Process returned ExitCoce {0}: {1}{2}", PVSResult.ExecutableExitCode, PVSResult.ExecutableExitCode,
                        PVSResult.ExecutableStdOut, PVSResult.ExecutableStdErr);
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    return false;
                }

                File.Delete(ASCIIPVSPath);

                File.WriteAllText(OutFile, string.Format("0 {0}", ConversionResult.ResultFile));
            }
            catch (Exception e)
            {
                log.Error(e);
                File.WriteAllText(OutFile, "1 " + e.Message.Replace(System.Environment.NewLine, "<br />"));
                return false;
            }

            return true;
        }

        private IList<string> CreateAdditionalFileFormats(TranslateInfo TranslateInfo)
        {
            IList<string> CreatedAdditionalFiles = new List<string>();
            string InputFile = TranslateInfo.FileName;
            foreach (string FileFormat in TranslateInfo.AdditionalFileFormats)
            {

                string ResultFile = string.Format("{0}\\{1}.{2}", TranslateInfo.ConversionOutputDir, InputFile, FileFormat);
                if (File.Exists(ResultFile))
                {
                    log.DebugFormat("Additional File already exists {0} reusing", ResultFile);
                    CreatedAdditionalFiles.Add(ResultFile);
                    continue;
                }

                IntermediateFormat IntermediateFormat = IntermediateFormat.Unknown;
                foreach (IntermediateFormat key in FormatDictiornary.Keys)
                {
                    string FormatExtension = FormatDictiornary[key];
                    if (string.Equals(FormatExtension, FileFormat, StringComparison.CurrentCultureIgnoreCase))
                    {
                        IntermediateFormat = key;
                        break;
                    }
                }

                ExternalExecutableResult ConversionResult = PerformSolidEdgeTranslation(TranslateInfo, IntermediateFormat);

                log.DebugFormat("Additional File {0} Result", FileFormat);
                log.DebugFormat("Executable return code: {0}", ConversionResult.ExecutableExitCode);
                log.DebugFormat("Executable STDOUT: {0}", ConversionResult.ExecutableStdOut);
                log.DebugFormat("Executable STDERR: {0}", ConversionResult.ExecutableStdErr);
                log.DebugFormat("Executable Additional Job Log: {0}", ConversionResult.JobLog);

                if (Directory.GetFiles(TranslateInfo.ConversionOutputDir, string.Format("*.{0}", FileFormat)).Length < 1)
                {
                    string ReturnMessage = string.Format("Additional file generation failed, failed to generete {0}. Process returned ExitCoce {0}: {1}{2}", FileFormat, ConversionResult.ExecutableExitCode,
                        ConversionResult.ExecutableStdErr, ConversionResult.JobLog);
                    File.WriteAllText(OutFile, "1 " + ReturnMessage.Replace(System.Environment.NewLine, "<br />"));
                    CreatedAdditionalFiles.Clear();
                    return CreatedAdditionalFiles;
                }

                string[] GeneratedOutputFiles = Directory.GetFiles(TranslateInfo.ConversionOutputDir, string.Format("*.{0}", FileFormat));
                if (GeneratedOutputFiles.Length == 1)
                {
                    CreatedAdditionalFiles.Add(GeneratedOutputFiles[0]);
                }
                else
                {
                    string PackedFilePath = PackFilesToZip(TranslateInfo, InputFile, FileFormat, GeneratedOutputFiles);
                    CreatedAdditionalFiles.Add(PackedFilePath);
                }

                CleanUpLogs(TranslateInfo);
            }
            return CreatedAdditionalFiles;
        }

        private static void CleanUpLogs(TranslateInfo TranslateInfo)
        {
            //CleanUp Intermediate Log Files
            foreach (string LogFile in Directory.GetFiles(TranslateInfo.ConversionOutputDir, "*.log"))
            {
                File.Delete(LogFile);
            }
        }

        private ExternalExecutableResult PerformSolidEdgeTranslation(TranslateInfo TranslateInfo, IntermediateFormat Format)
        {
            log.Debug("Starting SolidEdge conversion");
            ExternalExecutableResult result = new ExternalExecutableResult();
            string SETransPath = string.Format("{0}\\Program\\SolidEdgeTranslationServices.exe", ConfigurationManager.AppSettings["SolidEdgeHome"]);

            string OutFileExtension = FormatDictiornary.ContainsKey(Format) ? FormatDictiornary[Format] : throw new Exception("Extension not found for " + Format);
            string InputFile = TranslateInfo.FileName;
            string ConversionInputPath = string.Format("{0}\\{1}.{2}", TranslateInfo.ConversionInputDir, InputFile, TranslateInfo.FileType);
            string ConversionOutputPath = string.Format("{0}\\{1}.{2}", TranslateInfo.ConversionOutputDir, InputFile, OutFileExtension);

            log.DebugFormat("SolidEdge conversion File Input Path is {0}", ConversionInputPath);
            log.DebugFormat("SolidEdge conversion File Input OutPaht is {0}", ConversionOutputPath);
            log.DebugFormat("Transaltion command {0} -i=\"{1}\" -o=\"{2}\" -t=\"{3}\"", SETransPath, ConversionInputPath, ConversionOutputPath, OutFileExtension);

            using (Process ConversionProcess = new Process())
            {

                ConversionProcess.StartInfo.FileName = SETransPath;
                ConversionProcess.StartInfo.Arguments = string.Format("-i=\"{0}\" -o=\"{1}\" -t=\"{2}\"", ConversionInputPath, ConversionOutputPath, OutFileExtension);
                ConversionProcess.StartInfo.RedirectStandardError = true;
                ConversionProcess.StartInfo.RedirectStandardOutput = true;
                ConversionProcess.StartInfo.UseShellExecute = false;
                ConversionProcess.Start();

                result.ExecutableStdOut = ConversionProcess.StandardOutput.ReadToEnd();
                result.ExecutableStdErr = ConversionProcess.StandardError.ReadToEnd();
                ConversionProcess.WaitForExit();
                result.ExecutableExitCode = ConversionProcess.ExitCode;
                result.Result = result.ExecutableExitCode == 0;
                result.ResultFile = ConversionOutputPath;

                string ConversionLogFile = string.Format("{0}\\{1}.{2}", TranslateInfo.ConversionInputDir, InputFile, "log");
                if (File.Exists(ConversionLogFile))
                {
                    log.Debug("Log file found");
                    result.JobLog = File.ReadAllText(ConversionLogFile);
                }
            }
            return result;
        }

        private ExternalExecutableResult PerformPVSConversion(string InputFilePath, TranslateInfo TranslateInfo, IntermediateFormat Format)
        {
            log.Debug("Starting pvs conversion");
            ExternalExecutableResult result = new ExternalExecutableResult();
            string AssemblyStartDirecotry = Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string ConversionExecutablePath = Format == IntermediateFormat.STP ? string.Format("{0}\\bin\\STEP2PV\\stepbatch.bat", AssemblyStartDirecotry) : string.Format("{0}\bin\\JT2PV\\jtbatch.bat", AssemblyStartDirecotry);
            string InputFile = TranslateInfo.FileName;

            log.DebugFormat("PVS File Input Path is {0}", InputFilePath);
            log.DebugFormat("Transaltion command {0} -p \"{1}\" \"{2}\"", ConversionExecutablePath, TranslateInfo.ConversionOutputDir, InputFilePath);

            using (Process ConversionProcess = new Process())
            {

                ConversionProcess.StartInfo.FileName = ConversionExecutablePath;
                ConversionProcess.StartInfo.Arguments = string.Format("-p \"{0}\" \"{1}\"", TranslateInfo.ConversionOutputDir, InputFilePath);
                ConversionProcess.StartInfo.RedirectStandardError = true;
                ConversionProcess.StartInfo.RedirectStandardOutput = true;
                ConversionProcess.StartInfo.UseShellExecute = false;
                ConversionProcess.Start();

                result.ExecutableStdOut = ConversionProcess.StandardOutput.ReadToEnd();
                result.ExecutableStdErr = ConversionProcess.StandardError.ReadToEnd();
                ConversionProcess.WaitForExit();
                result.ExecutableExitCode = ConversionProcess.ExitCode;

                string[] GeneratedOutputFiles = Directory.GetFiles(TranslateInfo.ConversionOutputDir, "*.pvs");
                if(GeneratedOutputFiles.Length > 0)
                {
                    result.ResultFile = GeneratedOutputFiles[0];
                    result.Result = true;
                }
                else
                {
                    result.ResultFile = string.Format("{0}\\{1}.pvs", TranslateInfo.ConversionOutputDir, InputFile);
                    result.Result = false;
                }
                
            }

            return result;
        }

        private ExternalExecutableResult PerfomASCIIPVSToBinaryConversion(string ASCIIPVSPath, TranslateInfo TranslateInfo)
        {
            log.Debug("Starting pvs binary conversion");
            ExternalExecutableResult result = new ExternalExecutableResult();
            string AssemblyStartDirecotry = Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string ConversionExecutablePath = string.Format("{0}\\bin\\PVSCHANGE\\pvschangebatch.bat", AssemblyStartDirecotry);
            string InputFile = TranslateInfo.FileName;

            log.DebugFormat("PVS File Input Path is {0}", ASCIIPVSPath);
            log.DebugFormat("Transaltion command {0} -Dradapter/outputAsciiED 0 -p \"{1}\" -o \"{2}\" \"{3}\"", ConversionExecutablePath, TranslateInfo.ConversionOutputDir, string.Format("{0}.pvs", TranslateInfo.FileName), ASCIIPVSPath);

            using (Process ConversionProcess = new Process())
            {

                ConversionProcess.StartInfo.FileName = ConversionExecutablePath;
                ConversionProcess.StartInfo.Arguments = string.Format("-Dradapter/outputAsciiED 0 -p \"{0}\" -o \"{1}\" \"{2}\"", TranslateInfo.ConversionOutputDir, string.Format("{0}.pvs", TranslateInfo.FileName), ASCIIPVSPath);
                ConversionProcess.StartInfo.RedirectStandardError = true;
                ConversionProcess.StartInfo.RedirectStandardOutput = true;
                ConversionProcess.StartInfo.UseShellExecute = false;
                ConversionProcess.Start();

                result.ExecutableStdOut = ConversionProcess.StandardOutput.ReadToEnd();
                result.ExecutableStdErr = ConversionProcess.StandardError.ReadToEnd();
                ConversionProcess.WaitForExit();
                result.ExecutableExitCode = ConversionProcess.ExitCode;
                result.Result = result.ExecutableExitCode == 0;
                string[] GeneratedOutputFiles = Directory.GetFiles(TranslateInfo.ConversionOutputDir, "*.pvs");
                if (GeneratedOutputFiles.Length > 0)
                {
                    result.ResultFile = GeneratedOutputFiles[0];
                    result.Result = true;
                }
                else
                {
                    result.ResultFile = string.Format("{0}\\{1}.pvs", TranslateInfo.ConversionOutputDir, InputFile);
                    result.Result = false;
                }
            }

            return result;
        }

        private static string CrateASCIIPVSFile(TranslateInfo TranslateInfo, ExternalExecutableResult ConversionResult)
        {
            string ASCIIPVSPath = string.Format("{0}\\{1}_ascii.pvs", TranslateInfo.ConversionOutputDir, TranslateInfo.FileName);
            string SourceFileName = Path.GetFileName(ConversionResult.ResultFile);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(false)
            };
            using (XmlWriter writer = XmlWriter.Create(ASCIIPVSPath, settings))
            {
                writer.WriteStartDocument();
                //Root
                writer.WriteStartElement("PV_FILE");
                writer.WriteAttributeString("type", "PVS");
                writer.WriteAttributeString("version", "0303");

                //Section index
                {
                    writer.WriteStartElement("section_index");
                    //internal section 2
                    writer.WriteStartElement("internal_section");
                    writer.WriteAttributeString("type", "2");
                    writer.WriteEndElement();

                    //internal section 3
                    writer.WriteStartElement("internal_section");
                    writer.WriteAttributeString("type", "3");
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }
                //Section structure
                {
                    writer.WriteStartElement("section_structure");

                    writer.WriteStartElement("component");
                    writer.WriteAttributeString("name", TranslateInfo.FileName);

                    writer.WriteStartElement("document_source");
                    writer.WriteAttributeString("file_name", SourceFileName);
                    writer.WriteEndElement();

                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }


                //Section properties
                {
                    writer.WriteStartElement("section_properties");

                    writer.WriteStartElement("property_component_ref");

                    writer.WriteStartElement("property");
                    writer.WriteAttributeString("name", "Source_file_name");
                    writer.WriteAttributeString("value", SourceFileName);
                    writer.WriteEndElement();

                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return ASCIIPVSPath;
        }

        private string PackFilesToZip(TranslateInfo TranslateInfo, string InputFile, string PackedExtension, IEnumerable<string> GeneratedOutputFiles)
        {
            string WorkingDir = Directory.CreateDirectory(string.Format("{0}\\{1}", TranslateInfo.ConversionOutputDir, Guid.NewGuid())).FullName;

            foreach (string GeneratedOutputFile in GeneratedOutputFiles)
            {
                string CopyDestinationPath = string.Format("{0}\\{1}", WorkingDir, Path.GetFileName(GeneratedOutputFile));
                File.Move(GeneratedOutputFile, CopyDestinationPath);
            }

            string ZipPath = string.Format("{0}\\{1}_{2}.zip", TranslateInfo.ConversionOutputDir, InputFile, PackedExtension);
            ZipFile.CreateFromDirectory(WorkingDir, ZipPath);

            Directory.Delete(WorkingDir, true);

            return ZipPath;
        }

        private string PackFilesToPvoa(TranslateInfo TranslateInfo, string InputFile, IEnumerable<string> GeneratedOutputFiles)
        {
            string WorkingDir = Directory.CreateDirectory(string.Format("{0}\\{1}", TranslateInfo.ConversionOutputDir, Guid.NewGuid())).FullName;

            foreach (string GeneratedOutputFile in GeneratedOutputFiles)
            {
                string CopyDestinationPath = string.Format("{0}\\{1}", WorkingDir, Path.GetFileName(GeneratedOutputFile));

                if (GeneratedOutputFile.ToLower().EndsWith(".pdf") && string.Equals(TranslateInfo.FileType, "dft", StringComparison.CurrentCultureIgnoreCase))
                {
                    File.Copy(GeneratedOutputFile, CopyDestinationPath);
                }
                else
                {
                    File.Move(GeneratedOutputFile, CopyDestinationPath);
                }
            }

            string ZipPath = string.Format("{0}\\{1}.zip", TranslateInfo.ConversionOutputDir, Guid.NewGuid());
            ZipFile.CreateFromDirectory(WorkingDir, ZipPath);

            File.Move(ZipPath, string.Format("{0}\\additionals.pvoa", TranslateInfo.ConversionOutputDir, InputFile));

            Directory.Delete(WorkingDir, true);

            return ZipPath;
        }

    }

    public enum IntermediateFormat
    {
        Unknown = 0,
        STP = 1,
        JT = 2,
        IGS = 3,
        PDF = 4,
        DWG = 5,
        DXF = 6
    }
}
