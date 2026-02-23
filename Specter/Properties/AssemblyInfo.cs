
using Specter.Rules;
using System.Runtime.CompilerServices;

[assembly: RuleCollection(Name = "PS")]
[assembly: InternalsVisibleTo("Specter.Test")]
[assembly: InternalsVisibleTo("Specter.Module")]
[assembly: InternalsVisibleTo("Specter.PssaCompatibility")]