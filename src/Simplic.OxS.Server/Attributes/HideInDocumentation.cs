﻿namespace Simplic.OxS.Server
{
    /// <summary>
    /// Hide the actual controller or endpoint in the swagger documentation
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class HideInDocumentation : Attribute
    {

    }
}
