﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.Mobile.Crashes.Shared;
using Microsoft.Azure.Mobile.Crashes.iOS.Bindings;
using Foundation;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Microsoft.Azure.Mobile.Crashes
{
    using iOSCrashes = iOS.Bindings.MSCrashes;
    using iOSException = iOS.Bindings.MSException;
    using iOSStackFrame = iOS.Bindings.MSStackFrame;

    class PlatformCrashes : PlatformCrashesBase
    {
        public override Type BindingType => typeof(iOSCrashes);

        public override bool Enabled
        {
            get { return MSCrashes.IsEnabled(); }
            set { MSCrashes.SetEnabled(value); }
        }

        public override bool HasCrashedInLastSession => iOSCrashes.HasCrashedInLastSession;

        //public override void TrackException(Exception exception)
        //{
        //	throw new NotImplementedException();
        //}

        static PlatformCrashes()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            iOSException exception = GenerateiOSException((Exception)e.ExceptionObject);
            iOS.Bindings.MSWrapperExceptionManager.SetWrapperException(exception);
        }

        private static iOSException GenerateiOSException(Exception exception)
        {
            iOSException iosException = new iOSException();
            iosException.Type = exception.GetType().FullName;
            iosException.Message = exception.Message;
            iosException.Frames = GenerateStackFrames(exception);
            iosException.WrapperSdkName = WrapperSdk.Name;

            var aggregateException = exception as AggregateException;
            List<iOSException> innerExceptions = new List<iOSException>();

            if (aggregateException?.InnerExceptions != null)
            {
                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    innerExceptions.Add(GenerateiOSException(innerException));
                }
            }
            else if (exception.InnerException != null)
            {
                innerExceptions.Add(GenerateiOSException(exception.InnerException));
            }

            iosException.InnerExceptions = innerExceptions.Count > 0 ? innerExceptions.ToArray() : null;

            return iosException;
        }

        private static iOSStackFrame[] GenerateStackFrames(Exception e)
        {
            StackTrace trace = new StackTrace(e, true);

            List<iOSStackFrame> frameList = new List<iOSStackFrame>();
            for (int i = 0; i < trace.FrameCount; ++i)
            {
                StackFrame dotnetFrame = trace.GetFrame(i);
                if (dotnetFrame.GetMethod() == null) continue;
                iOSStackFrame iosFrame = new iOSStackFrame();
                iosFrame.Address = null;
                iosFrame.Code = null;
                iosFrame.MethodName = dotnetFrame.GetMethod().Name;
                iosFrame.ClassName = dotnetFrame.GetMethod().DeclaringType?.FullName;
                iosFrame.LineNumber = dotnetFrame.GetFileLineNumber() == 0 ? null : (NSNumber)(dotnetFrame.GetFileLineNumber());
                iosFrame.FileName = AnonymizePath(dotnetFrame.GetFileName());
                frameList.Add(iosFrame);
            }
            return frameList.Count == 0 ? null : frameList.ToArray();
        }

        private static string AnonymizePath(string path)
        {
            if ((path == null) || (path.Count() == 0) || !path.Contains("/Users/"))
            {
                return path;
            }
                
            string pattern = "(/Users/[^/]+/)";
            return Regex.Replace(path, pattern, "/Users/USER/");
        }
    }
}
