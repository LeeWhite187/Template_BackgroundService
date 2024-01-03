using System.Threading;
using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;

namespace OGA.Template
{
    /// <summary>
    /// Background Service base class.
    /// A minimum viable background service class, needs a constructor, and to override the setup, teardown, and loop methods.
    /// See this article for how to register and consume a background service: https://oga.atlassian.net/wiki/spaces/~311198967/pages/48660613/Consuming+NET+Core+Background+Service+with+DI
    /// See this article for how to derive from this base class: https://oga.atlassian.net/wiki/spaces/~311198967/pages/124420097/NET+Core+Background+Services
    /// </summary>
    public class BackgroundService_Base : BackgroundService
    {
        #region Private Fields

        protected string _classname;
        protected string _loggingprefix = "";

        readonly protected IServiceProvider _svcprovider;

        static protected volatile int _servicecounter;

        private object _statelock = new object();
        private eServiceState _state;

        private object _shutdownlock = new object();
        private bool _shutdownflag = false;

        /// <summary>
        /// Internal flag used by the service main loop to know if the dispose method has been called.
        /// </summary>
        private bool _disposecalled = false;

        private int _loopduration;

        #endregion


        #region Public Properties

        public int InstanceId { get; private set; }

        /// <summary>
        /// Set while the service is in the active state.
        /// Use this flag across the service, when checking if new data can be accepted, or operations performed.
        /// </summary>
        public bool IsActive { get => (this._state == eServiceState.Active); }

        /// <summary>
        /// Current state of the service.
        /// </summary>
        public eServiceState State { get => _state; }

        /// <summary>
        /// Delay between loop iteration calls.
        /// Amount of time, in milliseconds, between iterations of the service's main thread loop.
        /// </summary>
        public int LoopIterationDelay
        {
            get => _loopduration;

            set
            {
                if (value < 0)
                    return;

                _loopduration = value;
            }
        }

        /// <summary>
        /// Number of times the service's main thread has executed its loop.
        /// </summary>
        public int LoopIterationCounter { get; private set; }

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Default constructor made private, to ensure all derived types pass in a service provider instance.
        /// </summary>
        private BackgroundService_Base()
        {
            _servicecounter++;
            InstanceId = _servicecounter;

            this._classname = nameof(BackgroundService_Base);

            this._loopduration = 3000;

            // Baseline the service state...
            this._state = eServiceState.NotStarted;
        }
        /// <summary>
        /// Accepts a service provider, so the service can perform any work it needs.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public BackgroundService_Base(IServiceProvider serviceProvider) : this()
        {
            this._svcprovider = serviceProvider;
        }

        /// <summary>
        /// Override the dispose call, so we can shutdown the service.
        /// </summary>
        public override void Dispose()
        {
            // Move to the stopping state, if possible...
            this.UpdateState(eServiceState.ShuttingDown);

            // We don't know if the service loop thread is active or not.
            // So, we cannot trust it to close down the service.
            // So, we will do shutdown things, here...
            this._priv_Shutdown();

            // Move to the disposed state...
            this.UpdateState(eServiceState.Disposed);

            base.Dispose();
        }

        #endregion
    
    
        #region Public Methods

        /// <summary>
        /// This method is called by the Host, via StartAsync, when starting the service.
        /// The thread given to this method needs to remain in this method for the lifetime of the service.
        /// The host can kill this service by triggering the given "stoppingtoken".
        /// Meaning: When this method returns, the service is considered dead, and should do NO more work.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool startup_success = false;

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                $"is starting.");

            // Move the service to the starting state...
            this.UpdateState(eServiceState.Starting);

            // Register a cancellation delegate...
            stoppingToken.Register(this.Cancellation_Callback);

            // Do any service startup activities...
            int res = this._priv_Startup(stoppingToken);
            if(res != 1)
            {
                // Service failed to setup.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error($"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                    "Failed to setup service. We will skip active, and shutdown...");

                // Set a failed startup state...
                this.UpdateState(eServiceState.FailStart);

                startup_success = false;
            }
            else
            {
                // Startup succeeded.

                // Set a flag, so the main loop will run...
                startup_success = true;
            }

            // Do work until told to cancel operations...
            try
            {
                // Only fall into the active loop if startup was successful...
                if(startup_success)
                {
                    // Move the service to the active state...
                    this.UpdateState(eServiceState.Active);

                    // Keep the given thread active until the host shuts us down...
                    // Let it drop if the dispose method is called.
                    while (!stoppingToken.IsCancellationRequested && !_disposecalled)
                    {
                        try
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug($"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                                $"performing task loop iteration...");

                            // Increment the loop counter...
                            this.LoopIterationCounter++;

                            // Call the loop...
                            // Wrap this in a try-catch, to ensure the delay still happens of the overridden activities method throws.
                            try
                            {
                                await this.PerformLoopActivities(stoppingToken);
                            }
                            catch (Exception) { }

                            // Wait a little bit before the next iteration...
                            await Task.Delay(_loopduration, stoppingToken);
                        }
                        catch(Exception e)
                        {
                            // Swallow any exception, so that we don't leave this loop until desired...

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e, $"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                                $"Exception occurred in service main thread loop.");
                        }
                    }
                    // Left the while loop.
                }
                // Either the while loop dropped, or we failed to complete startup activities.
                // Either way, the outcome is the same: We perform shutdown activities and leave.

                // Advance to the shutting down state...
                this.UpdateState(eServiceState.ShuttingDown);

                // Do shutdown activities...
                this._priv_Shutdown();

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                        $"Has stopped.");

                // Update state...
                this.UpdateState(eServiceState.Stopped);
            }
            catch(Exception ex)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ex, $"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                    "Exception occurred on service's main thread.");

                // Update state since our main thread is leaving...
                this.UpdateState(eServiceState.Stopped);

                return;
            }
        }

        #endregion


        #region Control Methods

        /// <summary>
        /// Override this method to add startup activities for the derived service implementation.
        /// Make sure to return 1 if startup activities were successful. Otherwise, the service will error out.
        /// No need to call the base method.
        /// </summary>
        /// <returns></returns>
        protected virtual int DoStartupActivities(CancellationToken token)
        {
            return 1;
        }

        /// <summary>
        /// Override this method to add shutdown activities for the derived service implementation.
        /// No need to call the base method.
        /// </summary>
        /// <returns></returns>
        protected virtual void DoShutdownActivities()
        {
            return;
        }

        /// <summary>
        /// Override this method to include any administrative or periodic actions the derived service needs to perform.
        /// Doesn't matter what this method returns. It's defined as a value-task, to ensure unhandled exceptions don't unwind to the TaskScheduler.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async Task<int> PerformLoopActivities(CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return 1;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Private startup method that adds idempotency logic and a try-catch to any startup activities of the derived class.
        /// </summary>
        private int _priv_Startup(CancellationToken stoppingToken)
        {
            // Do any service startup activities...
            // Wrap this in a try-catch, to ensure no exceptions in the derived service upset the logic flow, here.
            try
            {
                // Call the derived class startup method...
                int res = this.DoStartupActivities(stoppingToken);
                if(res != 1)
                {
                    // Service failed to setup.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error($"{_classname}:{InstanceId.ToString()}:{nameof(_priv_Startup)}- " +
                        "Failed to setup service.");
                }

                return res;
            }
            catch(Exception esh)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(esh, $"{_classname}:{InstanceId.ToString()}:{nameof(_priv_Startup)}- " +
                    "Exception occurred in DoShutdownActivities.");

                return -2;
            }
        }

        /// <summary>
        /// Private shutdown method that adds idempotency logic and try-catch to any shutdown logic.
        /// This method ensure the actual shutdown logic executes only once.
        /// It also ensures any derived class shutdown logic is wrapped with a try-catch.
        /// </summary>
        private void _priv_Shutdown()
        {
            // Do shutdown activities...
            // Wrap this in a try-catch, to ensure no exceptions in the derived service upset the logic flow, here.
            try
            {
                lock(_shutdownlock)
                {
                    // Check if we've done shutdown already...
                    if(_shutdownflag)
                    {
                        // The shutdown method has already been called.
                        // We will not execute it twice.

                        return;
                    }

                    // Set the shutdown flag, so we can do it only once...
                    this._shutdownflag = true;
                }
                // If here, we haven't performed a shutdown yet.

                // We will do so, now...
                this.DoShutdownActivities();
            }
            catch(Exception esh)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(esh, $"{_classname}:{InstanceId.ToString()}:{nameof(ExecuteAsync)}- " +
                    "Exception occurred in DoShutdownActivities.");
            }
        }

        /// <summary>
        /// Thread-safe update method that logs state changes.
        /// </summary>
        /// <param name="newstate"></param>
        protected void UpdateState(eServiceState newstate)
        {
            lock(_statelock)
            {
                // Leave if no change...
                if (this._state == newstate)
                    return;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(UpdateState)}- " +
                                $"Attempting state update from ({this._state.ToString()}) to ({newstate.ToString()})...");

                // Make sure the new state is legal...
                if (this._state == eServiceState.FailStart)
                {
                    // Once in a failed start state, the service can only become disposed.
                    if(newstate != eServiceState.Disposed)
                        return;
                }
                if (this._state == eServiceState.Disposed)
                {
                    // No changes are allowed from disposed.
                    return;
                }
                else if (this._state == eServiceState.Stopped)
                {
                    if(newstate != eServiceState.Disposed)
                        return;
                }
                else if (this._state == eServiceState.ShuttingDown)
                {
                    if(newstate != eServiceState.Disposed &&
                       newstate != eServiceState.Stopped)
                        return;
                }
                else if (this._state == eServiceState.Active)
                {
                    if(newstate != eServiceState.Disposed &&
                       newstate != eServiceState.Stopped &&
                       newstate != eServiceState.ShuttingDown)
                        return;
                }
                else if (this._state == eServiceState.Starting)
                {
                    if(newstate != eServiceState.Disposed &&
                       newstate != eServiceState.Stopped &&
                       newstate != eServiceState.ShuttingDown &&
                       newstate != eServiceState.Active &&
                       newstate != eServiceState.FailStart)
                        return;
                }
                else if (this._state == eServiceState.NotStarted)
                {
                    if(newstate != eServiceState.Starting &&
                       newstate != eServiceState.Disposed)
                        return;
                }

                // Update state...
                this._state = newstate;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(UpdateState)}- " +
                                $"State updated from ({this._state.ToString()}) to ({newstate.ToString()}).");
            }
        }

        /// <summary>
        /// Registered to the stopping token, given to the ExecuteAsync method call.
        /// This method will initiate a service shutdown when the stopping token is tripped.
        /// </summary>
        private void Cancellation_Callback()
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(Cancellation_Callback)}- " +
                            $"is stopping.");

            // It's not clear when this registered callback is called.
            // So, we will perform similar tasks to what the dispose method does.

            // Since this callback is registered to the same token that the service's main thread loop watches, we don't need to set a flag to indicate this method was called.
            // We can simply do shutdown activities.

            // Move to the stopping state, if possible...
            this.UpdateState(eServiceState.ShuttingDown);

            // Do shutdown activities...
            this._priv_Shutdown();

            // Move to the stopped state...
            this.UpdateState(eServiceState.Stopped);

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info($"{_classname}:{InstanceId.ToString()}:{nameof(Cancellation_Callback)}- " +
                            $"is stopped.");
        }

        /// <summary>
        /// Composes a standard string to add context to log messages.
        /// Should be called on object construction.
        /// </summary>
        /// <returns></returns>
        protected string Create_LoggingPrefix([CallerMemberName] string caller = null)
        {
            // Make it of the form:
            //  class:instance:methodname-
            string lp = $"{_classname}:{InstanceId.ToString()}:{(caller ?? "")}- ";
            return lp;
        }

        #endregion
    }

    public enum eServiceState
    {
        /// <summary>
        /// Service has not been started.
        /// </summary>
        NotStarted = 0,
        /// <summary>
        /// Service is starting up, but is not yet open for business.
        /// </summary>
        Starting = 1,
        /// <summary>
        /// Service is active and open for business.
        /// </summary>
        Active = 2,
        /// <summary>
        /// Service has been told to shutdown, and is no longer open for business.
        /// </summary>
        ShuttingDown = 3,
        /// <summary>
        /// Service is stopped, and waiting to be disposed.
        /// </summary>
        Stopped = 4,
        /// <summary>
        /// Servivce is disposed.
        /// </summary>
        Disposed = 5,
        /// <summary>
        /// Servivce failed to start.
        /// </summary>
        FailStart = 6
    }
}
