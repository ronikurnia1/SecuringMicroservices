using Azure.Messaging.ServiceBus;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Ordering.Entities;
using GloboTicket.Services.Ordering.Helpers;
using GloboTicket.Services.Ordering.Messages;
using GloboTicket.Services.Ordering.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Messaging
{
    public class AzServiceBusConsumer : IAzServiceBusConsumer
    {
        private readonly string subscriptionName = "globoticketorder";

        private readonly IConfiguration _configuration;

        private readonly OrderRepository _orderRepository;
        private readonly IMessageBus _messageBus;
        private readonly ServiceBusProcessor _serviceBusProcessor;

        private readonly string checkoutMessageTopic;

        private readonly IServiceScopeFactory _scopeFactory;

        public AzServiceBusConsumer(IConfiguration configuration, IMessageBus messageBus, 
            OrderRepository orderRepository, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _orderRepository = orderRepository;
            _scopeFactory = scopeFactory;
            // _logger = logger;
            _messageBus = messageBus;

            var serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);

            checkoutMessageTopic = _configuration.GetValue<string>("CheckoutMessageTopic");

            _serviceBusProcessor = serviceBusClient.CreateProcessor(checkoutMessageTopic, subscriptionName);
        }

        public void Start()
        {
            _serviceBusProcessor.ProcessMessageAsync += MessageHandler;
            _serviceBusProcessor.ProcessErrorAsync += ErrorHandler;

            _serviceBusProcessor.StartProcessingAsync();
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            var body = Encoding.UTF8.GetString(args.Message.Body);//json from service bus

            // Save order with status "not paid"
            BasketCheckoutMessage basketCheckoutMessage = JsonConvert.DeserializeObject<BasketCheckoutMessage>(body);

            using (var scope = _scopeFactory.CreateScope())
            {
                var tokenValidationService = scope.ServiceProvider
                    .GetRequiredService<TokenValidationService>();

                if (!await tokenValidationService.ValidateTokenAsync(
                        basketCheckoutMessage.SecurityContext.AccessToken,
                        args.Message.EnqueuedTime.UtcDateTime))
                {
                    // log, cleanup, ... but don't throw an exception as that will result
                    // in the message not being regarded as handled.  
                    return;
                }
            }

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                UserId = basketCheckoutMessage.UserId,
                Id = orderId,
                OrderPaid = false,
                OrderPlaced = DateTime.Now,
                OrderTotal = basketCheckoutMessage.BasketTotal
            };

            await _orderRepository.AddOrder(order);

            // Trigger payment service by sending a new message.  
            // Functionality not included in demo on purpose.  
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());

            return Task.CompletedTask;
        }

        public void Stop()
        {
            _serviceBusProcessor.StopProcessingAsync();
            _serviceBusProcessor.ProcessMessageAsync -= MessageHandler;
            _serviceBusProcessor.ProcessErrorAsync -= ErrorHandler;
        }
    }
}
