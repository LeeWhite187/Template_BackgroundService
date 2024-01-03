using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OGA.Template.Service.DiagP2P.Service
{
    /// <summary>
    /// Provides diagnostic P2P message handling.
    /// See this for method to register and run a Hosted Service in NET6: https://oga.atlassian.net/wiki/spaces/~311198967/pages/48660613/Consuming+NET+Core+Background+Service+with+DI
    /// </summary>
    public class Template_BackgroundService : BackgroundService_Base
    {
        #region Private Fields

        private int _instancecounter;

        #endregion


        #region ctor / dtor

        public Template_BackgroundService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            this._classname = nameof(Template_BackgroundService);
        }

        #endregion


        #region Setup and Teardown

        /// <summary>
        /// Include in this method any steps that MUST pass. Meaning, if this method fails, the process is not allowed to start.
        /// </summary>
        /// <returns></returns>
        public int MandatoryStartup()
        {
            // Do any startup activites, here, that must run to completion, or the app cannot start....
            // NOTE: This will be called, also, during service startup, via Hosted DI.
            // So, make sure anything work, here, is idempotent.

            //// We will only setup the queue client if it doesn't already exist...
            //if(this._queueclient == null)
            //{
            //    // Client is not yet setup.

            //    // Create the client queue...
            //    _queueclient = new DiagP2P_RMQ_Client();
            //    _queueclient.Username = _rmp_config.Username;
            //    _queueclient.Password = _rmp_config.Password;
            //    _queueclient.Http_Port = _rmp_config.Http_Port;
            //    _queueclient.Amqp_Port = _rmp_config.Amqp_Port;
            //    _queueclient.Host = _rmp_config.Host;
            //    _queueclient.ClientProvidedName = "DiagP2PService";
            //    _queueclient.OnChannelClosed = Handle_QueueClient_ChannelClosed;
            //    _queueclient.OnConnectionClosed = Handle_QueueClient_ConnectionClosed;
            //    _queueclient.OnFlowControl = Handle_QueueClient_FlowControl;
            //    _queueclient.OnIncomingDiagP2PMessageReceived = this.Handle_Received_DiagP2PMessage_fromClient;


            //    // Do processor startup, here, to ensure they are live before messages come in...
            //    Setup_Processors();


            //    // Connect the client...
            //    if (_queueclient.Start() != 1)
            //    {
            //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error("Failed to start Queue Client. Aborting process...");

            //        _queueclient.Stop();

            //        return -1;
            //    }
            //}


            // Add any other mandatory setup steps...

            return 1;
        }

        /// <summary>
        /// Override this method to add startup activities for the derived service implementation.
        /// Make sure to return 1 if startup activities were successful. Otherwise, the service will error out.
        /// No need to call the base method.
        /// </summary>
        /// <returns></returns>
        protected override int DoStartupActivities(CancellationToken token)
        {
            // Startup is allowed in two pieces:
            //  Pre-Host Start Actions:
            //      These are steps that need to be performed before the NET Core Host is active.
            //      Specifically, we do these before a host-start, so that, if they fail, we will not startup the process.
            //  Normal Service Start Actions:
            //      These are steps that can be called by the Host, once it's active.

            // So knowing that:
            // Do any mandatory startup activities in the call to: ExternalStartup.
            // And, all the other starutp actions, in this method.

            // Make sure the ExternalStartup method has been called....
            int res = this.MandatoryStartup();
            if(res != 1)
            {
                // Failed to start.
                return -1;
            }

            // Include any startup actions, here...

            // Return '1' for success...
            return 1;
        }

        /// <summary>
        /// This override used to close the RMQ client, during shutdown or dispose.
        /// </summary>
        /// <returns></returns>
        protected override void DoShutdownActivities()
        {
            // Add any teardown activities, here...
            //try
            //{
            //    _queueclient?.Dispose();
            //} catch(Exception e) { }

            //try
            //{
            //    _handler_RawDiagP2PEnvelope?.Closedown();
            //} catch(Exception e) { }

            // Add other shutdown actions, here...
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Performs a single loop iteration of admin activities for the service.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task<int> PerformLoopActivities(CancellationToken token)
        {
            // Add periodic tasks, to this list...


            // Do other cleanup actions...

            return 1;
        }

        #endregion


        #region Periodic Tasks

        #endregion


        #region Public Access Methods

        ///// <summary>
        ///// Accepts diagnostic P2P messages from the RMQ.
        ///// Calls the common accept method for diag P2P messages.
        ///// Returns the following:
        /////      1 - Message was successfully sent over the client's websocket.
        /////      0 - Client connection was found, but the message could not be sent over it.
        /////     -1 - Client connection could not be identified for message.
        /////     -2 - Message failed validation. Could not be sent.
        /////     -3 -  Exception occurred.
        ///// </summary>
        ///// <param name="qc"></param>
        ///// <param name="msg"></param>
        ///// <returns></returns>
        //public int Handle_Received_DiagP2PMessage_fromClient(IRMQ_Client qc, DiagP2PEnvelopeDTO msg)
        //{
        //    var logprefix = Create_LoggingPrefix();

        //    try
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(logprefix +
        //                    $"Received an incoming diagnostic P2P message from RMQ. Sending it to the common handler...");

        //        // Hand it off to our dedicated handler...
        //        var res = this._handler_RawDiagP2PEnvelope.Accept_Incoming(msg, true).GetAwaiter().GetResult();

        //        return res;

        //        //// Call the common accept handler for diagnostic P2P messages...
        //        //// Treat it like an RPC call, so that we get a return that indicates if the message was processed and sent to the websocket client, or not.
        //        ///// Returns the following:
        //        /////      1 - Message was successfully sent over the client's websocket.
        //        /////      0 - Client connection was found, but the message could not be sent over it.
        //        /////     -1 - Client connection could not be identified for message.utty
        //        /////     -2 - Message failed validation. Could not be sent.
        //        /////     -3 -  Exception occurred.
        //        //var res = this.Common_Accept_Received_DiagP2PMessage_fromClient(msg, true).GetAwaiter().GetResult();

        //        //return res;
        //    }
        //    catch(Exception e)
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e, logprefix +
        //                    $"Exception occurred while handling diagnostic P2P message.");

        //        return -20;
        //    }
        //}

        ///// <summary>
        ///// Accepts incoming Diagnostic P2P messages from a websocket-connected client, via RMQ handler, or from a REST API endpoint.
        ///// Will treat message as initially received, and attempt immediate delivery.
        ///// Accepts an optional parameter that defines if the caller is RPC or async. This determines if the caller needs immediate response.
        ///// Returns the following:
        /////      1 - Message was successfully sent over the client's websocket.
        /////      0 - Client connection was found, but the message could not be sent over it.
        /////     -1 - Client connection could not be identified for message.
        /////     -2 - Message failed validation. Could not be sent.
        /////     -3 -  Exception occurred.
        ///// </summary>
        ///// <param name="menv"></param>
        ///// <param name="viarpc"></param>
        ///// <returns></returns>
        //public async Task<int> Common_Accept_Received_DiagP2PMessage_fromClient(DiagP2PEnvelopeDTO menv, bool viarpc = false)
        //{
        //    var logprefix = Create_LoggingPrefix();

        //    try
        //    {
        //        // New Diagnostic P2P message arrived from field.
        //        // We don't care what method brought it in; we process the same for all sources.

        //        // Validate the message...
        //        {
        //            // Determine if the message is null...
        //            if(menv == null)
        //            {
        //                // Cannot process

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                        $"Received null message envelope. Cannot process.");

        //                return -2;
        //            }
        //            if(menv.data == null)
        //            {
        //                // Cannot process

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                        $"Received null message. Cannot process.");

        //                return -2;
        //            }
        //            if(string.IsNullOrEmpty(menv.messageType))
        //            {
        //                // Cannot process

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                        $"Received null message type. Cannot process.");

        //                return -2;
        //            }
        //            // Check if we have a defined destination...
        //            if(menv.destination == null)
        //            {
        //                // Cannot process

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                        $"Received null destination. Cannot process.");

        //                return -2;
        //            }
        //        }


        //        // Split out the inner message...
        //        var msg = menv.data ?? "";
        //        var mtype = menv.messageType ?? "";

        //        // Get the messageId...
        //        var msgid = menv.msgId ?? "";

        //        // Get the destination...
        //        var dest = menv.destination;


        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(logprefix +
        //            $"Attempting to process received Diagnostic P2P message...\r\n" +
        //            $"MsgId = {msgid.ToString()}");


        //        // Perform any intermediate processing of the received diagnostic P2P message...
        //        this.Update_DiagP2PEnvelopeData(menv);

        //        // Attempt delivery of the given message...
        //        // NOTE: We leave the userid as null, since this type of message has only a deviceid.
        //        var ressent = await this._wsroutingclient.AttemptoSendMessage_toRecipient_viaWS<DiagP2PEnvelopeDTO, DiagP2PEnvelopeDTO>(menv,
        //                                    WSHostRouting_MessageCategories.CONST_MessageCategory_Outgoing_DiagP2P,
        //                                    this._queueclient.Forward_DiagP2PMessage_toWShost,
        //                                    null,
        //                                    dest.deviceId);
        //        if(ressent == 1)
        //        {
        //            // The message was sent to the recipient.
        //            // Since diagnostic P2P messages are realtime messages, we won't update any inflight queue for sent status.

        //            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(logprefix +
        //                $"Diagnostic P2P message was delivered to client.");

        //            return 1;
        //        }
        //        else if(ressent == -1)
        //        {
        //            // No connected client could be identified for the recipient userid.
        //            // We need to send a push to the current participant.

        //            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                $"Failed to identify connected client for given diagnostic P2P message. Nowhere to send.");

        //            return -1;
        //        }
        //        else
        //        {
        //            // An error occurred while attempting to send the message, via websocket.
        //            // We don't define a push functionality for this message type.
        //            // So, we will return an error.

        //            // If here, a routing exists, but we could not send our message with it.
        //            // We need to send a push to the current participant.

        //            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(logprefix +
        //                $"Failed to delivery diagnostic P2P message to client. Could not deliver.");

        //            return 0;
        //        }
        //    }
        //    catch(Exception e)
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e, logprefix +
        //            $"Exception ocurred while attempting to handle diagnostic P2P message to client.");

        //        return -3;
        //    }
        //}

        #endregion


        #region Private Methods

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
}
