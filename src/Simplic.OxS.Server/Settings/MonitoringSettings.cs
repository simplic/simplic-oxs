using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Settings
{
    /// <summary>
    /// Contains all monitoring settings
    /// </summary>
    public class MonitoringSettings
    {
        /// <summary>
        /// Gets or sets the current otlp (open telemtry endpoint). E.g. "http://otel-collector:4317"
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";

        /// <summary>
        /// Gets or sets the exporter settings. Possible values: console, otlp.
        /// Values can be combined by seperating them with a comma.
        /// </summary>
        public string TracingExporter { get; set; } = "console,otlp";

        /// <summary>
        /// Gets or sets the exporter settings. Possible values: console, otlp.
        /// Values can be combined by seperating them with a comma.
        /// </summary>
        public string LoggingExporter { get; set; } = "console,otlp";

        /// <summary>
        /// Gets or sets the exporter settings. Possible values: console, otlp.
        /// Values can be combined by seperating them with a comma.
        /// </summary>
        public string MetricsExporter { get; set; } = "console,otlp";
    }
}
