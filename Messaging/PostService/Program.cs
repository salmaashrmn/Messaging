using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace PostService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
            };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.ExchangeDeclare("user", ExchangeType.Fanout);
            // Deklarasikan antrean yang sesuai
            string queueName = "user.postservice";
            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            // Bind antrean dengan routing key yang sesuai
            string routingKey = "user.add";
            channel.QueueBind(queue: queueName, exchange: "user", routingKey: routingKey);

            // Buat objek konsumen
            var consumer = new EventingBasicConsumer(channel);

            // Set handler untuk menangani pesan yang diterima
            consumer.Received += (model, ea) =>
            {
                var contextOptions = new DbContextOptionsBuilder<PostServiceContext>()
                    .UseSqlite(@"Data Source=post.db")
                    .Options;
                var dbContext = new PostServiceContext(contextOptions);
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine(" [x] Received {0}", message);
                var data = JObject.Parse(message);
                var type = ea.RoutingKey;
                if (type == "user.add")
                {
                    if (dbContext.User.Any(a => a.ID == data["id"].Value<int>()))
                    {
                        Console.WriteLine("Ignoring old/duplicate entity");
                    }
                    else
                    {
                        dbContext.User.Add(new User()
                        {
                            ID = data["id"].Value<int>(),
                            Name = data["name"].Value<string>(),
                            Version = data["version"].Value<int>()
                        });
                        dbContext.SaveChanges();
                    }
                }
                else if (type == "user.update")
                {
                    int newVersion = data["version"].Value<int>();
                    var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                    if (user.Version >= newVersion)
                    {
                        Console.WriteLine("Ignoring old/duplicate entity");
                    }
                    else
                    {
                        user.Name = data["newname"].Value<string>();
                        user.Version = newVersion;
                        dbContext.SaveChanges();
                    }
                }
                channel.BasicAck(ea.DeliveryTag, false);
            };

            // Mulai mengonsumsi pesan
            channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);
            //ListenForIntegrationEvents();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        public static void ListenForIntegrationEvents()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    Port = 5672,
                    UserName = "guest",
                    Password = "guest",
                };
                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();
                var consumer = new EventingBasicConsumer(channel);

                consumer.Received += (model, ea) =>
                {
                    var contextOptions = new DbContextOptionsBuilder<PostServiceContext>()
                        .UseSqlite(@"Data Source=post.db")
                        .Options;
                    var dbContext = new PostServiceContext(contextOptions);

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine(" [x] Received {0}", message);
                    var data = JObject.Parse(message);
                    var type = ea.RoutingKey;
                    if (type == "user.add")
                    {
                        dbContext.User.Add(new User()
                        {
                            ID = data["id"].Value<int>(),
                            Name = data["name"].Value<string>()
                        });
                        dbContext.SaveChanges();
                    }
                    else if (type == "user.update")
                    {
                        var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                        user.Name = data["newname"].Value<string>();
                        dbContext.SaveChanges();
                    }
                };
                channel.BasicConsume(queue: "user.postservice",
                                         autoAck: true,
                                         consumer: consumer);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }  
        }
    }
}

//using Microsoft.AspNetCore.Hosting;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Hosting;
//using Newtonsoft.Json.Linq;
//using PostService.Data;
//using PostService.Entities;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using System;
//using System.Linq;
//using System.Text;

//namespace PostService
//{
//    public class Program
//    {
//        public static void Main(string[] args)
//        {
//            ListenForIntegrationEvents();
//            CreateHostBuilder(args).Build().Run();
//        }

//        private static void ListenForIntegrationEvents()
//        {
//            var factory = new ConnectionFactory { HostName = "localhost" };
//            using var connection = factory.CreateConnection();
//            using var channel = connection.CreateModel();
//            channel.QueueDeclare(queue: "user.postservice",
//                     durable: true,
//                     exclusive: false,
//                     autoDelete: false,
//                     arguments: null);
//            Console.WriteLine(" [*] Waiting for messages.");
//            var consumer = new EventingBasicConsumer(channel);

//            consumer.Received += (model, ea) =>
//            {
//                var contextOptions = new DbContextOptionsBuilder<PostServiceContext>()
//                    .UseSqlite(@"Data Source=post.db")
//                    .Options;
//                var dbContext = new PostServiceContext(contextOptions);

//                var body = ea.Body.ToArray();
//                var message = Encoding.UTF8.GetString(body);
//                Console.WriteLine(" [x] Received {0}", message);
//                var data = JObject.Parse(message);
//                var type = ea.RoutingKey;
//                if (type == "user.add")
//                {
//                    dbContext.User.Add(new User()
//                    {
//                        ID = data["id"].Value<int>(),
//                        Name = data["name"].Value<string>()
//                    });
//                    dbContext.SaveChanges();
//                }
//                else if (type == "user.update")
//                {
//                    var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
//                    user.Name = data["newname"].Value<string>();
//                    dbContext.SaveChanges();
//                }
//            };
//            channel.BasicConsume(queue: "user.postservice",
//                                     autoAck: true,
//                                     consumer: consumer);
//        }

//        public static IHostBuilder CreateHostBuilder(string[] args) =>
//            Host.CreateDefaultBuilder(args)
//                .ConfigureWebHostDefaults(webBuilder =>
//                {
//                    webBuilder.UseStartup<Startup>();
//                });
//    }
//}