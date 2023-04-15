using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using FFMpegEncoder.Services;
using System.Reflection;
using FFMpegCore;
using System.Text;
using FFMpegEncoder.Models;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using FFMpegEncoder.Infrastructure.Metrics;
using FFMpegEncoder.Infrastructure;
using OpenTelemetry.Exporter;
using Microsoft.Extensions.Options;
using Serilog;

if (WindowsServiceHelpers.IsWindowsService())
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// Azure AppConfiguration
IConfigurationRefresher? _configurationRefresher = null;

var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: Program.ServiceName, serviceInstanceId: Program.InstanceName, serviceVersion: Program.GetVersion());

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

IHost host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();
        IHostEnvironment env = context.HostingEnvironment;
        #region WorkingDirectory
        string workingDirectory = env.ContentRootPath;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            workingDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FreeHand", env.ApplicationName);

        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            workingDirectory = System.IO.Path.Combine($"/opt/", env.ApplicationName, "etc", env.ApplicationName);
        }
        if (!System.IO.Directory.Exists(workingDirectory))
            System.IO.Directory.CreateDirectory(workingDirectory);

        config.SetBasePath(workingDirectory);

        // add workingDirectory service configuration
        config.AddInMemoryCollection(new Dictionary<string, string>
           {
              {"WorkingDirectory", workingDirectory}
              ,
           });
        #endregion

        //
        Console.WriteLine($"$Env:EnvironmentName={env.EnvironmentName}");
        Console.WriteLine($"$Env:ApplicationName={env.ApplicationName}");
        Console.WriteLine($"$Env:ContentRootPath={env.ContentRootPath}");
        Console.WriteLine($"WorkingDirectory={workingDirectory}");
        Console.WriteLine($"CurrentDirectory={Directory.GetCurrentDirectory()}");

        config.AddJsonFile($"{env.ApplicationName}.json", optional: true, reloadOnChange: true);
        config.AddIniFile($"{env.ApplicationName}.conf", optional: true, reloadOnChange: true);

        config.AddCommandLine(args);
        config.AddEnvironmentVariables();

        string? localConfig = context.Configuration.GetValue<string>("local-config");
        string? azureConfig = context.Configuration.GetValue<string>("azure-config");
        Console.WriteLine($"Config={localConfig ?? azureConfig ?? "none"}");

        Program.InstanceName = context.Configuration.GetValue<string>("name") ?? Program.InstanceName;
        Console.WriteLine($"ServiceName={Program.ServiceName}; InstanceName={Program.InstanceName}");

        // 
        if (!string.IsNullOrEmpty(Program.InstanceName))
        {
            config.AddJsonFile($"{Program.InstanceName}.json", optional: true, reloadOnChange: true);
            config.AddIniFile($"{Program.InstanceName}.conf", optional: true, reloadOnChange: true);
        }

        //
        if (!string.IsNullOrEmpty(localConfig))
        {
            switch (System.IO.Path.GetExtension(localConfig))
            {
                case ".json":
                    config.AddJsonFile(localConfig, optional: true, reloadOnChange: true);
                    break;
                case ".ini":
                case ".conf":
                    config.AddIniFile(localConfig, optional: true, reloadOnChange: true);
                    break;
                default:
                    break;
            }
        }

        //
        if (!string.IsNullOrEmpty(azureConfig))
        {

            //Connect to your App Config Store using the connection string
            config.AddAzureAppConfiguration(options =>
            {
                options.Connect(azureConfig);
                options.ConfigureClientOptions(clientOptions => clientOptions.Retry.MaxRetries = 5);
                options.ConfigureRefresh(refresh =>
                {
                    refresh.Register("Sentinel", true)
                           .SetCacheExpiration(TimeSpan.FromSeconds(1));
                });
                _configurationRefresher = options.GetRefresher();
            }, optional: true);
        }

    })
    .ConfigureServices((context, services) =>
    {

        GlobalFFOptions.Configure(opt =>
        {
            opt.BinaryFolder = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg-bin");
            opt.Encoding = Encoding.UTF8;
        });

        // FFMpegProgressListener
        services.AddHostedService<FFMpegProgressListener>();


        // Configuration FFMpegOptions
        services.Configure<FFMpegOptions>(context.Configuration.GetSection("ffmpeg"));

        // FFMpegWorker
        services.AddHostedService<FFMpegWorker>();

        // Azure AppConfiguration
        if (_configurationRefresher != null)
        {
            services.AddHostedService<AzureAppConfigRefresher>();
            services.AddSingleton<IConfigurationRefresher>(_configurationRefresher);
        }



        // OpenTelemetry
        services.AddOpenTelemetry().WithMetrics(builder =>
        {
            builder.SetResourceBuilder(resourceBuilder);
            builder.AddMeter(FFMpegProgressMeter.Meter.Name);
            builder.AddOtlpExporter((exporterOptions, metricReaderOptions) =>
            {
                context.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(exporterOptions);
                System.Console.WriteLine($"OTLP Exporter is using {exporterOptions.Protocol} protocol and endpoint {exporterOptions.Endpoint}");

                //
                //metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
                //metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                //
                //exporterOptions.HttpClientFactory = () =>
                //{
                //    HttpClient client = new HttpClient();
                //    client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
                //    return client;
                //};
            });
            //builder.AddConsoleExporter();
        });


    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();

        logging.AddConfiguration((IConfiguration)context.Configuration.GetSection("Logging"));
        logging.AddConsole();

        // serilog
        string serilogPath = Path.Combine(context.Configuration["WorkingDirectory"] ?? Directory.GetCurrentDirectory(), $"logs\\{Program.InstanceName}_.txt");
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(serilogPath,
                        rollingInterval: RollingInterval.Day,

                        rollOnFileSizeLimit: true)
            .CreateLogger();
        logger.Information($"Logging path is {Path.GetDirectoryName(serilogPath)}");
        logger.Information($"WorkingDirectory is {context.Configuration["WorkingDirectory"] ?? "none"}");
        logger.Information($"GetCurrentDirectory is {Directory.GetCurrentDirectory()}");
        logging.AddSerilog(logger);

        // OpenTelemetry
        logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.AddOtlpExporter(otlpOptions =>
            {
                    context.Configuration.GetSection("OpenTelemetry:OtlpExporter").Bind(options);
            });
/*
            options.AddConsoleExporter(options =>
            {
                options.MetricReaderType = MetricReaderType.Periodic;
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            });
*/
            // Export the body of the message
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
        });
    })
    .UseWindowsService(options =>
    {
        options.ServiceName = Program.GetServiceName();
    })
    .UseSystemd()
    .Build();


await host.RunAsync();


partial class Program
{
    public static string InstanceName = "Default";
    public static readonly string ServiceName = "FreeHand FFMpegEncoder";

    public static void PrintProductVersion()
    {
        var assembly = typeof(Program).Assembly;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Starting {product} v{version}...");
        Console.ResetColor();
    }


    public static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "unknown";
    }
    public static string GetServiceName()
    {
        return String.Format("{0} ({1})", Program.ServiceName, Program.InstanceName);
    }
}
