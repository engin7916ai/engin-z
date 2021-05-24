﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Identity.Test.LabInfrastructure
{
    public interface ILabService
    {
        LabResponse GetLabResponse(UserQuery query);
    }
}
