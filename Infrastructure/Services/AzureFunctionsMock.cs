using System;

namespace Microsoft.Azure.Functions.Worker
{
    [AttributeUsage(AttributeTargets.Method)]
    public class FunctionAttribute : Attribute
    {
        public FunctionAttribute(string name) { }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class TimerTriggerAttribute : Attribute
    {
        public TimerTriggerAttribute(string schedule) { }
    }

    public class TimerInfo
    {
    }
}
