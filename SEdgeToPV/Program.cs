using log4net;
using System;
using System.Configuration;
using System.IO;

namespace SEdgeToPV
{
    internal class Program
    {

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static readonly string WVSJOBFileName = "_wvsjob.paj";

        private static int Main(string[] args)
        {

            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: SEdgeToPV.exe inputfile outputfile");
                return -1;
            }

            string InputFile = args[0];
            string OutputFile = args[1];

            bool IsDebug = string.Compare("true", ConfigurationManager.AppSettings["DebugFlag"], true) == 0;
            if (IsDebug)
            {
                log4net.Config.XmlConfigurator.Configure();
            }

            log.DebugFormat("SEdgeToPV invoked with In File: {0} Out File: {1}", InputFile, OutputFile);
            try
            {
                using (Translator t = new Translator(InputFile, OutputFile))
                {
                    t.Execute();
                }
            }
            catch (Exception e)
            {
                log.Error(e);
                File.WriteAllText(OutputFile, "1 " + e.Message);
            }
            return 0;
        }
    }
}
