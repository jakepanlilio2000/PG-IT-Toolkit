using System.Collections.Generic;

namespace PuregoldITToolkit.Core.Interfaces
{
    public interface IToolRegistryService
    {
        void RegisterTool(ITool tool);
        IEnumerable<ITool> GetAllTools();
    }
}