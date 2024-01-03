using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OGA.SharedKernel.Process;
using OGA.SharedKernel.Services;
using OGA.SharedKernel;
using OGA.Template.Service.DiagP2P.Service;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OGA.Template
{
    public class Startup
    {
        protected readonly IWebHostEnvironment _env;

        protected string _classname;

        protected IConfiguration Configuration { get; }
        
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _classname = nameof(Startup);
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(_classname + ":WebAPI_Startup_Base - constructor started.");

            _env = env;

            Configuration = configuration;

            Program.Config = configuration;
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(_classname + ":WebAPI_Startup_Base - constructor done.");
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                "Startup:ConfigureServices - started.");

            // Add in Swagger...
            Add_Swagger_Service(services);

            // Set any System.Text.Json Options....
            {
                // NOTE: Newtonsoft.Json usage was deprecated by some library components of NET5, for their own library, System.Text.Json.
                // And since Microsoft migrated NET5 to System.Text.Json, they've created several incompatibilities in json parsing.
                // Specifically, their library choice is case-sensitive by default, which breaks lots of things that used to work.
                // And, they no longer forgive trailing commas in json.
                // These issues are the exact opposite of what any library/system/framework should do.
                // They should have adhered to the compatibility Best Practice of: Be forgiving on input, and rigid on output.
                // Such a practice would ensure their library choice maintained compatibility.
                // But, here we are.
                // Adapted from this article about the issue and fixes:
                //  https://khalidabuhakmeh.com/aspnet-core-6-mvc-upgrade-systemtextjson-serialization-issues

                // So, the below global options fix some of the new defaults...
                // Specifically, we allow trailing commas, that will cause the new library to throw.
                // And, we allow case-insensitive property matching on deserialization.
                // Also. We added a switch to format all serialized output, so it's readable for diagnostics.
                services.Configure<JsonOptions>(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.AllowTrailingCommas = true;
                    options.JsonSerializerOptions.WriteIndented = true;
                });
            }


            // Configure DI for application services...
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Startup:ConfigureServices: Adding services...");


                // Register the Template Service for use...
                {
                    // See this for details: https://oga.atlassian.net/wiki/spaces/~311198967/pages/48660613/Consuming+NET+Core+Background+Service+with+DI
                    // Registering a hosted service is a multi-step process:
                    // First, we will register a singleton for the service.
                    // This singleton background service will be instantiated, but will not actually be started.
                    // Then, we start it up, to ensure we can throw an exception if it fails to start.
                    // And then, we register it as a hosted service, so the runtime can shut it down ready to close.

                    // To make things a little easier, we will register the diagnostic P2P service with a lamba.
                    // This allows us to reference the queue client config property of Program.cs.
		            services.AddSingleton<Template_BackgroundService>(sp =>
		            {
                        // Pull in the other dependencies...
                        // These have been removed from construction.
			            //var secdirsvc = sp.GetService<IDirectoryService_for_UserSecurityCache_v2>();
			            //var connmult = sp.GetService<IConnectionMultiplexer>();
                        // return new DiagP2P_BackgroundService(sp, secdirsvc, Program.RMQ_ClientConfig, connmult);
			         
                        return new Template_BackgroundService(sp);
		            });
                    // Now, we will register the service as a hosted service...
                    services.AddHostedService<Template_BackgroundService>(svcprovider =>
                    {
                        // Get the singleton instance that we just registered...
                        var svc = svcprovider.GetService<Template_BackgroundService>();

                        // NOTE: The singleton instance has not been started yet.
                        // So, we can set it up, now.
                        svc.LoopIterationDelay = ConfigItems_WaitingforStorage.TemplateService_AdminLoop_Delay;

                        // Tell the service to do its mandatory startup steps, so we can throw if they fail...
                        if(svc.MandatoryStartup() != 1)
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error($"{nameof(Template_BackgroundService)} failed to startup.");
                            Console.Error.WriteLine($"{nameof(Template_BackgroundService)} failed to startup.");
                            throw new Exception($"{nameof(Template_BackgroundService)} failed to start.");
                        }
                        // If here, the service has started.

                        // Save off a reference to the service, so it's easier to reach...
                        Program.TemplateSvc = svc;

                        // Register the hosted service...
                        return svc;
                    });
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Startup:ConfigureServices: Services added.");
            }

            // Add what level of services the host will need...
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Startup:ConfigureServices: Adding Controller setup...");

                // This describes what to add to a startup. Taken from:
                // https://www.strathweb.com/2020/02/asp-net-core-mvc-3-x-addmvc-addmvccore-addcontrollers-and-other-bootstrapping-approaches/
                // Set the level of core services needed for the application...
                // services.AddMvcCore; // This registers core services for MVC applications, no model validation, and no authorization.
                // services.AddControllers(); // Includes cores services of AddMVCCore, plus authorization services, API explorer, data annotations (for model validation), formatter mappings, and CORS.
                // services.AddControllersWithViews(); // Includes AddControllers, views functionality (Razor view engine), and cache tag helper. Use this for a classic MVC site.
                // services.AddRazorPages(); // Includes MVCCore. Intended for bootstrapping Razor Pages support, adds authorization services, data annotations (for model validation), and cache tag helper.
                // services.AddMvc(); Includes AddControllersWithViews() and AddRazorPages(). This is the kitchen sink of all features.
                // Pick one of these to provide NewtonSoft JSON support, based on the level of services chosen above...
                var mvcbuilder = services.AddControllers().AddNewtonsoftJson();
                //services.AddControllersWithViews().AddNewtonsoftJson();
                //services.AddRazorPages().AddNewtonsoftJson();

                // Since we do leverage Memory Cache in Security Directory and UserContext lookups, we need to ensure the caching service is available...
                // This includes services such as: DirectoryService_Adapter_for_USC and cUserSecurityService.
                // And, we add it here, in case memory cache was not included in the choice of API level (above).
                // See this article for memory cache implementation notes:
                //  https://oga.atlassian.net/wiki/spaces/~311198967/pages/93159425/NET+Core+In-Memory+Cache
                services.AddMemoryCache();

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Startup:ConfigureServices: Controller setup added.");
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                "Startup:Configure: Configure Services done.");
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Order of calls in this method are listed in the following guide:
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-5.0
            // This reference desribes the russian-doll implementation of how middleware calls are organized for each request:
            // https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built
            // Here's the expected order...
            //  ExceptionHandler... developer or production mode
            //  Swagger support... if in development
            //  HSTS
            //  Application Lifetime Event Handlers (Not really sure if this HAS to be here, but there is no documentation to say, actually, where).
            //  HttpsRedirection
            //  Static Files
            //  Routing -   Adds the EndpointRoutingMiddleware middleware to the request pipeline.
            //              This middleware builds endpoint metadata for each request, that is used by later middleware.
            //  CORS
            //  Authentication
            //  Authorization
            //  Custom Middleware
            //  Endpoint -  Adds the EndpointMiddleware to the request pipeline.
            //              This is where you register all endpoints for the application.

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Startup:Configure - Configure started.");

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Startup:Configure: Performing Configuration...");

            // Decide what error path to take for unhandled exceptions in middleware and final action processing....
            if (env.IsDevelopment())
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Startup:Configure: Running in development environment.");

                // For development, we will redirect to a developer exception handler to return the unhandled exception data to the caller.
                app.UseDeveloperExceptionPage();

                // Add swagger support so the as-built API can be listed for development.
                app.UseSwagger();
                // Create an endpoint name of the form: Processname v1.0.2.3
                string endpointname = "test v1.0.1(1)";
                //string endpointname = OGA.SharedKernel.Process.App_Data_v2.Process_Name +
                //                        " v" + OGA.SharedKernel.Process.App_Data_v2.Version3.ToString() +
                //                        $"({App_Data_v2.BuildNumber.ToString()})";
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", endpointname));
            }
            else
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Startup:Configure: Running in production.");

                // Traps unhandled exceptions in production.
                // Will retriggers a request to the /error path if a response hasn't started.
                app.UseExceptionHandler("/Error");

                //// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }

            //// Setup callbacks for application lifetime events...
            //{
            //    lifetime.ApplicationStarted.Register(OnApplicationStarted);
            //    lifetime.ApplicationStopping.Register(OnApplicationStopping);
            //    lifetime.ApplicationStopped.Register(OnApplicationStopped);
            //}

            if (!env.IsDevelopment())
			{
				// Redirect the caller to https in production...
				app.UseHttpsRedirection();
			}				

            //// Added to allow for clients to download files directly from the site.
            //app.UseStaticFiles();

            // Added to create endpoint metadata for each request that is used by later middleware.
            // Add this, after static files.
            // Ensure this occurs before any authentication, authorization, or middleware.
            app.UseRouting();

            //// Add telemetry and monitoring for all web calls...
            //app.UseMiddleware<OGA.WebAPI_Base.Middleware.TelemetryMiddleware>();

            //Commented out use authorization because we are using auth tokens through a middleware.
            app.UseAuthentication();
            app.UseAuthorization();

            //// global error handler
            //app.UseMiddleware<OGA.WebAPI_Base.Helpers.ErrorHandlerMiddleware>();

            //// Add in any middleware before authentication, so we have additional data, and are not adding headers after a response has been started...
            //app.UseMiddleware<OGA.WebAPI_Base.Middleware.ClientRequestLoggingMiddleware>();

            // Instead of using the JWT middleware (that terminates tokens in-process), we will use the external auth middleware.
            // The external auth middleware is needed when we are leveraging auth token processing at the API gateway.
            //app.UseMiddleware<OGA.Authentication.Web.Middleware.ExternalAuth_Middleware>();
            //app.UseMiddleware<OGA.Authentication.Web.Middleware.JwtMiddleware>();

            // Register endpoints, here.
            // This is where controllers are mapped to routes.
            app.UseEndpoints(endpoints =>
            {
//                app.UseEndpoints(x => x.MapControllers());
                endpoints.MapControllers();

            });

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Startup:Configure - Configure completed.");
        }

        protected void Add_Swagger_Service(IServiceCollection services)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(_classname + ":Add_Swagger_Service - Adding Swagger doc API...");

            services.AddSwaggerGen(delegate (SwaggerGenOptions c)
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = App_Data_v2.Process_Name + " v" + App_Data_v2.Version3.ToString() + "(" + App_Data_v2.BuildNumber + ")",
                    Version = "v1"
                });
            });

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(_classname + ":Add_Swagger_Service - Swagger doc API added.");
        }
    }
}
