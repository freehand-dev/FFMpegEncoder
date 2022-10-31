using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using sdi2srt_ffmpeg.Services;
using System.Reflection;
using FFMpegCore;
using System.Text;
using sdi2srt_ffmpeg.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Azure;

if (WindowsServiceHelpers.IsWindowsService())
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// Azure AppConfiguration
IConfigurationRefresher? _configurationRefresher = null;

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

    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConfiguration((IConfiguration)context.Configuration.GetSection("Logging"));
        logging.AddConsole();
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

    public static string GetServiceName()
    {
        return String.Format("{0} ({1})", Program.ServiceName, Program.InstanceName);
    }
}
