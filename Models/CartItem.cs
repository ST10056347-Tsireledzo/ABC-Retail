﻿using Azure;
using Azure.Data.Tables;

namespace ABC_Retail.Models
{
    public class CartItem:ITableEntity
    {
        public string PartitionKey { get; set; }  // customerEmail.ToLower()
        public string RowKey { get; set; }        // productId
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedOn { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

    }
}
