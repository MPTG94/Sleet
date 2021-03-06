﻿using System;
using System.Collections.Concurrent;
using System.Text;
using NuGet.Common;

namespace Sleet.Test
{
    public class TestLogger : ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

        public void LogDebug(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogError(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogErrorSummary(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogInformationSummary(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogMinimal(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogSummary(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogWarning(string data)
        {
            Messages.Enqueue(data);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var message in Messages)
            {
                sb.AppendLine(message);
            }

            return sb.ToString();
        }
    }
}
