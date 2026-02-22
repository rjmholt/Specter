
using PSpecter.Rules;
using System.Runtime.CompilerServices;

[assembly: RuleCollection(Name = "PS")]
[assembly: InternalsVisibleTo("PSpecter.Test")]
[assembly: InternalsVisibleTo("PSpecter.Module")]
[assembly: InternalsVisibleTo("PSpecter.PssaCompatibility")]