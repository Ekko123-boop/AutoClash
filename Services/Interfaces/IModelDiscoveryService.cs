using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface IModelDiscoveryService
    {
        List<ModelSourceNode> DiscoverModels(Document doc);
    }
}
