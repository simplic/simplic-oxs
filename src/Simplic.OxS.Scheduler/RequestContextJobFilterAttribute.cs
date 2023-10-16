using Hangfire.Client;
using Hangfire.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Scheduler
{
    public class RequestContextJobFilterAttribute : JobFilterAttribute, IClientFilter
    {
        public void OnCreating(CreatingContext context)
        {
            var argue = context.Job.Args.FirstOrDefault(x => x is IJobWithRequestContext);
            if (argue == null)
                throw new Exception($"This job does not implement the {nameof(IJobWithRequestContext)} interface");

            var jobParameters = argue as IJobWithRequestContext;
            var requestContext = jobParameters?.ReqeustContext;

            if (requestContext == null)
                throw new Exception($"Request context could not be set in job definition");

            context.SetJobParameter("RequestContext", requestContext);
        }

        public void OnCreated(CreatedContext context)
        {
        }
    }
}