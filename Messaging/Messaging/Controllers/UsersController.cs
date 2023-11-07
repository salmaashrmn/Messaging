using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserService.Data;
using UserService.Entities;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserServiceContext _context;
        private readonly IntegrationEventSenderService _integrationEventSenderService;

        public UsersController(UserServiceContext context, IntegrationEventSenderService integrationEventSenderService)
        {
            _context = context;
            _integrationEventSenderService = integrationEventSenderService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.User.ToListAsync();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            using var transaction = _context.Database.BeginTransaction();

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                newname = user.Name,
            });
            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.update",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();
            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            user.Version = 1;
            using var transaction = _context.Database.BeginTransaction();
            _context.User.Add(user);
            _context.SaveChanges();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name,
                version = user.Version
            });

            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.add",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();

            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using UserService.Data;
//using UserService.Entities;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Newtonsoft.Json;
//using RabbitMQ.Client;
//using System.Text;

//namespace UserService.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class UsersController : ControllerBase
//    {
//        private readonly UserServiceContext _context;

//        public UsersController(UserServiceContext context)
//        {
//            _context = context;
//        }

//        [HttpGet]
//        public async Task<ActionResult<IEnumerable<User>>> GetUser()
//        {
//            return await _context.User.ToListAsync();
//        }


//        private void PublishToMessageQueue(string routingKey, string messageBody)
//        {
//            var factory = new ConnectionFactory
//            {
//                HostName = "localhost",
//                Port = 5672,
//                UserName = "guest",
//                Password = "guest",
//            };
//            using (var connection = factory.CreateConnection())
//            using (var channel = connection.CreateModel())
//            {
//                // Deklarasikan pertukaran dengan tipe yang sesuai (direct, fanout, dll)
//                channel.ExchangeDeclare("user", ExchangeType.Fanout);

//                // Konversi pesan ke bentuk byte array
//                var body = Encoding.UTF8.GetBytes(messageBody);

//                // Kirim pesan ke pertukaran dengan routing key yang sesuai
//                channel.BasicPublish("user", routingKey, null, body);
//            }
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> PutUser(int id, User user)
//        {
//            _context.Entry(user).State = EntityState.Modified;
//            await _context.SaveChangesAsync();

//            var integrationEventData = JsonConvert.SerializeObject(new
//            {
//                id = user.ID,
//                newname = user.Name
//            });
//            PublishToMessageQueue("user.update", integrationEventData);

//            return NoContent();
//        }

//        [HttpPost]
//        public async Task<ActionResult<User>> PostUser(User user)
//        {
//            _context.User.Add(user);
//            await _context.SaveChangesAsync();

//            var integrationEventData = JsonConvert.SerializeObject(new
//            {
//                id = user.ID,
//                name = user.Name
//            });
//            PublishToMessageQueue("user.add", integrationEventData);

//            return CreatedAtAction("GetUser", new { id = user.ID }, user);
//        }
//    }
//}
