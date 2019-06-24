---
title: Troubleshoot ASP.NET Core on Azure App Service
author: guardrex
description: Learn how to diagnose problems with ASP.NET Core Azure App Service deployments.
ms.author: riande
ms.custom: mvc
ms.date: 03/06/2019
uid: host-and-deploy/azure-apps/troubleshoot
---
# Troubleshoot ASP.NET Core on Azure App Service

By [Luke Latham](https://github.com/guardrex)

[!INCLUDE [Azure App Service Preview Notice](../../includes/azure-apps-preview-notice.md)]

This article provides instructions on how to diagnose an ASP.NET Core app startup issue using Azure App Service's diagnostic tools. For additional troubleshooting advice, see [Azure App Service diagnostics overview](/azure/app-service/app-service-diagnostics) and [How to: Monitor Apps in Azure App Service](/azure/app-service/web-sites-monitor) in the Azure documentation.

## App startup errors

**502.5 Process Failure**
The worker process fails. The app doesn't start.

The [ASP.NET Core Module](xref:host-and-deploy/aspnet-core-module) attempts to start the worker process but it fails to start. Examining the Application Event Log often helps troubleshoot this type of problem. Accessing the log is explained in the [Application Event Log](#application-event-log) section.

The *502.5 Process Failure* error page is returned when a misconfigured app causes the worker process to fail:

![Browser window showing the 502.5 Process Failure page](troubleshoot/_static/process-failure-page.png)

**500 Internal Server Error**

The app starts, but an error prevents the server from fulfilling the request.

This error occurs within the app's code during startup or while creating a response. The response may contain no content, or the response may appear as a *500 Internal Server Error* in the browser. The Application Event Log usually states that the app started normally. From the server's perspective, that's correct. The app did start, but it can't generate a valid response. [Run the app in the Kudu console](#run-the-app-in-the-kudu-console) or [enable the ASP.NET Core Module stdout log](#aspnet-core-module-stdout-log) to troubleshoot the problem.

::: moniker range="= aspnetcore-2.2"

### 500.30 In-Process Startup Failure

The worker process fails. The app doesn't start.

The ASP.NET Core Module attempts to start the .NET Core CLR in-process, but it fails to start. The cause of a process startup failure is usually determined from entries in the [Application Event Log](#application-event-log) and the [ASP.NET Core Module stdout log](#aspnet-core-module-stdout-log).

::: moniker-end

::: moniker range=">= aspnetcore-3.0"

### 500.31 ANCM Failed to Find Native Dependencies

The worker process fails. The app doesn't start.

The ASP.NET Core Module attempts to start the .NET Core runtime in-process, but it fails to start. The most common cause of this startup failure is when the `Microsoft.NETCore.App` or `Microsoft.AspNetCore.App` runtime isn't installed. If the app is deployed to target ASP.NET Core 3.0 and that version doesn't exist on the machine, this error occurs. An example error message follows:

```
The specified framework 'Microsoft.NETCore.App', version '3.0.0' was not found.
  - The following frameworks were found:
      2.2.1 at [C:\Program Files\dotnet\x64\shared\Microsoft.NETCore.App]
      3.0.0-preview5-27626-15 at [C:\Program Files\dotnet\x64\shared\Microsoft.NETCore.App]
      3.0.0-preview6-27713-13 at [C:\Program Files\dotnet\x64\shared\Microsoft.NETCore.App]
      3.0.0-preview6-27714-15 at [C:\Program Files\dotnet\x64\shared\Microsoft.NETCore.App]
      3.0.0-preview6-27723-08 at [C:\Program Files\dotnet\x64\shared\Microsoft.NETCore.App]
```

The error message lists all the installed .NET Core versions and the version requested by the app. To fix this error, either:

* Install the appropriate version of .NET Core on the machine.
* Change the app to target a version of .NET Core that's present on the machine.
* Publish the app as a [self-contained deployment](/dotnet/core/deploying/#self-contained-deployments-scd).

When running in development (the `ASPNETCORE_ENVIRONMENT` environment variable is set to `Development`), the specific error is written to the HTTP response. The cause of a process startup failure is also found in the [Application Event Log](#application-event-log).

### 500.32 ANCM Failed to Load dll

The worker process fails. The app doesn't start.

The most common cause for this error is that the app is published for an incompatible processor architecture. If the worker process is running as a 32-bit app and the app was published to target 64-bit, this error occurs.

To fix this error, either:

* Republish the app for the same processor architecture as the worker process.
* Publish the app as a [framework-dependent deployment](/dotnet/core/deploying/#framework-dependent-executables-fde).

### 500.33 ANCM Request Handler Load Failure

The worker process fails. The app doesn't start.

The app didn't reference the `Microsoft.AspNetCore.App` framework. Only apps targeting the `Microsoft.AspNetCore.App` framework can be hosted by the ASP.NET Core Module.

To fix this error, confirm that the app is targeting the `Microsoft.AspNetCore.App` framework. Check the `.runtimeconfig.json` to verify the framework targeted by the app.

### 500.34 ANCM Mixed Hosting Models Not Supported

The worker process can't run both an in-process app and an out-of-process app in the same process.

To fix this error, run apps in separate IIS application pools.

### 500.35 ANCM Multiple In-Process Applications in same Process

The worker process can't run both an in-process app and an out-of-process app in the same process.

To fix this error, run apps in separate IIS application pools.

### 500.36 ANCM Out-Of-Process Handler Load Failure

The out-of-process request handler, *aspnetcorev2_outofprocess.dll*, isn't next to the *aspnetcorev2.dll* file. This indicates a corrupted installation of the ASP.NET Core Module.

To fix this error, repair the installation of the [.NET Core Hosting Bundle](xref:host-and-deploy/iis/index#install-the-net-core-hosting-bundle) (for IIS) or Visual Studio (for IIS Express).

### 500.37 ANCM Failed to Start Within Startup Time Limit

ANCM failed to start within the provied startup time limit. By default, the timeout is 120 seconds.

This error can occur when starting a large number of apps on the same machine. Check for CPU/Memory usage spikes on the server during startup. You may need to stagger the startup process of multiple apps.

### 500.30 In-Process Startup Failure

The worker process fails. The app doesn't start.

The ASP.NET Core Module attempts to start the .NET Core runtime in-process, but it fails to start. The cause of a process startup failure is usually determined from entries in the [Application Event Log](#application-event-log) and the [ASP.NET Core Module stdout log](#aspnet-core-module-stdout-log).

### 500.0 In-Process Handler Load Failure

The worker process fails. The app doesn't start.

The cause of a process startup failure is also found in the [Application Event Log](#application-event-log).

::: moniker-end

**Connection reset**

If an error occurs after the headers are sent, it's too late for the server to send a **500 Internal Server Error** when an error occurs. This often happens when an error occurs during the serialization of complex objects for a response. This type of error appears as a *connection reset* error on the client. [Application logging](xref:fundamentals/logging/index) can help troubleshoot these types of errors.

## Default startup limits

The ASP.NET Core Module is configured with a default *startupTimeLimit* of 120 seconds. When left at the default value, an app may take up to two minutes to start before the module logs a process failure. For information on configuring the module, see [Attributes of the aspNetCore element](xref:host-and-deploy/aspnet-core-module#attributes-of-the-aspnetcore-element).

## Troubleshoot app startup errors

### Application Event Log

To access the Application Event Log, use the **Diagnose and solve problems** blade in the Azure portal:

1. In the Azure portal, open the app in **App Services**.
1. Select **Diagnose and solve problems**.
1. Select the **Diagnostic Tools** heading.
1. Under **Support Tools**, select the **Application Events** button.
1. Examine the latest error provided by the *IIS AspNetCoreModule* or *IIS AspNetCoreModule V2* entry in the **Source** column.

An alternative to using the **Diagnose and solve problems** blade is to examine the Application Event Log file directly using [Kudu](https://github.com/projectkudu/kudu/wiki):

1. Open **Advanced Tools** in the **Development Tools** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.
1. Open the **LogFiles** folder.
1. Select the pencil icon next to the *eventlog.xml* file.
1. Examine the log. Scroll to the bottom of the log to see the most recent events.

### Run the app in the Kudu console

Many startup errors don't produce useful information in the Application Event Log. You can run the app in the [Kudu](https://github.com/projectkudu/kudu/wiki) Remote Execution Console to discover the error:

1. Open **Advanced Tools** in the **Development Tools** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.

#### Test a 32-bit (x86) app

##### Current release

1. `cd d:\home\site\wwwroot`
1. Run the app:
   * If the app is a [framework-dependent deployment](/dotnet/core/deploying/#framework-dependent-deployments-fdd):

     ```console
     dotnet .\{ASSEMBLY NAME}.dll
     ```

   * If the app is a [self-contained deployment](/dotnet/core/deploying/#self-contained-deployments-scd):

     ```console
     {ASSEMBLY NAME}.exe
     ```

The console output from the app, showing any errors, is piped to the Kudu console.

##### Framework-dependent deployment running on a preview release

*Requires installing the ASP.NET Core {VERSION} (x86) Runtime site extension.*

1. `cd D:\home\SiteExtensions\AspNetCoreRuntime.{X.Y}.x32` (`{X.Y}` is the runtime version)
1. Run the app: `dotnet \home\site\wwwroot\{ASSEMBLY NAME}.dll`

The console output from the app, showing any errors, is piped to the Kudu console.

#### Test a 64-bit (x64) app

##### Current release

* If the app is a 64-bit (x64) [framework-dependent deployment](/dotnet/core/deploying/#framework-dependent-deployments-fdd):
  1. `cd D:\Program Files\dotnet`
  1. Run the app: `dotnet \home\site\wwwroot\{ASSEMBLY NAME}.dll`
* If the app is a [self-contained deployment](/dotnet/core/deploying/#self-contained-deployments-scd):
  1. `cd D:\home\site\wwwroot`
  1. Run the app: `{ASSEMBLY NAME}.exe`

The console output from the app, showing any errors, is piped to the Kudu console.

##### Framework-dependent deployment running on a preview release

*Requires installing the ASP.NET Core {VERSION} (x64) Runtime site extension.*

1. `cd D:\home\SiteExtensions\AspNetCoreRuntime.{X.Y}.x64` (`{X.Y}` is the runtime version)
1. Run the app: `dotnet \home\site\wwwroot\{ASSEMBLY NAME}.dll`

The console output from the app, showing any errors, is piped to the Kudu console.

### ASP.NET Core Module stdout log

The ASP.NET Core Module stdout log often records useful error messages not found in the Application Event Log. To enable and view stdout logs:

1. Navigate to the **Diagnose and solve problems** blade in the Azure portal.
1. Under **SELECT PROBLEM CATEGORY**, select the **Web App Down** button.
1. Under **Suggested Solutions** > **Enable Stdout Log Redirection**, select the button to **Open Kudu Console to edit Web.Config**.
1. In the Kudu **Diagnostic Console**, open the folders to the path **site** > **wwwroot**. Scroll down to reveal the *web.config* file at the bottom of the list.
1. Click the pencil icon next to the *web.config* file.
1. Set **stdoutLogEnabled** to `true` and change the **stdoutLogFile** path to: `\\?\%home%\LogFiles\stdout`.
1. Select **Save** to save the updated *web.config* file.
1. Make a request to the app.
1. Return to the Azure portal. Select the **Advanced Tools** blade in the **DEVELOPMENT TOOLS** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.
1. Select the **LogFiles** folder.
1. Inspect the **Modified** column and select the pencil icon to edit the stdout log with the latest modification date.
1. When the log file opens, the error is displayed.

Disable stdout logging when troubleshooting is complete:

1. In the Kudu **Diagnostic Console**, return to the path **site** > **wwwroot** to reveal the *web.config* file. Open the **web.config** file again by selecting the pencil icon.
1. Set **stdoutLogEnabled** to `false`.
1. Select **Save** to save the file.

> [!WARNING]
> Failure to disable the stdout log can lead to app or server failure. There's no limit on log file size or the number of log files created. Only use stdout logging to troubleshoot app startup problems.
>
> For general logging in an ASP.NET Core app after startup, use a logging library that limits log file size and rotates logs. For more information, see [third-party logging providers](xref:fundamentals/logging/index#third-party-logging-providers).

::: moniker range=">= aspnetcore-2.2"

### ASP.NET Core Module debug log

The ASP.NET Core Module debug log provides additional, deeper logging from the ASP.NET Core Module. To enable and view stdout logs:

1. To enable the enhanced diagnostic log, perform either of the following:
   * Follow the instructions in [Enhanced diagnostic logs](xref:host-and-deploy/aspnet-core-module#enhanced-diagnostic-logs) to configure the app for an enhanced diagnostic logging. Redeploy the app.
   * Add the `<handlerSettings>` shown in [Enhanced diagnostic logs](xref:host-and-deploy/aspnet-core-module#enhanced-diagnostic-logs) to the live app's *web.config* file using the Kudu console:
     1. Open **Advanced Tools** in the **Development Tools** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
     1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.
     1. Open the folders to the path **site** > **wwwroot**. Edit the *web.config* file by selecting the pencil button. Add the `<handlerSettings>` section as shown in [Enhanced diagnostic logs](xref:host-and-deploy/aspnet-core-module#enhanced-diagnostic-logs). Select the **Save** button.
1. Open **Advanced Tools** in the **Development Tools** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.
1. Open the folders to the path **site** > **wwwroot**. If you didn't supply a path for the *aspnetcore-debug.log* file, the file appears in the list. If you supplied a path, navigate to the location of the log file.
1. Open the log file with the pencil button next to the file name.

Disable debug logging when troubleshooting is complete:

1. To disable the enhanced debug log, perform either of the following:
   * Remove the `<handlerSettings>` from the *web.config* file locally and redeploy the app.
   * Use the Kudu console to edit the *web.config* file and remove the `<handlerSettings>` section. Save the file.

> [!WARNING]
> Failure to disable the debug log can lead to app or server failure. There's no limit on log file size. Only use debug logging to troubleshoot app startup problems.
>
> For general logging in an ASP.NET Core app after startup, use a logging library that limits log file size and rotates logs. For more information, see [third-party logging providers](xref:fundamentals/logging/index#third-party-logging-providers).

::: moniker-end

## Common startup errors

See <xref:host-and-deploy/azure-iis-errors-reference>. Most of the common problems that prevent app startup are covered in the reference topic.

## Slow or hanging app

When an app responds slowly or hangs on a request, see the following articles:

* [Troubleshoot slow web app performance issues in Azure App Service](/azure/app-service/app-service-web-troubleshoot-performance-degradation)
* [Use Crash Diagnoser Site Extension to Capture Dump for Intermittent Exception issues or performance issues on Azure Web App](https://blogs.msdn.microsoft.com/asiatech/2015/12/28/use-crash-diagnoser-site-extension-to-capture-dump-for-intermittent-exception-issues-or-performance-issues-on-azure-web-app/)

## Remote debugging

See the following topics:

* [Remote debugging web apps section of Troubleshoot a web app in Azure App Service using Visual Studio](/azure/app-service/web-sites-dotnet-troubleshoot-visual-studio#remotedebug) (Azure documentation)
* [Remote Debug ASP.NET Core on IIS in Azure in Visual Studio 2017](/visualstudio/debugger/remote-debugging-azure) (Visual Studio documentation)

## Application Insights

[Application Insights](https://azure.microsoft.com/services/application-insights/) provides telemetry from apps hosted in the Azure App Service, including error logging and reporting features. Application Insights can only report on errors that occur after the app starts when the app's logging features become available. For more information, see [Application Insights for ASP.NET Core](/azure/application-insights/app-insights-asp-net-core).

## Monitoring blades

Monitoring blades provide an alternative troubleshooting experience to the methods described earlier in the topic. These blades can be used to diagnose 500-series errors.

Confirm that the ASP.NET Core Extensions are installed. If the extensions aren't installed, install them manually:

1. In the **DEVELOPMENT TOOLS** blade section, select the **Extensions** blade.
1. The **ASP.NET Core Extensions** should appear in the list.
1. If the extensions aren't installed, select the **Add** button.
1. Choose the **ASP.NET Core Extensions** from the list.
1. Select **OK** to accept the legal terms.
1. Select **OK** on the **Add extension** blade.
1. An informational pop-up message indicates when the extensions are successfully installed.

If stdout logging isn't enabled, follow these steps:

1. In the Azure portal, select the **Advanced Tools** blade in the **DEVELOPMENT TOOLS** area. Select the **Go&rarr;** button. The Kudu console opens in a new browser tab or window.
1. Using the navigation bar at the top of the page, open **Debug console** and select **CMD**.
1. Open the folders to the path **site** > **wwwroot** and scroll down to reveal the *web.config* file at the bottom of the list.
1. Click the pencil icon next to the *web.config* file.
1. Set **stdoutLogEnabled** to `true` and change the **stdoutLogFile** path to: `\\?\%home%\LogFiles\stdout`.
1. Select **Save** to save the updated *web.config* file.

Proceed to activate diagnostic logging:

1. In the Azure portal, select the **Diagnostics logs** blade.
1. Select the **On** switch for **Application Logging (Filesystem)** and **Detailed error messages**. Select the **Save** button at the top of the blade.
1. To include failed request tracing, also known as Failed Request Event Buffering (FREB) logging, select the **On** switch for **Failed request tracing**.
1. Select the **Log stream** blade, which is listed immediately under the **Diagnostics logs** blade in the portal.
1. Make a request to the app.
1. Within the log stream data, the cause of the error is indicated.

Be sure to disable stdout logging when troubleshooting is complete. See the instructions in the [ASP.NET Core Module stdout log](#aspnet-core-module-stdout-log) section.

To view the failed request tracing logs (FREB logs):

1. Navigate to the **Diagnose and solve problems** blade in the Azure portal.
1. Select **Failed Request Tracing Logs** from the **SUPPORT TOOLS** area of the sidebar.

See [Failed request traces section of the Enable diagnostics logging for web apps in Azure App Service topic](/azure/app-service/web-sites-enable-diagnostic-log#failed-request-traces) and the [Application performance FAQs for Web Apps in Azure: How do I turn on failed request tracing?](/azure/app-service/app-service-web-availability-performance-application-issues-faq#how-do-i-turn-on-failed-request-tracing) for more information.

For more information, see [Enable diagnostics logging for web apps in Azure App Service](/azure/app-service/web-sites-enable-diagnostic-log).

> [!WARNING]
> Failure to disable the stdout log can lead to app or server failure. There's no limit on log file size or the number of log files created.
>
> For routine logging in an ASP.NET Core app, use a logging library that limits log file size and rotates logs. For more information, see [third-party logging providers](xref:fundamentals/logging/index#third-party-logging-providers).

## Additional resources

* <xref:fundamentals/error-handling>
* <xref:host-and-deploy/azure-iis-errors-reference>
* [Troubleshoot a web app in Azure App Service using Visual Studio](/azure/app-service/web-sites-dotnet-troubleshoot-visual-studio)
* [Troubleshoot HTTP errors of "502 bad gateway" and "503 service unavailable" in your Azure web apps](/azure/app-service/app-service-web-troubleshoot-http-502-http-503)
* [Troubleshoot slow web app performance issues in Azure App Service](/azure/app-service/app-service-web-troubleshoot-performance-degradation)
* [Application performance FAQs for Web Apps in Azure](/azure/app-service/app-service-web-availability-performance-application-issues-faq)
* [Azure Web App sandbox (App Service runtime execution limitations)](https://github.com/projectkudu/kudu/wiki/Azure-Web-App-sandbox)
* [Azure Friday: Azure App Service Diagnostic and Troubleshooting Experience (12-minute video)](https://channel9.msdn.com/Shows/Azure-Friday/Azure-App-Service-Diagnostic-and-Troubleshooting-Experience)