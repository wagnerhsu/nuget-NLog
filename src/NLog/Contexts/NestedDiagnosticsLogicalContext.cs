// 
// Copyright (c) 2004-2020 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
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


namespace NLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NLog.Internal;

    /// <summary>
    /// Async version of <see cref="NestedDiagnosticsContext" /> - a logical context structure that keeps a stack
    /// Allows for maintaining scope across asynchronous tasks and call contexts.
    /// </summary>
    public static class NestedDiagnosticsLogicalContext
    {
        /// <summary>
        /// Pushes the specified value on current stack
        /// </summary>
        /// <param name="value">The value to be pushed.</param>
        /// <returns>An instance of the object that implements IDisposable that returns the stack to the previous level when IDisposable.Dispose() is called. To be used with C# using() statement.</returns>
        public static IDisposable Push<T>(T value)
        {
            return ScopeContext.PushOperationState(value);
        }

        /// <summary>
        /// Pushes the specified value on current stack
        /// </summary>
        /// <param name="value">The value to be pushed.</param>
        /// <returns>An instance of the object that implements IDisposable that returns the stack to the previous level when IDisposable.Dispose() is called. To be used with C# using() statement.</returns>
        public static IDisposable PushObject(object value)
        {
            return Push(value);
        }

        /// <summary>
        /// Pops the top message off the NDLC stack.
        /// </summary>
        /// <returns>The top message which is no longer on the stack.</returns>
        /// <remarks>this methods returns a object instead of string, this because of backwards-compatibility</remarks>
        public static object Pop()
        {
            //NLOG 5: return string (breaking change)
            return PopObject();
        }

        /// <summary>
        /// Pops the top message from the NDLC stack.
        /// </summary>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when converting the value to a string.</param>
        /// <returns>The top message, which is removed from the stack, as a string value.</returns>
        public static string Pop(IFormatProvider formatProvider)
        {
            return FormatHelper.ConvertToString(PopObject() ?? string.Empty, formatProvider);
        }

        /// <summary>
        /// Pops the top message off the current NDLC stack
        /// </summary>
        /// <returns>The object from the top of the NDLC stack, if defined; otherwise <c>null</c>.</returns>
        public static object PopObject()
        {
#if !NET35 && !NET40 && !NET45
            return ScopeContext.PopNestedContextLegacy();
#else
            var currentContext = GetThreadLocal();
            if (currentContext?.Count > 0)
            {
                var objectValue = currentContext.First.Value;
                if (objectValue is ObjectHandleSerializer objectHandle)
                    objectValue = objectHandle.Unwrap();
                var newContext = currentContext.Count > 1 ? new LinkedList<object>(currentContext) : null;
                if (newContext != null)
                    newContext.RemoveFirst();
                SetThreadLocal(newContext);
                return objectValue;
            }
            return null;
#endif
        }

        /// <summary>
        /// Peeks the top object on the current NDLC stack
        /// </summary>
        /// <returns>The object from the top of the NDLC stack, if defined; otherwise <c>null</c>.</returns>
        public static object PeekObject()
        {
            return ScopeContext.PeekOperationState();
        }

        /// <summary>
        /// Clears current stack.
        /// </summary>
        public static void Clear()
        {
#if !NET35 && !NET40 && !NET45
            ScopeContext.ClearNestedContextLegacy();
#else
            ClearStack();
#endif
        }

        /// <summary>
        /// Gets all messages on the stack.
        /// </summary>
        /// <returns>Array of strings on the stack.</returns>
        public static string[] GetAllMessages()
        {
            return GetAllMessages(null);
        }

        /// <summary>
        /// Gets all messages from the stack, without removing them.
        /// </summary>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when converting a value to a string.</param>
        /// <returns>Array of strings.</returns>
        public static string[] GetAllMessages(IFormatProvider formatProvider)
        {
            return GetAllObjects().Select((o) => FormatHelper.ConvertToString(o, formatProvider)).ToArray();
        }

        /// <summary>
        /// Gets all objects on the stack. The objects are not removed from the stack.
        /// </summary>
        /// <returns>Array of objects on the stack.</returns>
        public static object[] GetAllObjects()
        {
            return ScopeContext.GetAllOperationStates();
        }

#if NET35 || NET40 || NET45

        internal static IDisposable PushOperationState<T>(T value)
        {
            var oldContext = GetThreadLocal();
            var newContext = oldContext?.Count > 0 ? new LinkedList<object>(oldContext) : new LinkedList<object>();
            object objectValue = value;
            if (Convert.GetTypeCode(objectValue) == TypeCode.Object)
                objectValue = new ObjectHandleSerializer(objectValue);
            newContext.AddFirst(objectValue);
            SetThreadLocal(newContext);
            return new NestedScope(oldContext, objectValue);
        }

        internal static object PeekOperationState()
        {
            var currentContext = GetThreadLocal();
            var objectValue = currentContext?.Count > 0 ? currentContext.First.Value : null;
            if (objectValue is ObjectHandleSerializer objectHandle)
                objectValue = objectHandle.Unwrap();
            return objectValue;
        }

        internal static object[] GetAllOperationStates()
        {
            var currentContext = GetThreadLocal();
            if (currentContext?.Count > 0)
            {
                int index = 0;
                object[] messages = new object[currentContext.Count];
                foreach (var node in currentContext)
                {
                    if (node is ObjectHandleSerializer objectHandle)
                        messages[index++] = objectHandle.Unwrap();
                    else
                        messages[index++] = node;
                }
                return messages;
            }
            return ArrayHelper.Empty<object>();
        }

        internal static void ClearStack()
        {
            SetThreadLocal(null);
        }

        private sealed class NestedScope : IDisposable
        {
            private readonly LinkedList<object> _oldContext;
            private readonly object _value;
            private bool _diposed;

            public NestedScope(LinkedList<object> oldContext, object value)
            {
                _oldContext = oldContext;
                _value = value;
            }

            public void Dispose()
            {
                if (!_diposed)
                {
                    SetThreadLocal(_oldContext);
                    _diposed = true;
                }
            }

            public override string ToString()
            {
                return _value?.ToString() ?? "null";
            }
        }

        private static void SetThreadLocal(LinkedList<object> nestedContext)
        {
            if (nestedContext == null)
                System.Runtime.Remoting.Messaging.CallContext.FreeNamedDataSlot(NestedDiagnosticsContextKey);
            else
                System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(NestedDiagnosticsContextKey, nestedContext);
        }

        private static LinkedList<object> GetThreadLocal()
        {
            return System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(NestedDiagnosticsContextKey) as LinkedList<object>;
        }

        private const string NestedDiagnosticsContextKey = "NLog.NestedDiagnosticsLogicalContext";
#endif

        [Obsolete("Required to be compatible with legacy NLog versions, when using remoting. Marked obsolete on NLog 5.0")]
        interface INestedContext : IDisposable
        {
            INestedContext Parent { get; }
            int FrameLevel { get; }
            object Value { get; }
            long CreatedTimeUtcTicks { get; }
        }

#if !NETSTANDARD1_0
        [Serializable]
#endif
        [Obsolete("Required to be compatible with legacy NLog versions, when using remoting. Marked obsolete on NLog 5.0")]
        sealed class NestedContext<T> : INestedContext
        {
            public INestedContext Parent { get; }
            public T Value { get; }
            public long CreatedTimeUtcTicks { get; }
            public int FrameLevel { get; }
            private int _disposed;

            object INestedContext.Value
            {
                get
                {
                    object value = Value;
#if NET35 || NET40 || NET45
                    if (value is ObjectHandleSerializer objectHandle)
                    {
                        return objectHandle.Unwrap();
                    }
#endif
                    return value;
                }
            }

            public NestedContext(INestedContext parent, T value)
            {
                Parent = parent;
                Value = value;
                CreatedTimeUtcTicks = DateTime.UtcNow.Ticks; // Low time resolution, but okay fast
                FrameLevel = parent?.FrameLevel + 1 ?? 1;
            }

            void IDisposable.Dispose()
            {
                if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 1)
                {
                    PopObject();
                }
            }

            public override string ToString()
            {
                object value = Value;
                return value?.ToString() ?? "null";
            }
        }
    }
}
