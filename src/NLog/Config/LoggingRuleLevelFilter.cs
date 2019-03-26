// 
// Copyright (c) 2004-2019 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 

namespace NLog.Config
{
    using System;
    using System.Collections.Generic;
    using NLog.Common;
    using NLog.Internal;
    using NLog.Layouts;

    internal interface ILoggingRuleLevelFilter
    {
        /// <summary>
        /// Level enabled flags for each LogLevel ordinal
        /// </summary>
        bool[] LogLevels { get; }

        /// <summary>
        /// Converts the filter into a simple <see cref="LoggingRuleLevelFilter"/>
        /// </summary>
        LoggingRuleLevelFilter GetSimpleFilterForUpdate();
    }

    /// <summary>
    /// Default filtering with static level config
    /// </summary>
    class LoggingRuleLevelFilter : ILoggingRuleLevelFilter
    {
        public static readonly LoggingRuleLevelFilter Off = new LoggingRuleLevelFilter();
        public bool[] LogLevels { get; }

        public LoggingRuleLevelFilter(bool[] logLevels = null)
        {
            LogLevels = new bool[LogLevel.MaxLevel.Ordinal + 1];
            if (logLevels != null)
            {
                for (int i = 0; i < Math.Min(logLevels.Length, LogLevels.Length); ++i)
                    LogLevels[i] = logLevels[i];
            }
        }

        public LoggingRuleLevelFilter GetSimpleFilterForUpdate()
        {
            if (!ReferenceEquals(LogLevels, Off.LogLevels))
                return this;
            else
                return new LoggingRuleLevelFilter();
        }

        public LoggingRuleLevelFilter SetLoggingLevels(LogLevel minLevel, LogLevel maxLevel, bool enable)
        {
            for (int i = minLevel.Ordinal; i <= maxLevel.Ordinal; ++i)
                LogLevels[i] = enable;
            return this;
        }
    }

    /// <summary>
    /// Dynamic filtering with a positive list of enabled levels
    /// </summary>
    class DynamicLogLevelFilter : ILoggingRuleLevelFilter
    {
        private static readonly char[] _levelFilerSplitter = { ',' };
        readonly LoggingRule _loggingRule;
        readonly SimpleLayout _levelFilter;
        private KeyValuePair<string, bool[]> _activeFilter;

        public bool[] LogLevels => GenerateLogLevels();

        public DynamicLogLevelFilter(LoggingRule loggingRule, SimpleLayout levelFilter)
        {
            _loggingRule = loggingRule;
            _levelFilter = levelFilter;
            _activeFilter = new KeyValuePair<string, bool[]>(string.Empty, LoggingRuleLevelFilter.Off.LogLevels);
        }

        public LoggingRuleLevelFilter GetSimpleFilterForUpdate()
        {
            return new LoggingRuleLevelFilter(LogLevels);
        }

        private bool[] GenerateLogLevels()
        {
            var levelFilter = _levelFilter.Render(LogEventInfo.CreateNullEvent());
            if (string.IsNullOrEmpty(levelFilter))
                return LoggingRuleLevelFilter.Off.LogLevels;

            var activeFilter = _activeFilter;
            if (activeFilter.Key != levelFilter)
            {
                if (levelFilter.IndexOf(',') >= 0)
                {
                    bool[] logLevels = ParseLevels(levelFilter);
                    _activeFilter = activeFilter = new KeyValuePair<string, bool[]>(levelFilter, logLevels);
                }
                else
                {
                    try
                    {
                        if (StringHelpers.IsNullOrWhiteSpace(levelFilter))
                            return LoggingRuleLevelFilter.Off.LogLevels;

                        var logLevel = LogLevel.FromString(levelFilter.Trim());
                        if (logLevel == LogLevel.Off)
                            return LoggingRuleLevelFilter.Off.LogLevels;

                        bool[] logLevels = new bool[LogLevel.MaxLevel.Ordinal + 1];
                        logLevels[logLevel.Ordinal] = true;
                        _activeFilter = activeFilter = new KeyValuePair<string, bool[]>(levelFilter, logLevels);
                    }
                    catch (ArgumentException ex)
                    {
                        InternalLogger.Warn(ex, "Logging rule {0} with filter `{1}` has invalid level filter: {2}", _loggingRule.RuleName, _loggingRule.LoggerNamePattern, levelFilter);
                        return LoggingRuleLevelFilter.Off.LogLevels;
                    }
                }
            }

            return activeFilter.Value;
        }

        private bool[] ParseLevels(string levelFilter)
        {
            var levels = levelFilter.Split(_levelFilerSplitter, StringSplitOptions.RemoveEmptyEntries);
            bool[] logLevels = new bool[LogLevel.MaxLevel.Ordinal + 1];
            foreach (var level in levels)
            {
                try
                {
                    if (StringHelpers.IsNullOrWhiteSpace(level))
                        continue;

                    var logLevel = LogLevel.FromString(level.Trim());
                    if (logLevel == LogLevel.Off)
                        continue;

                    logLevels[logLevel.Ordinal] = true;
                }
                catch (ArgumentException ex)
                {
                    InternalLogger.Warn(ex, "Logging rule {0} with filter `{1}` has invalid level filter: {2}", _loggingRule.RuleName, _loggingRule.LoggerNamePattern, levelFilter);
                }
            }

            return logLevels;
        }
    }

    /// <summary>
    /// Dynamic filtering with a minlevel and maxlevel range
    /// </summary>
    class DynamicRangeLevelFilter : ILoggingRuleLevelFilter
    {
        readonly LoggingRule _loggingRule;
        readonly SimpleLayout _minLevel;
        readonly SimpleLayout _maxLevel;
        private KeyValuePair<MinMaxLevels, bool[]> _activeFilter;

        public bool[] LogLevels => GenerateLogLevels();

        public DynamicRangeLevelFilter(LoggingRule loggingRule, SimpleLayout minLevel, SimpleLayout maxLevel)
        {
            _loggingRule = loggingRule;
            _minLevel = minLevel;
            _maxLevel = maxLevel;
            _activeFilter = new KeyValuePair<MinMaxLevels, bool[]>(new MinMaxLevels(string.Empty, string.Empty), LoggingRuleLevelFilter.Off.LogLevels);
        }

        public LoggingRuleLevelFilter GetSimpleFilterForUpdate()
        {
            return new LoggingRuleLevelFilter(LogLevels);
        }

        private bool[] GenerateLogLevels()
        {
            var minLevelFilter = _minLevel?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
            var maxLevelFilter = _maxLevel?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
            if (string.IsNullOrEmpty(minLevelFilter) && string.IsNullOrEmpty(maxLevelFilter))
                return LoggingRuleLevelFilter.Off.LogLevels;

            var activeFilter = _activeFilter;
            if (!activeFilter.Key.Equals(new MinMaxLevels(minLevelFilter, maxLevelFilter)))
            {
                bool[] logLevels = ParseLevelRange(minLevelFilter, maxLevelFilter);
                _activeFilter = activeFilter = new KeyValuePair<MinMaxLevels, bool[]>(new MinMaxLevels(minLevelFilter, maxLevelFilter), logLevels);
            }
            return activeFilter.Value;
        }

        private bool[] ParseLevelRange(string minLevelFilter, string maxLevelFilter)
        {
            LogLevel minLevel = ParseLogLevel(minLevelFilter, LogLevel.MinLevel) ?? LogLevel.MaxLevel;
            LogLevel maxLevel = ParseLogLevel(maxLevelFilter, LogLevel.MaxLevel) ?? LogLevel.MinLevel;

            bool[] logLevels = new bool[LogLevel.MaxLevel.Ordinal + 1];
            for (int i = minLevel.Ordinal; i <= logLevels.Length - 1 && i <= maxLevel.Ordinal; ++i)
            {
                logLevels[i] = true;
            }

            return logLevels;
        }

        LogLevel ParseLogLevel(string logLevel, LogLevel levelIfEmpty)
        {
            try
            {
                if (string.IsNullOrEmpty(logLevel))
                    return levelIfEmpty;

                return LogLevel.FromString(logLevel.Trim());
            }
            catch (ArgumentException ex)
            {
                InternalLogger.Warn(ex, "Logging rule {0} with filter `{1}` has invalid level filter: {2}", _loggingRule.RuleName, _loggingRule.LoggerNamePattern, logLevel);
                return null;
            }
        }

        struct MinMaxLevels : IEquatable<MinMaxLevels>
        {
            readonly string MinLevel;
            readonly string MaxLevel;

            public MinMaxLevels(string minLevel, string maxLevel)
            {
                MinLevel = minLevel;
                MaxLevel = maxLevel;
            }

            public bool Equals(MinMaxLevels other)
            {
                return MinLevel == other.MinLevel && MaxLevel == other.MaxLevel;
            }
        }
    }
}
