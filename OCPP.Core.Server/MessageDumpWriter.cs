using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Writes raw OCPP messages to per-day / per-chargepoint log files:
    ///   {MessageDumpDir}/{yyyy-MM-dd}/{ChargePointId}.log
    /// Each line: "{timestamp}\t{IN|OUT}\t{protocol}\t{raw message}"
    ///
    /// Callers only enqueue (non-blocking, no Task/file per message). A single background
    /// consumer appends to cached file handles, flushing after each drained batch.
    /// Day folders older than MessageDumpRetentionDays are deleted automatically.
    /// </summary>
    public sealed class MessageDumpWriter : BackgroundService
    {
        private const int QueueCapacity = 10000;
        private const string DayFolderFormat = "yyyy-MM-dd";

        private readonly string _dumpDir;
        private readonly int _retentionDays;
        private readonly ILogger _logger;
        private readonly Channel<DumpEntry> _channel;

        // Only touched by the background consumer => no locking needed
        private readonly Dictionary<string, StreamWriter> _writers = new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);
        private DateTime _currentDay = DateTime.MinValue;

        private long _droppedCount;

        private readonly struct DumpEntry
        {
            public DumpEntry(DateTimeOffset timestamp, string chargePointId, string protocol, string direction, string message)
            {
                Timestamp = timestamp;
                ChargePointId = chargePointId;
                Protocol = protocol;
                Direction = direction;
                Message = message;
            }

            public DateTimeOffset Timestamp { get; }
            public string ChargePointId { get; }
            public string Protocol { get; }
            public string Direction { get; }
            public string Message { get; }
        }

        public MessageDumpWriter(IConfiguration configuration, ILoggerFactory logFactory)
        {
            _logger = logFactory.CreateLogger("MessageDumpWriter");

            string dumpDir = configuration.GetValue<string>("MessageDumpDir");
            _dumpDir = string.IsNullOrWhiteSpace(dumpDir) ? null : dumpDir;
            _retentionDays = configuration.GetValue<int>("MessageDumpRetentionDays", 0);

            // Never block the websocket loop: if the disk can't keep up, drop and count
            _channel = Channel.CreateBounded<DumpEntry>(
                new BoundedChannelOptions(QueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                },
                _ => Interlocked.Increment(ref _droppedCount));
        }

        public bool IsEnabled => _dumpDir != null;

        /// <summary>
        /// Enqueues a message for dumping. Returns immediately.
        /// </summary>
        /// <param name="direction">"IN" (from chargepoint) or "OUT" (to chargepoint)</param>
        public void Dump(string chargePointId, string protocol, string direction, string message)
        {
            if (!IsEnabled) return;

            _channel.Writer.TryWrite(new DumpEntry(DateTimeOffset.Now, chargePointId, protocol, direction, message));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!IsEnabled)
            {
                _logger.LogInformation("MessageDumpWriter => Disabled (no MessageDumpDir configured)");
                return;
            }

            _logger.LogInformation("MessageDumpWriter => Writing to '{0}' (retention: {1} days)", _dumpDir, _retentionDays > 0 ? _retentionDays.ToString() : "unlimited");

            try
            {
                while (await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    DrainQueue();
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown
            }
            finally
            {
                // Write whatever is still queued before closing
                DrainQueue();
                CloseAllWriters();
            }
        }

        private void DrainQueue()
        {
            while (_channel.Reader.TryRead(out DumpEntry entry))
            {
                WriteEntry(entry);
            }

            foreach (KeyValuePair<string, StreamWriter> kvp in _writers)
            {
                try
                {
                    kvp.Value.Flush();
                }
                catch (Exception exp)
                {
                    _logger.LogError(exp, "MessageDumpWriter => Error flushing dump file for chargepoint '{0}'", kvp.Key);
                }
            }

            long dropped = Interlocked.Exchange(ref _droppedCount, 0);
            if (dropped > 0)
            {
                _logger.LogWarning("MessageDumpWriter => Queue full, dropped {0} message(s)", dropped);
            }
        }

        private void WriteEntry(DumpEntry entry)
        {
            DateTime day = entry.Timestamp.Date;
            if (day != _currentDay)
            {
                // New day => new folder; release yesterday's handles and apply retention
                CloseAllWriters();
                _currentDay = day;
                ApplyRetention();
            }

            string chargePointId = entry.ChargePointId ?? "unknown";
            try
            {
                if (!_writers.TryGetValue(chargePointId, out StreamWriter writer))
                {
                    writer = OpenWriter(day, chargePointId);
                    _writers[chargePointId] = writer;
                }

                writer.Write(entry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
                writer.Write('\t');
                writer.Write(entry.Direction);
                writer.Write('\t');
                writer.Write(entry.Protocol);
                writer.Write('\t');
                writer.Write(OneLine(entry.Message));
                writer.Write('\n');
            }
            catch (Exception exp)
            {
                _logger.LogError(exp, "MessageDumpWriter => Error dumping message for chargepoint '{0}'", chargePointId);

                // Drop the broken handle so the next message reopens the file
                if (_writers.Remove(chargePointId, out StreamWriter broken))
                {
                    try { broken.Dispose(); } catch { }
                }
            }
        }

        private StreamWriter OpenWriter(DateTime day, string chargePointId)
        {
            string dayDir = Path.Combine(_dumpDir, day.ToString(DayFolderFormat, CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dayDir);

            string path = Path.Combine(dayDir, SanitizeFileName(chargePointId) + ".log");

            // FileShare.ReadWrite|Delete => files can be opened/tailed/deleted while the server runs
            FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 64 * 1024);
            return new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024);
        }

        private void CloseAllWriters()
        {
            foreach (StreamWriter writer in _writers.Values)
            {
                try { writer.Dispose(); } catch { }
            }
            _writers.Clear();
        }

        private void ApplyRetention()
        {
            if (_retentionDays <= 0) return;

            try
            {
                if (!Directory.Exists(_dumpDir)) return;

                DateTime cutoff = _currentDay.AddDays(-_retentionDays);
                foreach (string dir in Directory.EnumerateDirectories(_dumpDir))
                {
                    // Only touch folders created by this writer
                    if (DateTime.TryParseExact(Path.GetFileName(dir), DayFolderFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDay) &&
                        folderDay < cutoff)
                    {
                        Directory.Delete(dir, true);
                        _logger.LogInformation("MessageDumpWriter => Deleted expired dump folder '{0}'", dir);
                    }
                }
            }
            catch (Exception exp)
            {
                _logger.LogError(exp, "MessageDumpWriter => Error applying retention in '{0}'", _dumpDir);
            }
        }

        private static string OneLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (message.IndexOf('\n') < 0 && message.IndexOf('\r') < 0) return message;
            return message.Replace("\r", string.Empty).Replace('\n', ' ');
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalid) < 0) return name;

            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString();
        }
    }
}
