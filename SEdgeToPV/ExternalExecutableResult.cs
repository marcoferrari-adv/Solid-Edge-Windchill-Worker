namespace SEdgeToPV
{
    internal class ExternalExecutableResult
    {
        public bool Result { get; set; } = false;

        public int ExecutableExitCode { get; set; } = -999;

        public string JobLog { get; set; } = "";

        public string ExecutableStdOut { get; set; } = "";

        public string ExecutableStdErr { get; set; } = "";

        public string ResultFile { get; set; } = "";
    }
}
