using PuregoldITToolkit.Core.Base;

namespace PuregoldITToolkit.Core.Interfaces
{
    public interface ITool
    {
        // Metadata for the UI navigation menu
        string ToolName { get; }
        string Description { get; }
        string IconResourceKey { get; } // For loading vector icons from resources

        // The actual logic and data context for the tool
        ViewModelBase ToolViewModel { get; }
    }
}