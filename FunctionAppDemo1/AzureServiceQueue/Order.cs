using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AzureServiceQueue
{
    class Order
    {
        // Properties of the Order Class
        public string orderID
        {
            get;
            set;
        }

        public int quantity
        {
            get;
            set;
        }

        public decimal unitPrice
        {
            get;
            set;
        }

        // Override the ToString method
        public override string ToString()
        {
            // get a Json String representation of the object
            return JsonSerializer.Serialize(this);
        }
    }
}
