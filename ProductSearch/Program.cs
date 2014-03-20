using System.Linq;
using System.Web;
using Drl.ProductService.Interface.Events;
using Everest;
using MassTransit;
using MassTransit.Log4NetIntegration;
using Topshelf;

namespace ProductSearch
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<ProductLoader>(s =>
                {
                    s.ConstructUsing(() => new ProductLoader());
                    s.WhenStarted((service, control) => service.Start(control));
                    s.WhenStopped((service, control) =>
                    {
                        service.Stop(control);
                        return true;
                    });
                });
                x.SetDescription("ProductLoader");
                x.SetDisplayName("ProductLoader");
                x.SetServiceName("ProductLoader");
            });
        }
    }

    internal class ProductLoader : ServiceControl
    {
        private IServiceBus _bus;

        public bool Start(HostControl hostControl)
        {
            _bus = CreateServiceBus();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _bus.Dispose();
            return true;
        }

        private IServiceBus CreateServiceBus()
        {
            return ServiceBusFactory.New(sbc =>
            {
                sbc.UseLog4Net("log4net.config");
                sbc.UseRabbitMq();
                sbc.UseJsonSerializer();
                sbc.ReceiveFrom("rabbitmq://localhost/productsearch");
                sbc.Subscribe(subs => subs.Handler<ProductChangedEvent>(@event =>
                    {
                        var product = @event.Products[0];
                        var restClient = new RestClient();
                        restClient.Put(string.Format("http://localhost:9200//ao_com//product//{0}", product.Code),
                                       GetSearchableProduct(product));
                    }));
            });
        }

        private static string GetSearchableProduct(Product product)
        {
            return string.Format(@"{{""title"" : ""{0} {1}"", ""description"" : ""{2}"" }}", product.Brand,
                                 product.ApplianceType, Description(product));
        }

        private static string Description(Product product)
        {
            var description = product.HtmlDescriptions.FirstOrDefault(d => d.ClientId == 1);
            return description != null ? HttpUtility.HtmlEncode(description.Description) : string.Empty;
        }
    }
}
