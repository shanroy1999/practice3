using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FunctionAppDemo1
{
    class MessageContent
    {
        public string MessageId { get; set; }
        public string Content { get; set; }

        public override string ToString()
        {
            // Get Json String representation of properties of object
            return JsonSerializer.Serialize(this);
        }
    }
}
