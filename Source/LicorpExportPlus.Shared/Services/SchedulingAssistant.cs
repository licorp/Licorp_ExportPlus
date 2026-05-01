using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Services
{
    public class SchedulingAssistant
    {
        private readonly List<ScheduledExport> _scheduledExports;
        private readonly System.Timers.Timer _timer;

        public event EventHandler<ScheduledExportEventArgs> ExportTriggered;

        public SchedulingAssistant()
        {
            _scheduledExports = new List<ScheduledExport>();
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += CheckScheduledExports;
            _timer.Start();
        }

        public void AddScheduledExport(ScheduledExport export)
        {
            _scheduledExports.Add(export);
        }

        public void RemoveScheduledExport(string id)
        {
            var export = _scheduledExports.Find(e => e.Id == id);
            if (export != null)
            {
                _scheduledExports.Remove(export);
            }
        }

        public List<ScheduledExport> GetScheduledExports()
        {
            return new List<ScheduledExport>(_scheduledExports);
        }

        private async void CheckScheduledExports(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;

            foreach (var export in _scheduledExports.ToArray())
            {
                if (ShouldTriggerExport(export, now))
                {
                    TriggerExport(export);

                    if (export.Frequency == ScheduleRepeatType.Once)
                    {
                        _scheduledExports.Remove(export);
                    }
                    else
                    {
                        UpdateNextRunTime(export);
                    }
                }
                await Task.Yield();
            }
        }

        private bool ShouldTriggerExport(ScheduledExport export, DateTime now)
        {
            return export.NextRunTime <= now && export.IsEnabled;
        }

        private void TriggerExport(ScheduledExport export)
        {
            ExportTriggered?.Invoke(this, new ScheduledExportEventArgs(export));
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

        public ScheduleRepeatType ParseFrequency(string frequencyString)
        {
            return (ScheduleRepeatType)Enum.Parse(typeof(ScheduleRepeatType), frequencyString);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    public class ScheduledExportEventArgs : EventArgs
    {
        public ScheduledExport Export { get; }

        public ScheduledExportEventArgs(ScheduledExport export)
        {
            Export = export;
        }
    }
}
