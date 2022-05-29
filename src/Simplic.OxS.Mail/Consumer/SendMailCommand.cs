using MassTransit;
using sib_api_v3_sdk.Api;
using Simplic.OxS.Mail.SchemaRegistry;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Simplic.OxS.Mail.Consumer
{
    [MessageBroker.Queue("simplic.oxs.send_mail.command")]
    public class SendMailCommandConsumer : IConsumer<SendMailCommand>
    {
        public async Task Consume(ConsumeContext<SendMailCommand> context)
        {
            var message = context.Message;

            var mailApi = new TransactionalEmailsApi();

            var mailTo = new List<sib_api_v3_sdk.Model.SendSmtpEmailTo>
            {
                new sib_api_v3_sdk.Model.SendSmtpEmailTo(message.MailAddress, message.MailAddress)
            };

            var info = new sib_api_v3_sdk.Model.SendSmtpEmail(to: mailTo, templateId: message.TemplateId, _params: message.Parameter);

            await mailApi.SendTransacEmailAsync(info);
        }
    }
}
