﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;

namespace Microsoft.Identity.Test.Unit.TelemetryTests
{
    internal class TestTelemetryDispatcher
    {
        public void DispatchEvent(ITelemetryEventPayload eventPayload)
        {
            string eventName = eventPayload.Name.ToLowerInvariant();
            if (eventName.Contains("error"))
            {
                ErrorEventCount++;
            }
            else if (eventName.Contains("scenario"))
            {
                ScenarioEventCount++;
            }
            else if (eventName.Contains("action"))
            {
                ActionEventCount++;
            }
        }

        public int ErrorEventCount { get; private set; }
        public int ScenarioEventCount { get; private set; }
        public int ActionEventCount { get; private set; }
        public int TotalEventCount => ErrorEventCount + ScenarioEventCount + ActionEventCount;

        public void FlushEvents()
        {
            ErrorEventCount = 0;
            ScenarioEventCount = 0;
            ActionEventCount = 0;
        }
    }
}
