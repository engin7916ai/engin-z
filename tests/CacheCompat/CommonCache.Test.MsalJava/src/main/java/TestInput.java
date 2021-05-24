// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. 

import com.google.gson.annotations.SerializedName;

public class TestInput {

    @SerializedName("Scope")
    String scope;

    @SerializedName("CacheFilePath")
    String cacheFilePath;

    @SerializedName("ResultsFilePath")
    String resultsFilePath;

    @SerializedName("LabUserDatas")
    LabUserData[] users;

    class LabUserData{
        @SerializedName("Upn")
        String upn;

        @SerializedName("Password")
        String password;

        @SerializedName("Authority")
        String authority;

        @SerializedName("ClientId")
        String clientId;
    }
}
