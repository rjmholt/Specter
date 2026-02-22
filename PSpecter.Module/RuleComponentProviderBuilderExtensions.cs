using System.Management.Automation;
using PSpecter.Builder;
using PSpecter.CommandDatabase;
using PSpecter.Module.CommandDatabase;

namespace PSpecter.Module
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
