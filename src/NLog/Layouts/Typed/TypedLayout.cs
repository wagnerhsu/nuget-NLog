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

using System;
using System.Globalization;
using System.Linq;
using NLog.Common;
using NLog.Internal;

namespace NLog.Layouts
{
    /// <summary>
    /// Layout rendering to int
    /// </summary>
    public abstract class TypedLayout<T> : Layout
    {
        private readonly Layout _layout;
        private readonly T _value;

        /// <summary>
        /// Layout with template
        /// </summary>
        /// <param name="layout"></param>
        protected TypedLayout(Layout layout)
        {
            if (layout != null && layout is SimpleLayout simpleLayout && simpleLayout.IsFixedText)
            {
                // ReSharper disable once VirtualMemberCallInConstructor
                if (!TryParse(simpleLayout.FixedText, out var value))
                {
                    InternalLogger.Warn($"layout with text '{simpleLayout.FixedText}' isn't an int");
                }

                _value = value;
                //keep layout also for context
            }
            else
            {
                _value = default(T);
            }

            _layout = layout;
        }

        /// <summary>
        /// Layout with fixed value
        /// </summary>
        /// <param name="value"></param>
        protected TypedLayout(T value)
        {
            _value = value;
            _layout = null;
        }



        /// <summary>
        /// Is fixed?
        /// </summary>
        public bool IsFixed => _value != null;

        #region Implementation of IRawValue

        /// <inheritdoc cref="IRawValue" />
        internal override bool TryGetRawValue(LogEventInfo logEvent, out object rawValue)
        {
            if (_value != null)
            {
                rawValue = _value;
                return true;
            }

            if (_layout == null)
            {
                rawValue = null;
                return true;
            }

            if (_layout.TryGetRawValue(logEvent, out var raw))
            {
                var success = TryConvertRawToValue(raw, out var i);
                rawValue = i;
                return success;
            }

            rawValue = null;
            return false;
        }

        #endregion

        ///// <summary>
        ///// Converts a given text to a <see cref="Layout" />.
        ///// </summary>
        ///// <param name="number">Text to be converted.</param>
        ///// <returns><see cref="SimpleLayout" /> object represented by the text.</returns>
        //public static implicit operator GenericLayout<T>(T number)
        //{
        //    return new GenericLayout<T>(number);
        //}

        ///// <summary>
        ///// Converts a given text to a <see cref="Layout" />.
        ///// </summary>
        ///// <param name="layout">Text to be converted.</param>
        ///// <returns><see cref="SimpleLayout" /> object represented by the text.</returns>
        //public static implicit operator GenericLayout<T>([Localizable(false)] string layout)
        //{
        //    return new GenericLayout<T>(layout);
        //}

        /// <summary>
        /// Render To int
        /// </summary>
        /// <returns></returns>
        public T RenderToValue(LogEventInfo logEvent)
        {
            if (_value != null)
            {
                return _value;
            }

            if (_layout == null)
            {
                return default(T);
            }

            if (_layout.TryGetRawValue(logEvent, out var raw))
            {
                if (TryConvertRawToValue(raw, out var renderToInt))
                {
                    return renderToInt;
                }

                InternalLogger.Warn("rawvalue isn't a int ");
            }

            var text = _layout.Render(logEvent);
            if (TryParse(text, out var value))
            {
                return value;
            }

            InternalLogger.Warn("Parse {0} to int failed", text);
            return default(T);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="cultureInfo"></param>
        /// <returns></returns>
        protected abstract string ValueToString(T value, CultureInfo cultureInfo);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected abstract bool TryParse(string text, out T value);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="raw"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected abstract bool TryConvertTo(object raw, out T value);

        private bool TryConvertRawToValue(object raw, out T value)
        {
            if (raw == null)
            {
                value = default(T);
                return true;
            }

            if (raw is T i)
            {
                value = i;
                return true;
            }

            if (TryConvertTo(raw, out value))
            {
                return true;
            }

            value = default(T);

            return false;
        }



        #region Overrides of Layout

        /// <inheritdoc />
        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            return ValueToString(_value, LoggingConfiguration.DefaultCultureInfo) ?? _layout.Render(logEvent);
        }



        #endregion
    }
}