using System.Threading.Tasks;
using Bluscream;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Notifications")]
[ModuleDescription("Send notifications to Desktop, XSOverlay, and OVRToolkit")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class NotificationsModule : Module
{
    public const string LOGO_BASE64 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAaRElEQVR4nO3dCXQUVboH8H8SkbDJOizBJ5LIjCyGgcgWQOABGQEhiMMSBESFAZEZweMbeA/0RIURZ56jjCNbVIRhxhCQJQMSCI6ggEBE0ACOkV0IjwCRRcIWUu98dao5EdNdt9PVnaqu/++cPgh2V9W91ffrqrr3fhdERERERERERERERERERERERERERERERERERERERGRXERYe1x0AugJoD6AFgKYA6hv/HgVA47eAyJS0yRsALgAoAHAYwNcAdgD41Ph3ywQaACIBDAQwGkBvANE8v0RBcwVANoBFAFYCKKmoqr4NwGQAp4xfdr744iu0L2l7zwKoFEhDLs8VwK8BzAVQL5AdE5ElzgCYAGBZeTbmTwCoBeB9AA/yvBHZThaAFADn/Dkw1QDQEcA6IwgQkT1J4+8DYLvq0UUqvGcogM/Y+Ilsr5bRVoepHqjZFcAo44ljuVWpUgXx8fFo1qwZ6tati2rVqqFy5crQNPYKEkVERODq1au4dOkSzp49i7y8POTm5uLy5cuB1s1olbbrKwD0B5BZnj3XqVMHw4YNw4ABA9C5c2dUr169PJshcqWLFy9i27ZtWL16NdLT0/H999+XtxqSzdqwtwDQCkCOv/36jRs3RmpqKh5//HFERUX5daRE9FM3btzAwoUL9XZ14sQJf2voKoB2AHK9vcFbAJDGf78/e3rppZfw/PPP+3uARKTo5ZdfxgsvvOBvde3y1ZbLCgCzAExR3XrTpk2xbt06/OIXv+B5JAqyb775Bn369MHhw4f92dGrAKaW9T9uDQAJAD5X3WpSUhLWrFmDSpUCGoxERH64fv06+vXrh+zsbH8+1q6stn1rN+D/qG4tOTkZ69evZ+MnCjFpcxs2bNAfsvuhzLZd+gqgo9GHaCoxMRFbt27leSeqYNLLJj0GihJvbeOlrwDGqGyjQYMGWLasXMOOichiGRkZeptU9OStb/MEABlBNFhlG2lpaYiJieF5JLIB6XpfsGCB6oEMAVC79D94AsAAI3GHT4888gj69+/P805kI/IsYNCgQSoHVMNo6zd5AkAXlU9PmzaN553IhqZPn656UD9q65FG199/mn1Khva2adOG557IhqRtDh06VOXAepYeGCQBIBbAXWafGjVqFM87kY0pttH/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";
    public static readonly byte[] LOGO_BYTES = Convert.FromBase64String(NotificationsModule.LOGO_BASE64);
    private int _notificationsSent = 0;

    // Enums for type-safe settings/parameters/etc
    private enum NotificationsSetting
    {
        DefaultTitle,
        DefaultMessage,
        DefaultTimeout,
        DefaultOpacity,
        EnableDesktop,
        EnableXSOverlay,
        EnableOVRToolkit,
        EnableWebhook,
        WebhookUrl,
        WebhookMethod,
        LogNotifications
    }

    private enum NotificationsParameter
    {
        NotificationSent,
        NotificationFailed,
        NotificationCount
    }

    private enum NotificationsVariable
    {
        LastTitle,
        LastMessage,
        NotificationCount,
        LastTarget
    }

    private enum NotificationsState
    {
        Idle,
        Sending
    }

    private enum NotificationsEvent
    {
        OnNotificationSent,
        OnNotificationFailed
    }

    private enum WebhookMethod
    {
        GET,
        POST,
        PUT,
        PATCH,
        DELETE
    }

    protected override void OnPreLoad()
    {
        // Default settings
        CreateTextBox(NotificationsSetting.DefaultTitle, "Title", "Default notification title (used if input is empty)", "VRCOSC");
        CreateTextBox(NotificationsSetting.DefaultMessage, "Message", "Default notification message (used if input is empty)", "");
        CreateSlider(NotificationsSetting.DefaultTimeout, "Timeout (ms)", "Default notification display duration in milliseconds", 5000, 1000, 30000, 1000);
        CreateSlider(NotificationsSetting.DefaultOpacity, "Opacity (%)", "Default notification opacity percentage (0-100)", 100, 0, 95, 5);
        
        // Enable/disable specific notification types
        CreateToggle(NotificationsSetting.EnableDesktop, "Enable Desktop Notifications", "Show Windows desktop notifications", true);
        CreateToggle(NotificationsSetting.EnableXSOverlay, "Enable XSOverlay Notifications", "Send notifications to XSOverlay", false);
        CreateToggle(NotificationsSetting.EnableOVRToolkit, "Enable OVRToolkit Notifications", "Send notifications to OVRToolkit", false);
        CreateToggle(NotificationsSetting.EnableWebhook, "Enable Webhook Notifications", "Send notifications to webhook URL", false);
        
        // Webhook settings
        CreateTextBox(NotificationsSetting.WebhookUrl, "Webhook URL", "HTTP(S) URL to send notifications to", string.Empty);
        CreateDropdown(NotificationsSetting.WebhookMethod, "Webhook Method", "HTTP method for webhook requests", WebhookMethod.POST);
        
        // Debug
        CreateToggle(NotificationsSetting.LogNotifications, "Log Notifications", "Log all notification sends to console", false);

        // OSC Parameters
        RegisterParameter<bool>(NotificationsParameter.NotificationSent, "VRCOSC/Notifications/Sent", ParameterMode.Write, "Sent", "True for 1 second when notification is sent");
        RegisterParameter<bool>(NotificationsParameter.NotificationFailed, "VRCOSC/Notifications/Failed", ParameterMode.Write, "Failed", "True for 1 second when notification fails");
        RegisterParameter<int>(NotificationsParameter.NotificationCount, "VRCOSC/Notifications/Count", ParameterMode.Write, "Count", "Total number of notifications sent");

        // Groups
        CreateGroup("Defaults", "Default notification settings", NotificationsSetting.DefaultTitle, NotificationsSetting.DefaultMessage, NotificationsSetting.DefaultTimeout, NotificationsSetting.DefaultOpacity);
        CreateGroup("Targets", "Enable/disable notification targets", NotificationsSetting.EnableDesktop, NotificationsSetting.EnableXSOverlay, NotificationsSetting.EnableOVRToolkit, NotificationsSetting.EnableWebhook);
        CreateGroup("Webhook", "Webhook configuration", NotificationsSetting.WebhookUrl, NotificationsSetting.WebhookMethod);
        CreateGroup("Debug", "Debug settings", NotificationsSetting.LogNotifications);
    }

    protected override void OnPostLoad()
    {
        // ChatBox Variables
        CreateVariable<string>(NotificationsVariable.LastTitle, "Last Title");
        CreateVariable<string>(NotificationsVariable.LastMessage, "Last Message");
        CreateVariable<int>(NotificationsVariable.NotificationCount, "Notification Count");
        CreateVariable<string>(NotificationsVariable.LastTarget, "Last Target");

        // ChatBox States
        CreateState(NotificationsState.Idle, "Idle");
        CreateState(NotificationsState.Sending, "Sending");

        // ChatBox Events
        CreateEvent(NotificationsEvent.OnNotificationSent, "On Notification Sent");
        CreateEvent(NotificationsEvent.OnNotificationFailed, "On Notification Failed");
    }

    protected override Task<bool> OnModuleStart()
    {
        // Initialize ChatBox
        SetVariableValue(NotificationsVariable.LastTitle, "None");
        SetVariableValue(NotificationsVariable.LastMessage, "None");
        SetVariableValue(NotificationsVariable.NotificationCount, 0);
        SetVariableValue(NotificationsVariable.LastTarget, "None");
        ChangeState(NotificationsState.Idle);
        
        return Task.FromResult(true);
    }

    public void UpdateNotificationStatus(string title, string message, string target, bool success)
    {
        if (success)
        {
            _notificationsSent++;
            SetVariableValue(NotificationsVariable.LastTitle, title);
            SetVariableValue(NotificationsVariable.LastMessage, message);
            SetVariableValue(NotificationsVariable.NotificationCount, _notificationsSent);
            SetVariableValue(NotificationsVariable.LastTarget, target);
            ChangeState(NotificationsState.Idle);
            TriggerEvent(NotificationsEvent.OnNotificationSent);
            
            SendParameter(NotificationsParameter.NotificationSent, true);
            SendParameter(NotificationsParameter.NotificationCount, _notificationsSent);
            Task.Delay(1000).ContinueWith(_ => SendParameter(NotificationsParameter.NotificationSent, false));
        }
        else
        {
            ChangeState(NotificationsState.Idle);
            TriggerEvent(NotificationsEvent.OnNotificationFailed);
            
            SendParameter(NotificationsParameter.NotificationFailed, true);
            Task.Delay(1000).ContinueWith(_ => SendParameter(NotificationsParameter.NotificationFailed, false));
        }
    }

    public void SetSending()
    {
        ChangeState(NotificationsState.Sending);
    }

    public bool IsDesktopEnabled() => GetSettingValue<bool>(NotificationsSetting.EnableDesktop);
    public bool IsXSOverlayEnabled() => GetSettingValue<bool>(NotificationsSetting.EnableXSOverlay);
    public bool IsOVRToolkitEnabled() => GetSettingValue<bool>(NotificationsSetting.EnableOVRToolkit);
    public bool IsWebhookEnabled() => GetSettingValue<bool>(NotificationsSetting.EnableWebhook);
    public bool IsLoggingEnabled() => GetSettingValue<bool>(NotificationsSetting.LogNotifications);
    public string GetDefaultTitle() => GetSettingValue<string>(NotificationsSetting.DefaultTitle);
    public string GetDefaultMessage() => GetSettingValue<string>(NotificationsSetting.DefaultMessage);
    public int GetDefaultTimeout() => GetSettingValue<int>(NotificationsSetting.DefaultTimeout);
    public int GetDefaultOpacity() => GetSettingValue<int>(NotificationsSetting.DefaultOpacity);
    public string GetWebhookUrl() => GetSettingValue<string>(NotificationsSetting.WebhookUrl);
    public string GetWebhookMethod() => GetSettingValue<WebhookMethod>(NotificationsSetting.WebhookMethod).ToString();
}
