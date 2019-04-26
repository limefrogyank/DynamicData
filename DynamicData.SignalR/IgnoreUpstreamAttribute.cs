using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.SignalR
{
    // This is an attribute to prevent serialization of properties from the client to the server only.  A typical JsonIgnore would work on both ends
    // if we're sharing the same model class.  We only want the server to be allowed to pass along foreign key classes.  

    // For now, we can't just create a serialization contract to handle this attribute because the javascript client does not allow customization
    // serialization.  This is fine


    [AttributeUsage(AttributeTargets.Field)]
    public class IgnoreUpstreamAttribute : Attribute
    {
        public IgnoreUpstreamAttribute()
        {
        }
    }
}
