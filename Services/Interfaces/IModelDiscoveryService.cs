using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface IModelDiscoveryService
    {
        List<ModelSourceNode> DiscoverModels(Document doc);
        List<ModelItem> GetSiblingNwcs(Document doc, ModelSourceNode targetNwc);
    }
}
