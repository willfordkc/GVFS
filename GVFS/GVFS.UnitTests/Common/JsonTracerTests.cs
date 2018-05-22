using GVFS.Common.Tracing;
using GVFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace GVFS.UnitTests.Common
{
    [TestFixture]
    public class JsonTracerTests
    {
        [TestCase]
        public void EventsAreFilteredByVerbosity()
        {
            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventsAreFilteredByVerbosity1", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Informational, Keywords.Any))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedEvent(EventLevel.Informational, "ShouldReceive", metadata: null);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldReceive"));

                tracer.RelatedEvent(EventLevel.Verbose, "ShouldNotReceive", metadata: null);
                listener.EventNamesRead.ShouldNotContain(name => name.Equals("ShouldNotReceive"));
            }

            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventsAreFilteredByVerbosity2", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Verbose, Keywords.Any))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedEvent(EventLevel.Informational, "ShouldReceive", metadata: null);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldReceive"));

                tracer.RelatedEvent(EventLevel.Verbose, "ShouldAlsoReceive", metadata: null);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldAlsoReceive"));
            }
        }

        [TestCase]
        public void EventsAreFilteredByKeyword()
        {
            // Network filters all but network out
            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventsAreFilteredByKeyword1", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Verbose, Keywords.Network))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedEvent(EventLevel.Informational, "ShouldReceive", metadata: null, keyword: Keywords.Network);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldReceive"));

                tracer.RelatedEvent(EventLevel.Verbose, "ShouldNotReceive", metadata: null);
                listener.EventNamesRead.ShouldNotContain(name => name.Equals("ShouldNotReceive"));
            }

            // Any filters nothing out
            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventsAreFilteredByKeyword2", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Verbose, Keywords.Any))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedEvent(EventLevel.Informational, "ShouldReceive", metadata: null, keyword: Keywords.Network);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldReceive"));

                tracer.RelatedEvent(EventLevel.Verbose, "ShouldAlsoReceive", metadata: null);
                listener.EventNamesRead.ShouldContain(name => name.Equals("ShouldAlsoReceive"));
            }
             
            // None filters everything out (including events marked as none)
            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventsAreFilteredByKeyword3", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Verbose, Keywords.None))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedEvent(EventLevel.Informational, "ShouldNotReceive", metadata: null, keyword: Keywords.Network);
                listener.EventNamesRead.ShouldBeEmpty();

                tracer.RelatedEvent(EventLevel.Verbose, "ShouldAlsoNotReceive", metadata: null);
                listener.EventNamesRead.ShouldBeEmpty();
            }
        }

        [TestCase]
        public void EventMetadataWithKeywordsIsOptional()
        {
            using (JsonTracer tracer = new JsonTracer("Microsoft-GVFS-Test", "EventMetadataWithKeywordsIsOptional", useCriticalTelemetryFlag: false))
            using (MockListener listener = new MockListener(EventLevel.Verbose, Keywords.Any))
            {
                tracer.AddInProcEventListener(listener);

                tracer.RelatedWarning(metadata: null, message: string.Empty, keywords: Keywords.Telemetry);
                listener.EventNamesRead.ShouldContain(x => x.Equals("Warning"));

                tracer.RelatedError(metadata: null, message: string.Empty, keywords: Keywords.Telemetry);
                listener.EventNamesRead.ShouldContain(x => x.Equals("Error"));
            }
        }

        public class MockListener : InProcEventListener
        {
            public readonly List<string> EventNamesRead = new List<string>();

            public MockListener(EventLevel maxVerbosity, Keywords keywordFilter)
                : base(maxVerbosity, keywordFilter)
            {
            }

            protected override void RecordMessageInternal(string eventName, Guid activityId, Guid parentActivityId, EventLevel level, Keywords keywords, EventOpcode opcode, string jsonPayload)
            {
                this.EventNamesRead.Add(eventName);
            }
        }
    }
}
