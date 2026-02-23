using System.Management.Automation;
using Specter.Builder;
using Specter.CommandDatabase;
using Specter.Module.CommandDatabase;

namespace Specter.Module
{
    public static class RuleComponentProviderBuilderExtensions
    {
        public static RuleComponentProviderBuilder UseSessionDatabase(
            this RuleComponentProviderBuilder builder,
            CommandInvocationIntrinsics invokeCommand)
        {
            return builder.AddSingleton<IPowerShellCommandDatabase>(
                SessionStateCommandDatabase.Create(invokeCommand));
        }
    }
}
