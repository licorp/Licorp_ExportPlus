using System;
using System.Collections.Generic;
using Licorp.Diagnostics;
using LicorpExportPlus.Models;
using LicorpExportPlus.Services.Interfaces;

namespace LicorpExportPlus.Services;

public class SchedulingAssistantAdapter : ISchedulingService, IDisposable
{
    private readonly List<ScheduledExport> _scheduledExports = new();
    private readonly System.Timers.Timer _timer;

    public event EventHandler<ScheduledExportEventArgs> ExportTriggered;

    public SchedulingAssistantAdapter()
    {
        _timer = new System.Timers.Timer(60000);
        _timer.Elapsed += CheckScheduledExports;
        _timer.Start();
        LicorpTrace.Info("[SchedulingAssistant] Initialized");
    }

    public void AddScheduledExport(ScheduledExport export)
    {
        _scheduledExports.Add(export);
        LicorpTrace.Info($"[SchedulingAssistant] Added schedule: {export.Name}");
    }

    public void RemoveScheduledExport(string id)
    {
        var export = _scheduledExports.Find(e => e.Id == id);
        if (export != null)
        {
            _scheduledExports.Remove(export);
            LicorpTrace.Info($"[SchedulingAssistant] Removed schedule: {export.Name}");
        }
    }

    public List<ScheduledExport> GetScheduledExports()
    {
        return new List<ScheduledExport>(_scheduledExports);
    }

    private void CheckScheduledExports(object sender, System.Timers.ElapsedEventArgs e)
    {
        var now = DateTime.Now;

        foreach (var export in _scheduledExports.ToArray())
        {
            if (export.NextRunTime <= now && export.IsEnabled)
            {
                ExportTriggered?.Invoke(this, new ScheduledExportEventArgs(export));

                if (export.Frequency == ScheduleRepeatType.Once)
                {
                    _scheduledExports.Remove(export);
                }
                else
                {
                    UpdateNextRunTime(export);
                }
            }
        }
    }

    private void UpdateNextRunTime(ScheduledExport export)
    {
        switch (export.Frequency)
        {
            case ScheduleRepeatType.Daily:
                export.NextRunTime = export.NextRunTime.AddDays(1);
                break;
            case ScheduleRepeatType.Weekly:
                export.NextRunTime = export.NextRunTime.AddDays(7);
                break;
            case ScheduleRepeatType.Monthly:
                export.NextRunTime = export.NextRunTime.AddMonths(1);
                break;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
