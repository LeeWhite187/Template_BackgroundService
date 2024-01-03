using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OGA.SharedKernel;
using OGA.Template.Service.DiagP2P.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;

namespace OGA.Template
{
    public class Program
    {
        static private string _classname = nameof(Program);
        static public IConfiguration Config;

        static public Template_BackgroundService TemplateSvc;

        static public IHost host_ref;


        public static int Main(string[] args)
        {
            // Look to see if we are to wait for the debugger...
            // NOTE: To leverage this wait, add this command line argument:
            //    -waitfordebugger=yes
            {
                bool waitfordebugguer = false;
                // Get the command line arguments, so we can quickly parse them for a debugger wait signal...
                string[] arguments = Environment.GetCommandLineArgs();
                foreach(var f in arguments)
                {
                    if(f.ToLower().Contains("waitfordebugger=yes"))
                    {
                        waitfordebugguer = true;
                        break;
                    }
                }

                // We are to wait for the debugger.
                if(waitfordebugguer)
                {
                    //Spin our wheels waiting for a debugger to be attached....
                    while (!System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Threading.Thread.Sleep(100); //Or Task.Delay()
                    }

                    // We will stop here, once the debugger is attached, so no explicit breakpoint is required.
                    System.Diagnostics.Debugger.Break();

                    Console.WriteLine("Debugger is attached!");
                }
            }


            // Place the following inside a try follow so that we guarantee a closing log entry on shutdown...
            try
            {
                // Setup Host...
                IHost host = null;
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                        "Program:Main - Starting CreateWebHostBuilder....");

                    // Setup the host so that we can use its services...
                    //host = CreateHostBuilder(args).Build();
                    var hb = CreateHostBuilder(args);

                    //hb.ConfigureAppConfiguration((config) =>
                    //{
                    //    // Add in config.json file...
                    //    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    //    //config.AddWriteableJsonFile("config.json", optional: true, reloadOnChange: true);

                    //    var dbgv = config.Build().GetDebugView();

                    //    var bc = config.Build();
                    //    IConfigurationSection bd = bc.GetSection(OGA_SharedKernel.Config.structs.cConfig_BuildData.CONSTANT_SectionName);
                    //    var builddata = bd.Get<OGA_SharedKernel.Config.structs.cConfig_BuildData>();

                    //    int x = 0;

                    //    //// Replace the JSON config sources with our own writeable JSON source...
                    //    //Replace_JSONConfigSources_with_Writeable_Sources(config);
                    //});

                    host = hb.Build();

                    // Store the host reference, so we can use it for DI queries throughout the app...
                    host_ref = host;

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        "Program:Main - Host built and ready.");
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Program:Main - starting host...");

                // Startup the host, so that it can open for business...
                // Different types of run exists here:
                //  Run                  - runs the app and blocks the calling thread until the host is shutdown.
                //  RunAsync             - runs the app and returns a Task that completes when the cancellation token or shutdown is triggered.
                //  Start                - starts the host synchronously.
                //  StartAsync           - starts the host and returns a Task that completes when the cancellation token or shutdown is triggered.
                //                         WaitForStartAsync is called at the start of StartAsync, which waits until it's complete before continuing.
                //                         This can be used to delay startup until signaled by an external event.
                //  StopAsync            - Attempts to stop the host within the provided timeout.
                //  WaitForShutdown      - blocks the calling thread until shutdown is triggered by the IHostLifetime, such as via Ctrl+C/SIGINT or SIGTERM.
                //  WaitForShutdownAsync - returns a Task that completes when shutdown is triggered via the given token and calls StopAsync.
                host.Run();

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Program:Main - host shutdown.");
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Program:Main - closing process...");

                return 1;
            }
            catch (Exception ex)
            {
                //NLog: catch setup errors
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ex,
                    "Program:Main - Stopped program because of exception");

                Console.Error.WriteLine("Program:Main - Stopped program because of exception:");
                Console.Error.WriteLine(ex?.Message?.ToString());
                Console.Error.WriteLine(ex?.StackTrace?.ToString());

                return -20;
            }
            finally
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Program:Main - ****************Ending Logging.");

                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                NLog.LogManager.Shutdown();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
