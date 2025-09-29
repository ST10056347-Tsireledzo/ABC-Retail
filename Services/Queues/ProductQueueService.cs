﻿using ABC_Retail.Models.DTOs;
using Azure.Storage.Queues;
using System.Text;
using System.Text.Json;

namespace ABC_Retail.Services.Queues
{
    public class ProductQueueService
    {
        private readonly QueueClient _queueClient;

        public ProductQueueService(string? connectionString, string queueName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Storage connection string is missing.");

            _queueClient = new QueueClient(connectionString, queueName);
            _queueClient.CreateIfNotExists(); // Ensures queue exists
        }

        public async Task EnqueueProductChangeAsync(ProductChangeMessageDto message)
        {
            var json = JsonSerializer.Serialize(message);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await _queueClient.SendMessageAsync(base64);
        }

    }
}
