﻿# WeatherBot Sample with Semantic Kernel

This is a sample of a simple Weather Forecast Agent that is hosted on an Asp.net core web service.  This Agent is configured to accept a request asking for information about a wheather forecast and respond to the caller with an Adaptive Card.

This Agent Sample is intended to introduce you the basics of integrating Semantic Kernel with the Microsoft 365 Agents SDK in order to build powerful Agents. It can also be used as a the base for a custom Agent that you choose to develop.

***Note:*** This sample requires JSON output from the model which works best from newer versions of the model such as gpt-4o-mini.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases) for Testing Web Chat.

## Running this sample

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure [Entra ID Application Identities](https://aka.ms/AgentsSDK-CreateBot)
    - Create and configure an [Azure Bot Service](https://aka.ms/azuredeployment) for your bot
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your bot on to.
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

    1. Configure your AI service settings. The sample provides configuration placeholders for using Azure OpenAI or OpenAI, but others can be used as well.
    1. With Azure OpenAI:
        1. With Crendtial Free (Keyless):
        
           This is a secure way to authenticate to Azure resources without needing to store credentials in your code. Your Azure user account is assigned the "Cognitive Services OpenAI User" role, which allows you to access the OpenAI resource.
           Follow this guide [Role-based access control for Azure resources](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/role-based-access-control) to assign the "Cognitive Services OpenAI User" role to your Azure user account and Managed Identities.    
           
           Then you just need to configure Azure OpenAI Endpoint and DeploymentName in the appsettings.json file
        
        1. With dotnet user-secrets (for running locally)
            1. From a terminal or command prompt, navigate to the root of the sample project.
            1. Run the following commands to set the Azure OpenAI settings:
				```bash
				dotnet user-secrets set "AIServices:AzureOpenAI:ApiKey" "<YOUR_AZURE_OPENAI_API_KEY>"
                dotnet user-secrets set "AIServices:AzureOpenAI:Endpoint" "<YOUR_AZURE_OPENAI_ENDPOINT>"
                dotnet user-secrets set "AIServices:AzureOpenAI:DeploymentName" "<YOUR_AZURE_OPENAI_DEPLOYMENT_NAME>"
                dotnet user-secrets set "AIServices:UseAzureOpenAI" true
                ```
        1. With environment variables (for deployment)
            1. Set the following environment variables:
                1. `AIServices__AzureOpenAI__ApiKey` - Your Azure OpenAI API key
                1. `AIServices__AzureOpenAI__Endpoint` - Your Azure OpenAI endpoint
                1. `AIServices__AzureOpenAI__DeploymentName` - Your Azure OpenAI deployment name
                1. `AIServices__UseAzureOpenAI` - `true`
    1. With OpenAI:
		1. With dotnet user-secrets (for running locally)
			1. From a terminal or command prompt, navigate to the root of the sample project.
			1. Run the following commands to set the OpenAI settings:
               ```bash
				dotnet user-secrets set "AIServices:OpenAI:ModelId" "<YOUR_OPENAI_MODEL_ID_>"
                dotnet user-secrets set "AIServices:OpenAI:ApiKey" "<YOUR_OPENAI_API_KEY_>"
                dotnet user-secrets set "AIServices:UseAzureOpenAI" false
                ```
        1. With environment variables (for deployment)
            1. Set the following environment variables:
                1. `AIServices__OpenAI__ModelId` - Your OpenAI model ID
                1. `AIServices__OpenAI__ApiKey` - Your OpenAI API key
                1. `AIServices__UseAzureOpenAI` - `false`

## Getting Started with WeatherBot Sample

Read more about [Running an Agent](../../../docs/HowTo/running-an-agent.md)

### QuickStart using Bot Framework Emulator

1. Open the WeatherBot Sample in Visual Studio 2022
1. Run it in Debug Mode (F5)
1. A blank web page will open, note down the URL which should be similar too `http://localhost:65349/`
1. Open the [BotFramework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases)
    1. Click **Open Bot**
    1. In the bot URL field input the URL you noted down from the web page and add /api/messages to it. It should appear similar to `http://localhost:65349/api/messages`
    1. Click **Connect**

If all is working correctly, the Bot Emulator should show you a Web Chat experience with the words **"Hello and Welcome! I'm here to help with all your weather forecast needs!"**

If you type a message and hit enter, or the send arrow, you should receive a message asking for more information, or with a weather forecast card.

### QuickStart using WebChat

1. Create an Azure Bot
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Audiences": [
          "00000000-0000-0000-0000-000000000000" // this is the Client ID used for the Azure Bot
        ]
      },

      "Connections": {
          "BotServiceConnection": {
          "Assembly": "Microsoft.Agents.Authentication.Msal",
          "Type":  "MsalAuth",
          "Settings": {
              "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
              "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
              "ClientId": "00000000-0000-0000-0000-000000000000", // this is the Client ID used for the connection.
              "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
              "Scopes": [
                "https://api.botframework.com/.default"
              ],
              "TenantId": "{{TenantId}}" // This is the Tenant ID used for the Connection. 
          }
      }
      ```

      1. Set the **ClientId** to the AppId of the bot identity.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      1. Set the **TenantId** to the Tenant Id where your application is registered.
      1. Set the **Audience** to the AppId of the bot identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent in Visual Studio

1. Select **Test in WebChat** on the Azure Bot

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.