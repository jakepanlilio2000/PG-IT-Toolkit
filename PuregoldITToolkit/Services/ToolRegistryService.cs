using PuregoldITToolkit.Core.Interfaces;
using System.Collections.Generic;

namespace PuregoldITToolkit.Services
{
    public class ToolRegistryService : IToolRegistryService
    {
        private readonly List<ITool> _tools = new List<ITool>();

        public void RegisterTool(ITool tool)
        {
            if (tool != null && !_tools.Contains(tool))
            {
                _tools.Add(tool);
            }
        }

        public IEnumerable<ITool> GetAllTools()
        {
            return _tools.AsReadOnly();
        }
    }
}