using MassTransit;
using Simplix.OxS.Sms.SchemaRegistry;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Simplic.OxS.Sms.Consumer
{
    [MessageBroker.Queue("simplic.oxs.send_sms.command")]
    public class SendMailCommand : IConsumer<SendSmsCommand>
    {
        public async Task Consume(ConsumeContext<SendSmsCommand> context)
        {
            var message = context.Message;

            string accountSid = "";
            string authToken = "";

            TwilioClient.Init(accountSid, authToken);

            // TODO: Send message using template
            MessageResource.Create(
                body: $"Your Simplic.OxS code: {message.Parameter["Code"]}",
                from: new Twilio.Types.PhoneNumber("+13365022471"),
                to: new Twilio.Types.PhoneNumber(message.PhoneNumber)
            );
        }
    }
}
