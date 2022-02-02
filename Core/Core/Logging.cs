using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace SipgateVirtualFax.Core
{
    public static class Logging
    {
        private static volatile bool _initialized;

        private static void SetupLogging()
        {
            var layout = new SimpleLayout("${longdate}|${logger}|${level:uppercase=true} - ${message} ${exception:format=ToString,StackTrace}");
            var config = new LoggingConfiguration();
            Target[] targets = {
                new FileTarget()
                {
                    FileName = Util.AppPath("application.log"),
                    Layout = layout
                },
                new ConsoleTarget()
                {
                    Layout = layout
                }
            };
            foreach (var target in targets)
            {
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, target);
            }

            LogManager.Configuration = config;
        }

        public static Logger GetLogger(string name)
        {
            if (!_initialized)
            {
                _initialized = true;
                SetupLogging();
            }
            return LogManager.GetLogger(name);
        }
    }
}