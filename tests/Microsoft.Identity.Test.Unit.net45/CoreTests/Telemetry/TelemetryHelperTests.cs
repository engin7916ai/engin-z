﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Identity.Client.TelemetryCore.Internal.Events;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Test.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Test.Unit.CoreTests.Telemetry
{
    [TestClass]
    public class TelemetryHelperTests
    {
        private const string CorrelationId = "thetelemetrycorrelationid";
        private const string ClientId = "theclientid";
        private _TestEvent _trackingEvent;
        private TelemetryManager _telemetryManager;
        private _TestReceiver _testReceiver;

        [TestInitialize]
        public void Setup()
        {
            TestCommon.ResetInternalStaticCaches();
            _testReceiver = new _TestReceiver();
            var serviceBundle = TestCommon.CreateServiceBundleWithCustomHttpManager(null, clientId: ClientId);
            _telemetryManager = new TelemetryManager(serviceBundle.Config, serviceBundle.PlatformProxy, _testReceiver.HandleTelemetryEvents);
            _trackingEvent = new _TestEvent("tracking event", CorrelationId);
        }

        private class _TestReceiver : ITelemetryReceiver
        {
            public readonly List<Dictionary<string, string>> ReceivedEvents = new List<Dictionary<string, string>>();

            /// <inheritdoc />
            public void HandleTelemetryEvents(List<Dictionary<string, string>> events)
            {
                ReceivedEvents.AddRange(events);
            }
        }

        private class _TestEvent : EventBase
        {
            public _TestEvent(string eventName, string correlationId) : base(eventName, correlationId)
            {
            }
        }

        [TestMethod]
        public void TestTelemetryHelper()
        {
            using (_telemetryManager.CreateTelemetryHelper(_trackingEvent))
            {
            }

            ValidateResults(ClientId, false);
        }

        [TestMethod]
        public void TestTelemetryHelperWithFlush()
        {
            using (_telemetryManager.CreateTelemetryHelper(_trackingEvent))
            {
            }

            _telemetryManager.Flush(CorrelationId);

            ValidateResults(ClientId, true);
        }

        private void ValidateResults(
            string expectedClientId,
            bool shouldFlush)
        {
            if (shouldFlush)
            {
                Assert.AreEqual(2, _testReceiver.ReceivedEvents.Count);

                var first = _testReceiver.ReceivedEvents[0];
                Assert.AreEqual(13, first.Count);
                Assert.IsTrue(first.ContainsKey(EventBase.EventNameKey));
                Assert.AreEqual("msal.default_event", first[EventBase.EventNameKey]);
                Assert.AreEqual(expectedClientId, first["msal.client_id"]);

                var second = _testReceiver.ReceivedEvents[1];
                Assert.AreEqual(4, second.Count);
                Assert.IsTrue(second.ContainsKey(EventBase.EventNameKey));
                Assert.AreEqual("tracking event", second[EventBase.EventNameKey]);
            }
            else
            {
                Assert.AreEqual(0, _testReceiver.ReceivedEvents.Count);
            }
        }
    }
}
