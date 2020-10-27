﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Client;
using OpcPublisher.Configurations;
using System;
using System.Globalization;
using System.Threading;

namespace OpcPublisher
{
    public class HeartBeatPublishing
    {
        public HeartBeatPublishing(uint heartbeatInterval, Session session, NodeId nodeId)
        {
            // setup heartbeat processing
            if (heartbeatInterval > 0)
            {
                // setup the heartbeat timer
                Tuple<Session, NodeId> tuple = new Tuple<Session, NodeId>(session, nodeId);
                _timer = new Timer(HeartbeatSend, tuple, heartbeatInterval * 1000, heartbeatInterval * 1000);
                Program.Instance.Logger.Debug($"Setting up {heartbeatInterval} sec heartbeat for node '{nodeId}'.");
            }
        }

        /// <summary>
        /// Timer callback for heartbeat telemetry send.
        /// </summary>
        static void HeartbeatSend(object state)
        {
            Tuple<Session, NodeId> tuple = (Tuple<Session, NodeId>)state;
            Session session = tuple.Item1;
            NodeId nodeId = tuple.Item2;

            MessageDataModel messageData = new MessageDataModel();

            try
            {
                DataValue value = session.ReadValue(nodeId);

                messageData.EndpointUrl = session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
                messageData.NodeId = nodeId.ToString();
                messageData.ApplicationUri = session.Endpoint.Server.ApplicationUri + (string.IsNullOrEmpty(SettingsConfiguration.PublisherSite) ? "" : $":{SettingsConfiguration.PublisherSite}");

                // use the SourceTimestamp as reported in the notification event argument in ISO8601 format
                messageData.SourceTimestamp = value.SourceTimestamp.ToString("o", CultureInfo.InvariantCulture);

                // use the StatusCode as reported in the notification event argument
                messageData.StatusCode = value.StatusCode.Code;

                // use the StatusCode as reported in the notification event argument to lookup the symbolic name
                messageData.Status = StatusCode.LookupSymbolicId(value.StatusCode.Code);

                messageData.Value = value.Value.ToString();

                // enqueue the message
                HubClientWrapper.Enqueue(messageData);
                Program.Instance.Logger.Debug($"Message enqueued for heartbeat with sourceTimestamp '{messageData.SourceTimestamp}'.");
            }
            catch (Exception ex)
            {
                Program.Instance.Logger.Error($"Message for heartbeat with sourceTimestamp '{messageData.SourceTimestamp} failed with {ex.Message}'.");
            }
        }

        private Timer _timer;
    }
}
