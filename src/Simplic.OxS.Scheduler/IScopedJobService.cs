﻿using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Scheduler
{
    public interface IScopedJobService
    {
        void Execute();
    }
}