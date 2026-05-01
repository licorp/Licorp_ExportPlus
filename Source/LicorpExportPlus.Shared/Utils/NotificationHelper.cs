using System;
using Licorp.Diagnostics;

namespace LicorpExportPlus.Utils
{
public static class NotificationHelper
{
public static void LogInfo(string message)
{
LicorpTrace.Info(message);
}

public static void LogWarning(string message)
{
LicorpTrace.Warn(message);
}

public static void LogError(string message)
{
LicorpTrace.Error(message);
}

public static void LogError(string message, Exception ex)
{
LicorpTrace.Error(message, ex);
}

public static void ShowNotification(string title, string message, NotificationType type = NotificationType.Information)
{
try
{
switch (type)
{
case NotificationType.Information:
LogInfo($"{title}: {message}");
break;
case NotificationType.Warning:
LogWarning($"{title}: {message}");
break;
case NotificationType.Error:
LogError($"{title}: {message}");
break;
}

ShowWindowsNotification(title, message, type);
}
catch (Exception ex)
{
LogError("Failed to show notification", ex);
}
}

private static void ShowWindowsNotification(string title, string message, NotificationType type)
{
try
{
Console.WriteLine($"[{type}] {title}: {message}");
}
catch
{
}
}

public static void ClearOldLogs(int daysToKeep = 30)
{
}

public static void OpenLogFolder()
{
try
{
var logFolder = System.IO.Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
"ExportPlusAddin", "Logs");
if (System.IO.Directory.Exists(logFolder))
{
System.Diagnostics.Process.Start("explorer.exe", logFolder);
}
}
catch (Exception ex)
{
LogError("Could not open log folder", ex);
}
}

public static void SendEmailNotification(string toEmail, string subject, string body, bool isHtml = false)
{
try
{
LogInfo($"Email notification would be sent to: {toEmail}");
LogInfo($"Subject: {subject}");
LogInfo($"Body: {body}");
}
catch (Exception ex)
{
LogError("Failed to send email notification", ex);
}
}

public static string GetLogFilePath()
{
return "";
}

public static void ExportLogToFile(string destinationPath)
{
}
}

public enum NotificationType
{
Information,
Warning,
Error,
Success
}
}
